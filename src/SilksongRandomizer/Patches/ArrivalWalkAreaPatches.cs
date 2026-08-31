using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Suppresses only the first-arrival walk zones which hold Hornet at
    /// walking speed while the Bone Bottom and Bellhart titles are displayed.
    /// The title presentation, story state, colliders and all other walk
    /// zones remain native.
    /// </summary>
    [HarmonyPatch]
    internal static class ArrivalWalkAreaPatches
    {
        private const string BoneBottomScene = "Bonetown";
        private const string BoneBottomArrivalPath = "Walk Area Init";
        private const string BellhartScene = "Belltown";
        private const string BellhartCutsceneArrivalPath =
            "Cutscene States/Cutscene/Walk Area";
        private const string BellhartNoCutsceneArrivalPath =
            "Cutscene States/No Cutscene/Walk Area";
        private const string BoneBottomTitlePath =
            "Black Thread States/Normal World/Area Title Controller";
        private const string BellhartTitlePath =
            "Cutscene States/No Cutscene/Area Title Controller";
        private const string BellhartDoorOneTitlePath =
            "Cutscene States/No Cutscene/Area Title Controller (1)";
        private const string AreaTitleFsmName = "Area Title Control";
        private const string FleursUpState = "Fleurs Up";
        private const string FleursUpCityState = "Fleurs Up 2";
        private const string BigTitleEndState = "Big Title End";
        private const string BigTitleEndCityState = "Big Title End 2";
        private const string DestroyTitleState = "Destroy";

        // The Bellhart visited bool is written while its first title is still
        // playing. Capture the selected WalkArea in Awake, before any title
        // controller Start can begin that write and retain the decision for
        // the whole activation.
        private static readonly Dictionary<int, string>
            SuppressedWalkAreas = new Dictionary<int, string>();

        // The native controller and shared title FSM both write the global
        // interaction flag. Track only an exact first-arrival title paired
        // with one of the captured walk zones above.
        private static string ActiveArrivalTitleScene;
        private static int ArrivalTitleWriteDepth;

        [HarmonyPatch(typeof(WalkArea), "Awake")]
        [HarmonyPostfix]
        private static void AwakePostfix(WalkArea __instance)
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                !state.IsRoomBound ||
                __instance == null ||
                !ShouldSuppressFirstArrival(
                    __instance.gameObject.scene.name,
                    Utils.GetHierarchyPath(__instance.transform),
                    PlayerData.instance
                ))
            {
                return;
            }

            int instanceId = __instance.GetInstanceID();
            if (!SuppressedWalkAreas.ContainsKey(instanceId))
            {
                SuppressedWalkAreas.Add(
                    instanceId,
                    __instance.gameObject.scene.name
                );
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] Suppressing arrival slow-walk callbacks for '" +
                    __instance.gameObject.scene.name + "/" +
                    Utils.GetHierarchyPath(__instance.transform) + "'."
                );
            }
        }

        [HarmonyPatch(typeof(WalkArea), "OnTriggerEnter2D")]
        [HarmonyPrefix]
        private static bool OnTriggerEnter2DPrefix(WalkArea __instance)
        {
            return ShouldRunNativeTrigger(__instance);
        }

        [HarmonyPatch(typeof(WalkArea), "OnTriggerStay2D")]
        [HarmonyPrefix]
        private static bool OnTriggerStay2DPrefix(WalkArea __instance)
        {
            return ShouldRunNativeTrigger(__instance);
        }

        // Native OnTriggerExit2D remains intact so it can clear any walk state.
        // Once an arrival branch disables, later activations are ordinary
        // revisits and must retain the native WalkArea behaviour.
        [HarmonyPatch(typeof(WalkArea), "OnDisable")]
        [HarmonyPostfix]
        private static void OnDisablePostfix(WalkArea __instance)
        {
            if (__instance != null)
            {
                SuppressedWalkAreas.Remove(__instance.GetInstanceID());
            }
        }

        internal static bool ShouldRunNativeTrigger(WalkArea walkArea)
        {
            return walkArea == null ||
                   !SuppressedWalkAreas.ContainsKey(
                       walkArea.GetInstanceID()
                   );
        }

        private static bool HasSuppressedWalkArea(string sceneName)
        {
            foreach (string suppressedScene in SuppressedWalkAreas.Values)
            {
                if (string.Equals(
                        suppressedScene,
                        sceneName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldTrackArrivalTitle(
            AreaTitleController controller
        )
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                !state.IsRoomBound ||
                controller == null ||
                playerData == null)
            {
                return false;
            }

            string sceneName = controller.gameObject.scene.name;
            string hierarchyPath = Utils.GetHierarchyPath(
                controller.transform
            );
            if (!HasSuppressedWalkArea(sceneName))
            {
                return false;
            }

            if (string.Equals(
                    sceneName,
                    BoneBottomScene,
                    StringComparison.Ordinal))
            {
                return !playerData.visitedBoneBottom &&
                       string.Equals(
                           hierarchyPath,
                           BoneBottomTitlePath,
                           StringComparison.Ordinal
                       );
            }

            if (!string.Equals(
                    sceneName,
                    BellhartScene,
                    StringComparison.Ordinal) ||
                (
                    !string.Equals(
                        hierarchyPath,
                        BellhartTitlePath,
                        StringComparison.Ordinal
                    ) &&
                    !string.Equals(
                        hierarchyPath,
                        BellhartDoorOneTitlePath,
                        StringComparison.Ordinal
                    )
                ))
            {
                return false;
            }

            return playerData.spinnerDefeated
                ? !playerData.visitedBellhartSaved
                : !playerData.visitedBellhartHaunted;
        }

        private static bool IsActiveArrivalTitle()
        {
            if (string.IsNullOrEmpty(ActiveArrivalTitleScene))
            {
                return false;
            }

            SaveState state = SaveState.Instance;
            GameManager gameManager = GameManager.instance;
            if (state == null ||
                !state.IsRoomBound ||
                gameManager == null ||
                !string.Equals(
                    ActiveArrivalTitleScene,
                    gameManager.sceneName,
                    StringComparison.Ordinal
                ))
            {
                ClearActiveArrivalTitle();
                return false;
            }

            return true;
        }

        private static void ClearActiveArrivalTitle()
        {
            ActiveArrivalTitleScene = null;
        }

        private static bool IsSharedAreaTitleAction(
            ActivateInteraction action
        )
        {
            AreaTitle areaTitle = ManagerSingleton<AreaTitle>.Instance;
            return action != null &&
                   areaTitle != null &&
                   action.Owner == areaTitle.gameObject &&
                   action.Fsm != null &&
                   string.Equals(
                       action.Fsm.Name,
                       AreaTitleFsmName,
                       StringComparison.Ordinal
                   ) &&
                   action.State != null;
        }

        private static IEnumerator CaptureArrivalTitleWrites(
            IEnumerator nativeEnumerator
        )
        {
            while (true)
            {
                bool hasNext;
                ArrivalTitleWriteDepth++;
                try
                {
                    hasNext = nativeEnumerator.MoveNext();
                }
                finally
                {
                    ArrivalTitleWriteDepth--;
                }

                if (!hasNext)
                {
                    AreaTitle areaTitle =
                        ManagerSingleton<AreaTitle>.Instance;
                    if (areaTitle == null ||
                        !areaTitle.gameObject.activeInHierarchy ||
                        FSMUtility.GetFSM(areaTitle.gameObject) == null)
                    {
                        // Fail closed if the shared title was unavailable or
                        // ended synchronously before this coroutine returned.
                        ClearActiveArrivalTitle();
                    }

                    yield break;
                }

                yield return nativeEnumerator.Current;
            }
        }

        internal static bool ShouldSuppressFirstArrival(
            string sceneName,
            string hierarchyPath,
            PlayerData playerData
        )
        {
            if (string.IsNullOrEmpty(sceneName) ||
                string.IsNullOrEmpty(hierarchyPath))
            {
                return false;
            }

            if (string.Equals(
                    sceneName,
                    BoneBottomScene,
                    StringComparison.Ordinal))
            {
                return string.Equals(
                           hierarchyPath,
                           BoneBottomArrivalPath,
                           StringComparison.Ordinal
                       );
            }

            if (!string.Equals(
                    sceneName,
                    BellhartScene,
                    StringComparison.Ordinal))
            {
                return false;
            }

            // This branch is selected only while the native Bellhart
            // cutscene has not been seen, so the exact WalkArea instance is
            // intrinsically a first-arrival zone.
            if (string.Equals(
                    hierarchyPath,
                    BellhartCutsceneArrivalPath,
                    StringComparison.Ordinal))
            {
                return true;
            }

            // No Cutscene is reused on later visits. Awake runs as the native
            // selector activates this branch, before its sibling title
            // controllers can write either visited flag.
            if (!string.Equals(
                    hierarchyPath,
                    BellhartNoCutsceneArrivalPath,
                    StringComparison.Ordinal) ||
                playerData == null)
            {
                return false;
            }

            return playerData.spinnerDefeated
                ? !playerData.visitedBellhartSaved
                : !playerData.visitedBellhartHaunted;
        }

        [HarmonyPatch(typeof(AreaTitleController), "UnvisitPause")]
        private static class ArrivalTitleCoroutinePatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                AreaTitleController __instance,
                ref IEnumerator __result
            )
            {
                if (__result == null ||
                    !ShouldTrackArrivalTitle(__instance))
                {
                    return;
                }

                ActiveArrivalTitleScene =
                    __instance.gameObject.scene.name;
                __result = CaptureArrivalTitleWrites(__result);
            }
        }

        [HarmonyPatch(typeof(InteractManager), "set_IsDisabled")]
        private static class ArrivalTitleInteractionSetterPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(bool value)
            {
                return !value ||
                       ArrivalTitleWriteDepth <= 0 ||
                       !IsActiveArrivalTitle();
            }
        }

        [HarmonyPatch(
            typeof(ActivateInteraction),
            nameof(ActivateInteraction.OnEnter)
        )]
        private static class ArrivalTitleInteractionActionPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ActivateInteraction __instance)
            {
                if (!IsActiveArrivalTitle() ||
                    !IsSharedAreaTitleAction(__instance) ||
                    __instance.Activate == null ||
                    __instance.Activate.Value ||
                    (
                        !string.Equals(
                            __instance.State.Name,
                            FleursUpState,
                            StringComparison.Ordinal
                        ) &&
                        !string.Equals(
                            __instance.State.Name,
                            FleursUpCityState,
                            StringComparison.Ordinal
                        )
                    ))
                {
                    return true;
                }

                // Complete only the shared title FSM's own disable action.
                // Its wait, animation and FINISHED transition remain native.
                __instance.Finish();
                return false;
            }

            [HarmonyPostfix]
            private static void Postfix(ActivateInteraction __instance)
            {
                if (!IsActiveArrivalTitle() ||
                    !IsSharedAreaTitleAction(__instance) ||
                    __instance.Activate == null ||
                    !__instance.Activate.Value)
                {
                    return;
                }

                string stateName = __instance.State.Name;
                if (string.Equals(
                        stateName,
                        BigTitleEndState,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        stateName,
                        BigTitleEndCityState,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        stateName,
                        DestroyTitleState,
                        StringComparison.Ordinal))
                {
                    ClearActiveArrivalTitle();
                }
            }
        }

        [HarmonyPatch(typeof(InteractManager), "ResetState")]
        private static class ArrivalTitleSceneResetPatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                ClearActiveArrivalTitle();
            }
        }
    }
}
