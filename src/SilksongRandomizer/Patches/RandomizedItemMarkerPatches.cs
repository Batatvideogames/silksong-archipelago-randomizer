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
        };

        private static readonly List<MarkerRecord> Records =
            new List<MarkerRecord>();
        private static readonly List<MarkerRecord> WideRecords =
            new List<MarkerRecord>();
        private static readonly HashSet<string> ReportedErrors =
            new HashSet<string>(StringComparer.Ordinal);
        private static GameMap currentMap;
        private static InventoryWideMap currentWideMap;

        internal static void Refresh(GameMap map)
        {
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
                if (state.TryGetRandomizedItemMarkerLocation(
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
                        "Its randomized check has no map position."
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
                if (state.TryGetRandomizedItemMarkerLocation(
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
                        "Its randomized check has no wide map position."
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
            }
            ReportedErrors.Clear();
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

            UpdateRecords(state, Records);
            UpdateRecords(state, WideRecords);
        }

        private static void UpdateRecords(
            SaveState state,
            List<MarkerRecord> records)
        {
            foreach (MarkerRecord record in records)
            {
                if (record.Replacement == null ||
                    record.NativeRenderer == null)
                {
                    continue;
                }

                bool collected = state.receivedItems.Contains(
                    record.Definition.ItemName
                ) || state.IsLocationChecked(record.LocationName);
                bool visible = !collected &&
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
                if (record.NativeRenderer != null)
                {
                    record.NativeRenderer.sprite = record.NativeSprite;
                }
                if (record.Replacement != null)
                {
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
