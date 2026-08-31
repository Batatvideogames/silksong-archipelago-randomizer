using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using SetPlayerDataBoolAction =
    HutongGames.PlayMaker.Actions.SetPlayerDataBool;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class TrobbioMirrorRewardPatches
    {
        private const string SceneName = "Library_13";
        private const string FsmName = "Control";
        private const string RegularOwnerName = "Trobbio";
        private const string RegularParentName = "Boss Scene Trobbio";
        private const string StageOwnerName = "Grand Stage Scene";
        private const string RegularRewardStateName = "Item Appear";
        private const string RegularEndStateName = "Battle End";
        private const string TormentedReadyStateName = "Trobbio Ready 2";
        private const string TormentedRewardEventName =
            "TORMENTED BATTLE END";
        private const string TormentedRewardStateName =
            "Spawn Tormented Item";
        private const string TormentedEndStateName = "End Battle";
        private const string RegularLocationName =
            "Tool Unlock: Dazzle Bind";
        private const string TormentedLocationName =
            "Tool Unlock: Dazzle Bind Upgraded";

        [HarmonyPostfix]
        private static void Postfix(PlayMakerFSM __instance)
        {
            if (__instance == null ||
                __instance.gameObject == null ||
                !string.Equals(
                    __instance.gameObject.scene.name,
                    SceneName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    __instance.FsmName,
                    FsmName,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (IsRegularRewardFsm(__instance))
            {
                PatchRegularReward(__instance);
                return;
            }

            if (string.Equals(
                    __instance.gameObject.name,
                    StageOwnerName,
                    StringComparison.Ordinal))
            {
                PatchTormentedReward(__instance);
            }
        }

        private static bool IsRegularRewardFsm(PlayMakerFSM fsm)
        {
            return string.Equals(
                       fsm.gameObject.name,
                       RegularOwnerName,
                       StringComparison.Ordinal) &&
                   fsm.transform.parent != null &&
                   string.Equals(
                       fsm.transform.parent.name,
                       RegularParentName,
                       StringComparison.Ordinal) &&
                   fsm.transform.parent.parent != null &&
                   string.Equals(
                       fsm.transform.parent.parent.name,
                       StageOwnerName,
                       StringComparison.Ordinal);
        }

        private static void PatchRegularReward(PlayMakerFSM fsm)
        {
            FsmState rewardState =
                fsm.Fsm?.GetState(RegularRewardStateName);
            FsmStateAction[] actions = rewardState?.Actions;
            if (ContainsCompletionAction(
                    rewardState,
                    RegularLocationName))
            {
                return;
            }

            if (actions == null ||
                actions.Length != 4 ||
                !(actions[0] is Wait) ||
                !(actions[1] is SetPositionToObject) ||
                !(actions[2] is ActivateGameObject) ||
                !(actions[3] is Wait) ||
                !HasOnlyTransition(
                    rewardState,
                    FsmEvent.Finished.Name,
                    RegularEndStateName))
            {
                LogFailure(RegularLocationName);
                return;
            }

            AppendCompletionAction(
                rewardState,
                RegularLocationName
            );
        }

        private static void PatchTormentedReward(PlayMakerFSM fsm)
        {
            FsmState readyState =
                fsm.Fsm?.GetState(TormentedReadyStateName);
            FsmState rewardState =
                fsm.Fsm?.GetState(TormentedRewardStateName);
            FsmStateAction[] readyActions = readyState?.Actions;
            FsmStateAction[] rewardActions = rewardState?.Actions;
            if (ContainsCompletionAction(
                    rewardState,
                    TormentedLocationName))
            {
                return;
            }

            if (readyActions == null ||
                readyActions.Length != 2 ||
                !(readyActions[0] is ActivateGameObject) ||
                !(readyActions[1] is ActivateGameObject) ||
                rewardActions == null ||
                rewardActions.Length != 3 ||
                !(rewardActions[0] is SetPlayerDataBoolAction) ||
                !(rewardActions[1] is Wait) ||
                !(rewardActions[2] is SendEventToRegister) ||
                !HasOnlyTransition(
                    readyState,
                    TormentedRewardEventName,
                    TormentedRewardStateName) ||
                !HasOnlyTransition(
                    rewardState,
                    FsmEvent.Finished.Name,
                    TormentedEndStateName))
            {
                LogFailure(TormentedLocationName);
                return;
            }

            AppendCompletionAction(
                rewardState,
                TormentedLocationName
            );
        }

        private static bool HasOnlyTransition(
            FsmState state,
            string eventName,
            string targetStateName)
        {
            FsmTransition[] transitions = state?.Transitions;
            return transitions != null &&
                   transitions.Length == 1 &&
                   transitions[0] != null &&
                   string.Equals(
                       transitions[0].EventName,
                       eventName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       transitions[0].ToState,
                       targetStateName,
                       StringComparison.Ordinal);
        }

        private static void AppendCompletionAction(
            FsmState rewardState,
            string locationName)
        {
            CompleteMirrorLocation completion =
                new CompleteMirrorLocation(locationName);
            completion.Init(rewardState);

            FsmStateAction[] actions = rewardState.Actions;
            FsmStateAction[] patchedActions =
                new FsmStateAction[actions.Length + 1];
            Array.Copy(actions, patchedActions, actions.Length);
            patchedActions[actions.Length] = completion;
            rewardState.Actions = patchedActions;
        }

        private static bool ContainsCompletionAction(
            FsmState rewardState,
            string locationName)
        {
            if (rewardState?.Actions == null)
            {
                return false;
            }

            foreach (FsmStateAction action in rewardState.Actions)
            {
                CompleteMirrorLocation existing =
                    action as CompleteMirrorLocation;
                if (existing != null &&
                    string.Equals(
                        existing.LocationName,
                        locationName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void LogFailure(string locationName)
        {
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Trobbio mirror completion hook was not " +
                "installed for " + locationName + " because the " +
                "validated native reward sequence changed."
            );
        }

        private sealed class CompleteMirrorLocation : FsmStateAction
        {
            internal readonly string LocationName;

            internal CompleteMirrorLocation(string locationName)
            {
                LocationName = locationName;
            }

            public override void OnEnter()
            {
                try
                {
                    SaveState state = SaveState.Instance;
                    if (state != null &&
                        state.IsRandomized(ItemType.Tool) &&
                        state.IsLocationEnabled(LocationName) &&
                        state.IsLocationInSeed(LocationName) &&
                        !state.IsLocationChecked(LocationName))
                    {
                        state.CheckLocation(LocationName);
                    }
                }
                finally
                {
                    Finish();
                }
            }
        }
    }
}
