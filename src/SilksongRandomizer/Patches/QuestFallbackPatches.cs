using HarmonyLib;
using System;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class QuestFallbackPatches
    {
        private const string RockRollersLocation =
            "Quest Completion: Rock Rollers";
        private const string RockRollersFallbackPath =
            "Black Thread States Thread Only Variant/Black Thread World/" +
            "Rock Rollers Quest Not Completed/" +
            "Collectable Item Pickup Locket";

        [HarmonyPatch(
            typeof(CollectableItemPickup),
            "DoPickupAction",
            new[] { typeof(bool) }
        )]
        private static class RockRollersFallbackPickupPatch
        {
            private static void Postfix(
                CollectableItemPickup __instance,
                bool __result
            )
            {
                SaveState state = SaveState.Instance;
                if (
                    !__result ||
                    __instance == null ||
                    state == null ||
                    !state.IsRandomized(ItemType.Quest) ||
                    !state.IsLocationEnabled(RockRollersLocation) ||
                    !state.IsLocationInSeed(RockRollersLocation) ||
                    state.IsLocationChecked(RockRollersLocation) ||
                    PlayerData.instance == null ||
                    !PlayerData.instance.blackThreadWorld ||
                    !string.Equals(
                        __instance.gameObject.scene.name,
                        "Bone_10",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    !string.Equals(
                        Utils.GetHierarchyPath(__instance.transform),
                        RockRollersFallbackPath,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return;
                }

                // The vanilla Memory Locket remains. This reports only the
                // same Quest Sanity location that the normal Wishwall turn-in
                // would have reported before Act 3 removed the open quest.
                state.CheckLocation(RockRollersLocation);
            }
        }
    }
}
