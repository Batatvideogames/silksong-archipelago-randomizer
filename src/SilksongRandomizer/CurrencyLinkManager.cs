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
    /// Shares loose Rosaries and the base Shell Shard pouch between enabled
    /// Silksong slots on the same Archipelago team. Tool Pouch capacity above
    /// the vanilla 400-Shard base remains local to each player.
    /// </summary>
    internal static class CurrencyLinkManager
    {
        internal const int RosarySharedCapacity = 9999999;
        internal const int ShellShardSharedCapacity = 400;
        internal const int MinimumSharedBalance = -9999999;
        internal const string RosaryStorageKey =
            "SilksongRandomizer:RosaryLink:v1:LooseRosaries";
        internal const string ShellShardStorageKey =
            "SilksongRandomizer:ShellShardLink:v1:BaseShards";

        internal enum LinkedCurrency
        {
            Rosaries,
            ShellShards,
        }

        private sealed class LinkState
        {
            internal readonly LinkedCurrency Kind;
            internal readonly string DisplayName;
            internal readonly string StorageKey;
            internal readonly CurrencyType CurrencyType;
            internal readonly int SharedCapacity;

            internal bool Enabled;
            internal bool StorageStarted;
            internal bool SharedValueReady;
            internal bool WasTemporarilyStored;
            internal bool TemporaryStorageActive;
            internal int AuthoritativeShared;
            internal int PendingSharedDelta;
            internal int PrivateAmount;
            internal int LastObserved;
            internal int OfflineBaseline = -1;
            internal bool NeedsOfflineReconciliation;
            internal DataStorageElement SubscribedElement;
            internal DataStorageHelper.DataStorageUpdatedHandler
                SubscribedHandler;

            internal LinkState(
                LinkedCurrency kind,
                string displayName,
                string storageKey,
                CurrencyType currencyType,
                int sharedCapacity)
            {
                Kind = kind;
                DisplayName = displayName;
                StorageKey = storageKey;
                CurrencyType = currencyType;
                SharedCapacity = sharedCapacity;
            }
        }

        private struct QueuedStorageUpdate
        {
            internal int Generation;
            internal LinkedCurrency Kind;
            internal StorageMutationPurpose Purpose;
            internal int TransactionId;
            internal bool HasOriginalValue;
            internal int OriginalValue;
            internal int Value;
            internal bool IsAcknowledgement;
            internal int RequestedDelta;
        }

        private enum StorageMutationPurpose
        {
            Ordinary,
            RosaryDeathDebit,
        }

        private sealed class RosaryDeathRecord
        {
            internal int Sequence;
            internal PlayerData PlayerData;
            internal int Before;
            internal int NativeLoss = -1;
            internal bool SuppressSharedMutation;
            internal bool DebitSent;
            internal bool DebitConfirmed;
            internal int RequestedDebit;
            internal int RecoverableAmount;
            internal bool CocoonAvailable = true;
            internal bool RecoveryRequested;
            internal bool RecoveryCredited;
        }

        internal struct LocalMutationState
        {
            internal LinkedCurrency Kind;
            internal bool Active;
            internal int Before;
            internal bool HasMemorySnapshot;
            internal int MemorySnapshotBefore;

            internal LocalMutationState(
                LinkedCurrency linkedCurrency,
                int value)
            {
                Kind = linkedCurrency;
                Active = true;
                Before = value;
                HasMemorySnapshot = false;
                MemorySnapshotBefore = 0;
            }
        }

        internal struct RosaryCocoonMutationState
        {
            internal bool Active;
            internal int Sequence;
            internal int OriginalNativeAmount;
        }

        private static readonly object QueueLock = new object();
        private static readonly Queue<QueuedStorageUpdate> StorageUpdates =
            new Queue<QueuedStorageUpdate>();
        private static readonly Queue<string> StatusMessages =
            new Queue<string>();
        private static readonly Dictionary<int, RosaryDeathRecord>
            RosaryDeaths = new Dictionary<int, RosaryDeathRecord>();
        private static readonly LinkState Rosaries = new LinkState(
            LinkedCurrency.Rosaries,
            "Rosary Link",
            RosaryStorageKey,
            CurrencyType.Money,
            RosarySharedCapacity
        );
        private static readonly LinkState ShellShards = new LinkState(
            LinkedCurrency.ShellShards,
            "Shell Shard Link",
            ShellShardStorageKey,
            CurrencyType.Shard,
            ShellShardSharedCapacity
        );

        private static Action<string> statusReporter;
        private static ArchipelagoSession session;
        private static SaveState boundSaveState;
        private static PlayerData boundPlayerData;
        private static int generation;
        private static int ignoredLocalMutationDepth;
        private static int nextRosaryDeathSequence;
        private static int activeRosaryCocoonSequence;
        private const long RosaryCannonReloadDiagnosticIntervalTicks =
            TimeSpan.TicksPerSecond * 2;
        private static long nextRosaryCannonReloadDiagnosticTicks;

        internal static bool IsEnabled
        {
            get
            {
                lock (QueueLock)
                {
                    return Rosaries.Enabled || ShellShards.Enabled;
                }
            }
        }

        internal static void Initialize(Action<string> reporter = null)
        {
            Reset();
            statusReporter = reporter;
        }

        internal static bool Configure(
            ArchipelagoSession connectedSession,
            bool rosaryLink,
            bool shellShardLink)
        {
            Reset();

            if (!rosaryLink && !shellShardLink)
            {
                return true;
            }

            if (connectedSession == null)
            {
                QueueStatus(
                    "Currency links could not start because their " +
                    "Archipelago session was missing."
                );
                return false;
            }

            lock (QueueLock)
            {
                session = connectedSession;
                Rosaries.Enabled = rosaryLink;
                ShellShards.Enabled = shellShardLink;
            }

            if (rosaryLink)
            {
                QueueStatus(
                    "Rosary Link enabled (experimental): loose Rosaries " +
                    "are shared; strings, bank storage and cocoons remain " +
                    "local."
                );
            }

            if (shellShardLink)
            {
                QueueStatus(
                    "Shell Shard Link enabled (experimental): the base " +
                    "400 Shards are shared; Tool Pouch overflow remains " +
                    "individual."
                );
            }

            return true;
        }

        /// <summary>
        /// Network callbacks only enqueue values. PlayerData and Unity UI are
        /// touched here on the main thread.
        /// </summary>
        internal static void Update()
        {
            FlushStatusMessages();

            if (!IsEnabled)
            {
                return;
            }

            // QuitToMenu replaces the live save's PlayerData with a fresh,
            // zeroed singleton while the Archipelago session is still alive.
            // That teardown object is not interpreted as a real team purchase.
            if (!HasLiveGameplayContext())
            {
                return;
            }

            SaveState saveState = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (saveState == null || playerData == null)
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
                BindPlayer(
                    saveState,
                    playerData,
                    continuingSession
                );
            }

            TryStartStorage(Rosaries, saveState, playerData);
            TryStartStorage(ShellShards, saveState, playerData);
            ProcessStorageUpdates();
            SynchronizeState(Rosaries, saveState, playerData);
            SynchronizeState(ShellShards, saveState, playerData);
        }

        internal static void Reset()
        {
            Unsubscribe(Rosaries);
            Unsubscribe(ShellShards);

            lock (QueueLock)
            {
                generation++;
                session = null;
                StorageUpdates.Clear();
                StatusMessages.Clear();
            }

            ResetState(Rosaries);
            ResetState(ShellShards);
            boundSaveState = null;
            boundPlayerData = null;
            ignoredLocalMutationDepth = 0;
            // The v1 DataStorage value is a scalar, not an idempotent ledger.
            // If a socket disappears after a death write is sent but before
            // its callback, neither the client nor a reconnect can prove
            // whether that write committed. Runtime escrow must therefore be
            // discarded here. Strict crash-safe exactly-once transfer would
            // require a versioned operation-id protocol.
            RosaryDeaths.Clear();
            nextRosaryDeathSequence = 0;
            activeRosaryCocoonSequence = 0;
            nextRosaryCannonReloadDiagnosticTicks = 0;
        }

        private static void ResetState(LinkState state)
        {
            state.Enabled = false;
            state.StorageStarted = false;
            state.SharedValueReady = false;
            state.WasTemporarilyStored = false;
            state.TemporaryStorageActive = false;
            state.AuthoritativeShared = 0;
            state.PendingSharedDelta = 0;
            state.PrivateAmount = 0;
            state.LastObserved = 0;
            state.OfflineBaseline = -1;
            state.NeedsOfflineReconciliation = false;
            state.SubscribedElement = null;
            state.SubscribedHandler = null;
        }

        private static void BindPlayer(
            SaveState saveState,
            PlayerData playerData,
            bool continuingSession)
        {
            boundSaveState = saveState;
            boundPlayerData = playerData;

            BindState(
                Rosaries,
                saveState,
                playerData,
                continuingSession
            );
            BindState(
                ShellShards,
                saveState,
                playerData,
                continuingSession
            );
        }

        private static void BindState(
            LinkState state,
            SaveState saveState,
            PlayerData playerData,
            bool continuingSession)
        {
            state.LastObserved = GetCurrent(state, playerData);
            state.WasTemporarilyStored =
                IsTemporarilyStored(state, playerData);
            state.OfflineBaseline = GetSavedBaseline(state, saveState);
            state.NeedsOfflineReconciliation =
                ShouldReconcileOfflineBalance(
                    continuingSession,
                    state.OfflineBaseline
                );
            if (!state.NeedsOfflineReconciliation)
            {
                state.OfflineBaseline = -1;
            }

            if (state.Kind == LinkedCurrency.ShellShards)
            {
                int privateCapacity = GetPrivateCapacity(state, playerData);
                int persistentCurrent =
                    MemorySequenceSync.GetPersistentCurrency(
                        playerData,
                        state.CurrencyType,
                        state.LastObserved
                    );
                state.PrivateAmount = saveState.shellShardLinkPrivateShards >= 0
                    ? Clamp(
                        saveState.shellShardLinkPrivateShards,
                        0,
                        privateCapacity
                    )
                    : Clamp(
                        Math.Max(
                            0,
                            persistentCurrent - state.SharedCapacity
                        ),
                        0,
                        privateCapacity
                    );
                StorePrivateAmount(state);
            }

        }

        internal static bool CanSynchronizeCurrency(
            bool hasGameManager,
            bool isGameplayScene,
            bool hasHero)
        {
            return hasGameManager && isGameplayScene && hasHero;
        }

        /// <summary>
        /// A queued DeathLink waits until Rosary Link has a shared
        /// baseline and is outside native temporary-currency storage. Memory
        /// scenes are live: the linked value is mirrored into their restore
        /// snapshot. Remote deaths remain currency-neutral because the
        /// originating local death already debits the shared team pool.
        /// </summary>
        internal static bool CanApplyRemoteDeath(PlayerData playerData)
        {
            if (!Rosaries.Enabled)
            {
                return true;
            }

            return playerData != null &&
                   ReferenceEquals(playerData, boundPlayerData) &&
                   Rosaries.SharedValueReady &&
                   boundSaveState != null &&
                   boundSaveState.rosaryLink &&
                   !IsTemporarilyStored(Rosaries, playerData);
        }

        internal static bool ShouldReconcileOfflineBalance(
            bool continuingSession,
            int offlineBaseline)
        {
            return !continuingSession && offlineBaseline >= 0;
        }

        private static bool HasLiveGameplayContext()
        {
            GameManager gameManager = GameManager.UnsafeInstance;
            HeroController hero = HeroController.UnsafeInstance;
            return CanSynchronizeCurrency(
                gameManager != null,
                gameManager != null && gameManager.IsGameplayScene(),
                hero != null
            );
        }

        private static void TryStartStorage(
            LinkState state,
            SaveState saveState,
            PlayerData playerData)
        {
            if (!state.Enabled || state.StorageStarted ||
                !IsOptionEnabled(state, saveState))
            {
                return;
            }

            ArchipelagoSession currentSession;
            int currentGeneration;
            lock (QueueLock)
            {
                if (session == null)
                {
                    return;
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
                    state.StorageKey
                ];
                handler = (originalValue, newValue, arguments) =>
                    QueueStorageValue(
                        currentGeneration,
                        state.Kind,
                        StorageMutationPurpose.Ordinary,
                        0,
                        originalValue,
                        newValue,
                        false,
                        0
                    );
                element.OnValueChanged += handler;

                int current = MemorySequenceSync.GetPersistentCurrency(
                    playerData,
                    state.CurrencyType,
                    GetCurrent(state, playerData)
                );
                if (!MemorySequenceSync.HasRecordedSnapshot(playerData) &&
                    IsTemporarilyStored(state, playerData))
                {
                    current = GetTemporarilyStored(state, playerData);
                }

                if (state.Kind == LinkedCurrency.Rosaries &&
                    TryGetCapturedPreDeathBalance(
                        playerData,
                        out int capturedPreDeathBalance))
                {
                    current = capturedPreDeathBalance;
                }

                int initialShared = CalculateInitialSharedValue(
                    current,
                    state.PrivateAmount,
                    state.OfflineBaseline,
                    state.NeedsOfflineReconciliation,
                    state.SharedCapacity
                );
                element.Initialize(initialShared);

                state.SubscribedElement = element;
                state.SubscribedHandler = handler;
                state.StorageStarted = true;
                SendSharedDelta(state, 0);
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

                state.SubscribedElement = null;
                state.SubscribedHandler = null;
                state.StorageStarted = false;
                state.Enabled = false;
                QueueStatus(
                    state.DisplayName +
                    " could not initialize its shared pool: " +
                    exception.Message
                );
            }
        }

        private static void ProcessStorageUpdates()
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

                LinkState state = GetState(update.Kind);
                if (!state.Enabled)
                {
                    continue;
                }

                if (update.IsAcknowledgement)
                {
                    state.PendingSharedDelta -= update.RequestedDelta;
                }

                state.AuthoritativeShared = Clamp(
                    update.Value,
                    MinimumSharedBalance,
                    state.SharedCapacity
                );
                state.SharedValueReady = true;

                if (update.IsAcknowledgement &&
                    update.Purpose ==
                        StorageMutationPurpose.RosaryDeathDebit)
                {
                    ConfirmRosaryDeathDebit(
                        update.TransactionId,
                        update.HasOriginalValue
                            ? update.OriginalValue
                            : update.Value,
                        update.Value
                    );
                }
            }
        }

        private static void SynchronizeState(
            LinkState state,
            SaveState saveState,
            PlayerData playerData)
        {
            if (!state.Enabled || !state.SharedValueReady ||
                !IsOptionEnabled(state, saveState))
            {
                return;
            }

            if (state.Kind == LinkedCurrency.Rosaries &&
                TryProcessRosaryDeath(state, playerData))
            {
                return;
            }

            int privateCapacity = GetPrivateCapacity(state, playerData);
            state.PrivateAmount = Clamp(
                state.PrivateAmount,
                0,
                privateCapacity
            );
            StorePrivateAmount(state);

            if (IsTemporarilyStored(state, playerData))
            {
                state.WasTemporarilyStored = true;
                state.LastObserved = GetCurrent(state, playerData);
                return;
            }

            if (state.NeedsOfflineReconciliation)
            {
                int baseline = state.OfflineBaseline;
                state.NeedsOfflineReconciliation = false;
                state.OfflineBaseline = -1;
                state.WasTemporarilyStored = false;
                ReconcileLocalMutation(
                    state,
                    playerData,
                    baseline,
                    MemorySequenceSync.GetPersistentCurrency(
                        playerData,
                        state.CurrencyType,
                        GetCurrent(state, playerData)
                    )
                );
                return;
            }

            if (state.WasTemporarilyStored)
            {
                state.WasTemporarilyStored = false;
                ApplyLinkedValue(state, playerData, true);
                RefreshCounter(
                    state,
                    GetCurrent(state, playerData)
                );
                return;
            }

            int current = GetCurrent(state, playerData);
            if (current != state.LastObserved)
            {
                bool hasMemorySnapshot =
                    MemorySequenceSync.TryCaptureCurrency(
                        playerData,
                        state.CurrencyType,
                        out int memorySnapshotBefore
                    );
                ReconcileLocalMutation(
                    state,
                    playerData,
                    state.LastObserved,
                    current,
                    hasMemorySnapshot,
                    memorySnapshotBefore
                );
                return;
            }

            ApplyLinkedValue(state, playerData, true);
        }

        private static bool BeginLocalMutation(
            LinkState state,
            PlayerData playerData,
            out LocalMutationState mutation)
        {
            mutation = default(LocalMutationState);
            if (!OwnsLocalMutation(state, playerData))
            {
                return false;
            }

            mutation = new LocalMutationState(
                state.Kind,
                GetCurrent(state, playerData)
            );
            int memorySnapshotBefore;
            mutation.HasMemorySnapshot =
                MemorySequenceSync.TryCaptureCurrency(
                    playerData,
                    state.CurrencyType,
                    out memorySnapshotBefore
                );
            mutation.MemorySnapshotBefore = memorySnapshotBefore;
            return true;
        }

        /// <summary>
        /// True only when the matching PlayerData Harmony prefix/postfix will
        /// own this exact mutation. AP's synchronous memory-currency helper
        /// uses this to avoid applying a second snapshot rebase after the
        /// link has already mirrored the resulting value.
        /// </summary>
        internal static bool OwnsLocalMutation(
            PlayerData playerData,
            CurrencyType currencyType)
        {
            if (currencyType == CurrencyType.Money)
            {
                return OwnsLocalMutation(Rosaries, playerData);
            }

            if (currencyType == CurrencyType.Shard)
            {
                return OwnsLocalMutation(ShellShards, playerData);
            }

            return false;
        }

        private static bool OwnsLocalMutation(
            LinkState state,
            PlayerData playerData)
        {
            return state != null &&
                   state.Enabled &&
                   state.SharedValueReady &&
                   ignoredLocalMutationDepth <= 0 &&
                   boundSaveState != null &&
                   IsOptionEnabled(state, boundSaveState) &&
                   playerData != null &&
                   ReferenceEquals(playerData, boundPlayerData);
        }

        private static void EndLocalMutation(
            PlayerData playerData,
            LocalMutationState mutation)
        {
            if (!mutation.Active || playerData == null ||
                !ReferenceEquals(playerData, boundPlayerData) ||
                ignoredLocalMutationDepth > 0)
            {
                return;
            }

            LinkState state = GetState(mutation.Kind);
            int after = GetCurrent(state, playerData);
            if (after != mutation.Before)
            {
                ReconcileLocalMutation(
                    state,
                    playerData,
                    mutation.Before,
                    after,
                    mutation.HasMemorySnapshot,
                    mutation.MemorySnapshotBefore
                );
            }
            else
            {
                state.LastObserved = after;
            }
        }

        private static void ReconcileLocalMutation(
            LinkState state,
            PlayerData playerData,
            int before,
            int after,
            bool hasMemorySnapshot = false,
            int memorySnapshotBefore = 0)
        {
            int predictedShared = GetPredictedShared(state);
            int previousPrivateAmount = state.PrivateAmount;
            int[] reservoirs = CalculateLocalReservoirs(
                predictedShared,
                state.PrivateAmount,
                before,
                after,
                state.SharedCapacity,
                GetPrivateCapacity(state, playerData)
            );
            int requestedSharedDelta = reservoirs[0] - predictedShared;
            state.PrivateAmount = reservoirs[1];
            StorePrivateAmount(state);

            if (requestedSharedDelta != 0)
            {
                if (!SendSharedDelta(state, requestedSharedDelta))
                {
                    // A synchronous socket/DataStorage failure must not turn
                    // a purchase into a refund or erase a locally earned
                    // pickup. The native result remains and the saved reconnect
                    // baseline applies this delta once after reconfiguration.
                    state.PrivateAmount = previousPrivateAmount;
                    StorePrivateAmount(state);
                    state.LastObserved = after;
                    if (hasMemorySnapshot)
                    {
                        MemorySequenceSync.RebaseCurrencyDelta(
                            playerData,
                            state.CurrencyType,
                            memorySnapshotBefore,
                            before,
                            after
                        );
                    }
                    RefreshCounter(state, after);
                    return;
                }
            }

            if (IsTemporarilyStored(state, playerData))
            {
                SetCurrent(state, playerData, 0);
                state.LastObserved = 0;
                state.WasTemporarilyStored = true;
                return;
            }

            ApplyLinkedValue(state, playerData, after != GetVisibleValue(
                state,
                playerData
            ));
        }

        /// <summary>
        /// Pure shared/private bucket transition. Shared values may be
        /// negative: simultaneous stale spends become team debt instead of a
        /// free purchase and later gains repay that debt first.
        /// </summary>
        internal static int[] CalculateLocalReservoirs(
            int shared,
            int personal,
            int before,
            int after,
            int sharedCapacity,
            int personalCapacity)
        {
            sharedCapacity = Math.Max(0, sharedCapacity);
            personalCapacity = Math.Max(0, personalCapacity);
            shared = Clamp(
                shared,
                MinimumSharedBalance,
                sharedCapacity
            );
            personal = Clamp(personal, 0, personalCapacity);

            long difference = (long)after - before;
            if (difference > 0)
            {
                int sharedRoom = Math.Max(0, sharedCapacity - shared);
                int sharedGain = (int)Math.Min(difference, sharedRoom);
                shared += sharedGain;
                long personalGain = difference - sharedGain;
                personal = (int)Math.Min(
                    personalCapacity,
                    personal + personalGain
                );
            }
            else if (difference < 0)
            {
                long remainingCost = -difference;
                int personalCost = (int)Math.Min(
                    remainingCost,
                    personal
                );
                personal -= personalCost;
                remainingCost -= personalCost;
                shared = (int)Math.Max(
                    MinimumSharedBalance,
                    (long)shared - remainingCost
                );
            }

            return new[] { shared, personal };
        }

        internal static int ComposeVisibleCurrency(
            int shared,
            int personal,
            int currentMaximum)
        {
            long visible = Math.Max(0, shared) + (long)Math.Max(0, personal);
            return (int)Math.Min(
                Math.Max(0, currentMaximum),
                visible
            );
        }

        internal static int CalculateInitialSharedValue(
            int current,
            int personal,
            int offlineBaseline,
            bool needsOfflineReconciliation,
            int sharedCapacity)
        {
            int initialVisible = needsOfflineReconciliation &&
                offlineBaseline >= 0
                    ? offlineBaseline
                    : current;
            return Clamp(
                initialVisible - Math.Max(0, personal),
                0,
                Math.Max(0, sharedCapacity)
            );
        }

        private static int GetVisibleValue(
            LinkState state,
            PlayerData playerData)
        {
            return ComposeVisibleCurrency(
                GetPredictedShared(state),
                state.PrivateAmount,
                GetCurrentMaximum(state, playerData)
            );
        }

        private static void ApplyLinkedValue(
            LinkState state,
            PlayerData playerData,
            bool refreshCounter)
        {
            if (playerData == null ||
                IsTemporarilyStored(state, playerData))
            {
                return;
            }

            int desired = GetVisibleValue(state, playerData);
            int current = GetCurrent(state, playerData);
            if (current != desired)
            {
                SetCurrent(state, playerData, desired);
                if (refreshCounter)
                {
                    RefreshCounter(state, desired);
                }
            }

            // The native memory exit restores this saved value after the
            // current scene is unloaded. Mirror even when the live field was
            // already equal so a remote linked update cannot revert.
            MemorySequenceSync.MirrorCurrency(
                playerData,
                state.CurrencyType,
                desired
            );
            state.LastObserved = desired;
            StoreBaseline(state, desired);
        }

        private static bool SendSharedDelta(LinkState state, int delta)
        {
            return SendSharedDelta(
                state,
                delta,
                MinimumSharedBalance,
                StorageMutationPurpose.Ordinary,
                0
            );
        }

        private static bool SendSharedDelta(
            LinkState state,
            int delta,
            int minimumSharedBalance,
            StorageMutationPurpose purpose,
            int transactionId)
        {
            ArchipelagoSession currentSession;
            int currentGeneration;
            lock (QueueLock)
            {
                if (!state.Enabled || session == null ||
                    !state.StorageStarted)
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
                        state.Kind,
                        purpose,
                        transactionId,
                        originalValue,
                        newValue,
                        true,
                        delta
                    );

            state.PendingSharedDelta += delta;
            try
            {
                currentSession.DataStorage[Scope.Team, state.StorageKey] =
                    currentSession.DataStorage[Scope.Team, state.StorageKey] +
                    delta +
                    Operation.Max(minimumSharedBalance) +
                    Operation.Min(state.SharedCapacity) +
                    Callback.Add(callback);
                return true;
            }
            catch (Exception exception)
            {
                state.PendingSharedDelta -= delta;
                state.Enabled = false;
                Unsubscribe(state);
                state.SubscribedElement = null;
                state.SubscribedHandler = null;
                state.StorageStarted = false;
                state.SharedValueReady = false;
                QueueStatus(
                    state.DisplayName +
                    " paused after a shared-pool update failed; your " +
                    "local currency change was preserved and will be " +
                    "reconciled after reconnecting. " + exception.Message
                );
                return false;
            }
        }

        private static void QueueStorageValue(
            int callbackGeneration,
            LinkedCurrency kind,
            StorageMutationPurpose purpose,
            int transactionId,
            JToken originalValue,
            JToken value,
            bool isAcknowledgement,
            int requestedDelta)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                QueueStatus(
                    GetState(kind).DisplayName +
                    " ignored an empty shared-pool update."
                );
                return;
            }

            int parsedValue;
            try
            {
                parsedValue = value.ToObject<int>();
            }
            catch (Exception exception)
            {
                QueueStatus(
                    GetState(kind).DisplayName +
                    " received an invalid shared value: " +
                    exception.Message
                );
                return;
            }

            bool hasOriginalValue = false;
            int parsedOriginalValue = 0;
            if (originalValue != null &&
                originalValue.Type != JTokenType.Null)
            {
                try
                {
                    parsedOriginalValue = originalValue.ToObject<int>();
                    hasOriginalValue = true;
                }
                catch
                {
                    // The resulting value is still valid. A death
                    // acknowledgement without an original value safely
                    // confirms zero recoverable Rosaries below.
                }
            }

            lock (QueueLock)
            {
                if (callbackGeneration != generation)
                {
                    return;
                }

                StorageUpdates.Enqueue(new QueuedStorageUpdate
                {
                    Generation = callbackGeneration,
                    Kind = kind,
                    Purpose = purpose,
                    TransactionId = transactionId,
                    HasOriginalValue = hasOriginalValue,
                    OriginalValue = parsedOriginalValue,
                    Value = parsedValue,
                    IsAcknowledgement = isAcknowledgement,
                    RequestedDelta = requestedDelta,
                });
            }
        }

        private static int GetPredictedShared(LinkState state)
        {
            long predicted =
                (long)state.AuthoritativeShared + state.PendingSharedDelta;
            return (int)Math.Min(
                state.SharedCapacity,
                Math.Max(MinimumSharedBalance, predicted)
            );
        }

        private static int GetPrivateCapacity(
            LinkState state,
            PlayerData playerData)
        {
            if (state.Kind != LinkedCurrency.ShellShards ||
                playerData == null)
            {
                return 0;
            }

            return Math.Max(
                0,
                GetCurrentMaximum(state, playerData) -
                    ShellShardSharedCapacity
            );
        }

        private static int GetCurrentMaximum(
            LinkState state,
            PlayerData playerData)
        {
            try
            {
                return GlobalSettings.Gameplay.GetCurrencyCap(
                    state.CurrencyType
                );
            }
            catch
            {
                return state.SharedCapacity +
                    (state.Kind == LinkedCurrency.ShellShards &&
                     playerData != null
                        ? Math.Max(0, playerData.ToolPouchUpgrades) * 100
                        : 0);
            }
        }

        private static int GetCurrent(
            LinkState state,
            PlayerData playerData)
        {
            return state.Kind == LinkedCurrency.Rosaries
                ? playerData.geo
                : playerData.ShellShards;
        }

        private static void SetCurrent(
            LinkState state,
            PlayerData playerData,
            int value)
        {
            if (state.Kind == LinkedCurrency.Rosaries)
            {
                playerData.geo = value;
            }
            else
            {
                playerData.ShellShards = value;
            }
        }

        private static int GetTemporarilyStored(
            LinkState state,
            PlayerData playerData)
        {
            return state.Kind == LinkedCurrency.Rosaries
                ? playerData.TempGeoStore
                : playerData.TempShellShardStore;
        }

        private static bool IsTemporarilyStored(
            LinkState state,
            PlayerData playerData)
        {
            return playerData != null &&
                   (state.TemporaryStorageActive ||
                    GetTemporarilyStored(state, playerData) > 0);
        }

        private static bool IsMemorySuspended(PlayerData playerData)
        {
            return playerData != null && playerData.HasStoredMemoryState;
        }

        private static bool IsOptionEnabled(
            LinkState state,
            SaveState saveState)
        {
            return saveState != null &&
                   (state.Kind == LinkedCurrency.Rosaries
                       ? saveState.rosaryLink
                       : saveState.shellShardLink);
        }

        private static LinkState GetState(LinkedCurrency kind)
        {
            return kind == LinkedCurrency.Rosaries
                ? Rosaries
                : ShellShards;
        }

        private static void StorePrivateAmount(LinkState state)
        {
            if (state.Kind == LinkedCurrency.ShellShards &&
                boundSaveState != null &&
                boundSaveState.shellShardLink)
            {
                boundSaveState.shellShardLinkPrivateShards =
                    Math.Max(0, state.PrivateAmount);
            }
        }

        private static int GetSavedBaseline(
            LinkState state,
            SaveState saveState)
        {
            if (saveState == null)
            {
                return -1;
            }

            return state.Kind == LinkedCurrency.Rosaries
                ? saveState.rosaryLinkLastSyncedBalance
                : saveState.shellShardLinkLastSyncedBalance;
        }

        private static void StoreBaseline(LinkState state, int value)
        {
            if (boundSaveState == null ||
                !IsOptionEnabled(state, boundSaveState))
            {
                return;
            }

            value = Math.Max(0, value);
            if (state.Kind == LinkedCurrency.Rosaries)
            {
                boundSaveState.rosaryLinkLastSyncedBalance = value;
            }
            else
            {
                boundSaveState.shellShardLinkLastSyncedBalance = value;
            }
        }

        private static bool TryGetCapturedPreDeathBalance(
            PlayerData playerData,
            out int balance)
        {
            balance = 0;
            if (activeRosaryCocoonSequence == 0 ||
                !RosaryDeaths.TryGetValue(
                    activeRosaryCocoonSequence,
                    out RosaryDeathRecord death) ||
                death.DebitSent ||
                !ReferenceEquals(death.PlayerData, playerData))
            {
                return false;
            }

            balance = death.Before;
            return true;
        }

        private static void MarkRosaryDeathStarted()
        {
            if (!Rosaries.Enabled ||
                !PlayerData.HasInstance ||
                DeathLinkManager.IsRemoteCocoonProtectionInFlight)
            {
                return;
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return;
            }

            if (activeRosaryCocoonSequence != 0 &&
                RosaryDeaths.TryGetValue(
                    activeRosaryCocoonSequence,
                    out RosaryDeathRecord previousDeath))
            {
                // Native death replaces an unrecovered prior cocoon. Its
                // already-confirmed debit remains spent, but it is no longer
                // recoverable.
                if (previousDeath.NativeLoss < 0)
                {
                    previousDeath.NativeLoss =
                        CurrencyLinkAccounting.CalculateNativeDeathLoss(
                            previousDeath.Before,
                            playerData.geo,
                            playerData.HeroCorpseMoneyPool
                        );
                }

                previousDeath.CocoonAvailable = false;
                if (previousDeath.DebitConfirmed &&
                    !previousDeath.RecoveryRequested)
                {
                    RosaryDeaths.Remove(previousDeath.Sequence);
                }
            }

            int sequence = ++nextRosaryDeathSequence;
            if (sequence <= 0)
            {
                nextRosaryDeathSequence = 1;
                sequence = 1;
            }

            RosaryDeathRecord death = new RosaryDeathRecord
            {
                Sequence = sequence,
                PlayerData = playerData,
                Before = Math.Max(0, playerData.geo),
                SuppressSharedMutation =
                    DeathLinkManager.IsRemoteDeathApplicationInFlight,
            };
            RosaryDeaths[sequence] = death;
            activeRosaryCocoonSequence = sequence;

            if (ReferenceEquals(playerData, boundPlayerData))
            {
                // The pre-death observation remains even when DeathLink runs
                // before this manager's Update in the same Unity frame.
                Rosaries.LastObserved = death.Before;
            }
        }

        private static void CaptureNativeRosaryDeathResult()
        {
            if (DeathLinkManager.IsRemoteCocoonProtectionInFlight ||
                activeRosaryCocoonSequence == 0 ||
                !PlayerData.HasInstance ||
                !RosaryDeaths.TryGetValue(
                    activeRosaryCocoonSequence,
                    out RosaryDeathRecord death) ||
                death.DebitSent)
            {
                return;
            }

            PlayerData playerData = PlayerData.instance;
            if (!ReferenceEquals(playerData, death.PlayerData))
            {
                return;
            }

            death.NativeLoss =
                CurrencyLinkAccounting.CalculateNativeDeathLoss(
                    death.Before,
                    playerData.geo,
                    playerData.HeroCorpseMoneyPool
                );
            if (death.SuppressSharedMutation)
            {
                // CheckDeathCatch's StartCoroutine runs Die through its first
                // yield before returning, so the native pool now exists. Zero
                // a received DeathLink cocoon immediately, even if the socket
                // closes before Currency Link's next Update.
                playerData.HeroCorpseMoneyPool = 0;
            }
        }

        private static bool TryProcessRosaryDeath(
            LinkState state,
            PlayerData playerData)
        {
            if (TryCreditPendingRosaryRecovery())
            {
                return true;
            }

            RosaryDeathRecord death = null;
            if (activeRosaryCocoonSequence != 0)
            {
                RosaryDeaths.TryGetValue(
                    activeRosaryCocoonSequence,
                    out death
                );
                if (death != null &&
                    (death.DebitSent ||
                     !ReferenceEquals(death.PlayerData, playerData)))
                {
                    death = null;
                }
            }

            if (death == null)
            {
                foreach (RosaryDeathRecord candidate in
                         RosaryDeaths.Values)
                {
                    if (!candidate.DebitSent &&
                        ReferenceEquals(candidate.PlayerData, playerData))
                    {
                        death = candidate;
                        break;
                    }
                }
            }

            if (death == null)
            {
                return false;
            }

            int current = GetCurrent(state, playerData);
            if (death.NativeLoss < 0)
            {
                death.NativeLoss =
                    CurrencyLinkAccounting.CalculateNativeDeathLoss(
                        death.Before,
                        current,
                        playerData.HeroCorpseMoneyPool
                    );
            }

            state.LastObserved = current;
            int requestedDebit =
                CurrencyLinkAccounting.CalculateDeathDebit(
                    GetPredictedShared(state),
                    death.NativeLoss,
                    death.SuppressSharedMutation
                );
            death.RequestedDebit = requestedDebit;
            if (death.SuppressSharedMutation)
            {
                // The source player's natural death owns the shared debit.
                // Charging every received DeathLink would debit one shared
                // team balance once per participant in the cascade.
                death.DebitSent = true;
                death.DebitConfirmed = true;
                death.RecoverableAmount = 0;
                SetTrackedCocoonAmount(death, 0);
                ApplyLinkedValue(state, playerData, true);
                if (death.RecoveryRequested || !death.CocoonAvailable)
                {
                    RosaryDeaths.Remove(death.Sequence);
                }

                return true;
            }

            if (requestedDebit <= 0)
            {
                death.DebitSent = true;
                death.DebitConfirmed = true;
                death.RecoverableAmount = 0;
                SetTrackedCocoonAmount(death, 0);
                ApplyLinkedValue(state, playerData, true);
                if (death.RecoveryRequested || !death.CocoonAvailable)
                {
                    RosaryDeaths.Remove(death.Sequence);
                }

                return true;
            }

            if (!SendSharedDelta(
                    state,
                    -requestedDebit,
                    0,
                    StorageMutationPurpose.RosaryDeathDebit,
                    death.Sequence))
            {
                // The reconnect baseline remains in use when a synchronous
                // send fails. The native cocoon also remains in that path.
                death.CocoonAvailable = false;
                if (activeRosaryCocoonSequence == death.Sequence)
                {
                    activeRosaryCocoonSequence = 0;
                }

                RosaryDeaths.Remove(death.Sequence);
                return true;
            }

            death.DebitSent = true;
            SetTrackedCocoonAmount(death, requestedDebit);
            ApplyLinkedValue(state, playerData, true);
            return true;
        }

        private static void ConfirmRosaryDeathDebit(
            int sequence,
            int originalShared,
            int resultingShared)
        {
            if (!RosaryDeaths.TryGetValue(
                    sequence,
                    out RosaryDeathRecord death))
            {
                return;
            }

            death.DebitConfirmed = true;
            death.RecoverableAmount =
                CurrencyLinkAccounting.CalculateConfirmedDeathDebit(
                    death.RequestedDebit,
                    originalShared,
                    resultingShared
                );
            SetTrackedCocoonAmount(
                death,
                death.RecoverableAmount
            );

            if (death.RecoveryRequested)
            {
                TryCreditRosaryRecovery(death);
            }
            else if (!death.CocoonAvailable)
            {
                RosaryDeaths.Remove(death.Sequence);
            }
        }

        private static void SetTrackedCocoonAmount(
            RosaryDeathRecord death,
            int amount)
        {
            if (death == null ||
                !death.CocoonAvailable ||
                death.RecoveryRequested ||
                activeRosaryCocoonSequence != death.Sequence ||
                death.PlayerData == null)
            {
                return;
            }

            death.PlayerData.HeroCorpseMoneyPool = Math.Max(0, amount);
        }

        private static void BeginRosaryCocoonRecovery(
            out RosaryCocoonMutationState mutation)
        {
            mutation = default(RosaryCocoonMutationState);
            if (!Rosaries.Enabled ||
                activeRosaryCocoonSequence == 0 ||
                !PlayerData.HasInstance ||
                !RosaryDeaths.TryGetValue(
                    activeRosaryCocoonSequence,
                    out RosaryDeathRecord death) ||
                !death.CocoonAvailable)
            {
                return;
            }

            PlayerData playerData = PlayerData.instance;
            if (!ReferenceEquals(playerData, death.PlayerData))
            {
                return;
            }

            int nativeAmount = Math.Max(
                0,
                playerData.HeroCorpseMoneyPool
            );
            if (death.NativeLoss < 0)
            {
                death.NativeLoss =
                    CurrencyLinkAccounting.CalculateNativeDeathLoss(
                        death.Before,
                        playerData.geo,
                        nativeAmount
                    );
            }

            mutation = new RosaryCocoonMutationState
            {
                Active = true,
                Sequence = death.Sequence,
                OriginalNativeAmount = nativeAmount,
            };
            death.RecoveryRequested = true;

            // Native CocoonBroken clears this field and feeds it through
            // AddGeo. Zero it first so only the confirmed shared debit below
            // can be credited, exactly once.
            playerData.HeroCorpseMoneyPool = 0;
        }

        private static void EndRosaryCocoonRecovery(
            RosaryCocoonMutationState mutation,
            Exception exception)
        {
            if (!mutation.Active ||
                !RosaryDeaths.TryGetValue(
                    mutation.Sequence,
                    out RosaryDeathRecord death))
            {
                return;
            }

            if (exception != null)
            {
                death.RecoveryRequested = false;
                if (death.PlayerData != null)
                {
                    death.PlayerData.HeroCorpseMoneyPool =
                        mutation.OriginalNativeAmount;
                }

                return;
            }

            death.CocoonAvailable = false;
            if (activeRosaryCocoonSequence == death.Sequence)
            {
                activeRosaryCocoonSequence = 0;
            }

            if (death.DebitConfirmed)
            {
                TryCreditRosaryRecovery(death);
            }
        }

        private static bool TryCreditPendingRosaryRecovery()
        {
            List<RosaryDeathRecord> snapshot =
                new List<RosaryDeathRecord>(RosaryDeaths.Values);
            foreach (RosaryDeathRecord death in snapshot)
            {
                if (death.RecoveryRequested &&
                    death.DebitConfirmed &&
                    !death.RecoveryCredited &&
                    TryCreditRosaryRecovery(death))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryCreditRosaryRecovery(
            RosaryDeathRecord death)
        {
            if (death == null ||
                !death.RecoveryRequested ||
                !death.DebitConfirmed ||
                death.RecoveryCredited)
            {
                return false;
            }

            int amount = Math.Max(0, death.RecoverableAmount);
            if (amount == 0)
            {
                death.RecoveryCredited = true;
                RosaryDeaths.Remove(death.Sequence);
                return true;
            }

            if (!Rosaries.Enabled ||
                !Rosaries.SharedValueReady ||
                death.PlayerData == null ||
                !ReferenceEquals(death.PlayerData, boundPlayerData) ||
                IsTemporarilyStored(Rosaries, death.PlayerData))
            {
                return false;
            }

            try
            {
                death.RecoveryCredited = true;
                CurrencyManager.AddCurrency(
                    amount,
                    CurrencyType.Money,
                    true
                );
                RosaryDeaths.Remove(death.Sequence);
                return true;
            }
            catch (Exception exception)
            {
                death.RecoveryCredited = false;
                QueueStatus(
                    "Rosary Link could not restore its confirmed death " +
                    "cocoon yet: " + exception.Message
                );
                return false;
            }
        }

        private static void RefreshCounter(LinkState state, int value)
        {
            try
            {
                CurrencyCounter.ToValue(value, state.CurrencyType);
            }
            catch (Exception exception)
            {
                LogDirect(
                    state.DisplayName +
                    " could not refresh its currency display: " +
                    exception.Message,
                    true
                );
            }
        }

        private static void MarkTemporaryStoreApplied()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null ||
                !ReferenceEquals(playerData, boundPlayerData))
            {
                return;
            }

            MarkTemporaryStoreApplied(Rosaries, playerData);
            MarkTemporaryStoreApplied(ShellShards, playerData);
        }

        private static void MarkTemporaryStoreApplied(
            LinkState state,
            PlayerData playerData)
        {
            if (!state.Enabled)
            {
                return;
            }

            state.TemporaryStorageActive = true;
            if (!state.SharedValueReady)
            {
                return;
            }

            state.WasTemporarilyStored = true;
            state.LastObserved = GetCurrent(state, playerData);
        }

        private static void MarkTemporaryRestoreApplied()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null ||
                !ReferenceEquals(playerData, boundPlayerData))
            {
                return;
            }

            MarkTemporaryRestoreApplied(Rosaries, playerData);
            MarkTemporaryRestoreApplied(ShellShards, playerData);
        }

        private static void MarkTemporaryRestoreApplied(
            LinkState state,
            PlayerData playerData)
        {
            if (!state.Enabled)
            {
                return;
            }

            state.TemporaryStorageActive = false;
            if (!state.SharedValueReady)
            {
                return;
            }

            state.LastObserved = GetCurrent(state, playerData);
            state.WasTemporarilyStored = true;
        }

        private static void MarkMemorySnapshotApplied()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null ||
                !ReferenceEquals(playerData, boundPlayerData))
            {
                return;
            }

            MarkMemorySnapshotApplied(Rosaries, playerData);
            MarkMemorySnapshotApplied(ShellShards, playerData);
        }

        private static void MarkMemorySnapshotApplied(
            LinkState state,
            PlayerData playerData)
        {
            if (!state.Enabled || !state.SharedValueReady)
            {
                return;
            }

            state.LastObserved = GetCurrent(state, playerData);
            // HeroItemsState.Apply has already cleared IsRecorded, but
            // GameManager clears HasStoredMemoryState immediately after this
            // postfix. The applied value is an observation rather than a new
            // local spend or gain on the following frame.
        }

        private static void Unsubscribe(LinkState state)
        {
            if (state.SubscribedElement == null ||
                state.SubscribedHandler == null)
            {
                return;
            }

            try
            {
                state.SubscribedElement.OnValueChanged -=
                    state.SubscribedHandler;
            }
            catch (Exception exception)
            {
                LogDirect(
                    state.DisplayName +
                    " cleanup warning: " + exception.Message,
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

        private static void LogRosaryCannonReloadDiagnostic(
            ToolItem tool,
            bool canReload)
        {
            SaveState saveState = SaveState.Instance;
            if (tool == null ||
                !Rosaries.Enabled ||
                saveState == null ||
                !saveState.rosaryLink ||
                !string.Equals(
                    tool.name,
                    "Rosary Cannon",
                    StringComparison.Ordinal
                ))
            {
                return;
            }

            long now = DateTime.UtcNow.Ticks;
            if (now < nextRosaryCannonReloadDiagnosticTicks)
            {
                return;
            }

            nextRosaryCannonReloadDiagnosticTicks =
                now + RosaryCannonReloadDiagnosticIntervalTicks;

            try
            {
                PlayerData playerData = PlayerData.instance;
                ToolItemsData.Data savedData = tool.SavedData;
                int storage = ToolItemManager.GetToolStorageAmount(tool);
                bool memorySuspended = IsMemorySuspended(playerData);
                bool temporarilyStored =
                    IsTemporarilyStored(Rosaries, playerData);

                LogDirect(
                    "Rosary Cannon reload diagnostic: canReload=" +
                    canReload +
                    ", amountLeft=" + savedData.AmountLeft +
                    ", storage=" + storage +
                    ", nativeGeo=" +
                    (playerData == null ? -1 : playerData.geo) +
                    ", tempGeo=" +
                    (playerData == null ? -1 : playerData.TempGeoStore) +
                    ", memorySuspended=" + memorySuspended +
                    ", temporarilyStored=" + temporarilyStored +
                    ", temporaryActive=" +
                    Rosaries.TemporaryStorageActive +
                    ", storageStarted=" + Rosaries.StorageStarted +
                    ", sharedReady=" + Rosaries.SharedValueReady +
                    ", authoritative=" + Rosaries.AuthoritativeShared +
                    ", pending=" + Rosaries.PendingSharedDelta +
                    ", predicted=" + GetPredictedShared(Rosaries) +
                    ", lastObserved=" + Rosaries.LastObserved +
                    ", boundSave=" +
                    ReferenceEquals(saveState, boundSaveState) +
                    ", boundPlayer=" +
                    ReferenceEquals(playerData, boundPlayerData) +
                    ", ignoredMutationDepth=" +
                    ignoredLocalMutationDepth,
                    !canReload
                );
            }
            catch (Exception exception)
            {
                LogDirect(
                    "Rosary Cannon reload diagnostic could not read its " +
                    "runtime state: " + exception.Message,
                    true
                );
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Min(Math.Max(value, minimum), maximum);
        }

        [HarmonyPatch(
            typeof(ToolItem),
            nameof(ToolItem.CanReload),
            new Type[0])]
        internal static class ToolItem_CanReload_RosaryLink_Diagnostic_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(
                ToolItem __instance,
                bool __result)
            {
                LogRosaryCannonReloadDiagnostic(__instance, __result);
            }
        }

        [HarmonyPatch(
            typeof(HeroController),
            "Die",
            new Type[] { typeof(bool), typeof(bool) })]
        internal static class HeroController_Die_RosaryLink_Patch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                MarkRosaryDeathStarted();
            }
        }

        [HarmonyPatch(
            typeof(HeroController),
            nameof(HeroController.CheckDeathCatch))]
        internal static class HeroController_CheckDeathCatch_RosaryLink_Patch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                CaptureNativeRosaryDeathResult();
            }
        }

        [HarmonyPatch(
            typeof(HeroController),
            nameof(HeroController.CocoonBroken),
            new Type[] { typeof(bool), typeof(bool) })]
        internal static class HeroController_CocoonBroken_RosaryLink_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(
                out RosaryCocoonMutationState __state)
            {
                BeginRosaryCocoonRecovery(out __state);
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(
                Exception __exception,
                RosaryCocoonMutationState __state)
            {
                EndRosaryCocoonRecovery(__state, __exception);
                return __exception;
            }
        }

        [HarmonyPatch(
            typeof(PlayerData),
            nameof(PlayerData.AddGeo),
            new Type[] { typeof(int) })]
        internal static class PlayerData_AddGeo_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(
                PlayerData __instance,
                out LocalMutationState __state)
            {
                BeginLocalMutation(Rosaries, __instance, out __state);
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
            nameof(PlayerData.TakeGeo),
            new Type[] { typeof(int) })]
        internal static class PlayerData_TakeGeo_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(
                PlayerData __instance,
                out LocalMutationState __state)
            {
                BeginLocalMutation(Rosaries, __instance, out __state);
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
            nameof(PlayerData.AddShards),
            new Type[] { typeof(int) })]
        internal static class PlayerData_AddShards_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(
                PlayerData __instance,
                out LocalMutationState __state)
            {
                BeginLocalMutation(ShellShards, __instance, out __state);
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
            nameof(PlayerData.TakeShards),
            new Type[] { typeof(int) })]
        internal static class PlayerData_TakeShards_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(
                PlayerData __instance,
                out LocalMutationState __state)
            {
                BeginLocalMutation(ShellShards, __instance, out __state);
            }

            [HarmonyPostfix]
            private static void Postfix(
                PlayerData __instance,
                LocalMutationState __state)
            {
                EndLocalMutation(__instance, __state);
            }
        }

        [HarmonyPatch(typeof(CurrencyManager), "TempStoreCurrency")]
        internal static class CurrencyManager_TempStoreCurrency_Patch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                MarkTemporaryStoreApplied();
            }
        }

        [HarmonyPatch(typeof(CurrencyManager), "RestoreTempStoredCurrency")]
        internal static class CurrencyManager_RestoreTempStoredCurrency_Patch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                ignoredLocalMutationDepth++;
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(Exception __exception)
            {
                ignoredLocalMutationDepth = Math.Max(
                    0,
                    ignoredLocalMutationDepth - 1
                );
                if (__exception == null)
                {
                    MarkTemporaryRestoreApplied();
                }

                return __exception;
            }
        }

        [HarmonyPatch(typeof(HeroItemsState), nameof(HeroItemsState.Apply))]
        internal static class HeroItemsState_Apply_Patch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                MarkMemorySnapshotApplied();
            }
        }
    }
}
