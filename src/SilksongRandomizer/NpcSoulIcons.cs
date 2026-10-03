using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.U2D;

namespace SilksongRandomizer
{
    internal static class NpcSoulIcons
    {
        private sealed class Portrait
        {
            internal readonly string Asset, Frame, Bundle;
            internal readonly bool Atlas;
            internal Portrait(string asset, string frame, string bundle, bool atlas = false)
            {
                Asset = asset;
                Frame = frame;
                Bundle = bundle;
                Atlas = atlas;
            }
        }

        private static readonly Dictionary<string, string> JournalRecords =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Shakra", "Shakra" },
                { "Conductor Ballador", "Conductor Boss" },
                { "Pinstress", "Pinstress Boss" },
                { "Seth", "Seth" },
                { "Garmond", "Garmond_Zaza" },
                { "Zaza", "Garmond_Zaza" },
            };
        private static readonly Dictionary<string, Portrait> Portraits =
            new Dictionary<string, Portrait>(StringComparer.OrdinalIgnoreCase)
            {
                { "Greyroot", new Portrait("Assets/Collections/Hornet NPCs/Wood Witch Cln Data/Wood Witch Cln.prefab", "WW_idle0000", "tk2dcollections_assets_areashellwood.bundle") },
                { "Pinmaster Plinney", new Portrait("Assets/Collections/Hornet NPCs/Plinney Inside Cln Data/Plinney Inside Cln.prefab", "plinney_inside0000", "tk2dcollections_assets_areabell.bundle") },
                { "Scrounge", new Portrait("Assets/Collections/Hornet NPCs/Relic Dealer Cln Data/Relic Dealer Cln.prefab", "listen_talk0002", "tk2dcollections_assets_areabell.bundle") },
                { "Forge Daughter", new Portrait("Assets/Collections/Hornet NPCs/Forge Daughter Cln Data/Forge Daughter Cln.prefab", "talk_idle0000", "tk2dcollections_assets_areadocks.bundle") },
                { "Twelfth Architect", new Portrait("Assets/Collections/Hornet NPCs/Architect Cln Data/Architect Cln.prefab", "talk_left0005", "tk2dcollections_assets_areaunderstore.bundle") },
                { "Pavo", new Portrait("Assets/Collections/Hornet NPCs/Belltown Greeter Cln Data/Belltown Greeter Cln.prefab", "belltown_greeter_idle0000", "tk2dcollections_assets_areabell.bundle") },
                { "Moss Druid", new Portrait("Assets/Collections/Moss Creep Cln Data/Moss Creep Cln.prefab", "Creep_idle0000", "tk2dcollections_assets_areamoss.bundle") },
                { "Seamstress", new Portrait("Assets/Collections/Hornet NPCs/Seamstress Cln Data/Seamstress Cln.prefab", "idle0000", "tk2dcollections_assets_areawilds.bundle") },
                { "Alchemist Zylotol", new Portrait("Assets/Collections/Hornet NPCs/Blue Scientist Sit Cln Data/Blue Scientist Sit Cln.prefab", "blue_scientist_sit0000", "tk2dcollections_assets_areacrawl.bundle") },
                { "Mort", new Portrait("Assets/Collections/Hornet NPCs/Pilgrims Rest Shop Cln Data/Pilgrims Rest Shop Cln.prefab", "Pilgrims_Rest_Shop0000", "tk2dcollections_assets_areabone.bundle") },
                { "Creige", new Portrait("Assets/Collections/Hornet NPCs/HH Bartender Cln Data/HH Bartender Cln.prefab", "bartender0000", "tk2dcollections_assets_areagreymoor.bundle") },
                { "Nuu", new Portrait("Assets/Collections/Hornet NPCs/Hunter Fan Inside Cln Data/Hunter Fan Inside Cln.prefab", "Hunter_fan_idle0000", "tk2dcollections_assets_areagreymoor.bundle") },
                { "Pebb", new Portrait("Assets/Collections/Hornet NPCs/Bonetown/Bonechurch Shopkeep Cln Data/Bonechurch Shopkeep Cln.prefab", "Bonechurch_shopkeep0008", "tk2dcollections_assets_areamoss.bundle") },
                { "Frey", new Portrait("Assets/Collections/Hornet NPCs/Belltown Shop Cln Data/Belltown Shop Cln.prefab", "belltown_shop_standard0000", "tk2dcollections_assets_areabell.bundle") },
                { "Jubilana", new Portrait("Assets/Collections/Hornet NPCs/City Merchant Cln Data/City Merchant Cln.prefab", "idle0000", "tk2dcollections_assets_areasong.bundle") },
                { "Grindle", new Portrait("Assets/Collections/Thief Cln Data/Thief Cln.prefab", "Thief_idle0000", "thief_assets_all.bundle") },
                { "Mottled Skarr", new Portrait("Assets/Collections/Hornet NPCs/Ant Merchant Cln Data/Ant Merchant Cln.prefab", "AM_idle000", "tk2dcollections_assets_areaant.bundle") },
                { "Loddie", new Portrait("Assets/Collections/Hornet NPCs/Lady Bug Cln Data/Lady Bug Cln.prefab", "Lady_Bug_Large0019", "tk2dcollections_assets_areabone.bundle") },
                { "Lumble", new Portrait("Assets/Collections/Dice Game Cln Data/Dice Game Cln.prefab", "DP_idle0000", "tk2dcollections_assets_areacoral.bundle") },
                { "Ballow", new Portrait("Assets/Collections/Hornet Enemies/Dock Worker Cln Data/Dock Worker Cln.prefab", "DW_idle0000", "tk2dcollections_assets_areadocks.bundle") },
                { "Vaultkeeper Cardinius", new Portrait("Assets/Collections/Hornet NPCs/Librarian Cln Data/Librarian Cln.prefab", "L_idle0000", "tk2dcollections_assets_arealibrary.bundle") },
                { "Caretaker", new Portrait("Assets/Collections/Hornet NPCs/Enclave Pilgrims Cln Data/Enclave Pilgrims Cln.prefab", "EC_idle0000", "tk2dcollections_assets_areaenclave.bundle") },
                { "Sherma", new Portrait("Assets/Collections/Sherma Cln Data/Sherma Cln.prefab", "CP_idle0000", "tk2dcollections_assets_sherma.bundle") },
                { "Huntress", new Portrait("Assets/Collections/Hornet NPCs/Huntress Cln Data/Huntress Cln.prefab", "Huntress0000", "tk2dcollections_assets_areaswamp.bundle") },
                { "Mask Maker", new Portrait("Assets/Collections/Hornet NPCs/Peak Mask Maker Cln Data/Peak Mask Maker Cln.prefab", "MMH0021", "tk2dcollections_assets_areapeak.bundle") },
                { "Sprintmaster Swift", new Portrait("Assets/Collections/Hornet NPCs/Sprintmaster Cln Data/Sprintmaster Cln.prefab", "idle_jog0000", "tk2dcollections_assets_areaaqueduct.bundle") },
                { "Mr Mushroom", new Portrait("Assets/Collections/Mr Mushroom Cln Data/Mr Mushroom Cln.prefab", "mrmush_idle0000", "tk2dcollections_assets_mrmushroom.bundle") },
                { "Mooshka", new Portrait("Assets/Collections/Hornet NPCs/Caravan Troupe Leader Cln Data/Caravan Troupe Leader Cln.prefab", "CL_idle0000", "tk2dcollections_assets_fleacaravan.bundle") },
                { "Gilly", new Portrait("Assets/Collections/Hornet NPCs/Gilly Cln Data/Gilly Cln.prefab", "G_idle0000", "tk2dcollections_assets_areawilds.bundle") },
                { "Tipp", new Portrait("Assets/Collections/Hornet NPCs/Courier Short Cln Data/Courier Short Cln.prefab", "CS_idle0000", "tk2dcollections_assets_areabell.bundle") },
                { "Pill", new Portrait("Assets/Collections/Hornet NPCs/Couriers Packless Cln Data/Couriers Packless Cln.prefab", "courier_packless_idle0000", "tk2dcollections_assets_areabell.bundle") },
                { "Chapel Maid", new Portrait("Assets/Collections/Hornet NPCs/Bonetown/Churchkeeper Cln Data/Churchkeeper Cln.prefab", "Churchkeeper_talk_up000", "tk2dcollections_assets_areatutorial.bundle") },
                { "Bell Hermit", new Portrait("Assets/Collections/Hornet NPCs/Bell Hermit Cln Data/Bell Hermit Cln.prefab", "bell_hermit0009", "tk2dcollections_assets_areabell.bundle") },
                { "Crull", new Portrait("Assets/Collections/Hornet NPCs/Dust Traders Cln Data/Dust Traders Cln.prefab", "Dust_Trader_Large0010", "tk2dcollections_assets_areadust.bundle") },
                { "Benjin", new Portrait("Assets/Collections/Hornet NPCs/Dust Traders Cln Data/Dust Traders Cln.prefab", "dust_trader_small0000", "tk2dcollections_assets_areadust.bundle") },
                { "Flick", new Portrait("Assets/Collections/Hornet NPCs/Bonetown/Fixer Pilgrim Cln Data/Fixer Pilgrim Cln.prefab", "Fixer_idle_stand0000", "tk2dcollections_assets_areabone.bundle") },
                { "Green Prince", new Portrait("Assets/Collections/Hornet NPCs/Green Prince Stand Cln Data/Green Prince Stand Cln.prefab", "Prince_Stand0000", "tk2dcollections_assets_areagreymoor.bundle") },
                { "Kratt", new Portrait("Assets/Collections/Hornet NPCs/Caravan Lech Cln Data/Caravan Lech Cln.prefab", "caravan_lech0000", "tk2dcollections_assets_fleacaravan.bundle") },
                { "Vog", new Portrait("Assets/Collections/Hornet NPCs/Caravan Troupe Hunter Cln Data/Caravan Troupe Hunter Cln.prefab", "idle0000", "tk2dcollections_assets_fleacaravan.bundle") },
                { "Grishkin", new Portrait("Assets/Collections/Hornet NPCs/Caravan Troupe Member Cln Data/Caravan Troupe Member Cln.prefab", "CS_idle0000", "tk2dcollections_assets_fleacaravan.bundle") },
                { "Varga", new Portrait("Assets/Collections/Hornet NPCs/Caravan Troupe Member Cln Data/Caravan Troupe Member Cln.prefab", "CT_idle0000", "tk2dcollections_assets_fleacaravan.bundle") },
                { "Runt", new Portrait("Assets/Collections/Hornet NPCs/Huntress Runt Cln Data/Huntress Runt Cln.prefab", "idle0000", "tk2dcollections_assets_areaswamp.bundle") },
                { "Styx", new Portrait("Assets/Collections/Grub Farmer Cln Data/Grub Farmer Cln.prefab", "idle0000", "tk2dcollections_assets_areadust.bundle") },
                { "Skynx", new Portrait("Assets/Collections/Hornet NPCs/Grub Farmer Mimic Cln Data/Grub Farmer Mimic Cln.prefab", "grub_farmer_mimic0005", "tk2dcollections_assets_areadust.bundle") },
                { "Old Penitent", new Portrait("Assets/Collections/Hornet NPCs/Slab Prisoner Cln Data/Slab Prisoner Cln.prefab", "Slab_prisoner_NPC0015", "tk2dcollections_assets_areaslab.bundle") },
                { "Yarnaby", new Portrait("Assets/Collections/Hornet NPCs/Doctor Fly Cln Data/Doctor Fly Cln.prefab", "fly0000", "tk2dcollections_assets_areabell.bundle") },
                { "Pondcatcher Reed", new Portrait("Assets/Collections/Hornet NPCs/Pilgrim Fisher NPC Cln Data/Pilgrim Fisher NPC Cln.prefab", "idle0000", "tk2dcollections_assets_areashellwood.bundle") },
                { "Mergwin", new Portrait("Assets/Collections/Hornet NPCs/Great Gourmand Cln Data/Great Gourmand Cln.prefab", "servant_idle0000", "tk2dcollections_assets_areasong.bundle") },
                { "Sula", new Portrait("Assets/Collections/Steel Servant Data/Steel Servant.prefab", "idle0000", "tk2dcollections_assets_areabone.bundle") },
                { "Steel Seer Zi", new Portrait("Assets/Collections/Hornet NPCs/Steel Sentinel Cln Data/Steel Sentinel Cln.prefab", "idle", "tk2dcollections_assets_areacoral.bundle") },
                { "Pilby", new Portrait("Assets/Collections/Hornet NPCs/Bonetown/Bonetown Resident Cln Data/Bonetown Resident Cln.prefab", "WP_look_idle0000", "tk2dcollections_assets_areabone.bundle") },
                { "Loam", new Portrait("Assets/Collections/Hornet NPCs/Understore Large Worker Cln Data/Understore Large Worker Cln.prefab", "rest_talk0002", "tk2dcollections_assets_areaunderstore.bundle") },
                { "Great Gourmand", new Portrait("Assets/Collections/Hornet NPCs/Great Gourmand Cln Data/Great Gourmand Cln.prefab", "idle0000", "tk2dcollections_assets_areasong.bundle") },
                { "Fayforn", new Portrait("Assets/Sprites/_Atlases/Fayforn_after_sit.spriteatlas", "after_sit_head_idle0000", "atlases_assets_assets/sprites/_atlases/fayforn_after_sit.spriteatlas.bundle", true) },
                { "Eva", new Portrait("Assets/Sprites/_Atlases/Weaver_lift.spriteatlas", "crest_upgrade_shrine_0001_1_glass", "atlases_assets_assets/sprites/_atlases/weaver_lift.spriteatlas.bundle", true) },
            };
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> RetryAfter = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<string> Queue = new Queue<string>();
        private static readonly Dictionary<string, IResourceLocation> Bundles = new Dictionary<string, IResourceLocation>(StringComparer.OrdinalIgnoreCase);
        private static bool loading, indexed;

        internal static Sprite GetIcon(string itemName)
        {
            const string prefix = "NPC Soul: ";
            if (itemName == null || !itemName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            string name = itemName.Substring(prefix.Length);
            if (JournalRecords.TryGetValue(name, out string recordName))
            {
                EnemyJournalRecord record = EnemyJournalManager.GetRecord(recordName);
                return record != null ? record.IconSprite : null;
            }
            if (Cache.TryGetValue(name, out Sprite cached) && cached != null) return cached;
            if (!Portraits.ContainsKey(name) || RandomizerPlugin.Instance == null || Pending.Contains(name) ||
                (RetryAfter.TryGetValue(name, out float retry) && Time.realtimeSinceStartup < retry)) return null;
            Pending.Add(name);
            Queue.Enqueue(name);
            if (!loading)
            {
                loading = true;
                RandomizerPlugin.Instance.StartCoroutine(LoadQueued());
            }
            return null;
        }

        private static IEnumerator LoadQueued()
        {
            try
            {
                while (Queue.Count > 0)
                {
                    string name = Queue.Dequeue();
                    var routine = Load(name, Portraits[name]);
                    try
                    {
                        while (true)
                        {
                            bool next;
                            try { next = routine.MoveNext(); }
                            catch (Exception ex)
                            {
                                RandomizerPlugin.Log?.LogWarning("Could not load NPC portrait for " + name + ": " + ex.Message);
                                break;
                            }
                            if (!next) break;
                            yield return routine.Current;
                        }
                    }
                    finally
                    {
                        (routine as IDisposable)?.Dispose();
                        Pending.Remove(name);
                        if (!Cache.ContainsKey(name)) RetryAfter[name] = Time.realtimeSinceStartup + 30f;
                    }
                    yield return null;
                }
            }
            finally { loading = false; }
        }

        private static IEnumerator Load(string name, Portrait portrait)
        {
            var init = Addressables.InitializeAsync(false);
            try
            {
                yield return init;
                if (init.Status != AsyncOperationStatus.Succeeded) throw new InvalidOperationException("Asset catalogue is unavailable.");
            }
            finally { if (init.IsValid()) Addressables.Release(init); }
            if (!indexed) IndexBundles();
            string key = portrait.Asset;
            var dependencies = new List<IResourceLocation> { GetBundle(portrait.Bundle) };
            if (!portrait.Atlas)
            {
                dependencies.Add(GetBundle("_monoscripts.bundle"));
                dependencies.Add(GetBundle("shaders_assets_all.bundle"));
                dependencies.Add(GetBundle("_unitybuiltinassets.bundle"));
            }
            var location = new ResourceLocationBase(key, key, typeof(BundledAssetProvider).FullName,
                portrait.Atlas ? typeof(SpriteAtlas) : typeof(GameObject), dependencies.ToArray());
            var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(location);
            try
            {
                yield return handle;
                if (handle.Status != AsyncOperationStatus.Succeeded) throw new InvalidOperationException("Native asset failed to load: " + key);
                if (portrait.Atlas)
                {
                    Sprite sprite = (handle.Result as SpriteAtlas)?.GetSprite(portrait.Frame);
                    if (sprite == null) throw new InvalidOperationException("Missing native sprite " + portrait.Frame);
                    try
                    {
                        Cache[name] = NpcPortrait.Create(name, sprite.texture, sprite.vertices, sprite.uv,
                            sprite.triangles.Select(i => (int)i).ToArray(), false);
                    }
                    finally { UnityEngine.Object.Destroy(sprite); }
                }
                else
                {
                    var prefab = handle.Result as GameObject;
                    var frame = prefab?.GetComponentsInChildren<tk2dSpriteCollectionData>(true)
                        .SelectMany(c => c.inst.spriteDefinitions).FirstOrDefault(d => d.name == portrait.Frame);
                    var material = frame?.materialInst ?? frame?.material;
                    if (frame == null || material == null || material.mainTexture == null)
                        throw new InvalidOperationException("Missing native frame " + portrait.Frame);
                    Cache[name] = NpcPortrait.Create(name, material.mainTexture,
                        frame.positions.Select(v => new Vector2(v.x, v.y)).ToArray(), frame.uvs, frame.indices,
                        material.shader != null && material.shader.name.IndexOf("premul", StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }
            finally { if (handle.IsValid()) Addressables.Release(handle); }
        }

        private static IResourceLocation GetBundle(string name)
        {
            if (!Bundles.TryGetValue(name, out var location))
                throw new InvalidOperationException("Missing native bundle " + name);
            return location;
        }

        private static void IndexBundles()
        {
            var wanted = new HashSet<string>(Portraits.Values.Select(p => p.Bundle), StringComparer.OrdinalIgnoreCase)
            {
                "_monoscripts.bundle", "shaders_assets_all.bundle", "_unitybuiltinassets.bundle"
            };
            foreach (var locator in Addressables.ResourceLocators)
            foreach (object key in locator.Keys)
            {
                if (!locator.Locate(key, typeof(IAssetBundleResource), out var locations)) continue;
                foreach (var location in locations)
                {
                    string path = location.InternalId.Replace('\\', '/');
                    foreach (string bundle in wanted)
                    {
                        string suffix = bundle[0] == '_' ? bundle : "/" + bundle;
                        if (!path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                        if (Bundles.TryGetValue(bundle, out var previous) && previous.InternalId != location.InternalId)
                            throw new InvalidOperationException("Ambiguous native bundle " + bundle);
                        Bundles[bundle] = location;
                    }
                }
            }
            indexed = true;
        }
    }
}
