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
        private const string CureCrestChangeState = "Crest Change";

        private const string NativeCurseScene = "Shellwood_25b";
        private const string NativeCurseObject = "door_curseSequenceEnd";
        private const string NativeCurseFsm = "Curse Sequence";
        private const string NativeSetCursedState = "Set Cursed";

        private const string CursedEndingScene = "Cradle_03";
        private const string CursedEndingObjectPath = "Boss Scene/Silk Boss";
        private const string CursedEndingFsm = "Phase Control";
        private const string CursedEndingGateState = "Death Type";

        internal static bool CanUseCursedServices =>
            SaveState.Instance?.IsRoomBound == true && HasGenuineGreyrootCurse(PlayerData.instance);

        private static bool IsService(FsmStateAction action, string scene, string owner, string state) =>
            action?.Owner != null && action.Owner.scene.name == scene && action.Owner.name == owner &&
            action.Fsm?.Name == "Dialogue" && action.State?.Name == state;

        private static bool IsCrestShrine(FsmStateAction action, string state)
        {
            if (action?.Owner == null || action.Owner.name != "Crest Get Shrine" ||
                action.Fsm?.Name != "Control" || action.State?.Name != state) return false;
            string scene = action.Owner.scene.name;
            return scene == "Ant_19" || scene == "Chapel_Wanderer" || scene == "Greymoor_20c" ||
                scene == "Tut_04" || scene == "Tut_05" || scene == "Under_20";
        }

        [HarmonyPatch(typeof(CheckIfCrestEquipped), "get_IsTrue")]
        private static class KeepCursedServicesAvailable
        {
            private static void Postfix(CheckIfCrestEquipped __instance, ref bool __result)
            {
                if (!__result || !CanUseCursedServices || !IsNativeCursedCrest(__instance.Crest)) return;
                if (IsService(__instance, "Weave_10", "Crest Upgrade Shrine", "Met?") ||
                    IsService(__instance, "Halfway_01", "HH Bartender", "Met?") ||
                    IsService(__instance, "Belltown", "Couriers Quest Giver", "Has Met?") ||
                    IsService(__instance, "Belltown", "Couriers Quest Giver", "Shop?"))
                    __result = false;
            }
        }

        [HarmonyPatch(typeof(PlayerDataBoolMultiTest), nameof(PlayerDataBoolMultiTest.OnEnter))]
        private static class KeepPavoHouseConversationAvailable
        {
            private static bool Prefix(PlayerDataBoolMultiTest __instance)
            {
                if (!CanUseCursedServices ||
                    !IsService(__instance, "Belltown", "Belltown Greeter NPC", "Convo") ||
                    __instance.passedEvent?.Name != "CURSED") return true;
                __instance.storeValue.Value = false;
                __instance.Fsm.Event(__instance.failedEvent);
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerDataVariableTest), nameof(PlayerDataVariableTest.OnEnter))]
        private static class KeepCursedShrinesAvailable
        {
            private static bool Prefix(PlayerDataVariableTest __instance)
            {
                if (!CanUseCursedServices || __instance.VariableName?.Value != "IsAnyCursed" ||
                    !__instance.ExpectedValue.boolValue ||
                    !(IsCrestShrine(__instance, "Cursed?") ||
                      IsService(__instance, "Tut_04", "Snail Shamans Set", "Can Sit?"))) return true;
                __instance.Fsm.Event(__instance.IsNotExpectedEvent);
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(AutoEquipCrestV2), nameof(AutoEquipCrestV2.OnEnter))]
        private static class PreserveCurseAfterShrine
        {
            private static bool Prefix(AutoEquipCrestV2 __instance)
            {
                if (!CanUseCursedServices || !IsCrestShrine(__instance, "Crest Change")) return true;
                __instance.Finish();
                return false;
            }
        }

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
                PatchYarnabyCureCrestRestore(__instance);
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
            else if (SaveState.Instance?.IsRoomBound == true &&
                     MatchesCursedEndingFsm(__instance, sceneName))
            {
                PatchCursedEndingGate(__instance);
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

        private static bool MatchesCursedEndingFsm(
            PlayMakerFSM fsm,
            string actualScene)
        {
            return string.Equals(
                       actualScene,
                       CursedEndingScene,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       Utils.GetHierarchyPath(fsm.transform),
                       CursedEndingObjectPath,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       fsm.FsmName,
                       CursedEndingFsm,
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
                IsNativeCursedCrest(nativeEquip.Crest) &&
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

        private static void PatchYarnabyCureCrestRestore(PlayMakerFSM fsm)
        {
            FsmState crestChange = fsm.Fsm?.GetState(CureCrestChangeState);
            FsmStateAction[] actions = crestChange?.Actions;
            if (actions != null &&
                actions.Length == 7 &&
                actions[1] is RestoreOwnedCrestAfterCure)
            {
                return;
            }

            UnlockCrest nativeUnlock =
                actions != null && actions.Length > 0
                    ? actions[0] as UnlockCrest
                    : null;
            AutoEquipCrestV2 nativeEquip =
                actions != null && actions.Length > 1
                    ? actions[1] as AutoEquipCrestV2
                    : null;
            ToolCrest unlockCrest = nativeUnlock?.Crest?.Value as ToolCrest;
            ToolCrest equipCrest = nativeEquip?.Crest?.Value as ToolCrest;
            bool matches =
                actions != null &&
                actions.Length == 7 &&
                nativeUnlock != null &&
                nativeEquip != null &&
                unlockCrest != null &&
                equipCrest != null &&
                string.Equals(
                    unlockCrest.name,
                    "Witch",
                    StringComparison.Ordinal) &&
                string.Equals(
                    equipCrest.name,
                    unlockCrest.name,
                    StringComparison.Ordinal) &&
                actions[2] is CallMethodProper &&
                actions[3] is CallMethodProper &&
                actions[4] is Wait &&
                actions[5] is QueueSaveGameV2 &&
                actions[6] is Tk2dPlayAnimationWithEvents &&
                HasTransition(crestChange, "FINISHED", "Curse Fix Fade Up");
            if (!matches)
            {
                LogFailure(
                    "Yarnaby's cure crest change no longer matched"
                );
                return;
            }

            RestoreOwnedCrestAfterCure replacement =
                new RestoreOwnedCrestAfterCure(
                    equipCrest,
                    nativeEquip.SkipToAppear?.Value == true
                );
            replacement.Init(crestChange);
            actions[1] = replacement;
        }

        private static void PatchCursedEndingGate(PlayMakerFSM fsm)
        {
            FsmState deathType = fsm.Fsm?.GetState(CursedEndingGateState);
            FsmStateAction[] actions = deathType?.Actions;
            if (actions != null &&
                actions.Length == 1 &&
                actions[0] is CheckGenuineGreyrootCurse)
            {
                return;
            }

            CheckIfCrestEquipped nativeCheck =
                actions != null && actions.Length == 1
                    ? actions[0] as CheckIfCrestEquipped
                    : null;
            bool matches =
                nativeCheck != null &&
                nativeCheck.Enabled &&
                IsNativeCursedCrest(nativeCheck.Crest) &&
                IsNamedLocalEvent(nativeCheck.trueEvent, "CURSED") &&
                IsNoEvent(nativeCheck.falseEvent) &&
                nativeCheck.storeValue != null &&
                nativeCheck.storeValue.UseVariable &&
                deathType.Transitions != null &&
                deathType.Transitions.Length == 2 &&
                HasTransition(
                    deathType,
                    "FINISHED",
                    "Start Death Sequence") &&
                HasTransition(
                    deathType,
                    "CURSED",
                    "Start Death Sequence Curse");
            if (!matches)
            {
                LogFailure(
                    "Grand Mother Silk's Death Type state no " +
                    "longer matched"
                );
                return;
            }

            CheckGenuineGreyrootCurse replacement =
                new CheckGenuineGreyrootCurse(nativeCheck);
            replacement.Init(deathType);
            actions[0] = replacement;
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

        private static bool IsNativeCursedCrest(FsmObject crest)
        {
            ToolCrest expected = GlobalSettings.Gameplay.CursedCrest;
            return expected != null &&
                   crest?.Value is ToolCrest actual &&
                   string.Equals(
                       actual.name,
                       expected.name,
                       StringComparison.Ordinal);
        }

        private static bool IsNoEvent(FsmEvent fsmEvent)
        {
            return fsmEvent == null || string.IsNullOrEmpty(fsmEvent.Name);
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

        internal static bool HasGenuineGreyrootCurse(PlayerData playerData)
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

        private static bool IsCursedEndingGoal()
        {
            SaveState state = SaveState.Instance;
            return state?.IsRoomBound == true &&
                   string.Equals(
                       state.goal,
                       Archipelago.CursedEndingGoal,
                       StringComparison.Ordinal
                   );
        }

        private static bool ShouldOfferYarnabyCure(PlayerData playerData)
        {
            return !IsCursedEndingGoal() &&
                   HasGenuineGreyrootCurse(playerData);
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

                if (ProgressionShufflePatches.TryOpenPendingDoctorWish(__instance))
                {
                    return false;
                }

                if (ShouldOfferYarnabyCure(PlayerData.instance))
                {
                    // The TRUE event and all vanilla quest handling remain
                    // active for Greyroot's real permanent curse unless the
                    // configured goal requires preserving it.
                    return true;
                }

                // PlayerDataVariableTest normally sends its not-expected
                // event and then finishes. These two actions have no
                // not-expected event, so Finish follows their FINISHED
                // transition into Yarnaby's ordinary non-curse dialogue.
                __instance.Fsm.Event(__instance.IsNotExpectedEvent);
                __instance.Finish();
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] Yarnaby skipped the curse cure for the " +
                    "configured goal or a non-Greyroot Cursed Crest."
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

        private sealed class RestoreOwnedCrestAfterCure : FsmStateAction
        {
            private readonly ToolCrest nativeCrest;
            private readonly bool skipToAppear;

            internal RestoreOwnedCrestAfterCure(
                ToolCrest nativeCrest,
                bool skipToAppear)
            {
                this.nativeCrest = nativeCrest;
                this.skipToAppear = skipToAppear;
            }

            public override void OnEnter()
            {
                if (skipToAppear)
                {
                    BindOrbHudFrame.SkipToNextAppear = true;
                }

                try
                {
                    RestoreCrest();
                }
                finally
                {
                    BindOrbHudFrame.SkipToNextAppear = false;
                }
                Finish();
            }

            private void RestoreCrest()
            {
                SaveState state = SaveState.Instance;
                if (state == null || !state.IsRandomized(ItemType.Crest))
                {
                    ToolItemManager.AutoEquip(nativeCrest, false, true);
                    return;
                }

                PlayerData playerData = PlayerData.instance;
                ToolCrest target = playerData == null
                    ? null
                    : ToolItemManager.GetCrestByName(
                        playerData.PreviousCrestID
                    );
                if (!IsReceivedCrest(state, target))
                {
                    target = null;
                    foreach (ToolCrest candidate in
                             ToolItemManager.GetAllCrests())
                    {
                        if (IsReceivedCrest(state, candidate))
                        {
                            target = candidate;
                            break;
                        }
                    }
                }

                if (target == null)
                {
                    LogFailure(
                        "Yarnaby could not find a received crest after cure"
                    );
                    return;
                }

                if (!ToolPatches.AutoEquipOwnedCrestAfterNativeCure(target))
                {
                    LogFailure(
                        "Yarnaby could not restore the received crest after cure"
                    );
                }
            }

            private static bool IsReceivedCrest(
                SaveState state,
                ToolCrest crest)
            {
                return state != null &&
                       crest != null &&
                       !string.Equals(
                           crest.name,
                           GlobalSettings.Gameplay.CursedCrest?.name,
                           StringComparison.Ordinal) &&
                       state.receivedItems.Contains(
                           CrestNames.GetItemNameFromInternal(crest.name)
                       );
            }
        }

        private sealed class CheckGenuineGreyrootCurse :
            FSMUtility.CheckFsmStateAction
        {
            internal CheckGenuineGreyrootCurse(
                CheckIfCrestEquipped nativeCheck)
            {
                trueEvent = nativeCheck.trueEvent;
                falseEvent = nativeCheck.falseEvent;
                storeValue = nativeCheck.storeValue;
            }

            public override bool IsTrue =>
                HasGenuineGreyrootCurse(PlayerData.instance);
        }
    }
}
