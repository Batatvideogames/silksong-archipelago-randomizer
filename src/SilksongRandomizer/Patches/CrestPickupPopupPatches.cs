using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections;
using UnityEngine;
using SetPlayerDataBoolAction =
    HutongGames.PlayMaker.Actions.SetPlayerDataBool;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Removes the full-screen native crest panel from shuffled crest sources.
    /// The AP item notification is a CollectableUIMsg and is not intercepted.
    /// </summary>
    internal static class CrestPickupPopupPatches
    {
        private const float SourceFadeDuration = 2f;
        private const float SourceWaitDuration = 2.5f;

        private enum CompletionMode
        {
            SourceSequence,
            MemoryStateActions,
        }

        private readonly struct PopupSource
        {
            internal readonly string LocationName;
            internal readonly string CrestInternalName;
            internal readonly string SceneName;
            internal readonly string OwnerName;
            internal readonly string FsmName;
            internal readonly string StateName;
            internal readonly CompletionMode Completion;

            internal PopupSource(
                string locationName,
                string crestInternalName,
                string sceneName,
                string ownerName,
                string fsmName,
                string stateName,
                CompletionMode completion
            )
            {
                LocationName = locationName;
                CrestInternalName = crestInternalName;
                SceneName = sceneName;
                OwnerName = ownerName;
                FsmName = fsmName;
                StateName = stateName;
                Completion = completion;
            }

            internal bool Matches(ShowToolCrestUIMsg action)
            {
                ToolCrest crest =
                    action == null || action.Crest == null
                        ? null
                        : action.Crest.Value as ToolCrest;
                return crest != null &&
                       action.Owner != null &&
                       string.Equals(
                           action.Owner.scene.name,
                           SceneName,
                           StringComparison.Ordinal
                       ) &&
                       string.Equals(
                           action.Owner.name,
                           OwnerName,
                           StringComparison.Ordinal
                       ) &&
                       action.Fsm != null &&
                       string.Equals(
                           action.Fsm.Name,
                           FsmName,
                           StringComparison.Ordinal
                       ) &&
                       action.State != null &&
                       string.Equals(
                           action.State.Name,
                           StateName,
                           StringComparison.Ordinal
                       ) &&
                       string.Equals(
                           crest.name,
                           CrestInternalName,
                           StringComparison.Ordinal
                       );
            }
        }

            // These fallback panels are used after a crest
        // shrine skips its already-viewed memory. Crest Msg is a sequence:
        // ScreenFader finishes, then Wait consumes 2.5 seconds and only then
        // does ShowToolCrestUIMsg enter as its final action.
        private static readonly PopupSource[] SourceSequencePopups =
        {
            new PopupSource(
                "Crest Unlock: Beast",
                "Warrior",
                "Ant_19",
                "Crest Get Shrine",
                "Control",
                "Crest Msg",
                CompletionMode.SourceSequence
            ),
            new PopupSource(
                "Crest Unlock: Reaper",
                "Reaper",
                "Greymoor_20c",
                "Crest Get Shrine",
                "Control",
                "Crest Msg",
                CompletionMode.SourceSequence
            ),
            new PopupSource(
                "Crest Unlock: Wanderer",
                "Wanderer",
                "Chapel_Wanderer",
                "Crest Get Shrine",
                "Control",
                "Crest Msg",
                CompletionMode.SourceSequence
            ),
            new PopupSource(
                "Crest Unlock: Architect",
                "Toolmaster",
                "Under_20",
                "Crest Get Shrine",
                "Control",
                "Crest Msg",
                CompletionMode.SourceSequence
            ),
            new PopupSource(
                "Crest Unlock: Shaman",
                "Spell",
                "Tut_04",
                "Crest Get Shrine",
                "Control",
                "Crest Msg",
                CompletionMode.SourceSequence
            ),
            new PopupSource(
                "Crest Unlock: Shaman",
                "Spell",
                "Tut_05",
                "Crest Get Shrine",
                "Control",
                "Crest Msg",
                CompletionMode.SourceSequence
            ),
        };

        // First-time crest sources all pass through Tut_05's shared memory
        // controller. ShowToolCrestUIMsg is action zero. The following two
        // SetStringValue actions establish the return scene/gate and the last
        // action records the completed memory before FINISHED may be sent.
        private static readonly PopupSource[] MemoryPopups =
        {
            new PopupSource(
                "Crest Unlock: Reaper",
                "Reaper",
                "Tut_05",
                "Memory Control",
                "Memory Control",
                "Reaper Msg",
                CompletionMode.MemoryStateActions
            ),
            new PopupSource(
                "Crest Unlock: Wanderer",
                "Wanderer",
                "Tut_05",
                "Memory Control",
                "Memory Control",
                "Wanderer Msg",
                CompletionMode.MemoryStateActions
            ),
            new PopupSource(
                "Crest Unlock: Beast",
                "Warrior",
                "Tut_05",
                "Memory Control",
                "Memory Control",
                "Beast Msg",
                CompletionMode.MemoryStateActions
            ),
                // Tut_05/Witch Msg is absent. In the FSM,
            // its ShowToolCrestUIMsg action is disabled and points at Cursed,
            // not Witch, so OnEnter cannot be a physical Witch-source popup.
            new PopupSource(
                "Crest Unlock: Shaman",
                "Spell",
                "Tut_05",
                "Memory Control",
                "Memory Control",
                "Shaman Msg",
                CompletionMode.MemoryStateActions
            ),
            new PopupSource(
                "Crest Unlock: Architect",
                "Toolmaster",
                "Tut_05",
                "Memory Control",
                "Memory Control",
                "Toolmaster Msg",
                CompletionMode.MemoryStateActions
            ),
        };

        private static bool IsActiveApLocation(PopupSource source)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsRandomized(ItemType.Crest) &&
                   state.IsLocationEnabled(source.LocationName) &&
                   state.IsLocationInSeed(source.LocationName);
        }

        private static bool TryGetActiveSource(
            ShowToolCrestUIMsg action,
            out PopupSource source
        )
        {
            foreach (PopupSource candidate in SourceSequencePopups)
            {
                if (candidate.Matches(action) &&
                    IsActiveApLocation(candidate) &&
                    HasExactSourceSequenceLifecycle(action))
                {
                    source = candidate;
                    return true;
                }
            }

            foreach (PopupSource candidate in MemoryPopups)
            {
                if (candidate.Matches(action) &&
                    IsActiveApLocation(candidate) &&
                    HasExactMemoryLifecycle(action))
                {
                    source = candidate;
                    return true;
                }
            }

            source = default;
            return false;
        }

        private static bool HasFinishedEvent(ShowToolCrestUIMsg action)
        {
            return action != null &&
                   action.FinishEvent != null &&
                   string.Equals(
                       action.FinishEvent.Name,
                       FsmEvent.Finished.Name,
                       StringComparison.Ordinal
                   );
        }

        private static bool HasEmptyFinishEvent(
            ShowToolCrestUIMsg action
        )
        {
            return action != null &&
                   (action.FinishEvent == null ||
                    string.IsNullOrEmpty(action.FinishEvent.Name));
        }

        private static bool HasExactSourceSequenceLifecycle(
            ShowToolCrestUIMsg action
        )
        {
            FsmStateAction[] actions = action?.State?.Actions;
            if (actions == null ||
                !action.State.IsSequence ||
                actions.Length != 3 ||
                !(actions[0] is ScreenFader screenFader) ||
                !(actions[1] is HutongGames.PlayMaker.Actions.Wait wait) ||
                !ReferenceEquals(actions[2], action) ||
                !HasEmptyFinishEvent(action))
            {
                return false;
            }

            return Mathf.Approximately(
                       screenFader.duration.Value,
                       SourceFadeDuration
                   ) &&
                   !wait.realTime &&
                   Mathf.Approximately(
                       wait.time.Value,
                       SourceWaitDuration
                   );
        }

        private static bool HasExactMemoryLifecycle(
            ShowToolCrestUIMsg action
        )
        {
            FsmStateAction[] actions = action?.State?.Actions;
            return actions != null &&
                   !action.State.IsSequence &&
                   actions.Length == 4 &&
                   ReferenceEquals(actions[0], action) &&
                   actions[1] is SetStringValue &&
                   actions[2] is SetStringValue &&
                   actions[3] is SetPlayerDataBoolAction &&
                   HasFinishedEvent(action);
        }

        private static bool IsStillInSourceState(
            ShowToolCrestUIMsg action,
            PopupSource source
        )
        {
            return source.Matches(action) &&
                   action.Fsm != null &&
                   string.Equals(
                       action.Fsm.ActiveStateName,
                       source.StateName,
                       StringComparison.Ordinal
                   );
        }

        private static void CompleteNativeCallback(
            ShowToolCrestUIMsg action
        )
        {
            // This is ShowToolCrestUIMsg.OnCrestMsgEnd's exact order. The
            // event advances the owning FSM. Finish then retires the action.
            action.Fsm.Event(action.FinishEvent);
            action.Finish();
        }

        private static IEnumerator CompleteAfterMemoryStateActions(
            ShowToolCrestUIMsg action,
            PopupSource source
        )
        {
            // PlayMaker enters actions in array order. Yield one frame so the
            // two return-target writes and completed-memory flag following
            // ShowToolCrestUIMsg all execute before FINISHED enters End Scene.
            yield return null;
            if (IsStillInSourceState(action, source))
            {
                CompleteNativeCallback(action);
            }
        }

        [HarmonyPatch(
            typeof(ShowToolCrestUIMsg),
            nameof(ShowToolCrestUIMsg.OnEnter))]
        internal static class RandomizedCrestGetMessagePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ShowToolCrestUIMsg __instance)
            {
                if (!TryGetActiveSource(
                        __instance,
                        out PopupSource source
                    ))
                {
                    return true;
                }

                if (source.Completion == CompletionMode.SourceSequence)
                {
                    // This action enters only after the sequential fader and
                    // 2.5-second Wait have completed. Its serialized
                    // FinishEvent is empty. Finish lets PlayMaker emit the
                    // state's normal system FINISHED transition exactly as
                    // the native popup callback would.
                    __instance.Finish();
                    return false;
                }

                RandomizerPlugin plugin = RandomizerPlugin.Instance;
                if (plugin == null)
                {
                    return true;
                }

                plugin.StartCoroutine(
                    CompleteAfterMemoryStateActions(__instance, source)
                );
                return false;
            }
        }
    }
}
