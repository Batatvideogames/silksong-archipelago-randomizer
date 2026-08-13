using System.Collections.Generic;

namespace SilksongRandomizer
{
    internal sealed class QuestLocationDefinition
    {
        internal readonly string AssetName;
        internal readonly string LocationName;

        internal QuestLocationDefinition(string assetName)
        {
            AssetName = assetName;
            LocationName = "Quest Completion: " + assetName;
        }
    }

    internal static class QuestLocationManifest
    {
        private static readonly HashSet<string>
            DonationsWithoutVanillaReward =
                new HashSet<string>(System.StringComparer.Ordinal)
                {
                    "Belltown House Start",
                    "Belltown House Mid",
                    "Building Materials",
                    "Building Materials (Bridge)",
                    "Building Materials (Statue)",
                    "Songclave Donation 1",
                    "Songclave Donation 2",
                };

        internal static readonly QuestLocationDefinition[] QuestLocations =
        {
            new QuestLocationDefinition("A Pinsmiths Tools"),
            // Restoration of Bellhart and Bellhart's Glory are two distinct
            // donations with separate QuestCompletionData records.
            new QuestLocationDefinition("Belltown House Start"),
            new QuestLocationDefinition("Belltown House Mid"),
            new QuestLocationDefinition("Building Materials"),
            new QuestLocationDefinition("Building Materials (Bridge)"),
            new QuestLocationDefinition("Building Materials (Statue)"),
            new QuestLocationDefinition("Courier Delivery Bonebottom"),
            new QuestLocationDefinition("Courier Delivery Dustpens Slave"),
            new QuestLocationDefinition("Courier Delivery Fixer"),
            new QuestLocationDefinition("Courier Delivery Fleatopia"),
            new QuestLocationDefinition("Courier Delivery Mask Maker"),
            new QuestLocationDefinition("Courier Delivery Pilgrims Rest"),
            new QuestLocationDefinition("Courier Delivery Songclave"),
            new QuestLocationDefinition("Extractor Blue Worms"),
            new QuestLocationDefinition("Fine Pins"),
            new QuestLocationDefinition("Garmond Black Threaded"),
            new QuestLocationDefinition("Great Gourmand"),
            new QuestLocationDefinition("Journal"),
            new QuestLocationDefinition("Mr Mushroom"),
            new QuestLocationDefinition("Pilgrim Rags"),
            new QuestLocationDefinition("Rock Rollers"),
            new QuestLocationDefinition("Save City Merchant"),
            new QuestLocationDefinition("Save City Merchant Bridge"),
            new QuestLocationDefinition("Save Courier Short"),
            new QuestLocationDefinition("Save Courier Tall"),
            new QuestLocationDefinition("Save Sherma"),
            new QuestLocationDefinition("Shiny Bell Goomba"),
            new QuestLocationDefinition("Skull King"),
            new QuestLocationDefinition("Song Pilgrim Cloaks"),
            new QuestLocationDefinition("Songclave Donation 1"),
            new QuestLocationDefinition("Songclave Donation 2"),
            new QuestLocationDefinition("Steel Sentinel Pt2"),
        };

        // Generic currency rewards from the quest
        // bundle. These are the only vanilla rewards Quest Sanity replaces.
        private static readonly Dictionary<string, string>
            ReplaceableVanillaRewards =
                new Dictionary<string, string>(
                    System.StringComparer.Ordinal
                )
                {
                    {
                        "Courier Delivery Bonebottom",
                        "Money Reward"
                    },
                    {
                        "Courier Delivery Dustpens Slave",
                        "Money Reward"
                    },
                    {
                        "Courier Delivery Fixer",
                        "Money Reward"
                    },
                    {
                        "Courier Delivery Fleatopia",
                        "Money Reward"
                    },
                    {
                        "Courier Delivery Mask Maker",
                        "Money Reward"
                    },
                    {
                        "Courier Delivery Pilgrims Rest",
                        "Money Reward"
                    },
                    {
                        "Courier Delivery Songclave",
                        "Money Reward"
                    },
                    { "Fine Pins", "Rosary_Set_Large" },
                    { "Pilgrim Rags", "Rosary_Set_Medium" },
                    { "Shiny Bell Goomba", "Rosary_Set_Medium" },
                    { "Skull King", "Rosary_Set_Large" },
                    { "Song Pilgrim Cloaks", "Rosary_Set_Large" },
                };

        internal static Location[] AppendTo(Location[] existingLocations)
        {
            List<Location> locations = new List<Location>(existingLocations);
            foreach (QuestLocationDefinition quest in QuestLocations)
            {
                string assetName = quest.AssetName;
                locations.Add(new Location(
                    quest.LocationName,
                    ItemType.Quest,
                    () => IsQuestCompleted(assetName)
                ));
            }

            return locations.ToArray();
        }

        internal static bool TryGetLocationName(
            string assetName,
            out string locationName
        )
        {
            if (!string.IsNullOrWhiteSpace(assetName))
            {
                foreach (QuestLocationDefinition quest in QuestLocations)
                {
                    if (string.Equals(
                            quest.AssetName,
                            assetName,
                            System.StringComparison.Ordinal))
                    {
                        locationName =
                            LocationSet.GetCanonicalLocationName(
                                quest.LocationName
                            );
                        return true;
                    }
                }
            }

            locationName = null;
            return false;
        }

        internal static bool IsDonationWithoutVanillaReward(
            string assetName
        )
        {
            return !string.IsNullOrWhiteSpace(assetName) &&
                   DonationsWithoutVanillaReward.Contains(assetName);
        }

        internal static bool TryGetReplaceableVanillaRewardLocation(
            string assetName,
            string nativeRewardAssetName,
            out string locationName
        )
        {
            if (!string.IsNullOrWhiteSpace(assetName) &&
                !string.IsNullOrWhiteSpace(nativeRewardAssetName) &&
                ReplaceableVanillaRewards.TryGetValue(
                    assetName,
                    out string expectedRewardAssetName) &&
                string.Equals(
                    expectedRewardAssetName,
                    nativeRewardAssetName,
                    System.StringComparison.Ordinal))
            {
                if (TryGetLocationName(assetName, out locationName))
                {
                    return true;
                }
            }

            locationName = null;
            return false;
        }

        internal static bool IsQuestCompleted(string assetName)
        {
            SaveState state = SaveState.Instance;
            if (state == null || !state.IsRandomized(ItemType.Quest) ||
                string.IsNullOrWhiteSpace(assetName))
            {
                return false;
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData == null || playerData.QuestCompletionData == null)
            {
                return false;
            }

            QuestCompletionData.Completion completion =
                playerData.QuestCompletionData.GetData(assetName);
            return completion.IsCompleted || completion.WasEverCompleted;
        }
    }
}
