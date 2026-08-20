using HarmonyLib;
using HutongGames.PlayMaker;
using System;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class BellBeastReturnSafety
    {
        private const string SceneName = "Bone_05_boss";
        private const string ShermaObjectName = "Sherma Bell Beast NPC";
        private const string ShermaFsmName = "Conversation Control";

        internal static bool ShouldHideShermaOnReturn(
            bool isRoomBound,
            string sceneName,
            string objectName,
            string fsmName,
            bool encounteredBellBeast,
            bool defeatedBellBeast
        )
        {
            return isRoomBound &&
                   encounteredBellBeast &&
                   !defeatedBellBeast &&
                   string.Equals(
                       sceneName,
                       SceneName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       objectName,
                       ShermaObjectName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       fsmName,
                       ShermaFsmName,
                       StringComparison.Ordinal
                   );
        }

        [HarmonyPostfix]
        private static void Postfix(PlayMakerFSM __instance)
        {
            if (__instance == null || __instance.gameObject == null)
            {
                return;
            }

            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.HasInstance
                ? PlayerData.instance
                : null;
            if (state == null ||
                playerData == null ||
                !ShouldHideShermaOnReturn(
                    state.IsRoomBound,
                    __instance.gameObject.scene.name,
                    __instance.gameObject.name,
                    __instance.FsmName,
                    playerData.encounteredBellBeast,
                    playerData.defeatedBellBeast
                ))
            {
                return;
            }

            __instance.gameObject.SetActive(false);
        }
    }
}
