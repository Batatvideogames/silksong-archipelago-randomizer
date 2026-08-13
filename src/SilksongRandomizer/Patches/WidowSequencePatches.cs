using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using BeginSceneTransitionAction =
    HutongGames.PlayMaker.Actions.BeginSceneTransition;
using SetPlayerDataBoolAction =
    HutongGames.PlayMaker.Actions.SetPlayerDataBool;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Separates Widow's randomized Needolin check from vanilla ownership and
    /// keeps players without the AP Needolin out of its song-gated memory.
    /// </summary>
    internal static class WidowSequencePatches
    {
        private const string BossObjectName = "Spinner Boss";
        private const string BossFsmName = "Control";
        private const string NeedolinLocationName =
            "Skill Unlock: Needolin";

        private static bool IsRandomizedWidowAction(
            FsmStateAction action,
            string stateName
        )
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsRandomized(ItemType.Skill) &&
                   action != null &&
                   action.Owner != null &&
                   string.Equals(
                       action.Owner.scene.name,
                       WidowSequenceSafety.WidowShrineSceneName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       action.Owner.name,
                       BossObjectName,
                       StringComparison.Ordinal
                   ) &&
                   action.Fsm != null &&
                   string.Equals(
                       action.Fsm.Name,
                       BossFsmName,
                       StringComparison.Ordinal
                   ) &&
                   action.State != null &&
                   string.Equals(
                       action.State.Name,
                       stateName,
                       StringComparison.Ordinal
                   );
        }

        [HarmonyPatch(
            typeof(SetPlayerDataBoolAction),
            nameof(SetPlayerDataBoolAction.OnEnter)
        )]
        private static class RandomizedNeedolinCheckPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(SetPlayerDataBoolAction __instance)
            {
                if (!IsRandomizedWidowAction(
                        __instance,
                        "Final Bind Burst"
                    ) ||
                    __instance.boolName == null ||
                    !string.Equals(
                        __instance.boolName.Value,
                        "hasNeedolin",
                        StringComparison.Ordinal
                    ))
                {
                    return true;
                }

                SaveState.Instance.CheckLocation(NeedolinLocationName);
                __instance.Finish();
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] Widow reported the randomized Needolin " +
                    "location without granting vanilla Needolin ownership."
                );
                return false;
            }
        }

        [HarmonyPatch(
            typeof(BeginSceneTransitionAction),
            nameof(BeginSceneTransitionAction.OnEnter)
        )]
        private static class MissingNeedolinMemoryBypassPatch
        {
            [HarmonyPrefix]
            private static void Prefix(
                BeginSceneTransitionAction __instance
            )
            {
                SaveState state = SaveState.Instance;
                if (!IsRandomizedWidowAction(
                        __instance,
                        "To Memory Scene"
                    ) ||
                    state == null ||
                    state.canUseNeedolin ||
                    __instance.sceneName == null ||
                    !string.Equals(
                        __instance.sceneName.Value,
                        WidowSequenceSafety.NeedolinMemorySceneName,
                        StringComparison.Ordinal
                    ))
                {
                    return;
                }

                __instance.sceneName.Value =
                    WidowSequenceSafety.WidowShrineSceneName;
                __instance.entryGateName.Value =
                    WidowSequenceSafety.WidowWakeGateName;
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Skipped Widow's Needolin memory because " +
                    "the AP Needolin has not been received; returning through " +
                    "the native shrine wake sequence."
                );
            }
        }
    }
}
