using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using PlayerDataBoolTestAction =
    HutongGames.PlayMaker.Actions.PlayerDataBoolTest;
using PlayerDataVariableTestAction =
    HutongGames.PlayMaker.Actions.PlayerDataVariableTest;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Lets all three overworld Wardenflies use randomized Cling Grip
    /// ownership without granting the vanilla wall-jump source flag.
    /// Every unrelated appearance and retirement gate remains native.
    /// </summary>
    internal static class FarFieldsWardenflyPatches
    {
        private const string BoneEastScene = "Bone_East_04c";
        private const string GreymoorScene = "Greymoor_05";
        private const string ShadowScene = "Shadow_21";
        private const string StandardCagePath =
            "Scene Control/Slab Jailer Scene/Slab Fly Large Cage";
        private const string GreymoorCagePath =
            "Scene Control/Slab Jailer Enemy/Slab Fly Large Cage";
        private const string CageFsm = "Control";
        private const string CageStartState = "Init";
        private const string CageGateState = "Is Here?";
        private const string CageAbsentState = "Not Here";
        private const string WallJumpBool = "hasWalljump";
        private const string BlackThreadBool = "blackThreadWorld";

        private const string BoneSceneControlPath = "Scene Control";
        private const string BoneJailerScenePath =
            "Scene Control/Slab Jailer Scene";
        private const string BoneHuntersScenePath =
            "Scene Control/Bone Hunters Scene";
        private const string GreymoorJailerEnemyPath =
            "Scene Control/Slab Jailer Enemy";

        private const string GreymoorSceneControlPath = "Scene Control";
        private const string GreymoorSceneControlFsm = "Scene Control";
        private const string GreymoorEnemySuiteState = "Enemy Suite";
        private const string GreymoorTerminalState = "End";
        private const string FarmersEvent = "FARMERS";

        private static readonly HashSet<string> WardenflyScenes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                BoneEastScene,
                GreymoorScene,
                ShadowScene,
            };

        private static readonly System.Reflection.FieldInfo
            BoneActivatorTestField =
            AccessTools.Field(
                typeof(TestGameObjectActivator),
                "playerDataTest"
            );
        private static readonly System.Reflection.FieldInfo
            BoneActivateTargetField =
            AccessTools.Field(
                typeof(TestGameObjectActivator),
                "activateGameObject"
            );
        private static readonly System.Reflection.FieldInfo
            BoneDeactivateTargetField =
            AccessTools.Field(
                typeof(TestGameObjectActivator),
                "deactivateGameObject"
            );
        private static readonly System.Reflection.FieldInfo
            BoneActivateEventField = AccessTools.Field(
                typeof(TestGameObjectActivator),
                "activateEventRegister"
            );
        private static readonly System.Reflection.FieldInfo
            BoneDeactivateEventField = AccessTools.Field(
                typeof(TestGameObjectActivator),
                "deactivateEventRegister"
            );
        private static readonly System.Reflection.FieldInfo
            BoneQuestTestsField = AccessTools.Field(
                typeof(TestGameObjectActivator),
                "questTests"
            );
        private static readonly System.Reflection.FieldInfo
            BoneEquipTestsField = AccessTools.Field(
                typeof(TestGameObjectActivator),
                "equipTests"
            );
        private static readonly System.Reflection.FieldInfo
            BoneEntryWhitelistField = AccessTools.Field(
                typeof(TestGameObjectActivator),
                "entryGateWhitelist"
            );
        private static readonly System.Reflection.FieldInfo
            BoneEntryBlacklistField = AccessTools.Field(
                typeof(TestGameObjectActivator),
                "entryGateBlacklist"
            );
        private static readonly System.Reflection.FieldInfo
            BoneCheckActiveField = AccessTools.Field(
                typeof(TestGameObjectActivator),
                "checkActive"
            );
        private static readonly System.Reflection.FieldInfo
            BoneExpectedActiveField = AccessTools.Field(
                typeof(TestGameObjectActivator),
                "expectedActive"
            );

        [HarmonyPatch(
            typeof(PlayerDataVariableTestAction),
            nameof(PlayerDataVariableTestAction.OnEnter)
        )]
        private static class RandomizedClingGripInnerGatePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                PlayerDataVariableTestAction __instance)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.Skill) ||
                    !IsExactCageWallJumpGate(__instance))
                {
                    return true;
                }

            // The first action expects hasWalljump == false.
                // FALSE therefore means the cage remains and proceeds into
                // Dormant. The following blackThreadWorld action is left
                // untouched.
                __instance.Fsm.Event(
                    state.canWallJump
                        ? __instance.IsNotExpectedEvent
                        : __instance.IsExpectedEvent
                );
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(
            typeof(PlayerDataBoolTestAction),
            nameof(PlayerDataBoolTestAction.OnEnter)
        )]
        private static class RandomizedClingGripGreymoorOuterGatePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                PlayerDataBoolTestAction __instance)
            {
                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null ||
                    playerData == null ||
                    !state.IsRandomized(ItemType.Skill) ||
                    !IsExactGreymoorOuterWallJumpGate(__instance))
                {
                    return true;
                }

                // Substitute only this exact hasWalljump branch. The
                // UnlockedFastTravel component on the cage root and the six
                // following Enemy Suite conditions remain native.
                bool eligible = state.canWallJump;
                __instance.Fsm.Event(
                    eligible ? __instance.isTrue : __instance.isFalse
                );
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(TestGameObjectActivator), "Evaluate")]
        private static class RandomizedClingGripBoneEastOuterGatePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                TestGameObjectActivator __instance)
            {
                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null ||
                    playerData == null ||
                    !state.IsRandomized(ItemType.Skill) ||
                    !IsExactBoneEastOuterActivator(__instance))
                {
                    return true;
                }

                ApplyBoneEastOuterGate(__instance, state, playerData);
                return false;
            }
        }

        internal static bool SynchronizeActiveScene()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            GameManager gameManager = GameManager.instance;
            if (state == null ||
                playerData == null ||
                gameManager == null ||
                !state.IsRandomized(ItemType.Skill) ||
                !state.canWallJump ||
                !IsNativeReceiptEligible(
                    GameManager.GetBaseSceneName(
                        gameManager.sceneName ?? string.Empty),
                    playerData))
            {
                return false;
            }

            string activeScene = GameManager.GetBaseSceneName(
                gameManager.sceneName ?? string.Empty
            );
            if (!WardenflyScenes.Contains(activeScene))
            {
                return false;
            }

            bool outerReady;
            if (string.Equals(
                    activeScene,
                    BoneEastScene,
                    StringComparison.Ordinal))
            {
                outerReady = SynchronizeBoneEastOuterGate();
            }
            else if (string.Equals(
                         activeScene,
                         GreymoorScene,
                         StringComparison.Ordinal))
            {
                outerReady = SynchronizeGreymoorOuterGate();
            }
            else
            {
                outerReady = HasActiveCageParentForScene(activeScene);
            }

            if (!outerReady)
            {
                return false;
            }

            bool synchronized = false;

            // A cage that already evaluated AP ownership as absent disables
            // itself in Not Here. Re-enable only that exact cage and let the
            // two-part gate run again. The cage-root
            // UnlockedFastTravel component and every retirement condition
            // remain untouched.
            foreach (PlayMakerFSM fsm in
                     Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!IsExactCageControlFsm(fsm, activeScene) ||
                    fsm.Fsm == null ||
                    !IsExactCageParentActive(fsm, activeScene) ||
                    !string.Equals(
                        fsm.Fsm.ActiveStateName,
                        CageAbsentState,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                bool wasActive = fsm.gameObject.activeSelf;
                try
                {
                    fsm.gameObject.SetActive(true);
                    if (!fsm.gameObject.activeInHierarchy ||
                        fsm.Fsm == null ||
                        string.IsNullOrEmpty(fsm.Fsm.ActiveStateName) ||
                        string.Equals(
                            fsm.Fsm.ActiveStateName,
                            CageAbsentState,
                            StringComparison.Ordinal))
                    {
                        if (!wasActive)
                        {
                            fsm.gameObject.SetActive(false);
                        }

                        continue;
                    }

                    synchronized = true;
                    RandomizerPlugin.Log?.LogInfo(
                        "[RANDOMIZER] Re-armed the " + activeScene +
                        " Wardenfly after receiving randomized " +
                        "Cling Grip."
                    );
                }
                catch (Exception ex)
                {
                    if (!wasActive && fsm.gameObject != null)
                    {
                        fsm.gameObject.SetActive(false);
                    }

                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] " + activeScene +
                        " Wardenfly synchronization failed closed: " +
                        ex.Message
                    );
                    return synchronized;
                }
            }

            return synchronized;
        }

        private static bool SynchronizeBoneEastOuterGate()
        {
            foreach (TestGameObjectActivator activator in
                     Resources.FindObjectsOfTypeAll<
                         TestGameObjectActivator>())
            {
                if (!IsExactBoneEastOuterActivator(activator))
                {
                    continue;
                }

                try
                {
                    ApplyBoneEastOuterGate(
                        activator,
                        SaveState.Instance,
                        PlayerData.instance
                    );
                    return GetBoneActivateTarget(activator)?.activeInHierarchy ==
                           true;
                }
                catch (Exception ex)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Bone East Wardenfly outer-gate " +
                        "synchronization failed closed: " + ex.Message
                    );
                    return false;
                }
            }

            return false;
        }

        private static void ApplyBoneEastOuterGate(
            TestGameObjectActivator activator,
            SaveState state,
            PlayerData playerData)
        {
            GameObject activateTarget = GetBoneActivateTarget(activator);
            GameObject deactivateTarget =
                GetBoneDeactivateTarget(activator);
            bool eligible =
                state.canWallJump &&
                !playerData.blackThreadWorld &&
                !playerData.boneEastJailerClearedOut &&
                !playerData.slab_cloak_battle_completed &&
                !playerData.visitedUpperSlab;

            activateTarget.SetActive(eligible);
            deactivateTarget.SetActive(!eligible);
        }

        private static bool SynchronizeGreymoorOuterGate()
        {
            if (HasActiveCageParentForScene(GreymoorScene))
            {
                return true;
            }

            foreach (PlayMakerFSM fsm in
                     Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!IsExactGreymoorSceneControlFsm(fsm) ||
                    fsm.Fsm == null ||
                    !string.Equals(
                        fsm.Fsm.ActiveStateName,
                        GreymoorTerminalState,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    fsm.SetState(GreymoorEnemySuiteState);
                    return HasActiveCageParentForScene(GreymoorScene);
                }
                catch (Exception ex)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Greymoor Wardenfly outer-gate " +
                        "synchronization failed closed: " + ex.Message
                    );
                    return false;
                }
            }

            return false;
        }

        private static bool IsNativeReceiptEligible(
            string sceneName,
            PlayerData playerData)
        {
            if (playerData == null ||
                !playerData.UnlockedFastTravel ||
                playerData.blackThreadWorld ||
                playerData.visitedUpperSlab ||
                playerData.slab_cloak_battle_completed)
            {
                return false;
            }

            if (string.Equals(
                    sceneName,
                    BoneEastScene,
                    StringComparison.Ordinal))
            {
                return !playerData.boneEastJailerKilled &&
                       !playerData.boneEastJailerClearedOut &&
                       !playerData.CurseKilledFlyBoneEast;
            }

            if (string.Equals(
                    sceneName,
                    GreymoorScene,
                    StringComparison.Ordinal))
            {
                return playerData.defeatedVampireGnatBoss &&
                       playerData.citadelWoken &&
                       !playerData.greymoor05_clearedOut &&
                       !playerData.greymoor05_killedJailer &&
                       !playerData.CurseKilledFlyGreymoor;
            }

            return string.Equals(
                       sceneName,
                       ShadowScene,
                       StringComparison.Ordinal) &&
                   !playerData.CurseKilledFlySwamp;
        }

        private static bool HasActiveCageParentForScene(
            string sceneName)
        {
            foreach (PlayMakerFSM fsm in
                     Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (IsExactCageControlFsm(fsm, sceneName) &&
                    IsExactCageParentActive(fsm, sceneName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExactCageParentActive(
            PlayMakerFSM fsm,
            string sceneName)
        {
            Transform parent = fsm?.transform?.parent;
            if (parent == null ||
                !parent.gameObject.activeInHierarchy)
            {
                return false;
            }

            string expectedPath = string.Equals(
                sceneName,
                GreymoorScene,
                StringComparison.Ordinal)
                ? GreymoorJailerEnemyPath
                : BoneJailerScenePath;
            return string.Equals(
                Utils.GetHierarchyPath(parent),
                expectedPath,
                StringComparison.Ordinal);
        }

        private static bool IsExactCageWallJumpGate(
            PlayerDataVariableTestAction action)
        {
            if (action == null ||
                !action.Enabled ||
                action.VariableName == null ||
                !string.Equals(
                    action.VariableName.Value,
                    WallJumpBool,
                    StringComparison.Ordinal) ||
                action.ExpectedValue == null ||
                action.ExpectedValue.Type != VariableType.Bool ||
                action.ExpectedValue.useVariable ||
                action.ExpectedValue.boolValue ||
                !IsNamedLocalEvent(
                    action.IsExpectedEvent,
                    "TRUE") ||
                !IsNamedLocalEvent(
                    action.IsNotExpectedEvent,
                    "FALSE") ||
                !IsExactCageControlAction(action))
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            if (actions == null ||
                actions.Length != 2 ||
                !ReferenceEquals(actions[0], action) ||
                !(actions[1] is PlayerDataVariableTestAction actThreeGate))
            {
                return false;
            }

            return actThreeGate.VariableName != null &&
                   actThreeGate.Enabled &&
                   string.Equals(
                       actThreeGate.VariableName.Value,
                       BlackThreadBool,
                       StringComparison.Ordinal) &&
                   actThreeGate.ExpectedValue != null &&
                   actThreeGate.ExpectedValue.Type == VariableType.Bool &&
                   !actThreeGate.ExpectedValue.useVariable &&
                   actThreeGate.ExpectedValue.boolValue &&
                   IsNamedLocalEvent(
                       actThreeGate.IsExpectedEvent,
                       "TRUE") &&
                   IsNamedLocalEvent(
                       actThreeGate.IsNotExpectedEvent,
                       "FALSE");
        }

        private static bool IsExactGreymoorOuterWallJumpGate(
            PlayerDataBoolTestAction action)
        {
            if (action == null ||
                action.boolName == null ||
                !string.Equals(
                    action.boolName.Value,
                    WallJumpBool,
                    StringComparison.Ordinal) ||
                !IsNoEvent(action.isTrue) ||
                !IsNamedLocalEvent(action.isFalse, FarmersEvent) ||
                !IsExactActionIdentity(
                    action,
                    GreymoorScene,
                    GreymoorSceneControlPath,
                    GreymoorSceneControlFsm,
                    GreymoorEnemySuiteState))
            {
                return false;
            }

            FsmStateAction[] actions = action.State.Actions;
            return actions != null &&
                   actions.Length == 7 &&
                   ReferenceEquals(actions[0], action) &&
                   IsExactBoolAction(
                       actions[1],
                       BlackThreadBool,
                       FarmersEvent,
                       null,
                       true) &&
                   IsExactBoolAction(
                       actions[2],
                       "citadelWoken",
                       null,
                       FarmersEvent,
                       true) &&
                   IsExactBoolAction(
                       actions[3],
                       "greymoor05_killedJailer",
                       FarmersEvent,
                       null,
                       true) &&
                   IsExactBoolAction(
                       actions[4],
                       "previouslyVisitedGreymoor_05",
                       null,
                       FarmersEvent,
                       false) &&
                   IsExactBoolAction(
                       actions[5],
                       "visitedUpperSlab",
                       FarmersEvent,
                       null,
                       true) &&
                   IsExactBoolAction(
                       actions[6],
                       "slab_cloak_battle_completed",
                       FarmersEvent,
                       "JAILER",
                       true);
        }

        private static bool IsExactBoolAction(
            FsmStateAction action,
            string fieldName,
            string trueEvent,
            string falseEvent,
            bool enabled)
        {
            if (!(action is PlayerDataBoolTestAction test) ||
                test.Enabled != enabled ||
                test.boolName == null ||
                !string.Equals(
                    test.boolName.Value,
                    fieldName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return IsExpectedEvent(test.isTrue, trueEvent) &&
                   IsExpectedEvent(test.isFalse, falseEvent);
        }

        private static bool IsExpectedEvent(
            FsmEvent fsmEvent,
            string expectedName)
        {
            return expectedName == null
                ? IsNoEvent(fsmEvent)
                : IsNamedLocalEvent(fsmEvent, expectedName);
        }

        private static bool IsNoEvent(FsmEvent fsmEvent)
        {
            return fsmEvent == null ||
                   string.IsNullOrEmpty(fsmEvent.Name);
        }

        private static bool IsNamedLocalEvent(
            FsmEvent fsmEvent,
            string expectedName)
        {
            return fsmEvent != null &&
                   !fsmEvent.IsSystemEvent &&
                   !fsmEvent.IsGlobal &&
                   string.Equals(
                       fsmEvent.Name,
                       expectedName,
                       StringComparison.Ordinal);
        }

        private static bool IsExactBoneEastOuterActivator(
            TestGameObjectActivator activator)
        {
            if (activator == null ||
                activator.gameObject == null ||
                !string.Equals(
                    activator.gameObject.scene.name,
                    BoneEastScene,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    Utils.GetHierarchyPath(activator.transform),
                    BoneSceneControlPath,
                    StringComparison.Ordinal) ||
                !HasExactPath(
                    GetBoneActivateTarget(activator),
                    BoneEastScene,
                    BoneJailerScenePath) ||
                !HasExactPath(
                    GetBoneDeactivateTarget(activator),
                    BoneEastScene,
                    BoneHuntersScenePath) ||
                !HasExactBoneActivatorSideData(activator))
            {
                return false;
            }

            PlayerDataTest test = BoneActivatorTestField?.GetValue(
                activator
            ) as PlayerDataTest;
            return HasExactBoneEastOuterTest(test);
        }

        private static bool HasExactBoneActivatorSideData(
            TestGameObjectActivator activator)
        {
            return BoneActivateEventField != null &&
                   BoneDeactivateEventField != null &&
                   BoneQuestTestsField != null &&
                   BoneEquipTestsField != null &&
                   BoneEntryWhitelistField != null &&
                   BoneEntryBlacklistField != null &&
                   BoneCheckActiveField != null &&
                   BoneExpectedActiveField != null &&
                   string.IsNullOrEmpty(
                       BoneActivateEventField.GetValue(activator) as
                       string) &&
                   string.IsNullOrEmpty(
                       BoneDeactivateEventField.GetValue(activator) as
                       string) &&
                   IsEmptyArrayField(BoneQuestTestsField, activator) &&
                   IsEmptyArrayField(BoneEquipTestsField, activator) &&
                   IsEmptyArrayField(BoneEntryWhitelistField, activator) &&
                   IsEmptyArrayField(BoneEntryBlacklistField, activator) &&
                   BoneCheckActiveField.GetValue(activator) == null &&
                   BoneExpectedActiveField.GetValue(activator) is
                       bool expectedActive &&
                   !expectedActive;
        }

        private static bool IsEmptyArrayField(
            System.Reflection.FieldInfo field,
            object instance)
        {
            return field.GetValue(instance) is Array values &&
                   values.Length == 0;
        }

        private static bool HasExactBoneEastOuterTest(
            PlayerDataTest test)
        {
            if (test == null ||
                test.TestGroups == null ||
                test.TestGroups.Length != 1 ||
                test.TestGroups[0].Tests == null)
            {
                return false;
            }

            PlayerDataTest.Test[] conditions =
                test.TestGroups[0].Tests;
            return conditions.Length == 5 &&
                   IsExactBoolCondition(
                       conditions[0],
                       WallJumpBool,
                       true) &&
                   IsExactBoolCondition(
                       conditions[1],
                       BlackThreadBool,
                       false) &&
                   IsExactBoolCondition(
                       conditions[2],
                       "boneEastJailerClearedOut",
                       false) &&
                   IsExactBoolCondition(
                       conditions[3],
                       "slab_cloak_battle_completed",
                       false) &&
                   IsExactBoolCondition(
                       conditions[4],
                       "visitedUpperSlab",
                       false);
        }

        private static bool IsExactBoolCondition(
            PlayerDataTest.Test condition,
            string fieldName,
            bool expectedValue)
        {
            return condition.Type == PlayerDataTest.TestType.Bool &&
                   string.Equals(
                       condition.FieldName,
                       fieldName,
                       StringComparison.Ordinal) &&
                   condition.BoolValue == expectedValue;
        }

        private static GameObject GetBoneActivateTarget(
            TestGameObjectActivator activator)
        {
            return BoneActivateTargetField?.GetValue(activator) as
                   GameObject;
        }

        private static GameObject GetBoneDeactivateTarget(
            TestGameObjectActivator activator)
        {
            return BoneDeactivateTargetField?.GetValue(activator) as
                   GameObject;
        }

        private static bool HasExactPath(
            GameObject gameObject,
            string sceneName,
            string hierarchyPath)
        {
            return gameObject != null &&
                   string.Equals(
                       gameObject.scene.name,
                       sceneName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       Utils.GetHierarchyPath(gameObject.transform),
                       hierarchyPath,
                       StringComparison.Ordinal);
        }

        private static bool IsExactCageControlAction(
            FsmStateAction action)
        {
            return action != null &&
                   action.Owner != null &&
                   WardenflyScenes.Contains(action.Owner.scene.name) &&
                   string.Equals(
                       Utils.GetHierarchyPath(action.Owner.transform),
                       GetExpectedCagePath(action.Owner.scene.name),
                       StringComparison.Ordinal) &&
                   action.Fsm != null &&
                   string.Equals(
                       action.Fsm.Name,
                       CageFsm,
                       StringComparison.Ordinal) &&
                   action.State != null &&
                   string.Equals(
                       action.State.Name,
                       CageGateState,
                       StringComparison.Ordinal);
        }

        private static bool IsExactActionIdentity(
            FsmStateAction action,
            string sceneName,
            string hierarchyPath,
            string fsmName,
            string stateName)
        {
            return action != null &&
                   action.Owner != null &&
                   string.Equals(
                       action.Owner.scene.name,
                       sceneName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       Utils.GetHierarchyPath(action.Owner.transform),
                       hierarchyPath,
                       StringComparison.Ordinal) &&
                   action.Fsm != null &&
                   string.Equals(
                       action.Fsm.Name,
                       fsmName,
                       StringComparison.Ordinal) &&
                   action.State != null &&
                   string.Equals(
                       action.State.Name,
                       stateName,
                       StringComparison.Ordinal);
        }

        private static bool IsExactCageControlFsm(
            PlayMakerFSM fsm,
            string activeScene)
        {
            return fsm != null &&
                   fsm.gameObject != null &&
                   string.Equals(
                       fsm.gameObject.scene.name,
                       activeScene,
                       StringComparison.Ordinal) &&
                   WardenflyScenes.Contains(activeScene) &&
                   string.Equals(
                       Utils.GetHierarchyPath(fsm.transform),
                       GetExpectedCagePath(activeScene),
                       StringComparison.Ordinal) &&
                   string.Equals(
                       fsm.FsmName,
                       CageFsm,
                       StringComparison.Ordinal) &&
                   fsm.Fsm != null &&
                   fsm.Fsm.RestartOnEnable &&
                   string.Equals(
                       fsm.Fsm.StartState,
                       CageStartState,
                       StringComparison.Ordinal);
        }

        private static bool IsExactGreymoorSceneControlFsm(
            PlayMakerFSM fsm)
        {
            return fsm != null &&
                   fsm.gameObject != null &&
                   string.Equals(
                       fsm.gameObject.scene.name,
                       GreymoorScene,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       Utils.GetHierarchyPath(fsm.transform),
                       GreymoorSceneControlPath,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       fsm.FsmName,
                       GreymoorSceneControlFsm,
                       StringComparison.Ordinal);
        }

        private static string GetExpectedCagePath(string sceneName)
        {
            return string.Equals(
                sceneName,
                GreymoorScene,
                StringComparison.Ordinal)
                ? GreymoorCagePath
                : StandardCagePath;
        }
    }
}
