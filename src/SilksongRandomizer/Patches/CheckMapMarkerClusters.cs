using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static partial class CheckMapMarkerManager
    {
        private static MarkerRecord hoveredMarker;
        private const float CategoryPinSpacing = 0.18f;

        private static bool RefreshCategoryCluster(
            GameMap map,
            SaveState state,
            MarkerRecord anchor,
            List<string> names,
            bool visible,
            IReadOnlyDictionary<string, MapCheckReachability> reachability
        )
        {
            var groups = RandomizerPlugin.Instance?.UseCategoryMapIcons == true &&
                names.Count > 1 && CategoryMapIcons.Category(state, names) == null
                ? names.GroupBy(name => CategoryMapIcons.Category(state, new[] { name }) ?? string.Empty)
                    .OrderBy(group => group.Key, StringComparer.Ordinal).ToArray()
                : Array.Empty<IGrouping<string, string>>();
            if (groups.Length < 2)
                groups = Array.Empty<IGrouping<string, string>>();

            foreach (string key in anchor.CategoryMarkers.Keys.ToArray())
            {
                if (groups.Any(group => group.Key == key)) continue;
                MarkerRecord old = anchor.CategoryMarkers[key];
                if (hoveredMarker == old) SetHoveredMarker(null);
                old.MarkerObject.SetActive(false);
                UnityEngine.Object.Destroy(old.MarkerObject);
                anchor.CategoryMarkers.Remove(key);
            }
            anchor.Renderer.enabled = groups.Length == 0;
            if (groups.Length == 0) return false;

            anchor.MarkerObject.SetActive(visible);
            if (anchor.OutlineRenderer != null)
                anchor.OutlineRenderer.gameObject.SetActive(false);
            float spacing = Math.Min(CategoryPinSpacing, 0.54f / (groups.Length - 1));
            for (int index = 0; index < groups.Length; index++)
            {
                var group = groups[index];
                if (!anchor.CategoryMarkers.TryGetValue(group.Key, out MarkerRecord pin))
                {
                    pin = CreateMarker(map, anchor.Scene, group.First(), anchor.MapPosition,
                        GetMarkerSprite(false), GetMarkerOutlineSprite(false), anchor.Confidence);
                    if (pin == null) continue;
                    pin.MarkerObject.transform.SetParent(anchor.MarkerObject.transform, true);
                    anchor.CategoryMarkers.Add(group.Key, pin);
                }
                pin.LocationNames.Clear();
                pin.LocationNames.AddRange(group);
                RefreshMarkerState(map, state, pin, reachability);
                pin.MarkerObject.transform.position = anchor.MarkerObject.transform.position +
                    map.transform.TransformVector(new Vector3((index - (groups.Length - 1) * 0.5f) * spacing, 0f, 0f));
                pin.SortingOrder = anchor.SortingOrder + index * 2;
                ApplyMarkerSorting(pin);
            }
            return true;
        }

        private static IEnumerable<MarkerRecord> GetDisplayedMarkers()
        {
            foreach (MarkerRecord anchor in GetUniqueMarkers())
            {
                if (anchor.CategoryMarkers.Count == 0)
                    yield return anchor;
                else
                    foreach (MarkerRecord pin in anchor.CategoryMarkers.Values)
                        yield return pin;
            }
        }

        private static MarkerRecord FindHoveredMarker(Camera camera, Vector2 pointer)
        {
            MarkerRecord closest = null;
            float closestDistanceSquared = 34f * 34f;
            foreach (MarkerRecord marker in GetDisplayedMarkers())
            {
                if (marker.MarkerObject == null || !marker.MarkerObject.activeInHierarchy)
                    continue;
                Vector3 screen = camera.WorldToScreenPoint(marker.MarkerObject.transform.position);
                if (screen.z <= 0f) continue;
                float distanceSquared = ((Vector2)screen - pointer).sqrMagnitude;
                if (distanceSquared <= closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closest = marker;
                }
            }
            return closest;
        }

        private static void SetHoveredMarker(MarkerRecord marker)
        {
            if (hoveredMarker == marker) return;
            MarkerRecord previous = hoveredMarker;
            hoveredMarker = marker;
            ApplyMarkerSorting(previous);
            ApplyMarkerSorting(marker);
        }

        private static void ApplyMarkerSorting(MarkerRecord marker)
        {
            if (marker?.Renderer == null) return;
            marker.Renderer.sortingOrder = marker.SortingOrder + (hoveredMarker == marker ? 100 : 0);
            if (marker.OutlineRenderer != null)
                marker.OutlineRenderer.sortingOrder = marker.Renderer.sortingOrder + 1;
        }
    }
}
