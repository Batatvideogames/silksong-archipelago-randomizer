using HarmonyLib;
using HutongGames.PlayMaker;
using System;

namespace SilksongRandomizer.Patches
{
    internal static class SpoolFragmentPatches
    {
        private const int BaseSilkMax = 9;
        private const int FragmentsPerSpool = 2;
        private const int TotalSpoolFragments = 18;
        private const string SilkSpoolAssetName = "Silk Spool";
        private const string LooseSpoolFsmName = "Control";
        private static readonly string[] LooseSpoolPickupStateNames =
        {
            "Get",
            "Get And Shift Up",
        };

        internal static int CountSpoolFragments()
        {
            SaveState state = SaveState.Instance;
            if (state == null)
            {
                return 0;
            }

            int spoolFragments = 0;
            for (int i = 1; i <= TotalSpoolFragments; i++)
            {
                if (state.receivedItems.Contains("Spool Fragment #" + i))
                {
                    spoolFragments++;
                }
            }

            return spoolFragments;
        }

        /// <summary>
        /// Makes the two native spool-progress fields represent received AP
        /// fragments while that category is randomized. The shipped HUD does
        /// not use CurrentSilkMax exclusively: its Animate FSM reads silkMax
        /// directly before running the vanilla lengthen-spool path. Keeping
        /// these fields synchronized lets every native HUD path display the
        /// same value, including an entirely empty spool.
        /// Physical-source persistence remains separate and is never forged.
        /// </summary>
        internal static bool SynchronizeReceivedSpoolProgress(
            PlayerData playerData)
        {
            SaveState state = SaveState.Instance;
            if (playerData == null ||
                state == null ||
                !state.IsRandomized(ItemType.SpoolFragment))
            {
                return false;
            }

            int receivedCount = CountSpoolFragments();
            playerData.silkMax =
                BaseSilkMax + receivedCount / FragmentsPerSpool;
            playerData.silkSpoolParts =
                receivedCount % FragmentsPerSpool;
            return true;
        }

        internal static void RefreshReceivedSpoolHud()
        {
            // A received fragment should update the native state first, then
            // use the same DrawSpool path as vanilla. That path owns all base
            // frame pieces, the bind notch and the cursed/broken/final-
            // cutscene presentation branches. The separate Spool Extender
            // HUD follows the resulting base cap. If the persistent HUD does
            // not exist yet, its later DrawSpool call is covered by the
            // prefix below.
            try
            {
                PlayerData playerData = PlayerData.HasInstance
                    ? PlayerData.instance
                    : null;
                if (!SynchronizeReceivedSpoolProgress(playerData))
                {
                    return;
                }

                SilkSpool spool = SilkSpool.Instance;
                if (spool != null)
                {
                    // Normal vanilla HUD initialization and permanent
                    // capacity upgrades draw with zero offset. The equipped
                    // Spool Extender is its own HUD object which follows the
                    // base cap, so including its capacity here would extend
                    // the frame twice. Native special sequences which call
                    // DrawSpool(int) retain their own offset unchanged.
                    spool.DrawSpool();
                }
            }
            catch (Exception ex)
            {
                // HUD reconciliation must never interrupt the ordered AP item
                // queue. The CurrentSilkMaxBasic patch below remains a safe
                // gameplay-capacity fallback if scene UI is unavailable.
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Spool Fragment was applied, but native " +
                    "spool progress/HUD reconciliation failed: " + ex
                );
            }
        }

        [HarmonyPatch(
            typeof(SilkSpool),
            nameof(SilkSpool.DrawSpool),
            new Type[] { typeof(int) })]
        internal static class SilkSpool_DrawSpool_Patch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                // Idle initialization, curse recovery, max-up animation and
                // tool changes all converge here. Synchronize before native
                // code reads CurrentSilkMaxBasic so it calculates and owns the
                // complete frame geometry exactly as it does in vanilla.
                SynchronizeReceivedSpoolProgress(PlayerData.instance);
            }
        }

        private static bool IsRandomizedSilkSpool(
            PrefabCollectable item)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsRandomized(ItemType.SpoolFragment) &&
                   item != null &&
                   string.Equals(
                       item.name,
                       SilkSpoolAssetName,
                       StringComparison.Ordinal
                   );
        }

        /// <summary>
        /// The thirteen loose world Spool Fragments use scene objects driven
        /// entirely by a PlayMaker Control FSM, so PrefabCollectable.Get never
        /// runs for them. Scene + root object + FSM is their stable runtime
        /// identity. The scene and PersistentBool IDs are the same identities
        /// used by PlayerDataFixer.FixSilkSpools in the shipped game.
        /// </summary>
        private static bool TryGetLoosePhysicalLocation(
            string sceneName,
            string gameObjectName,
            string fsmName,
            out string locationName)
        {
            locationName = null;
            if (!string.Equals(
                    gameObjectName,
                    SilkSpoolAssetName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    fsmName,
                    LooseSpoolFsmName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return MaskAndSpoolLocationManifest.TryGetPhysicalLocation(
                ItemType.SpoolFragment,
                sceneName,
                out locationName
            );
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        internal static class LoosePhysicalSpoolFragmentPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(PlayMakerFSM __instance)
            {
                SaveState state = SaveState.Instance;
                if (__instance == null ||
                    state == null ||
                    !state.IsRandomized(ItemType.SpoolFragment) ||
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

                // A source without its shipped persistence component remains
                // vanilla. This avoids producing a check which can reappear
                // after a reload.
                if (__instance.GetComponent<PersistentBoolItem>() == null)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Physical Spool Fragment patch found " +
                        locationName + " without its PersistentBoolItem."
                    );
                    return;
                }

                int patchedStateCount = 0;
                foreach (string stateName in
                    LooseSpoolPickupStateNames)
                {
                    FsmState pickupState = FindState(
                        __instance,
                        stateName
                    );
                    if (pickupState == null)
                    {
                        continue;
                    }

                    FsmStateAction[] existingActions =
                        pickupState.Actions;
                    if (existingActions != null &&
                        existingActions.Length == 1 &&
                        existingActions[0] is
                            CompleteLooseSpoolLocation)
                    {
                        patchedStateCount++;
                        continue;
                    }

                    CompleteLooseSpoolLocation replacement =
                        new CompleteLooseSpoolLocation(locationName);
                    replacement.Init(pickupState);
                    pickupState.Actions = new FsmStateAction[]
                    {
                        replacement,
                    };
                    patchedStateCount++;
                }

                if (patchedStateCount !=
                    LooseSpoolPickupStateNames.Length)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Physical Spool Fragment patch found " +
                        locationName + " but not both shipped pickup states."
                    );
                }
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

        private sealed class CompleteLooseSpoolLocation :
            FsmStateAction
        {
            private readonly string locationName;

            internal CompleteLooseSpoolLocation(string locationName)
            {
                this.locationName = locationName;
            }

            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.SpoolFragment) ||
                    !state.IsLocationEnabled(locationName) ||
                    !state.IsLocationInSeed(locationName) ||
                    Owner == null)
                {
                    // The action is installed only for an active randomized
                    // source. If that invariant changes at runtime, leave the
                    // object in place instead of consuming it incorrectly.
                    return;
                }

                PersistentBoolItem persistence =
                    Owner.GetComponent<PersistentBoolItem>();
                if (persistence == null)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Physical Spool Fragment lost its " +
                        "PersistentBoolItem before collection: " +
                        locationName + "."
                    );
                    return;
                }

                // The native source keeps its durable identity without running
                // its vanilla Silk Spool popup, hero animation or counter
                // mutation. Persistence is written before the object is hidden
                // so it stays gone after a reload even if the AP connection is
                // temporarily unavailable.
                var activated = Fsm == null
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

        [HarmonyPatch(
            typeof(PrefabCollectable),
            nameof(PrefabCollectable.Get),
            new Type[] { typeof(bool) })]
        internal static class PhysicalSpoolFragmentPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(PrefabCollectable __instance)
            {
                if (!IsRandomizedSilkSpool(__instance))
                {
                    return true;
                }

                GameManager gameManager = GameManager.instance;
                string sceneName = gameManager == null
                    ? null
                    : gameManager.GetSceneNameString();
                if (MaskAndSpoolLocationManifest.TryGetPhysicalLocation(
                        ItemType.SpoolFragment,
                        sceneName,
                        out string locationName))
                {
                    SaveState.Instance.CheckLocation(locationName);
                }

                // The pickup object owns its persistence outside Get(). Its
                // vanilla Part/Full reward prefab, gain presentation and
                // native counters are bypassed.
                // Shop and wish sources are reported by their persistent flag
                // in MaskAndSpoolLocationManifest on the next polling pass.
                return false;
            }
        }

        [HarmonyPatch(
            typeof(PrefabCollectable),
            nameof(PrefabCollectable.TryGetPrespawnedItem))]
        internal static class PhysicalSpoolFragmentPreSpawnPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                PrefabCollectable __instance,
                ref PreSpawnedItem item,
                ref bool __result)
            {
                if (!IsRandomizedSilkSpool(__instance))
                {
                    return true;
                }

                // Quest boards would otherwise activate a prespawned
                // Part/Full prefab after PreSpawnGet(), bypassing the Get()
                // patch above and showing/applying the vanilla fragment.
                item = null;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(
            typeof(InventoryItemSpoolPieces),
            "UpdateState",
            new Type[0])]
        internal static class InventoryItemSpoolPieces_UpdateState_Patch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                // InventoryItemSpoolPieces reads the same two raw fields as
                // the HUD Animate FSM. They remain permanently synchronized.
                // Restoring stale native-source progress here would shrink
                // the empty HUD again after opening the inventory.
                SynchronizeReceivedSpoolProgress(PlayerData.instance);
            }
        }

        [HarmonyPatch(
            typeof(PlayerData),
            "get_CurrentSilkMaxBasic",
            new Type[0])]
        internal static class PlayerData_CurrentSilkMaxBasic_Patch
        {
            private static void Postfix(
                PlayerData __instance,
                ref int __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.SpoolFragment))
                {
                    return;
                }

                if (__result == __instance.silkMax)
                {
                    __result =
                        BaseSilkMax +
                        CountSpoolFragments() / FragmentsPerSpool;
                }
            }
        }
    }
}
