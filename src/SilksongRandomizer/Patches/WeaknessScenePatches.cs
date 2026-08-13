using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Removes the forced weakness/limping sequences without skipping the
    /// story bookkeeping around them.
    ///
    /// The known FSM targets and the Act 3 cleanup strategy were informed by
    /// Vitaxses' MIT-licensed Silksong.QoL SkipWeakness implementation:
    /// https://github.com/Vitaxses/Silksong.QoL
    ///
    /// This implementation uses the game's own PlayMaker types, checks each
    /// state and action before changing the graph and
    /// keeps Bone Bottom's native quest, dialogue, save and message actions.
    /// </summary>
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class WeaknessScenePatches
    {
        private const string ControlFsm = "Control";
        private const string FinishedEvent = "FINISHED";
        private const string GetItemMessageEndEvent = "GET ITEM MSG END";
        private const string ChurchRespawnMarker =
            "Death Respawn Marker Church";

        [HarmonyPostfix]
        private static void Postfix(PlayMakerFSM __instance)
        {
            if (__instance == null ||
                !string.Equals(
                    __instance.FsmName,
                    ControlFsm,
                    StringComparison.Ordinal
                ))
            {
                return;
            }

            string sceneName = __instance.gameObject.scene.name;
            string objectName = __instance.name;

            try
            {
                if (IsOneOf(sceneName, "Tut_01", "Tut_03", "Bonetown") &&
                    string.Equals(
                        objectName,
                        "Weakness Scene",
                        StringComparison.Ordinal
                    ))
                {
                    TryRedirectFinished(
                        __instance,
                        "Check PD Bool",
                        "Activated",
                        "early weakness"
                    );
                }
                else if (string.Equals(
                             sceneName,
                             "Bonetown",
                             StringComparison.Ordinal
                         ) &&
                         string.Equals(
                             objectName,
                             "Churchkeeper Intro Scene",
                             StringComparison.Ordinal
                         ))
                {
                    TryPatchChurchkeeperIntro(__instance);
                }
                else if (string.Equals(
                             sceneName,
                             "Cog_09_Destroyed",
                             StringComparison.Ordinal
                         ) &&
                         string.Equals(
                             objectName,
                             "Weakness Cog Drop Scene",
                             StringComparison.Ordinal
                         ))
                {
                    TryRedirectFinished(
                        __instance,
                        "Init",
                        "Activated",
                        "Act 3 cog weakness"
                    );
                }
                else if (string.Equals(
                             sceneName,
                             "Song_25",
                             StringComparison.Ordinal
                         ) &&
                         string.Equals(
                             objectName,
                             "Weakness Scene Act3 Final",
                             StringComparison.Ordinal
                         ))
                {
                    TryPatchAct3Final(__instance);
                }
                else if (string.Equals(
                             sceneName,
                             "Song_25",
                             StringComparison.Ordinal
                         ) &&
                         string.Equals(
                             objectName,
                             "Hatch",
                             StringComparison.Ordinal
                         ))
                {
                    TryPatchAct3Hatch(__instance);
                }
            }
            catch (Exception ex)
            {
                LogFailure(
                    __instance,
                    "unexpected exception; leaving any uncommitted edit alone: " +
                    ex
                );
            }
        }

        private static void TryRedirectFinished(
            PlayMakerFSM fsm,
            string fromStateName,
            string toStateName,
            string description
        )
        {
            if (!TryPrepareTransition(
                    fsm,
                    fromStateName,
                    FinishedEvent,
                    toStateName,
                    out FsmTransition transition,
                    out FsmState target,
                    out string reason
                ))
            {
                LogFailure(fsm, description + ": " + reason);
                return;
            }

            CommitTransition(transition, target);
            LogSuccess(fsm, description);
        }

        private static void TryPatchChurchkeeperIntro(PlayMakerFSM fsm)
        {
            if (!TryPrepareTransition(
                    fsm,
                    "Pause",
                    FinishedEvent,
                    "Set End",
                    out FsmTransition pauseFinished,
                    out FsmState setEnd,
                    out string reason
                ))
            {
                LogFailure(fsm, "Churchkeeper intro: " + reason);
                return;
            }

            FsmState end = FindState(fsm, "End");
            if (end == null)
            {
                LogFailure(
                    fsm,
                    "Churchkeeper intro: required state 'End' is missing"
                );
                return;
            }

            if (!HasTransition(
                    setEnd,
                    GetItemMessageEndEvent,
                    "End"
                ))
            {
                LogFailure(
                    fsm,
                    "Churchkeeper intro: Set End -> End event is missing"
                );
                return;
            }

            if (!HasTruePlayerDataBoolAction(
                    setEnd,
                    "churchKeeperIntro"
                ) ||
                !HasActionType(
                    setEnd,
                    "HutongGames.PlayMaker.Actions.EndDialogue"
                ) ||
                !HasActionType(
                    setEnd,
                    "QuestPlaymakerActions.BeginQuestV2"
                ) ||
                !HasActionType(
                    setEnd,
                    "QueueSaveGameV2"
                ) ||
                !HasActionType(
                    setEnd,
                    "HutongGames.PlayMaker.Actions.ActivateGameObject"
                ))
            {
                LogFailure(
                    fsm,
                    "Churchkeeper intro: Set End no longer contains the " +
                    "expected story/quest/dialogue/save/message actions"
                );
                return;
            }

            SetDeathRespawnV2 churchRespawn =
                FindOnlyAction<SetDeathRespawnV2>(end);
            if (churchRespawn == null ||
                churchRespawn.RespawnMarkerName == null ||
                !string.Equals(
                    churchRespawn.RespawnMarkerName.Value,
                    ChurchRespawnMarker,
                    StringComparison.Ordinal
                ))
            {
                LogFailure(
                    fsm,
                    "Churchkeeper intro: End no longer contains the " +
                    "expected Church death-respawn action"
                );
                return;
            }

            FsmStateAction[] setEndActions = setEnd.Actions;
            FsmStateAction[] endActions = end.Actions;
            if (setEndActions == null || endActions == null)
            {
                LogFailure(
                    fsm,
                    "Churchkeeper intro: completion actions could not be loaded"
                );
                return;
            }

            bool hasFallback = false;
            foreach (FsmStateAction action in setEndActions)
            {
                if (action is DelayedFsmEventFallback)
                {
                    hasFallback = true;
                    break;
                }
            }

            FsmStateAction[] patchedEndActions =
                PrepareActionRemoval(endActions, churchRespawn);

            // Commit only after the entire native completion path has been
            // validated. Set End retains all vanilla actions, including the
            // Great Citadel quest prompt and its sound. The delayed event is
            // only a softlock fallback: the native prompt normally sends the
            // same event first, causing this state (and action) to exit. The
            // skipped intro must not retain End's Church respawn override:
            // GET ITEM MSG END can arrive several seconds after the player
            // has already sat at Bone Bottom's bench and would otherwise
            // replace that freshly selected bench.
            if (!hasFallback)
            {
                AppendAction(
                    setEnd,
                    new DelayedFsmEventFallback(
                        GetItemMessageEndEvent,
                        8f
                    )
                );
            }

            end.Actions = patchedEndActions;
            CommitTransition(pauseFinished, setEnd);
            if (PlayerData.instance != null)
            {
                PlayerData.instance.churchKeeperIntro = true;
            }

            LogSuccess(
                fsm,
                "Churchkeeper weakness bypass with native quest completion"
            );
        }

        private static void TryPatchAct3Final(PlayMakerFSM fsm)
        {
            FsmState dormant = FindState(fsm, "Dormant");
            FsmState walkRight = FindState(fsm, "Walk R");
            FsmState weakFall = FindState(fsm, "Weak Fall");
            FsmState firstLand = FindState(fsm, "First Land");
            FsmState weakFallLand = FindState(fsm, "Weak Fall Land");
            FsmState fadeOut = FindState(fsm, "Fade Out");
            FsmState toEnclave = FindState(fsm, "To Enclave Scene");
            FsmState releaseLock = FindState(fsm, "Release Lock");

            if (dormant == null ||
                walkRight == null ||
                weakFall == null ||
                firstLand == null ||
                weakFallLand == null ||
                fadeOut == null ||
                toEnclave == null ||
                releaseLock == null)
            {
                LogFailure(
                    fsm,
                    "Act 3 final: one or more required states are missing"
                );
                return;
            }

            Trigger2dEvent dormantTrigger =
                FindFirstAction<Trigger2dEvent>(dormant);
            Trigger2dEvent hatchReference =
                FindLastAction<Trigger2dEvent>(walkRight);
            AddHeroInputBlocker inputBlockerReference =
                FindFirstAction<AddHeroInputBlocker>(firstLand);
            Wait landingWait = FindFirstAction<Wait>(weakFallLand);
            ScreenFader screenFader =
                FindFirstAction<ScreenFader>(fadeOut);
            Wait fadeWait = FindFirstAction<Wait>(fadeOut);
            RemoveHeroInputBlocker inputReleaseReference =
                FindFirstAction<RemoveHeroInputBlocker>(releaseLock);

            if (dormantTrigger == null ||
                hatchReference == null ||
                hatchReference.gameObject == null ||
                hatchReference.sendEvent == null ||
                inputBlockerReference == null ||
                inputBlockerReference.Blocker == null ||
                landingWait == null ||
                landingWait.time == null ||
                screenFader == null ||
                screenFader.duration == null ||
                fadeWait == null ||
                fadeWait.time == null ||
                inputReleaseReference == null ||
                inputReleaseReference.Blocker == null)
            {
                LogFailure(
                    fsm,
                    "Act 3 final: one or more required actions/values are missing"
                );
                return;
            }

            if (!string.Equals(
                    hatchReference.sendEvent.Name,
                    "HATCH",
                    StringComparison.Ordinal
                ))
            {
                LogFailure(
                    fsm,
                    "Act 3 final: Walk R trigger no longer sends HATCH"
                );
                return;
            }

            if (!TryPrepareTransition(
                    fsm,
                    "Get Up To Kneel",
                    FinishedEvent,
                    "Fade Out",
                    out FsmTransition getUpFinished,
                    out FsmState fadeOutTarget,
                    out string reason
                ))
            {
                LogFailure(fsm, "Act 3 final: " + reason);
                return;
            }

            FsmStateAction[] weakFallActions = weakFall.Actions;
            FsmStateAction[] enclaveActions = toEnclave.Actions;
            if (weakFallActions == null ||
                enclaveActions == null ||
                enclaveActions.Length < 4)
            {
                LogFailure(
                    fsm,
                    "Act 3 final: action insertion points are unavailable"
                );
                return;
            }

            bool alreadyAddsBlocker =
                FindFirstAction<AddHeroInputBlocker>(weakFall) != null;
            bool alreadyReleasesBlocker =
                FindFirstAction<RemoveHeroInputBlocker>(toEnclave) != null;
            FsmStateAction[] patchedWeakFallActions = weakFallActions;
            FsmStateAction[] patchedEnclaveActions = enclaveActions;

            // Runtime action initialization is the only nontrivial operation
            // in these insertions and can throw on a changed PlayMaker build.
            // Prepare and initialize both replacement arrays before touching
            // any native action, transition or state array.
            if (!alreadyAddsBlocker)
            {
                patchedWeakFallActions = PrepareActionInsertion(
                    weakFall,
                    0,
                    new AddHeroInputBlocker
                    {
                        Blocker = inputBlockerReference.Blocker
                    }
                );
            }

            if (!alreadyReleasesBlocker)
            {
                patchedEnclaveActions = PrepareActionInsertion(
                    toEnclave,
                    4,
                    new RemoveHeroInputBlocker
                    {
                        Blocker = inputReleaseReference.Blocker
                    }
                );
            }

            dormantTrigger.gameObject = hatchReference.gameObject;
            dormantTrigger.trigger = Trigger2DType.OnTriggerStay2D;
            dormantTrigger.sendEvent = hatchReference.sendEvent;
            dormantTrigger.storeCollider = hatchReference.storeCollider;

            if (!alreadyAddsBlocker)
            {
                weakFall.Actions = patchedWeakFallActions;
            }

            landingWait.time.Value = 0.5f;
            screenFader.duration.Value = 0.6f;
            fadeWait.time.Value = 0.6f;
            CommitTransition(getUpFinished, fadeOutTarget);

            if (!alreadyReleasesBlocker)
            {
                toEnclave.Actions = patchedEnclaveActions;
            }

            LogSuccess(
                fsm,
                "Act 3 final weakness bypass with input/fade cleanup"
            );
        }

        private static void TryPatchAct3Hatch(PlayMakerFSM fsm)
        {
            FsmState waitForCall = FindState(fsm, "Wait for Call");
            if (waitForCall == null)
            {
                LogFailure(
                    fsm,
                    "Act 3 hatch: required state 'Wait for Call' is missing"
                );
                return;
            }

            SetFloatValue valueAction =
                FindOnlyAction<SetFloatValue>(waitForCall);
            if (valueAction == null || valueAction.floatValue == null)
            {
                LogFailure(
                    fsm,
                    "Act 3 hatch: expected single SetFloatValue action is missing"
                );
                return;
            }

            valueAction.floatValue.Value = 0.1f;
            LogSuccess(fsm, "Act 3 hatch wake-up delay");
        }

        private static bool TryPrepareTransition(
            PlayMakerFSM fsm,
            string fromStateName,
            string eventName,
            string toStateName,
            out FsmTransition transition,
            out FsmState target,
            out string reason
        )
        {
            transition = null;
            target = FindState(fsm, toStateName);
            if (target == null)
            {
                reason = "required target state '" + toStateName +
                         "' is missing";
                return false;
            }

            FsmState source = FindState(fsm, fromStateName);
            if (source == null)
            {
                reason = "required source state '" + fromStateName +
                         "' is missing";
                return false;
            }

            transition = FindTransition(source, eventName);
            if (transition == null)
            {
                reason = "state '" + fromStateName +
                         "' has no '" + eventName + "' transition";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static FsmState FindState(
            PlayMakerFSM fsm,
            string stateName
        )
        {
            FsmState[] states = fsm.FsmStates;
            if (states == null)
            {
                return null;
            }

            foreach (FsmState state in states)
            {
                if (state != null &&
                    string.Equals(
                        state.Name,
                        stateName,
                        StringComparison.Ordinal
                    ))
                {
                    return state;
                }
            }

            return null;
        }

        private static FsmTransition FindTransition(
            FsmState state,
            string eventName
        )
        {
            FsmTransition[] transitions = state.Transitions;
            if (transitions == null)
            {
                return null;
            }

            foreach (FsmTransition transition in transitions)
            {
                if (transition != null &&
                    string.Equals(
                        transition.EventName,
                        eventName,
                        StringComparison.Ordinal
                    ))
                {
                    return transition;
                }
            }

            return null;
        }

        private static bool HasTransition(
            FsmState state,
            string eventName,
            string toStateName
        )
        {
            FsmTransition transition = FindTransition(state, eventName);
            return transition != null &&
                   string.Equals(
                       transition.ToState,
                       toStateName,
                       StringComparison.Ordinal
                   );
        }

        private static void CommitTransition(
            FsmTransition transition,
            FsmState target
        )
        {
            // Start() has already initialized this FSM, so update both the
            // serialized name and PlayMaker's resolved runtime state cache.
            transition.ToState = target.Name;
            transition.ToFsmState = target;
        }

        private static bool HasTruePlayerDataBoolAction(
            FsmState state,
            string boolName
        )
        {
            FsmStateAction[] actions = state.Actions;
            if (actions == null)
            {
                return false;
            }

            foreach (FsmStateAction action in actions)
            {
                if (action is
                    HutongGames.PlayMaker.Actions.SetPlayerDataBool setBool &&
                    setBool.boolName != null &&
                    setBool.value != null &&
                    string.Equals(
                        setBool.boolName.Value,
                        boolName,
                        StringComparison.Ordinal
                    ) &&
                    setBool.value.Value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasActionType(
            FsmState state,
            string fullTypeName
        )
        {
            FsmStateAction[] actions = state.Actions;
            if (actions == null)
            {
                return false;
            }

            foreach (FsmStateAction action in actions)
            {
                if (action != null &&
                    string.Equals(
                        action.GetType().FullName,
                        fullTypeName,
                        StringComparison.Ordinal
                    ))
                {
                    return true;
                }
            }

            return false;
        }

        private static T FindFirstAction<T>(FsmState state)
            where T : FsmStateAction
        {
            FsmStateAction[] actions = state.Actions;
            if (actions == null)
            {
                return null;
            }

            foreach (FsmStateAction action in actions)
            {
                if (action is T typed)
                {
                    return typed;
                }
            }

            return null;
        }

        private static T FindLastAction<T>(FsmState state)
            where T : FsmStateAction
        {
            FsmStateAction[] actions = state.Actions;
            if (actions == null)
            {
                return null;
            }

            for (int i = actions.Length - 1; i >= 0; i--)
            {
                if (actions[i] is T typed)
                {
                    return typed;
                }
            }

            return null;
        }

        private static T FindOnlyAction<T>(FsmState state)
            where T : FsmStateAction
        {
            FsmStateAction[] actions = state.Actions;
            if (actions == null)
            {
                return null;
            }

            T result = null;
            foreach (FsmStateAction action in actions)
            {
                if (!(action is T typed))
                {
                    continue;
                }

                if (result != null)
                {
                    return null;
                }

                result = typed;
            }

            return result;
        }

        private static void AppendAction(
            FsmState state,
            FsmStateAction action
        )
        {
            InsertAction(state, state.Actions.Length, action);
        }

        private static void InsertAction(
            FsmState state,
            int index,
            FsmStateAction action
        )
        {
            state.Actions = PrepareActionInsertion(
                state,
                index,
                action
            );
        }

        private static FsmStateAction[] PrepareActionInsertion(
            FsmState state,
            int index,
            FsmStateAction action
        )
        {
            FsmStateAction[] oldActions = state.Actions;
            int safeIndex = Math.Max(
                0,
                Math.Min(index, oldActions.Length)
            );
            FsmStateAction[] newActions =
                new FsmStateAction[oldActions.Length + 1];

            Array.Copy(
                oldActions,
                0,
                newActions,
                0,
                safeIndex
            );
            // Harmony reaches this code after PlayMakerFSM.Start has already
            // initialized the state's original actions. Initialize any action
            // inserted at runtime so its State, Fsm and Owner references are
            // valid before the state can enter it.
            action.Init(state);
            newActions[safeIndex] = action;
            Array.Copy(
                oldActions,
                safeIndex,
                newActions,
                safeIndex + 1,
                oldActions.Length - safeIndex
            );
            return newActions;
        }

        private static FsmStateAction[] PrepareActionRemoval(
            FsmStateAction[] oldActions,
            FsmStateAction action
        )
        {
            int removeIndex = Array.IndexOf(oldActions, action);
            if (removeIndex < 0)
            {
                throw new InvalidOperationException(
                    "The validated FSM action was not present in its state."
                );
            }

            FsmStateAction[] newActions =
                new FsmStateAction[oldActions.Length - 1];
            Array.Copy(
                oldActions,
                0,
                newActions,
                0,
                removeIndex
            );
            Array.Copy(
                oldActions,
                removeIndex + 1,
                newActions,
                removeIndex,
                oldActions.Length - removeIndex - 1
            );
            return newActions;
        }

        private static bool IsOneOf(
            string value,
            params string[] candidates
        )
        {
            foreach (string candidate in candidates)
            {
                if (string.Equals(
                        value,
                        candidate,
                        StringComparison.Ordinal
                    ))
                {
                    return true;
                }
            }

            return false;
        }

        private static void LogSuccess(
            PlayMakerFSM fsm,
            string description
        )
        {
            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Disabled forced weakness (" +
                description + ") in " + Describe(fsm) + "."
            );
        }

        private static void LogFailure(
            PlayMakerFSM fsm,
            string reason
        )
        {
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Forced weakness patch skipped for " +
                Describe(fsm) + ": " + reason + "."
            );
        }

        private static string Describe(PlayMakerFSM fsm)
        {
            return "'" + fsm.gameObject.scene.name + "/" + fsm.name +
                   "/" + fsm.FsmName + "'";
        }

        private sealed class DelayedFsmEventFallback : FsmStateAction
        {
            private readonly string eventName;
            private readonly float delay;
            private float elapsed;

            public DelayedFsmEventFallback(
                string eventName,
                float delay
            )
            {
                this.eventName = eventName;
                this.delay = delay;
            }

            public override void OnEnter()
            {
                elapsed = 0f;
            }

            public override void OnUpdate()
            {
                // The native notification UI may pause scaled game time.
                // The softlock fallback continues while that UI is open.
                elapsed += Time.unscaledDeltaTime;
                if (elapsed < delay)
                {
                    return;
                }

                Finish();
                Fsm.Event(eventName);
            }
        }
    }
}
