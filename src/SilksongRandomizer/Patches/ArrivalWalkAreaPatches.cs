using System;
using System.Collections.Generic;
using HarmonyLib;
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

        // The Bellhart visited bool is written while its first title is still
        // playing. Capture the selected WalkArea in Awake, before any title
        // controller Start can begin that write and retain the decision for
        // the whole activation.
        private static readonly HashSet<int> SuppressedWalkAreas =
            new HashSet<int>();

        [HarmonyPatch(typeof(WalkArea), "Awake")]
        [HarmonyPostfix]
        private static void AwakePostfix(WalkArea __instance)
        {
            if (__instance == null ||
                !ShouldSuppressFirstArrival(
                    __instance.gameObject.scene.name,
                    Utils.GetHierarchyPath(__instance.transform),
                    PlayerData.instance
                ))
            {
                return;
            }

            if (SuppressedWalkAreas.Add(__instance.GetInstanceID()))
            {
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
                   !SuppressedWalkAreas.Contains(walkArea.GetInstanceID());
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

    }
}
