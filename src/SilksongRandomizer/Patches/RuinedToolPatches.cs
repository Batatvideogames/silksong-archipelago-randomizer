using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class RuinedToolPatches
    {
        internal const string LocationName = "Ruined Tool";
        internal const string NativeAssetName = "Broken SilkShot";
        internal const string NativeCraftmetalAssetName = "Tool Metal";
        internal const string NativeSceneName = "Shadow_Weavehome";
        internal const float NativePositionX = 76.642f;
        internal const float NativePositionY = 54.577f;
        private const string OriginalRepairSceneName = "Peak_12";
        private const string OriginalRepairOwnerName = "Webshot Scene";
        private const string OriginalRepairFsmName = "Repair webshot";
        private const string OriginalRepairGateStateName =
            "Has broken SilkShot?";

        private const float SourceMatchTolerance = 0.75f;

        private static readonly Dictionary<string, string>
            RepairLocationByNativeTool =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    {
                        "WebShot Forge",
                        "Tool Unlock: WebShot Forge"
                    },
                    {
                        "WebShot Architect",
                        "Tool Unlock: WebShot Architect"
                    },
                    {
                        "WebShot Weaver",
                        "Tool Unlock: WebShot Weaver"
                    },
                };

        private static ArchipelagoRuinedToolLocation proxyItem;

        private sealed class ArchipelagoRuinedToolLocation : SavedItem
        {
            public override void Get(bool showPopup = true)
            {
                SaveState state = SaveState.Instance;
                if (IsActiveUncheckedLocation(state))
                {
                    state.CheckLocation(LocationName);
                }
            }

            public override bool CanGetMore()
            {
                return IsActiveUncheckedLocation(SaveState.Instance);
            }
        }

        internal static bool TryHandleWebShotRepair(
            SaveState state,
            string nativeToolName
        )
        {
            if (!RepairLocationByNativeTool.TryGetValue(
                    nativeToolName ?? string.Empty,
                    out string repairLocation))
            {
                return false;
            }

            if (state != null &&
                state.IsLocationEnabled(repairLocation) &&
                state.IsLocationInSeed(repairLocation))
            {
                state.CheckLocation(repairLocation);
            }

            RestoreSharedToolWhileRepairRemains(state);
            return true;
        }

        internal static bool TryReconcileReceivedOwnership()
        {
            SaveState state = SaveState.Instance;
            if (state == null)
            {
                return false;
            }
            if (!state.IsRandomized(ItemType.Tool) ||
                state.receivedItems == null ||
                !state.receivedItems.Contains(LocationName) ||
                !HasRemainingActiveRepair(state))
            {
                return true;
            }

            CollectableItem ruinedTool =
                CollectableItemManager.GetItemByName(NativeAssetName);
            if (ruinedTool == null)
            {
                return false;
            }
            if (ruinedTool.CollectedAmount <= 0)
            {
                ruinedTool.Collect(1, false);
            }
            return true;
        }

        private static void RestoreSharedToolWhileRepairRemains(
            SaveState state
        )
        {
            if (state == null || !HasRemainingActiveRepair(state))
            {
                return;
            }

            CollectableItem ruinedTool =
                CollectableItemManager.GetItemByName(NativeAssetName);
            if (ruinedTool == null)
            {
                throw new InvalidOperationException(
                    "Ruined Tool collectable asset is not ready."
                );
            }

            if (ruinedTool.CollectedAmount <= 0)
            {
                ruinedTool.Collect(1, false);
            }

            // Vanilla lets the player choose one of these three repairs. Each
            // consumes the same Ruined Tool plus one Craftmetal. In an
            // AP seed all three are distinct checks, so return both shared
            // ingredients after the first two and consume them for good only
            // when no active repair remains.
            CollectableItem craftmetal =
                CollectableItemManager.GetItemByName(
                    NativeCraftmetalAssetName
                );
            if (craftmetal == null)
            {
                throw new InvalidOperationException(
                    "Craftmetal collectable asset is not ready."
                );
            }

            craftmetal.Collect(1, false);
        }

        private static bool HasRemainingActiveRepair(SaveState state)
        {
            foreach (string repairLocation in
                RepairLocationByNativeTool.Values)
            {
                if (state.IsLocationEnabled(repairLocation) &&
                    state.IsLocationInSeed(repairLocation) &&
                    !state.IsLocationChecked(repairLocation))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsActiveUncheckedLocation(SaveState state)
        {
            return state != null &&
                   state.IsRandomized(ItemType.Tool) &&
                   state.IsLocationEnabled(LocationName) &&
                   state.IsLocationInSeed(LocationName) &&
                   !state.IsLocationChecked(LocationName);
        }

        private static SavedItem GetProxyItem()
        {
            if (proxyItem != null)
            {
                return proxyItem;
            }

            proxyItem =
                ScriptableObject.CreateInstance<
                    ArchipelagoRuinedToolLocation
                >();
            proxyItem.name =
                "Archipelago Location - " + LocationName;
            return proxyItem;
        }

        private static bool MatchesNativeSource(
            CollectableItemPickup pickup,
            SavedItem nativeItem
        )
        {
            if (pickup == null ||
                nativeItem == null ||
                !string.Equals(
                    pickup.gameObject.scene.name,
                    NativeSceneName,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    nativeItem.name,
                    NativeAssetName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            Vector2 offset =
                (Vector2)pickup.transform.position -
                new Vector2(NativePositionX, NativePositionY);
            return offset.sqrMagnitude <=
                   SourceMatchTolerance * SourceMatchTolerance;
        }

        private static bool MatchesOriginalRepairOwnershipGate(
            CollectableItemGetData action
        )
        {
            return action != null &&
                   action.Owner != null &&
                   action.Fsm != null &&
                   action.State != null &&
                   string.Equals(
                       action.Owner.scene.name,
                       OriginalRepairSceneName,
                       StringComparison.OrdinalIgnoreCase
                   ) &&
                   string.Equals(
                       action.Owner.name,
                       OriginalRepairOwnerName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       action.Fsm.Name,
                       OriginalRepairFsmName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       action.State.Name,
                       OriginalRepairGateStateName,
                       StringComparison.Ordinal
                   );
        }

        [HarmonyPatch(typeof(CollectableItemGetData), "OnEnter")]
        private static class OriginalRepairOwnershipGatePatch
        {
            [HarmonyPrefix]
            private static void Prefix(CollectableItemGetData __instance)
            {
                if (MatchesOriginalRepairOwnershipGate(__instance))
                {
                    TryReconcileReceivedOwnership();
                }
            }
        }

        [HarmonyPatch(typeof(CollectableItemPickup), "Awake")]
        private static class CollectableItemPickupAwakePatch
        {
            [HarmonyPrefix]
            private static void Prefix(CollectableItemPickup __instance)
            {
                SaveState state = SaveState.Instance;
                SavedItem nativeItem = __instance == null
                    ? null
                    : __instance.Item;
                if (state == null ||
                    !state.IsRandomized(ItemType.Tool) ||
                    !MatchesNativeSource(__instance, nativeItem) ||
                    !state.IsLocationEnabled(LocationName) ||
                    !state.IsLocationInSeed(LocationName))
                {
                    return;
                }

                // The pickup retains its native persistence and choreography,
                // but replace Broken SilkShot with an AP location proxy so
                // the physical source never leaks the vanilla inventory item.
                __instance.SetItem(
                    GetProxyItem(),
                    keepPersistence: true
                );
            }
        }
    }
}
