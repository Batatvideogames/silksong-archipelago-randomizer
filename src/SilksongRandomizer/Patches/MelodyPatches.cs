using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SilksongRandomizer.Patches
{
    internal static class MelodyPatches
    {
        private const string MelodyVaultAsset = "melody_Vault";
        private const string MelodyConductorAsset = "melody_Conductor";

        private static bool IsActive(string locationName)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsRandomized(ItemType.Melody) &&
                   state.IsLocationEnabled(locationName) &&
                   state.IsLocationInSeed(locationName);
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class MelodySourceFsmPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.Last)]
            private static void Prefix(PlayMakerFSM __instance)
            {
                if (__instance == null ||
                    __instance.gameObject == null ||
                    !MelodyLocationManifest.TryGet(
                        __instance.gameObject.scene.name,
                        __instance.gameObject.name,
                        __instance.FsmName,
                        out MelodyLocationManifest.Entry entry) ||
                    !IsActive(entry.LocationName))
                {
                    return;
                }

                // Library_08 contains multiple identically named Librarian
                // Dialogue FSMs. Only the relic-board instance owns the
                // Vaultkeeper melody reward. Silently ignore the ambient
                // duplicates instead of logging false blocking errors.
                if (string.Equals(
                        entry.LocationName,
                        MelodyLocationManifest.VaultkeepersMelody,
                        StringComparison.Ordinal
                    ) &&
                    FindState(__instance, "Open Relic Board") == null)
                {
                    return;
                }

                bool patched;
                switch (entry.LocationName)
                {
                    case MelodyLocationManifest.ArchitectsMelody:
                        patched = PatchArchitect(__instance);
                        break;
                    case MelodyLocationManifest.ConductorsMelody:
                        patched = PatchConductorQuestGate(__instance) &&
                            PatchNestedMelodyReward(
                                __instance,
                                "Run Melody Play Prompted",
                                5,
                                entry.LocationName
                            );
                        break;
                    case MelodyLocationManifest.VaultkeepersMelody:
                        patched = PatchNestedMelodyReward(
                            __instance,
                            "Needolin",
                            4,
                            entry.LocationName
                        );
                        break;
                    case MelodyLocationManifest.ElegyOfTheDeep:
                        patched = PatchElegy(__instance);
                        break;
                    case MelodyLocationManifest.BeastlingCall:
                        patched = PatchBeastlingCall(__instance);
                        break;
                    default:
                        patched = false;
                        break;
                }

                if (!patched)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Learned-song source patch failed " +
                        "closed for " + entry.LocationName + " at '" +
                        entry.SceneName + "/" + entry.ObjectName + "/" +
                        entry.FsmName + "'. The FSM has an unexpected layout."
                    );
                }
            }
        }

        private static bool PatchArchitect(PlayMakerFSM fsm)
        {
            FsmState waitForNotify = FindState(fsm, "Wait For Notify");
            FsmState showPrompt = FindState(fsm, "Show Prompt");
            if (waitForNotify == null || showPrompt == null)
            {
                return false;
            }

            if (showPrompt.Actions != null &&
                showPrompt.Actions.Length == 1 &&
                showPrompt.Actions[0] is CompleteLocationAction)
            {
                return true;
            }

            PlayerDataVariableTest nativeGate =
                waitForNotify.Actions == null
                    ? null
                    : waitForNotify.Actions
                        .OfType<PlayerDataVariableTest>()
                        .SingleOrDefault();
            if (nativeGate == null ||
                nativeGate.VariableName == null ||
                !string.Equals(
                    nativeGate.VariableName.Value,
                    "HasMelodyArchitect",
                    StringComparison.Ordinal) ||
                !HasTransition(waitForNotify, "CANCEL", "Has Melody") ||
                !HasTransition(
                    showPrompt,
                    "GET ITEM MSG COVERED",
                    "Singing End"))
            {
                return false;
            }

            ArchitectSourceGate sourceGate =
                new ArchitectSourceGate(
                    MelodyLocationManifest.ArchitectsMelody
                );
            sourceGate.Init(waitForNotify);
            waitForNotify.Actions = new FsmStateAction[] { sourceGate };

            CompleteLocationAction completion =
                new CompleteLocationAction(
                    MelodyLocationManifest.ArchitectsMelody,
                    "GET ITEM MSG COVERED",
                    restoreHeroControl: true
                );
            completion.Init(showPrompt);
            showPrompt.Actions = new FsmStateAction[] { completion };
            return true;
        }

        private static bool PatchConductorQuestGate(PlayMakerFSM fsm)
        {
            FsmState hasItem = FindState(fsm, "Has Item?");
            FsmState questActive = FindState(fsm, "Quest Active?");
            FsmBool melodyQuestActive =
                fsm?.Fsm?.Variables?.FindFsmBool(
                    "Is Melody Quest Active"
                );
            if (hasItem?.Actions == null ||
                hasItem.Actions.Length <= 3 ||
                questActive?.Actions == null ||
                questActive.Actions.Length != 1 ||
                melodyQuestActive == null ||
                !string.Equals(
                    hasItem.Actions[3]?.GetType().FullName,
                    "QuestPlaymakerActions.GetQuestState",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    questActive.Actions[0]?.GetType().FullName,
                    "QuestPlaymakerActions.CheckQuestStateV2",
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            FsmTransition trueTransition = questActive.Transitions?
                .SingleOrDefault(transition =>
                    transition != null &&
                    string.Equals(
                        transition.EventName,
                        "TRUE",
                        StringComparison.Ordinal
                    ) &&
                    string.Equals(
                        transition.ToState,
                        "Run Melody Play Prompted",
                        StringComparison.Ordinal
                    ));
            FieldInfo notTrackedEventField = AccessTools.Field(
                questActive.Actions[0].GetType(),
                "NotTrackedEvent"
            );
            if (trueTransition?.FsmEvent == null ||
                notTrackedEventField == null ||
                !typeof(FsmEvent).IsAssignableFrom(
                    notTrackedEventField.FieldType
                ))
            {
                return false;
            }

            SetBoolValue clearQuestGate = new SetBoolValue
            {
                boolVariable = melodyQuestActive,
                boolValue = false,
            };
            clearQuestGate.Init(hasItem);
            hasItem.Actions[3] = clearQuestGate;
            notTrackedEventField.SetValue(
                questActive.Actions[0],
                trueTransition.FsmEvent
            );
            return true;
        }

        private static bool PatchNestedMelodyReward(
            PlayMakerFSM owner,
            string stateName,
            int runFsmIndex,
            string locationName
        )
        {
            FsmState sourceState = FindState(owner, stateName);
            if (sourceState == null ||
                sourceState.Actions == null ||
                sourceState.Actions.Length <= runFsmIndex ||
                !(sourceState.Actions[runFsmIndex] is RunFSM runAction))
            {
                return false;
            }

            Fsm nestedFsm = runAction.fsmTemplateControl?.RunFsm;
            FsmState giveItem = nestedFsm?.GetState("Give Item");
            FsmState uiMessage = nestedFsm?.GetState("UI Msg");
            FsmState stopNeedolin = nestedFsm?.GetState("Stop Needolin");
            if (giveItem == null ||
                uiMessage == null ||
                stopNeedolin == null)
            {
                return false;
            }

            if (giveItem.Actions != null &&
                giveItem.Actions.Any(action =>
                    action is CompleteLocationAction))
            {
                return true;
            }

            if (giveItem.Actions == null ||
                giveItem.Actions.Length == 0 ||
                uiMessage.Actions == null ||
                uiMessage.Actions.Length < 5 ||
                !HasTransitionTo(uiMessage, "Stop Needolin") ||
                !HasTransitionTo(stopNeedolin, "Give Item"))
            {
                return false;
            }

            List<FsmStateAction> giveActions =
                giveItem.Actions.Skip(1).ToList();
            giveActions.Add(CreateWait(giveItem, 0.5f));
            CompleteLocationAction completion =
                new CompleteLocationAction(locationName);
            completion.Init(giveItem);
            giveActions.Add(completion);
            giveActions.Add(CreateWait(giveItem, 0.5f));
            giveItem.Actions = giveActions.ToArray();

            HashSet<int> removeUiIndices =
                new HashSet<int> { 0, 1, 3, 4 };
            uiMessage.Actions = uiMessage.Actions
                .Where((_, index) => !removeUiIndices.Contains(index))
                .ToArray();

            if (!RetargetTransition(
                    uiMessage,
                    "Stop Needolin",
                    FsmEvent.Finished) ||
                !RetargetTransition(
                    stopNeedolin,
                    "Give Item",
                    FsmEvent.Finished))
            {
                return false;
            }

            return true;
        }

        private static bool PatchElegy(PlayMakerFSM fsm)
        {
            FsmState getItem = FindState(fsm, "Get Item Msg");
            FsmState updateQuest = FindState(fsm, "Update Quest");
            if (getItem == null ||
                updateQuest == null ||
                !HasTransition(
                    getItem,
                    "GET ITEM MSG END",
                    "Stop Needolin") ||
                !ReplaceNamedPlayerDataWrite(
                    updateQuest,
                    "hasNeedolinMemoryPowerup",
                    new PreserveElegyOwnershipAction()))
            {
                return false;
            }

            if (getItem.Actions.Any(action =>
                    action is CompleteLocationAction))
            {
                return true;
            }

            List<FsmStateAction> actions = getItem.Actions
                .Where(action =>
                    action != null &&
                    action.GetType().Name != "CreateUIMsgGetItem" &&
                    action.GetType().Name != "SetFsmString" &&
                    !(action is Wait))
                .ToList();
            CompleteLocationAction completion =
                new CompleteLocationAction(
                    MelodyLocationManifest.ElegyOfTheDeep,
                    "GET ITEM MSG END"
                );
            completion.Init(getItem);
            actions.Add(completion);
            getItem.Actions = actions.ToArray();
            return true;
        }

        private static bool PatchBeastlingCall(PlayMakerFSM fsm)
        {
            FsmState getItem = FindState(fsm, "Get Item Msg");
            FsmState timePasses = FindState(fsm, "Time Passes");
            if (getItem == null ||
                timePasses == null ||
                !HasTransition(
                    getItem,
                    "GET ITEM MSG END",
                    "Time Passes") ||
                !ReplaceNamedPlayerDataWrite(
                    timePasses,
                    "UnlockedFastTravelTeleport",
                    new PreserveBeastlingOwnershipAction()))
            {
                return false;
            }

            if (getItem.Actions.Any(action =>
                    action is CompleteLocationAction))
            {
                return true;
            }

            List<FsmStateAction> actions = getItem.Actions
                .Where(action =>
                    action != null &&
                    action.GetType().Name != "CreateUIMsgGetItem" &&
                    action.GetType().Name != "SetFsmString")
                .ToList();
            CompleteLocationAction completion =
                new CompleteLocationAction(
                    MelodyLocationManifest.BeastlingCall,
                    "GET ITEM MSG END",
                    resolveBellEater: true
                );
            completion.Init(getItem);
            actions.Add(completion);
            getItem.Actions = actions.ToArray();
            return true;
        }

        [HarmonyPatch(typeof(SavedItemCanGetMore), "get_IsTrue")]
        private static class MelodyCanGetMorePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                SavedItemCanGetMore __instance,
                ref bool __result
            )
            {
                SavedItem item = __instance?.Item?.Value as SavedItem;
                if (item == null ||
                    !TryGetCanGetMoreLocation(
                        __instance,
                        item.name,
                        out string locationName) ||
                    !IsActive(locationName))
                {
                    return true;
                }

                __result = !SaveState.Instance.IsLocationChecked(
                    locationName
                );
                return false;
            }
        }

        private static bool TryGetCanGetMoreLocation(
            FsmStateAction action,
            string itemName,
            out string locationName
        )
        {
            locationName = null;
            if (action?.Owner == null ||
                action.Owner.scene.name == null)
            {
                return false;
            }

            if (string.Equals(
                    itemName,
                    MelodyConductorAsset,
                    StringComparison.Ordinal) &&
                string.Equals(
                    action.Owner.scene.name,
                    "Hang_12",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    action.Owner.name,
                    "Last Conductor NPC",
                    StringComparison.Ordinal))
            {
                locationName = MelodyLocationManifest.ConductorsMelody;
                return true;
            }

            if (string.Equals(
                    itemName,
                    MelodyVaultAsset,
                    StringComparison.Ordinal) &&
                string.Equals(
                    action.Owner.scene.name,
                    "Library_08",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    action.Owner.name,
                    "Librarian",
                    StringComparison.Ordinal))
            {
                locationName = MelodyLocationManifest.VaultkeepersMelody;
                return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(PlayerData), "get_BellCentipedeWaiting")]
        private static class BellCentipedeWaitingPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                PlayerData __instance,
                ref bool __result
            )
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.Melody) ||
                    !state.IsLocationInSeed(
                        MelodyLocationManifest.BeastlingCall))
                {
                    return;
                }

                __result = __instance.blackThreadWorld &&
                    !IsBellEaterResolved(state);
            }
        }

        [HarmonyPatch(typeof(PlayerData), "get_BellCentipedeLocked")]
        private static class BellCentipedeLockedPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                PlayerData __instance,
                ref bool __result
            )
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.Melody) ||
                    !state.IsLocationInSeed(
                        MelodyLocationManifest.BeastlingCall))
                {
                    return;
                }

                __result = __instance.bellCentipedeAppeared &&
                    !IsBellEaterResolved(state);
            }
        }

        private static bool IsBellEaterResolved(SaveState state)
        {
            return state.bellEaterResolved ||
                   state.IsLocationChecked(
                       MelodyLocationManifest.BeastlingCall
                   );
        }

        private static bool ReplaceNamedPlayerDataWrite(
            FsmState state,
            string variableName,
            FsmStateAction replacement
        )
        {
            if (state?.Actions == null)
            {
                return false;
            }

            for (int i = 0; i < state.Actions.Length; i++)
            {
                if (state.Actions[i] is PreserveElegyOwnershipAction ||
                    state.Actions[i] is PreserveBeastlingOwnershipAction)
                {
                    return true;
                }
                if (!(state.Actions[i] is SetPlayerDataVariable write) ||
                    write.VariableName == null ||
                    !string.Equals(
                        write.VariableName.Value,
                        variableName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                replacement.Init(state);
                state.Actions[i] = replacement;
                return true;
            }

            return false;
        }

        private static Wait CreateWait(FsmState state, float seconds)
        {
            Wait wait = new Wait
            {
                time = seconds,
                realTime = false,
            };
            wait.Init(state);
            return wait;
        }

        private static FsmState FindState(
            PlayMakerFSM fsm,
            string stateName
        )
        {
            return fsm?.Fsm?.GetState(stateName);
        }

        private static bool HasTransition(
            FsmState state,
            string eventName,
            string targetState
        )
        {
            return state?.Transitions != null &&
                   state.Transitions.Any(transition =>
                       transition != null &&
                       string.Equals(
                           transition.EventName,
                           eventName,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           transition.ToState,
                           targetState,
                           StringComparison.Ordinal));
        }

        private static bool HasTransitionTo(
            FsmState state,
            string targetState
        )
        {
            return state?.Transitions != null &&
                   state.Transitions.Any(transition =>
                       transition != null &&
                       string.Equals(
                           transition.ToState,
                           targetState,
                           StringComparison.Ordinal));
        }

        private static bool RetargetTransition(
            FsmState state,
            string targetStateName,
            FsmEvent newEvent
        )
        {
            FsmTransition transition = state?.Transitions?
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(
                        candidate.ToState,
                        targetStateName,
                        StringComparison.Ordinal));
            FsmState target = state?.Fsm?.GetState(targetStateName);
            if (transition == null || target == null || newEvent == null)
            {
                return false;
            }

            transition.FsmEvent = newEvent;
            transition.ToState = target.Name;
            transition.ToFsmState = target;
            return true;
        }

        private sealed class ArchitectSourceGate : FsmStateAction
        {
            private readonly string locationName;

            internal ArchitectSourceGate(string locationName)
            {
                this.locationName = locationName;
            }

            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (state != null &&
                    state.IsLocationChecked(locationName))
                {
                    Fsm.Event("CANCEL");
                    Finish();
                }
                // An unchecked source remains in this state.
                // The four cylinder events drive its native progression.
            }
        }

        private sealed class CompleteLocationAction : FsmStateAction
        {
            private readonly string locationName;
            private readonly string completionEvent;
            private readonly bool resolveBellEater;
            private readonly bool restoreHeroControl;

            internal CompleteLocationAction(
                string locationName,
                string completionEvent = null,
                bool resolveBellEater = false,
                bool restoreHeroControl = false
            )
            {
                this.locationName = locationName;
                this.completionEvent = completionEvent;
                this.resolveBellEater = resolveBellEater;
                this.restoreHeroControl = restoreHeroControl;
            }

            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (state != null &&
                    state.IsRandomized(ItemType.Melody) &&
                    state.IsLocationEnabled(locationName) &&
                    state.IsLocationInSeed(locationName))
                {
                    state.CheckLocation(locationName);
                    if (resolveBellEater)
                    {
                        state.bellEaterResolved = true;
                    }
                }

                if (restoreHeroControl)
                {
                    HeroController hero = HeroController.instance;
                    if (hero != null)
                    {
                        hero.RelinquishControl();
                    }
                    if (PlayerData.instance != null)
                    {
                        PlayerData.instance.disableInventory = false;
                    }
                    if (hero != null)
                    {
                        hero.RegainControl();
                    }
                }

                Finish();
                if (!string.IsNullOrEmpty(completionEvent))
                {
                    Fsm.Event(completionEvent);
                }
            }
        }

        private sealed class PreserveElegyOwnershipAction : FsmStateAction
        {
            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (PlayerData.instance != null && state != null)
                {
                    PlayerData.instance.hasNeedolinMemoryPowerup =
                        state.receivedItems.Contains(
                            MelodyLocationManifest.ElegyOfTheDeep
                        );
                }
                Finish();
            }
        }

        private sealed class PreserveBeastlingOwnershipAction : FsmStateAction
        {
            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (state != null)
                {
                    state.bellEaterResolved = true;
                }
                if (PlayerData.instance != null && state != null)
                {
                    PlayerData.instance.UnlockedFastTravelTeleport =
                        state.canUseBeastlingCall;
                }
                Finish();
            }
        }
    }
}
