using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using PlayerDataVariableTestAction =
    HutongGames.PlayMaker.Actions.PlayerDataVariableTest;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Lets Escape from the Abyss and Ecstasy of the End use randomized
    /// Silk Soar ownership without collecting the native source.
    /// </summary>
    internal static class SilkSoarStoryPatches
    {
        private const string SceneName = "Dock_04";
        private const string OwnerName = "left1";
        private const string FsmName = "Advance Quest";
        private const string StateName = "State 2";
        private const string NativeFlag = "hasSuperJump";
        private const string EcstasyQuestAssetName = "Flea Games Pre";

        private sealed class SilkSoarSnapshot
        {
            internal PlayerData PlayerData;
            internal bool NativeSuperJump;
        }

        [HarmonyPatch(
            typeof(PlayerDataVariableTestAction),
            nameof(PlayerDataVariableTestAction.OnEnter)
        )]
        private static class EscapeQuestGatePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                PlayerDataVariableTestAction __instance,
                out SilkSoarSnapshot __state)
            {
                __state = null;

                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null ||
                    playerData == null ||
                    !state.IsRandomized(ItemType.Skill) ||
                    !IsExactEscapeQuestGate(__instance))
                {
                    return;
                }

                __state = new SilkSoarSnapshot
                {
                    PlayerData = playerData,
                    NativeSuperJump = playerData.hasSuperJump,
                };
                playerData.hasSuperJump = state.canSilkSoar;
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static Exception Finalizer(
                Exception __exception,
                SilkSoarSnapshot __state)
            {
                if (__state != null && __state.PlayerData != null)
                {
                    __state.PlayerData.hasSuperJump =
                        __state.NativeSuperJump;
                }

                return __exception;
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_IsAvailable")]
        private static class EcstasyWishGatePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                FullQuestBase __instance,
                out SilkSoarSnapshot __state)
            {
                __state = null;

                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null ||
                    playerData == null ||
                    !state.IsRandomized(ItemType.Skill) ||
                    __instance == null ||
                    !string.Equals(
                        __instance.name,
                        EcstasyQuestAssetName,
                        StringComparison.Ordinal))
                {
                    return;
                }

                __state = new SilkSoarSnapshot
                {
                    PlayerData = playerData,
                    NativeSuperJump = playerData.hasSuperJump,
                };
                playerData.hasSuperJump = state.canSilkSoar;
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static Exception Finalizer(
                Exception __exception,
                SilkSoarSnapshot __state)
            {
                if (__state != null && __state.PlayerData != null)
                {
                    __state.PlayerData.hasSuperJump =
                        __state.NativeSuperJump;
                }

                return __exception;
            }
        }

        private static bool IsExactEscapeQuestGate(
            PlayerDataVariableTestAction action)
        {
            if (action == null ||
                !action.Enabled ||
                action.Owner == null ||
                action.Fsm == null ||
                action.State == null ||
                action.VariableName == null ||
                !string.Equals(
                    action.Owner.scene.name,
                    SceneName,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    action.Owner.name,
                    OwnerName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.Fsm.Name,
                    FsmName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.State.Name,
                    StateName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.VariableName.Value,
                    NativeFlag,
                    StringComparison.Ordinal) ||
                action.ExpectedValue == null ||
                action.ExpectedValue.Type != VariableType.Bool ||
                action.ExpectedValue.useVariable ||
                !action.ExpectedValue.boolValue ||
                !IsNoEvent(action.IsExpectedEvent) ||
                !IsNamedEvent(
                    action.IsNotExpectedEvent,
                    "FINISHED"))
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            return actions != null &&
                   actions.Length == 4 &&
                   ReferenceEquals(actions[0], action) &&
                   string.Equals(
                       actions[1]?.GetType().FullName,
                       "QuestPlaymakerActions.CheckQuestStateV2",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       actions[2]?.GetType().FullName,
                       "QuestPlaymakerActions.BeginQuestV2",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       actions[3]?.GetType().FullName,
                       "DoGameMapUpdate",
                       StringComparison.Ordinal);
        }

        private static bool IsNoEvent(FsmEvent fsmEvent)
        {
            return fsmEvent == null || string.IsNullOrEmpty(fsmEvent.Name);
        }

        private static bool IsNamedEvent(
            FsmEvent fsmEvent,
            string expectedName)
        {
            return fsmEvent != null &&
                   !fsmEvent.IsGlobal &&
                   string.Equals(
                       fsmEvent.Name,
                       expectedName,
                       StringComparison.Ordinal);
        }
    }
}
