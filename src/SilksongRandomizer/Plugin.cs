using BepInEx;
using BepInEx.Logging;
using Archipelago.MultiClient.Net.Enums;
using BepInEx.Configuration;
using HarmonyLib;
using SilksongRandomizer.Patches;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SilksongRandomizer
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class RandomizerPlugin : BaseUnityPlugin
    {
        public static RandomizerPlugin Instance { get; private set; }
        public const string PluginGuid = "moriko.silksong.randomizer";
        public const string PluginName = "Randomizer";
        public const string PluginVersion = "0.4.4";

        public static bool OverrideUnlock { get; set; } = true;

        internal static ManualLogSource Log;

        private const float ConnectionWindowWidth = 460f;
        private const float ConnectionWindowHeight = 350f;
        private const float HintsWindowWidth = 520f;
        private const float HintsWindowHeight = 430f;

        private enum ArchipelagoWindowTab
        {
            Connection,
            Hints,
        }

        private bool showConnectionGui;
        private bool connectionGuiDismissed;
        private bool cursorStateCaptured;
        private bool previousCursorVisible;
        private bool isConnecting;
        private CursorLockMode previousCursorLockState;
        private Rect connectionWindowRect = new Rect(0f, 0f, ConnectionWindowWidth, ConnectionWindowHeight);
        private string connectionHost = "localhost";
        private string connectionPort = "38281";
        private string connectionSlot = string.Empty;
        private string connectionPassword = string.Empty;
        private ConfigEntry<string> savedConnectionHost;
        private ConfigEntry<string> savedConnectionPort;
        private ConfigEntry<string> savedConnectionSlot;
        private ConfigEntry<int> mapMarkerTooltipFontSize;
        private string connectionStatus = string.Empty;
        private ArchipelagoWindowTab selectedWindowTab =
            ArchipelagoWindowTab.Connection;
        private Vector2 hintsScrollPosition;
        private volatile bool reopenConnectionGuiRequested;
        private volatile bool resetTransientEffectsRequested;
        private Archipelago subscribedArchipelago;

        private struct QueuedReceivedItem
        {
            public int Index;
            public string Name;
            public ItemFlags Flags;
        }

        private struct QueuedUnlockPopup
        {
            public string Text;
            public ItemFlags Flags;
        }

        private readonly object unlockQueueLock = new object();
        private readonly Queue<QueuedUnlockPopup> unlockQueue =
            new Queue<QueuedUnlockPopup>();
        private readonly object receivedItemQueueLock = new object();
        private readonly Queue<QueuedReceivedItem> receivedItemQueue = new Queue<QueuedReceivedItem>();
        private readonly object connectionStatusQueueLock = new object();
        private readonly Queue<string> connectionStatusQueue = new Queue<string>();
        private float delay = 0f;
        private float nextItemRetryTime;
        private const int MaxBootstrapNoOpItemsPerFrame = 8;

        public Sprite ArchipelagoIcon { get; private set; }
        public Sprite FillerIcon { get; private set; }
        public Sprite UsefulIcon { get; private set; }
        public Sprite ProgressionIcon { get; private set; }
        public Sprite TrapIcon { get; private set; }
        public Sprite MapCheckIcon { get; private set; }
        public Sprite MapCheckOutlineIcon { get; private set; }
        public Sprite LogicUnknownIcon { get; private set; }
        public Sprite LogicUnknownOutlineIcon { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            LoadSavedConnectionDetails();
            LoadClientDisplaySettings();
            DeathLinkManager.Initialize(QueueConnectionStatus);
            SilkLinkManager.Initialize(QueueConnectionStatus);
            CurrencyLinkManager.Initialize(QueueConnectionStatus);
            LoadArchipelagoIcon();
            LoadCheckClassificationIcons();
            try
            {
                Harmony h = new Harmony(PluginGuid);
                PatchAssemblySafely(h);
                Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
                DumpPatchState(h);
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to patch: {e}");
            }
        }

        private void LoadSavedConnectionDetails()
        {
            savedConnectionHost = Config.Bind(
                "Archipelago Connection",
                "Host",
                "localhost",
                "Host used by the last successful Archipelago connection."
            );
            savedConnectionPort = Config.Bind(
                "Archipelago Connection",
                "Port",
                "38281",
                "Port used by the last successful Archipelago connection."
            );
            savedConnectionSlot = Config.Bind(
                "Archipelago Connection",
                "Slot",
                string.Empty,
                "Slot used by the last successful Archipelago connection."
            );

            connectionHost = savedConnectionHost.Value ?? "localhost";
            connectionPort = savedConnectionPort.Value ?? "38281";
            connectionSlot = savedConnectionSlot.Value ?? string.Empty;
        }

        private void LoadClientDisplaySettings()
        {
            mapMarkerTooltipFontSize = Config.Bind(
                "Map Markers",
                "Tooltip Font Size",
                14,
                new ConfigDescription(
                    "Text size used for check names shown while focusing a " +
                    "map marker.",
                    new AcceptableValueRange<int>(10, 32)
                )
            );
        }

        internal int MapMarkerTooltipFontSize =>
            mapMarkerTooltipFontSize == null
                ? 14
                : Mathf.Clamp(mapMarkerTooltipFontSize.Value, 10, 32);

        private void SaveSuccessfulConnectionDetails(
            string host,
            int port,
            string slot
        )
        {
            connectionHost = host;
            connectionPort = port.ToString(CultureInfo.InvariantCulture);
            connectionSlot = slot;
            savedConnectionHost.Value = connectionHost;
            savedConnectionPort.Value = connectionPort;
            savedConnectionSlot.Value = connectionSlot;
            Config.Save();
        }

        private void LoadArchipelagoIcon()
        {
            string path = GetPluginAssetPath("ArchipelagoIcon.png");

            if (!File.Exists(path))
            {
                ArchipelagoIcon = CreateFallbackIcon(new Color(0.16f, 0.62f, 0.90f, 1f));
                Log.LogInfo("[RANDOMIZER] ArchipelagoIcon.png not found; using a generated fallback icon.");
                return;
            }

            try
            {
                byte[] data = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (!texture.LoadImage(data))
                {
                    ArchipelagoIcon = CreateFallbackIcon(new Color(0.16f, 0.62f, 0.90f, 1f));
                    Log.LogWarning("[RANDOMIZER] Failed to load ArchipelagoIcon.png; using a generated fallback icon.");
                    return;
                }

                ArchipelagoIcon = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );
                ArchipelagoIcon.name = "archipelagoicon";
            }
            catch (Exception ex)
            {
                ArchipelagoIcon = CreateFallbackIcon(new Color(0.16f, 0.62f, 0.90f, 1f));
                Log.LogWarning("[RANDOMIZER] Failed to load ArchipelagoIcon.png; using a generated fallback icon: " + ex);
            }
        }

        private static void PatchAssemblySafely(Harmony harmony)
        {
            Type[] patchTypes = AccessTools.GetTypesFromAssembly(
                    Assembly.GetExecutingAssembly()
                )
                .Where(type => type.GetCustomAttributes(
                    typeof(HarmonyPatch),
                    true
                ).Length > 0)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            int patchedTypeCount = 0;
            int failedTypeCount = 0;
            foreach (Type patchType in patchTypes)
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    patchedTypeCount++;
                }
                catch (Exception ex)
                {
                    failedTypeCount++;
                    Log?.LogError(
                        "[RANDOMIZER] Harmony patch group failed: " +
                        patchType.FullName + ": " + ex
                    );
                }
            }

            if (failedTypeCount > 0)
            {
                Log?.LogError(
                    "[RANDOMIZER] Loaded " + patchedTypeCount +
                    " Harmony patch groups, but " + failedTypeCount +
                    " failed. Features from the failed groups are disabled; " +
                    "remaining patch groups were still applied."
                );
            }
            else
            {
                Log?.LogInfo(
                    "[RANDOMIZER] Loaded all " + patchedTypeCount +
                    " Harmony patch groups."
                );
            }
        }

        private void LoadCheckClassificationIcons()
        {
            FillerIcon = LoadCheckClassificationIcon(
                "Filler.png",
                "filler",
                ArchipelagoIcon
            );
            UsefulIcon = LoadCheckClassificationIcon(
                "Useful.png",
                "useful",
                ArchipelagoIcon
            );
            ProgressionIcon = LoadCheckClassificationIcon(
                "Progression.png",
                "progression",
                ArchipelagoIcon
            );
            TrapIcon = LoadCheckClassificationIcon(
                "Trap.png",
                "trap",
                ArchipelagoIcon
            );
            MapCheckIcon = LoadCheckClassificationIcon(
                "MapCheck.png",
                "mapcheck",
                ArchipelagoIcon
            );
            LogicUnknownIcon = LoadCheckClassificationIcon(
                "LogicUnknown.png",
                "logicunknown",
                MapCheckIcon ?? ArchipelagoIcon
            );
            MapCheckOutlineIcon =
                CreateWhiteOutlineIcon(MapCheckIcon);
            LogicUnknownOutlineIcon =
                CreateWhiteOutlineIcon(LogicUnknownIcon);
        }

        private Sprite LoadCheckClassificationIcon(
            string fileName,
            string spriteName,
            Sprite fallback)
        {
            string relativePath = Path.Combine("CheckIcons", fileName);
            string path = GetPluginAssetPath(relativePath);
            if (!File.Exists(path))
            {
                Log.LogWarning(
                    "[RANDOMIZER] " + relativePath +
                    " not found; using the generic AP icon."
                );
                return fallback;
            }

            try
            {
                byte[] data = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false
                );
                if (!texture.LoadImage(data))
                {
                    Log.LogWarning(
                        "[RANDOMIZER] Failed to load " + relativePath +
                        "; using the generic AP icon."
                    );
                    return fallback;
                }

                texture.filterMode = FilterMode.Point;
                var createdSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );
                createdSprite.name = spriteName;
                return createdSprite;
            }
            catch (Exception ex)
            {
                Log.LogWarning(
                    "[RANDOMIZER] Failed to load " + relativePath +
                    "; using the generic AP icon: " + ex
                );
                return fallback;
            }
        }

        public Sprite GetItemClassificationIcon(ItemFlags flags)
        {
            if ((flags & ItemFlags.Advancement) != 0)
            {
                return ProgressionIcon ?? ArchipelagoIcon;
            }

            if ((flags & ItemFlags.NeverExclude) != 0)
            {
                return UsefulIcon ?? ArchipelagoIcon;
            }

            if ((flags & ItemFlags.Trap) != 0)
            {
                return TrapIcon ?? ArchipelagoIcon;
            }

            return flags == ItemFlags.None
                ? FillerIcon ?? ArchipelagoIcon
                : ArchipelagoIcon;
        }

        private static Sprite CreateWhiteOutlineIcon(Sprite source)
        {
            if (source == null || source.texture == null)
            {
                return null;
            }

            try
            {
                Rect sourceRect = source.rect;
                int sourceX = Mathf.RoundToInt(sourceRect.x);
                int sourceY = Mathf.RoundToInt(sourceRect.y);
                int width = Mathf.RoundToInt(sourceRect.width);
                int height = Mathf.RoundToInt(sourceRect.height);
                Color[] sourcePixels = source.texture.GetPixels(
                    sourceX,
                    sourceY,
                    width,
                    height
                );
                Color[] outlinePixels = new Color[sourcePixels.Length];
                int radius = Mathf.Max(
                    1,
                    Mathf.RoundToInt(Mathf.Max(width, height) / 24f)
                );

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width) + x;
                        if (sourcePixels[index].a > 0.05f)
                        {
                            outlinePixels[index] = Color.clear;
                            continue;
                        }

                        float alpha = 0f;
                        for (
                            int offsetY = -radius;
                            offsetY <= radius && alpha <= 0.05f;
                            offsetY++
                        )
                        {
                            int sampleY = y + offsetY;
                            if (sampleY < 0 || sampleY >= height)
                            {
                                continue;
                            }
                            for (
                                int offsetX = -radius;
                                offsetX <= radius;
                                offsetX++
                            )
                            {
                                int sampleX = x + offsetX;
                                if (sampleX < 0 || sampleX >= width)
                                {
                                    continue;
                                }
                                alpha = Mathf.Max(
                                    alpha,
                                    sourcePixels[
                                        (sampleY * width) + sampleX
                                    ].a
                                );
                            }
                        }
                        outlinePixels[index] = new Color(
                            1f,
                            1f,
                            1f,
                            alpha
                        );
                    }
                }

                Texture2D texture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false
                );
                texture.filterMode = FilterMode.Point;
                texture.SetPixels(outlinePixels);
                texture.Apply();

                var createdSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, width, height),
                    source.pivot / sourceRect.size,
                    source.pixelsPerUnit
                );
                createdSprite.name = source.name + " outline";
                return createdSprite;
            }
            catch (Exception ex)
            {
                Log?.LogWarning(
                    "[RANDOMIZER] Could not generate the white map-check " +
                    "hint outline: " + ex.Message
                );
                return null;
            }
        }

        private static string GetPluginAssetPath(string fileName)
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(RandomizerPlugin).Assembly.Location);
            return Path.Combine(
                string.IsNullOrEmpty(assemblyDirectory) ? Paths.PluginPath : assemblyDirectory,
                fileName
            );
        }

        private static Sprite CreateFallbackIcon(Color color)
        {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radiusSquared = 42.25f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    pixels[(y * size) + x] = ((dx * dx) + (dy * dy)) <= radiusSquared
                        ? color
                        : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            var createdSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f)
            );
            createdSprite.name = "archipelagoicon";
            return createdSprite;
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(5.0f);
            GetOrCreateArchipelago();

            while ((Archipelago.Instance == null || !Archipelago.Instance.Connected) && !connectionGuiDismissed)
            {
                ShowConnectionGui();
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(1.0f);
            }
            Time.timeScale = 1f;
            HideConnectionGui(true);

            while (true)
            {
                SaveState activeState = SaveState.Instance;
                if (activeState != null)
                {
                    foreach (Location location in activeState.locations.Locations)
                    {
                        if (!ReferenceEquals(activeState, SaveState.Instance))
                        {
                            break;
                        }

                        try
                        {
                            if (location.Check != null &&
                                activeState.IsRandomized(location.Type) &&
                                activeState.IsLocationInSeed(location.Name) &&
                                !activeState.checkedLocations.Contains(location.Name) &&
                                location.Check())
                            {
                                activeState.CheckLocation(location.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.LogWarning(
                                "[RANDOMIZER] Location check failed for " +
                                location.Name + ": " + ex.Message
                            );
                        }

                        yield return null;
                    }
                }
                yield return null;
            }
        }

        void Update()
        {
            if (resetTransientEffectsRequested)
            {
                resetTransientEffectsRequested = false;
                TrapManager.ResetTransientEffects();
            }

            TrapManager.Update();
            SlabCaptureWarpSafety.Update();
            MossMotherWarpSafety.Update();
            BellhomePhaseManager.Update();
            DeathLinkManager.Update();
            SilkLinkManager.Update();
            CurrencyLinkManager.Update();
            FleaRescueAudio.Update();
            FleaPatches.Update();
            VogHintManager.Update();
            WandererChapelPatches.Update();
            MinorCachePatches.UpdateInactiveCurrencyChestFallbacks();
            LoreTabletPatches.UpdateInactiveSources();
            ProcessConnectionStatusQueue();
            ProcessQueuedReceivedItems();
            ProcessQueuedUnlockPopups();
            ShopPatches.ProcessQueuedHintRefreshes();

            if (reopenConnectionGuiRequested)
            {
                reopenConnectionGuiRequested = false;
                selectedWindowTab = ArchipelagoWindowTab.Connection;
                ShowConnectionGui();
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                if (showConnectionGui)
                {
                    connectionGuiDismissed = true;
                    HideConnectionGui(true);
                }
                else
                {
                    connectionGuiDismissed = false;
                    ShowConnectionGui();
                }
            }

            if (!showConnectionGui && Input.GetKeyDown(KeyCode.F4))
            {
                TryWarpToPreferredHub();
            }

        }

        private void OnDisable()
        {
            TrapManager.ResetTransientEffects();
            DeathLinkManager.Reset();
            SilkLinkManager.Reset();
            CurrencyLinkManager.Reset();
            FleaRescueAudio.ResetPending();
            VogHintManager.Reset();
            MinorCachePatches.ResetInactiveCurrencyChestFallbacks();
            LoreTabletPatches.ResetInactiveSources();
        }

        private Archipelago GetOrCreateArchipelago()
        {
            Archipelago archipelago = Archipelago.Instance ?? new Archipelago();
            if (!ReferenceEquals(subscribedArchipelago, archipelago))
            {
                if (subscribedArchipelago != null)
                {
                    subscribedArchipelago.ConnectionStatusChanged -= QueueConnectionStatus;
                    subscribedArchipelago.OnItemSent -= QueueUnlockPopup;
                }

                subscribedArchipelago = archipelago;
                subscribedArchipelago.ConnectionStatusChanged += QueueConnectionStatus;
                subscribedArchipelago.OnItemSent += QueueUnlockPopup;
            }

            return archipelago;
        }

        private void QueueConnectionStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            lock (connectionStatusQueueLock)
            {
                connectionStatusQueue.Enqueue(status);
            }

            if (status.StartsWith("Disconnected", StringComparison.OrdinalIgnoreCase) ||
                status.StartsWith("Archipelago network error", StringComparison.OrdinalIgnoreCase))
            {
                reopenConnectionGuiRequested = true;
                resetTransientEffectsRequested = true;
            }
        }

        private void ProcessConnectionStatusQueue()
        {
            lock (connectionStatusQueueLock)
            {
                while (connectionStatusQueue.Count > 0)
                {
                    connectionStatus = connectionStatusQueue.Dequeue();
                }
            }
        }

        public void ReportBlockingError(string message)
        {
            QueueConnectionStatus(string.IsNullOrWhiteSpace(message) ? "Randomizer error." : message);
            connectionGuiDismissed = false;
            reopenConnectionGuiRequested = true;
            Log.LogError("[RANDOMIZER] " + message);
        }

        public void ClearPendingGameplayQueues()
        {
            TrapManager.ResetTransientEffects();
            VogHintManager.Reset();

            lock (receivedItemQueueLock)
            {
                receivedItemQueue.Clear();
            }

            lock (unlockQueueLock)
            {
                unlockQueue.Clear();
                delay = 0f;
            }

            nextItemRetryTime = 0f;
        }

        public void QueueReceivedItem(
            int itemIndex,
            string itemName,
            ItemFlags flags)
        {
            if (itemIndex < 0 || string.IsNullOrWhiteSpace(itemName))
            {
                return;
            }

            lock (receivedItemQueueLock)
            {
                receivedItemQueue.Enqueue(new QueuedReceivedItem
                {
                    Index = itemIndex,
                    Name = itemName,
                    Flags = flags
                });
            }
        }

        public void QueueUnlockPopup(string text, ItemFlags flags)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            lock (unlockQueueLock)
            {
                unlockQueue.Enqueue(new QueuedUnlockPopup
                {
                    Text = text,
                    Flags = flags
                });
                delay = 0.25f;
            }
        }

        private void ProcessQueuedReceivedItems()
        {
            for (int index = 0; index < MaxBootstrapNoOpItemsPerFrame; index++)
            {
                if (!ProcessQueuedReceivedItem())
                {
                    break;
                }
            }
        }

        // Returns true only after consuming a guaranteed start-with-maps
        // packet whose native state was applied by the one-time bootstrap.
        // That permits a bounded number of these bookkeeping-only packets to
        // drain in one frame without changing ordinary receipt pacing.
        private bool ProcessQueuedReceivedItem()
        {
            SaveState saveState = SaveState.Instance;
            if (saveState == null)
            {
                return false;
            }

            TryBootstrapStartWithMaps(saveState);
            if (Time.unscaledTime < nextItemRetryTime)
            {
                return false;
            }

            QueuedReceivedItem queuedItem;

            lock (receivedItemQueueLock)
            {
                if (receivedItemQueue.Count <= 0)
                {
                    return false;
                }

                queuedItem = receivedItemQueue.Peek();
            }

            if (queuedItem.Index < saveState.receivedItemIndex)
            {
                lock (receivedItemQueueLock)
                {
                    receivedItemQueue.Dequeue();
                }
                return saveState.IsStartWithMapsBootstrapItem(
                    queuedItem.Name
                );
            }

            if (queuedItem.Index > saveState.receivedItemIndex)
            {
                Log.LogWarning(
                    "[RANDOMIZER] AP item queue skipped index " +
                    saveState.receivedItemIndex + "; rebuilding the queue."
                );

                lock (receivedItemQueueLock)
                {
                    receivedItemQueue.Clear();
                }

                if (Archipelago.Instance != null && Archipelago.Instance.Connected)
                {
                    Archipelago.Instance.ResetReceivedItemQueueCursor();
                    Archipelago.Instance.Resynchronize();
                }

                nextItemRetryTime = Time.unscaledTime + 0.25f;
                return false;
            }

            Item item = saveState.GetItem(queuedItem.Name);
            string canonicalItemName = item == null ? queuedItem.Name : item.Name;
            bool alreadyReceived = saveState.receivedItems.Contains(canonicalItemName) &&
                                   (item == null || !item.Repeatable);

            if (item == null && !saveState.receivedItems.Contains(canonicalItemName))
            {
                ReportBlockingError("AP sent unknown item '" + queuedItem.Name + "'.");
                nextItemRetryTime = Time.unscaledTime + 5f;
                return false;
            }

            bool requiresPostCommitMaskSync =
                !alreadyReceived &&
                item != null &&
                item.Type == ItemType.MaskShard;
            bool requiresPostCommitConsumableSync =
                !alreadyReceived &&
                ConsumableToolPatches.RequiresInitialFill(
                    saveState,
                    canonicalItemName
                );

            if (!alreadyReceived &&
                (item.Receive != null ||
                 requiresPostCommitMaskSync ||
                 requiresPostCommitConsumableSync))
            {
                PlayerData currentPlayerData = PlayerData.instance;
                HeroController currentHero = HeroController.instance;
                bool durableMemoryReceiptReady =
                    MemorySequenceSync.CanApplyDurableReceipt(
                        currentPlayerData,
                        currentHero,
                        item
                    );
                bool gameplayReady =
                    currentPlayerData != null &&
                    (item.Type == ItemType.Crest
                        ? !currentPlayerData.HasStoredMemoryState &&
                          !SlabCaptureWarpSafety.IsActiveSlabCaptureCrest(
                              currentPlayerData) &&
                          StartingCrestFix.IsCrestRuntimeReady()
                        : currentHero != null &&
                          (currentHero.CanAttack() ||
                           NakedTrapManager.CanProcessReceivedItems(
                               currentHero) ||
                           durableMemoryReceiptReady));
                if (!gameplayReady)
                {
                    return false;
                }

                try
                {
                    item.Receive?.Invoke();
                }
                catch (Exception ex)
                {
                    nextItemRetryTime = Time.unscaledTime + 1f;
                    Log.LogError(
                        "[RANDOMIZER] Failed to apply item " + queuedItem.Name +
                        " at index " + queuedItem.Index + "; it will be retried: " + ex
                    );
                    return false;
                }
            }

            try
            {
                bool newlyReceived = saveState.CommitReceivedItemAtIndex(
                    queuedItem.Index,
                    canonicalItemName,
                    item != null && item.Repeatable
                );

                if (newlyReceived &&
                    item != null &&
                    item.Type == ItemType.MaskShard &&
                    !MaskShardsPatches.SynchronizeReceivedMaskShards(true))
                {
                    Log.LogError(
                        "[RANDOMIZER] Mask Shard was recorded, but its native " +
                        "health state was not ready. It will be reconciled on load."
                    );
                }

                if (newlyReceived &&
                    requiresPostCommitConsumableSync &&
                    !ConsumableToolPatches.TryInitializeReceivedConsumable(
                        saveState,
                        canonicalItemName
                    ))
                {
                    StartCoroutine(
                        ConsumableToolPatches
                            .SynchronizeReceivedConsumables(saveState)
                    );
                }

                // Spool Fragment items have no native Receive
                // action. Their capacity is derived from receivedItems, so
                // redraw only after CommitReceivedItemAtIndex has made the
                // new fragment visible to CurrentSilkMaxBasic.
                if (newlyReceived &&
                    item != null &&
                    item.Type == ItemType.SpoolFragment)
                {
                    SpoolFragmentPatches.RefreshReceivedSpoolHud();
                }

                // Play the receipt cue for every AP Flea packet, including a
                // duplicate manually sent for testing.  The packet is still
                // consumed normally. This only affects the client feedback.
                if (item != null && item.Type == ItemType.Flea)
                {
                    FleaRescueAudio.QueueForReceivedFlea();
                }

                lock (receivedItemQueueLock)
                {
                    receivedItemQueue.Dequeue();
                }

                if (newlyReceived)
                {
                    QueueUnlockPopup(canonicalItemName, queuedItem.Flags);
                }

                return alreadyReceived &&
                       saveState.IsStartWithMapsBootstrapItem(
                           canonicalItemName
                       );
            }
            catch (Exception ex)
            {
                nextItemRetryTime = Time.unscaledTime + 1f;
                Log.LogError(
                    "[RANDOMIZER] Failed to commit item " + queuedItem.Name +
                    " at index " + queuedItem.Index + "; it will be retried: " + ex
                );
                return false;
            }
        }

        private void TryBootstrapStartWithMaps(SaveState saveState)
        {
            TryApplyStartFullyMapped(saveState);
            if (saveState == null ||
                !saveState.NeedsStartWithMapsBootstrap())
            {
                return;
            }

            PlayerData playerData = PlayerData.instance;
            HeroController hero = HeroController.instance;
            if (playerData == null ||
                playerData.HasStoredMemoryState ||
                hero == null ||
                !hero.CanAttack())
            {
                return;
            }

            try
            {
                ItemGrants.GrantStartWithMaps();
                saveState.RecordStartWithMapsBootstrapItems();
            }
            catch (Exception ex)
            {
                nextItemRetryTime = Time.unscaledTime + 1f;
                Log.LogError(
                    "[RANDOMIZER] Failed to apply the starting maps; " +
                    "they will be retried: " + ex
                );
            }
        }

        private void TryApplyStartFullyMapped(SaveState saveState)
        {
            PlayerData playerData = PlayerData.instance;
            if (saveState == null ||
                !saveState.startFullyMapped ||
                playerData == null ||
                playerData.mapAllRooms)
            {
                return;
            }

            try
            {
                ItemGrants.GrantStartFullyMapped();
            }
            catch (Exception ex)
            {
                nextItemRetryTime = Time.unscaledTime + 1f;
                Log.LogError(
                    "[RANDOMIZER] Failed to draw the starting map. " +
                    "It will be retried: " + ex
                );
            }
        }

        private void ProcessQueuedUnlockPopups()
        {
            if (SaveState.Instance == null || HeroController.instance == null || !HeroController.instance.CanAttack())
            {
                return;
            }

            QueuedUnlockPopup popup = default(QueuedUnlockPopup);

            lock (unlockQueueLock)
            {
                if (delay > 0f)
                {
                    delay -= Time.deltaTime;
                    return;
                }

                if (unlockQueue.Count <= 0)
                {
                    return;
                }

                popup = unlockQueue.Dequeue();
            }

            CollectableUIMsg.Spawn(new UIMsgDisplay
            {
                Name = popup.Text,
                Icon = GetItemClassificationIcon(popup.Flags),
                IconScale = 1.0f,
                RepresentingObject = null,
            }, null, false);
        }

        private void OnGUI()
        {
            CheckMapMarkerManager.DrawTooltip();

            if (!showConnectionGui)
            {
                return;
            }

            UnlockCursorForConnectionGui();
            DrawConnectionBackground();

            float windowWidth = selectedWindowTab ==
                ArchipelagoWindowTab.Hints
                    ? HintsWindowWidth
                    : ConnectionWindowWidth;
            float windowHeight = selectedWindowTab ==
                ArchipelagoWindowTab.Hints
                    ? HintsWindowHeight
                    : ConnectionWindowHeight;
            connectionWindowRect.x = Mathf.Max(
                0f,
                (Screen.width - windowWidth) * 0.5f
            );
            connectionWindowRect.y = Mathf.Max(
                0f,
                (Screen.height - windowHeight) * 0.5f
            );
            connectionWindowRect.width = windowWidth;
            connectionWindowRect.height = windowHeight;

            GUILayout.Window(
                GetInstanceID(),
                connectionWindowRect,
                DrawConnectionWindow,
                "Archipelago"
            );
        }

        private void ShowConnectionGui()
        {
            if (showConnectionGui)
            {
                return;
            }

            showConnectionGui = true;
            CaptureCursorState();
        }

        private void HideConnectionGui(bool restoreCursor)
        {
            if (!showConnectionGui)
            {
                return;
            }

            showConnectionGui = false;

            if (restoreCursor)
            {
                RestoreCursorState();
            }
        }

        private void CaptureCursorState()
        {
            if (cursorStateCaptured)
            {
                return;
            }

            previousCursorVisible = Cursor.visible;
            previousCursorLockState = Cursor.lockState;
            cursorStateCaptured = true;
        }

        private void UnlockCursorForConnectionGui()
        {
            CaptureCursorState();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void RestoreCursorState()
        {
            if (!cursorStateCaptured)
            {
                return;
            }

            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockState;
            cursorStateCaptured = false;
        }

        private static void DrawConnectionBackground()
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void DrawConnectionWindow(int windowId)
        {
            GUILayout.BeginVertical();
            DrawArchipelagoWindowTabs();
            GUILayout.Space(8f);

            if (selectedWindowTab == ArchipelagoWindowTab.Hints)
            {
                DrawHintsTab();
            }
            else
            {
                DrawConnectionTab();
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawArchipelagoWindowTabs()
        {
            bool wasEnabled = GUI.enabled;
            GUILayout.BeginHorizontal();

            GUI.enabled =
                wasEnabled &&
                selectedWindowTab != ArchipelagoWindowTab.Connection;
            if (GUILayout.Button("Connection", GUILayout.Height(26f)))
            {
                selectedWindowTab = ArchipelagoWindowTab.Connection;
            }

            GUI.enabled =
                wasEnabled &&
                selectedWindowTab != ArchipelagoWindowTab.Hints;
            if (GUILayout.Button("Hints", GUILayout.Height(26f)))
            {
                selectedWindowTab = ArchipelagoWindowTab.Hints;
            }

            GUI.enabled = wasEnabled;
            GUILayout.EndHorizontal();
        }

        private void DrawConnectionTab()
        {
            bool connected = Archipelago.Instance != null && Archipelago.Instance.Connected;
            GUILayout.Label(connected
                ? "Connected. You can reconnect or switch servers below."
                : "Connect to an Archipelago room before playing this randomizer save.");
            GUILayout.Space(8f);

            GUILayout.Label("Host");
            connectionHost = GUILayout.TextField(connectionHost ?? string.Empty);

            GUILayout.Label("Port");
            connectionPort = GUILayout.TextField(connectionPort ?? string.Empty);

            GUILayout.Label("Slot");
            connectionSlot = GUILayout.TextField(connectionSlot ?? string.Empty);

            GUILayout.Label("Password");
            connectionPassword = GUILayout.PasswordField(connectionPassword ?? string.Empty, '*');

            if (!string.IsNullOrEmpty(connectionStatus))
            {
                GUILayout.Space(8f);
                GUILayout.Label(connectionStatus);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();

            GUI.enabled = !isConnecting;
            string connectButtonText = isConnecting
                ? "Connecting..."
                : connected ? "Reconnect / Switch" : "Connect";
            if (GUILayout.Button(connectButtonText, GUILayout.Height(32f)))
            {
                TryConnectFromGui();
            }

            GUI.enabled =
                !isConnecting &&
                FastTravelUtil.CanCyclePreferredHub();
            if (GUILayout.Button(
                    "\u2039",
                    GUILayout.Width(30f),
                    GUILayout.Height(32f)
                ))
            {
                FastTravelUtil.TryCyclePreferredHub(-1);
            }

            GUI.enabled =
                !isConnecting &&
                FastTravelUtil.CanTeleportToPreferredHub(out _);
            string warpButtonText =
                FastTravelUtil.GetPreferredHubName() + " (F4)";
            if (GUILayout.Button(warpButtonText, GUILayout.Height(32f)))
            {
                TryWarpToPreferredHub();
            }

            GUI.enabled =
                !isConnecting &&
                FastTravelUtil.CanCyclePreferredHub();
            if (GUILayout.Button(
                    "\u203A",
                    GUILayout.Width(30f),
                    GUILayout.Height(32f)
                ))
            {
                FastTravelUtil.TryCyclePreferredHub(1);
            }

            GUI.enabled = !isConnecting;
            if (GUILayout.Button(connected ? "Close" : "Load Offline", GUILayout.Height(32f)))
            {
                if (!connected && !SavePatches.HasOfflineLoadableSave())
                {
                    connectionStatus =
                        "Offline loading requires a save that was started while connected.";
                }
                else
                {
                    connectionGuiDismissed = true;
                    connectionStatus = string.Empty;
                    HideConnectionGui(true);
                }
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawHintsTab()
        {
            GUILayout.Label(
                "Official Archipelago hints and area rumours bought from Vog."
            );
            GUILayout.Space(8f);

            SaveState state = SaveState.Instance;
            IReadOnlyList<string> hints =
                state == null
                    ? Array.Empty<string>()
                    : state.BuildHintJournalEntries();
            hintsScrollPosition = GUILayout.BeginScrollView(
                hintsScrollPosition,
                GUILayout.ExpandHeight(true)
            );
            if (hints.Count == 0)
            {
                GUILayout.Label("No hints have been revealed yet.");
            }
            else
            {
                GUIStyle wrappingLabel = new GUIStyle(GUI.skin.label)
                {
                    richText = false,
                    wordWrap = true,
                };
                for (int index = 0; index < hints.Count; index++)
                {
                    GUILayout.Label(
                        (index + 1).ToString(
                            CultureInfo.InvariantCulture
                        ) + ". " + SanitizeHintJournalText(hints[index]),
                        wrappingLabel
                    );
                    GUILayout.Space(6f);
                }
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("Close", GUILayout.Height(32f)))
            {
                connectionGuiDismissed = true;
                HideConnectionGui(true);
            }
        }

        private static string SanitizeHintJournalText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unknown hint";
            }

            StringBuilder result = new StringBuilder();
            bool previousWasSpace = false;
            foreach (char character in value.Trim())
            {
                char output = character;
                if (character == '<')
                {
                    output = '\u2039';
                }
                else if (character == '>')
                {
                    output = '\u203A';
                }
                else if (char.IsControl(character) ||
                         char.IsWhiteSpace(character))
                {
                    output = ' ';
                }

                if (output == ' ')
                {
                    if (previousWasSpace)
                    {
                        continue;
                    }
                    previousWasSpace = true;
                }
                else
                {
                    previousWasSpace = false;
                }

                result.Append(output);
                if (result.Length >= 320)
                {
                    break;
                }
            }

            string sanitized = result.ToString().Trim();
            return sanitized.Length == 0 ? "Unknown hint" : sanitized;
        }

        private void TryWarpToPreferredHub()
        {
            try
            {
                if (!FastTravelUtil.TryTeleportToPreferredHub(
                    out string error
                ))
                {
                    Log.LogWarning("[RANDOMIZER] " + error);
                    if (showConnectionGui)
                    {
                        connectionStatus = error;
                    }
                    return;
                }

                connectionGuiDismissed = true;
                connectionStatus = string.Empty;
                HideConnectionGui(true);
                Log.LogInfo(
                    "[RANDOMIZER] Warping to " +
                    FastTravelUtil.GetPreferredHubName() + "."
                );
            }
            catch (Exception ex)
            {
                const string message =
                    "F4 warp failed because no transition was started.";
                Log.LogWarning("[RANDOMIZER] " + message + " " + ex);
                if (showConnectionGui)
                {
                    connectionStatus = message;
                }
            }
        }

        private void TryConnectFromGui()
        {
            int port;
            if (!int.TryParse(connectionPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)
            {
                connectionStatus = "Enter a valid port between 1 and 65535.";
                return;
            }

            if (string.IsNullOrWhiteSpace(connectionHost))
            {
                connectionStatus = "Enter the Archipelago host.";
                return;
            }

            if (string.IsNullOrWhiteSpace(connectionSlot))
            {
                connectionStatus = "Enter your slot name.";
                return;
            }

            isConnecting = true;
            connectionStatus = "Connecting...";
            StartCoroutine(ConnectFromGuiRoutine(
                connectionHost.Trim(),
                port,
                connectionSlot.Trim(),
                string.IsNullOrEmpty(connectionPassword) ? null : connectionPassword
            ));
        }

        private IEnumerator ConnectFromGuiRoutine(string host, int port, string slot, string password)
        {
            Task<bool> connectionTask = null;
            try
            {
                Archipelago archipelago = GetOrCreateArchipelago();
                connectionTask = Task.Run(() => archipelago.Connect(host, port, slot, password));
            }
            catch (Exception ex)
            {
                connectionStatus = "Connection failed: " + ex.Message;
                Log.LogWarning("[RANDOMIZER] Archipelago connection failed: " + ex);
                isConnecting = false;
                yield break;
            }

            while (!connectionTask.IsCompleted)
            {
                yield return null;
            }

            try
            {
                if (connectionTask.IsFaulted)
                {
                    throw connectionTask.Exception == null
                        ? new Exception("Unknown connection failure.")
                        : connectionTask.Exception.GetBaseException();
                }

                if (connectionTask.Result)
                {
                    Archipelago archipelago = GetOrCreateArchipelago();
                    if (archipelago.CompleteConnectionSync())
                    {
                        SaveSuccessfulConnectionDetails(host, port, slot);
                        connectionStatus = "Connected.";
                        connectionGuiDismissed = false;
                        HideConnectionGui(true);
                    }
                    else
                    {
                        connectionStatus = string.IsNullOrWhiteSpace(archipelago.LastError)
                            ? "Connection validation failed."
                            : archipelago.LastError;
                        ShowConnectionGui();
                    }
                }
                else
                {
                    string detail = Archipelago.Instance == null
                        ? string.Empty
                        : Archipelago.Instance.LastError;
                    connectionStatus = string.IsNullOrWhiteSpace(detail)
                        ? "Connection failed. Check the host, port, slot and password."
                        : detail;
                }
            }
            catch (Exception ex)
            {
                connectionStatus = "Connection failed: " + ex.Message;
                Log.LogWarning("[RANDOMIZER] Archipelago connection failed: " + ex);
            }
            finally
            {
                isConnecting = false;
            }
        }

        static void DumpPatchState(Harmony h)
        {
            foreach (var m in h.GetPatchedMethods().ToList())
            {
                var info = Harmony.GetPatchInfo(m);
                if (info == null || info.Owners == null) continue;
                if (!info.Owners.Contains(PluginGuid)) continue;

                var pre = info.Prefixes?.Count ?? 0;
                var post = info.Postfixes?.Count ?? 0;
                var tr = info.Transpilers?.Count ?? 0;
                Log.LogInfo($"[Harmony] Patched: {m.DeclaringType?.FullName}.{m.Name}  (prefixes={pre}, postfixes={post}, transpilers={tr})");
            }

            if (!Harmony.HasAnyPatches(PluginGuid))
                Log.LogError("[Harmony] No patches registered for our ID!");
        }
    }
}
