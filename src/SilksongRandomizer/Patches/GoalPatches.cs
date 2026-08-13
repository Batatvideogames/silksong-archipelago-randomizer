using HarmonyLib;
using HutongGames.PlayMaker;
using System;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Awards the Act 3 goal when Lost Lace is defeated,
    /// before the ending cinematic and credits begin. The later ending hook
    /// remains as an idempotent fallback.
    /// </summary>
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class LostLaceGoalPatch
    {
        private const string SceneName = "Abyss_Cocoon";
        private const string ObjectName = "Superjump Sequence";
        private const string FsmName = "Control";
        private const string DefeatState = "Dormant";
        private const string DefeatEvent = "LACE DEFEATED";
        private const string CompletionState = "Start Pause";

        [HarmonyPostfix]
        private static void Postfix(PlayMakerFSM __instance)
        {
            if (__instance == null ||
                !string.Equals(
                    __instance.gameObject.scene.name,
                    SceneName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    __instance.name,
                    ObjectName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    __instance.FsmName,
                    FsmName,
                    StringComparison.Ordinal))
            {
                return;
            }

            FsmState defeat = FindState(__instance, DefeatState);
            FsmState completion = FindState(__instance, CompletionState);
            if (defeat == null || completion == null ||
                !HasTransition(defeat, DefeatEvent, CompletionState) ||
                completion.Actions == null)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Lost Lace goal hook was not installed " +
                    "because its validated native completion path changed."
                );
                return;
            }

            foreach (FsmStateAction action in completion.Actions)
            {
                if (action is CompleteLostLaceGoal)
                {
                    return;
                }
            }

            CompleteLostLaceGoal goalAction =
                new CompleteLostLaceGoal();
            goalAction.Init(completion);
            FsmStateAction[] patchedActions =
                new FsmStateAction[completion.Actions.Length + 1];
            patchedActions[0] = goalAction;
            Array.Copy(
                completion.Actions,
                0,
                patchedActions,
                1,
                completion.Actions.Length
            );
            completion.Actions = patchedActions;

            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Act 3 goal anchored to Lost Lace defeat."
            );
        }

        private static FsmState FindState(
            PlayMakerFSM fsm,
            string stateName
        )
        {
            if (fsm.FsmStates == null)
            {
                return null;
            }

            foreach (FsmState state in fsm.FsmStates)
            {
                if (state != null &&
                    string.Equals(
                        state.Name,
                        stateName,
                        StringComparison.Ordinal))
                {
                    return state;
                }
            }

            return null;
        }

        private static bool HasTransition(
            FsmState state,
            string eventName,
            string targetState
        )
        {
            if (state.Transitions == null)
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

        private sealed class CompleteLostLaceGoal : FsmStateAction
        {
            public override void OnEnter()
            {
                SaveState saveState = SaveState.Instance;
                if (saveState != null &&
                    string.Equals(
                        saveState.goal,
                        Archipelago.ActThreeGoal,
                        StringComparison.Ordinal))
                {
                    saveState.CheckLocation(
                        Archipelago.GoalLocationName
                    );
                }

                Finish();
            }
        }
    }

    [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.SetEndingCompleted), "OnEnter")]
    internal static class EndingGoalPatch
    {
        private const int ActTwoRegularEndingValue = 1;
        private const int ActTwoCursedEndingValue = 2;
        private const int ActTwoSoulSnareEndingValue = 4;
        private const int ActThreeEndingValue = 8;

        private static void Postfix()
        {
            SaveState saveState = SaveState.Instance;
            if (saveState == null || PlayerData.instance == null)
            {
                return;
            }

            if (MatchesConfiguredGoal(
                saveState.goal,
                (int)PlayerData.instance.LastCompletedEnding
            ))
            {
                saveState.CheckLocation(Archipelago.GoalLocationName);
            }
        }

        internal static bool MatchesConfiguredGoal(string goal, int endingValue)
        {
            if (string.Equals(goal, Archipelago.ActTwoGoal, System.StringComparison.Ordinal))
            {
                return endingValue == ActTwoRegularEndingValue ||
                       endingValue == ActTwoCursedEndingValue ||
                       endingValue == ActTwoSoulSnareEndingValue;
            }

            return string.Equals(
                       goal,
                       Archipelago.ActThreeGoal,
                       System.StringComparison.Ordinal
                   ) &&
                   endingValue == ActThreeEndingValue;
        }
    }
}
