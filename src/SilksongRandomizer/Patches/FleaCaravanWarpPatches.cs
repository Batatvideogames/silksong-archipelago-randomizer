using HarmonyLib;
using HutongGames.PlayMaker;
using System;
using System.Linq;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Makes the first caravan ride nonmissable and records the completed
    /// Bone Bottom-to-Greymoor ride for the progressive F4 hub. The native
    /// caravan-location enum is unsuitable because Bone Bottom advances it
    /// even when the player declines the ride.
    /// </summary>
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class FleaCaravanWarpPatches
    {
        private const string ArrivalSceneName = "Greymoor_08_caravan";
        private const string ArrivalObjectName = "door_caravanTravelEnd";
        private const string ArrivalFsmName = "Travel End";
        private const string ArrivalStateName = "Send Event";
        private const string DepartureSceneName = "Bone_10";
        private const string DepartureObjectName = "Caravan";
        private const string DepartureFsmName = "Quest End Sequence";
        private const string DepartureWaitStateName = "Wait";
        private const string DepartureTravelStateName = "Travel";
        private const string DepartureNoTravelStateName = "No Travel";
        private const string DepartureDeclineEventName =
            "QUEST END NOTRAVEL";
        private const string LeaderObjectName =
            "Caravan Troupe Leader Bone NPC";
        private const string LeaderFsmName = "Dialogue";
        private const string LeaderPromptStateName = "Travel Prompt";
        private const string NoLabelPath =
            "Pane/Contents Layout/UI List/No/Text";

        private static readonly string[] ExpectedArrivalActionTypes =
        {
            "HutongGames.PlayMaker.Actions.CallMethodProper",
            "HutongGames.PlayMaker.Actions.CallMethodProper",
            "HutongGames.PlayMaker.Actions.SendMessage",
            "QueueSaveGame",
            "HutongGames.PlayMaker.Actions.RemoveHeroInputBlocker",
            "HutongGames.PlayMaker.Actions.SetPlayerDataBool",
            "HutongGames.PlayMaker.Actions.SendEventToRegister",
        };

        private static readonly string[] ExpectedDepartureTravelActionTypes =
        {
            "HutongGames.PlayMaker.Actions.SetBoolValue",
            "HutongGames.PlayMaker.Actions.TransitionToAudioSnapshot",
            "HutongGames.PlayMaker.Actions.TransitionToAudioSnapshot",
            "HutongGames.PlayMaker.Actions.TransitionToAudioSnapshot",
        };

        private static readonly string[] ExpectedLeaderPromptActionTypes =
        {
            "HutongGames.PlayMaker.Actions.EndDialogue",
            "HutongGames.PlayMaker.Actions.RunFSM",
        };

        private static bool departureTravelPatched;
        private static string originalPromptLabel;

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(PlayMakerFSM __instance)
        {
            if (__instance == null || __instance.gameObject == null)
            {
                return;
            }

            string sceneName = __instance.gameObject.scene.name;
            if (string.Equals(
                    sceneName,
                    ArrivalSceneName,
                    StringComparison.Ordinal))
            {
                PatchArrival(__instance);
                return;
            }

            if (!string.Equals(
                    sceneName,
                    DepartureSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(
                    __instance.gameObject.name,
                    DepartureObjectName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    __instance.FsmName,
                    DepartureFsmName,
                    StringComparison.Ordinal))
            {
                PatchDeparture(__instance);
                return;
            }

            if (string.Equals(
                    __instance.gameObject.name,
                    LeaderObjectName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    __instance.FsmName,
                    LeaderFsmName,
                    StringComparison.Ordinal))
            {
                PatchLeaderPrompt(__instance);
            }
        }

        private static void PatchArrival(PlayMakerFSM fsm)
        {
            if (!string.Equals(
                    fsm.gameObject.name,
                    ArrivalObjectName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    fsm.FsmName,
                    ArrivalFsmName,
                    StringComparison.Ordinal))
            {
                return;
            }

            FsmState arrivalState = fsm.Fsm?.GetState(
                ArrivalStateName
            );
            FsmStateAction[] actions = arrivalState?.Actions;
            if (actions == null)
            {
                LogFailure("the shipped Send Event state was missing");
                return;
            }

            if (actions.Any(action =>
                    action is RecordGreymoorCaravanRideAction))
            {
                return;
            }

            string[] actionTypes = actions
                .Select(action => action?.GetType().FullName ?? string.Empty)
                .ToArray();
            if (!actionTypes.SequenceEqual(ExpectedArrivalActionTypes))
            {
                LogFailure(
                    "the shipped Send Event action layout no longer matched"
                );
                return;
            }

            const int queueSaveIndex = 3;
            RecordGreymoorCaravanRideAction marker =
                new RecordGreymoorCaravanRideAction();
            marker.Init(arrivalState);

            arrivalState.Actions = actions
                .Take(queueSaveIndex)
                .Concat(new FsmStateAction[] { marker })
                .Concat(actions.Skip(queueSaveIndex))
                .ToArray();
        }

        private static void PatchDeparture(PlayMakerFSM fsm)
        {
            FsmState waitState = fsm.Fsm?.GetState(
                DepartureWaitStateName
            );
            FsmState travelState = fsm.Fsm?.GetState(
                DepartureTravelStateName
            );
            FsmState noTravelState = fsm.Fsm?.GetState(
                DepartureNoTravelStateName
            );
            FsmTransition declineTransition = waitState?.Transitions?
                .FirstOrDefault(transition =>
                    transition != null &&
                    string.Equals(
                        transition.EventName,
                        DepartureDeclineEventName,
                        StringComparison.Ordinal));
            FsmStateAction[] travelActions = travelState?.Actions;
            if (waitState?.Transitions == null ||
                waitState.Transitions.Length != 2 ||
                travelState == null ||
                noTravelState == null ||
                declineTransition == null ||
                travelActions == null)
            {
                LogDepartureFailure(
                    "the expected departure states were missing"
                );
                return;
            }

            bool alreadyRetargeted = string.Equals(
                declineTransition.ToState,
                travelState.Name,
                StringComparison.Ordinal
            );
            if (!alreadyRetargeted &&
                !string.Equals(
                    declineTransition.ToState,
                    noTravelState.Name,
                    StringComparison.Ordinal))
            {
                LogDepartureFailure(
                    "the decline branch had an unexpected target"
                );
                return;
            }

            bool hasReset = travelActions.Any(action =>
                action is RestoreCaravanPromptLabelAction);
            if (!hasReset)
            {
                string[] actionTypes = travelActions
                    .Select(action =>
                        action?.GetType().FullName ?? string.Empty)
                    .ToArray();
                if (!actionTypes.SequenceEqual(
                        ExpectedDepartureTravelActionTypes))
                {
                    LogDepartureFailure(
                        "the Travel action layout no longer matched"
                    );
                    return;
                }
            }

            if (!alreadyRetargeted)
            {
                declineTransition.ToState = travelState.Name;
                declineTransition.ToFsmState = travelState;
            }

            if (!hasReset)
            {
                RestoreCaravanPromptLabelAction reset =
                    new RestoreCaravanPromptLabelAction();
                reset.Init(travelState);
                travelState.Actions =
                    new FsmStateAction[] { reset }
                    .Concat(travelActions)
                    .ToArray();
            }

            departureTravelPatched = true;
        }

        private static void PatchLeaderPrompt(PlayMakerFSM fsm)
        {
            FsmState promptState = fsm.Fsm?.GetState(
                LeaderPromptStateName
            );
            FsmStateAction[] actions = promptState?.Actions;
            if (actions == null)
            {
                LogDepartureFailure(
                    "the Travel Prompt state was missing"
                );
                return;
            }

            if (actions.Any(action =>
                    action is SetCaravanPromptLabelAction))
            {
                return;
            }

            string[] actionTypes = actions
                .Select(action => action?.GetType().FullName ?? string.Empty)
                .ToArray();
            if (!actionTypes.SequenceEqual(ExpectedLeaderPromptActionTypes))
            {
                LogDepartureFailure(
                    "the Travel Prompt action layout no longer matched"
                );
                return;
            }

            SetCaravanPromptLabelAction label =
                new SetCaravanPromptLabelAction();
            label.Init(promptState);
            promptState.Actions = actions
                .Take(1)
                .Concat(new FsmStateAction[] { label })
                .Concat(actions.Skip(1))
                .ToArray();
        }

        private static void LogFailure(string reason)
        {
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Flea Caravan F4 unlock failed closed: " +
                reason + "."
            );
        }

        private static void LogDepartureFailure(string reason)
        {
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Flea Caravan departure patch failed closed: " +
                reason + "."
            );
        }

        private static void SetPromptLabel(string value)
        {
            DialogueYesNoBox prompt =
                UnityEngine.Object.FindFirstObjectByType<DialogueYesNoBox>();
            Transform labelTransform = prompt?.transform.Find(NoLabelPath);
            Component label = labelTransform?
                .GetComponents<Component>()
                .FirstOrDefault(component =>
                    component != null &&
                    string.Equals(
                        component.GetType().FullName,
                        "TMProOld.TMP_Text",
                        StringComparison.Ordinal));
            System.Reflection.PropertyInfo textProperty = label?
                .GetType()
                .GetProperty("text");
            if (label != null &&
                textProperty != null &&
                textProperty.PropertyType == typeof(string) &&
                textProperty.CanRead &&
                textProperty.CanWrite)
            {
                string current = textProperty.GetValue(label) as string;
                if (!string.Equals(
                        value,
                        originalPromptLabel,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        current,
                        "Of course",
                        StringComparison.Ordinal))
                {
                    originalPromptLabel = current;
                }

                textProperty.SetValue(label, value);
            }
        }

        private static void RestorePromptLabel()
        {
            string original = originalPromptLabel;
            originalPromptLabel = null;
            if (!string.IsNullOrEmpty(original))
            {
                SetPromptLabel(original);
                originalPromptLabel = null;
            }
        }

        private sealed class RecordGreymoorCaravanRideAction :
            FsmStateAction
        {
            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (state != null && state.IsRoomBound)
                {
                    state.rodeFleaCaravanToGreymoor = true;
                }

                Finish();
            }
        }

        private sealed class SetCaravanPromptLabelAction :
            FsmStateAction
        {
            public override void OnEnter()
            {
                if (departureTravelPatched)
                {
                    SetPromptLabel("Of course");
                }

                Finish();
            }
        }

        private sealed class RestoreCaravanPromptLabelAction :
            FsmStateAction
        {
            public override void OnEnter()
            {
                RestorePromptLabel();
                Finish();
            }
        }
    }
}
