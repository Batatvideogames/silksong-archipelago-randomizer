using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SilksongRandomizer.SaveState;

namespace SilksongRandomizer
{
    public sealed class Archipelago
    {
        public const string GoalLocationName = "Goal";
        public const string ActTwoGoal = "act_2";
        public const string ActThreeGoal = "act_3";
        public const string FleaHuntGoal = "flea_hunt";
        public const int DefaultFleaHuntGoalCount = 20;
        public const int MinimumFleaHuntGoalCount = 1;
        public const int MaximumFleaHuntGoalCount = 30;
        public const string RosaryMultiplierVanilla = "x1";
        public const string RosaryMultiplierOneAndHalf = "x1_5";
        public const string RosaryMultiplierDouble = "x2";
        public const string RosaryMultiplierTriple = "x3";
        public const string BellwayAccessBellBeastRequired =
            "bell_beast_required";
        public const string BellwayAccessRandomizedStations =
            "randomized_stations";
        public const string StartingLocationVanilla = "vanilla";
        public const string StartingLocationBoneBottom = "bone_bottom";
        public const string PriceModeVanilla = "vanilla";
        public const string PriceModeFree = "free";
        public const string PriceModeShuffled = "shuffled";
        public const string PriceModeCheap = "cheap";
        public const string PriceModeExpensive = "expensive";
        public static Archipelago Instance { get; private set; }

        public bool Connected => sessionReady && IsConnected();
        public string RoomSeed { get; private set; } = string.Empty;
        public string SlotName { get; private set; } = string.Empty;
        public int Team { get; private set; } = -1;
        public int Slot { get; private set; } = -1;
        public string WorldVersion { get; private set; } = string.Empty;
        public string Goal { get; private set; } = string.Empty;
        public int FleaHuntGoalCount { get; private set; } =
            DefaultFleaHuntGoalCount;
        public string StartingLocation { get; private set; } =
            StartingLocationVanilla;
        public string StartingCrest { get; private set; } = string.Empty;
        public bool SplitDashAndSprint { get; private set; }
        public bool RandomizeNeedleUpgrades { get; private set; }
        public bool StartWithMaps { get; private set; }
        public bool AutomaticCompass { get; private set; }
        public CheckMapMarkerMode CheckMapMarkers { get; private set; } =
            CheckMapMarkerMode.Off;
        public string BellwayAccess { get; private set; } =
            BellwayAccessBellBeastRequired;
        public string EnemyRosaryMultiplier { get; private set; } =
            RosaryMultiplierVanilla;
        public string EnemyShardMultiplier { get; private set; } =
            RosaryMultiplierVanilla;
        public string NormalShopPrices { get; private set; } =
            PriceModeVanilla;
        public string BellwayPrices { get; private set; } =
            PriceModeVanilla;
        public string MapPrices { get; private set; } = PriceModeVanilla;
        public string PinPrices { get; private set; } = PriceModeVanilla;
        public string UpgradePrices { get; private set; } =
            PriceModeVanilla;
        public string DonationPrices { get; private set; } =
            PriceModeVanilla;
        public IReadOnlyDictionary<string, int> PurchasePrices
        {
            get;
            private set;
        } = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(StringComparer.Ordinal)
        );
        public bool FasterDialogue { get; private set; }
        public bool DeathLink { get; private set; }
        public bool SilkLink { get; private set; }
        public bool RosaryLink { get; private set; }
        public bool ShellShardLink { get; private set; }
        public bool IndividualRelicTurnIns { get; private set; }
        public bool LogicAuditMode { get; private set; }
        public string MapLogicPayloadJson { get; private set; } =
            string.Empty;
        public RandomizationMode SkillRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode ToolRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode SilkSkillRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode CrestRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode FleaRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode CrestSlotRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode MaskShardRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode SpoolFragmentRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode SilkHeartRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode BellwayRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode VentricaRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode MapRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode MelodyRandomization { get; private set; } = RandomizationMode.Vanilla;
        public RandomizationMode PinRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode RelicRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode CraftingKitRandomization { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode MinorPickupRandomization { get; private set; } = RandomizationMode.Vanilla;
        public RandomizationMode SimpleKeyRandomization { get; private set; } = RandomizationMode.Vanilla;
        public RandomizationMode MemoryLocketRandomization { get; private set; } = RandomizationMode.Vanilla;
        public RandomizationMode CraftmetalRandomization { get; private set; } = RandomizationMode.Vanilla;
        public RandomizationMode MossberryRandomization { get; private set; } = RandomizationMode.Vanilla;
        public RandomizationMode PollipHeartRandomization { get; private set; } = RandomizationMode.Vanilla;
        public RandomizationMode SilkeaterRandomization { get; private set; } = RandomizationMode.Vanilla;
        public RandomizationMode MajorKeyRandomization { get; private set; } = RandomizationMode.Vanilla;
        public RandomizationMode ToolPouchRandomization { get; private set; } = RandomizationMode.Vanilla;
        public RandomizationMode BossSanity { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode BellShrineSanity { get; private set; } = RandomizationMode.Anywhere;
        public RandomizationMode QuestSanity { get; private set; } = RandomizationMode.Anywhere;
        public string LastError { get; private set; } = string.Empty;

        public event Action<string> OnItemReceived;
        public event Action<string, ItemFlags> OnItemSent;
        public event Action<string> ConnectionStatusChanged;

        private readonly object stateLock = new object();
        private readonly string configuredGameName;
        private readonly HashSet<string> unlockedItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> receivedItems = new List<string>();
        private readonly HashSet<string> unlockedLocationNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<long> unlockedLocationIds = new HashSet<long>();
        private readonly HashSet<string> pendingLocationNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> roomLocationNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task<HintData>> pendingHints =
            new Dictionary<string, Task<HintData>>(StringComparer.OrdinalIgnoreCase);
        private sealed class HintScoutChannel
        {
            internal readonly SemaphoreSlim Gate =
                new SemaphoreSlim(1, 1);
            internal volatile bool Poisoned;
        }

        private HintScoutChannel hintScoutChannel =
            new HintScoutChannel();
        private const int HintScoutTimeoutMilliseconds = 10000;

        private ArchipelagoSession session;
        private int lastQueuedItemIndex;
        private SaveState queuedForSaveState;
        private bool goalStatusPending;
        private volatile bool sessionReady;

        public Archipelago(string gameName = "Hollow Knight: Silksong")
        {
            configuredGameName = string.IsNullOrWhiteSpace(gameName) ? "Hollow Knight: Silksong" : gameName;
            Instance = this;
        }

        public bool Connect(string ip, int port, string slot, string pass)
        {
            if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(slot))
            {
                LastError = "Host and slot are required.";
                return false;
            }

            Disconnect();
            ClearState();
            LastError = string.Empty;

            try
            {
                session = ArchipelagoSessionFactory.CreateSession(ip, port);

                session.Items.ItemReceived += HandleItemReceived;
                session.MessageLog.OnMessageReceived += HandleMessageReceived;
                session.Locations.CheckedLocationsUpdated += OnCheckedLocationsUpdated;
                session.Socket.SocketClosed += OnSocketClosed;
                session.Socket.ErrorReceived += OnSocketError;

                LoginResult result = session.TryConnectAndLogin(
                    configuredGameName,
                    slot,
                    ItemsHandlingFlags.AllItems,
                    new Version(0, 6, 0),
                    new[] { "AP" },
                    null,
                    pass,
                    true
                );

                if (result == null || !result.Successful)
                {
                    LoginFailure failure = result as LoginFailure;
                    LastError = failure != null && failure.Errors != null && failure.Errors.Length > 0
                        ? string.Join("; ", failure.Errors)
                        : "The Archipelago server rejected the login.";
                    Disconnect();
                    return false;
                }

                LoginSuccessful successful = result as LoginSuccessful;
                RoomSeed = session.RoomState == null ? string.Empty : session.RoomState.Seed ?? string.Empty;
                SlotName = session.Players.ActivePlayer == null ||
                           string.IsNullOrWhiteSpace(session.Players.ActivePlayer.Name)
                    ? slot
                    : session.Players.ActivePlayer.Name;
                Team = successful == null ? session.ConnectionInfo.Team : successful.Team;
                Slot = successful == null ? session.ConnectionInfo.Slot : successful.Slot;
                WorldVersion = GetWorldVersion(successful);

                if (!IsSupportedWorldVersion(WorldVersion))
                {
                    string incompatible = "APWorld version '" +
                        (string.IsNullOrWhiteSpace(WorldVersion) ? "missing" : WorldVersion) +
                        "' is incompatible with plugin " + RandomizerPlugin.PluginVersion + ".";
                    Disconnect();
                    LastError = incompatible;
                    ReportStatus(incompatible);
                    return false;
                }

                string goal = GetGoal(successful);
                if (!IsSupportedGoal(goal))
                {
                    string incompatible = "APWorld goal '" +
                        (string.IsNullOrWhiteSpace(goal) ? "missing" : goal) +
                        "' is invalid; expected '" + ActTwoGoal +
                        "', '" + ActThreeGoal + "', or '" +
                        FleaHuntGoal + "'.";
                    Disconnect();
                    LastError = incompatible;
                    ReportStatus(incompatible);
                    return false;
                }

                string startingCrest = GetStartingCrest(successful);
                if (!CrestNames.IsSupportedStartingCrestKey(startingCrest))
                {
                    string incompatible = "APWorld starting crest '" +
                        (string.IsNullOrWhiteSpace(startingCrest) ? "missing" : startingCrest) +
                        "' is invalid; expected one of: " +
                        string.Join(", ", CrestNames.SupportedStartingCrestKeys) + ".";
                    Disconnect();
                    LastError = incompatible;
                    ReportStatus(incompatible);
                    return false;
                }

                string startingLocation = GetStartingLocation(successful);
                if (!IsSupportedStartingLocation(startingLocation))
                {
                    string incompatible = "APWorld starting location '" +
                        (string.IsNullOrWhiteSpace(startingLocation)
                            ? "missing"
                            : startingLocation) +
                        "' is invalid; expected '" +
                        StartingLocationVanilla + "' or '" +
                        StartingLocationBoneBottom + "'.";
                    Disconnect();
                    LastError = incompatible;
                    ReportStatus(incompatible);
                    return false;
                }

                Goal = goal;
                FleaHuntGoalCount = GetIntegerSlotData(
                    successful,
                    "flea_hunt_count",
                    DefaultFleaHuntGoalCount,
                    MinimumFleaHuntGoalCount,
                    MaximumFleaHuntGoalCount
                );
                StartingLocation = startingLocation;
                StartingCrest = startingCrest;
                SplitDashAndSprint = GetBooleanSlotData(
                    successful,
                    "split_dash_and_sprint",
                    false
                );
                RandomizeNeedleUpgrades = GetBooleanSlotData(
                    successful,
                    "randomize_needle_upgrades",
                    false
                );
                StartWithMaps = GetBooleanSlotData(
                    successful,
                    "start_with_maps",
                    false
                );
                AutomaticCompass = GetBooleanSlotData(
                    successful,
                    "automatic_compass",
                    false
                );
                CheckMapMarkers =
                    GetCheckMapMarkerMode(successful);
                BellwayAccess = GetBellwayAccess(successful);
                EnemyRosaryMultiplier =
                    GetEnemyRosaryMultiplier(successful);
                EnemyShardMultiplier =
                    GetEnemyShardMultiplier(successful);
                NormalShopPrices = GetPurchasePriceMode(
                    successful,
                    "normal_shop_prices"
                );
                BellwayPrices = GetPurchasePriceMode(
                    successful,
                    "bellway_prices"
                );
                MapPrices = GetPurchasePriceMode(
                    successful,
                    "map_prices"
                );
                PinPrices = GetPurchasePriceMode(
                    successful,
                    "pin_prices"
                );
                UpgradePrices = GetPurchasePriceMode(
                    successful,
                    "upgrade_prices"
                );
                DonationPrices = GetPurchasePriceMode(
                    successful,
                    "donation_prices"
                );
                PurchasePrices = GetPurchasePrices(successful);
                FasterDialogue = GetBooleanSlotData(
                    successful,
                    "faster_dialogue",
                    false
                );
                DeathLink = GetBooleanSlotData(
                    successful,
                    "death_link",
                    false
                );
                SilkLink = GetBooleanSlotData(
                    successful,
                    "silk_link",
                    false
                );
                RosaryLink = GetBooleanSlotData(
                    successful,
                    "rosary_link",
                    false
                );
                ShellShardLink = GetBooleanSlotData(
                    successful,
                    "shell_shard_link",
                    false
                );
                IndividualRelicTurnIns = GetBooleanSlotData(
                    successful,
                    "individual_relic_turn_ins",
                    false
                );
                LogicAuditMode = GetBooleanSlotData(
                    successful,
                    "logic_audit_mode",
                    false
                );
                SkillRandomization = GetRandomizationModeSlotData(
                    successful, "skill_randomization", RandomizationMode.Anywhere);
                ToolRandomization = GetRandomizationModeSlotData(
                    successful, "tool_randomization", RandomizationMode.Anywhere);
                SilkSkillRandomization = GetRandomizationModeSlotData(
                    successful, "silk_skill_randomization", RandomizationMode.Anywhere);
                CrestRandomization = GetRandomizationModeSlotData(
                    successful, "crest_randomization", RandomizationMode.Anywhere);
                FleaRandomization = GetRandomizationModeSlotData(
                    successful, "flea_randomization", RandomizationMode.Anywhere);
                CrestSlotRandomization = GetRandomizationModeSlotData(
                    successful, "crest_slot_randomization", RandomizationMode.Anywhere);
                MaskShardRandomization = GetRandomizationModeSlotData(
                    successful, "mask_shard_randomization", RandomizationMode.Anywhere);
                SpoolFragmentRandomization = GetRandomizationModeSlotData(
                    successful, "spool_fragment_randomization", RandomizationMode.Anywhere);
                SilkHeartRandomization = GetRandomizationModeSlotData(
                    successful, "silk_heart_randomization", RandomizationMode.Anywhere);
                BellwayRandomization = GetRandomizationModeSlotData(
                    successful, "bellway_randomization", RandomizationMode.Anywhere);
                VentricaRandomization = GetRandomizationModeSlotData(
                    successful, "ventrica_randomization", RandomizationMode.Anywhere);
                MapRandomization = GetRandomizationModeSlotData(
                    successful, "map_randomization", RandomizationMode.Anywhere);
                MelodyRandomization = GetRandomizationModeSlotData(
                    successful, "melody_randomization", RandomizationMode.Vanilla);
                PinRandomization = GetRandomizationModeSlotData(
                    successful, "pin_randomization", RandomizationMode.Anywhere);
                RelicRandomization = GetRandomizationModeSlotData(
                    successful, "relic_randomization", RandomizationMode.Anywhere);
                CraftingKitRandomization = GetRandomizationModeSlotData(
                    successful, "crafting_kit_randomization", RandomizationMode.Anywhere);
                MinorPickupRandomization = GetRandomizationModeSlotData(
                    successful, "minor_pickup_randomization", RandomizationMode.Vanilla);
                SimpleKeyRandomization = GetRandomizationModeSlotData(
                    successful, "simple_key_randomization", RandomizationMode.Vanilla);
                MemoryLocketRandomization = GetRandomizationModeSlotData(
                    successful, "memory_locket_randomization", RandomizationMode.Vanilla);
                CraftmetalRandomization = GetRandomizationModeSlotData(
                    successful, "craftmetal_randomization", RandomizationMode.Vanilla);
                MossberryRandomization = GetRandomizationModeSlotData(
                    successful, "mossberry_randomization", RandomizationMode.Vanilla);
                PollipHeartRandomization = GetRandomizationModeSlotData(
                    successful, "pollip_heart_randomization", RandomizationMode.Vanilla);
                SilkeaterRandomization = GetRandomizationModeSlotData(
                    successful, "silkeater_randomization", RandomizationMode.Vanilla);
                MajorKeyRandomization = GetRandomizationModeSlotData(
                    successful, "major_key_randomization", RandomizationMode.Vanilla);
                ToolPouchRandomization = GetRandomizationModeSlotData(
                    successful, "tool_pouch_randomization", RandomizationMode.Vanilla);
                BossSanity = GetRandomizationModeSlotData(
                    successful, "boss_sanity", RandomizationMode.Anywhere);
                BellShrineSanity = GetRandomizationModeSlotData(
                    successful, "bell_shrine_sanity", RandomizationMode.Anywhere);
                QuestSanity = GetRandomizationModeSlotData(
                    successful,
                    "quest_sanity",
                    RandomizationMode.Anywhere
                );
                MapLogicPayloadJson =
                    GetMapLogicPayloadJson(successful);
                CaptureRoomLocations();
                if (SaveState.Instance != null && SaveState.Instance.IsRoomBound &&
                    !SaveState.Instance.MatchesRoom(this))
                {
                    string mismatch = SaveState.Instance.GetRoomMismatchMessage(this);
                    Disconnect();
                    LastError = mismatch;
                    ReportStatus(mismatch);
                    return false;
                }

                RefreshReceivedItems();
                RefreshCheckedLocations();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Disconnect();
                ReportStatus("Connection failed: " + ex.Message);
                return false;
            }
        }

        public bool IsItemUnlocked(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            string canonicalItemName =
                ItemSet.GetCanonicalItemName(itemName);
            lock (stateLock)
            {
                return unlockedItems.Contains(canonicalItemName);
            }
        }

        public void UnlockItem(string itemName)
        {
            AddReceivedItem(itemName, true);
        }

        public IReadOnlyList<string> GetAllReceivedItems()
        {
            lock (stateLock)
            {
                return receivedItems.ToArray();
            }
        }

        public IReadOnlyDictionary<string, int> GetReceivedItemCounts()
        {
            Dictionary<string, int> counts =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase
                );

            try
            {
                if (IsConnected() &&
                    session?.Items?.AllItemsReceived != null)
                {
                    foreach (ItemInfo item in
                        session.Items.AllItemsReceived)
                    {
                        string itemName = GetItemName(item);
                        if (string.IsNullOrWhiteSpace(itemName))
                        {
                            continue;
                        }

                        counts.TryGetValue(
                            itemName,
                            out int previousCount
                        );
                        counts[itemName] = previousCount + 1;
                    }
                    return counts;
                }
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Could not read repeated AP item " +
                    "counts for map logic; using the saved inventory: " +
                    ex.Message
                );
            }

            lock (stateLock)
            {
                foreach (string itemName in receivedItems)
                {
                    counts[itemName] = 1;
                }
            }
            return counts;
        }

        public bool ValidateLoadedSave(SaveState saveState, out string error)
        {
            error = string.Empty;

            if (saveState == null || !IsConnected() ||
                !saveState.IsRoomBound || saveState.MatchesRoom(this))
            {
                return true;
            }

            error = saveState.GetRoomMismatchMessage(this);
            return false;
        }

        public void SynchronizeSaveState()
        {
            SaveState saveState = SaveState.Instance;
            if (saveState == null || !Connected)
            {
                return;
            }

            if (saveState.IsRoomBound && !saveState.MatchesRoom(this))
            {
                LastError = saveState.GetRoomMismatchMessage(this);
                ReportStatus(LastError);
                return;
            }

            if (!saveState.IsRoomBound)
            {
                saveState.BindToRoom(this);
            }

            saveState.SetRoomLocationNames(GetRoomLocationNames());
            saveState.mapLogicPayloadJson =
                MapLogicPayloadJson ?? string.Empty;
            saveState.logicAuditMode = LogicAuditMode;
            if (LogicAuditMode)
            {
                saveState.bellhomePhaseToggleUnlocked = true;
            }
            saveState.TryCompleteFleaHuntGoal();

            string[] serverChecks;
            lock (stateLock)
            {
                serverChecks = unlockedLocationNames.ToArray();
            }

            foreach (string locationName in serverChecks)
            {
                saveState.checkedLocations.Add(locationName);
                if (string.Equals(locationName, GoalLocationName, StringComparison.OrdinalIgnoreCase))
                {
                    saveState.goalCompleted = true;
                }
            }

            lock (stateLock)
            {
                foreach (string locationName in saveState.checkedLocations)
                {
                    if (!unlockedLocationNames.Contains(locationName))
                    {
                        pendingLocationNames.Add(locationName);
                    }
                }
            }

            QueueUnprocessedReceivedItems();

            if (saveState.goalCompleted)
            {
                goalStatusPending = true;
            }
        }

        public void Resynchronize()
        {
            SynchronizeSaveState();
            FlushPendingLocations();
        }

        public bool CompleteConnectionSync()
        {
            if (!IsConnected())
            {
                LastError = "The Archipelago socket closed during login.";
                return false;
            }

            SaveState saveState = SaveState.Instance;
            if (saveState != null && saveState.IsRoomBound && !saveState.MatchesRoom(this))
            {
                string mismatch = saveState.GetRoomMismatchMessage(this);
                Disconnect();
                LastError = mismatch;
                ReportStatus(mismatch);
                return false;
            }

            sessionReady = true;
            try
            {
                session.SetClientState(ArchipelagoClientState.ClientPlaying);
                Resynchronize();
                if (!DeathLinkManager.Configure(
                        session,
                        SlotName,
                        DeathLink
                    ))
                {
                    throw new InvalidOperationException(
                        "DeathLink could not be initialized."
                    );
                }
                if (!SilkLinkManager.Configure(
                        session,
                        SilkLink
                    ))
                {
                    throw new InvalidOperationException(
                        "Silk Link could not be initialized."
                    );
                }
                if (!CurrencyLinkManager.Configure(
                        session,
                        RosaryLink,
                        ShellShardLink
                    ))
                {
                    throw new InvalidOperationException(
                        "Rosary/Shell Shard links could not be initialized."
                    );
                }
                ReportStatus("Connected to " + SlotName + " on seed " + RoomSeed + ".");
                return true;
            }
            catch (Exception ex)
            {
                string failure = "Connection synchronization failed: " + ex.Message;
                Disconnect();
                LastError = failure;
                ReportStatus(failure);
                return false;
            }
        }

        public void ResetReceivedItemQueueCursor()
        {
            lock (stateLock)
            {
                queuedForSaveState = null;
                lastQueuedItemIndex = 0;
            }
        }

        public void UnlockLocation(string locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName))
            {
                return;
            }

            lock (stateLock)
            {
                if (unlockedLocationNames.Contains(locationName))
                {
                    if (string.Equals(locationName, GoalLocationName, StringComparison.OrdinalIgnoreCase))
                    {
                        SendGoalStatus();
                    }
                    return;
                }
            }

            if (!Connected)
            {
                lock (stateLock)
                {
                    pendingLocationNames.Add(locationName);
                }

                return;
            }

            long locationId = GetLocationId(locationName);
            if (locationId < 0)
            {
                return;
            }

            try
            {
                session.Locations.CompleteLocationChecks(locationId);
                MarkLocationUnlocked(locationName, locationId);
                if (string.Equals(locationName, GoalLocationName, StringComparison.OrdinalIgnoreCase))
                {
                    goalStatusPending = true;
                    SendGoalStatus();
                }
            }
            catch (Exception ex)
            {
                lock (stateLock)
                {
                    pendingLocationNames.Add(locationName);
                }

                LastError = "Could not send location '" + locationName + "': " + ex.Message;
            }
        }

        public bool IsLocationUnlocked(string locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName))
            {
                return false;
            }

            lock (stateLock)
            {
                if (unlockedLocationNames.Contains(locationName))
                {
                    return true;
                }
            }

            if (!Connected)
            {
                return false;
            }

            long locationId = GetLocationId(locationName);
            if (locationId < 0)
            {
                return false;
            }

            bool checkedOnServer = session.Locations.AllLocationsChecked.Contains(locationId);
            if (checkedOnServer)
            {
                MarkLocationUnlocked(locationName, locationId);
            }

            return checkedOnServer;
        }

        public bool IsLocationInRoom(string locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName))
            {
                return false;
            }

            string canonicalName =
                LocationSet.GetCanonicalLocationName(locationName);
            lock (stateLock)
            {
                return roomLocationNames.Contains(canonicalName);
            }
        }

        public IReadOnlyList<string> GetRoomLocationNames()
        {
            lock (stateLock)
            {
                return roomLocationNames.ToArray();
            }
        }

        public void Disconnect()
        {
            sessionReady = false;
            DeathLinkManager.Reset();
            SilkLinkManager.Reset();
            CurrencyLinkManager.Reset();
            ArchipelagoSession oldSession = session;
            session = null;

            if (oldSession == null)
            {
                return;
            }

            try
            {
                oldSession.Items.ItemReceived -= HandleItemReceived;
            }
            catch
            {
            }

            try
            {
                oldSession.MessageLog.OnMessageReceived -= HandleMessageReceived;
            }
            catch
            {
            }

            try
            {
                oldSession.Locations.CheckedLocationsUpdated -= OnCheckedLocationsUpdated;
            }
            catch
            {
            }

            try
            {
                oldSession.Socket.SocketClosed -= OnSocketClosed;
                oldSession.Socket.ErrorReceived -= OnSocketError;
            }
            catch
            {
            }

            try
            {
                if (oldSession.Socket != null && oldSession.Socket.Connected)
                {
                    oldSession.Socket.DisconnectAsync();
                }
            }
            catch
            {
            }
        }

        private void HandleItemReceived(ReceivedItemsHelper helper)
        {
            if (helper == null)
            {
                return;
            }

            try
            {
                while (helper.Any())
                {
                    ItemInfo item = helper.DequeueItem();
                    MarkItemUnlocked(item, true);
                }

                if (sessionReady)
                {
                    QueueUnprocessedReceivedItems();
                }
            }
            catch (Exception ex)
            {
                LastError = "Failed to process received items: " + ex.Message;
                ReportStatus(LastError);
            }
        }

        private void HandleMessageReceived(LogMessage message)
        {
            // Hint and !getitem messages derive from ItemSendLogMessage too.
            // Only the server message for a real item send belongs in
            // the gameplay notification queue.
            if (!sessionReady || message == null ||
                message.GetType() != typeof(ItemSendLogMessage))
            {
                return;
            }

            ItemSendLogMessage itemSend = (ItemSendLogMessage)message;
            if (!itemSend.IsSenderTheActivePlayer ||
                itemSend.IsReceiverTheActivePlayer ||
                itemSend.Receiver == null ||
                itemSend.Item == null)
            {
                return;
            }

            string itemName = itemSend.Item.ItemName;
            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = itemSend.Item.ItemDisplayName;
            }
            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = "Item " + itemSend.Item.ItemId;
            }

            string recipientName = itemSend.Receiver.Alias;
            if (string.IsNullOrWhiteSpace(recipientName))
            {
                recipientName = itemSend.Receiver.Name;
            }
            if (string.IsNullOrWhiteSpace(recipientName))
            {
                recipientName = "Player " + itemSend.Receiver.Slot;
            }

            Action<string, ItemFlags> handler = OnItemSent;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(
                    "Sent " + itemName + " to " + recipientName,
                    itemSend.Item.Flags
                );
            }
            catch
            {
            }
        }

        private void OnCheckedLocationsUpdated(ReadOnlyCollection<long> newCheckedLocations)
        {
            if (newCheckedLocations == null)
            {
                return;
            }

            foreach (long locationId in newCheckedLocations)
            {
                string locationName = GetLocationName(locationId);
                MarkLocationUnlocked(locationName, locationId);
            }
        }

        private void RefreshReceivedItems()
        {
            if (!IsConnected())
            {
                return;
            }

            foreach (ItemInfo item in session.Items.AllItemsReceived)
            {
                MarkItemUnlocked(item, false);
            }
        }

        private void CaptureRoomLocations()
        {
            if (!IsConnected())
            {
                return;
            }

            HashSet<string> captured =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (long locationId in session.Locations.AllLocations)
            {
                string locationName = GetLocationName(locationId);
                if (!string.IsNullOrWhiteSpace(locationName))
                {
                    captured.Add(locationName);
                }
            }

            lock (stateLock)
            {
                roomLocationNames.Clear();
                roomLocationNames.UnionWith(captured);
            }
        }

        private void RefreshCheckedLocations()
        {
            if (!IsConnected())
            {
                return;
            }

            foreach (long locationId in session.Locations.AllLocationsChecked)
            {
                string locationName = GetLocationName(locationId);
                MarkLocationUnlocked(locationName, locationId);
            }
        }

        private void FlushPendingLocations()
        {
            if (!Connected)
            {
                return;
            }

            string[] pending;

            lock (stateLock)
            {
                pending = pendingLocationNames.ToArray();
            }

            List<KeyValuePair<string, long>> resolved =
                new List<KeyValuePair<string, long>>();

            foreach (string locationName in pending)
            {
                long locationId = GetLocationId(locationName);
                if (locationId >= 0)
                {
                    resolved.Add(new KeyValuePair<string, long>(locationName, locationId));
                }
            }

            if (resolved.Count > 0)
            {
                try
                {
                    session.Locations.CompleteLocationChecks(
                        resolved.Select(entry => entry.Value).ToArray()
                    );

                    foreach (KeyValuePair<string, long> entry in resolved)
                    {
                        MarkLocationUnlocked(entry.Key, entry.Value);
                    }
                }
                catch (Exception ex)
                {
                    LastError = "Offline checks remain queued: " + ex.Message;
                    ReportStatus(LastError);
                }
            }

            if (goalStatusPending || (SaveState.Instance != null && SaveState.Instance.goalCompleted))
            {
                SendGoalStatus();
            }
        }

        private void QueueUnprocessedReceivedItems()
        {
            if (!Connected || SaveState.Instance == null || RandomizerPlugin.Instance == null)
            {
                return;
            }

            if (SaveState.Instance.IsRoomBound && !SaveState.Instance.MatchesRoom(this))
            {
                return;
            }

            ReadOnlyCollection<ItemInfo> allItems = session.Items.AllItemsReceived;
            List<Tuple<int, string, ItemFlags>> itemsToQueue =
                new List<Tuple<int, string, ItemFlags>>();

            lock (stateLock)
            {
                SaveState activeState = SaveState.Instance;
                if (activeState == null)
                {
                    return;
                }

                if (!ReferenceEquals(queuedForSaveState, activeState))
                {
                    queuedForSaveState = activeState;
                    lastQueuedItemIndex = activeState.receivedItemIndex;
                }

                int firstIndex = Math.Max(activeState.receivedItemIndex, lastQueuedItemIndex);
                for (int index = firstIndex; index < allItems.Count; index++)
                {
                    itemsToQueue.Add(Tuple.Create(
                        index,
                        GetItemName(allItems[index]),
                        allItems[index].Flags
                    ));
                }

                lastQueuedItemIndex = Math.Max(lastQueuedItemIndex, allItems.Count);
            }

            foreach (Tuple<int, string, ItemFlags> queuedItem in itemsToQueue)
            {
                RandomizerPlugin.Instance.QueueReceivedItem(
                    queuedItem.Item1,
                    queuedItem.Item2,
                    queuedItem.Item3
                );
            }
        }

        private void SendGoalStatus()
        {
            if (!Connected)
            {
                goalStatusPending = true;
                return;
            }

            try
            {
                session.SetGoalAchieved();
                goalStatusPending = false;
            }
            catch (Exception ex)
            {
                goalStatusPending = true;
                LastError = "Goal completion is queued for reconnect: " + ex.Message;
                ReportStatus(LastError);
            }
        }

        private void MarkItemUnlocked(ItemInfo item, bool raiseEvent)
        {
            AddReceivedItem(GetItemName(item), raiseEvent);
        }

        private string GetItemName(ItemInfo item)
        {
            if (item == null)
            {
                return null;
            }

            string itemName = item.ItemName;
            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = item.ItemDisplayName;
            }

            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = "Item " + item.ItemId;
            }

            return ItemSet.GetCanonicalItemName(itemName);
        }

        private void AddReceivedItem(string itemName, bool raiseEvent)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return;
            }

            Action<string> handler = null;
            string canonicalItemName =
                ItemSet.GetCanonicalItemName(itemName);

            lock (stateLock)
            {
                if (!unlockedItems.Add(canonicalItemName))
                {
                    return;
                }

                receivedItems.Add(canonicalItemName);

                if (raiseEvent)
                {
                    handler = OnItemReceived;
                }
            }

            if (handler == null)
            {
                return;
            }

            try
            {
                handler(canonicalItemName);
            }
            catch
            {
            }
        }

        private void MarkLocationUnlocked(string locationName, long locationId)
        {
            lock (stateLock)
            {
                if (!string.IsNullOrWhiteSpace(locationName))
                {
                    unlockedLocationNames.Add(locationName);
                    pendingLocationNames.Remove(locationName);
                }

                if (locationId >= 0)
                {
                    unlockedLocationIds.Add(locationId);
                }
            }
        }

        private long GetLocationId(string locationName)
        {
            if (!Connected || string.IsNullOrWhiteSpace(locationName))
            {
                return -1;
            }

            foreach (
                string roomLocationName
                in LocationSet.GetRoomLocationNameCandidates(
                    locationName
                ))
            {
                try
                {
                    long locationId =
                        session.Locations.GetLocationIdFromName(
                            GetGameName(),
                            roomLocationName
                        );
                    if (locationId >= 0)
                    {
                        return locationId;
                    }
                }
                catch
                {
                    // A missing name is treated as an unavailable location.
                }
            }

            return -1;
        }

        private string GetLocationName(long locationId)
        {
            if (!IsConnected() || locationId < 0)
            {
                return null;
            }

            try
            {
                return LocationSet.GetCanonicalLocationName(
                    session.Locations.GetLocationNameFromId(
                        locationId,
                        GetGameName()
                    )
                );
            }
            catch
            {
                return null;
            }
        }

        private string GetGameName()
        {
            if (session != null &&
                session.ConnectionInfo != null &&
                !string.IsNullOrWhiteSpace(session.ConnectionInfo.Game))
            {
                return session.ConnectionInfo.Game;
            }

            return configuredGameName;
        }

        private bool IsConnected()
        {
            return session != null &&
                   session.Socket != null &&
                   session.Socket.Connected;
        }

        private static string GetWorldVersion(LoginSuccessful login)
        {
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue("world_version", out object value) || value == null)
            {
                return string.Empty;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string GetGoal(LoginSuccessful login)
        {
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue("goal", out object value) || value == null)
            {
                return string.Empty;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string GetStartingCrest(LoginSuccessful login)
        {
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue("starting_crest", out object value) || value == null)
            {
                return string.Empty;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string GetStartingLocation(LoginSuccessful login)
        {
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue(
                    "starting_location",
                    out object value
                ) || value == null)
            {
                return string.Empty;
            }

            return (Convert.ToString(
                value,
                CultureInfo.InvariantCulture
            ) ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string GetMapLogicPayloadJson(
            LoginSuccessful login
        )
        {
            if (login == null || login.SlotData == null)
            {
                return string.Empty;
            }

            Dictionary<string, object> payload =
                new Dictionary<string, object>(
                    StringComparer.Ordinal
                );
            string[] keys =
            {
                "easy_skips",
                "requirements",
                "abstract_requirements",
                "logic_item_dependencies",
            };
            foreach (string key in keys)
            {
                if (login.SlotData.TryGetValue(
                        key,
                        out object value
                    ) &&
                    value != null)
                {
                    payload[key] = value;
                }
            }

            // Map markers remain neutral when slot data is incomplete.
            if (!payload.ContainsKey("requirements") ||
                !payload.ContainsKey("abstract_requirements") ||
                !payload.ContainsKey("logic_item_dependencies"))
            {
                return string.Empty;
            }

            return JsonConvert.SerializeObject(payload);
        }

        private static bool GetBooleanSlotData(
            LoginSuccessful login,
            string key,
            bool defaultValue
        )
        {
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue(key, out object value) ||
                value == null)
            {
                return defaultValue;
            }

            if (value is bool boolean)
            {
                return boolean;
            }

            string text = Convert.ToString(
                value,
                CultureInfo.InvariantCulture
            );
            if (bool.TryParse(text, out boolean))
            {
                return boolean;
            }

            if (long.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long number
            ))
            {
                return number != 0;
            }

            return defaultValue;
        }

        private static int GetIntegerSlotData(
            LoginSuccessful login,
            string key,
            int defaultValue,
            int minimum,
            int maximum
        )
        {
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue(key, out object value) ||
                value == null)
            {
                return defaultValue;
            }

            string text = Convert.ToString(
                value,
                CultureInfo.InvariantCulture
            );
            if (!int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsed
                ) ||
                parsed < minimum ||
                parsed > maximum)
            {
                throw new FormatException(
                    "APWorld setting '" + key + "' must be between " +
                    minimum + " and " + maximum + "; received '" +
                    text + "'."
                );
            }

            return parsed;
        }

        private static RandomizationMode GetRandomizationModeSlotData(
            LoginSuccessful login,
            string key,
            RandomizationMode defaultValue
        )
        {
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue(key, out object value) ||
                value == null)
            {
                return defaultValue;
            }

            if (value is bool boolean)
            {
                return boolean
                    ? RandomizationMode.Anywhere
                    : RandomizationMode.Vanilla;
            }

            string text = Convert.ToString(
                value,
                CultureInfo.InvariantCulture
            );
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new FormatException(
                    "APWorld setting '" + key + "' is empty."
                );
            }

            string normalized = text.Trim()
                .ToLowerInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");

            if (bool.TryParse(normalized, out boolean))
            {
                return boolean
                    ? RandomizationMode.Anywhere
                    : RandomizationMode.Vanilla;
            }

            if (long.TryParse(
                normalized,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long number
            ))
            {
                if (number >= (long)RandomizationMode.Vanilla &&
                    number <= (long)RandomizationMode.Shuffle)
                {
                    return (RandomizationMode)number;
                }

                throw new FormatException(
                    "APWorld setting '" + key +
                    "' must be 0 (vanilla), 1 (anywhere), or 2 (shuffle)."
                );
            }

            switch (normalized)
            {
                case "vanilla":
                case "off":
                    return RandomizationMode.Vanilla;
                case "anywhere":
                    return RandomizationMode.Anywhere;
                case "shuffle":
                case "shuffle_within_category":
                case "within_category":
                    return RandomizationMode.Shuffle;
                default:
                    throw new FormatException(
                        "APWorld setting '" + key + "' has invalid mode '" +
                        text + "'."
                    );
            }
        }

        private static CheckMapMarkerMode GetCheckMapMarkerMode(
            LoginSuccessful login
        )
        {
            const string key = "check_map_markers";
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue(key, out object value) ||
                value == null)
            {
                return CheckMapMarkerMode.Off;
            }

            string text = Convert.ToString(
                value,
                CultureInfo.InvariantCulture
            );
            string normalized = string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim()
                    .ToLowerInvariant()
                    .Replace("-", "_")
                    .Replace(" ", "_");

            if (long.TryParse(
                normalized,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long numericMode
            ) &&
                numericMode >= (long)CheckMapMarkerMode.Off &&
                numericMode <= (long)CheckMapMarkerMode.All)
            {
                return (CheckMapMarkerMode)numericMode;
            }

            switch (normalized)
            {
                case "off":
                    return CheckMapMarkerMode.Off;
                case "mapped_rooms":
                    return CheckMapMarkerMode.MappedRooms;
                case "owned_maps":
                    return CheckMapMarkerMode.OwnedMaps;
                case "all":
                    return CheckMapMarkerMode.All;
                default:
                    throw new FormatException(
                        "APWorld setting '" + key +
                        "' has invalid mode '" + text + "'."
                    );
            }
        }

        private static string GetEnemyRosaryMultiplier(
            LoginSuccessful login
        )
        {
            return GetEnemyDropMultiplier(
                login,
                "enemy_rosary_multiplier",
                "Rosary"
            );
        }

        private static string GetPurchasePriceMode(
            LoginSuccessful login,
            string key
        )
        {
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue(key, out object value) ||
                value == null)
            {
                return PriceModeVanilla;
            }

            string text = Convert.ToString(
                value,
                CultureInfo.InvariantCulture
            );
            string normalized = string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim()
                    .ToLowerInvariant()
                    .Replace("-", "_")
                    .Replace(" ", "_");
            if (long.TryParse(
                    normalized,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long numericMode
                ))
            {
                switch (numericMode)
                {
                    case 0:
                        normalized = PriceModeVanilla;
                        break;
                    case 1:
                        normalized = PriceModeFree;
                        break;
                    case 2:
                        normalized = PriceModeShuffled;
                        break;
                    case 3:
                        normalized = PriceModeCheap;
                        break;
                    case 4:
                        normalized = PriceModeExpensive;
                        break;
                }
            }

            if (IsSupportedPurchasePriceMode(normalized))
            {
                return normalized;
            }

            throw new FormatException(
                "APWorld setting '" + key +
                "' has invalid price mode '" + text + "'."
            );
        }

        private static IReadOnlyDictionary<string, int>
            GetPurchasePrices(LoginSuccessful login)
        {
            const string key = "purchase_prices";
            Dictionary<string, int> result =
                new Dictionary<string, int>(StringComparer.Ordinal);
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue(key, out object value) ||
                value == null)
            {
                return new ReadOnlyDictionary<string, int>(result);
            }

            JObject prices;
            try
            {
                prices = value as JObject ?? JObject.FromObject(value);
            }
            catch (Exception ex)
            {
                throw new FormatException(
                    "APWorld setting 'purchase_prices' is not an object.",
                    ex
                );
            }

            foreach (JProperty property in prices.Properties())
            {
                string priceKey = property.Name ?? string.Empty;
                if (!IsSupportedPurchasePriceKey(priceKey) ||
                    !int.TryParse(
                        Convert.ToString(
                            property.Value,
                            CultureInfo.InvariantCulture
                        ),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int price
                    ) ||
                    price < 0 ||
                    price > 2000)
                {
                    throw new FormatException(
                        "APWorld purchase price '" + priceKey +
                        "' has invalid value '" + property.Value + "'."
                    );
                }

                result.Add(priceKey, price);
            }

            return new ReadOnlyDictionary<string, int>(result);
        }

        private static string GetEnemyShardMultiplier(
            LoginSuccessful login
        )
        {
            return GetEnemyDropMultiplier(
                login,
                "enemy_shard_multiplier",
                "Shell Shard"
            );
        }

        private static string GetEnemyDropMultiplier(
            LoginSuccessful login,
            string key,
            string displayName
        )
        {
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue(key, out object value) ||
                value == null)
            {
                return RosaryMultiplierVanilla;
            }

            string text = Convert.ToString(
                value,
                CultureInfo.InvariantCulture
            );
            string normalized = string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim().ToLowerInvariant();
            if (IsSupportedEnemyDropMultiplier(normalized))
            {
                return normalized;
            }

            throw new FormatException(
                "APWorld " + displayName + " setting '" + key +
                "' has invalid value '" +
                text + "'."
            );
        }

        private static string GetBellwayAccess(
            LoginSuccessful login
        )
        {
            const string key = "bellway_access";
            if (login == null || login.SlotData == null ||
                !login.SlotData.TryGetValue(key, out object value) ||
                value == null)
            {
                return BellwayAccessBellBeastRequired;
            }

            string text = Convert.ToString(
                value,
                CultureInfo.InvariantCulture
            );
            string normalized = string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim().ToLowerInvariant();
            if (IsSupportedBellwayAccess(normalized))
            {
                return normalized;
            }

            throw new FormatException(
                "APWorld setting '" + key + "' has invalid value '" +
                text + "'."
            );
        }

        internal static bool IsSupportedBellwayAccess(
            string value
        )
        {
            return string.Equals(
                       value,
                       BellwayAccessBellBeastRequired,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       value,
                       BellwayAccessRandomizedStations,
                       StringComparison.Ordinal
                   );
        }

        internal static bool IsSupportedEnemyRosaryMultiplier(
            string value
        )
        {
            return IsSupportedEnemyDropMultiplier(value);
        }

        internal static bool IsSupportedEnemyShardMultiplier(
            string value
        )
        {
            return IsSupportedEnemyDropMultiplier(value);
        }

        internal static bool IsSupportedPurchasePriceMode(string value)
        {
            return string.Equals(
                       value,
                       PriceModeVanilla,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       value,
                       PriceModeFree,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       value,
                       PriceModeShuffled,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       value,
                       PriceModeCheap,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       value,
                       PriceModeExpensive,
                       StringComparison.Ordinal
                   );
        }

        private static bool IsSupportedPurchasePriceKey(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   (
                       value.StartsWith("shop:", StringComparison.Ordinal) ||
                       value.StartsWith(
                           "bellway:",
                           StringComparison.Ordinal
                       ) ||
                       value.StartsWith(
                           "upgrade:plinney:",
                           StringComparison.Ordinal
                       ) ||
                       value.StartsWith(
                           "donation:",
                           StringComparison.Ordinal
                       )
                   );
        }

        private static bool IsSupportedEnemyDropMultiplier(
            string value
        )
        {
            return string.Equals(
                       value,
                       RosaryMultiplierVanilla,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       value,
                       RosaryMultiplierOneAndHalf,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       value,
                       RosaryMultiplierDouble,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       value,
                       RosaryMultiplierTriple,
                       StringComparison.Ordinal
                   );
        }

        internal static bool IsSupportedGoal(string goal)
        {
            return string.Equals(goal, ActTwoGoal, StringComparison.Ordinal) ||
                   string.Equals(goal, ActThreeGoal, StringComparison.Ordinal) ||
                   string.Equals(goal, FleaHuntGoal, StringComparison.Ordinal);
        }

        internal static bool IsSupportedFleaHuntGoalCount(int count)
        {
            return count >= MinimumFleaHuntGoalCount &&
                   count <= MaximumFleaHuntGoalCount;
        }

        internal static bool IsSupportedStartingLocation(string value)
        {
            return string.Equals(
                       value,
                       StartingLocationVanilla,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       value,
                       StartingLocationBoneBottom,
                       StringComparison.Ordinal
                   );
        }

        internal static bool IsSupportedWorldVersion(string worldVersion)
        {
            return string.Equals(
                worldVersion,
                RandomizerPlugin.PluginVersion,
                StringComparison.Ordinal
            );
        }

        private void OnSocketClosed(string reason)
        {
            sessionReady = false;
            DeathLinkManager.Reset();
            SilkLinkManager.Reset();
            CurrencyLinkManager.Reset();
            LastError = string.IsNullOrWhiteSpace(reason)
                ? "Disconnected from Archipelago."
                : "Disconnected from Archipelago: " + reason;
            ReportStatus(LastError);
        }

        private void OnSocketError(Exception exception, string message)
        {
            string detail = !string.IsNullOrWhiteSpace(message)
                ? message
                : exception == null ? "Unknown socket error." : exception.Message;
            LastError = "Archipelago network error: " + detail;
            if (exception != null)
            {
                RandomizerPlugin.Log?.LogError(
                    "[RANDOMIZER] Archipelago socket error: " + exception
                );
            }
            ReportStatus(LastError);
        }

        private void ReportStatus(string status)
        {
            Action<string> handler = ConnectionStatusChanged;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(status);
            }
            catch
            {
            }
        }

        private void ClearState()
        {
            lock (stateLock)
            {
                unlockedItems.Clear();
                receivedItems.Clear();
                unlockedLocationNames.Clear();
                unlockedLocationIds.Clear();
                pendingLocationNames.Clear();
                roomLocationNames.Clear();
                pendingHints.Clear();
                hintScoutChannel = new HintScoutChannel();
                lastQueuedItemIndex = 0;
                queuedForSaveState = null;
            }

            RoomSeed = string.Empty;
            SlotName = string.Empty;
            Team = -1;
            Slot = -1;
            WorldVersion = string.Empty;
            Goal = string.Empty;
            FleaHuntGoalCount = DefaultFleaHuntGoalCount;
            StartingLocation = StartingLocationVanilla;
            StartingCrest = string.Empty;
            SplitDashAndSprint = false;
            RandomizeNeedleUpgrades = false;
            StartWithMaps = false;
            AutomaticCompass = false;
            CheckMapMarkers = CheckMapMarkerMode.Off;
            BellwayAccess = BellwayAccessBellBeastRequired;
            EnemyRosaryMultiplier = RosaryMultiplierVanilla;
            EnemyShardMultiplier = RosaryMultiplierVanilla;
            NormalShopPrices = PriceModeVanilla;
            BellwayPrices = PriceModeVanilla;
            MapPrices = PriceModeVanilla;
            PinPrices = PriceModeVanilla;
            UpgradePrices = PriceModeVanilla;
            DonationPrices = PriceModeVanilla;
            PurchasePrices = new ReadOnlyDictionary<string, int>(
                new Dictionary<string, int>(StringComparer.Ordinal)
            );
            FasterDialogue = false;
            DeathLink = false;
            SilkLink = false;
            RosaryLink = false;
            ShellShardLink = false;
            IndividualRelicTurnIns = false;
            LogicAuditMode = false;
            MapLogicPayloadJson = string.Empty;
            SkillRandomization = RandomizationMode.Anywhere;
            ToolRandomization = RandomizationMode.Anywhere;
            SilkSkillRandomization = RandomizationMode.Anywhere;
            CrestRandomization = RandomizationMode.Anywhere;
            FleaRandomization = RandomizationMode.Anywhere;
            CrestSlotRandomization = RandomizationMode.Anywhere;
            MaskShardRandomization = RandomizationMode.Anywhere;
            SpoolFragmentRandomization = RandomizationMode.Anywhere;
            SilkHeartRandomization = RandomizationMode.Anywhere;
            BellwayRandomization = RandomizationMode.Anywhere;
            VentricaRandomization = RandomizationMode.Anywhere;
            MapRandomization = RandomizationMode.Anywhere;
            MelodyRandomization = RandomizationMode.Vanilla;
            PinRandomization = RandomizationMode.Anywhere;
            RelicRandomization = RandomizationMode.Anywhere;
            CraftingKitRandomization = RandomizationMode.Anywhere;
            MinorPickupRandomization = RandomizationMode.Vanilla;
            SimpleKeyRandomization = RandomizationMode.Vanilla;
            PollipHeartRandomization = RandomizationMode.Vanilla;
            ToolPouchRandomization = RandomizationMode.Vanilla;
            BossSanity = RandomizationMode.Anywhere;
            BellShrineSanity = RandomizationMode.Anywhere;
            QuestSanity = RandomizationMode.Anywhere;
            goalStatusPending = false;
            sessionReady = false;
        }

        public HintData GetHint(string locationName)
        {
            if (!Connected || string.IsNullOrWhiteSpace(locationName))
            {
                return null;
            }
            locationName =
                LocationSet.GetCanonicalLocationName(locationName);

            Task<HintData> request = RequestHintAsync(locationName);

            if (request == null || !request.IsCompleted)
            {
                return null;
            }

            ReleaseHintRequest(locationName, request);

            if (request.IsCanceled || request.IsFaulted)
            {
                return null;
            }

            return request.GetAwaiter().GetResult();
        }

        internal Task<HintData> RequestHintAsync(string locationName)
        {
            if (!Connected || string.IsNullOrWhiteSpace(locationName))
            {
                return null;
            }

            locationName =
                LocationSet.GetCanonicalLocationName(locationName);
            lock (stateLock)
            {
                if (!pendingHints.TryGetValue(
                        locationName,
                        out Task<HintData> request))
                {
                    request = FetchHintAsync(
                        locationName,
                        HintCreationPolicy.CreateAndAnnounceOnce
                    );
                    pendingHints[locationName] = request;
                }

                return request;
            }
        }

        internal Task<Dictionary<string, HintData>> RequestHintsAsync(
            IEnumerable<string> locationNames,
            HintCreationPolicy hintCreationPolicy,
            string requestPurpose)
        {
            return FetchHintsAsync(
                locationNames,
                hintCreationPolicy,
                requestPurpose
            );
        }

        internal void ReleaseHintRequest(
            string locationName,
            Task<HintData> request)
        {
            if (request == null ||
                !request.IsCompleted ||
                string.IsNullOrWhiteSpace(locationName))
            {
                return;
            }

            locationName =
                LocationSet.GetCanonicalLocationName(locationName);
            lock (stateLock)
            {
                if (pendingHints.TryGetValue(
                        locationName,
                        out Task<HintData> currentRequest) &&
                    ReferenceEquals(currentRequest, request))
                {
                    pendingHints.Remove(locationName);
                }
            }
        }

        private async Task<HintData> FetchHintAsync(
            string locationName,
            HintCreationPolicy hintCreationPolicy
        )
        {
            Dictionary<string, HintData> hints = await FetchHintsAsync(
                new[] { locationName },
                hintCreationPolicy,
                "hint for " + locationName
            ).ConfigureAwait(false);
            string canonicalLocationName =
                LocationSet.GetCanonicalLocationName(locationName);
            return hints.TryGetValue(
                    canonicalLocationName,
                    out HintData hint
                )
                ? hint
                : null;
        }

        private async Task<Dictionary<string, HintData>> FetchHintsAsync(
            IEnumerable<string> locationNames,
            HintCreationPolicy hintCreationPolicy,
            string requestPurpose
        )
        {
            Dictionary<string, HintData> hints =
                new Dictionary<string, HintData>(
                    StringComparer.OrdinalIgnoreCase
                );
            if (!Connected || locationNames == null)
            {
                return hints;
            }

            HintScoutChannel scoutChannel = hintScoutChannel;
            if (scoutChannel.Poisoned)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Cannot scout " + requestPurpose +
                    " because a previous scout timed out. Reconnect to " +
                    "Archipelago before retrying."
                );
                return hints;
            }

            bool gateAcquired = false;
            try
            {
                gateAcquired = await scoutChannel.Gate.WaitAsync(
                    HintScoutTimeoutMilliseconds
                ).ConfigureAwait(false);
                if (!gateAcquired)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Timed out waiting to scout " +
                        requestPurpose + "."
                    );
                    return hints;
                }

                if (!ReferenceEquals(
                        scoutChannel,
                        hintScoutChannel
                    ) ||
                    scoutChannel.Poisoned ||
                    !Connected)
                {
                    return hints;
                }

                Dictionary<string, long> locations =
                    new Dictionary<string, long>(
                        StringComparer.OrdinalIgnoreCase
                    );
                foreach (string locationName in locationNames)
                {
                    string canonicalLocationName =
                        LocationSet.GetCanonicalLocationName(locationName);
                    if (string.IsNullOrWhiteSpace(
                            canonicalLocationName
                        ) ||
                        locations.ContainsKey(canonicalLocationName))
                    {
                        continue;
                    }

                    long locationId = GetLocationId(
                        canonicalLocationName
                    );
                    if (locationId >= 0)
                    {
                        locations.Add(
                            canonicalLocationName,
                            locationId
                        );
                    }
                    else
                    {
                        RandomizerPlugin.Log?.LogWarning(
                            "[RANDOMIZER] Could not resolve shop/hint " +
                            "location '" + canonicalLocationName +
                            "' while scouting " + requestPurpose + "."
                        );
                    }
                }

                if (locations.Count == 0)
                {
                    return hints;
                }

                Dictionary<long, ScoutedItemInfo> result;
                ArchipelagoSession requestSession;
                string requestSeed;
                int requestTeam;
                int requestSlot;
                requestSession = session;
                requestSeed = RoomSeed;
                requestTeam = Team;
                requestSlot = Slot;
                Task<Dictionary<long, ScoutedItemInfo>> scoutTask =
                    requestSession.Locations.ScoutLocationsAsync(
                        hintCreationPolicy,
                        locations.Values.Distinct().ToArray()
                    );
                Task completedTask = await Task.WhenAny(
                    scoutTask,
                    Task.Delay(HintScoutTimeoutMilliseconds)
                ).ConfigureAwait(false);
                if (!ReferenceEquals(completedTask, scoutTask))
                {
                    scoutChannel.Poisoned = true;
                    gateAcquired = false;
                    _ = ObservePoisonedScoutAsync(
                        scoutChannel,
                        scoutTask
                    );
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Timed out scouting " +
                        requestPurpose + ". Reconnect to Archipelago " +
                        "before retrying so no scout responses can cross."
                    );
                    return hints;
                }

                result = await scoutTask.ConfigureAwait(false);
                if (!IsSameHintSession(
                    requestSession,
                    requestSeed,
                    requestTeam,
                    requestSlot
                ))
                {
                    return hints;
                }

                if (result == null)
                {
                    return hints;
                }

                foreach (KeyValuePair<string, long> location in locations)
                {
                    if (result.TryGetValue(
                            location.Value,
                            out ScoutedItemInfo info
                        ) &&
                        info != null)
                    {
                        hints[location.Key] = CreateHintData(
                            location.Key,
                            info
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Failed to scout " + requestPurpose +
                    ": " + ex.Message
                );
            }
            finally
            {
                if (gateAcquired)
                {
                    scoutChannel.Gate.Release();
                }
            }

            return hints;
        }

        private static async Task ObservePoisonedScoutAsync(
            HintScoutChannel scoutChannel,
            Task<Dictionary<long, ScoutedItemInfo>> scoutTask
        )
        {
            try
            {
                await scoutTask.ConfigureAwait(false);
            }
            catch
            {
                // The timed-out request is detached. Observe a
                // later fault so it cannot become an unobserved task exception.
            }
            finally
            {
                // The channel remains poisoned after the request settles.
                // MultiClient.Net 6.7.1 owns one scout callback per session.
                // Only reconnecting and replacing the channel is unambiguous.
                scoutChannel.Gate.Release();
            }
        }

        private bool IsSameHintSession(
            ArchipelagoSession requestSession,
            string requestSeed,
            int requestTeam,
            int requestSlot
        )
        {
            return Connected &&
                   ReferenceEquals(requestSession, session) &&
                   string.Equals(
                       requestSeed,
                       RoomSeed,
                       StringComparison.Ordinal
                   ) &&
                   requestTeam == Team &&
                   requestSlot == Slot;
        }

        private HintData CreateHintData(
            string locationName,
            ScoutedItemInfo info
        )
        {
            string itemName = string.IsNullOrWhiteSpace(info.ItemName)
                ? info.ItemDisplayName
                : info.ItemName;
            if (info.Player != null &&
                string.Equals(
                    info.Player.Game,
                    configuredGameName,
                    StringComparison.Ordinal))
            {
                itemName = ItemSet.GetCanonicalItemName(itemName);
            }

            string userName = null;
            if (info.Player != null)
            {
                userName = string.IsNullOrWhiteSpace(info.Player.Alias)
                    ? info.Player.Name
                    : info.Player.Alias;
            }

            return new HintData
            {
                locationName = locationName,
                user = userName,
                item = itemName,
                flags = info.Flags
            };
        }
    }
}
