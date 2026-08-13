using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    public class CrestPatches
    {
        [HarmonyPatch(typeof(InventoryToolCrestList), "CanChangeCrests", new Type[0])]
        internal static class InventoryToolCrestList_CanChangeCrests_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (!TrapManager.IsCursedCrestActive &&
                    (state == null || !state.IsRandomized(ItemType.Crest)))
                {
                    return true;
                }

                __result = !TrapManager.IsCursedCrestActive;
                return false;
            }
        }

        [HarmonyPatch(typeof(ToolCrest), "Unlock", new Type[0])]
        internal static class ToolCrest_Unlock_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(ToolCrest __instance)
            {
                if (__instance == null)
                {
                    return false;
                }

                if (SaveState.Instance == null ||
                    !SaveState.Instance.IsRandomized(ItemType.Crest) ||
                    ToolPatches.canCrestBeUnlockedByRandomizer)
                {
                    return true;
                }

                string toolName = "<null>";

                if (!string.IsNullOrEmpty(__instance.name))
                {
                    toolName = __instance.name;
                }

                Debug.Log("[RANDOMIZER] Tried to Unlock: " + toolName);

                if (CrestNames.IsHunterInternalName(toolName) &&
                    !__instance.IsBaseVersion)
                {
                    // Eva's Hunter v2/v3 upgrades are not new AP checks.
                    // Permit the native version-chain upgrade only after the
                    // shuffled base Hunter crest has actually been received.
                    return SaveState.Instance.receivedItems.Contains(
                        "Crest: Hunter"
                    );
                }

                SaveState.Instance.CheckLocation(
                    CrestNames.GetLocationNameFromInternal(toolName)
                );
                return false;
            }
        }
    }
}
