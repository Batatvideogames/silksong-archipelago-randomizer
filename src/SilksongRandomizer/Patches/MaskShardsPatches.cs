using HarmonyLib;
using HutongGames.PlayMaker;
using System;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class MaskShardsPatches
    {
        private const int BaseHealth = 5;
        private const int ShardsPerMask = 4;
        private const int TotalMaskShards = 20;
        private const string HeartPieceAssetName = "Heart Piece";
        private const string HeartPieceControlFsmName =
            "Heart Container Control";
        private const string LooseMaskPickupStateName = "Shift Up?";

        private struct InventoryState
        {
            internal bool Active;
            internal int HeartPieces;
            internal int MaxHealthBase;
        }

        internal static int CountMaskShards()
        {
            SaveState state = SaveState.Instance;
            if (state == null)
            {
                return 0;
            }

            int maskShards = 0;
            for (int i = 1; i <= TotalMaskShards; i++)
            {
                if (state.receivedItems.Contains("Mask Shard #" + i))
                {
                    maskShards++;
                }
            }

            return maskShards;
        }

        internal static int GetReceivedMaxHealth()
        {
            return BaseHealth + CountMaskShards() / ShardsPerMask;
        }

        internal static int GetReceivedHeartPieces()
        {
            return CountMaskShards() % ShardsPerMask;
        }

        /// <summary>
        /// Keeps the game's raw health fields aligned with AP-owned Mask Shards.
        /// A newly completed group behaves like vanilla AddToMaxHealth and heals
        /// Hornet to the new maximum. Loading an existing save only reconciles
        /// and clamps. It does not provide a free heal.
        /// </summary>
        internal static bool SynchronizeReceivedMaskShards(
            bool refillNewMask)
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                !state.IsRandomized(ItemType.MaskShard) ||
                playerData == null)
            {
                return false;
            }

            int receivedCount = CountMaskShards();
            int desiredHeartPieces = receivedCount % ShardsPerMask;
            int desiredMaxHealth =
                BaseHealth + receivedCount / ShardsPerMask;
            bool maskStateChanged =
                playerData.heartPieces != desiredHeartPieces ||
                playerData.maxHealthBase != desiredMaxHealth ||
                playerData.maxHealth != desiredMaxHealth;
            bool refilledNewMask =
                refillNewMask &&
                receivedCount > 0 &&
                receivedCount % ShardsPerMask == 0;

            // heartPieces is the current partial mask (0-3), not the
            // lifetime shard total. Keeping the total here makes the
            // inventory clamp to a misleading permanent 4/4 Ancient Mask.
            playerData.heartPieces = desiredHeartPieces;
            playerData.maxHealthBase = desiredMaxHealth;
            playerData.maxHealth = desiredMaxHealth;

            if (refilledNewMask)
            {
                playerData.prevHealth = playerData.health;
                playerData.health = desiredMaxHealth;
            }
            else if (playerData.health > desiredMaxHealth)
            {
                playerData.health = desiredMaxHealth;
            }

            // The memory scene starts from a temporary full-health copy, so
            // a live before/after delta cannot represent the permanent mask
            // reward. A completed mask explicitly owns a full heal. Other
            // shards only constrain an already-recorded value to the new cap.
            MemorySequenceSync.SynchronizeMaskHealth(
                playerData,
                desiredMaxHealth,
                refilledNewMask
            );

            if (refilledNewMask)
            {
                // The vanilla Heart Piece FSM sends this after adding a full
                // mask. Randomized shards bypass that FSM, so mirror its HUD
                // signal after applying the AP-owned health fields.
                EventRegister.SendEvent("MAX HP UP");
            }

            if (maskStateChanged)
            {
                // A load or a large precollected inventory can reconcile the
                // raw PlayerData fields after the HUD has initialized. Force
                // the native health display to reread the corrected maximum.
                EventRegister.SendEvent(EventRegisterEvents.HealthUpdate);
            }

            return true;
        }

        private static bool IsRandomizedHeartPiece(
            PrefabCollectable item)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsRandomized(ItemType.MaskShard) &&
                   item != null &&
                   item.name.StartsWith(
                       HeartPieceAssetName,
                       StringComparison.Ordinal
                   );
        }

        /// <summary>
        /// Loose world Mask Shards are scene objects rather than
        /// PrefabCollectables. Their stable identity is the scene,
        /// root object/PersistentBool ID and Heart Container Control FSM.
        /// </summary>
        private static bool TryGetLoosePhysicalLocation(
            string sceneName,
            string gameObjectName,
            string fsmName,
            out string locationName)
        {
            locationName = null;
            if (!string.Equals(
                    fsmName,
                    HeartPieceControlFsmName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return MaskAndSpoolLocationManifest.TryGetPhysicalLocation(
                ItemType.MaskShard,
                sceneName,
                gameObjectName,
                out locationName
            );
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        internal static class LoosePhysicalMaskShardPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(PlayMakerFSM __instance)
            {
                SaveState state = SaveState.Instance;
                if (__instance == null ||
                    state == null ||
                    !state.IsRandomized(ItemType.MaskShard) ||
                    !TryGetLoosePhysicalLocation(
                        __instance.gameObject.scene.name,
                        __instance.name,
                        __instance.FsmName,
                        out string locationName) ||
                    !state.IsLocationEnabled(locationName) ||
                    !state.IsLocationInSeed(locationName))
                {
                    return;
                }

                // A source without a durable save identity remains vanilla.
                // This avoids creating a check which can reappear after reload.
                if (__instance.GetComponent<PersistentBoolItem>() == null)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Physical Mask Shard patch found " +
                        locationName + " without its PersistentBoolItem."
                    );
                    return;
                }

                // Shift Up? is the first state after the pickup trigger and
                // precedes the native AddHeroInputBlocker action. Replacing a
                // later Get state would leave that blocker active forever.
                FsmState pickupState = FindState(
                    __instance,
                    LooseMaskPickupStateName
                );
                if (pickupState == null)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Physical Mask Shard patch found " +
                        locationName +
                        " but not its shipped Shift Up? pickup state."
                    );
                    return;
                }

                FsmStateAction[] existingActions = pickupState.Actions;
                if (existingActions != null &&
                    existingActions.Length == 1 &&
                    existingActions[0] is
                        CompleteLooseMaskShardLocation)
                {
                    return;
                }

                CompleteLooseMaskShardLocation replacement =
                    new CompleteLooseMaskShardLocation(locationName);
                replacement.Init(pickupState);
                pickupState.Actions = new FsmStateAction[]
                {
                    replacement,
                };
            }
        }

        private static FsmState FindState(
            PlayMakerFSM fsm,
            string stateName)
        {
            FsmState[] states = fsm == null ? null : fsm.FsmStates;
            if (states == null)
            {
                return null;
            }

            foreach (FsmState state in states)
            {
                if (state != null &&
                    string.Equals(
                        state.Name,
                        stateName,
                        StringComparison.Ordinal))
                {
                    return state;
                }
            }

            return null;
        }

        private sealed class CompleteLooseMaskShardLocation :
            FsmStateAction
        {
            private readonly string locationName;

            internal CompleteLooseMaskShardLocation(
                string locationName)
            {
                this.locationName = locationName;
            }

            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.MaskShard) ||
                    !state.IsLocationEnabled(locationName) ||
                    !state.IsLocationInSeed(locationName) ||
                    Owner == null)
                {
                    return;
                }

                PersistentBoolItem persistence =
                    Owner.GetComponent<PersistentBoolItem>();
                if (persistence == null)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Physical Mask Shard lost its " +
                        "PersistentBoolItem before collection: " +
                        locationName + "."
                    );
                    return;
                }

                // The exact native source identity remains while the vanilla
                // Heart Piece animation, hero-control takeover, heartPieces
                // mutation and misleading reward UI are skipped.
                FsmBool activated = Fsm == null
                    ? null
                    : Fsm.Variables.FindFsmBool("Activated");
                if (activated != null)
                {
                    activated.Value = true;
                }
                persistence.SetValueOverride(true);
                persistence.SaveStateNoCondition();

                state.CheckLocation(locationName);
                Owner.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(PlayerData), "get_CurrentMaxHealth", new Type[0])]
        internal static class PlayerData_CurrentMaxHealth_Patch
        {
            private static void Postfix(
                PlayerData __instance,
                ref int __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.MaskShard))
                {
                    return;
                }

                int receivedMaxHealth = GetReceivedMaxHealth();

                // Temporary native caps such as a bound-shell boss sequence remain.
                // Outside a cap, the received AP maximum applies.
                __result = __result < __instance.maxHealth
                    ? Math.Min(__result, receivedMaxHealth)
                    : receivedMaxHealth;
            }
        }

        [HarmonyPatch(
            typeof(PlayerData),
            nameof(PlayerData.TakeHealth),
            new Type[] { typeof(int), typeof(bool), typeof(bool) })]
        internal static class PlayerData_TakeHealth_Patch
        {
            private static void Prefix()
            {
                SynchronizeReceivedMaskShards(false);
            }
        }

        [HarmonyPatch(
            typeof(PrefabCollectable),
            nameof(PrefabCollectable.Get),
            new Type[] { typeof(bool) })]
        internal static class PhysicalMaskShardPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(PrefabCollectable __instance)
            {
                if (!IsRandomizedHeartPiece(__instance))
                {
                    return true;
                }

                GameManager gameManager = GameManager.instance;
                string sceneName = gameManager == null
                    ? null
                    : gameManager.GetSceneNameString();
                if (MaskAndSpoolLocationManifest.TryGetPhysicalLocation(
                        ItemType.MaskShard,
                        sceneName,
                        out string locationName))
                {
                    SaveState.Instance.CheckLocation(locationName);
                }

                // The exact Heart Piece PrefabCollectable would otherwise
                // instantiate Heart Piece Instant even with showPopup=false.
                // Its Heart Container Control FSM takes over Hornet, plays
                // Collect Heart Piece and applies the native mask reward.
                // The pickup/shop owns payment and persistence outside Get().
                // stable purchase flags are observed by the manifest poller.
                return false;
            }
        }

        [HarmonyPatch(
            typeof(PrefabCollectable),
            nameof(PrefabCollectable.TryGetPrespawnedItem))]
        internal static class PhysicalMaskShardPreSpawnPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                PrefabCollectable __instance,
                ref PreSpawnedItem item,
                ref bool __result)
            {
                if (!IsRandomizedHeartPiece(__instance))
                {
                    return true;
                }

                // Quest boards pre-instantiate PrefabCollectable rewards and
                // later activate the prefab after calling PreSpawnGet().
                // Returning no prespawn makes the board use Get() instead,
                // whose patch above reports/suppresses the randomized source.
                // Otherwise Heart Piece Instant still plays the native
                // empty-mask fill sequence and mutates native shard state.
                item = null;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(
            typeof(PrefabCollectable),
            nameof(PrefabCollectable.GetTakesHeroControl))]
        internal static class PhysicalMaskShardHeroControlPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(
                PrefabCollectable __instance,
                ref bool __result)
            {
                if (!IsRandomizedHeartPiece(__instance))
                {
                    return;
                }

                // The paired Get() prefix above suppresses the Heart Piece
                // prefab and its quest-board continuation event. Report that
                // the randomized reward does not take hero control so the
                // board runs its native delayed completion path instead of
                // waiting forever for the suppressed prefab to return it.
                __result = false;
            }
        }

        [HarmonyPatch(
            typeof(InventoryItemHeartPieces),
            "UpdateState",
            new Type[0])]
        internal static class InventoryMaskShardPatch
        {
            [HarmonyPrefix]
            private static void Prefix(out InventoryState __state)
            {
                __state = default(InventoryState);
                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.MaskShard) ||
                    playerData == null)
                {
                    return;
                }

                __state.Active = true;
                __state.HeartPieces = playerData.heartPieces;
                __state.MaxHealthBase = playerData.maxHealthBase;

                playerData.heartPieces = GetReceivedHeartPieces();
                playerData.maxHealthBase = GetReceivedMaxHealth();
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(
                Exception __exception,
                InventoryState __state)
            {
                PlayerData playerData = PlayerData.instance;
                if (__state.Active && playerData != null)
                {
                    playerData.heartPieces = __state.HeartPieces;
                    playerData.maxHealthBase = __state.MaxHealthBase;
                }

                return __exception;
            }
        }
    }
}
