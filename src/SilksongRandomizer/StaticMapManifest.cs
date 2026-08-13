using System;
using System.Collections.Generic;

namespace SilksongRandomizer
{
    internal static class StaticMapManifest
    {
        internal const string CradleLocationName =
            "Map Purchase: The Cradle";

        internal sealed class Source
        {
            internal readonly string SceneName;
            internal readonly string HierarchyPath;
            internal readonly float X;
            internal readonly float Y;
            internal readonly float SceneWidth;
            internal readonly float SceneHeight;
            internal readonly bool IsActThreeFallback;

            internal Source(
                string sceneName,
                string hierarchyPath,
                float x,
                float y,
                float sceneWidth,
                float sceneHeight,
                bool isActThreeFallback = false
            )
            {
                SceneName = sceneName;
                HierarchyPath = hierarchyPath;
                X = x;
                Y = y;
                SceneWidth = sceneWidth;
                SceneHeight = sceneHeight;
                IsActThreeFallback = isActThreeFallback;
            }
        }

        internal sealed class Entry
        {
            internal readonly string AssetName;
            internal readonly string LocationName;
            internal readonly string ItemName;
            internal readonly string PlayerDataBool;
            internal readonly Source[] Sources;

            internal Entry(
                string assetName,
                string locationName,
                string itemName,
                string playerDataBool,
                params Source[] sources
            )
            {
                AssetName = assetName;
                LocationName = locationName;
                ItemName = itemName;
                PlayerDataBool = playerDataBool;
                Sources = sources ?? Array.Empty<Source>();
            }

            internal bool HasSourceScene(string sceneName)
            {
                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    return false;
                }

                foreach (Source source in Sources)
                {
                    if (source != null && string.Equals(
                            source.SceneName,
                            sceneName,
                            StringComparison.OrdinalIgnoreCase
                        ))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal static readonly Entry[] Entries =
        {
            new Entry(
                "Weavehome Map",
                "Map Pickup: Weavenest Atla",
                "Map: Weavenest Atla",
                "HasWeavehomeMap",
                new Source(
                    "Weave_12",
                    "weaver_harp_sign_map/Get Map Inspect",
                    14.018001f,
                    11.054f,
                    55f,
                    25f
                )
            ),
            new Entry(
                "Song Gate Map",
                "Map Purchase: Grand Gate",
                "Map: Grand Gate",
                "HasSongGateMap",
                new Source(
                    "Song_19_entrance",
                    "Map Machine (1)",
                    40.099998f,
                    7.008f,
                    75f,
                    109f
                )
            ),
            new Entry(
                "Understore Map",
                "Map Pickup: Underworks",
                "Map: Underworks",
                "HasCitadelUnderstoreMap",
                new Source(
                    "Under_16",
                    "map_collectable/Understore Map Inspect",
                    65.095837f,
                    35.326239f,
                    80f,
                    60f
                )
            ),
            new Entry(
                "Halls Map",
                "Map Purchase: Choral Chambers",
                "Map: Choral Chambers",
                "HasHallsMap",
                new Source(
                    "Bellway_City",
                    "Map Machine",
                    102.589996f,
                    10.04f,
                    112f,
                    36f
                ),
                new Source(
                    "Song_01b",
                    "Map Machine (2)",
                    102.720039f,
                    2.859291f,
                    124f,
                    19f
                )
            ),
            new Entry(
                "Library Map",
                "Map Purchase: Whispering Vaults",
                "Map: Whispering Vaults",
                "HasLibraryMap",
                new Source(
                    "Library_04",
                    "Map Machine (1)",
                    10.27f,
                    205.830002f,
                    67f,
                    220f
                )
            ),
            new Entry(
                "Ward Map",
                "Map Purchase: Whiteward",
                "Map: Whiteward",
                "HasWardMap",
                new Source(
                    "Ward_01",
                    "Map Machine (1)",
                    54.509998f,
                    19.98f,
                    75f,
                    111f
                )
            ),
            new Entry(
                "Cog Map",
                "Map Pickup: Cogwork Core",
                "Map: Cogwork Core",
                "HasCogMap",
                new Source(
                    "Cog_Bench",
                    "Group/Collectable Item Pickup Child",
                    26.264f,
                    29.038498f,
                    45f,
                    40f
                )
            ),
            new Entry(
                "Arborium Map",
                "Map Purchase: Memorium",
                "Map: Memorium",
                "HasArboriumMap",
                new Source(
                    "Arborium_11",
                    "Map Machine",
                    13.35f,
                    11.16f,
                    222f,
                    61f
                )
            ),
            new Entry(
                "Hang Map",
                "Map Purchase: High Halls",
                "Map: High Halls",
                "HasHangMap",
                new Source(
                    "Hang_06b",
                    "new_scene/Map Machine",
                    51f,
                    3.067242f,
                    62f,
                    22f
                )
            ),
            new Entry(
                "Slab Map",
                "Map Pickup: The Slab",
                "Map: The Slab",
                "HasSlabMap",
                new Source(
                    "Slab_20",
                    "Slab Map Inspect",
                    86.57f,
                    14.07f,
                    94f,
                    24f
                )
            ),
            new Entry(
                "Aqueduct Map",
                "Map Pickup: Putrified Ducts",
                "Map: Putrified Ducts",
                "HasAqueductMap",
                new Source(
                    "Aqueduct_07",
                    "Aqueduct Map Inspect",
                    28.93f,
                    28.98f,
                    41f,
                    40f
                )
            ),
            new Entry(
                "Cradle Map",
                CradleLocationName,
                "Map: The Cradle",
                "HasCradleMap",
                new Source(
                    "Cradle_02",
                    "Group/Map Machine (1)",
                    54.080002f,
                    67.167824f,
                    81f,
                    110f
                ),
                new Source(
                    "Tube_Hub",
                    "Black Thread States/Black Thread World/" +
                    "Collectable Item Pickup",
                    27.035999f,
                    29.396001f,
                    135f,
                    145f,
                    true
                )
            ),
            new Entry(
                "Clover Map",
                "Map Pickup: Verdania",
                "Map: Verdania",
                "HasCloverMap",
                new Source(
                    "Clover_20",
                    "Collectable Item Pickup",
                    133.55f,
                    16.2774963f,
                    150f,
                    47f
                )
            ),
            new Entry(
                "Abyss Map",
                "Map Pickup: The Abyss",
                "Map: The Abyss",
                "HasAbyssMap",
                new Source(
                    "Abyss_12",
                    "Get Map Inspect",
                    13.940001f,
                    26.83f,
                    33f,
                    40f
                )
            ),
        };

        private static readonly Dictionary<string, Entry> EntriesByAsset =
            BuildAssetLookup();

        private static readonly Dictionary<string, Entry> EntriesByLocation =
            BuildLocationLookup();

        internal static bool TryGetByAssetName(
            string assetName,
            out Entry entry
        )
        {
            return EntriesByAsset.TryGetValue(
                assetName ?? string.Empty,
                out entry
            );
        }

        internal static bool TryGetByLocationName(
            string locationName,
            out Entry entry
        )
        {
            return EntriesByLocation.TryGetValue(
                locationName ?? string.Empty,
                out entry
            );
        }

        private static Dictionary<string, Entry> BuildAssetLookup()
        {
            Dictionary<string, Entry> result =
                new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (Entry entry in Entries)
            {
                result.Add(entry.AssetName, entry);
            }
            return result;
        }

        private static Dictionary<string, Entry> BuildLocationLookup()
        {
            Dictionary<string, Entry> result =
                new Dictionary<string, Entry>(
                    StringComparer.OrdinalIgnoreCase
                );
            foreach (Entry entry in Entries)
            {
                result.Add(entry.LocationName, entry);
            }
            return result;
        }
    }
}
