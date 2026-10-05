using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        private static readonly ConditionalWeakTable<CollectableItemPickup, MinorPickupManifest.Entry>
            CageSources = new ConditionalWeakTable<CollectableItemPickup, MinorPickupManifest.Entry>();

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

        private static bool IsCageSource(MinorPickupManifest.Entry entry)
        {
            return entry != null &&
                   (entry.LocationName == "The Slab - Shard Bundle" ||
                    entry.LocationName == "The Slab - Frayed Rosary String #1");
        }

        private static MinorPickupManifest.Entry FindPickupSource(
            CollectableItemPickup pickup,
            SavedItem item)
        {
            if (CageSources.TryGetValue(pickup, out MinorPickupManifest.Entry cached) &&
                (item is ArchipelagoLocationItem || item.name == cached.AssetName))
            {
                return cached;
            }

            MinorPickupManifest.Entry entry = FindExactSource(
                pickup.gameObject.scene.name,
                item.name,
                pickup.transform.position,
                Utils.GetHierarchyPath(pickup.transform)
            );
            if (IsCageSource(entry))
            {
                CageSources.Remove(pickup);
                CageSources.Add(pickup, entry);
            }
            return entry;
        }

        private static MinorPickupManifest.Entry FindCageSource(PersistentBoolItem persistent)
        {
            string scene = persistent.gameObject.scene.name;
            if (!string.Equals(scene, "Slab_02", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(scene, "Slab_04", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            CollectableItemPickup pickup = persistent.GetComponent<CollectableItemPickup>();
            if (pickup != null && pickup.Item != null)
            {
                MinorPickupManifest.Entry entry = FindPickupSource(pickup, pickup.Item);
                return IsCageSource(entry) ? entry : null;
            }

            string path = Utils.GetHierarchyPath(persistent.transform);
            foreach (MinorPickupManifest.Entry entry in MinorPickupManifest.Entries)
            {
                if (!IsCageSource(entry) ||
                    !string.Equals(scene, entry.SceneName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string cage = entry.HierarchyPath.Substring(
                    0, entry.HierarchyPath.LastIndexOf("/Broken/", StringComparison.Ordinal));
                if (path == cage + "/Active" || path == cage + "/Active/Collectable Item Fake")
                {
                    return entry;
                }
            }
            return null;
        }

        [HarmonyPatch(typeof(BattleScene), nameof(BattleScene.BattleCompleted))]
        private static class ClappersCoreRecoveryPatch
        {
            [HarmonyPostfix]
            private static void Postfix(BattleScene __instance, bool ___completed)
            {
                if (!___completed || SaveState.Instance?.IsRoomBound != true ||
                    __instance.gameObject.scene.name != "Cog_07" ||
                    Utils.GetHierarchyPath(__instance.transform) != "Battle Scene Test/Battle Scene") return;
                Transform source = __instance.transform.Find("Wave 2 - Item/Item Placer/Collectable Item Pickup");
                CollectableItemPickup pickup = source?.GetComponent<CollectableItemPickup>();
                if (pickup == null || pickup.GetComponent<PersistentBoolItem>() == null) return;
                SavedItem item = pickup.Item;
                TryReplaceSourceItem(pickup, ref item);
                if (item is ArchipelagoLocationItem proxy)
                {
                    if (proxy.LocationName != "Cogwork Core - Pristine Core" || !proxy.CanGetMore()) return;
                }
                else if (item?.name != "Pristine Core") return;
                pickup.SetItem(item, keepPersistence: true);
                source.SetParent(null, true);
                source.position = new Vector3(28.23f, 77.69f, 0.003f);
                source.gameObject.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(PersistentBoolItem), "TryGetValue")]
        private static class CagePersistenceLoadPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                PersistentBoolItem __instance,
                ref PersistentItemData<bool> newItemData,
                ref bool __result)
            {
                if (__instance == null || newItemData == null)
                {
                    return;
                }

                MinorPickupManifest.Entry entry = FindCageSource(__instance);
                SaveState state = SaveState.Instance;
                if (entry == null || state == null ||
                    !state.IsRandomized(entry.Type) ||
                    !state.IsLocationEnabled(entry.LocationName) ||
                    !state.IsLocationInSeed(entry.LocationName))
                {
                    return;
                }

                // Opening the cage marks the native pickup used before collection.
                newItemData.Value = state.IsLocationChecked(entry.LocationName);
                __result = true;
            }
        }

        internal static bool IsResourceCheck(CollectableItemPickup pickup)
        {
            SaveState state = SaveState.Instance;
            if (pickup == null || state?.IsRoomBound != true) return false;
            SavedItem item = pickup.Item;
            if (!(item is ArchipelagoLocationItem)) TryReplaceSourceItem(pickup, ref item);
            return item is ArchipelagoLocationItem proxy && proxy.Type == ItemType.Resource &&
                   state.IsRandomized(proxy.Type) &&
                   state.IsLocationEnabled(proxy.LocationName) &&
                   state.IsLocationInSeed(proxy.LocationName);
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
            MinorPickupManifest.Entry entry = FindPickupSource(pickup, item);
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
                if (nativeItem == null)
                {
                    return;
                }

                MinorPickupManifest.Entry entry = FindPickupSource(__instance, nativeItem);
                if (entry == null || state == null ||
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

        private const float PickupSpeed = 1.5f;
        [ThreadStatic] private static int pickupDepth;
        private static readonly AccessTools.FieldRef<WaitForSeconds, float> WaitSeconds =
            AccessTools.FieldRefAccess<WaitForSeconds, float>("m_Seconds");
        private static readonly ConditionalWeakTable<tk2dSpriteAnimationClip, tk2dSpriteAnimationClip>
            FasterClips = new ConditionalWeakTable<tk2dSpriteAnimationClip, tk2dSpriteAnimationClip>();

        internal static IEnumerator SpeedUp(IEnumerator routine)
        {
            try
            {
                while (true)
                {
                    bool moved;
                    pickupDepth++;
                    try { moved = routine.MoveNext(); }
                    finally { pickupDepth--; }
                    if (!moved) yield break;

                    object step = routine.Current;
                    if (step is WaitForSeconds wait)
                    {
                        float seconds = WaitSeconds(wait);
                        if (Math.Abs(seconds - 0.75f) < 0.001f ||
                            Math.Abs(seconds - 0.5f) < 0.001f)
                        {
                            step = new WaitForSeconds(seconds / PickupSpeed);
                        }
                    }
                    yield return step;
                }
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }

        [HarmonyPatch(typeof(CollectableItemPickup), "Pickup")]
        private static class PickupPatch
        {
            [HarmonyPostfix]
            private static void Postfix(CollectableItemPickup __instance, ref IEnumerator __result)
            {
                if (__result != null && MinorPickupPatches.IsResourceCheck(__instance))
                    __result = SpeedUp(__result);
            }
        }

        [HarmonyPatch(typeof(HeroAnimationController), nameof(HeroAnimationController.GetClip))]
        private static class PickupClipPatch
        {
            [HarmonyPostfix]
            private static void Postfix(string clipName, ref tk2dSpriteAnimationClip __result)
            {
                if (pickupDepth == 0 || __result == null || __result.fps <= 0f) return;
                switch (clipName)
                {
                    case "Collect Normal 1":
                    case "Collect Normal 2":
                    case "Collect Normal 3":
                    case "Collect Stand 1":
                    case "Collect Stand 2":
                    case "Collect Stand 3":
                        __result = FasterClips.GetValue(__result, clip =>
                            new tk2dSpriteAnimationClip(clip) { fps = clip.fps * PickupSpeed });
                        break;
                }
            }
        }
    }
}
