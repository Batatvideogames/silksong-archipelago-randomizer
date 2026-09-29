using HarmonyLib;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(AnimatorSequence), nameof(AnimatorSequence.Begin))]
    internal static class CompletionScreenPatches
    {
        [HarmonyPrefix]
        private static void Prefix(
            AnimatorSequence __instance,
            Animator ___animator,
            ref string ___animatorStateName,
            ref string ___skipStateName
        )
        {
            if (SaveState.Instance == null || !SaveState.Instance.IsRoomBound ||
                PlayerData.instance == null ||
                __instance == null || ___animator == null ||
                __instance.gameObject.scene.name != "End_Game_Completion" ||
                ___animator.GetComponent<GameCompletionScreen>() == null ||
                ___animator.runtimeAnimatorController == null ||
                ___animator.runtimeAnimatorController.name != "game completion")
            {
                return;
            }

            bool actTwoPanel = __instance.name == "Game Completion Act 2";
            bool actThreePanel = __instance.name == "Game Completion Act 3";
            bool actTwoAnimation = ___animatorStateName == "Act 2" &&
                ___skipStateName == "Act 2 End";
            bool actThreeAnimation = ___animatorStateName == "Act 3" &&
                ___skipStateName == "Act 3 End";
            if ((!actTwoPanel && !actThreePanel) ||
                (!actTwoAnimation && !actThreeAnimation))
            {
                return;
            }

            bool showPercentage = PlayerData.instance.ConstructedFarsight;
            ___animatorStateName = showPercentage ? "Act 3" : "Act 2";
            ___skipStateName = showPercentage ? "Act 3 End" : "Act 2 End";
        }
    }
}
