using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Turns listed breakable currency caches into ordinary AP checks.
    /// Ordinary caches use the same pickup proxy
    /// used by the direct minor-pickup manifest. Hanging rosary strings retain
    /// their native hit choreography because their source transform is a
    /// ceiling anchor rather than an accessible pickup position.
    /// </summary>
    internal static class MinorCachePatches
    {
        private const float MatchTolerance = 0.75f;
        private const string HangingGlowName = "AP Check Shimmer";
        private const string StationaryHunterNecklaceLocation =
            "Rosary Necklace: Hunter's March";
        private const string StationaryFarFieldsNecklaceLocation =
            "Pale Rosary Necklace: Far Fields";
        private static readonly HashSet<string> DirectBreakCheckLocations =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Rosary Cache: Far Fields #19",
                "Shell Shard Cache: Bilewater #1",
                "Shell Shard Cache: Bilewater #2",
                "Shell Shard Cache: Mount Fay #5",
                "Shell Shard Cache: Mount Fay #6",
                "Shell Shard Cache: Mount Fay #7",
                "Shell Shard Cache: Putrified Ducts #6",
                "Shell Shard Cache: Putrified Ducts #8",
                "Shell Shard Cache: Putrified Ducts #9",
                "Shell Shard Cache: Putrified Ducts #10",
                "Shell Shard Cache: Putrified Ducts #11",
                "Shell Shard Cache: Sinner's Road #6",
                "Shell Shard Cache: Sinner's Road #7",
                "Shell Shard Cache: The Slab #4",
                "Shell Shard Cache: The Slab #5",
                "Shell Shard Cache: Underworks #3",
            };
        private static bool loggedReplacementFailure;
        private static bool loggedHangingGlowFailure;

        // The native ant_item_string root is carried by AntRegion. Its AP
        // replacement must stay at the starting coordinate, so give
        // only this proxy an explicit veto while retaining the pickup prefab's
        // normal layer and player-collection collision behavior.
        private sealed class PreventAntRegionCarry :
            MonoBehaviour,
            AntRegion.ICheck
        {
            public bool CanEnterAntRegion => false;

            public bool TryGetRenderer(out Renderer renderer)
            {
                renderer = null;
                return false;
            }
        }
        private static readonly AccessTools.FieldRef<RosaryCache, bool>
            IsReactivatingField =
                AccessTools.FieldRefAccess<RosaryCache, bool>(
                    "<IsReactivating>k__BackingField"
                );
        private static readonly FieldInfo RosaryGroupsField =
            AccessTools.Field(typeof(RosaryCacheString), "rosaryGroups");

        internal static MinorCacheManifest.Entry FindExactSource(
            string sceneName,
            string objectName,
            Vector2 position,
            MinorCacheManifest.SourceKind kind
        )
        {
            if (string.IsNullOrWhiteSpace(sceneName) ||
                string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            float toleranceSquared = MatchTolerance * MatchTolerance;
            MinorCacheManifest.Entry match = null;
            foreach (MinorCacheManifest.Entry entry in
                MinorCacheManifest.Entries)
            {
                if (entry.Kind != kind ||
                    !string.Equals(
                        entry.SceneName,
                        sceneName,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        entry.ObjectName,
                        objectName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                float deltaX = position.x - entry.X;
                float deltaY = position.y - entry.Y;
                if (deltaX * deltaX + deltaY * deltaY >
                    toleranceSquared)
                {
                    continue;
                }

                // Duplicate identities are ignored so a later manifest addition
                // cannot intercept the wrong vanilla object.
                if (match != null &&
                    !string.Equals(
                        match.LocationName,
                        entry.LocationName,
                        StringComparison.Ordinal))
                {
                    return null;
                }
                match = entry;
            }
            return match;
        }

        private static bool TryGetSeededEntry(
            Component source,
            MinorCacheManifest.SourceKind kind,
            out SaveState state,
            out MinorCacheManifest.Entry entry
        )
        {
            state = SaveState.Instance;
            entry = null;
            if (source == null ||
                state == null ||
                !state.IsRandomized(ItemType.Resource))
            {
                return false;
            }

            entry = FindExactSource(
                source.gameObject.scene.name,
                source.gameObject.name,
                source.transform.position,
                kind
            );
            if (entry == null ||
                !state.IsLocationEnabled(entry.LocationName) ||
                !state.IsLocationInSeed(entry.LocationName))
            {
                entry = null;
                return false;
            }
            return true;
        }

        private static bool TryReplace(
            Component source,
            MinorCacheManifest.SourceKind kind
        )
        {
            if (!TryGetSeededEntry(
                    source,
                    kind,
                    out SaveState state,
                    out MinorCacheManifest.Entry entry))
            {
                return false;
            }

            GameObject replacement = null;
            try
            {
                if (!state.IsLocationChecked(entry.LocationName))
                {
                    replacement = UnityEngine.Object.Instantiate(
                        GlobalSettings.Gameplay
                            .CollectableItemPickupPrefab.gameObject
                    );
                    replacement.name =
                        "AP Minor Cache - " + entry.LocationName;
                    replacement.transform.position =
                        source.transform.position;
                    if (RequiresStationaryAntVeto(entry.LocationName))
                    {
                        replacement.AddComponent<PreventAntRegionCarry>();
                    }

                    CollectableItemPickup pickup =
                        replacement.GetComponent<CollectableItemPickup>();
                    if (pickup == null)
                    {
                        UnityEngine.Object.Destroy(replacement);
                        return false;
                    }
                    pickup.SetItem(
                        MinorPickupPatches.GetProxyItem(
                            entry.LocationName
                        )
                    );
                }

                source.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(source.gameObject);
                return true;
            }
            catch (Exception ex)
            {
                if (replacement != null)
                {
                    UnityEngine.Object.Destroy(replacement);
                }
                if (!loggedReplacementFailure)
                {
                    loggedReplacementFailure = true;
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Could not replace a known minor " +
                        "cache; its vanilla source was left intact: " +
                        ex.Message
                    );
                }
                return false;
            }
        }

        internal static bool RequiresStationaryAntVeto(
            string locationName
        )
        {
            return string.Equals(
                    locationName,
                    StationaryHunterNecklaceLocation,
                    StringComparison.Ordinal) ||
                string.Equals(
                    locationName,
                    StationaryFarFieldsNecklaceLocation,
                    StringComparison.Ordinal);
        }

        private static bool RequiresDirectBreakCheck(
            MinorCacheManifest.Entry entry
        )
        {
            if (entry == null)
            {
                return false;
            }

            // These fossils can be broken but their source transforms are not
            // safe pickup positions due to water, spikes, ceilings or other
            // unstandable terrain. Their native break choreography awards the
            // check when the broken state is reached instead of spawning a
            // proximity pickup.
            return DirectBreakCheckLocations.Contains(entry.LocationName);
        }

        private static bool TryGetDirectBreakEntry(
            BreakableHolder source,
            out SaveState state,
            out MinorCacheManifest.Entry entry
        )
        {
            return TryGetSeededEntry(
                    source,
                    MinorCacheManifest.SourceKind.BreakableHolder,
                    out state,
                    out entry) &&
                RequiresDirectBreakCheck(entry);
        }

        private static bool TryGetRosaryVisualBounds(
            RosaryCacheString source,
            out Bounds bounds,
            out SpriteRenderer sortingReference
        )
        {
            bounds = default(Bounds);
            sortingReference = null;
            if (source == null || RosaryGroupsField == null)
            {
                return false;
            }

            Array groups = RosaryGroupsField.GetValue(source) as Array;
            if (groups == null)
            {
                return false;
            }

            bool hasBounds = false;
            foreach (object group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                FieldInfo representingObjectsField = AccessTools.Field(
                    group.GetType(),
                    "RepresentingObjects"
                );
                GameObject[] representingObjects =
                    representingObjectsField?.GetValue(group) as GameObject[];
                if (representingObjects == null)
                {
                    continue;
                }

                foreach (GameObject representingObject in representingObjects)
                {
                    if (representingObject == null)
                    {
                        continue;
                    }

                    foreach (SpriteRenderer renderer in
                        representingObject.GetComponentsInChildren<
                            SpriteRenderer
                        >(true))
                    {
                        if (renderer == null)
                        {
                            continue;
                        }

                        if (!hasBounds)
                        {
                            bounds = renderer.bounds;
                            hasBounds = true;
                        }
                        else
                        {
                            bounds.Encapsulate(renderer.bounds);
                        }

                        if (sortingReference == null ||
                            renderer.sortingOrder >
                            sortingReference.sortingOrder)
                        {
                            sortingReference = renderer;
                        }
                    }
                }
            }

            return hasBounds;
        }

        private static void TryCreateHangingGlow(
            RosaryCacheString source
        )
        {
            if (!TryGetSeededEntry(
                    source,
                    MinorCacheManifest.SourceKind.RosaryCache,
                    out SaveState state,
                    out MinorCacheManifest.Entry entry) ||
                state.IsLocationChecked(entry.LocationName) ||
                source.transform.Find(HangingGlowName) != null)
            {
                return;
            }

            Sprite sprite =
                RandomizerPlugin.Instance?.MapCheckIcon ??
                RandomizerPlugin.Instance?.ArchipelagoIcon;
            if (sprite == null)
            {
                return;
            }

            GameObject glow = null;
            try
            {
                if (!TryGetRosaryVisualBounds(
                        source,
                        out Bounds bounds,
                        out SpriteRenderer sortingReference))
                {
                    return;
                }

                glow = new GameObject(HangingGlowName);
                glow.SetActive(false);
                glow.layer = source.gameObject.layer;
                glow.transform.SetParent(source.transform, true);
                glow.transform.position = bounds.center;

                SpriteRenderer renderer =
                    glow.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color(1f, 1f, 1f, 0.66f);
                if (sortingReference != null)
                {
                    renderer.sortingLayerID =
                        sortingReference.sortingLayerID;
                    renderer.sortingOrder =
                        sortingReference.sortingOrder + 2;
                }

                float spriteSize = Mathf.Max(
                    sprite.bounds.size.x,
                    sprite.bounds.size.y
                );
                float parentScale = Mathf.Max(
                    Mathf.Abs(source.transform.lossyScale.x),
                    Mathf.Abs(source.transform.lossyScale.y)
                );
                float scale = spriteSize > 0f && parentScale > 0f
                    ? 0.9f / spriteSize / parentScale
                    : 1f;
                glow.transform.localScale =
                    new Vector3(scale, scale, 1f);
                SpriteFadePulse pulse =
                    glow.AddComponent<SpriteFadePulse>();
                pulse.lowAlpha = 0.42f;
                pulse.highAlpha = 0.9f;
                pulse.fadeDuration = 0.8f;
                pulse.startPaused = false;
                glow.SetActive(true);
            }
            catch (Exception ex)
            {
                if (glow != null)
                {
                    UnityEngine.Object.Destroy(glow);
                }
                if (!loggedHangingGlowFailure)
                {
                    loggedHangingGlowFailure = true;
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Could not add the visual AP shimmer " +
                        "to a hanging rosary cache: " + ex.Message
                    );
                }
            }
        }

        private static void RemoveHangingGlow(
            RosaryCacheString source
        )
        {
            Transform glow = source == null
                ? null
                : source.transform.Find(HangingGlowName);
            if (glow != null)
            {
                UnityEngine.Object.Destroy(glow.gameObject);
            }
        }

        [HarmonyPatch(typeof(GeoRock), "Start")]
        private static class GeoRockStartPatch
        {
            private static bool Prefix(GeoRock __instance)
            {
                return !TryReplace(
                    __instance,
                    MinorCacheManifest.SourceKind.GeoRock
                );
            }
        }

        [HarmonyPatch(typeof(BreakableHolder), "Awake")]
        private static class BreakableHolderAwakePatch
        {
            private static bool Prefix(BreakableHolder __instance)
            {
                if (TryGetDirectBreakEntry(
                        __instance,
                        out _,
                        out _))
                {
                    return true;
                }

                return !TryReplace(
                    __instance,
                    MinorCacheManifest.SourceKind.BreakableHolder
                );
            }
        }

        [HarmonyPatch(typeof(BreakableHolder), "FlingHolding")]
        private static class BreakableHolderFlingHoldingPatch
        {
            private static bool Prefix(BreakableHolder __instance)
            {
                // Direct-break caches keep their native breakable but must
                // not also leak their vanilla shell-shard payout.
                return !TryGetDirectBreakEntry(
                    __instance,
                    out _,
                    out _
                );
            }
        }

        [HarmonyPatch(typeof(BreakableHolder), "SetBroken")]
        private static class BreakableHolderSetBrokenPatch
        {
            private static void Postfix(BreakableHolder __instance)
            {
                if (!TryGetDirectBreakEntry(
                        __instance,
                        out SaveState state,
                        out MinorCacheManifest.Entry entry) ||
                    state.IsLocationChecked(entry.LocationName))
                {
                    return;
                }

                state.CheckLocation(entry.LocationName);
            }
        }

        [HarmonyPatch(typeof(RosaryCache), "Awake")]
        private static class RosaryCacheAwakePatch
        {
            private static bool Prefix(RosaryCache __instance)
            {
                // A RosaryCacheString's root is its ceiling anchor. Replacing
                // it with a proximity pickup strands that pickup in mid-air.
                // Its payout is redirected when FlingRosaries runs instead.
                if (__instance is RosaryCacheString)
                {
                    return true;
                }
                return !TryReplace(
                    __instance,
                    MinorCacheManifest.SourceKind.RosaryCache
                );
            }

            private static void Postfix(RosaryCache __instance)
            {
                if (__instance is RosaryCacheString stringCache)
                {
                    TryCreateHangingGlow(stringCache);
                }
            }
        }

        // Native FlingRosaries already deactivates the beads which have been
        // struck, but skips spawning their currency while IsReactivating is
        // true. Temporarily borrowing that guard preserves the complete
        // vanilla hit/break animation without leaking rosaries. None of the
        // 116 string sources has a second base flingOnStateChange payout.
        // Every source has a final-hit callback.
        [HarmonyPatch(typeof(RosaryCacheString), "FlingRosaries")]
        private static class RosaryCacheStringFlingRosariesPatch
        {
            private static void Prefix(
                RosaryCacheString __instance,
                bool isLast,
                ref bool __state
            )
            {
                if (!TryGetSeededEntry(
                        __instance,
                        MinorCacheManifest.SourceKind.RosaryCache,
                        out _,
                        out _))
                {
                    return;
                }

                __state = IsReactivatingField(__instance);
                IsReactivatingField(__instance) = true;
            }

            private static void Postfix(
                RosaryCacheString __instance,
                bool isLast,
                bool __state
            )
            {
                if (isLast)
                {
                    RemoveHangingGlow(__instance);
                }

                if (!TryGetSeededEntry(
                        __instance,
                        MinorCacheManifest.SourceKind.RosaryCache,
                        out SaveState state,
                        out MinorCacheManifest.Entry entry))
                {
                    return;
                }

                IsReactivatingField(__instance) = __state;
                if (isLast &&
                    !__state &&
                    !state.IsLocationChecked(entry.LocationName))
                {
                    state.CheckLocation(entry.LocationName);
                }
            }
        }
    }
}
