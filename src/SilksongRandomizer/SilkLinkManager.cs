using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace SilksongRandomizer
{
    /// <summary>
    /// Shares only the base nine points of current Silk between enabled
    /// Silksong slots on the same Archipelago team. Capacity and Silk above
    /// that base spool remain local to each player.
    /// </summary>
    internal static class SilkLinkManager
    {
        internal const int SharedSilkCapacity = 9;
        internal const string StorageKey =
            "SilksongRandomizer:SilkLink:v1:BaseSilk";

        private struct QueuedStorageUpdate
        {
            internal int Generation;
            internal int Value;
            internal bool IsAcknowledgement;
            internal int RequestedDelta;
        }

        internal struct LocalMutationState
        {
            internal bool Active;
            internal int Before;
            internal bool HasMemorySnapshot;
            internal int MemorySnapshotBefore;
        }

        private static readonly object QueueLock = new object();
        private static readonly Queue<QueuedStorageUpdate> StorageUpdates =
            new Queue<QueuedStorageUpdate>();
        private static readonly Queue<string> StatusMessages =
            new Queue<string>();

        private static Action<string> statusReporter;
        private static ArchipelagoSession session;
        private static DataStorageElement subscribedElement;
        private static DataStorageHelper.DataStorageUpdatedHandler
            subscribedHandler;
        private static HeroController subscribedHero;
        private static SaveState boundSaveState;
        private static PlayerData boundPlayerData;
        private static int generation;
        private static int authoritativeSharedSilk;
        private static int pendingSharedDelta;
        private static int privateSilk;
        private static bool enabled;
        private static bool storageStarted;
        private static bool sharedValueReady;
        private static bool resetWhenReady;
        private static bool pausedAfterSendFailure;
        private static int offlineBaseline = -1;
        private static bool needsOfflineReconciliation;

        internal static bool IsEnabled
        {
            get
            {
                lock (QueueLock)
                {
                    return enabled;
                }
            }
        }

        internal static bool IsSynchronized =>
            IsEnabled && sharedValueReady;

        internal static int SharedSilk =>
            sharedValueReady ? GetPredictedSharedSilk() : 0;

        internal static int PrivateSilk => privateSilk;

        internal static void Initialize(Action<string> reporter = null)
        {
            Reset();
            statusReporter = reporter;
        }

        internal static bool Configure(
            ArchipelagoSession connectedSession,
            bool shouldEnable)
        {
            Reset();

            if (!shouldEnable)
            {
                return true;
            }

            if (connectedSession == null)
            {
                QueueStatus(
                    "Silk Link could not start because its Archipelago " +
                    "session was missing."
                );
                return false;
            }

            lock (QueueLock)
            {
                session = connectedSession;
                enabled = true;
            }

            QueueStatus(
                "Silk Link enabled (experimental): the base nine Silk are " +
                "shared; upgraded capacity remains individual."
            );
            return true;
        }

        /// <summary>
        /// Performs all Unity-facing and PlayerData work on the main thread.
        /// Network callbacks only enqueue values for this method.
        /// </summary>
        internal static void Update()
        {
            FlushStatusMessages();

            if (!IsEnabled)
            {
                DetachHero();
                return;
            }

            // Tool/crest managers are briefly unavailable while Unity swaps
            // gameplay scenes. CurrentSilkMax consults those managers, so a
            // link update during that window can throw every frame and starve
            // the transition. The shared value stays queued until the new
            // gameplay scene is fully live.
            if (!HasLiveGameplayContext())
            {
                return;
            }

            SynchronizeHeroSubscription();

            SaveState saveState = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (saveState == null || !saveState.silkLink ||
                playerData == null)
            {
                boundSaveState = null;
                boundPlayerData = null;
                return;
            }

            if (!ReferenceEquals(boundSaveState, saveState) ||
                !ReferenceEquals(boundPlayerData, playerData))
            {
                bool continuingSession =
                    boundSaveState != null || boundPlayerData != null;
                BindPlayer(saveState, playerData, continuingSession);
            }

            if (!storageStarted && !TryStartStorage(playerData))
            {
                return;
            }

            ProcessStorageUpdates(playerData);
            if (!sharedValueReady)
            {
                return;
            }

            if (pausedAfterSendFailure)
            {
                return;
            }

            if (resetWhenReady)
            {
                resetWhenReady = false;
                ResetSharedSilkForDeath(playerData);
                return;
            }

            if (IsLinkSuspended(playerData))
            {
                return;
            }

            if (needsOfflineReconciliation)
            {
                int baseline = offlineBaseline;
                needsOfflineReconciliation = false;
                offlineBaseline = -1;
                ReconcileLocalMutation(
                    playerData,
                    baseline,
                    MemorySequenceSync.GetPersistentSilk(
                        playerData,
                        playerData.silk
                    )
                );
                return;
            }

            privateSilk = Math.Min(
                privateSilk,
                GetPrivateCapacity(playerData)
            );
            StorePrivateSilk();
            ApplyLinkedSilk(playerData);
        }

        internal static bool CanSynchronizeSilk(
            bool hasGameManager,
            bool isGameplayScene,
            bool isLoadingSceneTransition,
            bool isInSceneTransition,
            bool hasHero,
            bool hasToolManager)
        {
            return hasGameManager &&
                   isGameplayScene &&
                   !isLoadingSceneTransition &&
                   !isInSceneTransition &&
                   hasHero &&
                   hasToolManager;
        }

        private static bool HasLiveGameplayContext()
        {
            GameManager gameManager = GameManager.UnsafeInstance;
            return CanSynchronizeSilk(
                gameManager != null,
                gameManager != null && gameManager.IsGameplayScene(),
                gameManager != null &&
                    gameManager.IsLoadingSceneTransition,
                gameManager != null && gameManager.IsInSceneTransition,
                HeroController.UnsafeInstance != null,
                ManagerSingleton<ToolItemManager>.UnsafeInstance != null
            );
        }

        internal static void Reset()
        {
            DataStorageElement oldElement = subscribedElement;
            DataStorageHelper.DataStorageUpdatedHandler oldHandler =
                subscribedHandler;

            lock (QueueLock)
            {
                generation++;
                enabled = false;
                session = null;
                StorageUpdates.Clear();
                StatusMessages.Clear();
            }

            if (oldElement != null && oldHandler != null)
            {
                try
                {
                    oldElement.OnValueChanged -= oldHandler;
                }
                catch (Exception exception)
                {
                    LogDirect(
                        "Silk Link cleanup warning: " + exception.Message,
                        true
                    );
                }
            }

            subscribedElement = null;
            subscribedHandler = null;
            storageStarted = false;
            sharedValueReady = false;
            resetWhenReady = false;
            pausedAfterSendFailure = false;
            offlineBaseline = -1;
            needsOfflineReconciliation = false;
            authoritativeSharedSilk = 0;
            pendingSharedDelta = 0;
            privateSilk = 0;
            boundSaveState = null;
            boundPlayerData = null;
            DetachHero();
        }

        internal static bool BeginLocalMutation(
            PlayerData playerData,
            out LocalMutationState state)
        {
            state = default(LocalMutationState);
            if (!IsEnabled || !sharedValueReady ||
                pausedAfterSendFailure ||
                playerData == null ||
                !ReferenceEquals(playerData, boundPlayerData) ||
                IsLinkSuspended(playerData))
            {
                return false;
            }

            state.Active = true;
            state.Before = playerData.silk;
            int memorySnapshotBefore;
            state.HasMemorySnapshot =
                MemorySequenceSync.TryCaptureSilk(
                    playerData,
                    out memorySnapshotBefore
                );
            state.MemorySnapshotBefore = memorySnapshotBefore;
            return true;
        }

        internal static void EndLocalMutation(
            PlayerData playerData,
            LocalMutationState state)
        {
            if (!state.Active || playerData == null ||
                !ReferenceEquals(playerData, boundPlayerData) ||
                !sharedValueReady || pausedAfterSendFailure ||
                IsLinkSuspended(playerData))
            {
                return;
            }

            int after = playerData.silk;
            if (after == state.Before)
            {
                return;
            }

            ReconcileLocalMutation(
                playerData,
                state.Before,
                after,
                state.HasMemorySnapshot,
                state.MemorySnapshotBefore
            );
        }

        private static void ReconcileLocalMutation(
            PlayerData playerData,
            int before,
            int after,
            bool hasMemorySnapshot = false,
            int memorySnapshotBefore = 0)
        {
            if (playerData == null)
            {
                return;
            }

            int predictedShared = GetPredictedSharedSilk();
            int previousPrivateSilk = privateSilk;
            int[] reservoirs = CalculateLocalReservoirs(
                predictedShared,
                privateSilk,
                before,
                after,
                GetPrivateCapacity(playerData)
            );
            int requestedSharedDelta = reservoirs[0] - predictedShared;
            privateSilk = reservoirs[1];
            StorePrivateSilk();

            if (requestedSharedDelta != 0)
            {
                if (!SendSharedDelta(requestedSharedDelta))
                {
                    privateSilk = previousPrivateSilk;
                    StorePrivateSilk();
                    if (hasMemorySnapshot)
                    {
                        MemorySequenceSync.RebaseSilkDelta(
                            playerData,
                            memorySnapshotBefore,
                            before,
                            after
                        );
                    }
                    return;
                }
            }

            ApplyLinkedSilk(playerData);
        }

        /// <summary>
        /// Pure bucket transition used by the Harmony hooks and smoke tests.
        /// The returned values are shared Silk, private Silk, in that order.
        /// </summary>
        internal static int[] CalculateLocalReservoirs(
            int shared,
            int personal,
            int before,
            int after,
            int personalCapacity)
        {
            shared = Clamp(shared, 0, SharedSilkCapacity);
            personalCapacity = Math.Max(0, personalCapacity);
            personal = Clamp(personal, 0, personalCapacity);

            int difference = after - before;
            if (difference > 0)
            {
                int sharedGain = Math.Min(
                    difference,
                    SharedSilkCapacity - shared
                );
                shared += sharedGain;
                int personalGain = difference - sharedGain;
                personal = Math.Min(
                    personalCapacity,
                    personal + personalGain
                );
            }
            else if (difference < 0)
            {
                int remainingCost = -difference;
                int personalCost = Math.Min(remainingCost, personal);
                personal -= personalCost;
                remainingCost -= personalCost;
                shared = Math.Max(0, shared - remainingCost);
            }

            return new[] { shared, personal };
        }

        internal static int ComposeVisibleSilk(
            int shared,
            int personal,
            int currentMaximum)
        {
            return Math.Min(
                Math.Max(0, currentMaximum),
                Clamp(shared, 0, SharedSilkCapacity) +
                Math.Max(0, personal)
            );
        }

        internal static bool ShouldReconcileOfflineBalance(
            bool continuingSession,
            int runtimePrivateSilk,
            int savedBaseline)
        {
            return !continuingSession &&
                   runtimePrivateSilk >= 0 &&
                   savedBaseline >= 0;
        }

        private static void BindPlayer(
            SaveState saveState,
            PlayerData playerData,
            bool continuingSession)
        {
            boundSaveState = saveState;
            boundPlayerData = playerData;
            offlineBaseline = saveState.silkLinkLastSyncedBalance;
            needsOfflineReconciliation =
                ShouldReconcileOfflineBalance(
                    continuingSession,
                    saveState.silkLinkPrivateSilk,
                    offlineBaseline
                );
            if (!needsOfflineReconciliation)
            {
                offlineBaseline = -1;
            }

            int persistentSilk = MemorySequenceSync.GetPersistentSilk(
                playerData,
                playerData.silk
            );

            if (saveState.silkLinkPrivateSilk >= 0)
            {
                privateSilk = IsLinkSuspended(playerData)
                    ? saveState.silkLinkPrivateSilk
                    : Clamp(
                        saveState.silkLinkPrivateSilk,
                        0,
                        GetPrivateCapacity(playerData)
                    );
            }

            if (!sharedValueReady)
            {
                return;
            }

            if (saveState.silkLinkPrivateSilk < 0)
            {
                privateSilk = Clamp(
                    persistentSilk - GetPredictedSharedSilk(),
                    0,
                    GetPrivateCapacity(playerData)
                );
                StorePrivateSilk();
            }

            if (!needsOfflineReconciliation &&
                !IsLinkSuspended(playerData))
            {
                ApplyLinkedSilk(playerData);
            }
        }

        private static bool TryStartStorage(PlayerData playerData)
        {
            ArchipelagoSession currentSession;
            int currentGeneration;
            lock (QueueLock)
            {
                if (!enabled || session == null)
                {
                    return false;
                }

                currentSession = session;
                currentGeneration = generation;
            }

            DataStorageElement element = null;
            DataStorageHelper.DataStorageUpdatedHandler handler = null;
            try
            {
                element = currentSession.DataStorage[
                    Scope.Team,
                    StorageKey
                ];
                handler = (originalValue, newValue, arguments) =>
                    QueueStorageValue(
                        currentGeneration,
                        newValue,
                        false,
                        0
                    );
                element.OnValueChanged += handler;
                int persistentSilk = MemorySequenceSync.GetPersistentSilk(
                    playerData,
                    playerData.silk
                );
                int initialSharedSilk = needsOfflineReconciliation
                    ? Clamp(
                        offlineBaseline - Math.Max(0, privateSilk),
                        0,
                        SharedSilkCapacity
                    )
                    : Clamp(
                        persistentSilk,
                        0,
                        SharedSilkCapacity
                    );
                element.Initialize(
                    initialSharedSilk
                );

                subscribedElement = element;
                subscribedHandler = handler;
                storageStarted = true;

                SendSharedDelta(0);
                return true;
            }
            catch (Exception exception)
            {
                if (element != null && handler != null)
                {
                    try
                    {
                        element.OnValueChanged -= handler;
                    }
                    catch
                    {
                    }
                }

                subscribedElement = null;
                subscribedHandler = null;
                storageStarted = false;
                lock (QueueLock)
                {
                    enabled = false;
                }
                QueueStatus(
                    "Silk Link could not initialize its shared pool: " +
                    exception.Message
                );
                return false;
            }
        }

        private static void ProcessStorageUpdates(PlayerData playerData)
        {
            List<QueuedStorageUpdate> updates;
            int currentGeneration;
            lock (QueueLock)
            {
                if (StorageUpdates.Count == 0)
                {
                    return;
                }

                updates = new List<QueuedStorageUpdate>();
                currentGeneration = generation;
                while (StorageUpdates.Count > 0)
                {
                    updates.Add(StorageUpdates.Dequeue());
                }
            }

            foreach (QueuedStorageUpdate update in updates)
            {
                if (update.Generation != currentGeneration)
                {
                    continue;
                }

                if (update.IsAcknowledgement)
                {
                    pendingSharedDelta -= update.RequestedDelta;
                }

                authoritativeSharedSilk = Clamp(
                    update.Value,
                    0,
                    SharedSilkCapacity
                );

                if (!sharedValueReady)
                {
                    sharedValueReady = true;
                    if (boundSaveState != null &&
                        boundSaveState.silkLinkPrivateSilk >= 0)
                    {
                        privateSilk = IsLinkSuspended(playerData)
                            ? boundSaveState.silkLinkPrivateSilk
                            : Clamp(
                                boundSaveState.silkLinkPrivateSilk,
                                0,
                                GetPrivateCapacity(playerData)
                            );
                    }
                    else
                    {
                        int persistentSilk =
                            MemorySequenceSync.GetPersistentSilk(
                                playerData,
                                playerData.silk
                            );
                        privateSilk = Clamp(
                            persistentSilk - authoritativeSharedSilk,
                            0,
                            GetPrivateCapacity(playerData)
                        );
                    }
                    StorePrivateSilk();
                }
            }
        }

        private static bool SendSharedDelta(int delta)
        {
            ArchipelagoSession currentSession;
            int currentGeneration;
            lock (QueueLock)
            {
                if (!enabled || session == null || !storageStarted)
                {
                    return false;
                }

                currentSession = session;
                currentGeneration = generation;
            }

            DataStorageHelper.DataStorageUpdatedHandler callback =
                (originalValue, newValue, arguments) =>
                    QueueStorageValue(
                        currentGeneration,
                        newValue,
                        true,
                        delta
                    );

            pendingSharedDelta += delta;
            try
            {
                currentSession.DataStorage[Scope.Team, StorageKey] =
                    currentSession.DataStorage[Scope.Team, StorageKey] +
                    delta +
                    Operation.Max(0) +
                    Operation.Min(SharedSilkCapacity) +
                    Callback.Add(callback);
                return true;
            }
            catch (Exception exception)
            {
                pendingSharedDelta -= delta;
                pausedAfterSendFailure = true;
                QueueStatus(
                    "Silk Link paused after a shared-pool update failed; " +
                    "the local Silk result was preserved and will be " +
                    "reconciled after the link reconnects. " +
                    exception.Message
                );
                return false;
            }
        }

        private static void QueueStorageValue(
            int callbackGeneration,
            JToken value,
            bool isAcknowledgement,
            int requestedDelta)
        {
            int parsedValue;
            try
            {
                parsedValue = value == null
                    ? 0
                    : value.ToObject<int>();
            }
            catch (Exception exception)
            {
                QueueStatus(
                    "Silk Link received an invalid shared value: " +
                    exception.Message
                );
                return;
            }

            lock (QueueLock)
            {
                if (!enabled || callbackGeneration != generation)
                {
                    return;
                }

                StorageUpdates.Enqueue(new QueuedStorageUpdate
                {
                    Generation = callbackGeneration,
                    Value = parsedValue,
                    IsAcknowledgement = isAcknowledgement,
                    RequestedDelta = requestedDelta,
                });
            }
        }

        private static int GetPredictedSharedSilk()
        {
            return Clamp(
                authoritativeSharedSilk + pendingSharedDelta,
                0,
                SharedSilkCapacity
            );
        }

        private static int GetPrivateCapacity(PlayerData playerData)
        {
            if (playerData == null)
            {
                return 0;
            }

            int currentMaximum;
            try
            {
                currentMaximum = playerData.CurrentSilkMax;
            }
            catch
            {
                currentMaximum = playerData.silkMax;
            }

            return Math.Max(0, currentMaximum - SharedSilkCapacity);
        }

        private static bool IsLinkSuspended(PlayerData playerData)
        {
            if (playerData == null)
            {
                return true;
            }

            try
            {
                // A temporary or cursed crest can lower the usable spool below
                // nine. That state uses local Silk until the crest returns to
                // normal and restores the linked reservoirs.
                return playerData.IsAnyCursed ||
                       playerData.IsCurrentCrestTemp ||
                       playerData.UnlockSilkFinalCutscene ||
                       playerData.CurrentSilkMaxBasic <
                           SharedSilkCapacity;
            }
            catch
            {
                // Missing crest/tool managers are a temporary suspension,
                // never permission to continue into another unsafe capacity
                // read.
                return true;
            }
        }

        private static void ApplyLinkedSilk(PlayerData playerData)
        {
            if (playerData == null || IsLinkSuspended(playerData))
            {
                return;
            }

            int currentMaximum;
            try
            {
                currentMaximum = playerData.CurrentSilkMax;
            }
            catch
            {
                return;
            }

            int desired = ComposeVisibleSilk(
                GetPredictedSharedSilk(),
                privateSilk,
                currentMaximum
            );
            if (playerData.silk != desired)
            {
                playerData.silk = desired;
                RefreshSilkDisplay();
            }

            MemorySequenceSync.MirrorSilk(playerData, desired);
            StoreBaseline(desired);
        }

        private static void SynchronizeHeroSubscription()
        {
            HeroController hero = HeroController.SilentInstance;
            if (ReferenceEquals(hero, subscribedHero))
            {
                return;
            }

            DetachHero();
            subscribedHero = hero;
            if (subscribedHero != null)
            {
                subscribedHero.OnDeath += OnHeroDeath;
            }
        }

        private static void DetachHero()
        {
            if (subscribedHero != null)
            {
                subscribedHero.OnDeath -= OnHeroDeath;
            }

            subscribedHero = null;
        }

        private static void OnHeroDeath()
        {
            PlayerData playerData = PlayerData.instance;
            privateSilk = 0;
            StorePrivateSilk();
            if (!sharedValueReady)
            {
                resetWhenReady = true;
                if (playerData != null)
                {
                    playerData.silk = 0;
                    MemorySequenceSync.MirrorSilk(playerData, 0);
                    RefreshSilkDisplay();
                }
                return;
            }

            ResetSharedSilkForDeath(playerData);
        }

        private static void ResetSharedSilkForDeath(
            PlayerData playerData)
        {
            privateSilk = 0;
            StorePrivateSilk();
            int shared = GetPredictedSharedSilk();
            if (shared > 0)
            {
                SendSharedDelta(-shared);
            }

            if (playerData != null)
            {
                playerData.silk = 0;
                MemorySequenceSync.MirrorSilk(playerData, 0);
                RefreshSilkDisplay();
            }
        }

        private static void RefreshSilkDisplay()
        {
            try
            {
                SilkSpool spool = SilkSpool.Instance;
                if (spool != null)
                {
                    spool.RefreshSilk();
                }
            }
            catch (Exception exception)
            {
                LogDirect(
                    "Silk Link could not refresh the Silk display: " +
                    exception.Message,
                    true
                );
            }
        }

        private static void QueueStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lock (QueueLock)
            {
                StatusMessages.Enqueue(message);
            }
        }

        private static void StorePrivateSilk()
        {
            if (boundSaveState != null && boundSaveState.silkLink)
            {
                boundSaveState.silkLinkPrivateSilk =
                    Math.Max(0, privateSilk);
            }
        }

        private static void StoreBaseline(int value)
        {
            if (boundSaveState != null && boundSaveState.silkLink)
            {
                boundSaveState.silkLinkLastSyncedBalance =
                    Math.Max(0, value);
            }
        }

        private static void FlushStatusMessages()
        {
            List<string> messages;
            lock (QueueLock)
            {
                if (StatusMessages.Count == 0)
                {
                    return;
                }

                messages = new List<string>();
                while (StatusMessages.Count > 0)
                {
                    messages.Add(StatusMessages.Dequeue());
                }
            }

            foreach (string message in messages)
            {
                LogDirect(message, false);
                try
                {
                    statusReporter?.Invoke(message);
                }
                catch
                {
                }
            }
        }

        private static void LogDirect(string message, bool warning)
        {
            if (RandomizerPlugin.Log == null)
            {
                return;
            }

            if (warning)
            {
                RandomizerPlugin.Log.LogWarning(
                    "[RANDOMIZER] " + message
                );
            }
            else
            {
                RandomizerPlugin.Log.LogInfo(
                    "[RANDOMIZER] " + message
                );
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Min(Math.Max(value, minimum), maximum);
        }

        [HarmonyPatch(
            typeof(PlayerData),
            nameof(PlayerData.AddSilk),
            new Type[] { typeof(int) })]
        internal static class PlayerData_AddSilk_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(
                PlayerData __instance,
                out LocalMutationState __state)
            {
                BeginLocalMutation(__instance, out __state);
            }

            [HarmonyPostfix]
            private static void Postfix(
                PlayerData __instance,
                LocalMutationState __state)
            {
                EndLocalMutation(__instance, __state);
            }
        }

        [HarmonyPatch(
            typeof(PlayerData),
            nameof(PlayerData.TakeSilk),
            new Type[] { typeof(int) })]
        internal static class PlayerData_TakeSilk_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(
                PlayerData __instance,
                out LocalMutationState __state)
            {
                BeginLocalMutation(__instance, out __state);
            }

            [HarmonyPostfix]
            private static void Postfix(
                PlayerData __instance,
                LocalMutationState __state)
            {
                EndLocalMutation(__instance, __state);
            }
        }
    }
}
