using System;
using System.Collections.Generic;
using System.Linq;

namespace SilksongRandomizer
{
    internal static class LoreTabletManifest
    {
        internal sealed class SourceIdentity
        {
            internal readonly string SceneName;
            internal readonly string HierarchyPath;
            internal readonly string Sheet;
            internal readonly string Key;

            internal SourceIdentity(
                string sceneName,
                string hierarchyPath,
                string sheet,
                string key
            )
            {
                SceneName = sceneName;
                HierarchyPath = hierarchyPath;
                Sheet = sheet;
                Key = key;
            }
        }

        internal sealed class Entry
        {
            internal readonly string LocationName;
            internal readonly string ItemName;
            internal readonly string MarkerSceneName;
            internal readonly float MarkerX;
            internal readonly float MarkerY;
            internal readonly float SceneWidth;
            internal readonly float SceneHeight;
            internal readonly SourceIdentity[] Sources;

            internal Entry(
                string locationName,
                string markerSceneName,
                float markerX,
                float markerY,
                float sceneWidth,
                float sceneHeight,
                params SourceIdentity[] sources
            )
            {
                LocationName = locationName;
                ItemName = "Lore: " + locationName;
                MarkerSceneName = markerSceneName;
                MarkerX = markerX;
                MarkerY = markerY;
                SceneWidth = sceneWidth;
                SceneHeight = sceneHeight;
                Sources = sources ?? Array.Empty<SourceIdentity>();
            }
        }

        private static SourceIdentity Source(
            string sceneName,
            string hierarchyPath,
            string sheet,
            string key
        )
        {
            return new SourceIdentity(
                sceneName,
                hierarchyPath,
                sheet,
                key
            );
        }

        internal static readonly Entry[] Entries =
        {
            new Entry(
                "Abyss - Upper Lore Stone",
                "Abyss_02b",
                148.78f,
                47.05f,
                228f,
                55f,
                Source(
                    "abyss_02b",
                    "Abyss_lore_stone/Inspect Region",
                    "Inspect",
                    "ABYSS_LORE_STONE_TOP"
                )
            ),
            new Entry(
                "Abyss - Lower Lore Stone",
                "Abyss_06",
                42.13f,
                45.82f,
                60f,
                51f,
                Source(
                    "abyss_06",
                    "Abyss_lore_stone/Inspect Region",
                    "Inspect",
                    "ABYSS_LORE_STONE_BASE"
                )
            ),
            new Entry(
                "Putrified Ducts - White Lake Weaver Sign",
                "Aqueduct_05",
                187.51f,
                9.34f,
                332f,
                100f,
                Source(
                    "aqueduct_05_caravan",
                    "Caravan_States/Fleatopia/weaver_harp_sign (2)/Inspect Region",
                    "Inspect",
                    "WEAVE_WHITE_LAKE"
                ),
                Source(
                    "aqueduct_05_festival",
                    "Caravan_States/Flea_Festival_Intro/weaver_harp_sign (3)/Inspect Region",
                    "Inspect",
                    "WEAVE_WHITE_LAKE"
                ),
                Source(
                    "aqueduct_05_pre",
                    "Caravan_States/Pre_Fleatopia/weaver_harp_sign (1)/Inspect Region",
                    "Inspect",
                    "WEAVE_WHITE_LAKE"
                )
            ),
            new Entry(
                "Memorium - Lower Plaque",
                "Arborium_01",
                14.22f,
                19.79f,
                29f,
                151f,
                Source(
                    "arborium_01",
                    "Inspect Region",
                    "Inspect",
                    "ARBORIUM_PLAQUE"
                )
            ),
            new Entry(
                "Memorium - Orders",
                "Arborium_03",
                17.91f,
                98.269997f,
                29f,
                110f,
                Source(
                    "arborium_03",
                    "Inspect Region",
                    "Inspect",
                    "ARBORIUM_ORDERS"
                )
            ),
            new Entry(
                "The Marrow - Entrance Inscription",
                "Bone_01c",
                185.759995f,
                8.38f,
                198f,
                91f,
                Source(
                    "bone_01c",
                    "Inspect Region",
                    "Lore",
                    "MARROW_START_SIGN"
                )
            ),
            new Entry(
                "The Marrow - Pilgrim Diary",
                "Bone_18",
                30.95f,
                7.30f,
                49f,
                33f,
                Source(
                    "bone_18",
                    "Inspect Region",
                    "Inspect",
                    "CURIOUS_PILGRIM_DIARY"
                )
            ),
            new Entry(
                "Far Fields - Weavenest Cindril Inscription",
                "Bone_East_Weavehome",
                58.59f,
                65.450836f,
                205f,
                120f,
                Source(
                    "bone_east_weavehome",
                    "Group/Inspect Region (2)",
                    "Inspect",
                    "WEAVE_WILDS"
                )
            ),
            new Entry(
                "Verdania - Oath Plaque",
                "Clover_10",
                56.01f,
                8.36f,
                136f,
                54f,
                Source(
                    "clover_10",
                    "Plaque_Scene/Plaque/Inspect Region",
                    "Inspect",
                    "CLOVER_OATH_PLAQUE"
                )
            ),
            new Entry(
                "Verdania - Lake Plaque",
                "Clover_18",
                114.080002f,
                9.35f,
                195f,
                80f,
                Source(
                    "clover_18",
                    "Fountains Group/Plaque/Up/Inspect Region",
                    "Inspect",
                    "CLOVER_LAKE_PLAQUE"
                )
            ),
            new Entry(
                "Sands of Karak - Upper Coral Tablet",
                "Coral_25",
                129.485153f,
                85.096017f,
                149f,
                98f,
                Source(
                    "coral_25",
                    "Coral Crust Lore Tablet/Interact",
                    "Lore",
                    "CORAL_CRUST_TAB_1"
                )
            ),
            new Entry(
                "Blasted Steps - Judge Nursery Record",
                "Coral_36",
                17.07f,
                31.98f,
                70f,
                58f,
                Source(
                    "coral_36",
                    "Inspect Region",
                    "Inspect",
                    "JUDGE_NURSERY"
                )
            ),
            new Entry(
                "Sands of Karak - Lower Coral Tablet",
                "Coral_Tower_01",
                24.66f,
                5.85f,
                138f,
                22f,
                Source(
                    "coral_tower_01",
                    "Coral Crust Lore Tablet/Interact",
                    "Lore",
                    "CORAL_CRUST_TAB_2"
                )
            ),
            new Entry(
                "The Cradle - Upper Cage Inscription",
                "Cradle_02b",
                8.26f,
                30.74f,
                48f,
                47f,
                Source(
                    "cradle_02b",
                    "Inspect Region",
                    "Inspect",
                    "CRADLE_CAGE_01"
                )
            ),
            new Entry(
                "The Cradle - Middle Cage Inscription",
                "Cradle_02b",
                7.73f,
                15.83f,
                48f,
                47f,
                Source(
                    "cradle_02b",
                    "Inspect Region (1)",
                    "Inspect",
                    "CRADLE_CAGE_02"
                )
            ),
            new Entry(
                "The Cradle - Lower Cage Inscription",
                "Cradle_02b",
                23.959999f,
                7.51f,
                48f,
                47f,
                Source(
                    "cradle_02b",
                    "Inspect Region (2)",
                    "Inspect",
                    "CRADLE_CAGE_03"
                )
            ),
            new Entry(
                "Greymoor - Orders",
                "Greymoor_03",
                15.49515f,
                128.444397f,
                120f,
                140f,
                Source(
                    "greymoor_03",
                    "Inspect Region",
                    "Inspect",
                    "GREY_ORDERS"
                )
            ),
            new Entry(
                "Greymoor - Cage Record",
                "Greymoor_16",
                56.879997f,
                52.200001f,
                171f,
                60f,
                Source(
                    "greymoor_16",
                    "Inspect Region",
                    "Inspect",
                    "GREY_LORE"
                )
            ),
            new Entry(
                "Greymoor - Nuu's Scrolls",
                "Halfway_01",
                23.60f,
                21.059999f,
                56f,
                32f,
                Source(
                    "halfway_01",
                    "_NPCs/Hunter Fan Control/Nuu_Scrolls/Inspect Region",
                    "Wanderers",
                    "HUNTER_FAN_SCROLL_INSPECT"
                )
            ),
            new Entry(
                "Whispering Vaults - Theatre Desk",
                "Library_13b",
                21.91f,
                7.37f,
                73f,
                25f,
                Source(
                    "library_13b",
                    "Desk Inspect and Quill/Group/desk_inspect/Inspect Region Act 2",
                    "Inspect",
                    "LIBRARY_THEATRE_LINES"
                ),
                Source(
                    "library_13b",
                    "Desk Inspect and Quill/Group/desk_inspect/Inspect Region Act 3",
                    "Inspect",
                    "LIBRARY_THEATRE_LINES_ACT3"
                )
            ),
            new Entry(
                "Mosshome - Moss Plaque",
                "Mosstown_02",
                86.859999f,
                35.179999f,
                160f,
                65f,
                Source(
                    "mosstown_02",
                    "lore_tablet/moss_bone_plaque/Inspect Region",
                    "Inspect",
                    "MOSSTOWN_STONE"
                )
            ),
            new Entry(
                "Mosshome - Weaver Harp Inscription",
                "Mosstown_02",
                86.83778f,
                35.479999f,
                160f,
                65f,
                Source(
                    "mosstown_02",
                    "lore_tablet/weaver_harp_sign/Inspect Region",
                    "Inspect",
                    "WEAVE_HARP_MOSSTOWN"
                )
            ),
            new Entry(
                "Mount Fay - Weaver Inscription",
                "Peak_10",
                15.076999f,
                33.959999f,
                80f,
                50f,
                Source(
                    "peak_10",
                    "Inspect Region (2)",
                    "Inspect",
                    "WEAVE_PEAK"
                )
            ),
            new Entry(
                "Deep Docks - Forge Note",
                "Room_Forge",
                27.41f,
                17.32f,
                140f,
                69f,
                Source(
                    "room_forge",
                    "Inspect Region",
                    "Inspect",
                    "DOCKS_NOTE_1"
                )
            ),
            new Entry(
                "Bilewater - Storeroom Record",
                "Shadow_08",
                38.27f,
                8.47f,
                100f,
                55f,
                Source(
                    "shadow_08",
                    "Inspect Region",
                    "Inspect",
                    "SWAMP_STOREROOM"
                )
            ),
            new Entry(
                "Bilewater - Bilehaven Plaque",
                "Shadow_18",
                94.550003f,
                36.290001f,
                117f,
                45f,
                Source(
                    "shadow_18",
                    "Inspect Region",
                    "Inspect",
                    "BILEHAVEN_PLAQUE"
                )
            ),
            new Entry(
                "Bilewater - Weaver Workshop Scroll",
                "Shadow_Weavehome",
                172.052002f,
                39.34f,
                177f,
                68f,
                Source(
                    "shadow_weavehome",
                    "Inspect Region (1)",
                    "Inspect",
                    "WEAVE_WORKSHOP_SCROLL"
                )
            ),
            new Entry(
                "Shellwood - Shellgrave Inscription",
                "Shellgrave",
                92.799116f,
                0.5f,
                138f,
                22f,
                Source(
                    "shellgrave",
                    "Black Thread States Thread Only Variant/Normal World/Inspect Region",
                    "Inspect",
                    "SHELLGRAVE"
                )
            ),
            new Entry(
                "Shellwood - Weaver Harp Inscription",
                "Shellwood_10",
                40.34f,
                12.210614f,
                79f,
                102f,
                Source(
                    "shellwood_10",
                    "weaver_harp_sign/Inspect Region",
                    "Inspect",
                    "WEAVE_HARP_SHELLWOOD"
                )
            ),
            new Entry(
                "The Slab - Left Orders",
                "Slab_08",
                20.45f,
                13.87f,
                35f,
                40f,
                Source(
                    "slab_08",
                    "dock_b__0051_lore_sign_hang/Inspect Region",
                    "Inspect",
                    "SLAB_ORDERS_1"
                )
            ),
            new Entry(
                "The Slab - Right Orders",
                "Slab_08",
                27.050001f,
                13.87f,
                35f,
                40f,
                Source(
                    "slab_08",
                    "dock_b__0051_lore_sign_hang (1)/Inspect Region",
                    "Inspect",
                    "SLAB_ORDERS_2"
                )
            ),
            new Entry(
                "The Slab - Weaver Gate Inscription",
                "Slab_10c",
                22.812537f,
                12.817337f,
                42f,
                65f,
                Source(
                    "slab_10c",
                    "Break Gate Group/Group/gate/Inspect Region",
                    "Inspect",
                    "SLAB_WEAVER_GATE"
                )
            ),
            new Entry(
                "Moss Grotto - Shaman Storeroom Record",
                "Tut_04",
                79.080002f,
                9.18f,
                87f,
                36f,
                Source(
                    "tut_04",
                    "Inspect Region",
                    "Inspect",
                    "SHAMAN_STORE_ROOM"
                )
            ),
            new Entry(
                "Whiteward - Oath",
                "Ward_07",
                121.709991f,
                10.820001f,
                130f,
                20f,
                Source(
                    "ward_07",
                    "Group/Inspect Region",
                    "Inspect",
                    "WARD_OATH"
                )
            ),
            new Entry(
                "Weavenest Atla - Archive Inscription",
                "Weave_08",
                40.209f,
                30.365993f,
                77f,
                67f,
                Source(
                    "weave_08",
                    "Group/Inspect Region (1)",
                    "Inspect",
                    "WEAVE_ARCHIVE_RIGHT"
                )
            ),
            new Entry(
                "Bellhart - Outer Sign",
                "Belltown_07",
                57.874252f,
                5.059642f,
                70f,
                34f,
                Source(
                    "belltown_07",
                    "Black Thread States Thread Only Variant/Normal World/Inspect Region",
                    "Inspect",
                    "BELLTOWN_OUTER_SIGN"
                )
            ),
            new Entry(
                "Blasted Steps - Shellwood Entrance Sign",
                "Coral_19",
                299.188232f,
                39.795497f,
                427f,
                55f,
                Source(
                    "coral_19",
                    "Black Thread States Thread Only Variant/Normal World/Inspect Region",
                    "Inspect",
                    "CORAL_JUDGEMENT_SIGN"
                )
            ),
            new Entry(
                "The Cradle - Terminus Commandment",
                "Tube_Hub",
                118.279995f,
                39.840001f,
                135f,
                145f,
                Source(
                    "tube_hub",
                    "Black Thread States/Normal World/Inspect Region",
                    "Inspect",
                    "TUBE_HUB_NOTICE"
                )
            ),
        };

        internal static Entry FindExactSource(
            string sceneName,
            string hierarchyPath,
            string sheet,
            string key
        )
        {
            if (string.IsNullOrWhiteSpace(sceneName) ||
                string.IsNullOrWhiteSpace(hierarchyPath) ||
                string.IsNullOrWhiteSpace(sheet) ||
                string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            foreach (Entry entry in Entries)
            {
                foreach (SourceIdentity source in entry.Sources)
                {
                    if (string.Equals(
                            sceneName,
                            source.SceneName,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            hierarchyPath,
                            source.HierarchyPath,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            sheet,
                            source.Sheet,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            key,
                            source.Key,
                            StringComparison.Ordinal))
                    {
                        return entry;
                    }
                }
            }

            return null;
        }

        internal static Location[] AppendTo(IEnumerable<Location> existing)
        {
            return (existing ?? Enumerable.Empty<Location>())
                .Concat(Entries.Select(entry =>
                    new Location(
                        entry.LocationName,
                        ItemType.LoreTablet,
                        null
                    )
                ))
                .ToArray();
        }

        internal static Item[] AppendItems(IEnumerable<Item> existing)
        {
            return (existing ?? Enumerable.Empty<Item>())
                .Concat(Entries.Select(entry =>
                    new Item(
                        entry.ItemName,
                        ItemType.LoreTablet,
                        null
                    )
                ))
                .ToArray();
        }
    }
}
