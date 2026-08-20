using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using System;

namespace SilksongRandomizer.Patches
{
    internal static class ShakraIntroPersistencePatches
    {
        private const string MapperObject = "Mapper NPC";
        private const string CoralMapperObject = "Mapper NPC (1)";
        private const string DialogueFsm = "Dialogue";
        private const string StockDepletedState = "Stock Depleted";
        private const string DialogueSheet = "Wanderers";
        private const string StockDepletedKey =
            "MAPPER_STOCKDEPLETED";

        private static bool IsStandingMapper(
            string sceneName,
            string objectName)
        {
            switch (sceneName)
            {
                case "Ant_04_mid":
                case "Belltown":
                case "Bonetown":
                case "Bone_04":
                case "Bone_East_01":
                case "Crawl_01":
                case "Peak_02":
                case "Shellwood_16":
                    return string.Equals(
                        objectName,
                        MapperObject,
                        StringComparison.Ordinal);
                case "Coral_12":
                    return string.Equals(
                        objectName,
                        CoralMapperObject,
                        StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        internal static bool IsExactStockDepletedDialogue(
            RunDialogue action)
        {
            return action != null &&
                   action.Owner != null &&
                   action.Fsm != null &&
                   action.State != null &&
                   action.Sheet != null &&
                   action.Key != null &&
                   IsStandingMapper(
                       action.Owner.scene.name,
                       action.Owner.name) &&
                   string.Equals(
                       action.Fsm.Name,
                       DialogueFsm,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       action.State.Name,
                       StockDepletedState,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       action.Sheet.Value,
                       DialogueSheet,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       action.Key.Value,
                       StockDepletedKey,
                       StringComparison.Ordinal);
        }

        internal static bool TryPersistFirstMeeting(
            RunDialogue action)
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                !state.IsRoomBound ||
                playerData == null ||
                playerData.metMapper ||
                !IsExactStockDepletedDialogue(action))
            {
                return false;
            }

            // ShopHasStock changes state before Post Shop Intro reaches its
            // native metMapper write. Persist only that skipped global flag.
            playerData.metMapper = true;
            return true;
        }

        [HarmonyPatch(
            typeof(RunDialogue),
            "DialogueText",
            MethodType.Getter)]
        private static class StockDepletedDialoguePatch
        {
            [HarmonyPrefix]
            private static void Prefix(RunDialogue __instance)
            {
                TryPersistFirstMeeting(__instance);
            }
        }
    }
}
