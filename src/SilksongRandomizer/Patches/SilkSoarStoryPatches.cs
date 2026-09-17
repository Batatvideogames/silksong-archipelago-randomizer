using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using ConvertBoolToStringAction =
    HutongGames.PlayMaker.Actions.ConvertBoolToString;
using GetPlayerDataVariableAction =
    HutongGames.PlayMaker.Actions.GetPlayerDataVariable;
using GetPlayerDataBoolAction =
    HutongGames.PlayMaker.Actions.GetPlayerDataBool;
using PlayerDataVariableTestAction =
    HutongGames.PlayMaker.Actions.PlayerDataVariableTest;
using SendEventByNameAction =
    HutongGames.PlayMaker.Actions.SendEventByName;
using SetPlayerDataBoolAction =
    HutongGames.PlayMaker.Actions.SetPlayerDataBool;

namespace SilksongRandomizer.Patches
{
    internal static class SilkSoarStoryPatches
    {
        private const string EscapeSceneName = "Dock_04";
        private const string OwnerName = "left1";
        private const string FsmName = "Advance Quest";
        private const string StateName = "State 2";
        private const string NativeFlag = "hasSuperJump";
        private const string SilkSoarSourceSceneName = "Abyss_08";
        private const string SilkSoarSourcePath =
            "weaver_spire_base control/Shrine Weaver Ability";
        private const string SilkSoarSourceFsmName = "Inspection";
        private const string SilkSoarSourceEndStateName = "End";
        private const string SilkSoarSourceFinishedStateName =
            "Set Finished";
        private const string SilkSoarSourceLocationName =
            "Skill Unlock: Silk Soar";
        private const string ShrineSequenceEndEventName =
            "SHRINE SEQUENCE END";
        private const string EcstasyQuestAssetName = "Flea Games Pre";
        private const string DockSceneName = "Dock_12";
        private const string ForgeSceneName = "Room_Forge";
        private const string BellInteriorSceneName = "Room_Diving_Bell";
        private const string BellBrokenInteriorSceneName =
            "Room_Diving_Bell_Abyss";
        private const string BellFixedInteriorSceneName =
            "Room_Diving_Bell_Abyss_Fixed";
        private const string AbyssDivingBellSceneName = "Abyss_03";
        private const string DialogueFsmName = "Dialogue";
        private const string BallowPostStateName = "Post Ver?";
        private const string ForgePostStateName = "Post Abyss?";
        private const string BallowStandardPath =
            "Diving Bell/States/Standard/Ballow Diving Bell NPC";
        private const string BallowUpgradedPath =
            "Diving Bell/States/Repair/Upgraded/Ballow Diving Bell NPC";
        private const string ForgeDaughterPath = "_NPCs/Forge Daughter";
        private const string DivingBellStatesPath = "Diving Bell/States";
        private const string DivingBellRepairPath =
            "Diving Bell/States/Repair";
        private const string DivingBellStandardPath =
            "Diving Bell/States/Standard";
        private const string DivingBellHalfUpgradedPath =
            "Diving Bell/States/Repair/Half_Upgraded";
        private const string DivingBellUpgradedPath =
            "Diving Bell/States/Repair/Upgraded";
        private const string AbyssDivingBellStatesPath =
            "Diving Bell States";
        private const string AbyssDivingBellGonePath =
            "Diving Bell States/Gone";
        private const string AbyssDivingBellBrokenPath =
            "Diving Bell States/Diving Bell Broken";
        private const string AbyssDivingBellGoneZonePath =
            "Diving Bell States/Gone/Bell Gone Zone";
        private const string AbyssDivingBellUpgradedPath =
            "Diving Bell States/Gone/Diving Bell Upgraded";
        private const string AbyssDivingBellEntryGateName = "door2";
        private const string BellBrokenSurfaceEntryGateName =
            "door_wakeOnGround";
        private const string BellFixedSurfaceEntryGateName =
            "door_cinematicEnd";
        private const string DivingBellTrapdoorPath = "trapdoor states";
        private const string DivingBellTrapdoorOpenPath =
            "trapdoor states/trapdoor open";
        private const string DivingBellTrapdoorClosedPath =
            "trapdoor states/trapdoor closed";
        private const string DivingBellControlsPath = "Controls inspect";
        private const string BellSequencePath = "Travel Sequence Control";
        private const string BellDialoguePath =
            "Travel Sequence Control/Gramaphone Interact";
        private const string BellSequenceFsmName = "Sequence";
        private const string BellGoingDownStateName = "Going Down";
        private const string BellFirstPostStateName = "First Post?";
        private const string BellTravelDialogueStateName = "Travel Dlg";
        private const string BellBenchDialogueStateName = "Bench Dlg";
        private const string BellShieldedPath = "Diving Bell Shielded";
        private const string DivingBellInspectQuestName =
            "Diving Bell Pt1 Inspect";
        private const string DivingBellBallowQuestName =
            "Diving Bell Pt2 Ballow";
        private const string DivingBellDescendQuestName =
            "Diving Bell Pt3 Descend";
        private const string DivingBellAbyssQuestName =
            "Black Thread Pt2 Abyss";
        private const string BallowMovedFlag =
            "BallowMovedToDivingBell";
        private const string WhiteFlowerFlag = "HasWhiteFlower";

        private static readonly System.Reflection.FieldInfo
            ActivatorTestField = AccessTools.Field(
                typeof(TestGameObjectActivator),
                "playerDataTest"
            );
        private static readonly System.Reflection.FieldInfo
            ActivatorTargetField = AccessTools.Field(
                typeof(TestGameObjectActivator),
                "activateGameObject"
            );
        private static readonly System.Reflection.FieldInfo
            DeactivatorTargetField = AccessTools.Field(
                typeof(TestGameObjectActivator),
                "deactivateGameObject"
            );

        private sealed class SilkSoarSnapshot
        {
            internal PlayerData PlayerData;
            internal bool NativeSuperJump;
            internal PlayerDataTest.Test[] SelectorTests;
            internal PlayerDataTest.Test NativeSelectorTest;
            internal bool RestoreSelectorTest;
            internal GetPlayerDataBoolAction DialogueBoolAction;
            internal string NativeDialogueBoolName;
            internal bool SynchronizeBallow;
        }

        [HarmonyPatch(
            typeof(PlayerDataVariableTestAction),
            nameof(PlayerDataVariableTestAction.OnEnter)
        )]
        private static class PlayerDataStoryGatePatch
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
                bool isBallowPostGate =
                    IsExactBallowPostGate(__instance);
                bool isEscapeGate = IsExactEscapeQuestGate(__instance);
                if (state == null ||
                    playerData == null ||
                    !IsExactPlayerDataStoryGate(__instance) ||
                    (!isBallowPostGate && !isEscapeGate &&
                     !state.IsRandomized(ItemType.Skill)))
                {
                    return;
                }

                if (isBallowPostGate ||
                    IsExactForgeDaughterPostGate(__instance))
                {
                    TryEnableDivingBellForAcceptedQuest(
                        state,
                        playerData
                    );
                }

                __state = new SilkSoarSnapshot
                {
                    PlayerData = playerData,
                    NativeSuperJump = playerData.hasSuperJump,
                };
                playerData.hasSuperJump = isEscapeGate
                    ? HasCollectedAbyssShrine(state, playerData)
                    : isBallowPostGate
                        ? playerData.BallowMovedToDivingBell
                        : state.canSilkSoar;
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

        [HarmonyPatch(
            typeof(SetPlayerDataBoolAction),
            nameof(SetPlayerDataBoolAction.OnEnter)
        )]
        private static class SilkSoarSourceWritePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(SetPlayerDataBoolAction __instance)
            {
                if (!IsExactSilkSoarSourceWrite(__instance))
                {
                    return true;
                }

                SaveState state = SaveState.Instance;
                if (!IsSilkSoarRandomized(state))
                {
                    return true;
                }

                bool isSuperJumpWrite = string.Equals(
                    __instance.boolName.Value,
                    NativeFlag,
                    StringComparison.Ordinal
                );
                if (isSuperJumpWrite &&
                    IsRandomizedSilkSoarSource(state))
                {
                    state.CheckLocation(SilkSoarSourceLocationName);
                }

                PlayerData playerData = PlayerData.instance;
                if (state.canSilkSoar &&
                    (isSuperJumpWrite ||
                     (playerData != null && playerData.hasSuperJump)))
                {
                    return true;
                }

                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(
            typeof(SendEventByNameAction),
            nameof(SendEventByNameAction.OnEnter)
        )]
        private static class SilkSoarEscapeStartPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(SendEventByNameAction __instance)
            {
                if (!IsExactSilkSoarEscapeStart(__instance))
                {
                    return true;
                }

                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (!IsSilkSoarRandomized(state) ||
                    (state.canSilkSoar &&
                     playerData != null &&
                     playerData.hasSuperJump))
                {
                    return true;
                }

                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(TestGameObjectActivator), "Evaluate")]
        private static class DivingBellStatePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                TestGameObjectActivator __instance,
                out SilkSoarSnapshot __state)
            {
                __state = null;

                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                bool isWhiteFlowerSelector =
                    IsExactDivingBellWhiteFlowerActivator(__instance) ||
                    IsExactAbyssDivingBellWhiteFlowerActivator(
                        __instance
                    );
                if (state == null ||
                    playerData == null ||
                    (!IsExactDivingBellStateActivator(__instance) &&
                     !IsExactAbyssDivingBellStateActivator(__instance) &&
                     !isWhiteFlowerSelector))
                {
                    return;
                }

                bool synchronizeBallow =
                    TryEnableDivingBellForAcceptedQuest(
                        state,
                        playerData
                    );
                __state = new SilkSoarSnapshot
                {
                    PlayerData = playerData,
                    NativeSuperJump = playerData.hasSuperJump,
                    SynchronizeBallow = synchronizeBallow,
                };
                if (isWhiteFlowerSelector)
                {
                    PlayerDataTest test =
                        ActivatorTestField.GetValue(__instance) as
                            PlayerDataTest;
                    PlayerDataTest.Test[] tests =
                        test.TestGroups[0].Tests;
                    PlayerDataTest.Test original = tests[0];
                    PlayerDataTest.Test redirected = original;
                    redirected.FieldName = BallowMovedFlag;
                    tests[0] = redirected;
                    __state.SelectorTests = tests;
                    __state.NativeSelectorTest = original;
                    __state.RestoreSelectorTest = true;
                }
                else
                {
                    playerData.hasSuperJump =
                        playerData.BallowMovedToDivingBell;
                }
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
                    if (__state.RestoreSelectorTest &&
                        __state.SelectorTests != null)
                    {
                        __state.SelectorTests[0] =
                            __state.NativeSelectorTest;
                    }
                    if (__state.SynchronizeBallow)
                    {
                        SynchronizeRecoveredBallowObjects();
                    }
                }

                return __exception;
            }
        }

        [HarmonyPatch(
            typeof(GetPlayerDataVariableAction),
            nameof(GetPlayerDataVariableAction.OnEnter)
        )]
        private static class DivingBellDestinationPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                GetPlayerDataVariableAction __instance,
                out SilkSoarSnapshot __state)
            {
                __state = null;

                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null ||
                    playerData == null ||
                    !IsExactDivingBellVariableRead(__instance))
                {
                    return;
                }

                TryEnableDivingBellForAcceptedQuest(state, playerData);
                __state = new SilkSoarSnapshot
                {
                    PlayerData = playerData,
                    NativeSuperJump = playerData.hasSuperJump,
                };
                playerData.hasSuperJump =
                    playerData.BallowMovedToDivingBell;
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

        [HarmonyPatch(
            typeof(GetPlayerDataBoolAction),
            nameof(GetPlayerDataBoolAction.OnEnter)
        )]
        private static class DivingBellDialogueBoolPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                GetPlayerDataBoolAction __instance,
                out SilkSoarSnapshot __state)
            {
                __state = null;

                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                bool isSuperJumpRead =
                    IsExactDivingBellFirstPostRead(
                        __instance,
                        NativeFlag,
                        3
                    );
                bool isWhiteFlowerRead =
                    IsExactDivingBellFirstPostRead(
                        __instance,
                        WhiteFlowerFlag,
                        4
                    );
                if (state == null ||
                    playerData == null ||
                    (!isSuperJumpRead && !isWhiteFlowerRead))
                {
                    return;
                }

                TryEnableDivingBellForAcceptedQuest(state, playerData);
                __state = new SilkSoarSnapshot
                {
                    PlayerData = playerData,
                    NativeSuperJump = playerData.hasSuperJump,
                };
                if (isWhiteFlowerRead)
                {
                    __state.DialogueBoolAction = __instance;
                    __state.NativeDialogueBoolName =
                        __instance.boolName.Value;
                    __instance.boolName.Value = BallowMovedFlag;
                }
                else
                {
                    playerData.hasSuperJump =
                        playerData.BallowMovedToDivingBell;
                }
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
                    if (__state.DialogueBoolAction != null)
                    {
                        __state.DialogueBoolAction.boolName.Value =
                            __state.NativeDialogueBoolName;
                    }
                }

                return __exception;
            }
        }

        [HarmonyPatch(
            typeof(DeactivateIfPlayerdataFalse),
            nameof(DeactivateIfPlayerdataFalse.ForceEvaluate)
        )]
        private static class DivingBellShieldPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                DeactivateIfPlayerdataFalse __instance,
                out SilkSoarSnapshot __state)
            {
                __state = null;

                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null ||
                    playerData == null ||
                    !IsExactDivingBellShield(__instance))
                {
                    return;
                }

                __state = new SilkSoarSnapshot
                {
                    PlayerData = playerData,
                    NativeSuperJump = playerData.hasSuperJump,
                    SynchronizeBallow =
                        TryEnableDivingBellForAcceptedQuest(
                            state,
                            playerData
                        ),
                };
                playerData.hasSuperJump =
                    playerData.BallowMovedToDivingBell;
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
                    if (__state.SynchronizeBallow)
                    {
                        SynchronizeRecoveredBallowObjects();
                    }
                }

                return __exception;
            }
        }

        [HarmonyPatch(
            typeof(GameManager),
            nameof(GameManager.BeginSceneTransition)
        )]
        private static class DivingBellTransitionPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(GameManager.SceneLoadInfo info)
            {
                if (info == null ||
                    !string.Equals(
                        info.SceneName,
                        BellBrokenInteriorSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                TryEnableDivingBellForAcceptedQuest(state, playerData);
                if (IsDivingBellReady(state, playerData))
                {
                    info.SceneName = BellFixedInteriorSceneName;
                    if (string.Equals(
                            info.EntryGateName,
                            BellBrokenSurfaceEntryGateName,
                            StringComparison.Ordinal))
                    {
                        info.EntryGateName =
                            BellFixedSurfaceEntryGateName;
                    }
                }
            }
        }

        [HarmonyPatch(
            typeof(GameManager),
            nameof(GameManager.FinishedEnteringScene)
        )]
        private static class DivingBellAbyssQuestPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                GameManager gameManager = GameManager.SilentInstance;
                HeroController hero = HeroController.instance;
                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (gameManager == null ||
                    hero == null ||
                    !string.Equals(
                        gameManager.GetSceneNameString(),
                        AbyssDivingBellSceneName,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        hero.GetEntryGateName(),
                        AbyssDivingBellEntryGateName,
                        StringComparison.Ordinal))
                {
                    return;
                }

                TryEnableDivingBellForAcceptedQuest(state, playerData);
                if (IsDivingBellReady(state, playerData))
                {
                    TryBeginDivingBellAbyssQuest();
                }
            }
        }

        [HarmonyPatch(
            typeof(FullQuestBase),
            nameof(FullQuestBase.BeginQuest)
        )]
        private static class DivingBellQuestAcceptedPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(FullQuestBase __instance)
            {
                if (__instance == null ||
                    !IsDivingBellIntroductionQuest(__instance.name))
                {
                    return;
                }

                if (TryEnableDivingBellForAcceptedQuest(
                        SaveState.Instance,
                        PlayerData.instance))
                {
                    SynchronizeRecoveredBallowObjects();
                }
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

        private static bool IsSilkSoarRandomized(SaveState state)
        {
            return state != null &&
                   state.IsRandomized(ItemType.Skill);
        }

        private static bool IsRandomizedSilkSoarSource(
            SaveState state)
        {
            return IsSilkSoarRandomized(state) &&
                   state.IsLocationEnabled(SilkSoarSourceLocationName) &&
                   state.IsLocationInSeed(SilkSoarSourceLocationName);
        }

        private static bool IsExactSilkSoarSourceWrite(
            SetPlayerDataBoolAction action)
        {
            if (!IsExactSilkSoarSourceAction(
                    action,
                    SilkSoarSourceEndStateName) ||
                action.boolName == null ||
                action.value == null ||
                !action.value.Value)
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            SetPlayerDataBoolAction superJump =
                actions != null && actions.Length == 10
                    ? actions[2] as SetPlayerDataBoolAction
                    : null;
            SetPlayerDataBoolAction silkSpecial =
                actions != null && actions.Length == 10
                    ? actions[4] as SetPlayerDataBoolAction
                    : null;
            if (actions == null ||
                actions.Length != 10 ||
                !(actions[0] is SendEventByNameAction) ||
                superJump?.boolName == null ||
                superJump.value == null ||
                !string.Equals(
                    superJump.boolName.Value,
                    NativeFlag,
                    StringComparison.Ordinal) ||
                !superJump.value.Value ||
                silkSpecial?.boolName == null ||
                silkSpecial.value == null ||
                !string.Equals(
                    silkSpecial.boolName.Value,
                    "hasSilkSpecial",
                    StringComparison.Ordinal) ||
                !silkSpecial.value.Value)
            {
                return false;
            }

            return ReferenceEquals(action, superJump) ||
                   ReferenceEquals(action, silkSpecial);
        }

        private static bool IsExactSilkSoarEscapeStart(
            SendEventByNameAction action)
        {
            FsmEventTarget target = action?.eventTarget;
            FsmOwnerDefault targetObject = target?.gameObject;
            FsmGameObject parent = targetObject?.GameObject;
            if (!IsExactSilkSoarSourceAction(
                    action,
                    SilkSoarSourceFinishedStateName) ||
                action.sendEvent == null ||
                action.delay == null ||
                target == null ||
                target.target != FsmEventTarget.EventTarget.GameObject ||
                targetObject == null ||
                targetObject.OwnerOption !=
                    OwnerDefaultOption.SpecifyGameObject ||
                parent == null ||
                !parent.UseVariable ||
                !string.Equals(
                    parent.Name,
                    "Parent",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.sendEvent.Value,
                    ShrineSequenceEndEventName,
                    StringComparison.Ordinal) ||
                action.delay.Value != 0f ||
                action.everyFrame)
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            return actions != null &&
                   actions.Length == 5 &&
                   ReferenceEquals(actions[2], action) &&
                   actions[0] is SetPlayerDataBoolAction &&
                   string.Equals(
                       actions[3]?.GetType().FullName,
                       "SaveGameV2",
                       StringComparison.Ordinal);
        }

        private static bool IsExactSilkSoarSourceAction(
            FsmStateAction action,
            string stateName)
        {
            return action != null &&
                   action.Enabled &&
                   action.Owner != null &&
                   action.Fsm != null &&
                   action.State != null &&
                   HasExactPath(
                       action.Owner,
                       SilkSoarSourceSceneName,
                       SilkSoarSourcePath) &&
                   string.Equals(
                       action.Fsm.Name,
                       SilkSoarSourceFsmName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       action.State.Name,
                       stateName,
                       StringComparison.Ordinal);
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
                    EscapeSceneName,
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

        internal static bool SynchronizeActiveScene()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            GameManager gameManager = GameManager.SilentInstance;
            if (state == null ||
                playerData == null ||
                gameManager == null ||
                !state.IsRandomized(ItemType.Skill) ||
                !state.canSilkSoar)
            {
                return false;
            }

            string sceneName = GameManager.GetBaseSceneName(
                gameManager.sceneName ?? string.Empty
            );
            if (string.Equals(
                    sceneName,
                    DockSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                foreach (TestGameObjectActivator activator in
                         Resources.FindObjectsOfTypeAll<
                             TestGameObjectActivator>())
                {
                    if (!IsExactDivingBellStateActivator(activator))
                    {
                        continue;
                    }

                    activator.DoEvaluate();
                    return true;
                }
            }
            else if (IsDivingBellInteriorScene(sceneName))
            {
                foreach (DeactivateIfPlayerdataFalse source in
                         Resources.FindObjectsOfTypeAll<
                             DeactivateIfPlayerdataFalse>())
                {
                    if (!IsExactDivingBellShield(source))
                    {
                        continue;
                    }

                    source.gameObject.SetActive(true);
                    source.ForceEvaluate();
                    return source.gameObject.activeSelf;
                }
            }

            return false;
        }

        private static bool IsExactPlayerDataStoryGate(
            PlayerDataVariableTestAction action)
        {
            return IsExactEscapeQuestGate(action) ||
                   IsExactBallowPostGate(action) ||
                   IsExactForgeDaughterPostGate(action);
        }

        private static bool IsExactBallowPostGate(
            PlayerDataVariableTestAction action)
        {
            if (!IsExactExpectedTrueAction(
                    action,
                    DockSceneName,
                    DialogueFsmName,
                    BallowPostStateName,
                    NativeFlag,
                    "TRUE",
                    "FALSE") ||
                (!HasExactPath(
                     action.Owner,
                     DockSceneName,
                     BallowStandardPath) &&
                 !HasExactPath(
                     action.Owner,
                     DockSceneName,
                     BallowUpgradedPath)))
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            return actions != null &&
                   actions.Length == 1 &&
                   ReferenceEquals(actions[0], action);
        }

        private static bool IsExactForgeDaughterPostGate(
            PlayerDataVariableTestAction action)
        {
            if (!IsExactExpectedTrueAction(
                    action,
                    ForgeSceneName,
                    DialogueFsmName,
                    ForgePostStateName,
                    NativeFlag,
                    "TRUE",
                    "FALSE") ||
                !HasExactPath(
                    action.Owner,
                    ForgeSceneName,
                    ForgeDaughterPath))
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            PlayerDataVariableTestAction whiteFlower =
                actions != null && actions.Length == 3
                    ? actions[0] as PlayerDataVariableTestAction
                    : null;
            PlayerDataVariableTestAction postDialogue =
                actions != null && actions.Length == 3
                    ? actions[1] as PlayerDataVariableTestAction
                    : null;
            return actions != null &&
                   actions.Length == 3 &&
                   ReferenceEquals(actions[2], action) &&
                   string.Equals(
                       whiteFlower?.VariableName?.Value,
                       "HasWhiteFlower",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       postDialogue?.VariableName?.Value,
                       "ForgeDaughterPostAbyssDlg",
                       StringComparison.Ordinal);
        }

        private static bool IsExactExpectedTrueAction(
            PlayerDataVariableTestAction action,
            string sceneName,
            string fsmName,
            string stateName,
            string variableName,
            string expectedEvent,
            string notExpectedEvent)
        {
            return action != null &&
                   action.Enabled &&
                   action.Owner != null &&
                   action.Fsm != null &&
                   action.State != null &&
                   string.Equals(
                       action.Owner.scene.name,
                       sceneName,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       action.Fsm.Name,
                       fsmName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       action.State.Name,
                       stateName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       action.VariableName?.Value,
                       variableName,
                       StringComparison.Ordinal) &&
                   action.ExpectedValue != null &&
                   action.ExpectedValue.Type == VariableType.Bool &&
                   !action.ExpectedValue.useVariable &&
                   action.ExpectedValue.boolValue &&
                   IsNamedEvent(
                       action.IsExpectedEvent,
                       expectedEvent) &&
                   IsNamedEvent(
                       action.IsNotExpectedEvent,
                       notExpectedEvent);
        }

        private static bool IsExactDivingBellStateActivator(
            TestGameObjectActivator activator)
        {
            return IsExactSingleBoolActivator(
                activator,
                DockSceneName,
                DivingBellStatesPath,
                NativeFlag,
                DivingBellRepairPath,
                DivingBellStandardPath
            );
        }

        private static bool IsExactDivingBellWhiteFlowerActivator(
            TestGameObjectActivator activator)
        {
            return IsExactSingleBoolActivator(
                activator,
                DockSceneName,
                DivingBellRepairPath,
                WhiteFlowerFlag,
                DivingBellUpgradedPath,
                DivingBellHalfUpgradedPath
            );
        }

        private static bool IsExactAbyssDivingBellStateActivator(
            TestGameObjectActivator activator)
        {
            return IsExactSingleBoolActivator(
                activator,
                AbyssDivingBellSceneName,
                AbyssDivingBellStatesPath,
                NativeFlag,
                AbyssDivingBellGonePath,
                AbyssDivingBellBrokenPath
            );
        }

        private static bool IsExactAbyssDivingBellWhiteFlowerActivator(
            TestGameObjectActivator activator)
        {
            return IsExactSingleBoolActivator(
                activator,
                AbyssDivingBellSceneName,
                AbyssDivingBellGonePath,
                WhiteFlowerFlag,
                AbyssDivingBellUpgradedPath,
                AbyssDivingBellGoneZonePath
            );
        }

        private static bool IsExactBallowTrapdoorActivator(
            TestGameObjectActivator activator)
        {
            return IsExactSingleBoolActivator(
                activator,
                DockSceneName,
                DivingBellTrapdoorPath,
                BallowMovedFlag,
                DivingBellTrapdoorOpenPath,
                DivingBellTrapdoorClosedPath
            );
        }

        private static bool IsExactSingleBoolActivator(
            TestGameObjectActivator activator,
            string sceneName,
            string activatorPath,
            string fieldName,
            string activatePath,
            string deactivatePath)
        {
            if (activator == null ||
                ActivatorTestField == null ||
                ActivatorTargetField == null ||
                DeactivatorTargetField == null ||
                !HasExactPath(
                    activator.gameObject,
                    sceneName,
                    activatorPath) ||
                !HasExactPath(
                    ActivatorTargetField.GetValue(activator) as
                        GameObject,
                    sceneName,
                    activatePath) ||
                !HasExactPath(
                    DeactivatorTargetField.GetValue(activator) as
                        GameObject,
                    sceneName,
                    deactivatePath))
            {
                return false;
            }

            PlayerDataTest test = ActivatorTestField.GetValue(activator)
                as PlayerDataTest;
            if (test?.TestGroups == null ||
                test.TestGroups.Length != 1 ||
                test.TestGroups[0].Tests == null ||
                test.TestGroups[0].Tests.Length != 1)
            {
                return false;
            }

            PlayerDataTest.Test condition =
                test.TestGroups[0].Tests[0];
            return condition.Type == PlayerDataTest.TestType.Bool &&
                   string.Equals(
                       condition.FieldName,
                       fieldName,
                       StringComparison.Ordinal) &&
                   condition.BoolValue;
        }

        private static bool IsExactDivingBellVariableRead(
            GetPlayerDataVariableAction action)
        {
            return IsExactDivingBellDestinationRead(action) ||
                   IsExactDivingBellDialogueRead(action);
        }

        private static bool IsExactDivingBellDestinationRead(
            GetPlayerDataVariableAction action)
        {
            if (action == null ||
                !action.Enabled ||
                action.Owner == null ||
                action.Fsm == null ||
                action.State == null ||
                !HasExactDivingBellInteriorPath(
                    action.Owner,
                    BellSequencePath) ||
                !string.Equals(
                    action.Fsm.Name,
                    BellSequenceFsmName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.State.Name,
                    BellGoingDownStateName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.VariableName?.Value,
                    NativeFlag,
                    StringComparison.Ordinal))
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            ConvertBoolToStringAction destination =
                actions != null && actions.Length == 3
                    ? actions[1] as ConvertBoolToStringAction
                    : null;
            return actions != null &&
                   actions.Length == 3 &&
                   ReferenceEquals(actions[0], action) &&
                   destination != null &&
                   !destination.everyFrame &&
                   string.Equals(
                       destination.falseString?.Value,
                       "Room_Diving_Bell_Abyss",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       destination.trueString?.Value,
                       "Room_Diving_Bell_Abyss_Fixed",
                       StringComparison.Ordinal) &&
                   actions[2] is ConvertBoolToStringAction;
        }

        private static bool IsExactDivingBellDialogueRead(
            GetPlayerDataVariableAction action)
        {
            if (action == null ||
                !action.Enabled ||
                action.Owner == null ||
                action.Fsm == null ||
                action.State == null ||
                !HasExactDivingBellInteriorPath(
                    action.Owner,
                    BellDialoguePath) ||
                !string.Equals(
                    action.Fsm.Name,
                    DialogueFsmName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.VariableName?.Value,
                    NativeFlag,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string falseString;
            string trueString;
            if (string.Equals(
                    action.State.Name,
                    BellTravelDialogueStateName,
                    StringComparison.Ordinal))
            {
                falseString = "BALLOW_DIVING_BELL_TO_TRAVEL";
                trueString = "BALLOW_DIVING_BELL_TO_TRAVEL_DOWN";
            }
            else if (string.Equals(
                         action.State.Name,
                         BellBenchDialogueStateName,
                         StringComparison.Ordinal))
            {
                falseString = "BALLOW_DIVING_BELL_TO_BENCH";
                trueString =
                    "BALLOW_DIVING_BELL_TO_BENCH_REPAIRED";
            }
            else
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            ConvertBoolToStringAction dialogueKey =
                actions != null && actions.Length == 3
                    ? actions[1] as ConvertBoolToStringAction
                    : null;
            return actions != null &&
                   actions.Length == 3 &&
                   ReferenceEquals(actions[0], action) &&
                   dialogueKey != null &&
                   !dialogueKey.everyFrame &&
                   string.Equals(
                       dialogueKey.falseString?.Value,
                       falseString,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       dialogueKey.trueString?.Value,
                       trueString,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       actions[2]?.GetType().FullName,
                       "HutongGames.PlayMaker.Actions.RunDialogue",
                       StringComparison.Ordinal);
        }

        private static bool IsExactDivingBellFirstPostRead(
            GetPlayerDataBoolAction action,
            string fieldName,
            int actionIndex)
        {
            if (action == null ||
                !action.Enabled ||
                action.Owner == null ||
                action.Fsm == null ||
                action.State == null ||
                !HasExactDivingBellInteriorPath(
                    action.Owner,
                    BellDialoguePath) ||
                !string.Equals(
                    action.Fsm.Name,
                    DialogueFsmName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.State.Name,
                    BellFirstPostStateName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    action.boolName?.Value,
                    fieldName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            return actions != null &&
                   actions.Length == 9 &&
                   actionIndex >= 0 &&
                   actionIndex < actions.Length &&
                   ReferenceEquals(actions[actionIndex], action) &&
                   actions[2] is GetPlayerDataBoolAction &&
                   actions[3] is GetPlayerDataBoolAction &&
                   actions[4] is GetPlayerDataBoolAction &&
                   string.Equals(
                       actions[5]?.GetType().FullName,
                       "HutongGames.PlayMaker.Actions.BoolTestMulti",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       actions[6]?.GetType().FullName,
                       "HutongGames.PlayMaker.Actions.BoolTestMulti",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       actions[7]?.GetType().FullName,
                       "HutongGames.PlayMaker.Actions.BoolTest",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       actions[8]?.GetType().FullName,
                       "HutongGames.PlayMaker.Actions.PlayerDataBoolTest",
                       StringComparison.Ordinal);
        }

        private static bool IsExactDivingBellShield(
            DeactivateIfPlayerdataFalse source)
        {
            return source != null &&
                   source.objectToDeactivate == null &&
                   string.Equals(
                       source.boolName,
                       NativeFlag,
                       StringComparison.Ordinal) &&
                   HasExactDivingBellInteriorPath(
                       source.gameObject,
                       BellShieldedPath);
        }

        private static bool IsDivingBellIntroductionQuest(string questName)
        {
            return string.Equals(questName, DivingBellInspectQuestName, StringComparison.Ordinal) ||
                   string.Equals(questName, DivingBellBallowQuestName, StringComparison.Ordinal);
        }

        private static bool HasCollectedAbyssShrine(SaveState state, PlayerData playerData)
        {
            return state != null && playerData != null &&
                   (state.IsRandomized(ItemType.Skill)
                       ? state.IsLocationChecked(SilkSoarSourceLocationName)
                       : playerData.hasSuperJump);
        }

        private static bool TryEnableDivingBellForAcceptedQuest(
            SaveState state,
            PlayerData playerData)
        {
            if (state == null ||
                playerData == null)
            {
                return false;
            }

            FullQuestBase inspectQuest = QuestManager.GetQuest(
                DivingBellInspectQuestName
            );
            if (inspectQuest == null ||
                (!inspectQuest.IsAccepted &&
                 !inspectQuest.IsCompleted))
            {
                return false;
            }

            try
            {
                FullQuestBase descendQuest = QuestManager.GetQuest(
                    DivingBellDescendQuestName
                );
                if (descendQuest == null)
                {
                    return false;
                }

                bool changed =
                    !descendQuest.IsAccepted &&
                    !descendQuest.IsCompleted;
                if (changed)
                {
                    descendQuest.BeginQuest(null, false);
                }
                if (!descendQuest.IsAccepted &&
                    !descendQuest.IsCompleted)
                {
                    return false;
                }

                changed =
                    changed ||
                    !playerData.ForgeDaughterMentionedDivingBell ||
                    !playerData.BallowMovedToDivingBell;
                playerData.ForgeDaughterMentionedDivingBell = true;
                playerData.BallowMovedToDivingBell = true;
                if (changed)
                {
                    RandomizerPlugin.Log?.LogInfo(
                        "[RANDOMIZER] Enabled the repaired Diving Bell " +
                        "for SEEK: The Dark Below."
                    );
                }
                return changed;
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Diving Bell quest activation failed " +
                    "closed: " + ex.Message
                );
                return false;
            }
        }

        private static bool IsDivingBellReady(
            SaveState state,
            PlayerData playerData)
        {
            if (state == null ||
                playerData == null ||
                !playerData.BallowMovedToDivingBell)
            {
                return false;
            }

            FullQuestBase quest = QuestManager.GetQuest(
                DivingBellInspectQuestName
            );
            return quest != null &&
                   (quest.IsAccepted || quest.IsCompleted);
        }

        private static void TryBeginDivingBellAbyssQuest()
        {
            FullQuestBase quest = QuestManager.GetQuest(
                DivingBellAbyssQuestName
            );
            if (quest == null ||
                quest.IsAccepted ||
                quest.IsCompleted)
            {
                return;
            }

            try
            {
                quest.BeginQuest(null, true);
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Diving Bell Abyss quest start failed " +
                    "closed: " + ex.Message
                );
            }
        }

        private static void SynchronizeRecoveredBallowObjects()
        {
            try
            {
                foreach (TestGameObjectActivator activator in
                         Resources.FindObjectsOfTypeAll<
                             TestGameObjectActivator>())
                {
                    if (IsExactBallowTrapdoorActivator(activator) ||
                        IsExactDivingBellStateActivator(activator) ||
                        IsExactDivingBellWhiteFlowerActivator(
                            activator
                        ) ||
                        IsExactAbyssDivingBellStateActivator(
                            activator
                        ) ||
                        IsExactAbyssDivingBellWhiteFlowerActivator(
                            activator
                        ))
                    {
                        activator.DoEvaluate();
                    }
                }

                foreach (DeactivateIfPlayerdataFalse source in
                         Resources.FindObjectsOfTypeAll<
                             DeactivateIfPlayerdataFalse>())
                {
                    if (!IsExactBallowMovedSource(source))
                    {
                        continue;
                    }

                    source.gameObject.SetActive(true);
                    source.ForceEvaluate();
                }

                foreach (DeactivateIfPlayerdataTrue source in
                         Resources.FindObjectsOfTypeAll<
                             DeactivateIfPlayerdataTrue>())
                {
                    if (IsExactBallowMovedControl(source))
                    {
                        source.gameObject.SetActive(false);
                    }
                }
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Diving Bell scene synchronization " +
                    "failed closed: " + ex.Message
                );
            }
        }

        private static bool IsExactBallowMovedSource(
            DeactivateIfPlayerdataFalse source)
        {
            if (source == null ||
                source.objectToDeactivate != null ||
                !string.Equals(
                    source.boolName,
                    BallowMovedFlag,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string path = Utils.GetHierarchyPath(source.transform);
            return string.Equals(
                       source.gameObject.scene.name,
                       DockSceneName,
                       StringComparison.OrdinalIgnoreCase) &&
                   (string.Equals(
                        path,
                        BallowStandardPath,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        path,
                        BallowUpgradedPath,
                        StringComparison.Ordinal));
        }

        private static bool IsExactBallowMovedControl(
            DeactivateIfPlayerdataTrue source)
        {
            return source != null &&
                   source.objectToDeactivate == null &&
                   string.Equals(
                       source.boolName,
                       BallowMovedFlag,
                       StringComparison.Ordinal) &&
                   HasExactPath(
                       source.gameObject,
                       DockSceneName,
                       DivingBellControlsPath);
        }

        private static bool HasExactDivingBellInteriorPath(
            GameObject gameObject,
            string path)
        {
            return gameObject != null &&
                   IsDivingBellInteriorScene(
                       gameObject.scene.name) &&
                   string.Equals(
                       Utils.GetHierarchyPath(gameObject.transform),
                       path,
                       StringComparison.Ordinal);
        }

        private static bool IsDivingBellInteriorScene(string sceneName)
        {
            return string.Equals(
                       sceneName,
                       BellInteriorSceneName,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       sceneName,
                       BellFixedInteriorSceneName,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasExactPath(
            GameObject gameObject,
            string sceneName,
            string path)
        {
            return gameObject != null &&
                   string.Equals(
                       gameObject.scene.name,
                       sceneName,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       Utils.GetHierarchyPath(gameObject.transform),
                       path,
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
