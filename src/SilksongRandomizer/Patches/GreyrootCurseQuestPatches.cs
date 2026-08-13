using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Keeps the temporary AP Cursed Crest from being treated as Greyroot's
    /// Rite of Rebirth at Yarnaby. If the real curse is accepted while the
    /// trap is active, ownership transfers without disrupting the quest.
    /// </summary>
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class GreyrootCurseQuestPatches
    {
        private const string YarnabyScene = "Belltown_Room_doctor";
        private const string YarnabyObject = "Doctor Fly";
        private const string YarnabyFsm = "Dialogue";
        private const string FirstCurseGateState = "Cursed? 2";
        private const string RepeatCurseGateState = "Cursed? 3";

        private const string NativeCurseScene = "Shellwood_25b";
        private const string NativeCurseObject = "door_curseSequenceEnd";
        private const string NativeCurseFsm = "Curse Sequence";
        private const string NativeSetCursedState = "Set Cursed";

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(PlayMakerFSM __instance)
        {
            if (__instance == null || __instance.gameObject == null)
            {
                return;
            }

            string sceneName = __instance.gameObject.scene.name;
            if (Matches(
                    __instance,
                    sceneName,
                    YarnabyScene,
                    YarnabyObject,
                    YarnabyFsm))
            {
                ValidateYarnabyCurseGate(__instance);
            }
            else if (Matches(
                         __instance,
                         sceneName,
                         NativeCurseScene,
                         NativeCurseObject,
                         NativeCurseFsm))
            {
                PatchNativeCurseOwnership(__instance);
            }
        }

        private static bool Matches(
            PlayMakerFSM fsm,
            string actualScene,
            string expectedScene,
            string expectedObject,
            string expectedFsm)
        {
            return string.Equals(
                       actualScene,
                       expectedScene,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       fsm.gameObject.name,
                       expectedObject,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       fsm.FsmName,
                       expectedFsm,
                       StringComparison.Ordinal);
        }

        private static void ValidateYarnabyCurseGate(PlayMakerFSM fsm)
        {
            FsmState firstGate = fsm.Fsm?.GetState(FirstCurseGateState);
            FsmState repeatGate = fsm.Fsm?.GetState(RepeatCurseGateState);
            bool firstMatches =
                firstGate?.Actions != null &&
                firstGate.Actions.Length == 2 &&
                firstGate.Actions[0] is ActivateInteractible &&
                IsNativeAnyCurseTest(firstGate.Actions[1]) &&
                HasTransition(firstGate, "TRUE", "Quest Active?") &&
                HasTransition(firstGate, "FINISHED", "Convo Choice");
            bool repeatMatches =
                repeatGate?.Actions != null &&
                repeatGate.Actions.Length == 1 &&
                IsNativeAnyCurseTest(repeatGate.Actions[0]) &&
                HasTransition(repeatGate, "TRUE", "Quest Active?") &&
                HasTransition(repeatGate, "FINISHED", "Interacted");
            if (!firstMatches || !repeatMatches)
            {
                LogFailure(
                    "Yarnaby's shipped IsAnyCursed gates no longer matched"
                );
            }
        }

        private static void PatchNativeCurseOwnership(PlayMakerFSM fsm)
        {
            FsmState setCursed = fsm.Fsm?.GetState(NativeSetCursedState);
            FsmStateAction[] actions = setCursed?.Actions;
            if (actions != null &&
                actions.Length == 6 &&
                actions[4] is ApplyGenuineGreyrootCurse)
            {
                return;
            }

            AutoEquipCrest nativeEquip =
                actions != null && actions.Length > 4
                    ? actions[4] as AutoEquipCrest
                    : null;
            bool matches =
                actions != null &&
                actions.Length == 6 &&
                actions[0] is SetPlayerDataVariable &&
                actions[1] is SetPlayerDataVariable &&
                actions[2] is SetPlayerDataVariable &&
                actions[3] is SendEventToRegister &&
                nativeEquip != null &&
                IsNativeCursedCrest(nativeEquip) &&
                actions[5] is CallMethodProper &&
                HasTransition(setCursed, "FINISHED", "Wait");
            if (!matches)
            {
                LogFailure(
                    "Greyroot's shipped Set Cursed state no longer matched"
                );
                return;
            }

            ApplyGenuineGreyrootCurse replacement =
                new ApplyGenuineGreyrootCurse(
                    nativeEquip.Crest.Value as ToolCrest
                );
            replacement.Init(setCursed);
            actions[4] = replacement;
        }

        private static bool IsNativeAnyCurseTest(FsmStateAction action)
        {
            return action is PlayerDataVariableTest test &&
                   string.Equals(
                       test.VariableName?.Value,
                       "IsAnyCursed",
                       StringComparison.Ordinal) &&
                   test.ExpectedValue != null &&
                   test.ExpectedValue.boolValue &&
                   string.Equals(
                       test.IsExpectedEvent?.Name,
                       "TRUE",
                       StringComparison.Ordinal);
        }

        private static bool IsNativeCursedCrest(AutoEquipCrest action)
        {
            ToolCrest expected = GlobalSettings.Gameplay.CursedCrest;
            return expected != null &&
                   action?.Crest?.Value is ToolCrest actual &&
                   string.Equals(
                       actual.name,
                       expected.name,
                       StringComparison.Ordinal);
        }

        private static bool IsExactYarnabyCurseGate(
            PlayerDataVariableTest action)
        {
            if (action == null ||
                action.Owner == null ||
                action.Fsm == null ||
                action.State == null ||
                !string.Equals(
                    action.Owner.scene.name,
                    YarnabyScene,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    action.Owner.name,
                    YarnabyObject,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.Fsm.Name,
                    YarnabyFsm,
                    StringComparison.Ordinal) ||
                !(string.Equals(
                      action.State.Name,
                      FirstCurseGateState,
                      StringComparison.Ordinal) ||
                  string.Equals(
                      action.State.Name,
                      RepeatCurseGateState,
                      StringComparison.Ordinal)))
            {
                return false;
            }

            return IsNativeAnyCurseTest(action);
        }

        private static bool HasTransition(
            FsmState state,
            string eventName,
            string targetState)
        {
            if (state?.Transitions == null)
            {
                return false;
            }

            foreach (FsmTransition transition in state.Transitions)
            {
                if (transition != null &&
                    string.Equals(
                        transition.EventName,
                        eventName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        transition.ToState,
                        targetState,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasGenuineGreyrootCurse(PlayerData playerData)
        {
            // IsAnyCursed alone is only a CurrentCrestID comparison and is
            // therefore also true during the AP trap. gainedCurse can also
            // remain true on a save whose real curse is no longer the live
            // crest, so it is not sufficient proof while a temporary Cursed
            // Crest owns CurrentCrestID. The AP manager owns that state while
            // its timer is alive. IsCurrentCrestTemp covers missing ownership
            // bookkeeping. Greyroot's Rite equip is permanent (markTemp=false),
            // so the native curse remains eligible. Yarnaby writes
            // BelltownDoctorCuredCurse after the operation finishes.
            return playerData != null &&
                   !TrapManager.IsCursedCrestActive &&
                   !playerData.IsCurrentCrestTemp &&
                   playerData.IsAnyCursed &&
                   playerData.gainedCurse &&
                   !playerData.BelltownDoctorCuredCurse;
        }

        private static void LogFailure(string reason)
        {
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Rite of Rebirth safety patch failed closed: " +
                reason + "."
            );
        }

        [HarmonyPatch(
            typeof(PlayerDataVariableTest),
            nameof(PlayerDataVariableTest.OnEnter)
        )]
        private static class YarnabyCurseGateRuntimePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerDataVariableTest __instance)
            {
                if (!IsExactYarnabyCurseGate(__instance))
                {
                    return true;
                }

                if (HasGenuineGreyrootCurse(PlayerData.instance))
                {
                    // The TRUE event and all vanilla quest handling remain
                    // active for Greyroot's real permanent curse.
                    return true;
                }

                // PlayerDataVariableTest normally sends its not-expected
                // event and then finishes. These two actions have no
                // not-expected event, so Finish follows their FINISHED
                // transition into Yarnaby's ordinary non-curse dialogue.
                __instance.Fsm.Event(__instance.IsNotExpectedEvent);
                __instance.Finish();
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] Yarnaby ignored a temporary or " +
                    "non-Greyroot Cursed Crest."
                );
                return false;
            }
        }

        private sealed class ApplyGenuineGreyrootCurse : FsmStateAction
        {
            private readonly ToolCrest cursedCrest;

            internal ApplyGenuineGreyrootCurse(ToolCrest cursedCrest)
            {
                this.cursedCrest = cursedCrest;
            }

            public override void OnEnter()
            {
                TrapManager.RelinquishCursedCrestForNativeCurse();
                if (!ToolPatches.AutoEquipNativeRiteCursedCrest(cursedCrest))
                {
                    LogFailure(
                        "Greyroot's permanent Cursed Crest equip was rejected"
                    );
                }
                Finish();
            }
        }
    }
}
