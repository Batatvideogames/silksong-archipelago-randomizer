using HarmonyLib;
using System;

namespace SilksongRandomizer.Patches
{
    internal static class QuestRequirementPatches
    {
        private const string SoulSnareQuest = "Soul Snare";
        private const string ShakraFinalQuest = "Shakra Final Quest";
        private const string SnareSetterItem = "Tool: Snare Setter";

        internal static bool ApplySoulSnareRequirement(
            FullQuestBase quest,
            bool nativeCanComplete,
            SaveState state)
        {
            if (!nativeCanComplete ||
                quest == null ||
                state == null ||
                !state.IsRandomized(ItemType.Tool) ||
                !string.Equals(
                    quest.name,
                    SoulSnareQuest,
                    StringComparison.Ordinal))
            {
                return nativeCanComplete;
            }

            return state.receivedItems != null &&
                   state.receivedItems.Contains(
                       ItemSet.GetCanonicalItemName(SnareSetterItem));
        }

        internal static bool ApplyShakraFinalQuestRequirement(
            FullQuestBase quest,
            bool nativeIsAvailable,
            SaveState state,
            PlayerData playerData)
        {
            if (quest == null ||
                state == null ||
                !state.IsRandomized(ItemType.Skill) ||
                !string.Equals(
                    quest.name,
                    ShakraFinalQuest,
                    StringComparison.Ordinal))
            {
                return nativeIsAvailable;
            }

            // ShakraFinalQuestAppear is the shipped story/stock gate. Only
            // replace the physical Fayforn pickup flag with AP ownership.
            return playerData != null &&
                   playerData.ShakraFinalQuestAppear &&
                   state.canDoubleJump;
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_CanComplete")]
        private static class SoulSnareCanCompletePatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                FullQuestBase __instance,
                ref bool __result)
            {
                __result = ApplySoulSnareRequirement(
                    __instance,
                    __result,
                    SaveState.Instance);
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_IsAvailable")]
        private static class ShakraFinalQuestAvailablePatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                FullQuestBase __instance,
                ref bool __result)
            {
                __result = ApplyShakraFinalQuestRequirement(
                    __instance,
                    __result,
                    SaveState.Instance,
                    PlayerData.instance);
            }
        }
    }
}
