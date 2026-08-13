using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class MinorPickupPatches
    {
        private const float MatchTolerance = 0.75f;
        private static readonly Dictionary<string, ArchipelagoLocationItem>
            ProxyByLocation =
                new Dictionary<string, ArchipelagoLocationItem>(
                    StringComparer.OrdinalIgnoreCase
                );

        private sealed class ArchipelagoLocationItem : SavedItem
        {
            internal string LocationName;
            internal ItemType Type;

            public override void Get(bool showPopup = true)
            {
                SaveState state = SaveState.Instance;
                if (state != null &&
                    state.IsRandomized(Type) &&
                    state.IsLocationInSeed(LocationName) &&
                    !state.IsLocationChecked(LocationName))
                {
                    state.CheckLocation(LocationName);
                }
            }

            public override bool CanGetMore()
            {
                SaveState state = SaveState.Instance;
                return state != null &&
                       state.IsRandomized(Type) &&
                       state.IsLocationInSeed(LocationName) &&
                       !state.IsLocationChecked(LocationName);
            }
        }

        internal static SavedItem GetProxyItem(
            string locationName,
            ItemType type = ItemType.Resource)
        {
            if (ProxyByLocation.TryGetValue(
                    locationName,
                    out ArchipelagoLocationItem proxy) &&
                proxy != null)
            {
                return proxy;
            }

            proxy = ScriptableObject.CreateInstance<ArchipelagoLocationItem>();
            proxy.name = "Archipelago Location - " + locationName;
            proxy.LocationName = locationName;
            proxy.Type = type;
            ProxyByLocation[locationName] = proxy;
            return proxy;
        }

        internal static MinorPickupManifest.Entry FindExactSource(
            string sceneName,
            string assetName,
            Vector2 position,
            string hierarchyPath = ""
        )
        {
            if (string.IsNullOrWhiteSpace(sceneName) ||
                string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }

            float toleranceSquared = MatchTolerance * MatchTolerance;
            MinorPickupManifest.Entry match = null;
            foreach (MinorPickupManifest.Entry entry in
                MinorPickupManifest.Entries)
            {
                if (!string.Equals(
                        sceneName,
                        entry.SceneName,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        assetName,
                        entry.AssetName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                bool hierarchyMatches =
                    !string.IsNullOrEmpty(entry.HierarchyPath) &&
                    string.Equals(
                        hierarchyPath,
                        entry.HierarchyPath,
                        StringComparison.OrdinalIgnoreCase
                    );
                float deltaX = position.x - entry.X;
                float deltaY = position.y - entry.Y;
                bool positionMatches =
                    deltaX * deltaX + deltaY * deltaY <=
                    toleranceSquared;
                if (!hierarchyMatches && !positionMatches)
                {
                    continue;
                }

                // Ambiguous identities remain vanilla instead of intercepting a
                // possibly unrelated vanilla pickup.
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

        private static void TryReplaceSourceItem(
            CollectableItemPickup pickup,
            ref SavedItem item)
        {
            if (pickup == null || item == null)
            {
                return;
            }

            SaveState state = SaveState.Instance;
            MinorPickupManifest.Entry entry = FindExactSource(
                pickup.gameObject.scene.name,
                item.name,
                pickup.transform.position,
                Utils.GetHierarchyPath(pickup.transform)
            );
            if (entry == null ||
                state == null ||
                !state.IsRandomized(entry.Type) ||
                !state.IsLocationEnabled(entry.LocationName) ||
                !state.IsLocationInSeed(entry.LocationName))
            {
                return;
            }

            item = GetProxyItem(entry.LocationName, entry.Type);
        }

        [HarmonyPatch(typeof(CollectableItemPickup), "Awake")]
        private static class CollectableItemPickupAwakePatch
        {
            private static void Prefix(CollectableItemPickup __instance)
            {
                SaveState state = SaveState.Instance;
                SavedItem nativeItem = __instance == null
                    ? null
                    : __instance.Item;
                if (state == null ||
                    nativeItem == null)
                {
                    return;
                }

                MinorPickupManifest.Entry entry = FindExactSource(
                    __instance.gameObject.scene.name,
                    nativeItem.name,
                    __instance.transform.position,
                    Utils.GetHierarchyPath(__instance.transform)
                );
                if (entry == null ||
                    !state.IsRandomized(entry.Type) ||
                    !state.IsLocationEnabled(entry.LocationName) ||
                    !state.IsLocationInSeed(entry.LocationName))
                {
                    return;
                }

                // The source retains its PersistentBoolItem and UnityEvents. Only
                // the SavedItem reward is replaced, so no vanilla currency or
                // consumable leaks while all native collection choreography
                // and checked-source disappearance remain intact.
                __instance.SetItem(
                    GetProxyItem(entry.LocationName, entry.Type),
                    keepPersistence: true
                );
            }
        }

        [HarmonyPatch(
            typeof(CollectableItemPickup),
            nameof(CollectableItemPickup.SetItem),
            new Type[] { typeof(SavedItem), typeof(bool) }
        )]
        private static class CollectableItemPickupSetItemPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                CollectableItemPickup __instance,
                ref SavedItem newItem
            )
            {
                // Several minor deposits are assigned by an Item Placer
                // after Awake. In that path the Awake-only replacement misses
                // the source, so the vanilla Tool Metal reaches inventory and
                // never reports its AP location. Re-run the same exact-source
                // match at the assignment boundary.
                if (__instance == null || newItem == null)
                {
                    return;
                }

                TryReplaceSourceItem(__instance, ref newItem);
            }
        }

        [HarmonyPatch(typeof(CollectableItemPickup), "CheckActivation")]
        private static class CollectableItemPickupCheckActivationPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                CollectableItemPickup __instance,
                ref SavedItem ___item
            )
            {
                // CheckActivation may run again during native persistence
                // restore, so it also acts as a setup boundary. DoPickupAction
                // remains the final late-binding fallback.
                TryReplaceSourceItem(__instance, ref ___item);
            }
        }

        [HarmonyPatch(
            typeof(CollectableItemPickup),
            "DoPickupAction",
            new Type[] { typeof(bool) }
        )]
        private static class CollectableItemPickupDoPickupActionPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                CollectableItemPickup __instance,
                ref SavedItem ___item
            )
            {
                // This is the final grant boundary. A pickup can
                // finish Awake/Start before the AP room location set arrives,
                // so the earlier setup hooks are only an optimization. Swap
                // the reward again immediately before native TryGet runs.
                TryReplaceSourceItem(__instance, ref ___item);
            }
        }
    }
}
