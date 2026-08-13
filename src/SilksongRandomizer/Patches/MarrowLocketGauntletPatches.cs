using System;
using HarmonyLib;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Lets the normal-world Marrow locket gauntlet evaluate randomized
    /// Cling Grip ownership without granting the vanilla wall-jump flag.
    /// The Act 3 battle is a separate hierarchy and has no matching gate.
    /// </summary>
    internal static class MarrowLocketGauntletPatches
    {
        private const string SourceScene = "Bone_18";
        private const string QuestSceneObject = "Quest Scene";
        private const string FinalEncounterObject = "Final Encounter";
        private const string BattleSceneObject = "Battle Scene";
        private const string WallJumpBool = "hasWalljump";

        private sealed class WallJumpSnapshot
        {
            internal PlayerData PlayerData;
            internal bool NativeWallJump;
        }

        [HarmonyPatch(
            typeof(DeactivateIfPlayerdataFalse),
            nameof(DeactivateIfPlayerdataFalse.ForceEvaluate)
        )]
        private static class NormalWorldActivationPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                DeactivateIfPlayerdataFalse __instance,
                out WallJumpSnapshot __state)
            {
                __state = null;

                SaveState state = SaveState.Instance;
                GameManager gameManager = GameManager.instance;
                PlayerData playerData = gameManager == null
                    ? null
                    : gameManager.playerData;
                if (state == null ||
                    playerData == null ||
                    !state.IsRandomized(ItemType.Skill) ||
                    !IsExactNormalWorldGate(__instance))
                {
                    return;
                }

                __state = new WallJumpSnapshot
                {
                    PlayerData = playerData,
                    NativeWallJump = playerData.hasWalljump,
                };
                playerData.hasWalljump = state.canWallJump;
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static Exception Finalizer(
                Exception __exception,
                WallJumpSnapshot __state)
            {
                if (__state != null && __state.PlayerData != null)
                {
                    __state.PlayerData.hasWalljump =
                        __state.NativeWallJump;
                }

                return __exception;
            }
        }

        private static bool IsExactNormalWorldGate(
            DeactivateIfPlayerdataFalse source)
        {
            if (source == null ||
                source.gameObject == null ||
                !string.Equals(
                    source.gameObject.scene.name,
                    SourceScene,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    source.boolName,
                    WallJumpBool,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    source.gameObject.name,
                    BattleSceneObject,
                    StringComparison.Ordinal))
            {
                return false;
            }

            Transform finalEncounter = source.transform.parent;
            Transform questScene = finalEncounter == null
                ? null
                : finalEncounter.parent;
            return finalEncounter != null &&
                   questScene != null &&
                   questScene.parent == null &&
                   string.Equals(
                       finalEncounter.name,
                       FinalEncounterObject,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       questScene.name,
                       QuestSceneObject,
                       StringComparison.Ordinal);
        }
    }
}
