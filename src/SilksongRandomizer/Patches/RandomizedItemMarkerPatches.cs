using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class RandomizedItemMarkerManager
    {
        private sealed class Definition
        {
            internal readonly string MarkerName;
            internal readonly string ItemName;
            internal readonly ItemType ItemType;

            internal Definition(
                string markerName,
                string itemName,
                ItemType itemType)
            {
                MarkerName = markerName;
                ItemName = itemName;
                ItemType = itemType;
            }
        }

        private sealed class MarkerRecord
        {
            internal readonly Definition Definition;
            internal readonly SpriteRenderer NativeRenderer;
            internal readonly Sprite NativeSprite;
            internal readonly string LocationName;
            internal readonly GameObject Replacement;
            internal readonly MapMarkerArrow Arrow;
            internal readonly Vector2 OriginalPosition;
            internal readonly Vector2 DestinationPosition;

            internal MarkerRecord(
                Definition definition,
                SpriteRenderer nativeRenderer,
                Sprite nativeSprite,
                string locationName,
                GameObject replacement)
            {
                Definition = definition;
                NativeRenderer = nativeRenderer;
                NativeSprite = nativeSprite;
                LocationName = locationName;
                Replacement = replacement;
                Arrow = nativeRenderer.GetComponentInParent<MapMarkerArrow>(true);
                if (Arrow == null) return;
                OriginalPosition = Traverse.Create(Arrow).Field("initialPos").GetValue<Vector2>();
                DestinationPosition = OriginalPosition;
                if (replacement != null)
                {
                    Vector3 target = Arrow.transform.position +
                        replacement.transform.position - nativeRenderer.transform.position;
                    DestinationPosition = Arrow.transform.parent.InverseTransformPoint(target);
                }
                ArrowRecords[Arrow] = this;
                if (replacement != null)
                {
                    Arrow.SetPosition(DestinationPosition);
                    Arrow.transform.localPosition = new Vector3(
                        DestinationPosition.x, DestinationPosition.y, Arrow.transform.localPosition.z);
                    replacement.transform.SetParent(nativeRenderer.transform, false);
                    replacement.transform.localPosition = Vector3.zero;
                    replacement.transform.localRotation = Quaternion.identity;
                    replacement.transform.localScale = Vector3.one;
                }
            }
        }

        private static readonly Definition[] Definitions =
        {
            new Definition(
                "Quest_Pin_Bellshrine_BoneForest",
                "Bell: The Marrow",
                ItemType.BellShrine),
            new Definition(
                "Quest_Pin_Bellshrine_Wilds",
                "Bell: Deep Docks",
                ItemType.BellShrine),
            new Definition(
                "Quest_Pin_Bellshrine_Greymoor",
                "Bell: Greymoor",
                ItemType.BellShrine),
            new Definition(
                "Quest_Pin_Bellshrine_Shellwood",
                "Bell: Shellwood",
                ItemType.BellShrine),
            new Definition(
                "Quest_Pin_Bellshrine_Bellhart",
                "Bell: Bellhart",
                ItemType.BellShrine),
            new Definition(
                "Quest_Pin_Architect",
                "Architect's Melody",
                ItemType.Melody),
            new Definition(
                "Quest_Pin_Conductor",
                "Conductor's Melody",
                ItemType.Melody),
            new Definition(
                "Quest_Pin_Librarian",
                "Vaultkeeper's Melody",
                ItemType.Melody),
            new Definition(
                "Quest_Pin_Librarian Return",
                "Vaultkeeper's Melody",
                ItemType.Melody),
        };

        private static readonly Dictionary<MapMarkerArrow, MarkerRecord> ArrowRecords =
            new Dictionary<MapMarkerArrow, MarkerRecord>();

        [HarmonyPatch(typeof(MapMarkerArrow), nameof(MapMarkerArrow.SetPosition))]
        private static class RandomizedArrowPositionPatch
        {
            [HarmonyPrefix]
            private static void Prefix(MapMarkerArrow __instance, ref Vector2 position)
            {
                if (ArrowRecords.TryGetValue(__instance, out MarkerRecord record))
                    position = record.DestinationPosition;
            }
        }

        [HarmonyPatch(typeof(QuestMapMarker), "IsActive")]
        private static class RandomizedArrowVisibilityPatch
        {
            [HarmonyPostfix]
            private static void Postfix(QuestMapMarker __instance, ref bool __result)
            {
                if (ArrowRecords.TryGetValue(__instance, out MarkerRecord record))
                    __result &= record.Replacement != null && !IsCollected(SaveState.Instance, record);
            }
        }

        private static bool IsCollected(SaveState state, MarkerRecord record)
        {
            return state == null || state.receivedItems.Contains(record.Definition.ItemName) ||
                state.IsLocationChecked(record.LocationName);
        }

        private static readonly List<MarkerRecord> Records =
            new List<MarkerRecord>();
        private static readonly List<MarkerRecord> WideRecords =
            new List<MarkerRecord>();
        private static readonly HashSet<string> ReportedErrors =
            new HashSet<string>(StringComparer.Ordinal);
        private static GameMap currentMap;
        private static InventoryWideMap currentWideMap;
        private static InventoryPane wideMapPane;

        internal static void Refresh(GameMap map)
        {
            if (map != currentMap)
            {
                ReportedErrors.Clear();
            }
            Clear();
            currentMap = map;
            SaveState state = SaveState.Instance;
            if (map == null || state == null)
            {
                return;
            }

            Transform[] transforms =
                map.GetComponentsInChildren<Transform>(true);
            foreach (Definition definition in Definitions)
            {
                if (!IsEnabled(state, definition.ItemType))
                {
                    continue;
                }

                Transform marker = transforms.FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(
                        candidate.name,
                        definition.MarkerName,
                        StringComparison.Ordinal
                    )
                );
                SpriteRenderer nativeRenderer =
                    marker?.GetComponent<SpriteRenderer>() ??
                    marker?.GetComponentInChildren<SpriteRenderer>(true);
                if (nativeRenderer == null || nativeRenderer.sprite == null)
                {
                    ReportError(
                        definition.ItemName,
                        "Native map marker was not found."
                    );
                    continue;
                }

                Sprite nativeSprite = nativeRenderer.sprite;
                GameObject replacement = null;
                string locationName = string.Empty;
                if (TryGetPhysicalMarkerLocation(
                        state,
                        definition.ItemName,
                        out locationName) &&
                    CheckMapMarkerManager.TryGetProjectedWorldPosition(
                        map,
                        locationName,
                        out Vector3 worldPosition))
                {
                    replacement = CreateReplacement(
                        definition,
                        nativeRenderer,
                        nativeSprite,
                        worldPosition
                    );
                }
                else if (!string.IsNullOrWhiteSpace(locationName))
                {
                    ReportError(
                        definition.ItemName,
                        "No map position for randomized check: " + locationName
                    );
                }

                nativeRenderer.sprite = null;
                Records.Add(new MarkerRecord(
                    definition,
                    nativeRenderer,
                    nativeSprite,
                    locationName,
                    replacement
                ));
            }

            Update(state);
        }

        internal static void Refresh(InventoryWideMap wideMap)
        {
            ClearWide();
            currentWideMap = wideMap;
            wideMapPane = wideMap != null
                ? wideMap.GetComponentInParent<InventoryPane>(true)
                : null;
            SaveState state = SaveState.Instance;
            GameManager gameManager = GameManager.instance;
            GameMap map = gameManager != null
                ? gameManager.gameMap
                : null;
            if (wideMap == null || map == null || state == null)
            {
                return;
            }

            Transform[] transforms =
                wideMap.GetComponentsInChildren<Transform>(true);
            foreach (Definition definition in Definitions)
            {
                if (!IsEnabled(state, definition.ItemType))
                {
                    continue;
                }

                Transform marker = transforms.FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(
                        candidate.name,
                        definition.MarkerName,
                        StringComparison.Ordinal
                    )
                );
                SpriteRenderer nativeRenderer =
                    marker?.GetComponent<SpriteRenderer>() ??
                    marker?.GetComponentInChildren<SpriteRenderer>(true);
                if (nativeRenderer == null || nativeRenderer.sprite == null)
                {
                    ReportError(
                        definition.ItemName,
                        "Native wide map marker was not found."
                    );
                    continue;
                }

                Sprite nativeSprite = nativeRenderer.sprite;
                GameObject replacement = null;
                string locationName = string.Empty;
                if (TryGetPhysicalMarkerLocation(
                        state,
                        definition.ItemName,
                        out locationName) &&
                    CheckMapMarkerManager
                        .TryGetProjectedLocalBoundsPosition(
                            map,
                            locationName,
                            out GlobalEnums.MapZone mapZone,
                            out Vector2 localBoundsPosition) &&
                    TryGetWideMapWorldPosition(
                        wideMap,
                        mapZone,
                        localBoundsPosition,
                        out Vector3 worldPosition))
                {
                    replacement = CreateReplacement(
                        definition,
                        nativeRenderer,
                        nativeSprite,
                        worldPosition
                    );
                }
                else if (!string.IsNullOrWhiteSpace(locationName))
                {
                    ReportError(
                        definition.ItemName,
                        "No wide map position for randomized check: " + locationName
                    );
                }

                nativeRenderer.sprite = null;
                WideRecords.Add(new MarkerRecord(
                    definition,
                    nativeRenderer,
                    nativeSprite,
                    locationName,
                    replacement
                ));
            }

            Update(state);
        }

        internal static void Update(GameMap map)
        {
            if (map == null || map != currentMap)
            {
                return;
            }
            Update(SaveState.Instance);
        }

        internal static void Clear(GameMap map = null)
        {
            if (map != null && currentMap != null && map != currentMap)
            {
                return;
            }

            ClearRecords(Records);
            currentMap = null;
            if (map != null)
            {
                ClearWide();
                ReportedErrors.Clear();
            }
        }

        private static void ClearWide(InventoryWideMap wideMap = null)
        {
            if (wideMap != null &&
                currentWideMap != null &&
                wideMap != currentWideMap)
            {
                return;
            }

            ClearRecords(WideRecords);
            currentWideMap = null;
            wideMapPane = null;
        }

        internal static bool TryGetPhysicalMarkerLocation(
            SaveState state,
            string itemName,
            out string locationName)
        {
            if (!state.TryGetRandomizedItemMarkerLocation(itemName, out locationName))
            {
                return false;
            }

            string canonicalName = LocationSet.GetCanonicalLocationName(locationName);
            if (state.locations.Locations.Any(location =>
                location.Type == ItemType.CrestSlot &&
                string.Equals(location.Name, canonicalName, StringComparison.OrdinalIgnoreCase)))
            {
                locationName = string.Empty;
                return false;
            }
            return true;
        }

        private static bool IsEnabled(
            SaveState state,
            ItemType itemType)
        {
            return state.IsRandomized(itemType) &&
                (
                    itemType == ItemType.BellShrine
                        ? state.randomizedBellMarkers
                        : state.randomizedMelodyMarkers
                );
        }

        private static void Update(SaveState state)
        {
            if (state == null)
            {
                return;
            }

            UpdateRecords(state, Records, CheckMapMarkerManager.IsMapDisplayed(currentMap));
            UpdateRecords(
                state,
                WideRecords,
                wideMapPane != null && wideMapPane.IsPaneActive
            );
        }

        private static void UpdateRecords(
            SaveState state,
            List<MarkerRecord> records,
            bool mapVisible)
        {
            foreach (MarkerRecord record in records)
            {
                bool collected = IsCollected(state, record);
                if (record.Arrow != null && (record.Replacement == null || collected))
                    record.Arrow.gameObject.SetActive(false);
                if (record.Replacement == null || record.NativeRenderer == null)
                    continue;
                bool visible = mapVisible && !collected &&
                    record.NativeRenderer.enabled &&
                    record.NativeRenderer.gameObject.activeInHierarchy;
                if (record.Replacement.activeSelf != visible)
                {
                    record.Replacement.SetActive(visible);
                }
            }
        }

        private static bool TryGetWideMapWorldPosition(
            InventoryWideMap wideMap,
            GlobalEnums.MapZone mapZone,
            Vector2 localBoundsPosition,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (wideMap == null)
            {
                return false;
            }

            InventoryItemWideMapZone[] zones =
                wideMap.DefaultSelectables;
            if (zones == null)
            {
                return false;
            }

            foreach (InventoryItemWideMapZone zone in zones)
            {
                if (zone == null)
                {
                    continue;
                }

                IEnumerable<GlobalEnums.MapZone> mappedZones =
                    zone.EnumerateMapZones();
                if (mappedZones == null || !mappedZones.Contains(mapZone))
                {
                    continue;
                }

                Vector2 nodePosition =
                    zone.GetClosestNodePosLocalBounds(
                        localBoundsPosition
                    );
                worldPosition = zone.transform.TransformPoint(
                    new Vector3(nodePosition.x, nodePosition.y, 0f)
                );
                return true;
            }

            return false;
        }

        private static void ClearRecords(List<MarkerRecord> records)
        {
            foreach (MarkerRecord record in records)
            {
                if (record.Arrow != null)
                {
                    ArrowRecords.Remove(record.Arrow);
                    record.Arrow.SetPosition(record.OriginalPosition);
                    record.Arrow.transform.localPosition = new Vector3(
                        record.OriginalPosition.x, record.OriginalPosition.y,
                        record.Arrow.transform.localPosition.z);
                }
                if (record.NativeRenderer != null)
                {
                    record.NativeRenderer.sprite = record.NativeSprite;
                }
                if (record.Replacement != null)
                {
                    record.Replacement.SetActive(false);
                    UnityEngine.Object.Destroy(record.Replacement);
                }
            }
            records.Clear();
        }

        private static GameObject CreateReplacement(
            Definition definition,
            SpriteRenderer source,
            Sprite sprite,
            Vector3 worldPosition)
        {
            GameObject marker = new GameObject(
                "Randomized Marker: " + definition.ItemName
            );
            marker.SetActive(false);
            marker.layer = source.gameObject.layer;
            marker.transform.SetParent(source.transform.parent, false);
            worldPosition.z = source.transform.position.z;
            marker.transform.position = worldPosition;
            marker.transform.localScale = source.transform.localScale;

            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = source.color;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            renderer.maskInteraction = source.maskInteraction;
            renderer.spriteSortPoint = source.spriteSortPoint;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            source.GetPropertyBlock(properties);
            renderer.SetPropertyBlock(properties);
            return marker;
        }

        private static void ReportError(
            string itemName,
            string message)
        {
            string key = itemName + "|" + message;
            if (!ReportedErrors.Add(key))
            {
                return;
            }

            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] " + itemName + " marker: " + message
            );
        }
    }

    [HarmonyPatch(
        typeof(GameMap),
        nameof(GameMap.SetupMap),
        new[] { typeof(bool) }
    )]
    internal static class RandomizedItemMarkerSetupPatch
    {
        private static void Postfix(GameMap __instance)
        {
            RandomizedItemMarkerManager.Refresh(__instance);
        }
    }

    [HarmonyPatch(
        typeof(InventoryWideMap),
        nameof(InventoryWideMap.UpdatePositions)
    )]
    internal static class RandomizedItemMarkerWideMapSetupPatch
    {
        private static void Postfix(InventoryWideMap __instance)
        {
            RandomizedItemMarkerManager.Refresh(__instance);
        }
    }

    [HarmonyPatch(typeof(GameMap), "Update")]
    internal static class RandomizedItemMarkerUpdatePatch
    {
        private static void Postfix(GameMap __instance)
        {
            RandomizedItemMarkerManager.Update(__instance);
        }
    }

    [HarmonyPatch(typeof(GameMap), "OnDestroy")]
    internal static class RandomizedItemMarkerDestroyPatch
    {
        private static void Prefix(GameMap __instance)
        {
            RandomizedItemMarkerManager.Clear(__instance);
        }
    }
}
