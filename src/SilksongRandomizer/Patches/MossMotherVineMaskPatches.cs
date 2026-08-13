using HarmonyLib;
using HutongGames.PlayMaker;
using System;
using TeamCherry.NestedFadeGroup;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Reproduces the visual result of cutting Tut_03's two right-side vines
    /// when the quarantined Bone Bottom route enters from top1. The native
    /// vine objects, colliders, persistence and Control FSMs remain intact.
    /// </summary>
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
                   !state.IsLocationChecked(
                       MossMotherWarpSafety.LocationName
                   ) &&
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
