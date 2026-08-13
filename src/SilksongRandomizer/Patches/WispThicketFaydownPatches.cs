using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Mirrors Greymoor's native Faydown route gate for randomized ownership.
    /// The native component activates a solid blocker while hasDoubleJump is
    /// false. Randomized Faydown ownership removes only that blocker. The
    /// native PlayerData flag remains untouched so the physical Mount Fay
    /// source stays an independent AP location.
    /// </summary>
    [HarmonyPatch(typeof(ActivateIfPlayerdataFalse), "Start")]
    internal static class WispThicketFaydownPatches
    {
        private const string SourceScene = "Greymoor_06";
        private const string SourcePath = "wisp temporary block";
        private const string SourceBoolName = "hasDoubleJump";
        private const string TargetPath =
            "wisp temporary block/terrain collider temp blocker";

        private static readonly FieldInfo BoolNameField =
            AccessTools.Field(
                typeof(ActivateIfPlayerdataFalse),
                "boolName"
            );
        private static readonly FieldInfo ObjectToActivateField =
            AccessTools.Field(
                typeof(ActivateIfPlayerdataFalse),
                "objectToActivate"
            );

        [HarmonyPostfix]
        private static void Postfix(ActivateIfPlayerdataFalse __instance)
        {
            TryRemoveRandomizedFaydownBlocker(__instance);
        }

        internal static bool SynchronizeActiveScene()
        {
            SaveState state = SaveState.Instance;
            if (state == null || !state.canDoubleJump)
            {
                return false;
            }

            bool synchronized = false;
            foreach (ActivateIfPlayerdataFalse source in
                     Resources.FindObjectsOfTypeAll<
                         ActivateIfPlayerdataFalse>())
            {
                synchronized |= TryRemoveRandomizedFaydownBlocker(source);
            }

            return synchronized;
        }

        private static bool TryRemoveRandomizedFaydownBlocker(
            ActivateIfPlayerdataFalse source
        )
        {
            SaveState state = SaveState.Instance;
            if (state == null || !state.canDoubleJump ||
                !IsExactSource(source) ||
                BoolNameField == null ||
                ObjectToActivateField == null)
            {
                return false;
            }

            try
            {
                string boolName = BoolNameField.GetValue(source) as string;
                GameObject target =
                    ObjectToActivateField.GetValue(source) as GameObject;
                if (!string.Equals(
                        boolName,
                        SourceBoolName,
                        StringComparison.Ordinal
                    ) ||
                    !IsExactTarget(source.gameObject, target))
                {
                    return false;
                }

                if (target.activeSelf)
                {
                    target.SetActive(false);
                    RandomizerPlugin.Log?.LogInfo(
                        "[RANDOMIZER] Removed the Greymoor Wisp Thicket " +
                        "Faydown blocker for randomized cloak ownership."
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                // A changed native component layout must not grant unrelated
                // scene objects or alter the physical skill's PlayerData flag.
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Greymoor Faydown route synchronization " +
                    "failed closed: " + ex.Message
                );
                return false;
            }
        }

        private static bool IsExactSource(
            ActivateIfPlayerdataFalse source
        )
        {
            return source != null &&
                   source.gameObject != null &&
                   source.gameObject.activeInHierarchy &&
                   string.Equals(
                       source.gameObject.scene.name,
                       SourceScene,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       Utils.GetHierarchyPath(source.transform),
                       SourcePath,
                       StringComparison.Ordinal
                   );
        }

        private static bool IsExactTarget(
            GameObject source,
            GameObject target
        )
        {
            if (source == null || target == null ||
                !string.Equals(
                    target.scene.name,
                    SourceScene,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    source.scene.name,
                    target.scene.name,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    Utils.GetHierarchyPath(target.transform),
                    TargetPath,
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            return true;
        }

    }
}
