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
                PlayerData.instance == null || !PlayerData.instance.ConstructedFarsight ||
                __instance == null || ___animator == null ||
                __instance.gameObject.scene.name != "End_Game_Completion" ||
                __instance.name != "Game Completion Act 2" ||
                ___animatorStateName != "Act 2" || ___skipStateName != "Act 2 End" ||
                ___animator.GetComponent<GameCompletionScreen>() == null ||
                ___animator.runtimeAnimatorController == null ||
                ___animator.runtimeAnimatorController.name != "game completion")
            {
                return;
            }

            ___animatorStateName = "Act 3";
            ___skipStateName = "Act 3 End";
        }
    }
}
