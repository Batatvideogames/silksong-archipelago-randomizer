using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongRandomizer.Patches
{
    internal static class StaticMapPatches
    {
        private const string CradleFallbackSceneName = "Tube_Hub";
        private const string CradleFallbackPath =
            "Black Thread States/Black Thread World/" +
            "Collectable Item Pickup";
        private const string BlackThreadWorldName = "Black Thread World";

        private enum Resolution
        {
            PassThrough,
            Intercept,
            Block,
        }

        private static readonly FieldInfo LinkedPlayerDataBoolField =
            AccessTools.Field(
                typeof(PlayerDataCollectable),
                "linkedPDBool"
            );

        private static readonly FieldInfo IsMapField =
            AccessTools.Field(typeof(PlayerDataCollectable), "isMap");

        private static readonly FieldInfo DeactivateBoolNameField =
            AccessTools.Field(
                typeof(DeactivateIfPlayerdataTrue),
                "boolName"
            );

        private static readonly FieldInfo ObjectToDeactivateField =
            AccessTools.Field(
                typeof(DeactivateIfPlayerdataTrue),
                "objectToDeactivate"
            );

        private static readonly HashSet<string> ReportedBlockingErrors =
            new HashSet<string>(StringComparer.Ordinal);

        [ThreadStatic]
        private static DeactivateIfPlayerdataTrue cradleEvaluationInstance;

        private static Resolution Resolve(
            PlayerDataCollectable collectable,
            out SaveState state,
            out StaticMapManifest.Entry entry
        )
        {
            state = SaveState.Instance;
            entry = null;

            if (state == null ||
                !state.IsRandomized(ItemType.Map) ||
                collectable == null ||
                !StaticMapManifest.TryGetByAssetName(
                    collectable.name,
                    out entry
                ))
            {
                return Resolution.PassThrough;
            }

            if (!state.IsLocationEnabled(entry.LocationName) ||
                !state.IsLocationInSeed(entry.LocationName))
            {
                return Resolution.PassThrough;
            }

            if (LinkedPlayerDataBoolField == null || IsMapField == null)
            {
                ReportBlockingError(
                    entry,
                    "PlayerDataCollectable's required private fields " +
                    "could not be resolved."
                );
                return Resolution.Block;
            }

            try
            {
                string linkedPlayerDataBool =
                    LinkedPlayerDataBoolField.GetValue(collectable) as string;
                bool isMap = Convert.ToBoolean(
                    IsMapField.GetValue(collectable)
                );

                if (!isMap || !string.Equals(
                        linkedPlayerDataBool,
                        entry.PlayerDataBool,
                        StringComparison.Ordinal
                    ))
                {
                    ReportBlockingError(
                        entry,
                        "Asset metadata changed: expected isMap=true and " +
                        "linkedPDBool='" + entry.PlayerDataBool +
                        "', found isMap=" + isMap + " and linkedPDBool='" +
                        (linkedPlayerDataBool ?? "<null>") + "'."
                    );
                    return Resolution.Block;
                }
            }
            catch (Exception exception)
            {
                ReportBlockingError(
                    entry,
                    "Asset metadata could not be read: " +
                    exception.Message
                );
                return Resolution.Block;
            }

            string sceneName = SceneManager.GetActiveScene().name;
            if (!entry.HasSourceScene(sceneName))
            {
                ReportBlockingError(
                    entry,
                    "The asset was invoked from unexpected scene '" +
                    (sceneName ?? "<null>") + "'."
                );
                return Resolution.Block;
            }

            return Resolution.Intercept;
        }

        private static void ReportBlockingError(
            StaticMapManifest.Entry entry,
            string detail
        )
        {
            string message =
                "Could not safely randomize '" +
                (entry?.LocationName ?? "unknown static map") +
                "'. No vanilla map was granted. " + detail;

            lock (ReportedBlockingErrors)
            {
                if (!ReportedBlockingErrors.Add(message))
                {
                    return;
                }
            }

            if (RandomizerPlugin.Instance != null)
            {
                RandomizerPlugin.Instance.ReportBlockingError(message);
            }
            else
            {
                Debug.LogError("[RANDOMIZER] " + message);
            }
        }

        [HarmonyPatch(
            typeof(PlayerDataCollectable),
            nameof(PlayerDataCollectable.Get),
            new[] { typeof(bool) }
        )]
        internal static class PlayerDataCollectableGetPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(PlayerDataCollectable __instance)
            {
                Resolution resolution = Resolve(
                    __instance,
                    out SaveState state,
                    out StaticMapManifest.Entry entry
                );
                if (resolution == Resolution.PassThrough)
                {
                    return true;
                }

                if (resolution == Resolution.Intercept &&
                    !state.IsLocationChecked(entry.LocationName))
                {
                    state.CheckLocation(entry.LocationName);
                }

                return false;
            }
        }

        [HarmonyPatch(
            typeof(PlayerDataCollectable),
            nameof(PlayerDataCollectable.CanGetMore)
        )]
        internal static class PlayerDataCollectableCanGetMorePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                PlayerDataCollectable __instance,
                ref bool __result
            )
            {
                Resolution resolution = Resolve(
                    __instance,
                    out SaveState state,
                    out StaticMapManifest.Entry entry
                );
                if (resolution == Resolution.PassThrough)
                {
                    return true;
                }

                __result = resolution == Resolution.Intercept &&
                    !state.IsLocationChecked(entry.LocationName);
                return false;
            }
        }

        [HarmonyPatch(
            typeof(DeactivateIfPlayerdataTrue),
            "ForceEvaluate"
        )]
        internal static class CradleFallbackVisibilityPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                DeactivateIfPlayerdataTrue __instance
            )
            {
                if (__instance != null && ReferenceEquals(
                        cradleEvaluationInstance,
                        __instance
                    ))
                {
                    return false;
                }

                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.Map) ||
                    !state.IsLocationEnabled(
                        StaticMapManifest.CradleLocationName
                    ) ||
                    !state.IsLocationInSeed(
                        StaticMapManifest.CradleLocationName
                    ) ||
                    __instance == null ||
                    __instance.gameObject == null ||
                    !string.Equals(
                        __instance.gameObject.scene.name,
                        CradleFallbackSceneName,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    return true;
                }

                string sourcePath = Utils.GetHierarchyPath(__instance.transform);
                if (!string.Equals(
                        sourcePath,
                        CradleFallbackPath,
                        StringComparison.Ordinal
                    ) ||
                    !HasAncestorNamed(
                        __instance.transform,
                        BlackThreadWorldName
                    ))
                {
                    return true;
                }

                StaticMapManifest.TryGetByLocationName(
                    StaticMapManifest.CradleLocationName,
                    out StaticMapManifest.Entry entry
                );

                if (!TryGetActThreeFallbackSource(
                        entry,
                        out StaticMapManifest.Source fallbackSource
                    ) ||
                    !string.Equals(
                        fallbackSource.SceneName,
                        CradleFallbackSceneName,
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    !string.Equals(
                        fallbackSource.HierarchyPath,
                        CradleFallbackPath,
                        StringComparison.Ordinal
                    ))
                {
                    ReportBlockingError(
                        entry,
                        "The Cradle Act 3 fallback manifest no longer " +
                        "matches its Tube_Hub source."
                    );
                    return false;
                }

                if (DeactivateBoolNameField == null ||
                    ObjectToDeactivateField == null)
                {
                    ReportBlockingError(
                        entry,
                        "The Cradle fallback's visibility fields could " +
                        "not be resolved."
                    );
                    return false;
                }

                try
                {
                    string boolName =
                        DeactivateBoolNameField.GetValue(__instance) as string;
                    GameObject configuredTarget =
                        ObjectToDeactivateField.GetValue(__instance) as
                            GameObject;
                    GameObject effectiveTarget = configuredTarget != null
                        ? configuredTarget
                        : __instance.gameObject;

                    if (!string.Equals(
                            boolName,
                            entry.PlayerDataBool,
                            StringComparison.Ordinal
                        ))
                    {
                        ReportBlockingError(
                            entry,
                            "The Cradle fallback no longer uses " +
                            "HasCradleMap."
                        );
                        return false;
                    }

                    if (effectiveTarget != __instance.gameObject ||
                        !string.Equals(
                            effectiveTarget.scene.name,
                            fallbackSource.SceneName,
                            StringComparison.OrdinalIgnoreCase
                        ) ||
                        !string.Equals(
                            Utils.GetHierarchyPath(effectiveTarget.transform),
                            fallbackSource.HierarchyPath,
                            StringComparison.Ordinal
                        ) ||
                        !HasAncestorNamed(
                            effectiveTarget.transform,
                            BlackThreadWorldName
                        ))
                    {
                        ReportBlockingError(
                            entry,
                            "The Cradle Act 3 fallback no longer matches " +
                            "its HasCradleMap target."
                        );
                        return false;
                    }

                    bool shouldBeActive = !state.IsLocationChecked(
                        entry.LocationName
                    );
                    if (effectiveTarget.activeSelf != shouldBeActive)
                    {
                        DeactivateIfPlayerdataTrue previousEvaluation =
                            cradleEvaluationInstance;
                        cradleEvaluationInstance = __instance;
                        try
                        {
                            effectiveTarget.SetActive(shouldBeActive);
                        }
                        finally
                        {
                            cradleEvaluationInstance = previousEvaluation;
                        }
                    }
                }
                catch (Exception exception)
                {
                    ReportBlockingError(
                        entry,
                        "The Cradle Act 3 fallback could not be evaluated: " +
                        exception.Message
                    );
                }

                // The AP check state, not ownership of the received Cradle
                // map item, controls whether this alternate source remains.
                return false;
            }
        }

        private static bool TryGetActThreeFallbackSource(
            StaticMapManifest.Entry entry,
            out StaticMapManifest.Source fallbackSource
        )
        {
            fallbackSource = null;
            if (entry == null)
            {
                return false;
            }

            foreach (StaticMapManifest.Source source in entry.Sources)
            {
                if (source == null || !source.IsActThreeFallback)
                {
                    continue;
                }

                if (fallbackSource != null)
                {
                    return false;
                }

                fallbackSource = source;
            }

            return fallbackSource != null;
        }

        private static bool HasAncestorNamed(
            Transform transform,
            string expectedName
        )
        {
            for (Transform current = transform;
                 current != null;
                 current = current.parent)
            {
                if (string.Equals(
                        current.name,
                        expectedName,
                        StringComparison.Ordinal
                    ))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
