using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using SetPlayerDataBoolAction =
    HutongGames.PlayMaker.Actions.SetPlayerDataBool;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Reports four memory-boss checks at their victory-only
    /// boundaries. Native PlayerData predicates remain as idempotent
    /// fallbacks after the waking scene loads.
    /// </summary>
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class MemoryBossCompletionPatches
    {
        private const string ExitStateName = "Exit Memory";
        private const string WakeGateName = "door_wakeOnGround";
        private const string CoralKingSceneName = "Memory_Coral_Tower";
        private const string CoralKingParentName = "Boss Scene";
        private const string CoralKingObjectName = "Coral King";
        private const string CoralKingFsmName = "Control";
        private const string CoralKingVictorySourceState = "Death Stagger";
        private const string CoralKingVictoryState = "Heart Death Start";
        private const string CoralKingDefeatedFlag = "defeatedCoralKing";
        private const string CoralKingLocation = "Boss: Crust King Khann";

        private sealed class Entry
        {
            internal readonly string SceneName;
            internal readonly string ObjectName;
            internal readonly string FsmName;
            internal readonly string VictorySourceState;
            internal readonly string VictoryEvent;
            internal readonly string SecondaryVictorySourceState;
            internal readonly string ReturnSceneName;
            internal readonly string LocationName;
            internal readonly bool HasAudioSnapshotAction;

            internal Entry(
                string sceneName,
                string objectName,
                string fsmName,
                string victorySourceState,
                string victoryEvent,
                string secondaryVictorySourceState,
                string returnSceneName,
                string locationName,
                bool hasAudioSnapshotAction = false)
            {
                SceneName = sceneName;
                ObjectName = objectName;
                FsmName = fsmName;
                VictorySourceState = victorySourceState;
                VictoryEvent = victoryEvent;
                SecondaryVictorySourceState =
                    secondaryVictorySourceState;
                ReturnSceneName = returnSceneName;
                LocationName = locationName;
                HasAudioSnapshotAction = hasAudioSnapshotAction;
            }
        }

        private static readonly Entry[] Entries =
        {
            new Entry(
                "Shellwood_11b_Memory",
                "Boss Scene",
                "Scene End",
                "State 1",
                "BOSS DEFEAT",
                null,
                "Shellwood_11b",
                "Boss: Nyleth",
                hasAudioSnapshotAction: true
            ),
            new Entry(
                "Memory_Ant_Queen",
                "Boss Scene",
                "End Memory",
                "Idle",
                "BOSS DEFEAT",
                null,
                "Ant_Queen",
                "Boss: Skarrsinger Karmelita"
            ),
            new Entry(
                "Clover_10",
                "Boss Scene",
                "Scene End",
                "State 1",
                "BOSS DEFEAT",
                null,
                "Clover_01",
                "Boss: Clover Dancers"
            ),
        };

        /// <summary>
        /// Khann writes his persistent victory flag before running a long
        /// heart-death memory sequence. Report from the setter postfix so an
        /// AP reward can arrive during that sequence, while validating that
        /// this is the irreversible write.
        /// </summary>
        [HarmonyPatch(
            typeof(SetPlayerDataBoolAction),
            nameof(SetPlayerDataBoolAction.OnEnter)
        )]
        private static class CoralKingVictoryPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                SetPlayerDataBoolAction __instance)
            {
                if (IsExactCoralKingVictoryWrite(__instance))
                {
                    TryReportLocation(CoralKingLocation);
                }
            }
        }

        [HarmonyPostfix]
        private static void Postfix(PlayMakerFSM __instance)
        {
            Entry entry = FindEntry(__instance);
            if (entry == null)
            {
                return;
            }

            FsmState exitState = FindState(
                __instance,
                ExitStateName
            );
            if (exitState != null &&
                ContainsCompletionAction(
                    exitState,
                    entry.LocationName
                ))
            {
                return;
            }

            if (exitState == null ||
                !HasExactVictoryIncomingEdges(__instance, entry) ||
                !HasExactExitActions(exitState, entry))
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Memory-boss completion hook was not " +
                    "installed for " + entry.LocationName + " because " +
                    "the validated victory/exit graph changed."
                );
                return;
            }

            ReportMemoryBossCompletion completion =
                new ReportMemoryBossCompletion(entry.LocationName);
            completion.Init(exitState);

            FsmStateAction[] oldActions = exitState.Actions;
            FsmStateAction[] patchedActions =
                new FsmStateAction[oldActions.Length + 1];
            patchedActions[0] = completion;
            Array.Copy(
                oldActions,
                0,
                patchedActions,
                1,
                oldActions.Length
            );
            exitState.Actions = patchedActions;

            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Memory-boss completion anchored before " +
                "memory exit for " + entry.LocationName + "."
            );
        }

        private static Entry FindEntry(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.gameObject == null)
            {
                return null;
            }

            string sceneName = fsm.gameObject.scene.name;
            foreach (Entry entry in Entries)
            {
                if (string.Equals(
                        sceneName,
                        entry.SceneName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        fsm.gameObject.name,
                        entry.ObjectName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        fsm.FsmName,
                        entry.FsmName,
                        StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private static bool IsExactCoralKingVictoryWrite(
            SetPlayerDataBoolAction action)
        {
            if (action == null ||
                action.Owner == null ||
                action.Owner.transform == null ||
                action.Owner.transform.parent == null ||
                action.Fsm == null ||
                action.State == null ||
                action.boolName == null ||
                action.value == null ||
                PlayerData.instance == null ||
                !string.Equals(
                    action.Owner.scene.name,
                    CoralKingSceneName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.Owner.name,
                    CoralKingObjectName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.Owner.transform.parent.name,
                    CoralKingParentName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.Fsm.Name,
                    CoralKingFsmName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.State.Name,
                    CoralKingVictoryState,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.boolName.Value,
                    CoralKingDefeatedFlag,
                    StringComparison.Ordinal) ||
                !action.value.Value ||
                !PlayerData.instance.defeatedCoralKing)
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            if (actions == null ||
                actions.Length != 2 ||
                !ReferenceEquals(actions[0], action) ||
                !(actions[1] is RunFSM runFsm) ||
                !runFsm.Enabled ||
                runFsm.everyFrame ||
                (action.State.Transitions != null &&
                 action.State.Transitions.Length != 0))
            {
                return false;
            }

            return HasExactCoralKingVictoryIncomingEdge(action.Fsm);
        }

        private static bool HasExactCoralKingVictoryIncomingEdge(Fsm fsm)
        {
            if (fsm?.States == null)
            {
                return false;
            }

            int incomingCount = 0;
            foreach (FsmState state in fsm.States)
            {
                if (state == null || state.Transitions == null)
                {
                    continue;
                }

                foreach (FsmTransition transition in state.Transitions)
                {
                    if (transition == null ||
                        !string.Equals(
                            transition.ToState,
                            CoralKingVictoryState,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    incomingCount++;
                    if (!string.Equals(
                            state.Name,
                            CoralKingVictorySourceState,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            transition.EventName,
                            FsmEvent.Finished.Name,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            if (fsm.GlobalTransitions != null)
            {
                foreach (FsmTransition transition in fsm.GlobalTransitions)
                {
                    if (transition != null &&
                        string.Equals(
                            transition.ToState,
                            CoralKingVictoryState,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return incomingCount == 1;
        }

        private static FsmState FindState(
            PlayMakerFSM fsm,
            string stateName)
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

        private static bool ContainsCompletionAction(
            FsmState state,
            string locationName)
        {
            if (state.Actions == null)
            {
                return false;
            }

            foreach (FsmStateAction action in state.Actions)
            {
                if (action is ReportMemoryBossCompletion report &&
                    string.Equals(
                        report.LocationName,
                        locationName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasExactVictoryIncomingEdges(
            PlayMakerFSM fsm,
            Entry entry)
        {
            if (fsm.FsmStates == null)
            {
                return false;
            }

            int primaryIncomingCount = 0;
            int secondaryIncomingCount = 0;
            foreach (FsmState state in fsm.FsmStates)
            {
                if (state == null || state.Transitions == null)
                {
                    continue;
                }

                foreach (FsmTransition transition in state.Transitions)
                {
                    if (transition == null ||
                        !string.Equals(
                            transition.ToState,
                            ExitStateName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool isPrimaryVictory = string.Equals(
                        state.Name,
                        entry.VictorySourceState,
                        StringComparison.Ordinal
                    );
                    bool isSecondaryVictory =
                        !string.IsNullOrEmpty(
                            entry.SecondaryVictorySourceState
                        ) &&
                        string.Equals(
                            state.Name,
                            entry.SecondaryVictorySourceState,
                            StringComparison.Ordinal
                        );
                    if ((!isPrimaryVictory && !isSecondaryVictory) ||
                        !string.Equals(
                            transition.EventName,
                            entry.VictoryEvent,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }

                    if (isPrimaryVictory)
                    {
                        primaryIncomingCount++;
                    }
                    else
                    {
                        secondaryIncomingCount++;
                    }
                }
            }

            FsmTransition[] globalTransitions =
                fsm.FsmGlobalTransitions;
            if (globalTransitions != null)
            {
                foreach (FsmTransition transition in globalTransitions)
                {
                    if (transition != null &&
                        string.Equals(
                            transition.ToState,
                            ExitStateName,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            bool expectsSecondary = !string.IsNullOrEmpty(
                entry.SecondaryVictorySourceState
            );
            return primaryIncomingCount == 1 &&
                   secondaryIncomingCount ==
                       (expectsSecondary ? 1 : 0);
        }

        private static bool HasExactExitActions(
            FsmState exitState,
            Entry entry)
        {
            FsmStateAction[] actions = exitState.Actions;
            if (actions == null ||
                (exitState.Transitions != null &&
                 exitState.Transitions.Length != 0))
            {
                return false;
            }

            int expectedCount = entry.HasAudioSnapshotAction ? 6 : 5;
            if (actions.Length != expectedCount ||
                !(actions[0] is StartPreloadingScene preload))
            {
                return false;
            }

            if (entry.HasAudioSnapshotAction)
            {
                if (!(actions[1] is TransitionToAudioSnapshot))
                {
                    return false;
                }
            }
            else if (!(actions[1] is ScreenFader))
            {
                return false;
            }

            int offset = entry.HasAudioSnapshotAction ? 1 : 0;
            if (!(actions[1 + offset] is ScreenFader) ||
                !(actions[2 + offset] is Wait) ||
                !(actions[3 + offset] is SetMeshRenderer) ||
                !(actions[4 + offset] is BeginSceneTransition transition))
            {
                return false;
            }

            return preload.SceneName != null &&
                   string.Equals(
                       preload.SceneName.Value,
                       entry.ReturnSceneName,
                       StringComparison.Ordinal) &&
                   transition.sceneName != null &&
                   transition.entryGateName != null &&
                   string.Equals(
                       transition.sceneName.Value,
                       entry.ReturnSceneName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       transition.entryGateName.Value,
                       WakeGateName,
                       StringComparison.Ordinal);
        }

        private static void TryReportLocation(string locationName)
        {
            try
            {
                SaveState state = SaveState.Instance;
                if (state != null &&
                    state.IsRandomized(ItemType.Boss) &&
                    state.IsLocationEnabled(locationName) &&
                    state.IsLocationInSeed(locationName) &&
                    !state.IsLocationChecked(locationName))
                {
                    state.CheckLocation(locationName);
                }
            }
            catch (Exception exception)
            {
                RandomizerPlugin.Log?.LogError(
                    "[RANDOMIZER] Failed to report memory-boss " +
                    "location " + locationName + ": " + exception
                );
            }
        }

        private sealed class ReportMemoryBossCompletion : FsmStateAction
        {
            internal string LocationName { get; }

            internal ReportMemoryBossCompletion(string locationName)
            {
                LocationName = locationName;
            }

            public override void OnEnter()
            {
                try
                {
                    TryReportLocation(LocationName);
                }
                finally
                {
            // Reporting cannot strand the native memory exit.
                    Finish();
                }
            }
        }
    }
}
