using System;
using System.Collections.Generic;
using System.Linq;

namespace SilksongRandomizer
{
    internal static class MinorPickupManifest
    {
        internal sealed class Entry
        {
            internal readonly string LocationName;
            internal readonly string SceneName;
            internal readonly string AssetName;
            internal readonly float X;
            internal readonly float Y;
            internal readonly string HierarchyPath;
            internal readonly ItemType Type;

            internal Entry(
                string locationName,
                string sceneName,
                string assetName,
                float x,
                float y,
                string hierarchyPath = "",
                ItemType type = ItemType.Resource
            )
            {
                LocationName = locationName;
                SceneName = sceneName;
                AssetName = assetName;
                X = x;
                Y = y;
                HierarchyPath = hierarchyPath ?? string.Empty;
                Type = type;
            }
        }

        internal static readonly Entry[] Entries =
        {
            new Entry("Putrified Ducts - Frayed Rosary String", "aqueduct_01", "Rosary_Set_Frayed", 256.19f, 14.67f),
            new Entry("Fleatopia - Rosary Necklace", "aqueduct_05_festival", "Rosary_Set_Medium", 152.783f, 16.2396f),
            new Entry("Fleatopia - Rosary String", "aqueduct_05_festival", "Rosary_Set_Small", 154.043f, 15.9796f),
            new Entry("Memorium - Shard Bundle", "arborium_11", "Shard Pouch", 211.22f, 10.27f),
            new Entry("The Marrow (Flea Caravan Passage) - Frayed Rosary String", "bone_10", "Rosary_Set_Frayed", 98.9849f, 41.3206f),
            new Entry("Deep Docks - Frayed Rosary String", "bone_east_04b", "Rosary_Set_Frayed", 6.462f, 87.1857f),
            new Entry("Far Fields - Rosary String", "bone_east_14", "Rosary_Set_Small", 13.82f, 7.461f),
            new Entry("Cogwork Core - Pristine Core", "cog_07", "Pristine Core", 0f, 0f, "Battle Scene Test/Battle Scene/Wave 2 - Item/Item Placer/Collectable Item Pickup"),
            new Entry("Cogwork Core - Shard Bundle", "cog_10", "Shard Pouch", 6.78f, 44.67f),
            new Entry("Blasted Steps - Frayed Rosary String", "coral_03", "Rosary_Set_Frayed", 38.64f, 5.53f),
            new Entry("Blasted Steps - Beast Shard", "coral_36", "Great Shard", 20.0542f, 48.5738f),
            new Entry("Wormways - Frayed Rosary String", "crawl_02", "Rosary_Set_Frayed", 3.14f, 142.3655f),
            new Entry("Deep Docks - Shard Bundle #1", "dock_02", "Shard Pouch", 38.79f, 3.5175f),
            new Entry("Deep Docks - Beast Shard", "dock_11", "Great Shard", 100.63f, 6.28f),
            new Entry("Sinner's Road - Frayed Rosary String", "dust_01", "Rosary_Set_Frayed", 76.43f, 2.7178f),
            new Entry("Sinner's Road - Shard Bundle", "dust_06", "Shard Pouch", 26.11f, 96.25f),
            new Entry("Greymoor - Shard Bundle #1", "greymoor_05", "Shard Pouch", 59.01f, 62.74f),
            new Entry("Greymoor - Shard Bundle #2", "greymoor_12", "Shard Pouch", 61.29f, 14.4f),
            new Entry("Greymoor - Frayed Rosary String #1", "greymoor_15", "Rosary_Set_Frayed", 64.41f, 24.21f),
            new Entry("Greymoor - Frayed Rosary String #2", "greymoor_15b", "Rosary_Set_Frayed", 91.86f, 35.26f),
            new Entry("High Halls - Pale Rosary Necklace", "hang_06_bank", "Rosary_Set_Huge_White", 73.41f, 35.3895f),
            new Entry("High Halls - Frayed Rosary String", "hang_16", "Rosary_Set_Frayed", 68.15f, 6.38f),
            new Entry("Whispering Vaults - Heavy Rosary Necklace", "library_02", "Rosary_Set_Large", 76.437f, 48.02f),
            new Entry("Songclave - Heavy Rosary Necklace", "library_09", "Rosary_Set_Large", 14.08f, 36.42f),
            new Entry("Whispering Vaults - Shard Bundle", "library_12", "Shard Pouch", 126.39f, 24.4143f),
            new Entry("Bone Bottom (Silkspear Passage) - Frayed Rosary String", "mosstown_02", "Rosary_Set_Frayed", 35.32f, 40.27f),
            new Entry("Deep Docks - Shard Bundle #2", "room_forge", "Shard Pouch", 5.08f, 37.38f),
            new Entry("Bilewater - Frayed Rosary String", "shadow_02", "Rosary_Set_Frayed", 49.21f, 161.48f),
            new Entry("Shellwood - Frayed Rosary String", "shellwood_01", "Rosary_Set_Frayed", 48.99f, 23.27f),
            new Entry("Shellwood - Rosary String #1", "shellwood_01b", "Rosary_Set_Small", 33.13f, 72.38f),
            new Entry("Shellwood - Shard Bundle", "shellwood_13", "Shard Pouch", 82.2928f, 67.2101f),
            new Entry("Shellwood - Rosary String #2", "shellwood_25", "Rosary_Set_Small", 83.3f, 25.4172f),
            new Entry("The Slab - Frayed Rosary String #1", "slab_02", "Rosary_Set_Frayed", 53.4572f, 21.2107f),
            new Entry("The Slab - Shard Bundle", "slab_04", "Shard Pouch", 9.4863f, 15.6907f),
            new Entry("The Slab - Frayed Rosary String #2", "slab_18", "Rosary_Set_Frayed", 17.2344f, 21.2003f),
            new Entry("The Slab - Frayed Rosary String #3", "slab_22", "Rosary_Set_Frayed", 28.3f, 30.39f),
            new Entry("Choral Chambers - Heavy Rosary Necklace", "song_04", "Rosary_Set_Large", 121.49f, 37.57f),
            new Entry("Choral Chambers - Rosary Necklace #1", "song_07", "Rosary_Set_Medium", 3.83f, 4.91f),
            new Entry("Choral Chambers - Rosary Necklace #2", "song_09", "Rosary_Set_Medium", 39.38f, 8.2749f),
            new Entry("Moss Grotto - Frayed Rosary String", "tut_01", "Rosary_Set_Frayed", 43.96f, 20.43f),
            new Entry("Underworks - Shard Bundle #1", "under_03", "Shard Pouch", 4.5876f, 6.2287f),
            new Entry("Underworks - Frayed Rosary String #1", "under_07c", "Rosary_Set_Frayed", 75.57f, 76.08f),
            new Entry("Underworks - Frayed Rosary String #2", "under_12", "Rosary_Set_Frayed", 33.13f, 10.29f),
            new Entry("Underworks - Pristine Core", "under_17", "Pristine Core", 75.44f, 23.37f),
            new Entry("Underworks - Shard Bundle #2", "under_18", "Shard Pouch", 34.14f, 34.3f),
            new Entry("Wisp Thicket - Rosary Necklace", "wisp_02", "Rosary_Set_Medium", 111.32f, 37.99f),
            new Entry("Greymoor - Frayed Rosary String #3", "wisp_03", "Rosary_Set_Frayed", 62.89f, 35.37f),

            // The four vanilla Simple Key acquisitions become AP checks. The
            // reward is replaced at the SavedItem boundary, preserving each
            // source's native one-time persistence without granting a
            // fungible key that could be spent on the wrong door.
            new Entry(
                "Simple Key: Roachkeeper",
                "dust_06",
                "Simple Key",
                6.01f,
                179.63f,
                "Roachkeeper Key Control/Collectable Item SimpleKey",
                ItemType.SimpleKey
            ),
            new Entry(
                "Simple Key: Sands of Karak East Bench",
                "bellshrine_coral",
                "Simple Key",
                28.32f,
                16.54f,
                "",
                ItemType.SimpleKey
            ),

            // Physical Memory Lockets. Shops and the Volatile Flintbeetles
            // quest/fallback are registered by their dedicated source hooks.
            new Entry(
                "Memory Locket: Hunter's March",
                "ant_20",
                "Crest Socket Unlocker",
                147.39f,
                13.79f,
                "Enemy Break Cage (2)/Corpse/Collectable Item Pickup",
                ItemType.MemoryLocket
            ),
            new Entry("Memory Locket: Greymoor", "greymoor_16", "Crest Socket Unlocker", 130.90f, 51.48f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: Halfway Home", "halfway_01", "Crest Socket Unlocker", 8.292786f, 13.00f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: Bellhart Roof", "belltown", "Crest Socket Unlocker", 59.168686f, 66.165f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: The Marrow", "bone_18", "Crest Socket Unlocker", 38.15f, 20.51f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: Choral Chambers", "bellway_city", "Crest Socket Unlocker", 67.57f, 24.29f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: Wormways", "crawl_09", "Crest Socket Unlocker", 130.49f, 3.34f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: Blasted Steps", "coral_02", "Crest Socket Unlocker", 202.32f, 43.56f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: Underworks", "under_08", "Crest Socket Unlocker", 61.07f, 16.47f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: Whispering Vaults", "library_08", "Crest Socket Unlocker", 105.34f, 33.48f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: Bilewater West", "shadow_20", "Crest Socket Unlocker", 17.713f, 23.86f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: Deep Docks", "dock_13", "Crest Socket Unlocker", 15.26f, 3.29f, "", ItemType.MemoryLocket),
            new Entry(
                "Memory Locket: Bilewater East",
                "shadow_27",
                "Crest Socket Unlocker",
                195.09f,
                11.76f,
                "Breakable Hang Sack Memory Locket/Corpse Pilgrim 03/" +
                "Sack Corpse Pickup",
                ItemType.MemoryLocket
            ),
            new Entry("Memory Locket: The Slab", "slab_cell_quiet", "Crest Socket Unlocker", 42.441864f, 30.40f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: Memorium", "arborium_05", "Crest Socket Unlocker", 3.95f, 7.27f, "", ItemType.MemoryLocket),
            new Entry("Memory Locket: Far Fields (Act 3)", "bone_east_25", "Crest Socket Unlocker", 151.952515f, 6.346909f, "", ItemType.MemoryLocket),
            // The Coral_23 parent is mirrored on X, so this uses the composed
            // runtime position.
            new Entry(
                "Memory Locket: Sands of Karak",
                "coral_23",
                "Crest Socket Unlocker",
                90.7400055f,
                50.40f,
                "GameObject (3)/Collectable Item Pickup",
                ItemType.MemoryLocket
            ),

            // Physical Craftmetals. Pebb/Grindle and Jubilana use the common
            // shop hook and therefore are not duplicated here.
            new Entry("Craftmetal: The Marrow", "bone_07", "Tool Metal", 41.31f, 4.98f, "Explode Sequence/tool_metal_deposit/Collectable Item Pickup - Tool Metal", ItemType.Craftmetal),
            new Entry("Craftmetal: Deep Docks", "dock_03", "Tool Metal", 7.70f, 81.89f, "City Shard Chest/Item/Collectable Item Pickup", ItemType.Craftmetal),
            new Entry("Craftmetal: Blasted Steps", "coral_32", "Tool Metal", 74.51f, 9.06f, "tool_metal_deposit/Collectable Item Pickup - Tool Metal", ItemType.Craftmetal),
            new Entry("Craftmetal: Underworks", "under_19b", "Tool Metal", 7.57f, 7.18f, "tool_metal_deposit/Collectable Item Pickup - Tool Metal", ItemType.Craftmetal),
            new Entry("Craftmetal: Putrified Ducts", "aqueduct_05", "Tool Metal", 328.90f, 16.35f, "tool_metal_deposit/Collectable Item Pickup - Tool Metal", ItemType.Craftmetal),
            new Entry("Craftmetal: Wisp Thicket", "wisp_05", "Tool Metal", 46.40615f, 59.93595f, "sc_cart_plat/Art/tool_metal_deposit/Collectable Item Pickup - Tool Metal", ItemType.Craftmetal),
        };

        internal static Location[] AppendTo(IEnumerable<Location> existing)
        {
            return existing.Concat(
                Entries.Select(entry =>
                {
                    Entry capturedEntry = entry;
                    return new Location(
                        capturedEntry.LocationName,
                        capturedEntry.Type,
                        capturedEntry.Type == ItemType.Craftmetal
                            ? () => IsCraftmetalCollected(capturedEntry)
                            : (Func<bool>)null
                    );
                })
            ).ToArray();
        }

        private static bool IsCraftmetalCollected(Entry entry)
        {
            if (entry == null ||
                string.IsNullOrEmpty(entry.SceneName) ||
                string.IsNullOrEmpty(entry.HierarchyPath))
            {
                return false;
            }

            SceneData sceneData = SceneData.instance;
            if (sceneData == null)
            {
                return false;
            }

            int separator = entry.HierarchyPath.LastIndexOf('/');
            string persistentBoolId = separator >= 0
                ? entry.HierarchyPath.Substring(separator + 1)
                : entry.HierarchyPath;
            string persistentSceneName =
                char.ToUpperInvariant(entry.SceneName[0]) +
                entry.SceneName.Substring(1);
            return sceneData.PersistentBools.GetValueOrDefault(
                persistentSceneName,
                persistentBoolId
            );
        }
    }
}
