using HarmonyLib;
using HutongGames.PlayMaker;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TeamCherry.NestedFadeGroup;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Prevents the F4 recovery warp from impersonating Moss Mother's real
    /// defeat. Bonetown's opening-story setup can assert defeatedMossMother
    /// while crossing the tutorial boundary, so the AP check waits for the
    /// boss Control/End state instead.
    /// </summary>
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class MossMotherWarpSafety
    {
        internal const string LocationName = "Boss: Moss Mother";

        private const string OpeningScene = "Tut_03";
        private const string OpeningArena = "Battle Scene";

        private static bool IsSoulRandomized(SaveState state) =>
            !string.IsNullOrEmpty(state?.bossSoulsJson) && state.bossSoulsJson != "[]" &&
            JArray.Parse(state.bossSoulsJson).Values<string>().Contains(LocationName);

        internal static bool BlocksIntroDefeat(string scene, string owner, string fsm,
            string stateName, string flag, bool value, SaveState state) =>
            value && flag == "defeatedMossMother" && fsm == "Control" &&
            stateName == "Set End" && owner == "Churchkeeper Intro Scene" &&
            string.Equals(scene, "Bonetown", StringComparison.OrdinalIgnoreCase) &&
            IsSoulRandomized(state);

        [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.SetPlayerDataBool), "OnEnter")]
        private static class IntroDefeatPatch
        {
            private static bool Prefix(HutongGames.PlayMaker.Actions.SetPlayerDataBool __instance)
            {
                if (__instance.boolName?.Value != "defeatedMossMother" || __instance.value?.Value != true)
                    return true;
                Fsm fsm = __instance.Fsm;
                if (fsm?.GameObject == null || !BlocksIntroDefeat(fsm.GameObject.scene.name,
                    Utils.GetHierarchyPath(fsm.GameObject.transform), fsm.Name, __instance.State?.Name,
                    __instance.boolName.Value, __instance.value.Value, SaveState.Instance)) return true;
                __instance.Finish();
                return false;
            }
        }

        internal static bool RepairSavedDefeat(SaveState state, PlayerData data, bool? arenaCompleted)
        {
            if (data == null || !data.defeatedMossMother || !data.churchKeeperIntro ||
                arenaCompleted != false || !IsSoulRandomized(state) ||
                state.progressionShuffle?.TryGetBossDefeat(LocationName, out bool defeated) != true || defeated)
                return false;
            data.defeatedMossMother = false;
            state.mossMotherBypassedByBoneBottomWarp = true;
            return true;
        }

        internal static void ReconcileSavedDefeat()
        {
            SceneData sceneData = SceneData.instance;
            bool? completed = sceneData != null && sceneData.PersistentBools.TryGetValue(
                OpeningScene, OpeningArena, out PersistentItemData<bool> arena)
                ? arena.Value : (bool?)null;
            if (RepairSavedDefeat(SaveState.Instance, PlayerData.instance, completed))
                RandomizerPlugin.Log?.LogInfo("[RANDOMIZER] Restored the unfinished opening Moss Mother encounter after the tutorial marked it defeated.");
        }

        [HarmonyPatch(typeof(DeactivateIfPlayerdataTrue), "ForceEvaluate")]
        private static class ChapelMaidArrivalPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(DeactivateIfPlayerdataTrue __instance)
            {
                if (SaveState.Instance != null &&
                    __instance.gameObject.scene.name == "Tut_03" &&
                    __instance.gameObject.name == "Churchkeeper Basement" &&
                    __instance.boolName == "soulSnareReady" &&
                    !IsLegitimateDefeat())
                {
                    __instance.gameObject.SetActive(false);
                    return false;
                }
                return true;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(PlayMakerFSM __instance)
        {
            if (__instance == null ||
                !string.Equals(
                    __instance.gameObject.scene.name,
                    "Tut_03",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    __instance.name,
                    "Mossbone Mother",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    __instance.FsmName,
                    "Control",
                    StringComparison.Ordinal
                ))
            {
                return;
            }

            FsmState endState = FindState(__instance, "End");
            if (endState == null || endState.Actions == null)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Moss Mother F4 safety could not find " +
                    "the shipped Control/End state."
                );
                return;
            }

            for (int index = 0; index < endState.Actions.Length; index++)
            {
                if (endState.Actions[index] is ConfirmRealDefeat)
                {
                    return;
                }
            }

            int setPlayerDataIndex = -1;
            for (int index = 0; index < endState.Actions.Length; index++)
            {
                if (endState.Actions[index] is
                    HutongGames.PlayMaker.Actions.SetPlayerDataBool)
                {
                    setPlayerDataIndex = index;
                    break;
                }
            }
            if (setPlayerDataIndex < 0)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Moss Mother F4 safety found Control/End " +
                    "but not its defeatedMossMother action."
                );
                return;
            }

            ConfirmRealDefeat confirmation = new ConfirmRealDefeat();
            confirmation.Init(endState);
            FsmStateAction[] oldActions = endState.Actions;
            FsmStateAction[] patchedActions =
                new FsmStateAction[oldActions.Length + 1];
            Array.Copy(
                oldActions,
                0,
                patchedActions,
                0,
                setPlayerDataIndex
            );
            patchedActions[setPlayerDataIndex] = confirmation;
            Array.Copy(
                oldActions,
                setPlayerDataIndex,
                patchedActions,
                setPlayerDataIndex + 1,
                oldActions.Length - setPlayerDataIndex
            );
            endState.Actions = patchedActions;

            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Moss Mother F4 safety attached to the " +
                "shipped boss completion state."
            );
        }

        internal static bool PrepareForBoneBottomWarp()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                playerData == null ||
                playerData.defeatedMossMother)
            {
                return false;
            }

            state.mossMotherBypassedByBoneBottomWarp = true;
            return true;
        }

        internal static void CancelPreparedWarp()
        {
            SaveState state = SaveState.Instance;
            if (state != null)
            {
                state.mossMotherBypassedByBoneBottomWarp = false;
            }
        }

        internal static void RecoverInterruptedBoneBottomWarp()
        {
            SaveState state = SaveState.Instance;
            if (state == null)
            {
                return;
            }

            // If the game closes during the Bone Bottom warp, the intro can
            // finish before the randomizer saves that the warp was used.
            // Restore the bypass flag so it can finish cleaning up.
            state.mossMotherBypassedByBoneBottomWarp = true;
        }

        internal static void Update()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                playerData == null ||
                !state.mossMotherBypassedByBoneBottomWarp)
            {
                return;
            }

            // Bone Bottom temporarily marks Moss Mother as defeated during
            // its intro.
            if (!playerData.churchKeeperIntro)
            {
                return;
            }

            // A real kill clears the bypass flag before reaching this point.
            playerData.defeatedMossMother = false;
        }

        internal static bool IsLegitimateDefeat()
        {
            SaveState state = SaveState.Instance;
            return PlayerData.instance != null &&
                   PlayerData.instance.defeatedMossMother &&
                   (
                       state == null ||
                       !state.mossMotherBypassedByBoneBottomWarp
                   );
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

        private sealed class ConfirmRealDefeat : FsmStateAction
        {
            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (state != null)
                {
                    state.mossMotherBypassedByBoneBottomWarp = false;
                }
                Finish();
            }
        }
    }

    // The Bone Bottom warp leaves the vine masks up.
    [HarmonyPatch(typeof(HeroController), "SendHeroInPosition")]
    internal static class MossMotherVineMaskPatches
    {
        private const string SceneName = "Tut_03";
        private const string EntryGateName = "top1";
        private const string ControlFsmName = "Control";
        private const string MaskVariableName = "Mask";
        private const string MaskRendererName = "GameObject";
        private const string MaskSpriteName = "msk_generic";
        private const string MaskSortingLayerName = "Over";

        private static readonly string[] MaskPaths =
        {
            "Black Thread States/Normal World/Moss Vine Cluster/Mask",
            "Black Thread States/Normal World/Moss Vine Cluster (1)/Mask",
        };

        [HarmonyPostfix]
        private static void Postfix(HeroController __instance)
        {
            if (!IsExactBypassEntry(__instance))
            {
                return;
            }

            TryHideSkippedVineMasks();
        }

        private static bool IsExactBypassEntry(HeroController hero)
        {
            SaveState state = SaveState.Instance;
            GameManager gameManager = GameManager.instance;
            return hero != null &&
                   hero.gameObject != null &&
                   hero.sceneEntryGate != null &&
                   state != null &&
                   gameManager != null &&
                   state.mossMotherBypassedByBoneBottomWarp &&
                   string.Equals(
                       gameManager.GetSceneNameString(),
                       SceneName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       hero.sceneEntryGate.name,
                       EntryGateName,
                       StringComparison.Ordinal
                   );
        }

        private static void TryHideSkippedVineMasks()
        {
            try
            {
                NestedFadeGroup[] allGroups =
                    Resources.FindObjectsOfTypeAll<NestedFadeGroup>();
                NestedFadeGroup[] targets =
                    new NestedFadeGroup[MaskPaths.Length];

                for (int index = 0; index < MaskPaths.Length; index++)
                {
                    if (!TryResolveExactMask(
                            allGroups,
                            MaskPaths[index],
                            out targets[index]
                        ))
                    {
                        RandomizerPlugin.Log?.LogWarning(
                            "[RANDOMIZER] Moss Mother top1 mask repair " +
                            "found changed Tut_03 vine structure and made " +
                            "no scene changes."
                        );
                        return;
                    }
                }

                bool changed = false;
                foreach (NestedFadeGroup target in targets)
                {
                    changed |= !Mathf.Approximately(target.AlphaSelf, 0f);
                    target.AlphaSelf = 0f;
                }

                if (changed)
                {
                    RandomizerPlugin.Log?.LogInfo(
                        "[RANDOMIZER] Hid Tut_03's two skipped right-vine " +
                        "masks for the Bone Bottom top1 route."
                    );
                }
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Moss Mother top1 mask repair failed " +
                    "closed: " + ex.Message
                );
            }
        }

        private static bool TryResolveExactMask(
            NestedFadeGroup[] groups,
            string expectedPath,
            out NestedFadeGroup target
        )
        {
            target = null;
            int matches = 0;

            foreach (NestedFadeGroup candidate in groups)
            {
                if (candidate == null || candidate.gameObject == null ||
                    !string.Equals(
                        candidate.gameObject.scene.name,
                        SceneName,
                        StringComparison.Ordinal
                    ) ||
                    !string.Equals(
                        Utils.GetHierarchyPath(candidate.transform),
                        expectedPath,
                        StringComparison.Ordinal
                    ))
                {
                    continue;
                }

                matches++;
                target = candidate;
            }

            return matches == 1 && IsExactSerializedMask(target);
        }

        private static bool IsExactSerializedMask(NestedFadeGroup group)
        {
            if (group == null || group.transform.parent == null ||
                !string.Equals(
                    group.gameObject.name,
                    "Mask",
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            PlayMakerFSM control = null;
            int controlMatches = 0;
            foreach (PlayMakerFSM fsm in
                     group.transform.parent.GetComponents<PlayMakerFSM>())
            {
                if (fsm != null &&
                    string.Equals(
                        fsm.FsmName,
                        ControlFsmName,
                        StringComparison.Ordinal
                    ))
                {
                    control = fsm;
                    controlMatches++;
                }
            }

            FsmGameObject maskVariable = controlMatches == 1 &&
                                         control != null &&
                                         control.FsmVariables != null
                ? control.FsmVariables.GetFsmGameObject(MaskVariableName)
                : null;
            Transform rendererTransform =
                group.transform.Find(MaskRendererName);
            SpriteRenderer renderer = rendererTransform == null
                ? null
                : rendererTransform.GetComponent<SpriteRenderer>();

            return maskVariable != null &&
                   maskVariable.Value == group.gameObject &&
                   renderer != null &&
                   renderer.sprite != null &&
                   string.Equals(
                       renderer.sprite.name,
                       MaskSpriteName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       renderer.sortingLayerName,
                       MaskSortingLayerName,
                       StringComparison.Ordinal
                   );
        }

    }
}
