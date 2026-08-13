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
            FsmBool awardsToolPouch =
                fsm.FsmVariables.GetFsmBool("Awards Tool Pouch");
            if (awardsToolPouch != null)
            {
                // The Act 3 fallback awards a generic AP item. Disable its
                // Tool Pouch prompt.
                awardsToolPouch.Value = false;
            }
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
