using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlingObjectsFromGlobalPoolV3 =
    HutongGames.PlayMaker.Actions.FlingObjectsFromGlobalPoolV3;
using SetFsmBool = HutongGames.PlayMaker.Actions.SetFsmBool;

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
        private const string ChestControlFsmName = "Chest Control";
        private const string ChestSpawnStateName = "Spawn Items";
        private const string BankOpenedFsmName = "Set Opened";
        private const string BankOpenedStateName = "State 1";
        private const string InactiveChestPickupPrefix =
            "AP Inactive Currency Chest - ";
        private const string SongInactiveChestLocation =
            "Choral Chambers - Rosary Chest #3";
        private const string SongInactiveChestScene = "Song_24";
        private const string SongInactiveChestPath =
            "Black Thread States/Normal World/Enemy Control/" +
            "Song Fencer/Chest";
        private const string DockInactiveChestLocation =
            "Deep Docks Church - Shell Shard Chest";
        private const string DockInactiveChestScene = "Dock_06_Church";
        private const string DockInactiveChestPath =
            "Black Thread States Thread Only Variant/" +
            "Normal World/City Shard Chest";
        private const string StationaryHunterNecklaceLocation =
            "Hunter's March - Rosary Necklace";
        private const string StationaryFarFieldsNecklaceLocation =
            "Far Fields - Pale Rosary Necklace";
        private const string CoralDualAwardCacheLocation =
            "Blasted Steps - Shell Shard Cache #4";
        private static readonly HashSet<string> DirectBreakCheckLocations =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Far Fields - Rosary Cache #19",
                "Bilewater - Shell Shard Cache #1",
                "Bilewater - Shell Shard Cache #2",
                "Blasted Steps - Shell Shard Cache #1",
                "Blasted Steps - Shell Shard Cache #2",
                "Blasted Steps - Shell Shard Cache #3",
                "Mount Fay - Shell Shard Cache #5",
                "Mount Fay - Shell Shard Cache #6",
                "Mount Fay - Shell Shard Cache #7",
                "Putrified Ducts - Shell Shard Cache #6",
                "Putrified Ducts - Shell Shard Cache #8",
                "Putrified Ducts - Shell Shard Cache #9",
                "Putrified Ducts - Shell Shard Cache #10",
                "Putrified Ducts - Shell Shard Cache #11",
                "Sinner's Road - Shell Shard Cache #6",
                "Sinner's Road - Shell Shard Cache #7",
                "The Slab - Shell Shard Cache #4",
                "The Slab - Shell Shard Cache #5",
                "Underworks - Shell Shard Cache #3",
            };
        private static bool loggedReplacementFailure;
        private static bool loggedHangingGlowFailure;
        private static bool loggedCurrencyChestFailure;
        private static bool loggedInactiveChestFailure;
        [ThreadStatic]
        private static int dualAwardFlingDepth;
        private static readonly Dictionary<string, GameObject>
            InactiveChestPickups =
                new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Transform>
            InactiveChestSources =
                new Dictionary<string, Transform>(StringComparer.Ordinal);
        private static int inactiveChestSceneHandle = -1;

        private sealed class InactiveCurrencyChestEntry
        {
            internal readonly string LocationName;
            internal readonly string SceneName;
            internal readonly string HierarchyPath;
            internal readonly float X;
            internal readonly float Y;

            internal InactiveCurrencyChestEntry(
                string locationName,
                string sceneName,
                string hierarchyPath,
                float x,
                float y
            )
            {
                LocationName = locationName;
                SceneName = sceneName;
                HierarchyPath = hierarchyPath;
                X = x;
                Y = y;
            }
        }

        private static readonly InactiveCurrencyChestEntry[]
            InactiveCurrencyChests =
            {
                new InactiveCurrencyChestEntry(
                    SongInactiveChestLocation,
                    SongInactiveChestScene,
                    SongInactiveChestPath,
                    8.19f,
                    38.75f
                ),
                new InactiveCurrencyChestEntry(
                    DockInactiveChestLocation,
                    DockInactiveChestScene,
                    DockInactiveChestPath,
                    7.269272f,
                    21.892515f
                ),
            };

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

        private static bool IsCoralDualAwardEntry(
            MinorCacheManifest.Entry entry
        )
        {
            return entry != null && string.Equals(
                entry.LocationName,
                CoralDualAwardCacheLocation,
                StringComparison.Ordinal
            );
        }

        private static bool TryGetPreservedBreakEntry(
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
                (RequiresDirectBreakCheck(entry) ||
                 IsCoralDualAwardEntry(entry));
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

        private static void LogCurrencyChestFailure(
            string locationName,
            string reason
        )
        {
            if (loggedCurrencyChestFailure)
            {
                return;
            }

            loggedCurrencyChestFailure = true;
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Could not patch currency chest " +
                (locationName ?? "source") + ": " + reason
            );
        }

        private static bool IsRobbedBankChest(
            PlayMakerFSM fsm,
            MinorCacheManifest.Entry entry
        )
        {
            Transform parent = fsm == null ? null : fsm.transform.parent;
            bool exactBankChest =
                entry != null &&
                string.Equals(
                    entry.SceneName,
                    "Hang_06_bank",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                (string.Equals(
                     entry.ObjectName,
                     "Chest",
                     StringComparison.Ordinal
                 ) ||
                 string.Equals(
                     entry.ObjectName,
                     "Chest (1)",
                     StringComparison.Ordinal
                 ) ||
                 string.Equals(
                     entry.ObjectName,
                     "Chest (2)",
                     StringComparison.Ordinal
                 ));
            return exactBankChest &&
                   parent != null &&
                   string.Equals(
                       parent.name,
                       "Thieves Here",
                       StringComparison.Ordinal
                   );
        }

        internal static bool CanUseCurrencyChestPersistence(
            bool robbedBankChest,
            bool hasPersistence
        )
        {
            return hasPersistence || robbedBankChest;
        }

        internal static bool IsInactiveCurrencyChestIdentity(
            string locationName,
            string sceneName,
            string hierarchyPath
        )
        {
            return
                (string.Equals(
                     SongInactiveChestLocation,
                     locationName,
                     StringComparison.Ordinal) &&
                 string.Equals(
                     SongInactiveChestScene,
                     sceneName,
                     StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(
                     SongInactiveChestPath,
                     hierarchyPath,
                     StringComparison.Ordinal)) ||
                (string.Equals(
                     DockInactiveChestLocation,
                     locationName,
                     StringComparison.Ordinal) &&
                 string.Equals(
                     DockInactiveChestScene,
                     sceneName,
                     StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(
                     DockInactiveChestPath,
                     hierarchyPath,
                     StringComparison.Ordinal));
        }

        internal static bool ShouldUseInactiveCurrencyChestFallback(
            bool roomBound,
            bool resourceRandomized,
            bool locationEnabled,
            bool locationInSeed,
            bool locationChecked,
            bool nativeSourceFound,
            bool nativeSourceActive
        )
        {
            return roomBound &&
                   resourceRandomized &&
                   locationEnabled &&
                   locationInSeed &&
                   !locationChecked &&
                   nativeSourceFound &&
                   !nativeSourceActive;
        }

        private static Transform FindExactSceneTransform(
            Scene scene,
            string hierarchyPath
        )
        {
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(hierarchyPath))
            {
                return null;
            }

            int separator = hierarchyPath.IndexOf('/');
            string rootName = separator < 0
                ? hierarchyPath
                : hierarchyPath.Substring(0, separator);
            string childPath = separator < 0
                ? string.Empty
                : hierarchyPath.Substring(separator + 1);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null ||
                    !string.Equals(
                        root.name,
                        rootName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                return string.IsNullOrEmpty(childPath)
                    ? root.transform
                    : root.transform.Find(childPath);
            }
            return null;
        }

        private static bool IsExactInactiveCurrencyChestSource(
            InactiveCurrencyChestEntry fallback,
            Transform source
        )
        {
            if (fallback == null || source == null ||
                !IsInactiveCurrencyChestIdentity(
                    fallback.LocationName,
                    source.gameObject.scene.name,
                    fallback.HierarchyPath))
            {
                return false;
            }

            MinorCacheManifest.Entry manifestEntry = FindExactSource(
                source.gameObject.scene.name,
                source.name,
                source.position,
                MinorCacheManifest.SourceKind.CurrencyChest
            );
            return manifestEntry != null &&
                   string.Equals(
                       manifestEntry.LocationName,
                       fallback.LocationName,
                       StringComparison.Ordinal);
        }

        private static void RemoveInactiveCurrencyChestPickup(
            string locationName
        )
        {
            if (!InactiveChestPickups.TryGetValue(
                    locationName,
                    out GameObject pickup))
            {
                return;
            }

            if (pickup != null)
            {
                pickup.SetActive(false);
                UnityEngine.Object.Destroy(pickup);
            }
            InactiveChestPickups.Remove(locationName);
        }

        private static void CreateInactiveCurrencyChestPickup(
            InactiveCurrencyChestEntry fallback,
            Transform nativeSource
        )
        {
            if (fallback == null || nativeSource == null ||
                (InactiveChestPickups.TryGetValue(
                     fallback.LocationName,
                     out GameObject existing) &&
                 existing != null))
            {
                return;
            }

            GameObject pickupObject = null;
            try
            {
                pickupObject = UnityEngine.Object.Instantiate(
                    GlobalSettings.Gameplay
                        .CollectableItemPickupPrefab.gameObject
                );
                pickupObject.name =
                    InactiveChestPickupPrefix + fallback.LocationName;
                pickupObject.transform.position = new Vector3(
                    fallback.X,
                    fallback.Y,
                    nativeSource.position.z
                );
                CollectableItemPickup pickup =
                    pickupObject.GetComponent<CollectableItemPickup>();
                if (pickup == null)
                {
                    UnityEngine.Object.Destroy(pickupObject);
                    return;
                }

                pickup.SetItem(
                    MinorPickupPatches.GetProxyItem(
                        fallback.LocationName
                    )
                );
                InactiveChestPickups[fallback.LocationName] =
                    pickupObject;
            }
            catch (Exception ex)
            {
                if (pickupObject != null)
                {
                    UnityEngine.Object.Destroy(pickupObject);
                }
                if (!loggedInactiveChestFailure)
                {
                    loggedInactiveChestFailure = true;
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Could not create an inactive chest " +
                        "fallback: " + ex.Message
                    );
                }
            }
        }

        internal static void UpdateInactiveCurrencyChestFallbacks()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                ResetInactiveCurrencyChestFallbacks();
                return;
            }

            if (scene.handle != inactiveChestSceneHandle)
            {
                ResetInactiveCurrencyChestFallbacks();
                inactiveChestSceneHandle = scene.handle;
            }

            SaveState state = SaveState.Instance;
            foreach (InactiveCurrencyChestEntry fallback in
                     InactiveCurrencyChests)
            {
                if (!string.Equals(
                        scene.name,
                        fallback.SceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    RemoveInactiveCurrencyChestPickup(
                        fallback.LocationName
                    );
                    continue;
                }

                if (!InactiveChestSources.TryGetValue(
                        fallback.LocationName,
                        out Transform nativeSource) ||
                    nativeSource == null)
                {
                    nativeSource = FindExactSceneTransform(
                        scene,
                        fallback.HierarchyPath
                    );
                    if (nativeSource != null)
                    {
                        InactiveChestSources[fallback.LocationName] =
                            nativeSource;
                    }
                }
                bool exactSource =
                    IsExactInactiveCurrencyChestSource(
                        fallback,
                        nativeSource
                    );
                bool shouldUseFallback =
                    ShouldUseInactiveCurrencyChestFallback(
                        state != null && state.IsRoomBound,
                        state != null &&
                            state.IsRandomized(ItemType.Resource),
                        state != null &&
                            state.IsLocationEnabled(
                                fallback.LocationName
                            ),
                        state != null &&
                            state.IsLocationInSeed(
                                fallback.LocationName
                            ),
                        state != null &&
                            state.IsLocationChecked(
                                fallback.LocationName
                            ),
                        exactSource,
                        exactSource &&
                            nativeSource.gameObject.activeInHierarchy
                    );
                if (shouldUseFallback)
                {
                    CreateInactiveCurrencyChestPickup(
                        fallback,
                        nativeSource
                    );
                }
                else
                {
                    RemoveInactiveCurrencyChestPickup(
                        fallback.LocationName
                    );
                }
            }
        }

        internal static void ResetInactiveCurrencyChestFallbacks()
        {
            foreach (InactiveCurrencyChestEntry fallback in
                     InactiveCurrencyChests)
            {
                RemoveInactiveCurrencyChestPickup(
                    fallback.LocationName
                );
            }
            InactiveChestSources.Clear();
            inactiveChestSceneHandle = -1;
        }

        private static void PatchBankOpenedFsm(PlayMakerFSM fsm)
        {
            if (!string.Equals(
                    fsm.FsmName,
                    BankOpenedFsmName,
                    StringComparison.Ordinal
                ) ||
                !TryGetSeededEntry(
                    fsm,
                    MinorCacheManifest.SourceKind.CurrencyChest,
                    out SaveState state,
                    out MinorCacheManifest.Entry entry
                ) ||
                !IsRobbedBankChest(fsm, entry) ||
                state.IsLocationChecked(entry.LocationName))
            {
                return;
            }

            FsmState openedState = fsm.Fsm?.GetState(
                BankOpenedStateName
            );
            FsmStateAction[] actions = openedState?.Actions;
            if (actions == null ||
                actions.Length != 1 ||
                !(actions[0] is SetFsmBool setOpened) ||
                setOpened.fsmName == null ||
                setOpened.variableName == null ||
                setOpened.setValue == null ||
                !string.Equals(
                    setOpened.fsmName.Value,
                    ChestControlFsmName,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    setOpened.variableName.Value,
                    "Activated",
                    StringComparison.Ordinal
                ) ||
                !setOpened.setValue.Value)
            {
                LogCurrencyChestFailure(
                    entry.LocationName,
                    "the bank opened-state layout did not match"
                );
                return;
            }

            KeepBankChestClosed replacement =
                new KeepBankChestClosed();
            replacement.Init(openedState);
            openedState.Actions = new FsmStateAction[] { replacement };
        }

        private static void PatchCurrencyChestFsm(PlayMakerFSM fsm)
        {
            if (!string.Equals(
                    fsm.FsmName,
                    ChestControlFsmName,
                    StringComparison.Ordinal
                ) ||
                !TryGetSeededEntry(
                    fsm,
                    MinorCacheManifest.SourceKind.CurrencyChest,
                    out SaveState state,
                    out MinorCacheManifest.Entry entry
                ))
            {
                return;
            }

            PersistentBoolItem persistence =
                fsm.GetComponent<PersistentBoolItem>();
            bool robbedBankChest = IsRobbedBankChest(fsm, entry);
            FsmBool activated =
                fsm.FsmVariables?.FindFsmBool("Activated");
            FsmState spawnState = fsm.Fsm?.GetState(
                ChestSpawnStateName
            );
            FsmStateAction[] actions = spawnState?.Actions;
            if (!CanUseCurrencyChestPersistence(
                    robbedBankChest,
                    persistence != null
                ) ||
                activated == null ||
                actions == null)
            {
                LogCurrencyChestFailure(
                    entry.LocationName,
                    "its persistence or Spawn Items state was missing"
                );
                return;
            }

            int payoutActionCount = 0;
            foreach (FsmStateAction action in actions)
            {
                if (action is FlingObjectsFromGlobalPoolV3)
                {
                    payoutActionCount++;
                }
            }
            if (payoutActionCount != 3)
            {
                LogCurrencyChestFailure(
                    entry.LocationName,
                    "the native payout layout did not match"
                );
                return;
            }

            CompleteCurrencyChestLocation replacement =
                new CompleteCurrencyChestLocation(
                    entry.LocationName,
                    robbedBankChest
                );
            replacement.Init(spawnState);
            List<FsmStateAction> patchedActions =
                new List<FsmStateAction>(
                    actions.Length - payoutActionCount + 1
                );
            bool inserted = false;
            foreach (FsmStateAction action in actions)
            {
                if (action is FlingObjectsFromGlobalPoolV3)
                {
                    if (!inserted)
                    {
                        patchedActions.Add(replacement);
                        inserted = true;
                    }
                    continue;
                }
                patchedActions.Add(action);
            }
            spawnState.Actions = patchedActions.ToArray();

            bool checkedLocation =
                state.IsLocationChecked(entry.LocationName);
            activated.Value = checkedLocation;
            if (persistence != null)
            {
                persistence.SetValueOverride(checkedLocation);
                persistence.SaveStateNoCondition();
            }
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class CurrencyChestFsmPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.Last)]
            private static void Prefix(PlayMakerFSM __instance)
            {
                if (__instance == null ||
                    __instance.gameObject == null)
                {
                    return;
                }

                PatchBankOpenedFsm(__instance);
                PatchCurrencyChestFsm(__instance);
            }
        }

        private sealed class KeepBankChestClosed : FsmStateAction
        {
            public override void OnEnter()
            {
                Finish();
            }
        }

        private sealed class CompleteCurrencyChestLocation :
            FsmStateAction
        {
            private readonly string locationName;
            private readonly bool allowMissingPersistence;

            internal CompleteCurrencyChestLocation(
                string locationName,
                bool allowMissingPersistence
            )
            {
                this.locationName = locationName;
                this.allowMissingPersistence = allowMissingPersistence;
            }

            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                PersistentBoolItem persistence =
                    Owner == null
                        ? null
                        : Owner.GetComponent<PersistentBoolItem>();
                FsmBool activated = Fsm?.Variables?.FindFsmBool(
                    "Activated"
                );
                if (state != null &&
                    activated != null &&
                    CanUseCurrencyChestPersistence(
                        allowMissingPersistence,
                        persistence != null
                    ) &&
                    state.IsRandomized(ItemType.Resource) &&
                    state.IsLocationEnabled(locationName) &&
                    state.IsLocationInSeed(locationName))
                {
                    activated.Value = true;
                    if (persistence != null)
                    {
                        persistence.SetValueOverride(true);
                        persistence.SaveStateNoCondition();
                    }
                    if (!state.IsLocationChecked(locationName))
                    {
                        state.CheckLocation(locationName);
                    }
                }
                Finish();
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
                if (TryGetPreservedBreakEntry(
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
            private static bool Prefix(
                BreakableHolder __instance,
                ref bool __state
            )
            {
                if (!TryGetSeededEntry(
                        __instance,
                        MinorCacheManifest.SourceKind.BreakableHolder,
                        out _,
                        out MinorCacheManifest.Entry entry))
                {
                    return true;
                }
                if (!IsCoralDualAwardEntry(entry))
                {
                    return !RequiresDirectBreakCheck(entry);
                }
                dualAwardFlingDepth++;
                __state = true;
                return true;
            }

            private static Exception Finalizer(
                Exception __exception,
                bool __state
            )
            {
                if (__state)
                {
                    dualAwardFlingDepth--;
                }
                return __exception;
            }
        }

        [HarmonyPatch(
            typeof(FlingUtils),
            nameof(FlingUtils.SpawnAndFlingShellShards),
            new Type[]
            {
                typeof(FlingUtils.Config),
                typeof(Transform),
                typeof(Vector3),
                typeof(List<GameObject>),
            }
        )]
        private static class SpawnAndFlingShellShardsPatch
        {
            private static bool Prefix()
            {
                return dualAwardFlingDepth == 0;
            }
        }

        [HarmonyPatch(typeof(BreakableHolder), "SetBroken")]
        private static class BreakableHolderSetBrokenPatch
        {
            private static void Postfix(BreakableHolder __instance)
            {
                if (!TryGetPreservedBreakEntry(
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
