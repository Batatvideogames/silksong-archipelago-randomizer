using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

namespace SilksongRandomizer.Patches
{
    internal static class ToolPouchPatches
    {
        private static bool IsActive(string locationName)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsRandomized(ItemType.ToolPouch) &&
                   state.IsLocationEnabled(locationName) &&
                   state.IsLocationInSeed(locationName);
        }

        private static bool MatchesOwner(
            PlayMakerFSM fsm,
            string sceneName,
            string objectName,
            string fsmName)
        {
            return fsm != null &&
                   fsm.gameObject != null &&
                   string.Equals(
                       fsm.gameObject.scene.name,
                       sceneName,
                       StringComparison.OrdinalIgnoreCase
                   ) &&
                   string.Equals(
                       fsm.gameObject.name,
                       objectName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       fsm.FsmName,
                       fsmName,
                       StringComparison.Ordinal
                   );
        }

        private static void ReportFailedClosed(
            string locationName,
            string detail)
        {
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Tool Pouch source '" +
                locationName + "' was left vanilla because " + detail
            );
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class ToolPouchFsmPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.Last)]
            private static void Prefix(PlayMakerFSM __instance)
            {
                if (__instance == null || __instance.gameObject == null)
                {
                    return;
                }

                PatchLoddieReward(__instance);
                PatchLoddieActThreeFallback(__instance);
                PatchFleatopiaReward(__instance);
            }
        }

        private static void PatchLoddieReward(PlayMakerFSM fsm)
        {
            string locationName = ToolPouchLocationManifest.LoddieLocation;
            if (!IsActive(locationName) ||
                !MatchesOwner(
                    fsm,
                    ToolPouchLocationManifest.LoddieScene,
                    ToolPouchLocationManifest.LoddieNpcObject,
                    ToolPouchLocationManifest.LoddieNpcFsm))
            {
                return;
            }

            FsmState state = fsm.Fsm?.GetState(
                ToolPouchLocationManifest.LoddieRewardState
            );
            if (state?.Actions != null &&
                state.Actions.Length == 2 &&
                state.Actions[1] is CompleteToolPouchLocation)
            {
                return;
            }
            if (state?.Actions == null ||
                state.Actions.Length != 2 ||
                !(state.Actions[0] is SetPlayerDataInt setCompleted) ||
                !(state.Actions[1] is RunFSM))
            {
                ReportFailedClosed(
                    locationName,
                    "Loddie's Reward 1 action layout changed."
                );
                return;
            }

            if (setCompleted.intName == null ||
                setCompleted.value == null ||
                !string.Equals(
                    setCompleted.intName.Value,
                    "pinGalleriesCompleted",
                    StringComparison.Ordinal) ||
                setCompleted.value.Value != 1)
            {
                ReportFailedClosed(
                    locationName,
                    "Loddie's completion flag no longer matches the " +
                    "first challenge."
                );
                return;
            }

            CompleteToolPouchLocation replacement =
                new CompleteToolPouchLocation(locationName);
            replacement.Init(state);
            state.Actions[1] = replacement;
        }

        private static void PatchFleatopiaReward(PlayMakerFSM fsm)
        {
            string locationName = ToolPouchLocationManifest.FleatopiaLocation;
            if (!IsActive(locationName) ||
                !MatchesOwner(
                    fsm,
                    ToolPouchLocationManifest.FleatopiaScene,
                    ToolPouchLocationManifest.FleatopiaNpcObject,
                    ToolPouchLocationManifest.FleatopiaNpcFsm))
            {
                return;
            }

            FsmState state = fsm.Fsm?.GetState(
                ToolPouchLocationManifest.FleatopiaRewardState
            );
            if (state?.Actions != null &&
                state.Actions.Length == 4 &&
                state.Actions[2] is CompleteToolPouchLocation)
            {
                return;
            }
            if (state?.Actions == null ||
                state.Actions.Length != 4 ||
                !(state.Actions[0] is SetBoolValue) ||
                !(state.Actions[1] is SetBoolValue) ||
                !(state.Actions[2] is RunFSM) ||
                !(state.Actions[3] is Wait))
            {
                ReportFailedClosed(
                    locationName,
                    "Mooshka's Award Tool Pouch action layout changed."
                );
                return;
            }

            CompleteToolPouchLocation replacement =
                new CompleteToolPouchLocation(locationName);
            replacement.Init(state);
            state.Actions[2] = replacement;
        }

        private static void PatchLoddieActThreeFallback(PlayMakerFSM fsm)
        {
            string locationName = ToolPouchLocationManifest.LoddieLocation;
            if (!IsActive(locationName) ||
                !MatchesOwner(
                    fsm,
                    ToolPouchLocationManifest.LoddieScene,
                    ToolPouchLocationManifest.LoddieActThreeObject,
                    ToolPouchLocationManifest.LoddieActThreeFsm))
            {
                return;
            }

            FsmObject itemVariable =
                fsm.FsmVariables?.GetFsmObject("Item");
            FsmBool awardsToolPouch =
                fsm.FsmVariables?.GetFsmBool("Awards Tool Pouch");
            if (!HasExactLoddieFreePrompt(fsm, awardsToolPouch))
            {
                ReportFailedClosed(
                    locationName,
                    "the Act 3 fallback free-prompt structure changed."
                );
                return;
            }

            SavedItem proxy = CollectibleSourcePatches.GetProxyItem(
                locationName,
                ItemType.ToolPouch,
                showGenericPresentation: true
            );
            if (ReferenceEquals(itemVariable?.Value, proxy))
            {
                return;
            }
            if (!(itemVariable?.Value is SavedItem nativeItem) ||
                !string.Equals(
                    nativeItem.name,
                    ToolPouchLocationManifest.NativeAssetName,
                    StringComparison.Ordinal))
            {
                ReportFailedClosed(
                    locationName,
                    "the Act 3 fallback no longer contains its exact " +
                    "Tool Pouch item variable."
                );
                return;
            }

            itemVariable.Value = proxy;
            // This flag picks Loddie's free dialogue. The other prompt spends
            // Craftmetal, so keep the free branch while SavedItemGet awards
            // the AP item.
            awardsToolPouch.Value = true;
        }

        private static bool HasExactLoddieFreePrompt(
            PlayMakerFSM fsm,
            FsmBool awardsToolPouch)
        {
            if (awardsToolPouch == null ||
                !awardsToolPouch.Value ||
                fsm?.Fsm == null)
            {
                return false;
            }

            FsmState promptType = fsm.Fsm.GetState(
                ToolPouchLocationManifest.LoddieActThreePromptTypeState
            );
            FsmState freePrompt = fsm.Fsm.GetState(
                ToolPouchLocationManifest.LoddieActThreeFreePromptState
            );
            if (promptType?.Actions == null ||
                promptType.Actions.Length != 1 ||
                !(promptType.Actions[0] is BoolTest promptChoice) ||
                promptChoice.boolVariable == null ||
                !string.Equals(
                    promptChoice.boolVariable.Name,
                    "Awards Tool Pouch",
                    StringComparison.Ordinal) ||
                !IsNamedEvent(promptChoice.isTrue, "TOOL POUCH") ||
                !IsNamedEvent(promptChoice.isFalse, "FINISHED") ||
                freePrompt?.Actions == null ||
                freePrompt.Actions.Length != 1 ||
                !(freePrompt.Actions[0] is DialogueYesNoV2))
            {
                return false;
            }

            return HasOnlyTransitions(
                       promptType,
                       new Tuple<string, string>(
                           "TOOL POUCH",
                           ToolPouchLocationManifest
                               .LoddieActThreeFreePromptState),
                       new Tuple<string, string>(
                           "FINISHED",
                           ToolPouchLocationManifest
                               .LoddieActThreePaidPromptState)) &&
                   HasOnlyTransitions(
                       freePrompt,
                       new Tuple<string, string>(
                           "NO",
                           ToolPouchLocationManifest
                               .LoddieActThreeCancelState),
                       new Tuple<string, string>(
                           "YES",
                           ToolPouchLocationManifest
                               .LoddieActThreeWaitState));
        }

        private static bool IsNamedEvent(
            FsmEvent fsmEvent,
            string expectedName)
        {
            return fsmEvent != null &&
                   string.Equals(
                       fsmEvent.Name,
                       expectedName,
                       StringComparison.Ordinal
                   );
        }

        private static bool HasOnlyTransitions(
            FsmState state,
            params Tuple<string, string>[] expected)
        {
            if (state?.Transitions == null ||
                expected == null ||
                state.Transitions.Length != expected.Length)
            {
                return false;
            }

            foreach (Tuple<string, string> pair in expected)
            {
                bool matched = false;
                foreach (FsmTransition transition in state.Transitions)
                {
                    if (transition != null &&
                        IsNamedEvent(transition.FsmEvent, pair.Item1) &&
                        string.Equals(
                            transition.ToState,
                            pair.Item2,
                            StringComparison.Ordinal))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class CompleteToolPouchLocation : FsmStateAction
        {
            internal readonly string LocationName;

            internal CompleteToolPouchLocation(string locationName)
            {
                LocationName = locationName;
            }

            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (IsActive(LocationName) &&
                    !state.IsLocationChecked(LocationName))
                {
                    state.CheckLocation(LocationName);
                }
                Finish();
            }
        }
    }
}
