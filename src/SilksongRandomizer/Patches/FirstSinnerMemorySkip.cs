using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Finishes First Sinner at the Slab reward and returns through the native
    /// waking gate without loading the memory scene.
    /// </summary>
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class FirstSinnerMemorySkip
    {
        private const string SourceSceneName = "Slab_10b";
        private const string SourceObjectName =
            "Shrine First Weaver NPC";
        private const string BossObjectName = "Shrine First Weaver";
        private const string SourceFsmName = "Inspection";
        private const string TransitionStateName =
            "To First Sinner Memory";
        private const string CollectedCheckStateName = "Collected Check";
        private const string SkillMessageStateName = "Skill Msg";
        private const string AutoEquipStateName = "Auto Equip";
        private const string MemorySceneName = "Memory_First_Sinner";
        private const string WakeGateName = "door_wakeOnGround";
        private const string AchievementName = "DEFEATED_FIRST_SINNER";
        private const string RuneRageLocationName = "Rune Rage";
        private const string FirstSinnerLocationName =
            "Boss: First Sinner";
        private const string SilkBombPlayerDataBool = "hasSilkBomb";
        private const string EquipToolVariableName = "Equip Tool";
        private const string SkillItemVariableName = "Skill Item";
        private const float DirectReturnDelay = 1.5f;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(PlayMakerFSM __instance)
        {
            if (SaveState.Instance == null || !IsSourceFsm(__instance))
            {
                return;
            }

            FsmState transitionState = FindState(
                __instance,
                TransitionStateName
            );
            if (ContainsCompletionAction(transitionState))
            {
                return;
            }

            FsmState collectedCheck = FindState(
                __instance,
                CollectedCheckStateName
            );
            FsmState skillMessage = FindState(
                __instance,
                SkillMessageStateName
            );
            FsmState autoEquip = FindState(
                __instance,
                AutoEquipStateName
            );
            if (!MatchesShippedTransition(
                    __instance,
                    transitionState,
                    collectedCheck,
                    skillMessage,
                    autoEquip,
                    out BeginSceneTransition transition,
                    out Wait wait,
                    out FsmObject equipTool,
                    out FsmGameObject messagePrefab,
                    out FsmObject skillItem))
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] First Sinner memory skip failed closed " +
                    "because the Slab reward FSM changed."
                );
                return;
            }

            CompleteFirstSinnerReward completion =
                new CompleteFirstSinnerReward(
                    equipTool,
                    messagePrefab,
                    skillItem
                );
            completion.Init(transitionState);

            transition.sceneName.Value = SourceSceneName;
            wait.time.Value = DirectReturnDelay;
            transitionState.Actions = new FsmStateAction[]
            {
                transitionState.Actions[0],
                completion,
                wait,
                transition,
            };

            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] First Sinner memory now completes at the " +
                "Slab reward and returns through the waking gate."
            );
        }

        [HarmonyPatch(
            typeof(PlayerDataBoolTest),
            nameof(PlayerDataBoolTest.OnEnter)
        )]
        private static class RandomizedRuneRageCompletionGatePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerDataBoolTest __instance)
            {
                SaveState state = SaveState.Instance;
                if (!IsActiveLocation(
                        state,
                        ItemType.Spell,
                        RuneRageLocationName) ||
                    !state.IsLocationChecked(RuneRageLocationName) ||
                    !TryGetCheckedSourceEvent(
                        __instance,
                        out FsmEvent collectedEvent))
                {
                    return true;
                }

                __instance.Fsm.Event(collectedEvent);
                __instance.Finish();
                return false;
            }
        }

        private static bool IsSourceFsm(PlayMakerFSM fsm)
        {
            return fsm != null &&
                   fsm.gameObject != null &&
                   string.Equals(
                       fsm.gameObject.scene.name,
                       SourceSceneName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       fsm.gameObject.name,
                       SourceObjectName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       fsm.FsmName,
                       SourceFsmName,
                       StringComparison.Ordinal);
        }

        private static bool MatchesShippedTransition(
            PlayMakerFSM fsm,
            FsmState transitionState,
            FsmState collectedCheck,
            FsmState skillMessage,
            FsmState autoEquip,
            out BeginSceneTransition transition,
            out Wait wait,
            out FsmObject equipTool,
            out FsmGameObject messagePrefab,
            out FsmObject skillItem)
        {
            transition = null;
            wait = null;
            equipTool = null;
            messagePrefab = null;
            skillItem = null;

            FsmStateAction[] transitionActions = transitionState?.Actions;
            FsmStateAction[] collectedActions = collectedCheck?.Actions;
            FsmStateAction[] messageActions = skillMessage?.Actions;
            FsmStateAction[] autoEquipActions = autoEquip?.Actions;
            if (transitionActions == null ||
                transitionActions.Length != 3 ||
                !transitionState.IsSequence ||
                HasTransitions(transitionState) ||
                !(transitionActions[0] is
                    AwardAchievementProgress achievement) ||
                achievement.Key == null ||
                achievement.Key.UsesVariable ||
                !string.Equals(
                    achievement.Key.Value,
                    AchievementName,
                    StringComparison.Ordinal) ||
                achievement.CurrentValue == null ||
                !achievement.CurrentValue.IsNone ||
                achievement.MaxValue == null ||
                !achievement.MaxValue.IsNone ||
                !(transitionActions[1] is Wait nativeWait) ||
                nativeWait.time == null ||
                nativeWait.time.UsesVariable ||
                Math.Abs(nativeWait.time.Value - 4f) > 0.001f ||
                !IsNoEvent(nativeWait.finishEvent) ||
                nativeWait.realTime ||
                !(transitionActions[2] is
                    BeginSceneTransition nativeTransition) ||
                nativeTransition.sceneName == null ||
                nativeTransition.sceneName.UsesVariable ||
                nativeTransition.entryGateName == null ||
                nativeTransition.entryGateName.UsesVariable ||
                nativeTransition.entryDelay == null ||
                nativeTransition.entryDelay.UsesVariable ||
                !string.Equals(
                    nativeTransition.sceneName.Value,
                    MemorySceneName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    nativeTransition.entryGateName.Value,
                    WakeGateName,
                    StringComparison.Ordinal) ||
                Math.Abs(nativeTransition.entryDelay.Value) > 0.001f ||
                nativeTransition.preventCameraFadeOut ||
                collectedActions == null ||
                collectedActions.Length != 1 ||
                !(collectedActions[0] is
                    PlayerDataBoolTest collectedTest) ||
                !IsNamedLocalEvent(collectedTest.isTrue, "COLLECTED") ||
                !IsNoEvent(collectedTest.isFalse) ||
                !HasOnlyTransitions(
                    collectedCheck,
                    Tuple.Create("COLLECTED", "Ability Collected"),
                    Tuple.Create(FsmEvent.Finished.Name, "Idle Setup")) ||
                messageActions == null ||
                messageActions.Length != 1 ||
                !(messageActions[0] is SpawnSkillGetMsg message) ||
                !HasOnlyTransition(
                    skillMessage,
                    FsmEvent.Finished.Name,
                    "Heal") ||
                autoEquipActions == null ||
                autoEquipActions.Length != 1 ||
                !(autoEquipActions[0] is AutoEquipTool equip) ||
                !HasOnlyTransition(
                    autoEquip,
                    FsmEvent.Finished.Name,
                    "Msg Type?") ||
                !HasExactIncomingEdges(fsm, TransitionStateName))
            {
                return false;
            }

            FsmObject expectedEquipTool =
                fsm.FsmVariables?.GetFsmObject(EquipToolVariableName);
            FsmObject expectedSkillItem =
                fsm.FsmVariables?.GetFsmObject(SkillItemVariableName);
            if (expectedEquipTool == null ||
                expectedSkillItem == null ||
                !IsExactFsmVariable(
                    equip.Tool,
                    expectedEquipTool,
                    EquipToolVariableName) ||
                !IsExactFsmVariable(
                    message.Skill,
                    expectedSkillItem,
                    SkillItemVariableName) ||
                message.MsgPrefab == null)
            {
                return false;
            }

            transition = nativeTransition;
            wait = nativeWait;
            equipTool = expectedEquipTool;
            messagePrefab = message.MsgPrefab;
            skillItem = expectedSkillItem;
            return true;
        }

        private static bool HasExactIncomingEdges(
            PlayMakerFSM fsm,
            string targetStateName)
        {
            if (fsm?.FsmStates == null)
            {
                return false;
            }

            int incomingCount = 0;
            foreach (FsmState state in fsm.FsmStates)
            {
                if (state?.Transitions == null)
                {
                    continue;
                }

                foreach (FsmTransition transition in state.Transitions)
                {
                    if (transition == null ||
                        !string.Equals(
                            transition.ToState,
                            targetStateName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    incomingCount++;
                    bool isMemoryBranch =
                        string.Equals(
                            state.Name,
                            "To Memory?",
                            StringComparison.Ordinal) &&
                        string.Equals(
                            transition.EventName,
                            "RUNE BOMB",
                            StringComparison.Ordinal);
                    bool isFinishedRewardBranch =
                        string.Equals(
                            state.Name,
                            "Rune Bomb?",
                            StringComparison.Ordinal) &&
                        string.Equals(
                            transition.EventName,
                            "CHANGE SCENE",
                            StringComparison.Ordinal);
                    if (!isMemoryBranch && !isFinishedRewardBranch)
                    {
                        return false;
                    }
                }
            }

            if (fsm.FsmGlobalTransitions != null)
            {
                foreach (FsmTransition transition in fsm.FsmGlobalTransitions)
                {
                    if (transition != null &&
                        string.Equals(
                            transition.ToState,
                            targetStateName,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return incomingCount == 2;
        }

        private static bool IsExactFsmVariable(
            FsmObject actionVariable,
            FsmObject expectedVariable,
            string variableName)
        {
            return actionVariable != null &&
                   actionVariable.UsesVariable &&
                   string.Equals(
                       actionVariable.Name,
                       variableName,
                       StringComparison.Ordinal) &&
                   ReferenceEquals(
                       actionVariable.CastVariable,
                       expectedVariable);
        }

        private static bool TryGetCheckedSourceEvent(
            PlayerDataBoolTest action,
            out FsmEvent collectedEvent)
        {
            collectedEvent = null;
            if (action == null ||
                action.Owner == null ||
                action.Fsm == null ||
                action.State == null ||
                action.boolName == null ||
                !string.Equals(
                    action.Owner.scene.name,
                    SourceSceneName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.Fsm.Name,
                    SourceFsmName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.boolName.Value,
                    SilkBombPlayerDataBool,
                    StringComparison.Ordinal) ||
                !IsNoEvent(action.isFalse))
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            if (string.Equals(
                    action.Owner.name,
                    SourceObjectName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    action.State.Name,
                    CollectedCheckStateName,
                    StringComparison.Ordinal) &&
                actions != null &&
                actions.Length == 1 &&
                ReferenceEquals(actions[0], action) &&
                IsNamedLocalEvent(action.isTrue, "COLLECTED") &&
                HasOnlyTransitions(
                    action.State,
                    Tuple.Create("COLLECTED", "Ability Collected"),
                    Tuple.Create(FsmEvent.Finished.Name, "Idle Setup")))
            {
                collectedEvent = action.isTrue;
                return true;
            }

            if (string.Equals(
                    action.Owner.name,
                    BossObjectName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    action.State.Name,
                    "Init",
                    StringComparison.Ordinal) &&
                actions != null &&
                actions.Length == 25 &&
                ReferenceEquals(actions[0], action) &&
                IsNamedLocalEvent(action.isTrue, "COMPLETE") &&
                HasTransition(action.State, "COMPLETE", "Complete") &&
                HasTransition(
                    action.State,
                    FsmEvent.Finished.Name,
                    "Idle"))
            {
                collectedEvent = action.isTrue;
                return true;
            }

            return false;
        }

        private static bool ContainsCompletionAction(FsmState state)
        {
            if (state?.Actions == null)
            {
                return false;
            }

            foreach (FsmStateAction action in state.Actions)
            {
                if (action is CompleteFirstSinnerReward)
                {
                    return true;
                }
            }

            return false;
        }

        private static FsmState FindState(
            PlayMakerFSM fsm,
            string stateName)
        {
            return fsm?.Fsm?.GetState(stateName);
        }

        private static bool HasTransitions(FsmState state)
        {
            return state?.Transitions != null &&
                   state.Transitions.Length != 0;
        }

        private static bool HasTransition(
            FsmState state,
            string eventName,
            string targetStateName)
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
                        targetStateName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasOnlyTransition(
            FsmState state,
            string eventName,
            string targetStateName)
        {
            return state?.Transitions != null &&
                   state.Transitions.Length == 1 &&
                   HasTransition(state, eventName, targetStateName);
        }

        private static bool HasOnlyTransitions(
            FsmState state,
            params Tuple<string, string>[] expected)
        {
            if (state?.Transitions == null ||
                state.Transitions.Length != expected.Length)
            {
                return false;
            }

            foreach (Tuple<string, string> transition in expected)
            {
                if (!HasTransition(state, transition.Item1, transition.Item2))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsNoEvent(FsmEvent fsmEvent)
        {
            return fsmEvent == null || string.IsNullOrEmpty(fsmEvent.Name);
        }

        private static bool IsNamedLocalEvent(
            FsmEvent fsmEvent,
            string eventName)
        {
            return fsmEvent != null &&
                   !fsmEvent.IsSystemEvent &&
                   !fsmEvent.IsGlobal &&
                   string.Equals(
                       fsmEvent.Name,
                       eventName,
                       StringComparison.Ordinal);
        }

        private static bool IsActiveLocation(
            SaveState state,
            ItemType itemType,
            string locationName)
        {
            return state != null &&
                   state.IsRandomized(itemType) &&
                   state.IsLocationEnabled(locationName) &&
                   state.IsLocationInSeed(locationName);
        }

        private static void ReportLocation(
            SaveState state,
            ItemType itemType,
            string locationName)
        {
            if (IsActiveLocation(state, itemType, locationName) &&
                !state.IsLocationChecked(locationName))
            {
                state.CheckLocation(locationName);
            }
        }

        private sealed class CompleteFirstSinnerReward : FsmStateAction
        {
            private readonly FsmObject equipTool;
            private readonly FsmGameObject messagePrefab;
            private readonly FsmObject skillItem;
            private bool completed;

            internal CompleteFirstSinnerReward(
                FsmObject equipTool,
                FsmGameObject messagePrefab,
                FsmObject skillItem)
            {
                this.equipTool = equipTool;
                this.messagePrefab = messagePrefab;
                this.skillItem = skillItem;
            }

            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null || playerData == null)
                {
                    RandomizerPlugin.Log?.LogError(
                        "[RANDOMIZER] First Sinner reward could not finish " +
                        "because the save state was unavailable."
                    );
                    Finish();
                    return;
                }

                bool randomizedRuneRage = IsActiveLocation(
                    state,
                    ItemType.Spell,
                    RuneRageLocationName
                );
                if (randomizedRuneRage)
                {
                    ReportLocation(
                        state,
                        ItemType.Spell,
                        RuneRageLocationName
                    );
                    FinishReward(state, playerData, false);
                    return;
                }

                ToolItem tool = equipTool?.Value as ToolItem;
                ToolItemSkill skill = skillItem?.Value as ToolItemSkill;
                SkillGetMsg prefab = messagePrefab?.Value == null
                    ? null
                    : messagePrefab.Value.GetComponent<SkillGetMsg>();
                if (tool == null || skill == null || prefab == null)
                {
                    RandomizerPlugin.Log?.LogError(
                        "[RANDOMIZER] First Sinner reward could not resolve " +
                        "the Rune Rage assets."
                    );
                    Finish();
                    return;
                }

                playerData.hasSilkBomb = true;
                ToolItemManager.AutoEquip(tool);
                SkillGetMsg.Spawn(
                    prefab,
                    skill,
                    () => FinishReward(state, playerData, true)
                );
            }

            private void FinishReward(
                SaveState state,
                PlayerData playerData,
                bool nativeRuneRage)
            {
                if (completed)
                {
                    return;
                }
                completed = true;

                try
                {
                    playerData.defeatedFirstWeaver = true;
                    ReportLocation(
                        state,
                        ItemType.Boss,
                        FirstSinnerLocationName
                    );

                    GameManager gameManager = GameManager.UnsafeInstance;
                    if (gameManager != null)
                    {
                        if (nativeRuneRage)
                        {
                            gameManager.QueueAutoSave(
                                AutoSaveName.GAINED_RUNE_BOMB
                            );
                        }
                        gameManager.QueueSaveGame();
                    }

                    if (nativeRuneRage)
                    {
                        ToolItemManager.ReportToolUnlocked(
                            ToolItemType.Skill
                        );
                    }
                }
                catch (Exception exception)
                {
                    RandomizerPlugin.Log?.LogError(
                        "[RANDOMIZER] First Sinner reward completion failed: " +
                        exception
                    );
                }
                finally
                {
                    Finish();
                }
            }
        }
    }
}
