using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class ShakraStockPersistencePatches
    {
        private const string ShakraFinalQuestName = "Shakra Final Quest";
        private const string BellhartSceneName = "Belltown";
        private const string MapperControlObjectName = "Mapper Control";
        private const string MapperControlFsmName = "Control";
        private const string MapperControlStateName = "Check";
        private const string MapperDialogueFsmName = "Dialogue";
        private const string MapperQuestStateName = "Quest?";

        private static bool ShouldKeepShakraOnsite(
            SaveState state,
            PlayerData playerData)
        {
            return state != null &&
                   state.IsRoomBound &&
                   playerData != null;
        }

        private static void SetDepartureFlags(
            PlayerData playerData,
            bool value)
        {
            playerData.mapperAway = value;
            playerData.MapperLeftBellhart = value;
            playerData.MapperLeftBoneForest = value;
            playerData.MapperLeftBonetown = value;
            playerData.MapperLeftCoralCaverns = value;
            playerData.MapperLeftCrawl = value;
            playerData.MapperLeftDocks = value;
            playerData.MapperLeftDustpens = value;
            playerData.MapperLeftGreymoor = value;
            playerData.MapperLeftHuntersNest = value;
            playerData.MapperLeftJudgeSteps = value;
            playerData.MapperLeftPeak = value;
            playerData.MapperLeftShadow = value;
            playerData.MapperLeftShellwood = value;
            playerData.MapperLeftWilds = value;
        }

        private static bool IsMapperDepartureField(string boolName)
        {
            switch (boolName)
            {
                case "MapperLeftBellhart":
                case "MapperLeftBoneForest":
                case "MapperLeftBonetown":
                case "MapperLeftCoralCaverns":
                case "MapperLeftCrawl":
                case "MapperLeftDocks":
                case "MapperLeftDustpens":
                case "MapperLeftGreymoor":
                case "MapperLeftHuntersNest":
                case "MapperLeftJudgeSteps":
                case "MapperLeftPeak":
                case "MapperLeftShadow":
                case "MapperLeftShellwood":
                case "MapperLeftWilds":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsNoEvent(FsmEvent fsmEvent)
        {
            return fsmEvent == null || string.IsNullOrEmpty(fsmEvent.Name);
        }

        private static bool IsEventNamed(
            FsmEvent fsmEvent,
            string expectedName)
        {
            return fsmEvent != null &&
                   string.Equals(
                       fsmEvent.Name,
                       expectedName,
                       StringComparison.Ordinal);
        }

        private static bool IsExactShakraFinalQuestGate(
            QuestPlaymakerActions.CheckQuestState action)
        {
            if (action == null ||
                !action.Enabled ||
                action.Owner == null ||
                action.Fsm == null ||
                action.State == null ||
                (!action.Owner.name.StartsWith(
                     "Mapper NPC",
                     StringComparison.Ordinal) &&
                 !action.Owner.name.StartsWith(
                     "Mapper Sit NPC",
                     StringComparison.Ordinal)) ||
                !string.Equals(
                    action.Fsm.Name,
                    MapperDialogueFsmName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.State.Name,
                    MapperQuestStateName,
                    StringComparison.Ordinal) ||
                !(action.Quest?.Value is FullQuestBase quest) ||
                !string.Equals(
                    quest.name,
                    ShakraFinalQuestName,
                    StringComparison.Ordinal) ||
                !IsEventNamed(action.NotTrackedEvent, "FINISHED") ||
                !IsEventNamed(action.TrackedEvent, "CANCEL") ||
                !IsEventNamed(action.CompletedEvent, "FINISHED"))
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            return actions != null &&
                   actions.Length == 1 &&
                   ReferenceEquals(actions[0], action);
        }

        private static bool IsExactBellhartFinalQuestGate(
            BoolTestMulti action)
        {
            if (action == null ||
                !action.Enabled ||
                action.Owner == null ||
                action.Fsm == null ||
                action.State == null ||
                !string.Equals(
                    action.Owner.scene.name,
                    BellhartSceneName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.Owner.name,
                    MapperControlObjectName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.Fsm.Name,
                    MapperControlFsmName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.State.Name,
                    MapperControlStateName,
                    StringComparison.Ordinal) ||
                action.boolVariables == null ||
                action.boolVariables.Length != 2 ||
                action.boolStates == null ||
                action.boolStates.Length != 2 ||
                !IsEventNamed(action.trueEvent, "NONE") ||
                !IsNoEvent(action.falseEvent) ||
                action.storeResult == null ||
                action.everyFrame)
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            return actions != null &&
                   actions.Length == 12 &&
                   ReferenceEquals(actions[9], action) &&
                   string.Equals(
                       actions[6]?.GetType().FullName,
                       "HutongGames.PlayMaker.Actions.GetPlayerDataBool",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       actions[7]?.GetType().FullName,
                       "QuestPlaymakerActions.GetQuestState",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       actions[8]?.GetType().FullName,
                       "HutongGames.PlayMaker.Actions.BoolAnyTrue",
                       StringComparison.Ordinal) &&
                   actions[10] is PlayerDataBoolTest &&
                   actions[11] is PlayerDataBoolTest;
        }

        internal static bool TryPreserveShakraStock(
            SaveState state,
            PlayerData playerData)
        {
            if (!ShouldKeepShakraOnsite(state, playerData))
            {
                return false;
            }

            SetDepartureFlags(playerData, false);
            return true;
        }

        internal static bool ReconcileShakraStock(
            SaveState state,
            PlayerData playerData)
        {
            return TryPreserveShakraStock(state, playerData);
        }

        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.MapperLeaveAll))]
        private static class MapperLeaveAllPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(PlayerData __instance)
            {
                TryPreserveShakraStock(SaveState.Instance, __instance);
            }
        }

        [HarmonyPatch(
            typeof(GameManager),
            nameof(GameManager.MapperLeavePreviousLocations),
            new Type[] { typeof(string) })]
        private static class MapperLeavePreviousLocationsPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                TryPreserveShakraStock(
                    SaveState.Instance,
                    PlayerData.instance
                );
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.TimePasses))]
        private static class TimePassesPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                TryPreserveShakraStock(
                    SaveState.Instance,
                    PlayerData.instance
                );
            }
        }

        [HarmonyPatch(
            typeof(SceneTravelerTempEval),
            nameof(SceneTravelerTempEval.OnEnter))]
        private static class SceneTravelerEvaluationPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                TryPreserveShakraStock(
                    SaveState.Instance,
                    PlayerData.instance
                );
            }
        }

        [HarmonyPatch(
            typeof(GameManager),
            nameof(GameManager.FinishedEnteringScene))]
        private static class FinishedEnteringScenePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                TryPreserveShakraStock(
                    SaveState.Instance,
                    PlayerData.instance
                );
            }
        }

        [HarmonyPatch(
            typeof(QuestPlaymakerActions.CheckQuestState),
            "DoQuestAction")]
        private static class ShakraFinalQuestDialoguePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                QuestPlaymakerActions.CheckQuestState __instance)
            {
                if (!IsExactShakraFinalQuestGate(__instance) ||
                    !ShouldKeepShakraOnsite(
                        SaveState.Instance,
                        PlayerData.instance))
                {
                    return true;
                }

                __instance.Fsm.Event(__instance.NotTrackedEvent);
                return false;
            }
        }

        [HarmonyPatch(typeof(BoolTestMulti), "DoAllTrue")]
        private static class BellhartFinalQuestVisibilityPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(BoolTestMulti __instance)
            {
                if (!IsExactBellhartFinalQuestGate(__instance) ||
                    !ShouldKeepShakraOnsite(
                        SaveState.Instance,
                        PlayerData.instance))
                {
                    return true;
                }

                __instance.storeResult.Value = false;
                __instance.Fsm.Event(__instance.falseEvent);
                return false;
            }
        }

        [HarmonyPatch(
            typeof(DeactivateIfPlayerdataTrue),
            "ForceEvaluate")]
        private static class MapperDepartureVisibilityPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                DeactivateIfPlayerdataTrue __instance)
            {
                if (__instance == null ||
                    !IsMapperDepartureField(__instance.boolName) ||
                    !TryPreserveShakraStock(
                        SaveState.Instance,
                        PlayerData.instance
                    ))
                {
                    return true;
                }

                GameObject target =
                    __instance.objectToDeactivate ?? __instance.gameObject;
                if (target != null)
                {
                    target.SetActive(true);
                }

                return false;
            }
        }
    }
}
