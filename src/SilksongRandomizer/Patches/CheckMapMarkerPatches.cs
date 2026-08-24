using HarmonyLib;
using SilksongRandomizer.AlphabetMode;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class CheckMapMarkerManager
    {
        private sealed class MarkerRecord
        {
            internal readonly List<string> LocationNames;
            internal readonly GameMapScene Scene;
            internal readonly GameObject MarkerObject;
            internal readonly SpriteRenderer Renderer;
            internal readonly SpriteRenderer OutlineRenderer;
            internal readonly MapMarkerPositionConfidence Confidence;
            internal readonly Vector2 MapPosition;

            internal MarkerRecord(
                string locationName,
                GameMapScene scene,
                GameObject markerObject,
                SpriteRenderer renderer,
                SpriteRenderer outlineRenderer,
                MapMarkerPositionConfidence confidence,
                Vector2 mapPosition
            )
            {
                LocationNames = new List<string> { locationName };
                Scene = scene;
                MarkerObject = markerObject;
                Renderer = renderer;
                OutlineRenderer = outlineRenderer;
                Confidence = confidence;
                MapPosition = mapPosition;
            }

            internal string LocationName =>
                LocationNames.Count > 0 ? LocationNames[0] : string.Empty;

            internal void AddLocation(string locationName)
            {
                if (!LocationNames.Any(name =>
                        string.Equals(
                            name,
                            locationName,
                            StringComparison.OrdinalIgnoreCase
                        )))
                {
                    LocationNames.Add(locationName);
                }
            }

            internal void RemoveLocation(string locationName)
            {
                LocationNames.RemoveAll(name =>
                    string.Equals(
                        name,
                        locationName,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            }
        }

        private sealed class DesiredMarker
        {
            internal readonly string LocationName;
            internal readonly GameMapScene Scene;
            internal readonly Vector2 MapPosition;
            internal readonly MapMarkerPositionConfidence Confidence;

            internal DesiredMarker(
                string locationName,
                GameMapScene scene,
                Vector2 mapPosition,
                MapMarkerPositionConfidence confidence
            )
            {
                LocationName = locationName;
                Scene = scene;
                MapPosition = mapPosition;
                Confidence = confidence;
            }
        }

        private static readonly Dictionary<string, List<MarkerRecord>>
            MarkersByLocation =
            new Dictionary<string, List<MarkerRecord>>(
                StringComparer.OrdinalIgnoreCase
            );
        private static readonly Dictionary<string, MapCheckReachability>
            ReachabilityByLocation =
            new Dictionary<string, MapCheckReachability>(
                StringComparer.OrdinalIgnoreCase
            );

        private const float AnchorToleranceSquared = 0.000001f;

        private static readonly FieldInfo MapZoneInfoField =
            AccessTools.Field(typeof(GameMap), "mapZoneInfo");
        private static readonly FieldInfo MarkerParentField =
            AccessTools.Field(typeof(GameMap), "markerParent");
        private static readonly FieldInfo CompassIconField =
            AccessTools.Field(typeof(GameMap), "compassIcon");
        private static readonly FieldInfo MapManagerField =
            AccessTools.Field(typeof(GameMap), "mapManager");
        private static readonly FieldInfo MapCameraField =
            AccessTools.Field(typeof(InventoryMapManager), "mapCamera");
        private static readonly FieldInfo MarkerScrollAreaField =
            AccessTools.Field(
                typeof(InventoryMapManager),
                "markerScrollArea"
            );
        private static readonly FieldInfo GameMapMarkerScrollAreaField =
            AccessTools.Field(typeof(GameMap), "mapMarkerScrollArea");
        private static readonly FieldInfo MapMarkerBoundsField =
            AccessTools.Field(typeof(GameMap), "MapMarkerBounds");
        private static readonly FieldInfo ZoomedBoundsField =
            AccessTools.Field(typeof(GameMap), "ZoomedBounds");
        private static readonly MethodInfo PositionLocalBoundsMethod =
            AccessTools.Method(typeof(GameMap), "GetPositionLocalBounds");

        private static GameMap currentMap;
        private static Transform nativeMarkerParent;
        private static SpriteRenderer nativePinRenderer;
        private static bool loggedMapOwnershipFailure;
        private static bool loggedMarkerHostFailure;
        private static bool loggedPanBoundsFailure;
        private static bool loggedBuildSummary;
        private static bool markerStatesDirty;
        private static int lastMarkerStateRefreshFrame = -1;
        private static int lastTooltipHitTestFrame = -1;

        internal static void Refresh(GameMap map)
        {
            if (map == null)
            {
                Clear();
                return;
            }

            SaveState state = SaveState.Instance;
            if (state == null ||
                state.checkMapMarkers == CheckMapMarkerMode.Off)
            {
                Clear();
                currentMap = map;
                return;
            }

            if (currentMap == null || currentMap != map)
            {
                Clear();
                currentMap = map;
            }

            // Reconcile on every refresh. The map can be constructed before
            // the AP room's active location set reaches the save state and a
            // canonical check can have multiple legitimate physical sources.
            BuildMarkers(map, state);

            RefreshMarkerStates(map, state);
        }

        private static void RefreshMarkerStates(
            GameMap map,
            SaveState state
        )
        {
            if (map == null || state == null)
            {
                return;
            }

            IReadOnlyDictionary<string, MapCheckReachability>
                reachabilityByLocation =
                    MapLogicEvaluator.EvaluateAll(
                        state,
                        MarkersByLocation.Keys
                    );
            ReachabilityByLocation.Clear();
            foreach (KeyValuePair<string, MapCheckReachability> entry in
                reachabilityByLocation)
            {
                ReachabilityByLocation[entry.Key] = entry.Value;
            }
            foreach (MarkerRecord marker in GetUniqueMarkers())
            {
                RefreshMarkerState(
                    map,
                    state,
                    marker,
                    reachabilityByLocation
                );
            }

            IncludeVisibleMarkersInPanBounds(map);
            markerStatesDirty = false;
            lastMarkerStateRefreshFrame = Time.frameCount;
        }

        private static void RefreshMarkerState(
            GameMap map,
            SaveState state,
            MarkerRecord marker,
            IReadOnlyDictionary<string, MapCheckReachability>
                reachabilityByLocation
        )
        {
            if (map == null || state == null || marker == null)
            {
                return;
            }

            List<string> activeLocationNames =
                GetActiveLocationNames(state, marker).ToList();
            bool visible = ShouldShowMarker(
                state,
                map,
                marker,
                activeLocationNames.Count > 0
            );
            bool logicUnknown =
                activeLocationNames.Any(locationName =>
                    MapLogicEvaluator.RequiresLogicVerification(
                        state,
                        locationName
                    )
                );
            Sprite sprite = GetMarkerSprite(logicUnknown);
            if (marker.Renderer != null && sprite != null)
            {
                marker.Renderer.sprite = sprite;
                SetMarkerWorldScale(marker.Renderer.transform, map, sprite);
                bool allUnreachable =
                    activeLocationNames.Count > 0 &&
                    activeLocationNames.All(locationName =>
                        reachabilityByLocation != null &&
                        reachabilityByLocation.TryGetValue(
                            locationName,
                            out MapCheckReachability reachability
                        ) &&
                        reachability ==
                            MapCheckReachability.Unreachable
                    );
                float alpha = allUnreachable ? 0.48f : 1f;
                marker.Renderer.color = new Color(1f, 1f, 1f, alpha);
            }

            bool hinted = activeLocationNames.Any(locationName =>
                HasCachedHint(state, locationName)
            );
            if (marker.OutlineRenderer != null)
            {
                Sprite outlineSprite = GetMarkerOutlineSprite(logicUnknown);
                if (outlineSprite != null)
                {
                    marker.OutlineRenderer.sprite = outlineSprite;
                }
                marker.OutlineRenderer.gameObject.SetActive(
                    visible && hinted && outlineSprite != null
                );
                marker.OutlineRenderer.color = Color.white;
            }
            if (marker.MarkerObject != null &&
                marker.MarkerObject.activeSelf != visible)
            {
                marker.MarkerObject.SetActive(visible);
            }
        }

        internal static void RefreshCurrentMap()
        {
            GameMap map = currentMap;
            if (map != null)
            {
                Refresh(map);
            }
        }

        internal static void RefreshCurrentMarkerStates()
        {
            RequestCurrentMarkerStateRefresh();
            ProcessPendingMarkerStateRefresh(currentMap);
        }

        internal static void RequestCurrentMarkerStateRefresh()
        {
            markerStatesDirty = true;
        }

        internal static void ProcessPendingMarkerStateRefresh(GameMap map)
        {
            if (!markerStatesDirty || map == null || map != currentMap ||
                lastMarkerStateRefreshFrame == Time.frameCount)
            {
                return;
            }

            Camera mapCamera = GetMapCamera(map);
            if (mapCamera == null || !mapCamera.isActiveAndEnabled)
            {
                // The state remains dirty while the inventory map is not
                // rendered. Every received item joins one refresh when
                // the existing map camera becomes active again.
                return;
            }

            SaveState state = SaveState.Instance;
            if (state != null)
            {
                RefreshMarkerStates(map, state);
            }
        }

        internal static void RefreshHintedLocation(string locationName)
        {
            GameMap map = currentMap;
            SaveState state = SaveState.Instance;
            string canonicalName =
                LocationSet.GetCanonicalLocationName(locationName);
            if (map == null || state == null ||
                string.IsNullOrWhiteSpace(canonicalName) ||
                !MarkersByLocation.TryGetValue(
                    canonicalName,
                    out List<MarkerRecord> locationMarkers
                ))
            {
                return;
            }

            // A newly cached hint changes only the white outline. Refresh
            // every physical or grouped marker for this location against the
            // existing reachability snapshot. A shop getter observing its hint
            // does not rerun the full graph.
            foreach (MarkerRecord marker in
                locationMarkers.Where(marker => marker != null)
                    .Distinct()
                    .ToArray())
            {
                RefreshMarkerState(
                    map,
                    state,
                    marker,
                    ReachabilityByLocation
                );
            }
        }

        internal static void HideCheckedLocation(string locationName)
        {
            GameMap map = currentMap;
            SaveState state = SaveState.Instance;
            string canonicalName =
                LocationSet.GetCanonicalLocationName(locationName);
            if (map != null &&
                state != null &&
                !string.IsNullOrWhiteSpace(canonicalName) &&
                MarkersByLocation.TryGetValue(
                    canonicalName,
                    out List<MarkerRecord> locationMarkers
                ))
            {
                foreach (MarkerRecord marker in locationMarkers.ToArray())
                {
                    if (marker != null)
                    {
                        RefreshMarkerState(
                            map,
                            state,
                            marker,
                            ReachabilityByLocation
                        );
                    }
                }
            }
        }

        internal static void Clear(GameMap map = null)
        {
            if (map != null && currentMap != null && map != currentMap)
            {
                return;
            }

            foreach (MarkerRecord marker in GetUniqueMarkers())
            {
                if (marker.MarkerObject != null)
                {
                    UnityEngine.Object.Destroy(marker.MarkerObject);
                }
            }

            MarkersByLocation.Clear();
            ReachabilityByLocation.Clear();
            currentMap = null;
            nativeMarkerParent = null;
            nativePinRenderer = null;
            loggedMapOwnershipFailure = false;
            loggedMarkerHostFailure = false;
            loggedPanBoundsFailure = false;
            loggedBuildSummary = false;
            markerStatesDirty = false;
            lastMarkerStateRefreshFrame = -1;
            lastTooltipHitTestFrame = -1;
        }

        private static void BuildMarkers(GameMap map, SaveState state)
        {
            Dictionary<string, GameMapScene> scenes =
                map.GetComponentsInChildren<GameMapScene>(true)
                    .Where(scene => scene != null)
                    .GroupBy(
                        scene => scene.Name,
                        StringComparer.OrdinalIgnoreCase
                    )
                    .ToDictionary(
                        group => group.Key,
                        group => group.First(),
                        StringComparer.OrdinalIgnoreCase
                    );

            if (!TryResolveNativeMarkerHost(map))
            {
                if (!loggedMarkerHostFailure)
                {
                    loggedMarkerHostFailure = true;
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] AP map-marker host was not found; " +
                        "check markers cannot be rendered."
                    );
                }
                return;
            }

            int supportedCount = 0;
            int existingCount = 0;
            int inactiveCount = 0;
            int missingSceneCount = 0;
            int projectionFailureCount = 0;
            int createdCount = 0;
            int groupedCount = 0;
            int removedCount = 0;

            List<MapCheckPosition> positions =
                CheckMapMarkerManifest.GetPositions().ToList();
            List<DesiredMarker> desiredMarkers =
                new List<DesiredMarker>();
            Dictionary<string, List<DesiredMarker>> desiredByLocation =
                new Dictionary<string, List<DesiredMarker>>(
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (MapCheckPosition basePosition in positions)
            {
                MapCheckPosition position =
                    CheckMapMarkerManifest.ResolveCurrentWorldVariant(
                        basePosition
                    );
                supportedCount++;
                string canonicalName =
                    LocationSet.GetCanonicalLocationName(
                        position.LocationName
                    );
                if (string.IsNullOrWhiteSpace(canonicalName))
                {
                    inactiveCount++;
                    continue;
                }
                if (
                    !state.IsLocationEnabled(canonicalName) ||
                    !state.IsLocationInSeed(canonicalName) ||
                    state.IsLocationChecked(canonicalName))
                {
                    inactiveCount++;
                    continue;
                }
                if (!scenes.TryGetValue(
                        position.SceneName,
                        out GameMapScene scene
                    ))
                {
                    missingSceneCount++;
                    continue;
                }
                if (!TryProjectPosition(
                        map,
                        scene,
                        position,
                        out Vector2 mapPosition
                    ))
                {
                    projectionFailureCount++;
                    continue;
                }

                if (!desiredByLocation.TryGetValue(
                        canonicalName,
                        out List<DesiredMarker> locationMarkers
                    ))
                {
                    locationMarkers = new List<DesiredMarker>();
                    desiredByLocation.Add(canonicalName, locationMarkers);
                }
                if (locationMarkers.Any(desired =>
                    IsSameAnchor(
                        desired.Scene,
                        desired.MapPosition,
                        scene,
                        mapPosition
                    )))
                {
                    continue;
                }

                DesiredMarker desiredMarker = new DesiredMarker(
                    canonicalName,
                    scene,
                    mapPosition,
                    position.Confidence
                );
                desiredMarkers.Add(desiredMarker);
                locationMarkers.Add(desiredMarker);
            }

            // Associations remain only while their current position exists.
            // This moves state-dependent rewards without collapsing
            // canonical checks that legitimately have two simultaneous
            // sources, such as Choral Chambers and the Cradle.
            foreach (
                KeyValuePair<string, List<MarkerRecord>> pair in
                    MarkersByLocation.ToArray()
            )
            {
                foreach (MarkerRecord marker in pair.Value.ToArray())
                {
                    bool stillDesired =
                        desiredByLocation.TryGetValue(
                            pair.Key,
                            out List<DesiredMarker> locationMarkers
                        ) &&
                        locationMarkers.Any(desired =>
                        IsSameAnchor(
                            desired.Scene,
                            desired.MapPosition,
                            marker.Scene,
                            marker.MapPosition
                        )
                    );
                    if (!stillDesired)
                    {
                        DetachLocation(pair.Key, marker);
                        removedCount++;
                    }
                }
            }

            Dictionary<GameMapScene, List<MarkerRecord>> markersByScene =
                GetUniqueMarkers()
                    .Where(marker => marker.Scene != null)
                    .GroupBy(marker => marker.Scene)
                    .ToDictionary(
                        group => group.Key,
                        group => group.ToList()
                    );

            foreach (DesiredMarker desired in desiredMarkers)
            {
                if (TryFindLocationMarkerAtAnchor(
                        desired.LocationName,
                        desired.Scene,
                        desired.MapPosition,
                        out _
                    ))
                {
                    existingCount++;
                    continue;
                }

                if (TryFindMarkerAtAnchor(
                        markersByScene,
                        desired.Scene,
                        desired.MapPosition,
                        out MarkerRecord anchorMarker
                    ))
                {
                    AssociateLocation(
                        desired.LocationName,
                        anchorMarker
                    );
                    groupedCount++;
                    continue;
                }
                bool logicUnknown =
                    MapLogicEvaluator.RequiresLogicVerification(
                        state,
                        desired.LocationName
                    );

                MarkerRecord marker = CreateMarker(
                    map,
                    desired.Scene,
                    desired.LocationName,
                    desired.MapPosition,
                    GetMarkerSprite(logicUnknown),
                    GetMarkerOutlineSprite(logicUnknown),
                    desired.Confidence
                );
                if (marker != null)
                {
                    AssociateLocation(desired.LocationName, marker);
                    if (!markersByScene.TryGetValue(
                            desired.Scene,
                            out List<MarkerRecord> sceneMarkers
                        ))
                    {
                        sceneMarkers = new List<MarkerRecord>();
                        markersByScene.Add(desired.Scene, sceneMarkers);
                    }
                    sceneMarkers.Add(marker);
                    createdCount++;
                }
            }

            if (!loggedBuildSummary &&
                state.roomLocationNames != null &&
                state.roomLocationNames.Count > 0)
            {
                loggedBuildSummary = true;
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] AP map markers: " +
                    GetUniqueMarkers().Count() + " anchors for " +
                    MarkersByLocation.Count + " active checks (" +
                    createdCount + " newly created, " +
                    groupedCount + " grouped, " +
                    removedCount + " stale removed) from " +
                    supportedCount + " supported positions; " +
                    inactiveCount + " checked/not in seed, " +
                    missingSceneCount + " scene misses, " +
                    projectionFailureCount + " projection misses, " +
                    existingCount + " already present; mode=" +
                    state.checkMapMarkers + "."
                );
            }
        }

        private static MarkerRecord CreateMarker(
            GameMap map,
            GameMapScene scene,
            string locationName,
            Vector2 mapPosition,
            Sprite sprite,
            Sprite outlineSprite,
            MapMarkerPositionConfidence confidence
        )
        {
            if (map == null || scene == null || sprite == null)
            {
                return null;
            }

            GameObject markerObject = new GameObject(
                "AP Check: " + locationName
            );
            markerObject.SetActive(false);
            markerObject.layer = nativePinRenderer.gameObject.layer;
            markerObject.transform.SetParent(nativeMarkerParent, false);
            Vector3 worldPosition = map.transform.TransformPoint(
                new Vector3(mapPosition.x, mapPosition.y, -1f)
            );
            markerObject.transform.localPosition =
                nativeMarkerParent.InverseTransformPoint(worldPosition);

            SpriteRenderer renderer =
                markerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            if (nativePinRenderer != null)
            {
                renderer.sharedMaterial = nativePinRenderer.sharedMaterial;
                // Silksong gives native map markers renderer-specific shader
                // properties (including the shimmer time offset). Copy those
                // without advancing Unity's global random state.
                MaterialPropertyBlock nativeProperties =
                    new MaterialPropertyBlock();
                nativePinRenderer.GetPropertyBlock(nativeProperties);
                renderer.SetPropertyBlock(nativeProperties);
                renderer.sortingLayerID = nativePinRenderer.sortingLayerID;
                renderer.sortingOrder =
                    Math.Max(20, nativePinRenderer.sortingOrder + 1);
                renderer.maskInteraction =
                    nativePinRenderer.maskInteraction;
                renderer.spriteSortPoint =
                    nativePinRenderer.spriteSortPoint;
            }
            else
            {
                renderer.sortingOrder = 20;
            }

            SetMarkerWorldScale(markerObject.transform, map, sprite);

            SpriteRenderer outlineRenderer = null;
            if (outlineSprite != null)
            {
                GameObject outlineObject = new GameObject(
                    "Hint Outline"
                );
                outlineObject.layer = markerObject.layer;
                outlineObject.transform.SetParent(
                    markerObject.transform,
                    false
                );
                outlineObject.transform.localPosition =
                    new Vector3(0f, 0f, -0.01f);
                outlineRenderer =
                    outlineObject.AddComponent<SpriteRenderer>();
                outlineRenderer.sprite = outlineSprite;
                outlineRenderer.color = Color.white;
                outlineRenderer.sortingLayerID =
                    renderer.sortingLayerID;
                outlineRenderer.sortingOrder =
                    renderer.sortingOrder + 1;
                outlineRenderer.maskInteraction =
                    renderer.maskInteraction;
                outlineRenderer.spriteSortPoint =
                    renderer.spriteSortPoint;
                outlineObject.SetActive(false);
            }

            return new MarkerRecord(
                locationName,
                scene,
                markerObject,
                renderer,
                outlineRenderer,
                confidence,
                mapPosition
            );
        }

        private static bool TryFindMarkerAtAnchor(
            IReadOnlyDictionary<GameMapScene, List<MarkerRecord>>
                markersByScene,
            GameMapScene scene,
            Vector2 mapPosition,
            out MarkerRecord marker
        )
        {
            // Locations merge only when they resolve to the same physical source.
            // The tiny tolerance absorbs projection noise without grouping
            // genuinely separate nearby pickups.
            marker = null;
            if (markersByScene == null || scene == null ||
                !markersByScene.TryGetValue(
                    scene,
                    out List<MarkerRecord> sceneMarkers
                ))
            {
                return false;
            }

            marker = sceneMarkers.FirstOrDefault(candidate =>
                candidate != null &&
                IsSameAnchor(
                    candidate.Scene,
                    candidate.MapPosition,
                    scene,
                    mapPosition
                )
            );
            return marker != null;
        }

        private static bool TryFindLocationMarkerAtAnchor(
            string locationName,
            GameMapScene scene,
            Vector2 mapPosition,
            out MarkerRecord marker
        )
        {
            marker = null;
            if (!MarkersByLocation.TryGetValue(
                    locationName,
                    out List<MarkerRecord> locationMarkers
                ))
            {
                return false;
            }

            marker = locationMarkers.FirstOrDefault(candidate =>
                candidate != null &&
                IsSameAnchor(
                    candidate.Scene,
                    candidate.MapPosition,
                    scene,
                    mapPosition
                )
            );
            return marker != null;
        }

        private static bool IsSameAnchor(
            GameMapScene leftScene,
            Vector2 leftPosition,
            GameMapScene rightScene,
            Vector2 rightPosition
        )
        {
            return leftScene == rightScene &&
                   (leftPosition - rightPosition).sqrMagnitude <=
                       AnchorToleranceSquared;
        }

        private static void AssociateLocation(
            string locationName,
            MarkerRecord marker
        )
        {
            if (string.IsNullOrWhiteSpace(locationName) || marker == null)
            {
                return;
            }

            if (!MarkersByLocation.TryGetValue(
                    locationName,
                    out List<MarkerRecord> locationMarkers
                ))
            {
                locationMarkers = new List<MarkerRecord>();
                MarkersByLocation.Add(locationName, locationMarkers);
            }

            if (!locationMarkers.Contains(marker))
            {
                locationMarkers.Add(marker);
            }
            marker.AddLocation(locationName);
        }

        private static void DetachLocation(
            string locationName,
            MarkerRecord marker
        )
        {
            if (MarkersByLocation.TryGetValue(
                    locationName,
                    out List<MarkerRecord> locationMarkers
                ))
            {
                locationMarkers.Remove(marker);
                if (locationMarkers.Count == 0)
                {
                    MarkersByLocation.Remove(locationName);
                }
            }

            if (marker == null)
            {
                return;
            }

            marker.RemoveLocation(locationName);
            if (marker.LocationNames.Count == 0 &&
                marker.MarkerObject != null)
            {
                UnityEngine.Object.Destroy(marker.MarkerObject);
            }
        }

        private static IEnumerable<MarkerRecord> GetUniqueMarkers()
        {
            return MarkersByLocation.Values
                .SelectMany(markers => markers)
                .Where(marker => marker != null)
                .Distinct();
        }

        private static IEnumerable<string> GetActiveLocationNames(
            SaveState state,
            MarkerRecord marker
        )
        {
            if (state == null || marker == null)
            {
                yield break;
            }

            foreach (string locationName in marker.LocationNames)
            {
                if (state.IsLocationEnabled(locationName) &&
                    state.IsLocationInSeed(locationName) &&
                    !state.IsLocationChecked(locationName))
                {
                    yield return locationName;
                }
            }
        }

        private static bool TryResolveNativeMarkerHost(GameMap map)
        {
            if (map == null)
            {
                return false;
            }
            if (nativeMarkerParent != null &&
                nativePinRenderer != null)
            {
                return true;
            }

            nativeMarkerParent =
                MarkerParentField?.GetValue(map) as Transform;
            GameObject compassIcon =
                CompassIconField?.GetValue(map) as GameObject;
            Transform compassParent = compassIcon?.transform.parent;

            if (nativeMarkerParent == null && compassParent != null)
            {
                nativeMarkerParent = compassParent
                    .Cast<Transform>()
                    .FirstOrDefault(child =>
                        child != null &&
                        string.Equals(
                            child.name,
                            "Map Markers",
                            StringComparison.OrdinalIgnoreCase
                        )
                    );
            }
            if (nativeMarkerParent == null)
            {
                nativeMarkerParent = map
                    .GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(child =>
                        child != null &&
                        string.Equals(
                            child.name,
                            "Map Markers",
                            StringComparison.OrdinalIgnoreCase
                        )
                    );
            }

            // Native marker templates are direct children of markerParent.
            // Their root renderer carries the shimmer property block installed
            // by GameMap.OnAwake. A descendant renderer remains as a fallback
            // for minor hierarchy changes.
            nativePinRenderer = nativeMarkerParent?
                .Cast<Transform>()
                .Select(child =>
                    child?.GetComponent<SpriteRenderer>()
                )
                .FirstOrDefault(renderer => renderer != null);
            if (nativePinRenderer == null)
            {
                nativePinRenderer = nativeMarkerParent?
                    .GetComponentsInChildren<SpriteRenderer>(true)
                    .FirstOrDefault(renderer => renderer != null);
            }
            return nativeMarkerParent != null &&
                   nativePinRenderer != null;
        }

        private static bool TryProjectPosition(
            GameMap map,
            GameMapScene scene,
            MapCheckPosition position,
            out Vector2 mapPosition
        )
        {
            mapPosition = Vector2.zero;
            if (map == null || scene == null)
            {
                return false;
            }
            if (CheckMapMarkerManifest.TryGetDirectMapPosition(
                    position.LocationName,
                    out mapPosition
                ))
            {
                return IsFinite(mapPosition.x) &&
                       IsFinite(mapPosition.y);
            }
            if (scene.transform.parent == null)
            {
                return false;
            }

            // This is the game's GetMapPosition calculation. Keeping it here
            // avoids relying on private/publicized Assembly-CSharp members.
            Vector3 sceneLocal = scene.transform.localPosition;
            Vector3 parentLocal = scene.transform.parent.localPosition;
            Vector2 roomMapCenter = new Vector2(
                sceneLocal.x + parentLocal.x,
                sceneLocal.y + parentLocal.y
            );

            // Vanilla returns the authored GameMapScene centre when a room
            // has no BoundsSprite. Several chapel, vendor and reward
            // interiors use that representation. Rejecting it
            // made every AP check hosted there disappear from the map.
            if (scene.BoundsSprite == null)
            {
                mapPosition = roomMapCenter;
                return IsFinite(mapPosition.x) &&
                       IsFinite(mapPosition.y);
            }
            if (
                position.SceneSize.x <= 0f ||
                position.SceneSize.y <= 0f)
            {
                return false;
            }

            Vector2 roomMapSize =
                (Vector2)scene.BoundsSprite.bounds.size *
                (Vector2)scene.transform.localScale;

            mapPosition = new Vector2(
                roomMapCenter.x - (roomMapSize.x * 0.5f) +
                (
                    position.PositionInScene.x /
                    position.SceneSize.x *
                    roomMapSize.x
                ),
                roomMapCenter.y - (roomMapSize.y * 0.5f) +
                (
                    position.PositionInScene.y /
                    position.SceneSize.y *
                    roomMapSize.y
                )
            );
            return IsFinite(mapPosition.x) && IsFinite(mapPosition.y);
        }

        private static bool TryGetProjectedMapPosition(
            GameMap map,
            string locationName,
            out GameMapScene projectedScene,
            out Vector2 mapPosition
        )
        {
            projectedScene = null;
            mapPosition = Vector2.zero;
            if (map == null || string.IsNullOrWhiteSpace(locationName))
            {
                return false;
            }

            string canonicalName =
                LocationSet.GetCanonicalLocationName(locationName);
            Dictionary<string, GameMapScene> scenes =
                map.GetComponentsInChildren<GameMapScene>(true)
                    .Where(scene => scene != null)
                    .GroupBy(
                        scene => scene.Name,
                        StringComparer.OrdinalIgnoreCase
                    )
                    .ToDictionary(
                        group => group.Key,
                        group => group.First(),
                        StringComparer.OrdinalIgnoreCase
                    );

            foreach (MapCheckPosition basePosition in
                     CheckMapMarkerManifest.GetPositions())
            {
                MapCheckPosition position =
                    CheckMapMarkerManifest.ResolveCurrentWorldVariant(
                        basePosition
                    );
                if (!string.Equals(
                        LocationSet.GetCanonicalLocationName(
                            position.LocationName
                        ),
                        canonicalName,
                        StringComparison.OrdinalIgnoreCase) ||
                    !scenes.TryGetValue(
                        position.SceneName,
                        out GameMapScene scene) ||
                    !TryProjectPosition(
                        map,
                        scene,
                        position,
                        out mapPosition))
                {
                    continue;
                }

                projectedScene = scene;
                return true;
            }

            return false;
        }

        internal static bool TryGetProjectedWorldPosition(
            GameMap map,
            string locationName,
            out Vector3 worldPosition
        )
        {
            worldPosition = Vector3.zero;
            if (!TryGetProjectedMapPosition(
                    map,
                    locationName,
                    out _,
                    out Vector2 mapPosition))
            {
                return false;
            }

            worldPosition = map.transform.TransformPoint(
                new Vector3(mapPosition.x, mapPosition.y, -1f)
            );
            return true;
        }

        internal static bool TryGetProjectedLocalBoundsPosition(
            GameMap map,
            string locationName,
            out GlobalEnums.MapZone mapZone,
            out Vector2 localBoundsPosition
        )
        {
            mapZone = default;
            localBoundsPosition = Vector2.zero;
            if (PositionLocalBoundsMethod == null ||
                !TryGetProjectedMapPosition(
                    map,
                    locationName,
                    out GameMapScene scene,
                    out Vector2 mapPosition))
            {
                return false;
            }

            mapZone = map.GetMapZoneForScene(scene.transform);
            try
            {
                object projected = PositionLocalBoundsMethod.Invoke(
                    map,
                    new object[] { mapPosition, mapZone }
                );
                if (projected is Vector2 position &&
                    IsFinite(position.x) &&
                    IsFinite(position.y))
                {
                    localBoundsPosition = position;
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static bool ShouldShowMarker(
            SaveState state,
            GameMap map,
            MarkerRecord marker,
            bool hasActiveLocations
        )
        {
            if (state == null || map == null || marker == null ||
                marker.Scene == null ||
                !hasActiveLocations)
            {
                return false;
            }

            switch (state.checkMapMarkers)
            {
                case CheckMapMarkerMode.MappedRooms:
                    return marker.Scene.IsMapped;
                case CheckMapMarkerMode.OwnedMaps:
                    return IsAreaMapOwned(map, marker.Scene);
                case CheckMapMarkerMode.All:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAreaMapOwned(
            GameMap map,
            GameMapScene scene
        )
        {
            PlayerData playerData = PlayerData.instance;
            if (map == null || scene == null || playerData == null)
            {
                return false;
            }
            if (playerData.mapAllRooms)
            {
                return true;
            }

            try
            {
                Array zoneInfos = MapZoneInfoField?.GetValue(map) as Array;
                int zoneIndex = (int)map.GetMapZoneForScene(scene.transform);
                if (zoneInfos == null ||
                    zoneIndex < 0 ||
                    zoneIndex >= zoneInfos.Length)
                {
                    return false;
                }

                object zoneInfo = zoneInfos.GetValue(zoneIndex);
                FieldInfo parentsField =
                    AccessTools.Field(zoneInfo.GetType(), "Parents");
                IEnumerable parents =
                    parentsField?.GetValue(zoneInfo) as IEnumerable;
                if (parents == null)
                {
                    return false;
                }

                foreach (object parentInfo in parents)
                {
                    if (parentInfo == null)
                    {
                        continue;
                    }

                    Type parentType = parentInfo.GetType();
                    GameObject parentObject =
                        AccessTools.Field(parentType, "Parent")
                            ?.GetValue(parentInfo) as GameObject;
                    if (parentObject == null ||
                        parentObject.transform != scene.transform.parent)
                    {
                        continue;
                    }

                    string playerDataBool =
                        AccessTools.Field(parentType, "PlayerDataBool")
                            ?.GetValue(parentInfo) as string;
                    return !string.IsNullOrWhiteSpace(playerDataBool) &&
                           playerData.GetBool(playerDataBool);
                }
            }
            catch (Exception ex)
            {
                if (!loggedMapOwnershipFailure)
                {
                    loggedMapOwnershipFailure = true;
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Could not inspect map ownership; " +
                        "Owned Maps check markers will fail closed: " +
                        ex.Message
                    );
                }
            }

            return false;
        }

        private static Sprite GetMarkerSprite(bool logicUnknown)
        {
            RandomizerPlugin plugin = RandomizerPlugin.Instance;
            if (logicUnknown)
            {
                return plugin?.LogicUnknownIcon ??
                       plugin?.MapCheckIcon ??
                       plugin?.ArchipelagoIcon;
            }
            return plugin?.MapCheckIcon ?? plugin?.ArchipelagoIcon;
        }

        private static Sprite GetMarkerOutlineSprite(bool logicUnknown)
        {
            RandomizerPlugin plugin = RandomizerPlugin.Instance;
            return logicUnknown
                ? plugin?.LogicUnknownOutlineIcon ??
                  plugin?.MapCheckOutlineIcon
                : plugin?.MapCheckOutlineIcon;
        }

        private static bool HasCachedHint(
            SaveState state,
            string locationName
        )
        {
            return state?.receivedHints?.Any(hint =>
                    hint != null &&
                    string.Equals(
                        LocationSet.GetCanonicalLocationName(
                            hint.locationName
                        ),
                        locationName,
                        StringComparison.OrdinalIgnoreCase
                    )
                ) == true;
        }

        internal static void IncludeVisibleMarkersInPanBounds(GameMap map)
        {
            if (map == null || map != currentMap ||
                MapMarkerBoundsField == null ||
                GameMapMarkerScrollAreaField == null ||
                ZoomedBoundsField == null)
            {
                return;
            }

            try
            {
                Bounds markerBounds =
                    (Bounds)MapMarkerBoundsField.GetValue(map);
                Bounds scrollArea =
                    (Bounds)GameMapMarkerScrollAreaField.GetValue(map);
                if (scrollArea.size.x <= 0f ||
                    scrollArea.size.y <= 0f ||
                    !IsFinite(markerBounds.center.x) ||
                    !IsFinite(markerBounds.center.y))
                {
                    return;
                }

                // Vanilla subtracts the viewport extents from the full map
                // bounds. That keeps the map filling the viewport, but it
                // also means an outer marker can reach only the viewport
                // edge while controller tooltip focus stays at its center.
                // Rebuild those vanilla bounds, then add only the positions
                // needed to center each currently displayed AP marker.
                Vector3 panExtents =
                    markerBounds.extents - scrollArea.extents;
                panExtents.x = Mathf.Max(0f, panExtents.x);
                panExtents.y = Mathf.Max(0f, panExtents.y);
                panExtents.z = Mathf.Max(0f, panExtents.z);
                Bounds focusBounds = new Bounds(
                    markerBounds.center,
                    panExtents * 2f
                );

                foreach (MarkerRecord marker in GetUniqueMarkers())
                {
                    if (marker?.MarkerObject == null ||
                        !marker.MarkerObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Vector3 focusPosition = new Vector3(
                        scrollArea.center.x - marker.MapPosition.x,
                        scrollArea.center.y - marker.MapPosition.y,
                        focusBounds.center.z
                    );
                    focusBounds.Encapsulate(focusPosition);
                }

                ZoomedBoundsField.SetValue(map, focusBounds);
            }
            catch (Exception ex)
            {
                if (!loggedPanBoundsFailure)
                {
                    loggedPanBoundsFailure = true;
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Could not extend map panning for " +
                        "AP check-marker focus: " + ex.Message
                    );
                }
            }
        }

        internal static void DrawTooltip()
        {
            GameMap map = currentMap;
            if (map == null || MarkersByLocation.Count == 0)
            {
                return;
            }

            Camera mapCamera = GetMapCamera(map);
            if (mapCamera == null || !mapCamera.isActiveAndEnabled ||
                !TryGetTooltipPointer(map, mapCamera, out Vector2 pointer))
            {
                return;
            }

            Event currentEvent = Event.current;
            if ((currentEvent != null &&
                 currentEvent.type != EventType.Repaint) ||
                lastTooltipHitTestFrame == Time.frameCount)
            {
                return;
            }
            lastTooltipHitTestFrame = Time.frameCount;

            MarkerRecord closest = null;
            float closestDistanceSquared = 34f * 34f;
            foreach (MarkerRecord marker in GetUniqueMarkers())
            {
                if (marker?.MarkerObject == null ||
                    !marker.MarkerObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 markerScreen = mapCamera.WorldToScreenPoint(
                    marker.MarkerObject.transform.position
                );
                if (markerScreen.z <= 0f)
                {
                    continue;
                }
                Vector2 markerPoint = new Vector2(
                    markerScreen.x,
                    markerScreen.y
                );
                float distanceSquared =
                    (markerPoint - pointer).sqrMagnitude;
                if (distanceSquared <= closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closest = marker;
                }
            }

            if (closest == null)
            {
                return;
            }

            List<string> tooltipLocationNames =
                GetActiveLocationNames(
                    SaveState.Instance,
                    closest
                ).ToList();
            if (tooltipLocationNames.Count == 0)
            {
                return;
            }

            // A shared shop/NPC/source stays at its exact map coordinate.
            // Each unchecked AP check at that anchor gets its own tooltip
            // line. No placed-item classification is disclosed.
            Color outOfLogicNameColor = new Color(0.58f, 0.58f, 0.58f, 1f);
            List<LogicTooltipLine> lines = new List<LogicTooltipLine>();
            SaveState state = SaveState.Instance;
            foreach (string locationName in tooltipLocationNames)
            {
                bool haveReachability =
                    ReachabilityByLocation.TryGetValue(
                        locationName,
                        out MapCheckReachability reachability
                    );
                Color nameColor =
                    haveReachability &&
                    reachability == MapCheckReachability.Unreachable
                        ? outOfLogicNameColor
                        : Color.white;
                lines.Add(
                    new LogicTooltipLine(
                        AlphabetModeManager.FilterDirectText(locationName),
                        nameColor
                    )
                );
                if (RandomizerPlugin.Instance != null &&
                    !RandomizerPlugin.Instance.ShowMapMarkerLogic)
                {
                    continue;
                }
                foreach (LogicTooltipLine extra in
                    MapLogicEvaluator.Explain(
                        state,
                        locationName,
                        haveReachability
                            ? reachability
                            : MapCheckReachability.Unknown
                    ) ?? Array.Empty<LogicTooltipLine>())
                {
                    if (!string.IsNullOrWhiteSpace(extra?.Text))
                    {
                        lines.Add(
                            new LogicTooltipLine(
                                AlphabetModeManager.FilterDirectText(extra.Text),
                                extra.Color
                            )
                        );
                    }
                }
            }

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            int tooltipFontSize = RandomizerPlugin.Instance == null
                ? 14
                : RandomizerPlugin.Instance.MapMarkerTooltipFontSize;
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = tooltipFontSize,
                wordWrap = false,
            };
            float contentWidth = lines.Max(line =>
                labelStyle.CalcSize(new GUIContent(line.Text)).x
            );
            float boxWidth = contentWidth + 18f;
            float lineHeight = Math.Max(
                18f,
                labelStyle.CalcHeight(
                    new GUIContent("Ag"),
                    Math.Max(1f, contentWidth)
                )
            );
            float boxHeight = lineHeight * lines.Count + 10f;
            Vector2 size = new Vector2(boxWidth, boxHeight);
            Vector2 guiPointer = new Vector2(
                pointer.x,
                Screen.height - pointer.y
            );
            Rect rect = new Rect(
                Mathf.Clamp(
                    guiPointer.x + 18f,
                    4f,
                    Mathf.Max(4f, Screen.width - size.x - 4f)
                ),
                Mathf.Clamp(
                    guiPointer.y + 18f,
                    4f,
                    Mathf.Max(4f, Screen.height - size.y - 4f)
                ),
                size.x,
                size.y
            );
            GUI.Box(rect, GUIContent.none, boxStyle);
            for (int index = 0; index < lines.Count; index++)
            {
                Color textColor = lines[index].Color;
                labelStyle.normal.textColor = textColor;
                labelStyle.hover.textColor = textColor;
                labelStyle.active.textColor = textColor;
                labelStyle.focused.textColor = textColor;
                GUI.Label(
                    new Rect(
                        rect.x + 9f,
                        rect.y + 5f + index * lineHeight,
                        contentWidth,
                        lineHeight
                    ),
                    lines[index].Text,
                    labelStyle
                );
            }
        }

        private static Camera GetMapCamera(GameMap map)
        {
            InventoryMapManager mapManager =
                MapManagerField?.GetValue(map) as InventoryMapManager;
            if (mapManager == null)
            {
                return null;
            }
            return MapCameraField?.GetValue(mapManager) as Camera;
        }

        private static bool TryGetTooltipPointer(
            GameMap map,
            Camera mapCamera,
            out Vector2 pointer
        )
        {
            pointer = Vector2.zero;
            if (Cursor.visible)
            {
                pointer = Input.mousePosition;
                return true;
            }

            // Controller input pans GameMap itself. InventoryCursor remains
            // attached to an inventory selectable and is not a map-space
            // pointer. The center of Silksong's map view bounds acts as the
            // controller focus so panning a marker under it reveals the name
            // even when the map panel is not centered on the full camera.
            InventoryMapManager mapManager =
                MapManagerField?.GetValue(map) as InventoryMapManager;
            if (
                mapManager != null &&
                MarkerScrollAreaField?.GetValue(mapManager) is
                    Bounds mapViewBounds &&
                mapViewBounds.size.x > 0f &&
                mapViewBounds.size.y > 0f
            )
            {
                Vector3 screenPoint = mapCamera.WorldToScreenPoint(
                    mapViewBounds.center
                );
                if (screenPoint.z > 0f &&
                    IsFinite(screenPoint.x) &&
                    IsFinite(screenPoint.y))
                {
                    pointer = new Vector2(
                        screenPoint.x,
                        screenPoint.y
                    );
                    return true;
                }
            }

            // Fail soft if the view bounds have not been created yet.
            Rect viewport = mapCamera.pixelRect;
            if (viewport.width <= 0f || viewport.height <= 0f)
            {
                return false;
            }
            pointer = viewport.center;
            return IsFinite(pointer.x) && IsFinite(pointer.y);
        }

        private static void SetMarkerWorldScale(
            Transform marker,
            GameMap map,
            Sprite sprite
        )
        {
            if (marker == null || map == null || sprite == null ||
                marker.parent == null)
            {
                return;
            }

            const float targetMapSize = 0.42f;
            float spriteSize = Math.Max(
                sprite.bounds.size.x,
                sprite.bounds.size.y
            );
            if (spriteSize <= 0f)
            {
                return;
            }

            Vector3 mapScale = map.transform.lossyScale;
            Vector3 desiredWorldScale = new Vector3(
                mapScale.x * targetMapSize / spriteSize,
                mapScale.y * targetMapSize / spriteSize,
                mapScale.z
            );
            Vector3 parentScale = marker.parent.lossyScale;
            marker.localScale = new Vector3(
                SafeDivide(desiredWorldScale.x, parentScale.x),
                SafeDivide(desiredWorldScale.y, parentScale.y),
                SafeDivide(desiredWorldScale.z, parentScale.z)
            );
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Math.Abs(divisor) < 0.0001f ? value : value / divisor;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [HarmonyPatch(
        typeof(GameMap),
        nameof(GameMap.SetupMap),
        new[] { typeof(bool) }
    )]
    internal static class CheckMapMarkerSetupPatch
    {
        private static void Postfix(GameMap __instance)
        {
            CheckMapMarkerManager.Refresh(__instance);
        }
    }

    [HarmonyPatch(typeof(GameMap), nameof(GameMap.WorldMap))]
    internal static class CheckMapMarkerWorldMapPatch
    {
        private static void Postfix(GameMap __instance)
        {
            CheckMapMarkerManager.ProcessPendingMarkerStateRefresh(
                __instance
            );
            CheckMapMarkerManager.IncludeVisibleMarkersInPanBounds(
                __instance
            );
        }
    }

    [HarmonyPatch(typeof(GameMap), "Update")]
    internal static class CheckMapMarkerUpdatePatch
    {
        private static void Postfix(GameMap __instance)
        {
            CheckMapMarkerManager.ProcessPendingMarkerStateRefresh(
                __instance
            );
        }
    }

    [HarmonyPatch(typeof(GameMap), "OnDestroy")]
    internal static class CheckMapMarkerDestroyPatch
    {
        private static void Prefix(GameMap __instance)
        {
            CheckMapMarkerManager.Clear(__instance);
        }
    }

    [HarmonyPatch(typeof(SaveState), nameof(SaveState.CheckLocation))]
    internal static class CheckMapMarkerLocalCheckPatch
    {
        private static void Postfix(string locationName)
        {
            CheckMapMarkerManager.HideCheckedLocation(locationName);
        }
    }

    [HarmonyPatch(
        typeof(SaveState),
        nameof(SaveState.CommitReceivedItemAtIndex)
    )]
    internal static class CheckMapMarkerReceivedItemPatch
    {
        private static void Postfix(bool __result)
        {
            if (__result)
            {
                CheckMapMarkerManager.RequestCurrentMarkerStateRefresh();
            }
        }
    }

    [HarmonyPatch(
        typeof(Archipelago),
        nameof(Archipelago.SynchronizeSaveState)
    )]
    internal static class CheckMapMarkerReconcilePatch
    {
        private static void Postfix()
        {
            CheckMapMarkerManager.RefreshCurrentMap();
        }
    }

    [HarmonyPatch(typeof(SaveState), nameof(SaveState.CacheHint))]
    internal static class CheckMapMarkerCacheHintPatch
    {
        private static void Postfix(
            SaveState.HintData hint,
            bool __result)
        {
            if (__result && hint != null)
            {
                string canonicalLocationName =
                    LocationSet.GetCanonicalLocationName(
                        hint.locationName
                    );
                if (!string.IsNullOrWhiteSpace(canonicalLocationName))
                {
                    CheckMapMarkerManager.RefreshHintedLocation(
                        canonicalLocationName
                    );
                }
            }
        }
    }

    [HarmonyPatch(typeof(SaveState), nameof(SaveState.GetHint))]
    internal static class CheckMapMarkerCachedHintPatch
    {
        private static readonly HashSet<string> RefreshedHintLocations =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static SaveState refreshedHintOwner;

        private static void Postfix(
            SaveState __instance,
            string locationName,
            bool __result)
        {
            if (!ReferenceEquals(refreshedHintOwner, __instance))
            {
                refreshedHintOwner = __instance;
                RefreshedHintLocations.Clear();
            }

            string canonicalLocationName =
                LocationSet.GetCanonicalLocationName(locationName);
            if (__result &&
                !string.IsNullOrWhiteSpace(canonicalLocationName) &&
                RefreshedHintLocations.Add(canonicalLocationName))
            {
                // Shop properties query the same cached hint repeatedly while
                // their UI is open. A hint changes only the white outline for
                // its own physical/grouped marker, so never reevaluate every
                // location merely because this getter observed the hint.
                CheckMapMarkerManager.RefreshHintedLocation(
                    canonicalLocationName
                );
            }
        }
    }

    [HarmonyPatch(
        typeof(GameManager),
        "SetLoadedGameData",
        new[] { typeof(SaveGameData), typeof(int) }
    )]
    internal static class CheckMapMarkerLoadPatch
    {
        private static void Postfix()
        {
            CheckMapMarkerManager.RefreshCurrentMap();
        }
    }
}
