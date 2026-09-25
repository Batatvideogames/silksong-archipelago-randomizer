using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using SetPlayerDataBoolAction =
    HutongGames.PlayMaker.Actions.SetPlayerDataBool;

namespace SilksongRandomizer.Patches
{
    internal static class TrobbioMirrorRewardPatches
    {
        private const string SceneName = "Library_13";
        private const string FsmName = "Control";
        private const string RegularOwnerName = "Trobbio";
        private const string RegularParentName = "Boss Scene Trobbio";
        private const string StageOwnerName = "Grand Stage Scene";
        private const string RegularRewardStateName = "Collapse";
        private const string RegularEndStateName = "Battle End";
        private const string RegularJournalKey = "Trobbio";
        private const string TormentedReadyStateName = "Trobbio Ready 2";
        private const string TormentedRewardEventName =
            "TORMENTED BATTLE END";
        private const string TormentedRewardStateName =
            "Spawn Tormented Item";
        private const string TormentedEndStateName = "End Battle";
        private const string TormentedDefeatedFlag =
            "defeatedTormentedTrobbio";
        private const string RegularLocationName =
            "Tool Unlock: Dazzle Bind";
        private const string TormentedLocationName =
            "Tool Unlock: Dazzle Bind Upgraded";

        [HarmonyPatch(
            typeof(ActivateGameObject),
            nameof(ActivateGameObject.OnEnter)
        )]
        private static class RegularRewardPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ActivateGameObject __instance)
            {
                if (IsRegularRewardAction(__instance))
                {
                    CompleteLocation(RegularLocationName);
                }
            }
        }

        [HarmonyPatch(
            typeof(SetPlayerDataBoolAction),
            nameof(SetPlayerDataBoolAction.OnEnter)
        )]
        private static class TormentedRewardPatch
        {
            [HarmonyPostfix]
            private static void Postfix(SetPlayerDataBoolAction __instance)
            {
                if (IsTormentedRewardAction(__instance))
                {
                    CompleteLocation(TormentedLocationName);
                }
            }
        }

        [HarmonyPatch(typeof(CollectableItemPickup), "CheckActivation")]
        private static class RewardPickupPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(CollectableItemPickup __instance)
            {
                if (__instance == null || __instance.gameObject.scene.name != SceneName)
                    return true;

                string location = GetPickupLocation(Utils.GetHierarchyPath(__instance.transform));
                SaveState state = SaveState.Instance;
                if (location == null || state == null || !state.IsRandomized(ItemType.Tool) ||
                    !state.IsLocationEnabled(location) || !state.IsLocationInSeed(location) ||
                    !state.IsLocationChecked(location))
                    return true;

                __instance.SetActivation(true);
                __instance.gameObject.SetActive(false);
                return false;
            }
        }

        internal static string GetPickupLocation(string path)
        {
            switch (path)
            {
                case "Grand Stage Scene/Re-Entry Pickup":
                case "Grand Stage Scene/Boss Scene Trobbio/Collectable Item Pickup":
                    return RegularLocationName;
                case "Grand Stage Scene/Re-Entry Pickup Upgrade":
                case "Grand Stage Scene/Boss Scene TormentedTrobbio/Item Spawn/Collectable Item Pickup":
                    return TormentedLocationName;
                default:
                    return null;
            }
        }

        private static bool IsRegularRewardAction(
            ActivateGameObject action)
        {
            if (!IsExactAction(
                    action,
                    RegularOwnerName,
                    RegularRewardStateName) ||
                action.Owner.transform.parent == null ||
                !string.Equals(
                    action.Owner.transform.parent.name,
                    RegularParentName,
                    StringComparison.Ordinal) ||
                action.Owner.transform.parent.parent == null ||
                !string.Equals(
                    action.Owner.transform.parent.parent.name,
                    StageOwnerName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            FsmState state = action.State;
            FsmStateAction[] actions = state.Actions;
            return actions != null &&
                   actions.Length == 8 &&
                   actions[0] is AwardQueuedAchievements &&
                   actions[1] is CancelCameraShake &&
                   actions[2] is Tk2dPlayAnimation &&
                   actions[3] is SetPositionToObject &&
                   ReferenceEquals(actions[4], action) &&
                   actions[5] is Wait &&
                   actions[6] is AudioPlayerOneShotSingle &&
                   actions[7] is AudioPlayerOneShotSingle &&
                   HasOnlyTransition(
                       state,
                       FsmEvent.Finished.Name,
                       RegularEndStateName);
        }

        internal static void ReconcileRegularReward()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null ||
                !playerData.defeatedTrobbio ||
                playerData.EnemyJournalKillData == null ||
                playerData.EnemyJournalKillData
                    .GetKillData(RegularJournalKey).Kills <= 0)
            {
                return;
            }

            CompleteLocation(RegularLocationName);
        }

        private static bool IsTormentedRewardAction(
            SetPlayerDataBoolAction action)
        {
            if (!IsExactAction(
                    action,
                    StageOwnerName,
                    TormentedRewardStateName) ||
                action.boolName == null ||
                action.value == null ||
                !string.Equals(
                    action.boolName.Value,
                    TormentedDefeatedFlag,
                    StringComparison.Ordinal) ||
                !action.value.Value)
            {
                return false;
            }

            FsmState state = action.State;
            FsmStateAction[] actions = state.Actions;
            FsmState readyState =
                action.Fsm.GetState(TormentedReadyStateName);
            FsmStateAction[] readyActions = readyState?.Actions;
            return actions != null &&
                   actions.Length == 3 &&
                   ReferenceEquals(actions[0], action) &&
                   actions[1] is Wait &&
                   actions[2] is SendEventToRegister &&
                   readyActions != null &&
                   readyActions.Length == 2 &&
                   readyActions[0] is ActivateGameObject &&
                   readyActions[1] is ActivateGameObject &&
                   HasOnlyTransition(
                       readyState,
                       TormentedRewardEventName,
                       TormentedRewardStateName) &&
                   HasOnlyTransition(
                       state,
                       FsmEvent.Finished.Name,
                       TormentedEndStateName);
        }

        private static bool IsExactAction(
            FsmStateAction action,
            string ownerName,
            string stateName)
        {
            return action != null &&
                   action.Owner != null &&
                   action.Owner.transform != null &&
                   action.Fsm != null &&
                   action.State != null &&
                   string.Equals(
                       action.Owner.scene.name,
                       SceneName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       action.Owner.name,
                       ownerName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       action.Fsm.Name,
                       FsmName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       action.State.Name,
                       stateName,
                       StringComparison.Ordinal);
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

        private static void CompleteLocation(string locationName)
        {
            SaveState state = SaveState.Instance;
            if (state != null &&
                state.IsRandomized(ItemType.Tool) &&
                state.IsLocationEnabled(locationName) &&
                state.IsLocationInSeed(locationName) &&
                !state.IsLocationChecked(locationName))
            {
                state.CheckLocation(locationName);
            }
        }
    }
}
