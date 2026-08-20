using System;
using System.Collections.Generic;
using SilksongRandomizer.Patches;

namespace SilksongRandomizer
{
    internal sealed class NativeShopLocation
    {
        internal readonly string ShopAssetName;
        internal readonly string LocationName;
        internal readonly string PlayerDataFlag;
        internal readonly ItemType Type;

        internal NativeShopLocation(
            string shopAssetName,
            string locationName,
            string playerDataFlag,
            ItemType type)
        {
            ShopAssetName = shopAssetName;
            LocationName = locationName;
            PlayerDataFlag = playerDataFlag;
            Type = type;
        }
    }

    /// <summary>
    /// Shared definitions for locations backed directly by native shop
    /// assets, collectible assets or persistent completion flags.
    /// </summary>
    internal static class CoreLocationManifest
    {
        internal const string CraftingKitPickupAssetName = "Tool Kit Pickup";
        internal const string CrowFeathersCraftingKitLocation =
            "Crafting Kit Source: Crow Feathers";

        internal static readonly NativeShopLocation[] ShopLocations =
        {
            new NativeShopLocation("Mapper Moss Grotto Map", "Map Purchase: Mosslands", "HasMossGrottoMap", ItemType.Map),
            new NativeShopLocation("Mapper Boneforest Map", "Map Purchase: The Marrow", "HasBoneforestMap", ItemType.Map),
            new NativeShopLocation("Mapper Docks Map", "Map Purchase: Deep Docks", "HasDocksMap", ItemType.Map),
            new NativeShopLocation("Mapper Wilds Map", "Map Purchase: Far Fields", "HasWildsMap", ItemType.Map),
            new NativeShopLocation("Mapper Crawl Map", "Map Purchase: Wormways", "HasCrawlMap", ItemType.Map),
            new NativeShopLocation("Mapper Hunters Nest Map", "Map Purchase: Hunter's March", "HasHuntersNestMap", ItemType.Map),
            new NativeShopLocation("Mapper Greymoor Map", "Map Purchase: Greymoor", "HasGreymoorMap", ItemType.Map),
            new NativeShopLocation("Mapper Bellhart Map", "Map Purchase: Bellhart", "HasBellhartMap", ItemType.Map),
            new NativeShopLocation("Mapper Shellwood Map", "Map Purchase: Shellwood", "HasShellwoodMap", ItemType.Map),
            new NativeShopLocation("Mapper Coral Caverns Map", "Map Purchase: Sands of Karak", "HasCoralMap", ItemType.Map),
            new NativeShopLocation("Mapper Dustpens Map", "Map Purchase: Sinner's Road", "HasDustpensMap", ItemType.Map),
            new NativeShopLocation("Mapper Peak Map", "Map Purchase: Mount Fay", "HasPeakMap", ItemType.Map),
            new NativeShopLocation("Mapper JudgeSteps Map", "Map Purchase: Blasted Steps", "HasJudgeStepsMap", ItemType.Map),
            new NativeShopLocation("Mapper Shadow Map", "Map Purchase: Bilewater", "HasSwampMap", ItemType.Map),

            new NativeShopLocation("Mapper Bench Map Pin", "Pin Purchase: Bench", "hasPinBench", ItemType.Pin),
            new NativeShopLocation("Mapper Tube Map Pin", "Pin Purchase: Ventrica", "hasPinTube", ItemType.Pin),
            new NativeShopLocation("Mapper Bellway Map Pin", "Pin Purchase: Bellway", "hasPinStag", ItemType.Pin),
            new NativeShopLocation("Mapper Shop Map Pin", "Pin Purchase: Vendor", "hasPinShop", ItemType.Pin),

            // Pebb and Grindle sell the same one-time Bone Bottom key source.
            // Their shared vanilla purchase flag maps both shop
            // assets to one AP location.
            new NativeShopLocation(
                "Bonebottom Faith Token",
                "Simple Key: Bone Bottom Shop",
                "PurchasedBonebottomFaithToken",
                ItemType.SimpleKey
            ),
            new NativeShopLocation(
                "Grindle Simple Key",
                "Simple Key: Bone Bottom Shop",
                "PurchasedBonebottomFaithToken",
                ItemType.SimpleKey
            ),
            new NativeShopLocation(
                "City Merchant Simple Key",
                "Simple Key: Songclave Shop",
                "MerchantEnclaveSimpleKey",
                ItemType.SimpleKey
            ),

            // Repeatable collectible shops and their Act 3 fallback sellers
            // share one AP source identity per vanilla flag.
            new NativeShopLocation(
                "Pilgrims Rest Crest Socket Unlocker",
                "Memory Locket: Pilgrim's Rest Shop",
                "PurchasedPilgrimsRestMemoryLocket",
                ItemType.MemoryLocket
            ),
            new NativeShopLocation(
                "Grindle Crest Socket",
                "Memory Locket: Pilgrim's Rest Shop",
                "PurchasedPilgrimsRestMemoryLocket",
                ItemType.MemoryLocket
            ),
            new NativeShopLocation(
                "Pilgrims Rest Tool Pouch",
                ToolPouchLocationManifest.PilgrimsRestLocation,
                "PurchasedPilgrimsRestToolPouch",
                ItemType.ToolPouch
            ),
            new NativeShopLocation(
                "Grindle Tool Pouch",
                ToolPouchLocationManifest.PilgrimsRestLocation,
                "PurchasedPilgrimsRestToolPouch",
                ItemType.ToolPouch
            ),
            new NativeShopLocation(
                "Bellhart Crest Socket",
                "Memory Locket: Bellhart Shop",
                "PurchasedBelltownMemoryLocket",
                ItemType.MemoryLocket
            ),
            new NativeShopLocation(
                "Bonebottom Tool Metal",
                "Craftmetal: Bone Bottom Shop",
                "PurchasedBonebottomToolMetal",
                ItemType.Craftmetal
            ),
            new NativeShopLocation(
                "Grindle Tool Metal",
                "Craftmetal: Bone Bottom Shop",
                "PurchasedBonebottomToolMetal",
                ItemType.Craftmetal
            ),
            new NativeShopLocation(
                "City Merchant Tool Metal",
                "Craftmetal: Songclave Shop",
                "MerchantEnclaveToolMetal",
                ItemType.Craftmetal
            ),
            new NativeShopLocation(
                "City Merchant Ward Key",
                "White Key",
                "MerchantEnclaveWardKey",
                ItemType.MajorKey
            ),
            new NativeShopLocation(
                "Architect Key",
                "Architect's Key",
                "PurchasedArchitectKey",
                ItemType.MajorKey
            ),
        };

        internal static readonly string[] CraftingKitLocationNames =
        {
            "Crafting Kit Source: Forge Tool Kit",
            "Crafting Kit Source: Architect Tool Kit",
            "Crafting Kit Source: Grindle Tool Kit",
            CrowFeathersCraftingKitLocation,
        };

        internal static readonly string[] NeedleUpgradeLocationNames =
        {
            "Pinmaster Plinney: Sharpened Needle",
            "Pinmaster Plinney: Shining Needle",
            "Pinmaster Plinney: Hivesteel Needle",
            "Pinmaster Plinney: Pale Steel Needle",
            "Pale Oil: Whispering Vaults",
            "Pale Oil: Great Taste of Pharloom",
            "Pale Oil: Ecstasy of the End",
        };

        internal static readonly string[] RelicAssetNames =
        {
            "Weaver Totem Witch",
            "Psalm Cylinder Library Roof",
            "Bone Record Wisp Top",
            "Weaver Totem Bonetown_upper_room",
            "Librarian Melody Cylinder",
            "Seal Chit City Merchant",
            "Psalm Cylinder Ward",
            "Weaver Record Conductor",
            "Psalm Cylinder Librarian",
            "Psalm Cylinder Hang",
            "Seal Chit Ward Corpse",
            "Weaver Record Sprint_Challenge",
            "Psalm Cylinder Grindle",
            "Weaver Record Weave_08",
            "Bone Record Understore_Map_Room",
            "Bone Record Bone_East_14",
            "Seal Chit Aspid_01",
            "Seal Chit Silk Siphon",
            "Bone Record Greymoor_flooded_corridor",
            "Weaver Totem Slab_Bottom",
            "Ancient Egg Abyss Middle",
        };

        private static readonly Dictionary<string, NativeShopLocation> ShopLocationsByAsset =
            BuildShopLocationLookup();

        private static readonly Dictionary<string, string> CraftingKitShopLocationsByAsset =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {
                    "Forge Tool Kit",
                    "Crafting Kit Source: Forge Tool Kit"
                },
                {
                    "Architect Tool Kit",
                    "Crafting Kit Source: Architect Tool Kit"
                },
                {
                    "Grindle Tool Kit",
                    "Crafting Kit Source: Grindle Tool Kit"
                },
            };

        private static readonly Dictionary<string, string> RelicLocationsByAsset =
            BuildRelicLocationLookup();

        internal static Location[] AppendTo(Location[] existingLocations)
        {
            List<Location> locations = new List<Location>(existingLocations);
            HashSet<string> addedLocationNames = new HashSet<string>(
                locations.ConvertAll(location => location.Name),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (NativeShopLocation shopLocation in ShopLocations)
            {
                if (!addedLocationNames.Add(shopLocation.LocationName))
                {
                    continue;
                }
                locations.Add(new Location(
                    shopLocation.LocationName,
                    shopLocation.Type,
                    null
                ));
            }

            foreach (string locationName in CraftingKitLocationNames)
            {
                locations.Add(new Location(
                    locationName,
                    ItemType.Upgrade,
                    null
                ));
            }

            foreach (string locationName in NeedleUpgradeLocationNames)
            {
                locations.Add(new Location(
                    locationName,
                    ItemType.NeedleUpgrade,
                    null
                ));
            }

            foreach (string relicAssetName in RelicAssetNames)
            {
                locations.Add(new Location(
                    GetRelicLocationName(relicAssetName),
                    ItemType.Relic,
                    null
                ));
            }

            AddCompletionLocations(locations);
            return locations.ToArray();
        }

        internal static bool TryGetShopLocation(
            string shopAssetName,
            out NativeShopLocation location)
        {
            if (string.IsNullOrEmpty(shopAssetName))
            {
                location = null;
                return false;
            }

            return ShopLocationsByAsset.TryGetValue(shopAssetName, out location);
        }

        internal static bool TryGetCraftingKitShopLocation(
            string shopAssetName,
            out string locationName)
        {
            if (string.IsNullOrEmpty(shopAssetName))
            {
                locationName = null;
                return false;
            }

            return CraftingKitShopLocationsByAsset.TryGetValue(
                shopAssetName,
                out locationName
            );
        }

        internal static bool TryGetRelicLocation(
            string relicAssetName,
            out string locationName)
        {
            if (string.IsNullOrEmpty(relicAssetName))
            {
                locationName = null;
                return false;
            }

            return RelicLocationsByAsset.TryGetValue(relicAssetName, out locationName);
        }

        private static string GetRelicLocationName(string relicAssetName)
        {
            return "Relic Pickup: " + relicAssetName;
        }

        private static Dictionary<string, NativeShopLocation> BuildShopLocationLookup()
        {
            Dictionary<string, NativeShopLocation> lookup =
                new Dictionary<string, NativeShopLocation>(StringComparer.Ordinal);

            foreach (NativeShopLocation location in ShopLocations)
            {
                lookup.Add(location.ShopAssetName, location);
            }

            return lookup;
        }

        private static Dictionary<string, string> BuildRelicLocationLookup()
        {
            Dictionary<string, string> lookup =
                new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string relicAssetName in RelicAssetNames)
            {
                lookup.Add(relicAssetName, GetRelicLocationName(relicAssetName));
            }

            return lookup;
        }

        private static void AddCompletionLocations(List<Location> locations)
        {
            locations.Add(new Location(
                "Bell Shrine Completion: bellShrineBoneForest",
                ItemType.BellShrine,
                null
            ));
            locations.Add(new Location(
                "Bell Shrine Completion: bellShrineWilds",
                ItemType.BellShrine,
                null
            ));
            locations.Add(new Location(
                "Bell Shrine Completion: bellShrineGreymoor",
                ItemType.BellShrine,
                null
            ));
            locations.Add(new Location(
                "Bell Shrine Completion: bellShrineShellwood",
                ItemType.BellShrine,
                null
            ));
            locations.Add(new Location(
                "Bell Shrine Completion: bellShrineBellhart",
                ItemType.BellShrine,
                null
            ));
            // Only named boss completion flags are included. Guard, arena and
            // miniboss flags are excluded.
            locations.Add(new Location(
                "Boss Completion: defeatedMossMother",
                ItemType.Boss,
                MossMotherWarpSafety.IsLegitimateDefeat
            ));
            locations.Add(new Location(
                "Boss Completion: skullKingKilled",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.skullKingKilled
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedBellBeast",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedBellBeast
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedAntQueen",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedAntQueen
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedSongGolem",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedSongGolem
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedDockForemen",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedDockForemen
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedVampireGnatBoss",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedVampireGnatBoss
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedCrowCourt",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedCrowCourt
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedSplinterQueen",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedSplinterQueen
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedSeth",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedSeth
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedFlowerQueen",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedFlowerQueen
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedRoachkeeperChef",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedRoachkeeperChef
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedPhantom",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedPhantom
            ));
            locations.Add(new Location(
                "Boss Completion: DefeatedSwampShaman",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.DefeatedSwampShaman
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedCoralKing",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedCoralKing
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedCoralDrillers",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedCoralDrillers
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedLastJudge",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedLastJudge
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedGreyWarrior",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedGreyWarrior
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedFirstWeaver",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedFirstWeaver
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedBroodMother",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedBroodMother
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedTrobbio",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedTrobbio
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedTormentedTrobbio",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedTormentedTrobbio
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedCogworkDancers",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedCogworkDancers
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedLaceTower",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedLaceTower
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedSongChevalierBoss",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedSongChevalierBoss
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedWhiteCloverstag",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedWhiteCloverstag
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedCloverDancers",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedCloverDancers
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedWispPyreEffigy",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedWispPyreEffigy
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedAntTrapper",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedAntTrapper
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedCoralDrillerSolo",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedCoralDrillerSolo
            ));
            locations.Add(new Location(
                "Boss Completion: wardBossDefeated",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.wardBossDefeated
            ));
            locations.Add(new Location(
                "Boss Completion: defeatedZapCoreEnemy",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.defeatedZapCoreEnemy
            ));
            locations.Add(new Location(
                "Boss Completion: spinnerDefeated",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.spinnerDefeated
            ));
            locations.Add(new Location(
                "Boss Completion: skullKingDefeated",
                ItemType.Boss,
                () => PlayerData.instance != null && PlayerData.instance.skullKingDefeated
            ));
        }
    }
}
