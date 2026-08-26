using Archipelago.MultiClient.Net.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace SilksongRandomizer
{
    [System.Serializable]
    public class SaveState
    {
        public const int CurrentSchemaVersion = 26;

        internal static readonly string[] StartWithMapsItemNames =
        {
            "Map: Mosslands",
            "Map: The Marrow",
            "Map: Deep Docks",
            "Map: Far Fields",
            "Map: Wormways",
            "Map: Hunter's March",
            "Map: Greymoor",
            "Map: Bellhart",
            "Map: Shellwood",
            "Map: Blasted Steps",
            "Map: Sinner's Road",
            "Map: Mount Fay",
            "Map: Sands of Karak",
            "Map: Bilewater",
            "Map: Weavenest Atla",
            "Map: Grand Gate",
            "Map: Underworks",
            "Map: Choral Chambers",
            "Map: Whispering Vaults",
            "Map: Whiteward",
            "Map: Cogwork Core",
            "Map: Memorium",
            "Map: High Halls",
            "Map: The Slab",
            "Map: Putrified Ducts",
            "Map: The Cradle",
            "Map: The Abyss",
        };

        private static readonly HashSet<string> StartWithMapsItemNameSet =
            new HashSet<string>(
                StartWithMapsItemNames,
                StringComparer.OrdinalIgnoreCase
            );

        [System.Serializable]
        public class HintData
        {
            public string locationName;
            public string user;
            public string item;
            public ItemFlags flags;
            public bool official;
            public bool found;
        }

        [System.Serializable]
        public class PurchasePriceData
        {
            public string key = string.Empty;
            public int price;

            public PurchasePriceData()
            {
            }

            public PurchasePriceData(string key, int price)
            {
                this.key = key ?? string.Empty;
                this.price = price;
            }
        }

        [System.Serializable]
        public class ItemMarkerLocationData
        {
            public string itemName = string.Empty;
            public string locationName = string.Empty;

            public ItemMarkerLocationData()
            {
            }

            public ItemMarkerLocationData(
                string itemName,
                string locationName
            )
            {
                this.itemName = itemName ?? string.Empty;
                this.locationName = locationName ?? string.Empty;
            }
        }

        public static SaveState Instance { get; set; }

        // A bound save may only reconnect to this Archipelago room, slot and goal.
        [XmlIgnore]
        public int schemaVersion = CurrentSchemaVersion;
        [NonSerialized]
        [XmlIgnore]
        private bool schemaVersionWasDeserialized;
        [XmlElement("schemaVersion")]
        public int SerializedSchemaVersion
        {
            get
            {
                return schemaVersion;
            }
            set
            {
                schemaVersion = value;
                schemaVersionWasDeserialized = true;
            }
        }
        public string roomSeed = string.Empty;
        public string slotName = string.Empty;
        public int team = -1;
        public int slot = -1;
        public string worldVersion = string.Empty;
        public string goal = string.Empty;
        public string spellingBeePhrase = string.Empty;
        public int fleaHuntGoalCount =
            Archipelago.DefaultFleaHuntGoalCount;
        public string startingLocation = string.Empty;
        public bool startingLocationApplied;
        public string startingCrest = string.Empty;
        public bool splitDashAndSprint;
        public bool randomizeNeedleUpgrades;
        public bool startWithMaps;
        public bool startFullyMapped;
        public bool automaticCompass;
        public CheckMapMarkerMode checkMapMarkers =
            CheckMapMarkerMode.Off;
        public bool randomizedBellMarkers;
        public bool randomizedMelodyMarkers;
        public List<ItemMarkerLocationData> randomizedItemMarkerLocations =
            new List<ItemMarkerLocationData>();
        [NonSerialized]
        [XmlIgnore]
        private Dictionary<string, string> randomizedItemMarkerLocationLookup =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public bool vogHintSettingsBound;
        public int vogWothHintCount;
        public int vogFoolishHintCount;
        public int vogGeneralHintCount;
        public List<string> vogWothAreas = new List<string>();
        public List<string> vogFoolishAreas = new List<string>();
        public List<string> vogGeneralLocations = new List<string>();
        public HashSet<string> purchasedVogHints =
            new HashSet<string>(StringComparer.Ordinal);
        public List<string> vogHintHistory = new List<string>();
        public int vogHintHistoryReadIndex;
        public string pendingVogGeneralKey = string.Empty;
        public string pendingVogGeneralLocation = string.Empty;
        public int pendingVogGeneralPrice;
        public string bellwayAccess =
            Archipelago.BellwayAccessBellBeastRequired;
        public string trailsEndRequirement =
            Archipelago.TrailsEndRequirementShakraStock;
        public string enemyRosaryMultiplier =
            Archipelago.RosaryMultiplierVanilla;
        public string enemyShardMultiplier =
            Archipelago.RosaryMultiplierVanilla;
        public string normalShopPrices = Archipelago.PriceModeVanilla;
        public string bellwayPrices = Archipelago.PriceModeVanilla;
        public string mapPrices = Archipelago.PriceModeVanilla;
        public string pinPrices = Archipelago.PriceModeVanilla;
        public string upgradePrices = Archipelago.PriceModeVanilla;
        public string donationPrices = Archipelago.PriceModeVanilla;
        public string vogHintPrices = Archipelago.PriceModeVanilla;
        public List<PurchasePriceData> purchasePrices =
            new List<PurchasePriceData>();
        [NonSerialized]
        [XmlIgnore]
        private Dictionary<string, int> purchasePriceLookup;
        public bool fasterDialogue;
        public bool alphabetMode;
        public bool deathLink;
        public string deathLinkCocoon =
            Archipelago.DeathLinkCocoonProtected;
        public bool silkLink;
        // Retains upgraded-capacity Silk across a live network reconnect only.
        // The game clears current Silk on a full save load, so this runtime
        // cache is not written into the save file.
        [NonSerialized]
        [XmlIgnore]
        public int silkLinkPrivateSilk = -1;
        // Last visible linked Silk is persisted so a live reconnect can
        // replay only the local delta which survived a failed shared-pool
        // write. It is ignored after a full load, where the nonserialized
        // private-Silk marker returns to -1 and vanilla clears
        // current Silk.
        public int silkLinkLastSyncedBalance = -1;
        public bool rosaryLink;
        public bool shellShardLink;
        // Last visible linked balances let reconnect apply only currency that
        // was genuinely earned or spent while the socket was unavailable.
        public int rosaryLinkLastSyncedBalance = -1;
        public int shellShardLinkLastSyncedBalance = -1;
        // Shell Shards above the vanilla 400-capacity pouch remain local.
        // Unlike current Silk, this currency persists across full save loads.
        public int shellShardLinkPrivateShards = -1;
        public bool individualRelicTurnIns;
        public string preferredF4Hub = string.Empty;
        // Set only by the Bone Bottom-to-Greymoor caravan arrival
        // sequence. Unlike CaravanTroupeLocation, this cannot be advanced by
        // declining the ride and remains true if the troupe later relocates.
        public bool rodeFleaCaravanToGreymoor;
        public bool bellhomePhaseToggleUnlocked;
        // F4 may cross the opening story boundary and make vanilla briefly
        // assert defeatedMossMother. That synthetic state remains quarantined
        // until Moss Mother's real boss FSM reaches its End state.
        public bool mossMotherBypassedByBoneBottomWarp;
        // Minimal APWorld logic graph used only to shade check-map icons.
        // An empty payload means "unknown", never "unreachable".
        [NonSerialized]
        [XmlIgnore]
        public string mapLogicPayloadJson = string.Empty;

        [NonSerialized]
        [XmlIgnore]
        private string compressedMapLogicPayloadSource = string.Empty;

        [NonSerialized]
        [XmlIgnore]
        private string compressedMapLogicPayload = string.Empty;

        [XmlElement("mapLogicPayloadGzip")]
        public string MapLogicPayloadGzip
        {
            get
            {
                string payload = mapLogicPayloadJson ?? string.Empty;
                if (!ReferenceEquals(
                        payload,
                        compressedMapLogicPayloadSource
                    ))
                {
                    compressedMapLogicPayload =
                        CompressMapLogicPayload(payload);
                    compressedMapLogicPayloadSource = payload;
                }
                return compressedMapLogicPayload;
            }
            set
            {
                compressedMapLogicPayload = value ?? string.Empty;
                mapLogicPayloadJson = DecompressMapLogicPayload(
                    compressedMapLogicPayload
                );
                compressedMapLogicPayloadSource = mapLogicPayloadJson;
            }
        }

        internal void PrimeMapLogicPayloadCompression()
        {
            _ = MapLogicPayloadGzip;
        }

        private static string CompressMapLogicPayload(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return string.Empty;
            }

            byte[] input = Encoding.UTF8.GetBytes(payload);
            using (MemoryStream output = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(
                    output,
                    CompressionLevel.Fastest,
                    true
                ))
                {
                    gzip.Write(input, 0, input.Length);
                }
                return Convert.ToBase64String(output.ToArray());
            }
        }

        private static string DecompressMapLogicPayload(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return string.Empty;
            }

            byte[] input = Convert.FromBase64String(payload);
            using (MemoryStream source = new MemoryStream(input))
            using (GZipStream gzip = new GZipStream(
                source,
                CompressionMode.Decompress
            ))
            using (MemoryStream output = new MemoryStream())
            {
                gzip.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }
        public RandomizationMode skillRandomization = RandomizationMode.Anywhere;
        public RandomizationMode toolRandomization = RandomizationMode.Anywhere;
        public RandomizationMode silkSkillRandomization = RandomizationMode.Anywhere;
        public RandomizationMode crestRandomization = RandomizationMode.Anywhere;
        public RandomizationMode fleaRandomization = RandomizationMode.Anywhere;
        public RandomizationMode crestSlotRandomization = RandomizationMode.Anywhere;
        public RandomizationMode maskShardRandomization = RandomizationMode.Anywhere;
        public RandomizationMode spoolFragmentRandomization = RandomizationMode.Anywhere;
        public RandomizationMode silkHeartRandomization = RandomizationMode.Anywhere;
        public RandomizationMode bellwayRandomization = RandomizationMode.Anywhere;
        public RandomizationMode ventricaRandomization = RandomizationMode.Anywhere;
        public RandomizationMode mapRandomization = RandomizationMode.Anywhere;
        public RandomizationMode melodyRandomization = RandomizationMode.Vanilla;
        public RandomizationMode pinRandomization = RandomizationMode.Anywhere;
        public RandomizationMode relicRandomization = RandomizationMode.Anywhere;
        public RandomizationMode craftingKitRandomization = RandomizationMode.Anywhere;
        public RandomizationMode minorPickupRandomization = RandomizationMode.Vanilla;
        public RandomizationMode loreTabletRandomization = RandomizationMode.Vanilla;
        // Vanilla is the default when the sidecar has not yet been
        // bound to slot data. Current rooms replace it with their configured
        // destination-key mode during binding.
        public RandomizationMode simpleKeyRandomization = RandomizationMode.Vanilla;
        public RandomizationMode memoryLocketRandomization = RandomizationMode.Vanilla;
        public RandomizationMode craftmetalRandomization = RandomizationMode.Vanilla;
        public RandomizationMode mossberryRandomization = RandomizationMode.Vanilla;
        public RandomizationMode pollipHeartRandomization = RandomizationMode.Vanilla;
        public RandomizationMode silkeaterRandomization = RandomizationMode.Vanilla;
        public RandomizationMode majorKeyRandomization = RandomizationMode.Vanilla;
        public RandomizationMode toolPouchRandomization = RandomizationMode.Vanilla;
        public RandomizationMode bossSanity = RandomizationMode.Anywhere;
        public RandomizationMode bellShrineSanity = RandomizationMode.Anywhere;
        public RandomizationMode questSanityMode = RandomizationMode.Anywhere;
        public bool goalCompleted;
        public int receivedItemIndex;
        public List<string> receivedItemHistory = new List<string>();

        public bool canDoubleJump = false;
        public bool canChargeSlash = false;
        public bool canSilkSoar = false;
        public bool canWallJump = false;
        public bool canBrolly = false;
        public bool canDash = false;
        public bool canSprint = false;
        public bool canUseHarpoon = false;
        public bool canUseNeedolin = false;
        public bool canUseQuill = false;

        // Repeatable ordered upgrades cannot be represented by receivedItems,
        // because that collection stores unique item names.
        public int swiftStepLevel = 0;
        public int druidsEyeLevel = 0;
        public int clawMirrorLevel = 0;
        public int curveclawLevel = 0;
        public int silkHeartLevel = 0;
        public int needleUpgradeLevel = 0;

        // AP tool ownership is virtual, so the native ToolItem.Unlock path
        // does not initialize liquid amount/reserve data. These markers make
        // that native-equivalent initialization a one-time receipt/migration
        // operation rather than an unintended refill on every save load.
        public bool fleaBrewInitialFillApplied;
        public bool plasmiumPhialInitialFillApplied;

        public bool SavedFlea_Bone_06;
        public bool SavedFlea_Dock_16;
        public bool SavedFlea_Bone_East_05;
        public bool SavedFlea_Bone_East_17b;
        public bool SavedFlea_Ant_03;
        public bool SavedFlea_Greymoor_15b;
        public bool SavedFlea_Greymoor_06;
        public bool SavedFlea_Shellwood_03;
        public bool SavedFlea_Bone_East_10_Church;
        public bool SavedFlea_Coral_35;
        public bool SavedFlea_Dust_12;
        public bool SavedFlea_Dust_09;
        public bool SavedFlea_Belltown_04;
        public bool SavedFlea_Crawl_06;
        public bool SavedFlea_Slab_Cell;
        public bool SavedFlea_Shadow_28;
        public bool SavedFlea_Dock_03d;
        public bool SavedFlea_Under_23;
        public bool SavedFlea_Shadow_10;
        public bool SavedFlea_Song_14;
        public bool SavedFlea_Coral_24;
        public bool SavedFlea_Peak_05c;
        public bool SavedFlea_Library_09;
        public bool SavedFlea_Song_11;
        public bool SavedFlea_Library_01;
        public bool SavedFlea_Under_21;
        public bool SavedFlea_Slab_06;

        public bool UnlockedDocksStation;
        public bool UnlockedBoneforestEastStation;
        public bool UnlockedGreymoorStation;
        public bool UnlockedBelltownStation;
        public bool UnlockedCoralTowerStation;
        public bool UnlockedCityStation;
        public bool UnlockedPeakStation;
        public bool UnlockedShellwoodStation;
        public bool UnlockedShadowStation;
        public bool UnlockedAqueductStation;
        public bool canUseBeastlingCall;
        public bool bellEaterResolved;

        public bool UnlockedSongTube;
        public bool UnlockedUnderTube;
        public bool UnlockedCityBellwayTube;
        public bool UnlockedHangTube;
        public bool UnlockedEnclaveTube;
        public bool UnlockedArboriumTube;

        [NonSerialized] [XmlIgnore] public ItemSet items = new ItemSet();
        [NonSerialized] [XmlIgnore] public LocationSet locations = new LocationSet();

        public HashSet<string> receivedItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> checkedLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> roomLocationNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public List<HintData> receivedHints = new List<HintData>();
        public SaveState()
        {
        }

        public bool IsRoomBound
        {
            get
            {
                return !string.IsNullOrWhiteSpace(roomSeed) && team >= 0 && slot >= 0;
            }
        }

        public bool HasWorldVersionBinding
        {
            get
            {
                return Archipelago.IsSupportedWorldVersion(worldVersion);
            }
        }

        public bool HasGoalBinding
        {
            get
            {
                return Archipelago.IsSupportedGoal(goal) &&
                       (
                           !string.Equals(
                               goal,
                               Archipelago.FleaHuntGoal,
                               StringComparison.Ordinal
                           ) ||
                           Archipelago.IsSupportedFleaHuntGoalCount(
                               fleaHuntGoalCount
                           )
                       );
            }
        }

        public bool HasStartingCrestBinding
        {
            get
            {
                return CrestNames.IsSupportedStartingCrestKey(startingCrest);
            }
        }

        public bool HasStartingLocationBinding
        {
            get
            {
                return Archipelago.IsSupportedStartingLocation(
                    startingLocation
                );
            }
        }

        [XmlIgnore]
        public bool AllowsBellwaysBeforeBellBeast
        {
            get
            {
                return string.Equals(
                    bellwayAccess,
                    Archipelago.BellwayAccessRandomizedStations,
                    StringComparison.Ordinal
                );
            }
        }

        [XmlIgnore]
        public bool TrailsEndUsesOwnedMaps
        {
            get
            {
                return string.Equals(
                    trailsEndRequirement,
                    Archipelago.TrailsEndRequirementOwnedMaps,
                    StringComparison.Ordinal
                );
            }
        }

        internal void RequireExplicitSchemaVersion()
        {
            if (!schemaVersionWasDeserialized)
            {
                throw new InvalidDataException(
                    "Save metadata is missing the required schemaVersion " +
                    "element."
                );
            }
        }

        public void InitializeAfterLoad()
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    "Save metadata schema " + schemaVersion +
                    " does not match the required schema " +
                    CurrentSchemaVersion + "."
                );
            }

            roomSeed = roomSeed ?? string.Empty;
            slotName = slotName ?? string.Empty;
            worldVersion = worldVersion ?? string.Empty;
            goal = goal ?? string.Empty;
            spellingBeePhrase = spellingBeePhrase ?? string.Empty;
            if (string.Equals(
                    goal,
                    Archipelago.SpellingBeeGoal,
                    StringComparison.Ordinal
                ) && string.IsNullOrWhiteSpace(spellingBeePhrase))
            {
                spellingBeePhrase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            }
            mapLogicPayloadJson = mapLogicPayloadJson ?? string.Empty;
            PrimeMapLogicPayloadCompression();
            if (!Archipelago.IsSupportedFleaHuntGoalCount(
                    fleaHuntGoalCount
                ))
            {
                fleaHuntGoalCount =
                    Archipelago.DefaultFleaHuntGoalCount;
            }
            startingCrest = startingCrest ?? string.Empty;
            startingLocation = startingLocation ?? string.Empty;
            preferredF4Hub = FastTravelUtil.NormalizePreferredHubKey(
                preferredF4Hub
            );
            if (!Archipelago.IsSupportedBellwayAccess(bellwayAccess))
            {
                bellwayAccess =
                    Archipelago.BellwayAccessBellBeastRequired;
            }
            if (!Archipelago.IsSupportedTrailsEndRequirement(
                    trailsEndRequirement
                ))
            {
                trailsEndRequirement =
                    Archipelago.TrailsEndRequirementShakraStock;
            }
            if (!Archipelago.IsSupportedEnemyRosaryMultiplier(
                    enemyRosaryMultiplier
                ))
            {
                enemyRosaryMultiplier =
                    Archipelago.RosaryMultiplierVanilla;
            }
            if (!Archipelago.IsSupportedEnemyShardMultiplier(
                    enemyShardMultiplier
                ))
            {
                enemyShardMultiplier =
                    Archipelago.RosaryMultiplierVanilla;
            }
            normalShopPrices = NormalizePurchasePriceMode(
                normalShopPrices
            );
            bellwayPrices = NormalizePurchasePriceMode(bellwayPrices);
            mapPrices = NormalizePurchasePriceMode(mapPrices);
            pinPrices = NormalizePurchasePriceMode(pinPrices);
            upgradePrices = NormalizePurchasePriceMode(upgradePrices);
            donationPrices = NormalizePurchasePriceMode(donationPrices);
            vogHintPrices = NormalizePurchasePriceMode(vogHintPrices);
            SetPurchasePrices(
                (purchasePrices ?? new List<PurchasePriceData>())
                    .Where(entry => entry != null)
                    .GroupBy(
                        entry => entry.key ?? string.Empty,
                        StringComparer.Ordinal
                    )
                    .Where(group =>
                        !string.IsNullOrWhiteSpace(group.Key))
                    .ToDictionary(
                        group => group.Key,
                        group => Math.Max(0, group.Last().price),
                        StringComparer.Ordinal
                    )
            );
            if (checkMapMarkers < CheckMapMarkerMode.Off ||
                checkMapMarkers > CheckMapMarkerMode.All)
            {
                checkMapMarkers = CheckMapMarkerMode.Off;
            }
            SetRandomizedItemMarkerLocations(
                (randomizedItemMarkerLocations ??
                    new List<ItemMarkerLocationData>())
                    .Where(entry => entry != null)
                    .Select(entry => new KeyValuePair<string, string>(
                        entry.itemName,
                        entry.locationName
                    ))
            );
            vogWothHintCount = Math.Max(
                0,
                Math.Min(30, vogWothHintCount)
            );
            vogFoolishHintCount = Math.Max(
                0,
                Math.Min(30, vogFoolishHintCount)
            );
            vogGeneralHintCount = Math.Max(
                0,
                Math.Min(30, vogGeneralHintCount)
            );
            vogWothAreas = NormalizeVogHintPlan(vogWothAreas);
            vogFoolishAreas = NormalizeVogHintPlan(vogFoolishAreas);
            vogGeneralLocations = NormalizeVogHintPlan(
                vogGeneralLocations
            );
            purchasedVogHints = new HashSet<string>(
                (purchasedVogHints ?? new HashSet<string>())
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Select(key => key.Trim()),
                StringComparer.Ordinal
            );
            vogHintHistory = (vogHintHistory ?? new List<string>())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text.Trim())
                .ToList();
            vogHintHistoryReadIndex = vogHintHistory.Count == 0
                ? 0
                : Math.Max(0, vogHintHistoryReadIndex) %
                    vogHintHistory.Count;
            pendingVogGeneralKey =
                (pendingVogGeneralKey ?? string.Empty).Trim();
            pendingVogGeneralLocation =
                (pendingVogGeneralLocation ?? string.Empty).Trim();
            pendingVogGeneralPrice = Math.Max(
                0,
                pendingVogGeneralPrice
            );
            if (string.IsNullOrWhiteSpace(pendingVogGeneralKey) ||
                string.IsNullOrWhiteSpace(pendingVogGeneralLocation) ||
                !pendingVogGeneralKey.StartsWith(
                    "general:",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    pendingVogGeneralKey,
                    "general:" + pendingVogGeneralLocation,
                    StringComparison.Ordinal
                ))
            {
                pendingVogGeneralKey = string.Empty;
                pendingVogGeneralLocation = string.Empty;
                pendingVogGeneralPrice = 0;
            }
            receivedItems = new HashSet<string>(
                (receivedItems ?? new HashSet<string>())
                    .Select(ItemSet.GetCanonicalItemName),
                StringComparer.OrdinalIgnoreCase
            );
            receivedItemHistory = (
                receivedItemHistory ?? new List<string>()
            ).Select(itemName =>
                string.IsNullOrWhiteSpace(itemName)
                    ? string.Empty
                    : ItemSet.GetCanonicalItemName(itemName)
            ).ToList();
            silkHeartLevel = Math.Max(
                0,
                Math.Min(3, silkHeartLevel)
            );
            druidsEyeLevel = Math.Max(0, Math.Min(2, druidsEyeLevel));
            clawMirrorLevel = Math.Max(
                0,
                Math.Min(2, clawMirrorLevel)
            );
            curveclawLevel = Math.Max(
                0,
                Math.Min(2, curveclawLevel)
            );
            swiftStepLevel = Math.Max(0, Math.Min(2, swiftStepLevel));
            needleUpgradeLevel = Math.Max(
                0,
                Math.Min(4, needleUpgradeLevel)
            );
            if (splitDashAndSprint)
            {
                if (swiftStepLevel >= 1)
                {
                    canSprint = true;
                }
                if (swiftStepLevel >= 2)
                {
                    canDash = true;
                }
            }
            checkedLocations = new HashSet<string>(
                (checkedLocations ?? new HashSet<string>())
                    .Select(LocationSet.GetCanonicalLocationName),
                StringComparer.OrdinalIgnoreCase
            );
            roomLocationNames = new HashSet<string>(
                (roomLocationNames ?? new HashSet<string>())
                    .Select(LocationSet.GetCanonicalLocationName)
                    .Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase
            );
            receivedHints = receivedHints ?? new List<HintData>();
            foreach (HintData hint in receivedHints.Where(hint => hint != null))
            {
                hint.locationName =
                    LocationSet.GetCanonicalLocationName(hint.locationName);
                hint.item = ItemSet.GetCanonicalItemName(hint.item);
            }
            items = new ItemSet();
            locations = new LocationSet();
        }

        private static string NormalizePurchasePriceMode(string mode)
        {
            return Archipelago.IsSupportedPurchasePriceMode(mode)
                ? mode
                : Archipelago.PriceModeVanilla;
        }

        private static List<string> NormalizeVogHintPlan(
            IEnumerable<string> values
        )
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToList();
        }

        private void SetPurchasePrices(
            IEnumerable<KeyValuePair<string, int>> prices)
        {
            Dictionary<string, int> normalized =
                new Dictionary<string, int>(StringComparer.Ordinal);
            if (prices != null)
            {
                foreach (KeyValuePair<string, int> entry in prices)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Key) &&
                        entry.Value >= 0)
                    {
                        normalized[entry.Key] = entry.Value;
                    }
                }
            }

            purchasePrices = normalized
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new PurchasePriceData(
                    entry.Key,
                    entry.Value
                ))
                .ToList();
            purchasePriceLookup = normalized;
        }

        private Dictionary<string, int> GetPurchasePriceLookup()
        {
            if (purchasePriceLookup != null)
            {
                return purchasePriceLookup;
            }

            purchasePriceLookup =
                new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (PurchasePriceData entry in
                     purchasePrices ?? new List<PurchasePriceData>())
            {
                if (entry != null &&
                    !string.IsNullOrWhiteSpace(entry.key) &&
                    entry.price >= 0)
                {
                    purchasePriceLookup[entry.key] = entry.price;
                }
            }
            return purchasePriceLookup;
        }

        public bool TryGetPurchasePrice(string key, out int price)
        {
            price = 0;
            return !string.IsNullOrWhiteSpace(key) &&
                   GetPurchasePriceLookup().TryGetValue(key, out price);
        }

        private bool PurchasePriceSettingsMatch(
            Archipelago archipelago)
        {
            if (archipelago == null ||
                !string.Equals(
                    normalShopPrices,
                    archipelago.NormalShopPrices,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    bellwayPrices,
                    archipelago.BellwayPrices,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    mapPrices,
                    archipelago.MapPrices,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    pinPrices,
                    archipelago.PinPrices,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    upgradePrices,
                    archipelago.UpgradePrices,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    donationPrices,
                    archipelago.DonationPrices,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    vogHintPrices,
                    archipelago.VogHintPrices,
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            Dictionary<string, int> savedPrices =
                GetPurchasePriceLookup();
            IReadOnlyDictionary<string, int> roomPrices =
                archipelago.PurchasePrices;
            return roomPrices != null &&
                   savedPrices.Count == roomPrices.Count &&
                   savedPrices.All(entry =>
                       roomPrices.TryGetValue(
                           entry.Key,
                           out int roomPrice
                       ) &&
                       roomPrice == entry.Value);
        }

        public void BindToRoom(Archipelago archipelago)
        {
            if (archipelago == null || !archipelago.Connected)
            {
                return;
            }

            roomSeed = archipelago.RoomSeed ?? string.Empty;
            slotName = archipelago.SlotName ?? string.Empty;
            team = archipelago.Team;
            slot = archipelago.Slot;
            worldVersion = archipelago.WorldVersion ?? string.Empty;
            goal = archipelago.Goal ?? string.Empty;
            spellingBeePhrase =
                archipelago.SpellingBeePhrase ?? string.Empty;
            fleaHuntGoalCount = archipelago.FleaHuntGoalCount;
            startingLocation = archipelago.StartingLocation ?? string.Empty;
            startingCrest = archipelago.StartingCrest ?? string.Empty;
            splitDashAndSprint = archipelago.SplitDashAndSprint;
            randomizeNeedleUpgrades =
                archipelago.RandomizeNeedleUpgrades;
            startWithMaps = archipelago.StartWithMaps;
            startFullyMapped = archipelago.StartFullyMapped;
            automaticCompass = archipelago.AutomaticCompass;
            checkMapMarkers = archipelago.CheckMapMarkers;
            randomizedBellMarkers =
                archipelago.RandomizedBellMarkers;
            randomizedMelodyMarkers =
                archipelago.RandomizedMelodyMarkers;
            SetRandomizedItemMarkerLocations(
                archipelago.RandomizedItemMarkerLocations
            );
            BindVogHintSettings(archipelago);
            bellwayAccess = archipelago.BellwayAccess;
            trailsEndRequirement = archipelago.TrailsEndRequirement;
            enemyRosaryMultiplier =
                archipelago.EnemyRosaryMultiplier;
            enemyShardMultiplier =
                archipelago.EnemyShardMultiplier;
            normalShopPrices = archipelago.NormalShopPrices;
            bellwayPrices = archipelago.BellwayPrices;
            mapPrices = archipelago.MapPrices;
            pinPrices = archipelago.PinPrices;
            upgradePrices = archipelago.UpgradePrices;
            donationPrices = archipelago.DonationPrices;
            vogHintPrices = archipelago.VogHintPrices;
            SetPurchasePrices(archipelago.PurchasePrices);
            fasterDialogue = archipelago.FasterDialogue;
            alphabetMode = archipelago.AlphabetMode;
            deathLink = archipelago.DeathLink;
            deathLinkCocoon = archipelago.DeathLinkCocoon;
            silkLink = archipelago.SilkLink;
            rosaryLink = archipelago.RosaryLink;
            shellShardLink = archipelago.ShellShardLink;
            individualRelicTurnIns =
                archipelago.IndividualRelicTurnIns;
            mapLogicPayloadJson =
                archipelago.MapLogicPayloadJson ?? string.Empty;
            PrimeMapLogicPayloadCompression();
            skillRandomization = archipelago.SkillRandomization;
            toolRandomization = archipelago.ToolRandomization;
            silkSkillRandomization = archipelago.SilkSkillRandomization;
            crestRandomization = archipelago.CrestRandomization;
            fleaRandomization = archipelago.FleaRandomization;
            crestSlotRandomization = archipelago.CrestSlotRandomization;
            maskShardRandomization = archipelago.MaskShardRandomization;
            spoolFragmentRandomization = archipelago.SpoolFragmentRandomization;
            silkHeartRandomization = archipelago.SilkHeartRandomization;
            bellwayRandomization = archipelago.BellwayRandomization;
            ventricaRandomization = archipelago.VentricaRandomization;
            mapRandomization = archipelago.MapRandomization;
            melodyRandomization = archipelago.MelodyRandomization;
            pinRandomization = archipelago.PinRandomization;
            relicRandomization = archipelago.RelicRandomization;
            craftingKitRandomization = archipelago.CraftingKitRandomization;
            minorPickupRandomization = archipelago.MinorPickupRandomization;
            loreTabletRandomization = archipelago.LoreTabletRandomization;
            simpleKeyRandomization = archipelago.SimpleKeyRandomization;
            memoryLocketRandomization = archipelago.MemoryLocketRandomization;
            craftmetalRandomization = archipelago.CraftmetalRandomization;
            mossberryRandomization = archipelago.MossberryRandomization;
            pollipHeartRandomization = archipelago.PollipHeartRandomization;
            silkeaterRandomization = archipelago.SilkeaterRandomization;
            majorKeyRandomization = archipelago.MajorKeyRandomization;
            toolPouchRandomization = archipelago.ToolPouchRandomization;
            bossSanity = archipelago.BossSanity;
            bellShrineSanity = archipelago.BellShrineSanity;
            questSanityMode = archipelago.QuestSanity;
            SetRoomLocationNames(archipelago.GetRoomLocationNames());
            schemaVersion = CurrentSchemaVersion;
        }

        public bool MatchesRoom(Archipelago archipelago)
        {
            if (!IsRoomBound || archipelago == null)
            {
                return true;
            }

            return string.Equals(roomSeed, archipelago.RoomSeed, StringComparison.Ordinal) &&
                   team == archipelago.Team &&
                   slot == archipelago.Slot &&
                   string.Equals(
                       worldVersion,
                       archipelago.WorldVersion,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(goal, archipelago.Goal, StringComparison.Ordinal) &&
                   string.Equals(
                       spellingBeePhrase,
                       archipelago.SpellingBeePhrase,
                       StringComparison.Ordinal
                   ) &&
                   (
                       !string.Equals(
                           goal,
                           Archipelago.FleaHuntGoal,
                           StringComparison.Ordinal
                       ) ||
                       fleaHuntGoalCount == archipelago.FleaHuntGoalCount
                   ) &&
                   string.Equals(
                       startingLocation,
                       archipelago.StartingLocation,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       startingCrest,
                       archipelago.StartingCrest,
                       StringComparison.Ordinal
                   ) &&
                   splitDashAndSprint == archipelago.SplitDashAndSprint &&
                   randomizeNeedleUpgrades ==
                       archipelago.RandomizeNeedleUpgrades &&
                   startWithMaps == archipelago.StartWithMaps &&
                   startFullyMapped == archipelago.StartFullyMapped &&
                   automaticCompass == archipelago.AutomaticCompass &&
                   checkMapMarkers == archipelago.CheckMapMarkers &&
                   randomizedBellMarkers ==
                       archipelago.RandomizedBellMarkers &&
                   randomizedMelodyMarkers ==
                       archipelago.RandomizedMelodyMarkers &&
                   (!vogHintSettingsBound ||
                    VogHintSettingsMatch(archipelago)) &&
                   string.Equals(
                       bellwayAccess,
                       archipelago.BellwayAccess,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       trailsEndRequirement,
                       archipelago.TrailsEndRequirement,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       enemyRosaryMultiplier,
                       archipelago.EnemyRosaryMultiplier,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       enemyShardMultiplier,
                       archipelago.EnemyShardMultiplier,
                       StringComparison.Ordinal
                   ) &&
                   PurchasePriceSettingsMatch(archipelago) &&
                   fasterDialogue == archipelago.FasterDialogue &&
                   alphabetMode == archipelago.AlphabetMode &&
                   deathLink == archipelago.DeathLink &&
                   string.Equals(
                       deathLinkCocoon,
                       archipelago.DeathLinkCocoon,
                       StringComparison.Ordinal
                   ) &&
                    silkLink == archipelago.SilkLink &&
                    rosaryLink == archipelago.RosaryLink &&
                    shellShardLink == archipelago.ShellShardLink &&
                    individualRelicTurnIns ==
                        archipelago.IndividualRelicTurnIns &&
                   RandomizationModesMatch(archipelago);
        }

        internal void BindVogHintSettings(Archipelago archipelago)
        {
            if (archipelago == null)
            {
                return;
            }

            vogWothHintCount = archipelago.VogWothHintCount;
            vogFoolishHintCount = archipelago.VogFoolishHintCount;
            vogGeneralHintCount = archipelago.VogGeneralHintCount;
            vogWothAreas = NormalizeVogHintPlan(
                archipelago.VogWothAreas
            );
            vogFoolishAreas = NormalizeVogHintPlan(
                archipelago.VogFoolishAreas
            );
            vogGeneralLocations = NormalizeVogHintPlan(
                archipelago.VogGeneralLocations
            );
            vogHintSettingsBound = true;
        }

        private bool VogHintSettingsMatch(Archipelago archipelago)
        {
            return archipelago != null &&
                   vogWothHintCount == archipelago.VogWothHintCount &&
                   vogFoolishHintCount ==
                       archipelago.VogFoolishHintCount &&
                   vogGeneralHintCount ==
                       archipelago.VogGeneralHintCount &&
                   VogHintPlanMatches(
                       vogWothAreas,
                       archipelago.VogWothAreas
                   ) &&
                   VogHintPlanMatches(
                       vogFoolishAreas,
                       archipelago.VogFoolishAreas
                   ) &&
                   VogHintPlanMatches(
                       vogGeneralLocations,
                       archipelago.VogGeneralLocations
                   );
        }

        private static bool VogHintPlanMatches(
            IReadOnlyList<string> savedPlan,
            IReadOnlyList<string> roomPlan
        )
        {
            if (savedPlan == null || roomPlan == null ||
                savedPlan.Count != roomPlan.Count)
            {
                return false;
            }

            for (int index = 0; index < savedPlan.Count; index++)
            {
                if (!string.Equals(
                        savedPlan[index],
                        roomPlan[index],
                        StringComparison.Ordinal
                    ))
                {
                    return false;
                }
            }

            return true;
        }

        private bool RandomizationModesMatch(Archipelago archipelago)
        {
            return !TryGetRandomizationModeMismatch(
                archipelago,
                out _,
                out _,
                out _
            );
        }

        private bool TryGetRandomizationModeMismatch(
            Archipelago archipelago,
            out string optionName,
            out RandomizationMode savedMode,
            out RandomizationMode roomMode
        )
        {
            optionName = string.Empty;
            savedMode = RandomizationMode.Anywhere;
            roomMode = RandomizationMode.Anywhere;
            if (archipelago == null)
            {
                return false;
            }

            Tuple<string, RandomizationMode, RandomizationMode>[] modes =
            {
                Tuple.Create("skill_randomization", skillRandomization, archipelago.SkillRandomization),
                Tuple.Create("tool_randomization", toolRandomization, archipelago.ToolRandomization),
                Tuple.Create("silk_skill_randomization", silkSkillRandomization, archipelago.SilkSkillRandomization),
                Tuple.Create("crest_randomization", crestRandomization, archipelago.CrestRandomization),
                Tuple.Create("flea_randomization", fleaRandomization, archipelago.FleaRandomization),
                Tuple.Create("crest_slot_randomization", crestSlotRandomization, archipelago.CrestSlotRandomization),
                Tuple.Create("mask_shard_randomization", maskShardRandomization, archipelago.MaskShardRandomization),
                Tuple.Create("spool_fragment_randomization", spoolFragmentRandomization, archipelago.SpoolFragmentRandomization),
                Tuple.Create("silk_heart_randomization", silkHeartRandomization, archipelago.SilkHeartRandomization),
                Tuple.Create("bellway_randomization", bellwayRandomization, archipelago.BellwayRandomization),
                Tuple.Create("ventrica_randomization", ventricaRandomization, archipelago.VentricaRandomization),
                Tuple.Create("map_randomization", mapRandomization, archipelago.MapRandomization),
                Tuple.Create("melody_randomization", melodyRandomization, archipelago.MelodyRandomization),
                Tuple.Create("pin_randomization", pinRandomization, archipelago.PinRandomization),
                Tuple.Create("relic_randomization", relicRandomization, archipelago.RelicRandomization),
                Tuple.Create("crafting_kit_randomization", craftingKitRandomization, archipelago.CraftingKitRandomization),
                Tuple.Create("minor_pickup_randomization", minorPickupRandomization, archipelago.MinorPickupRandomization),
                Tuple.Create("lore_tablet_randomization", loreTabletRandomization, archipelago.LoreTabletRandomization),
                Tuple.Create("simple_key_randomization", simpleKeyRandomization, archipelago.SimpleKeyRandomization),
                Tuple.Create("memory_locket_randomization", memoryLocketRandomization, archipelago.MemoryLocketRandomization),
                Tuple.Create("craftmetal_randomization", craftmetalRandomization, archipelago.CraftmetalRandomization),
                Tuple.Create("mossberry_randomization", mossberryRandomization, archipelago.MossberryRandomization),
                Tuple.Create("pollip_heart_randomization", pollipHeartRandomization, archipelago.PollipHeartRandomization),
                Tuple.Create("silkeater_randomization", silkeaterRandomization, archipelago.SilkeaterRandomization),
                Tuple.Create("major_key_randomization", majorKeyRandomization, archipelago.MajorKeyRandomization),
                Tuple.Create("tool_pouch_randomization", toolPouchRandomization, archipelago.ToolPouchRandomization),
                Tuple.Create("boss_sanity", bossSanity, archipelago.BossSanity),
                Tuple.Create("bell_shrine_sanity", bellShrineSanity, archipelago.BellShrineSanity),
                Tuple.Create("quest_sanity", questSanityMode, archipelago.QuestSanity),
            };

            foreach (Tuple<string, RandomizationMode, RandomizationMode> mode in modes)
            {
                if (mode.Item2 == mode.Item3)
                {
                    continue;
                }

                optionName = mode.Item1;
                savedMode = mode.Item2;
                roomMode = mode.Item3;
                return true;
            }

            return false;
        }

        private bool TryGetPurchasePriceModeMismatch(
            Archipelago archipelago,
            out string optionName,
            out string savedMode,
            out string roomMode
        )
        {
            optionName = string.Empty;
            savedMode = string.Empty;
            roomMode = string.Empty;
            if (archipelago == null)
            {
                return false;
            }

            Tuple<string, string, string>[] modes =
            {
                Tuple.Create("normal_shop_prices", normalShopPrices, archipelago.NormalShopPrices),
                Tuple.Create("bellway_prices", bellwayPrices, archipelago.BellwayPrices),
                Tuple.Create("map_prices", mapPrices, archipelago.MapPrices),
                Tuple.Create("pin_prices", pinPrices, archipelago.PinPrices),
                Tuple.Create("upgrade_prices", upgradePrices, archipelago.UpgradePrices),
                Tuple.Create("donation_prices", donationPrices, archipelago.DonationPrices),
                Tuple.Create("vog_hint_prices", vogHintPrices, archipelago.VogHintPrices),
            };
            foreach (Tuple<string, string, string> mode in modes)
            {
                if (string.Equals(
                        mode.Item2,
                        mode.Item3,
                        StringComparison.Ordinal
                    ))
                {
                    continue;
                }

                optionName = mode.Item1;
                savedMode = mode.Item2;
                roomMode = mode.Item3;
                return true;
            }
            return false;
        }

        public string GetRoomMismatchMessage(Archipelago archipelago)
        {
            bool roomIdentityMatches = archipelago != null &&
                string.Equals(roomSeed, archipelago.RoomSeed, StringComparison.Ordinal) &&
                team == archipelago.Team &&
                slot == archipelago.Slot;

            if (roomIdentityMatches &&
                !string.Equals(
                    worldVersion,
                    archipelago.WorldVersion,
                    StringComparison.Ordinal
                ))
            {
                return "This randomizer save uses APWorld version '" +
                       (string.IsNullOrWhiteSpace(worldVersion)
                           ? "missing"
                           : worldVersion) +
                       "', but the current slot uses '" +
                       (string.IsNullOrWhiteSpace(archipelago.WorldVersion)
                           ? "missing"
                           : archipelago.WorldVersion) +
                       "'. Generate a new seed with the current APWorld and " +
                       "start a new randomizer save.";
            }

            if (roomIdentityMatches &&
                !string.Equals(goal, archipelago.Goal, StringComparison.Ordinal))
            {
                return "This randomizer save uses AP goal '" +
                       (string.IsNullOrWhiteSpace(goal) ? "missing" : goal) +
                       "', but the current slot uses '" +
                       (string.IsNullOrWhiteSpace(archipelago.Goal)
                           ? "missing"
                           : archipelago.Goal) +
                       "'. Start or load the save created for this slot's goal.";
            }

            if (roomIdentityMatches &&
                !string.Equals(
                    spellingBeePhrase,
                    archipelago.SpellingBeePhrase,
                    StringComparison.Ordinal
                ))
            {
                return "This randomizer save uses spelling_bee_phrase '" +
                       spellingBeePhrase +
                       "', but the current slot uses '" +
                       archipelago.SpellingBeePhrase +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                string.Equals(
                    goal,
                    Archipelago.FleaHuntGoal,
                    StringComparison.Ordinal
                ) &&
                fleaHuntGoalCount != archipelago.FleaHuntGoalCount)
            {
                return "This randomizer save uses flea_hunt_count '" +
                       fleaHuntGoalCount +
                       "', but the current slot uses '" +
                       archipelago.FleaHuntGoalCount +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                !string.Equals(
                    startingLocation,
                    archipelago.StartingLocation,
                    StringComparison.Ordinal
                ))
            {
                return "This randomizer save uses starting location '" +
                       (string.IsNullOrWhiteSpace(startingLocation)
                           ? "missing"
                           : startingLocation) +
                       "', but the current slot uses '" +
                       (archipelago == null ||
                        string.IsNullOrWhiteSpace(archipelago.StartingLocation)
                           ? "missing"
                           : archipelago.StartingLocation) +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                !string.Equals(
                    startingCrest,
                    archipelago.StartingCrest,
                    StringComparison.Ordinal
                ))
            {
                return "This randomizer save uses starting crest '" +
                       (string.IsNullOrWhiteSpace(startingCrest)
                           ? "missing"
                           : startingCrest) +
                       "', but the current slot uses '" +
                       (archipelago == null ||
                        string.IsNullOrWhiteSpace(archipelago.StartingCrest)
                           ? "missing"
                           : archipelago.StartingCrest) +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                splitDashAndSprint != archipelago.SplitDashAndSprint)
            {
                return "This randomizer save uses split_dash_and_sprint '" +
                       splitDashAndSprint.ToString().ToLowerInvariant() +
                       "', but the current slot uses '" +
                       archipelago.SplitDashAndSprint
                           .ToString()
                           .ToLowerInvariant() +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                randomizeNeedleUpgrades !=
                    archipelago.RandomizeNeedleUpgrades)
            {
                return "This randomizer save uses randomize_needle_upgrades '" +
                       randomizeNeedleUpgrades
                           .ToString()
                           .ToLowerInvariant() +
                       "', but the current slot uses '" +
                       archipelago.RandomizeNeedleUpgrades
                           .ToString()
                           .ToLowerInvariant() +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                startWithMaps != archipelago.StartWithMaps)
            {
                return GetBooleanSettingMismatchMessage(
                    "start_with_maps",
                    startWithMaps,
                    archipelago.StartWithMaps
                );
            }

            if (roomIdentityMatches &&
                startFullyMapped != archipelago.StartFullyMapped)
            {
                return GetBooleanSettingMismatchMessage(
                    "start_fully_mapped",
                    startFullyMapped,
                    archipelago.StartFullyMapped
                );
            }

            if (roomIdentityMatches &&
                automaticCompass != archipelago.AutomaticCompass)
            {
                return GetBooleanSettingMismatchMessage(
                    "automatic_compass",
                    automaticCompass,
                    archipelago.AutomaticCompass
                );
            }

            if (roomIdentityMatches &&
                checkMapMarkers != archipelago.CheckMapMarkers)
            {
                return "This randomizer save uses check_map_markers '" +
                       GetCheckMapMarkerModeName(checkMapMarkers) +
                       "', but the current slot uses '" +
                       GetCheckMapMarkerModeName(
                           archipelago.CheckMapMarkers
                       ) +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                randomizedBellMarkers !=
                    archipelago.RandomizedBellMarkers)
            {
                return GetBooleanSettingMismatchMessage(
                    "randomized_bell_markers",
                    randomizedBellMarkers,
                    archipelago.RandomizedBellMarkers
                );
            }

            if (roomIdentityMatches &&
                randomizedMelodyMarkers !=
                    archipelago.RandomizedMelodyMarkers)
            {
                return GetBooleanSettingMismatchMessage(
                    "randomized_melody_markers",
                    randomizedMelodyMarkers,
                    archipelago.RandomizedMelodyMarkers
                );
            }

            if (roomIdentityMatches &&
                vogHintSettingsBound &&
                !VogHintSettingsMatch(archipelago))
            {
                return "This randomizer save uses a different Vog hint " +
                       "plan than the current slot. Start or load the save " +
                       "created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                !string.Equals(
                    bellwayAccess,
                    archipelago.BellwayAccess,
                    StringComparison.Ordinal
                ))
            {
                return "This randomizer save uses bellway_access '" +
                       bellwayAccess +
                       "', but the current slot uses '" +
                       archipelago.BellwayAccess +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                !string.Equals(
                    trailsEndRequirement,
                    archipelago.TrailsEndRequirement,
                    StringComparison.Ordinal
                ))
            {
                return "This randomizer save uses trails_end_requirement '" +
                       trailsEndRequirement +
                       "', but the current slot uses '" +
                       archipelago.TrailsEndRequirement +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                !string.Equals(
                    enemyRosaryMultiplier,
                    archipelago.EnemyRosaryMultiplier,
                    StringComparison.Ordinal
                ))
            {
                return "This randomizer save uses enemy_rosary_multiplier '" +
                       enemyRosaryMultiplier +
                       "', but the current slot uses '" +
                       archipelago.EnemyRosaryMultiplier +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                !string.Equals(
                    enemyShardMultiplier,
                    archipelago.EnemyShardMultiplier,
                    StringComparison.Ordinal
                ))
            {
                return "This randomizer save uses enemy_shard_multiplier '" +
                       enemyShardMultiplier +
                       "', but the current slot uses '" +
                       archipelago.EnemyShardMultiplier +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                TryGetPurchasePriceModeMismatch(
                    archipelago,
                    out string priceOption,
                    out string savedPriceMode,
                    out string roomPriceMode
                ))
            {
                return "This randomizer save uses " + priceOption +
                       " '" + savedPriceMode +
                       "', but the current slot uses '" +
                       roomPriceMode +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                !PurchasePriceSettingsMatch(archipelago))
            {
                return "This randomizer save's resolved purchase prices " +
                       "do not match the current slot. Start or load the " +
                       "save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                fasterDialogue != archipelago.FasterDialogue)
            {
                return GetBooleanSettingMismatchMessage(
                    "faster_dialogue",
                    fasterDialogue,
                    archipelago.FasterDialogue
                );
            }

            if (roomIdentityMatches &&
                alphabetMode != archipelago.AlphabetMode)
            {
                return GetBooleanSettingMismatchMessage(
                    "alphabet_mode",
                    alphabetMode,
                    archipelago.AlphabetMode
                );
            }

            if (roomIdentityMatches &&
                deathLink != archipelago.DeathLink)
            {
                return GetBooleanSettingMismatchMessage(
                    "death_link",
                    deathLink,
                    archipelago.DeathLink
                );
            }

            if (roomIdentityMatches &&
                !string.Equals(
                    deathLinkCocoon,
                    archipelago.DeathLinkCocoon,
                    StringComparison.Ordinal
                ))
            {
                return "This randomizer save uses death_link_cocoon '" +
                       deathLinkCocoon +
                       "', but the current slot uses '" +
                       archipelago.DeathLinkCocoon +
                       "'. Start or load the save created for this slot's settings.";
            }

            if (roomIdentityMatches &&
                silkLink != archipelago.SilkLink)
            {
                return GetBooleanSettingMismatchMessage(
                    "silk_link",
                    silkLink,
                    archipelago.SilkLink
                );
            }

            if (roomIdentityMatches &&
                rosaryLink != archipelago.RosaryLink)
            {
                return GetBooleanSettingMismatchMessage(
                    "rosary_link",
                    rosaryLink,
                    archipelago.RosaryLink
                );
            }

            if (roomIdentityMatches &&
                shellShardLink != archipelago.ShellShardLink)
            {
                return GetBooleanSettingMismatchMessage(
                    "shell_shard_link",
                    shellShardLink,
                    archipelago.ShellShardLink
                );
            }

            if (roomIdentityMatches &&
                individualRelicTurnIns !=
                    archipelago.IndividualRelicTurnIns)
            {
                return GetBooleanSettingMismatchMessage(
                    "individual_relic_turn_ins",
                    individualRelicTurnIns,
                    archipelago.IndividualRelicTurnIns
                );
            }

            if (roomIdentityMatches &&
                TryGetRandomizationModeMismatch(
                    archipelago,
                    out string optionName,
                    out RandomizationMode savedMode,
                    out RandomizationMode roomMode
                ))
            {
                return "This randomizer save uses " + optionName + " '" +
                       savedMode.ToString().ToLowerInvariant() +
                       "', but the current slot uses '" +
                       roomMode.ToString().ToLowerInvariant() +
                       "'. Start or load the save created for this slot's settings.";
            }

            string savedSlot = string.IsNullOrWhiteSpace(slotName) ? slot.ToString() : slotName;
            string currentSlot = archipelago == null || string.IsNullOrWhiteSpace(archipelago.SlotName)
                ? "unknown"
                : archipelago.SlotName;

            return "This randomizer save belongs to AP seed '" + roomSeed +
                   "', slot '" + savedSlot + "', but the current connection is seed '" +
                   (archipelago == null ? "unknown" : archipelago.RoomSeed) +
                   "', slot '" + currentSlot + "'.";
        }

        private static string GetBooleanSettingMismatchMessage(
            string optionName,
            bool savedValue,
            bool roomValue
        )
        {
            return "This randomizer save uses " + optionName + " '" +
                   savedValue.ToString().ToLowerInvariant() +
                   "', but the current slot uses '" +
                   roomValue.ToString().ToLowerInvariant() +
                   "'. Start or load the save created for this slot's settings.";
        }

        private static string GetCheckMapMarkerModeName(
            CheckMapMarkerMode mode
        )
        {
            switch (mode)
            {
                case CheckMapMarkerMode.MappedRooms:
                    return "mapped_rooms";
                case CheckMapMarkerMode.OwnedMaps:
                    return "owned_maps";
                case CheckMapMarkerMode.All:
                    return "all";
                default:
                    return "off";
            }
        }

        public Item GetItem(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return null;
            }

            string canonicalName = ItemSet.GetCanonicalItemName(itemName);
            return items.items.FirstOrDefault(x =>
                string.Equals(x.Name, canonicalName, StringComparison.OrdinalIgnoreCase)
            );
        }

        public RandomizationMode GetRandomizationMode(ItemType type)
        {
            switch (type)
            {
                case ItemType.Skill:
                    return skillRandomization;
                case ItemType.Tool:
                    return toolRandomization;
                case ItemType.Spell:
                    return silkSkillRandomization;
                case ItemType.Crest:
                    return crestRandomization;
                case ItemType.Flea:
                    return fleaRandomization;
                case ItemType.CrestSlot:
                    return crestSlotRandomization;
                case ItemType.MaskShard:
                    return maskShardRandomization;
                case ItemType.SpoolFragment:
                    return spoolFragmentRandomization;
                case ItemType.SilkHeart:
                    return silkHeartRandomization;
                case ItemType.Bellway:
                    return bellwayRandomization;
                case ItemType.Ventrica:
                    return ventricaRandomization;
                case ItemType.Map:
                    return mapRandomization;
                case ItemType.Melody:
                    return melodyRandomization;
                case ItemType.Pin:
                    return pinRandomization;
                case ItemType.Relic:
                    return relicRandomization;
                case ItemType.Upgrade:
                    return craftingKitRandomization;
                case ItemType.Resource:
                    return minorPickupRandomization;
                case ItemType.LoreTablet:
                    return loreTabletRandomization;
                case ItemType.SimpleKey:
                    return simpleKeyRandomization;
                case ItemType.MemoryLocket:
                    return memoryLocketRandomization;
                case ItemType.Craftmetal:
                    return craftmetalRandomization;
                case ItemType.Mossberry:
                    return mossberryRandomization;
                case ItemType.PollipHeart:
                    return pollipHeartRandomization;
                case ItemType.Silkeater:
                    return silkeaterRandomization;
                case ItemType.MajorKey:
                    return majorKeyRandomization;
                case ItemType.ToolPouch:
                    return toolPouchRandomization;
                case ItemType.Boss:
                    return bossSanity;
                case ItemType.BellShrine:
                    return bellShrineSanity;
                case ItemType.Quest:
                    return questSanityMode;
                case ItemType.NeedleUpgrade:
                    return randomizeNeedleUpgrades
                        ? RandomizationMode.Anywhere
                        : RandomizationMode.Vanilla;
                default:
                    return RandomizationMode.Anywhere;
            }
        }

        public bool IsRandomized(ItemType type)
        {
            // start_with_maps converts every map source into a real AP check
            // even when map_randomization itself is vanilla: 27 map rewards
            // are precollected/replaced with filler and Verdania stays at its
            // source as an AP-delivered map. Runtime interception and popup
            // suppression must therefore remain active for the whole lane.
            if (type == ItemType.Map && startWithMaps)
            {
                return true;
            }

            if (type == ItemType.Flea &&
                string.Equals(
                    goal,
                    Archipelago.FleaHuntGoal,
                    StringComparison.Ordinal
                ))
            {
                return true;
            }

            return GetRandomizationMode(type) != RandomizationMode.Vanilla;
        }

        internal bool NeedsStartWithMapsBootstrap()
        {
            if (!startWithMaps)
            {
                return false;
            }

            if (receivedItems == null)
            {
                return true;
            }

            return StartWithMapsItemNames.Any(
                itemName => !receivedItems.Contains(itemName)
            );
        }

        internal bool RecordStartWithMapsBootstrapItems()
        {
            if (!startWithMaps)
            {
                return false;
            }

            if (receivedItems == null)
            {
                receivedItems = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase
                );
            }

            bool changed = false;
            foreach (string itemName in StartWithMapsItemNames)
            {
                changed |= receivedItems.Add(itemName);
            }
            return changed;
        }

        internal bool IsStartWithMapsBootstrapItem(string itemName)
        {
            if (!startWithMaps || string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            return StartWithMapsItemNameSet.Contains(
                ItemSet.GetCanonicalItemName(itemName)
            );
        }

        public bool IsLocationEnabled(string locationName)
        {
            string canonicalName =
                LocationSet.GetCanonicalLocationName(locationName);
            if (string.Equals(
                    canonicalName,
                    Archipelago.GoalLocationName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (randomizeNeedleUpgrades &&
                string.Equals(
                    canonicalName,
                    "Wish: Pinmaster's Oil",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Completing the vanilla oil wish is part of the Shining
                // Needle transaction. The randomized smith location reports
                // that transaction, so Quest Sanity must not report it again.
                return false;
            }

            if (IsRandomized(ItemType.ToolPouch) &&
                string.Equals(
                    canonicalName,
                    "Wish: Bugs of Pharloom",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Journal has one physical reward. When its Tool Pouch source
                // is active, Quest Sanity must not report that completion as
                // a second AP check.
                return false;
            }

            if (ScroungeRelicTurnInManifest.IsTurnInLocation(
                    canonicalName
                ) ||
                CardiniusCylinderTurnInManifest.IsTurnInLocation(
                    canonicalName
                ))
            {
                return individualRelicTurnIns;
            }

            if (Patches.FleaPatches.IsNamedNpcFleaLocation(canonicalName))
            {
                return IsLocationInSeed(canonicalName);
            }

            if (automaticCompass &&
                string.Equals(
                    canonicalName,
                    "Compass",
                    StringComparison.OrdinalIgnoreCase))
            {
                // The option turns the otherwise-vanilla Compass source into
                // an AP check even when Tool randomization itself is off.
                return true;
            }

            Location location = locations == null
                ? null
                : locations.Locations.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Name,
                        canonicalName,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            return location == null || IsRandomized(location.Type);
        }

        public bool IsLocationChecked(string locationName)
        {
            return !string.IsNullOrWhiteSpace(locationName) &&
                   checkedLocations.Contains(
                       LocationSet.GetCanonicalLocationName(locationName)
                   );
        }

        public void SetRoomLocationNames(
            IEnumerable<string> locationNames
        )
        {
            roomLocationNames = new HashSet<string>(
                (locationNames ?? Enumerable.Empty<string>())
                    .Select(LocationSet.GetCanonicalLocationName)
                    .Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase
            );
        }

        public void SetRandomizedItemMarkerLocations(
            IEnumerable<KeyValuePair<string, string>> locations
        )
        {
            randomizedItemMarkerLocationLookup = (
                locations ?? Enumerable.Empty<
                    KeyValuePair<string, string>>()
            )
                .Where(entry =>
                    !string.IsNullOrWhiteSpace(entry.Key) &&
                    !string.IsNullOrWhiteSpace(entry.Value))
                .GroupBy(
                    entry => ItemSet.GetCanonicalItemName(entry.Key),
                    StringComparer.OrdinalIgnoreCase
                )
                .ToDictionary(
                    group => group.Key,
                    group => LocationSet.GetCanonicalLocationName(
                        group.Last().Value
                    ),
                    StringComparer.OrdinalIgnoreCase
                );
            randomizedItemMarkerLocations =
                randomizedItemMarkerLocationLookup
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => new ItemMarkerLocationData(
                        entry.Key,
                        entry.Value
                    ))
                    .ToList();
        }

        public bool TryGetRandomizedItemMarkerLocation(
            string itemName,
            out string locationName
        )
        {
            locationName = string.Empty;
            return randomizedItemMarkerLocationLookup != null &&
                randomizedItemMarkerLocationLookup.TryGetValue(
                    ItemSet.GetCanonicalItemName(itemName),
                    out locationName
                ) &&
                !string.IsNullOrWhiteSpace(locationName);
        }

        public bool IsLocationInSeed(string locationName)
        {
            string canonicalName =
                LocationSet.GetCanonicalLocationName(locationName);
            if (string.IsNullOrWhiteSpace(canonicalName))
            {
                return false;
            }

            if (string.Equals(
                    canonicalName,
                    Archipelago.GoalLocationName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (roomLocationNames != null &&
                roomLocationNames.Count > 0)
            {
                return roomLocationNames.Contains(canonicalName);
            }

            // A sidecar without its room set can acquire it on connection.
            // Until then, do not show checks
            // that the seed may have left vanilla.
            Archipelago archipelago = Archipelago.Instance;
            return archipelago != null &&
                   archipelago.Connected &&
                   archipelago.IsLocationInRoom(canonicalName);
        }

        public void CheckLocation(string locationName)
        {
            string canonicalName =
                LocationSet.GetCanonicalLocationName(locationName);
            if (string.IsNullOrWhiteSpace(canonicalName) ||
                !IsLocationEnabled(canonicalName) ||
                !checkedLocations.Add(canonicalName))
            {
                return;
            }

            if (string.Equals(canonicalName, Archipelago.GoalLocationName, StringComparison.OrdinalIgnoreCase))
            {
                goalCompleted = true;
            }

            Archipelago archipelago = Archipelago.Instance;
            if (archipelago != null)
            {
                archipelago.UnlockLocation(canonicalName);
            }

            if (archipelago == null || !archipelago.Connected)
            {
                RandomizerPlugin.Instance?.RequestDisconnectSave();
            }
        }

        public bool CommitReceivedItemAtIndex(
            int itemIndex,
            string itemName,
            bool repeatable = false
        )
        {
            if (itemIndex < receivedItemIndex)
            {
                return false;
            }

            if (itemIndex > receivedItemIndex)
            {
                throw new InvalidOperationException(
                    "Received AP item out of order. Expected index " +
                    receivedItemIndex + ", got " + itemIndex + "."
                );
            }

            string canonicalItemName =
                ItemSet.GetCanonicalItemName(itemName);
            if (receivedItemHistory == null)
            {
                receivedItemHistory = new List<string>();
            }

            if (receivedItemHistory.Count > itemIndex)
            {
                throw new InvalidOperationException(
                    "Received AP item history is ahead of index " +
                    itemIndex + "."
                );
            }

            while (receivedItemHistory.Count < itemIndex)
            {
                receivedItemHistory.Add(string.Empty);
            }
            receivedItemHistory.Add(canonicalItemName);
            bool newlyReceived = receivedItems.Add(canonicalItemName);
            receivedItemIndex++;
            TryCompleteFleaHuntGoal();
            return repeatable || newlyReceived;
        }

        internal bool TrySynchronizeReceivedItemHistory(
            IReadOnlyList<string> serverItems
        )
        {
            if (serverItems == null ||
                receivedItemIndex < 0 ||
                serverItems.Count < receivedItemIndex ||
                receivedItemHistory == null ||
                receivedItemHistory.Count > receivedItemIndex)
            {
                return false;
            }

            List<string> synchronizedHistory =
                new List<string>(receivedItemIndex);
            for (int index = 0; index < receivedItemIndex; index++)
            {
                string serverItem = ItemSet.GetCanonicalItemName(
                    serverItems[index]
                );
                synchronizedHistory.Add(serverItem);
                if (receivedItems == null ||
                    !receivedItems.Contains(serverItem))
                {
                    return false;
                }

                string savedItem = index < receivedItemHistory.Count
                    ? receivedItemHistory[index]
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(savedItem))
                {
                    if (!string.Equals(
                            savedItem,
                            serverItem,
                            StringComparison.OrdinalIgnoreCase
                        ))
                    {
                        return false;
                    }
                }
            }

            receivedItemHistory = synchronizedHistory;
            return true;
        }

        public int GetReceivedFleaCount()
        {
            if (items == null || items.items == null ||
                receivedItems == null)
            {
                return 0;
            }

            return items.items.Count(
                item =>
                    item.Type == ItemType.Flea &&
                    receivedItems.Contains(item.Name)
            );
        }

        public int GetFleaHuntProgressCount()
        {
            if (IsRandomized(ItemType.Flea))
            {
                return GetReceivedFleaCount();
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return 0;
            }

            int count = Math.Max(0, Math.Min(27, playerData.SavedFleasCount));
            count += HasReceivedItemOrNativeFlea(
                Patches.FleaPatches.KrattItemName,
                playerData.CaravanLechSaved
            ) ? 1 : 0;
            count += HasReceivedItemOrNativeFlea(
                Patches.FleaPatches.VogItemName,
                playerData.MetTroupeHunterWild
            ) ? 1 : 0;
            count += HasReceivedItemOrNativeFlea(
                Patches.FleaPatches.HugeFleaItemName,
                playerData.tamedGiantFlea
            ) ? 1 : 0;
            return Math.Min(30, count);
        }

        private bool HasReceivedItemOrNativeFlea(
            string itemName,
            bool nativeFlag
        )
        {
            return nativeFlag || (
                receivedItems != null &&
                receivedItems.Contains(ItemSet.GetCanonicalItemName(itemName))
            );
        }

        public bool TryCompleteFleaHuntGoal()
        {
            if (goalCompleted ||
                !string.Equals(
                    goal,
                    Archipelago.FleaHuntGoal,
                    StringComparison.Ordinal
                ) ||
                !Archipelago.IsSupportedFleaHuntGoalCount(
                    fleaHuntGoalCount
                ) ||
                GetFleaHuntProgressCount() < fleaHuntGoalCount)
            {
                return false;
            }

            CheckLocation(Archipelago.GoalLocationName);
            return goalCompleted;
        }

        internal bool CacheHint(HintData hint)
        {
            if (hint == null ||
                string.IsNullOrWhiteSpace(hint.locationName))
            {
                return false;
            }

            string canonicalLocationName =
                LocationSet.GetCanonicalLocationName(hint.locationName);
            if (receivedHints == null)
            {
                receivedHints = new List<HintData>();
            }

            HintData existingHint = receivedHints.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(
                    candidate.locationName,
                    canonicalLocationName,
                    StringComparison.OrdinalIgnoreCase
                ));
            if (existingHint != null)
            {
                if (!hint.official)
                {
                    return false;
                }

                bool found = existingHint.found || hint.found;
                bool changed = !existingHint.official ||
                               existingHint.found != found ||
                               !string.Equals(
                                   existingHint.user,
                                   hint.user,
                                   StringComparison.Ordinal
                               ) ||
                               !string.Equals(
                                   existingHint.item,
                                   hint.item,
                                   StringComparison.Ordinal
                               ) ||
                               existingHint.flags != hint.flags;
                existingHint.user = hint.user;
                existingHint.item = hint.item;
                existingHint.flags = hint.flags;
                existingHint.official = true;
                existingHint.found = found;
                return changed;
            }

            hint.locationName = canonicalLocationName;
            receivedHints.Add(hint);
            return true;
        }

        internal IReadOnlyList<string> BuildHintJournalEntries()
        {
            List<HintData> officialHints = (receivedHints ??
                    new List<HintData>())
                .Where(hint =>
                    hint != null &&
                    hint.official &&
                    !string.IsNullOrWhiteSpace(hint.locationName) &&
                    !string.IsNullOrWhiteSpace(hint.item))
                .ToList();
            HashSet<string> addedOfficialLocations = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );
            HashSet<string> addedEntries = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );
            List<string> entries = new List<string>();

            foreach (string historyText in vogHintHistory ??
                     new List<string>())
            {
                string text = (historyText ?? string.Empty).Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                HintData hint = officialHints.FirstOrDefault(candidate =>
                    text.StartsWith(
                        candidate.locationName + " contains ",
                        StringComparison.OrdinalIgnoreCase
                    ));
                if (hint != null)
                {
                    addedOfficialLocations.Add(hint.locationName);
                    if (IsHintFound(hint))
                    {
                        continue;
                    }

                    string officialText = FormatOfficialJournalEntry(hint);
                    if (addedEntries.Add(officialText))
                    {
                        entries.Add(officialText);
                    }
                }
                else if (addedEntries.Add(text))
                {
                    entries.Add(text);
                }
            }

            foreach (HintData hint in officialHints)
            {
                if (!addedOfficialLocations.Add(hint.locationName))
                {
                    continue;
                }

                if (IsHintFound(hint))
                {
                    continue;
                }

                string text = FormatOfficialJournalEntry(hint);
                if (addedEntries.Add(text))
                {
                    entries.Add(text);
                }
            }

            return entries;
        }

        private static string FormatOfficialHint(HintData hint)
        {
            string recipient = string.IsNullOrWhiteSpace(hint.user)
                ? "Unknown player"
                : hint.user;
            return hint.locationName + " contains " + hint.item +
                   " for " + recipient + ".";
        }

        private bool IsHintFound(HintData hint)
        {
            return hint.found || IsLocationChecked(hint.locationName);
        }

        private static string FormatOfficialJournalEntry(HintData hint)
        {
            return "Unfound: " + FormatOfficialHint(hint);
        }

        public bool GetHint(
            string locationName,
            out string user,
            out string item,
            out ItemFlags flags,
            bool allowNetworkRequest = true)
        {
            user = null;
            item = null;
            flags = ItemFlags.None;

            if (string.IsNullOrWhiteSpace(locationName))
            {
                return false;
            }
            string canonicalLocationName =
                LocationSet.GetCanonicalLocationName(locationName);

            if (receivedHints == null)
            {
                receivedHints = new List<HintData>();
            }

            HintData hint = receivedHints.FirstOrDefault(x =>
                string.Equals(
                    x.locationName,
                    canonicalLocationName,
                    StringComparison.OrdinalIgnoreCase
                ));

            if (hint == null &&
                allowNetworkRequest &&
                Archipelago.Instance != null &&
                Archipelago.Instance.Connected)
            {
                hint = Archipelago.Instance.GetHint(canonicalLocationName);

                if (hint != null)
                {
                    CacheHint(hint);
                }
            }

            if (hint == null)
            {
                return false;
            }

            user = hint.user;
            item = hint.item;
            flags = hint.flags;
            return true;
        }
    }
}
