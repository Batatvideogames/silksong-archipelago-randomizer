using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilksongRandomizer
{
    /// <summary>
    /// Stable physical identities for collectible sources which are not part
    /// of the original randomizer manifest. Runtime hooks use
    /// scene + hierarchy/position identities rather than acquisition order.
    /// </summary>
    internal static class CollectibleSourceManifest
    {
        internal sealed class DirectPickupEntry
        {
            internal readonly string LocationName;
            internal readonly ItemType Type;
            internal readonly string SceneName;
            internal readonly string NativeItemName;
            internal readonly string HierarchyPath;
            internal readonly float X;
            internal readonly float Y;

            internal DirectPickupEntry(
                string locationName,
                ItemType type,
                string sceneName,
                string nativeItemName,
                string hierarchyPath,
                float x,
                float y)
            {
                LocationName = locationName;
                Type = type;
                SceneName = sceneName;
                NativeItemName = nativeItemName ?? string.Empty;
                HierarchyPath = hierarchyPath ?? string.Empty;
                X = x;
                Y = y;
            }
        }

        internal sealed class CocoonEntry
        {
            internal readonly string LocationName;
            internal readonly string SceneName;
            internal readonly float X;
            internal readonly float Y;

            internal CocoonEntry(
                string locationName,
                string sceneName,
                float x,
                float y)
            {
                LocationName = locationName;
                SceneName = sceneName;
                X = x;
                Y = y;
            }
        }

        internal sealed class CrawPinEntry
        {
            internal readonly string SceneName;
            internal readonly float X;
            internal readonly float Y;

            internal CrawPinEntry(string sceneName, float x, float y)
            {
                SceneName = sceneName;
                X = x;
                Y = y;
            }
        }

        internal sealed class PollipHeartEntry
        {
            internal readonly string LocationName;
            internal readonly string SceneName;
            internal readonly float X;
            internal readonly float Y;

            internal PollipHeartEntry(
                string locationName,
                string sceneName,
                float x,
                float y)
            {
                LocationName = locationName;
                SceneName = sceneName;
                X = x;
                Y = y;
            }
        }

        internal const string VolatileFlintbeetlesLocket =
            "Memory Locket: Volatile Flintbeetles";
        internal const string CrawSummons = "Craw Summons";

        // Slab_16 contains two mutually exclusive battle variants plus the
        // already-cleared-room recovery copy.  All three represent one Key of
        // Heretic source and share one AP location.
        internal static readonly DirectPickupEntry[] DirectPickups =
        {
            new DirectPickupEntry(
                "Key of Indolent",
                ItemType.MajorKey,
                "Slab_14",
                string.Empty,
                "slab_item_chain/breakable/Collectable Item Pickup (1)",
                12.900001f,
                6.892000f
            ),
            new DirectPickupEntry(
                "Key of Heretic",
                ItemType.MajorKey,
                "Slab_16",
                string.Empty,
                "Event Control/Battle Cloaked Scene/Wave 5 - Item/" +
                    "Item Placer/Collectable Item Pickup",
                25.540001f,
                23.670000f
            ),
            new DirectPickupEntry(
                "Key of Heretic",
                ItemType.MajorKey,
                "Slab_16",
                string.Empty,
                "Event Control/Battle Cloakless Scene/Wave 9 - Item/" +
                    "Item Placer/Collectable Item Pickup",
                25.540001f,
                23.670000f
            ),
            new DirectPickupEntry(
                "Key of Heretic",
                ItemType.MajorKey,
                "Slab_16",
                string.Empty,
                "Event Control/Battle Completed Scene/" +
                    "Collectable Item Pickup Return",
                25.450001f,
                18.200000f
            ),
            new DirectPickupEntry(
                "Key of Apostate",
                ItemType.MajorKey,
                "Aqueduct_04",
                string.Empty,
                "Breakable_cage/Collectable Item Pickup Slab Key",
                7.570004f,
                38.651804f
            ),
            new DirectPickupEntry(
                "White Key",
                ItemType.MajorKey,
                "Song_Enclave",
                "Ward Key",
                "Black Thread States/Normal World/Enclave States/" +
                    "Ward Key Scene/Collectable Item Pickup",
                102.075996f,
                6.290000f
            ),
            new DirectPickupEntry(
                "Surgeon's Key",
                ItemType.MajorKey,
                "Ward_07",
                "Ward Boss Key",
                "Group/Junk Hatch/Collectable Item Pickup",
                18.224485f,
                6.969246f
            ),
            new DirectPickupEntry(
                "Surgeon's Key",
                ItemType.MajorKey,
                "Ward_07",
                "Ward Boss Key",
                "Group/Junk Hatch/Return Corpse/Collectable Item Pickup",
                13.134485f,
                7.159246f
            ),
            new DirectPickupEntry(
                VolatileFlintbeetlesLocket,
                ItemType.MemoryLocket,
                "Bone_10",
                "Crest Socket Unlocker",
                "Black Thread States Thread Only Variant/Black Thread World/" +
                    "Rock Rollers Quest Not Completed/" +
                    "Collectable Item Pickup Locket",
                18.810000f,
                16.249999f
            ),
        };

        internal static readonly CocoonEntry[] SilkeaterCocoons =
        {
            new CocoonEntry("Silkeater: Deep Docks", "Dock_14", 23.340000f, 9.380000f),
            new CocoonEntry("Silkeater: Greymoor", "Greymoor_04", 19.420000f, 145.690002f),
            new CocoonEntry("Silkeater: Exhaust Organ", "Organ_01", 135.529999f, 40.369999f),
            new CocoonEntry("Silkeater: Choral Chambers West", "Song_24", 52.990002f, 21.530001f),
            new CocoonEntry("Silkeater: Choral Chambers East", "Song_09b", 151.970001f, 139.339996f),
            new CocoonEntry("Silkeater: Whispering Vaults", "Library_14", 54.509998f, 17.260000f),
            new CocoonEntry("Silkeater: Whiteward", "Ward_04", 24.196329f, 22.264452f),
            new CocoonEntry("Silkeater: The Cradle", "Tube_Hub", 20.350000f, 8.780000f),
            new CocoonEntry("Silkeater: Blasted Steps", "Coral_37", 19.110069f, 12.700000f),
        };

        internal static readonly CrawPinEntry[] CrawPins =
        {
            new CrawPinEntry("Belltown", 65.720001f, 7.360000f),
            new CrawPinEntry("Bellway_03", 44.639999f, 9.520000f),
            new CrawPinEntry("Bellway_Shadow", 33.317677f, 22.490593f),
            new CrawPinEntry("Bone_East_27", 27.059999f, 47.389999f),
            new CrawPinEntry("Dust_10", 118.415359f, 44.107395f),
            new CrawPinEntry("Dust_11", 106.651354f, 3.749151f),
            new CrawPinEntry("Dust_11", 107.769997f, 3.596507f),
            new CrawPinEntry("Mosstown_03", 17.799999f, 56.639999f),
            new CrawPinEntry("Shellwood_01b", 18.995138f, 36.516193f),
            new CrawPinEntry("Shellwood_08c", 10.940000f, 4.770000f),
            new CrawPinEntry("Wisp_04", 53.773636f, 15.503460f),
        };

        // Each listed scene contains one Shell Flower source. Its reward FSM
        // is the child at
        // purple_flower_set/Big Flower/Nectar Pickup. Scene + object name +
        // full hierarchy identify the source. Position is not part of matching
        // because several
        // flower roots apply rotation or negative scale. The coordinates are
        // Big Flower parent anchors retained for diagnostics.
        // Both Big Flower and Nectar Pickup own a PersistentBoolItem with
        // SceneData ID "Nectar Pickup".
        internal static readonly PollipHeartEntry[] PollipHearts =
        {
            new PollipHeartEntry(
                "Pollip Heart: Shellwood #1",
                "Shellwood_02",
                73.959999f,
                81.149998f
            ),
            new PollipHeartEntry(
                "Pollip Heart: Shellwood #2",
                "Shellwood_20",
                42.419998f,
                36.059998f
            ),
            new PollipHeartEntry(
                "Pollip Heart: Shellwood #3",
                "Shellwood_10",
                9.409075f,
                28.813651f
            ),
            new PollipHeartEntry(
                "Pollip Heart: Shellwood #4",
                "Shellwood_26",
                32.206911f,
                84.480610f
            ),
            new PollipHeartEntry(
                "Pollip Heart: Shellwood #5",
                "Shellwood_15",
                13.255055f,
                7.882488f
            ),
            new PollipHeartEntry(
                "Pollip Heart: Shellwood #6",
                "Shellwood_01",
                92.345070f,
                84.739197f
            ),
        };

        private static readonly Dictionary<string, string> MossberryByScene =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Tut_01b", "Mossberry: Moss Grotto #1" },
                { "Tut_02", "Mossberry: Moss Grotto #2" },
                { "Bonetown", "Mossberry: Bone Bottom" },
                { "Bone_05b", "Mossberry: Mosshome" },
                { "Bonegrave", "Mossberry: Bonegrave" },
                { "Weave_03", "Mossberry: Weavenest Atla" },
                { "Arborium_04", "Mossberry: Memorium" },
            };

        internal static bool TryGetMossberryLocation(
            string sceneName,
            out string locationName)
        {
            return MossberryByScene.TryGetValue(
                sceneName ?? string.Empty,
                out locationName
            );
        }

        internal static PollipHeartEntry FindPollipHeartSource(
            string sceneName,
            string objectName,
            string hierarchyPath)
        {
            if (!string.Equals(
                    objectName,
                    "Nectar Pickup",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    hierarchyPath,
                    "purple_flower_set/Big Flower/Nectar Pickup",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            foreach (PollipHeartEntry entry in PollipHearts)
            {
                if (string.Equals(
                        sceneName,
                        entry.SceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }

        internal static DirectPickupEntry FindDirectPickup(
            string sceneName,
            string nativeItemName,
            string hierarchyPath,
            Vector2 position)
        {
            const float positionToleranceSquared = 0.75f * 0.75f;
            DirectPickupEntry match = null;
            foreach (DirectPickupEntry entry in DirectPickups)
            {
                if (!string.Equals(
                        sceneName,
                        entry.SceneName,
                        StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(entry.NativeItemName) &&
                     !string.Equals(
                         nativeItemName,
                         entry.NativeItemName,
                         StringComparison.Ordinal)))
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
                    positionToleranceSquared;
                if (!hierarchyMatches && !positionMatches)
                {
                    continue;
                }

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

        internal static CocoonEntry FindSilkeaterCocoon(
            string sceneName,
            string objectName,
            Vector2 position)
        {
            if (!string.Equals(
                    objectName,
                    "Silk Grub Large Cocoon",
                    StringComparison.Ordinal))
            {
                return null;
            }

            const float positionToleranceSquared = 0.75f * 0.75f;
            foreach (CocoonEntry entry in SilkeaterCocoons)
            {
                if (!string.Equals(
                        sceneName,
                        entry.SceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                float deltaX = position.x - entry.X;
                float deltaY = position.y - entry.Y;
                if (deltaX * deltaX + deltaY * deltaY <=
                    positionToleranceSquared)
                {
                    return entry;
                }
            }
            return null;
        }

        internal static bool IsCrawPin(
            string sceneName,
            string objectName,
            Vector2 position)
        {
            if (string.IsNullOrEmpty(objectName) ||
                !objectName.StartsWith(
                    "craw_court_summons_pin",
                    StringComparison.Ordinal))
            {
                return false;
            }

            const float positionToleranceSquared = 0.75f * 0.75f;
            foreach (CrawPinEntry entry in CrawPins)
            {
                if (!string.Equals(
                        sceneName,
                        entry.SceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                float deltaX = position.x - entry.X;
                float deltaY = position.y - entry.Y;
                if (deltaX * deltaX + deltaY * deltaY <=
                    positionToleranceSquared)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Adds the sources owned by this manifest to the runtime location
        /// set. Multiple physical variants of one source (for example the
        /// three Key of Heretic states) collapse to one AP
        /// location.
        /// </summary>
        internal static IEnumerable<Location> AppendTo(
            IEnumerable<Location> existing)
        {
            HashSet<string> names = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

            if (existing != null)
            {
                foreach (Location location in existing)
                {
                    if (location == null)
                    {
                        continue;
                    }

                    names.Add(location.Name);
                    yield return location;
                }
            }

            foreach (DirectPickupEntry entry in DirectPickups)
            {
                if (names.Add(entry.LocationName))
                {
                    yield return new Location(
                        entry.LocationName,
                        entry.Type,
                        () => false
                    );
                }
            }

            foreach (CocoonEntry entry in SilkeaterCocoons)
            {
                if (names.Add(entry.LocationName))
                {
                    yield return new Location(
                        entry.LocationName,
                        ItemType.Silkeater,
                        () => false
                    );
                }
            }

            foreach (string locationName in MossberryByScene.Values)
            {
                if (names.Add(locationName))
                {
                    yield return new Location(
                        locationName,
                        ItemType.Mossberry,
                        () => false
                    );
                }
            }

            foreach (PollipHeartEntry entry in PollipHearts)
            {
                if (names.Add(entry.LocationName))
                {
                    yield return new Location(
                        entry.LocationName,
                        ItemType.PollipHeart,
                        () => false
                    );
                }
            }

            if (names.Add(CrawSummons))
            {
                yield return new Location(
                    CrawSummons,
                    ItemType.MajorKey,
                    () => false
                );
            }
        }
    }
}
