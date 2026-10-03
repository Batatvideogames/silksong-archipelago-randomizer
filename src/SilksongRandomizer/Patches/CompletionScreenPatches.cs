using HarmonyLib;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(AnimatorSequence), nameof(AnimatorSequence.Begin))]
    internal static class CompletionScreenPatches
    {
        private static void ApplyPercentageVisibility(GameCompletionScreen screen)
        {
            if (screen == null || screen.gameObject.scene.name != "End_Game_Completion" ||
                SaveState.Instance?.IsRoomBound != true || PlayerData.instance == null) return;
            bool visible = PlayerData.instance.ConstructedFarsight;
            screen.transform.Find("Percent_title")?.gameObject.SetActive(visible);
            screen.transform.Find("percentage_num")?.gameObject.SetActive(visible);
        }

        [HarmonyPatch(typeof(GameCompletionScreen), "Start")]
        private static class CompletionStartPatch
        {
            [HarmonyPrefix]
            private static void Prefix(GameCompletionScreen __instance) =>
                ApplyPercentageVisibility(__instance);
        }

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

            ApplyPercentageVisibility(___animator.GetComponent<GameCompletionScreen>());
            bool showPercentage = PlayerData.instance.ConstructedFarsight;
            ___animatorStateName = showPercentage ? "Act 3" : "Act 2";
            ___skipStateName = showPercentage ? "Act 3 End" : "Act 2 End";
        }
    }
}
