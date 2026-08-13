using System;
using System.Collections.Generic;

namespace SilksongRandomizer
{
    /// <summary>
    /// Stable save identities for every physical Mask Shard and Spool
    /// Fragment source. SceneData, purchase flags and quest completion
    /// identify the source which was actually collected without relying on
    /// cumulative upgrade counters or acquisition order.
    /// </summary>
    internal static class MaskAndSpoolLocationManifest
    {
        internal sealed class Source
        {
            internal readonly string SourceLocationName;
            internal readonly ItemType Type;
            internal readonly string SceneName;
            internal readonly string PersistentBoolId;
            internal readonly string PlayerDataBool;
            internal readonly string QuestAssetName;
            internal readonly string PlayerDataInt;
            internal readonly int MinimumIntValue;

            internal Source(
                string sourceLocationName,
                ItemType type,
                string sceneName = null,
                string persistentBoolId = null,
                string playerDataBool = null,
                string questAssetName = null,
                string playerDataInt = null,
                int minimumIntValue = 0)
            {
                SourceLocationName = sourceLocationName;
                Type = type;
                SceneName = sceneName;
                PersistentBoolId = persistentBoolId;
                PlayerDataBool = playerDataBool;
                QuestAssetName = questAssetName;
                PlayerDataInt = playerDataInt;
                MinimumIntValue = minimumIntValue;
            }

            internal string LocationName =>
                LocationSet.GetCanonicalLocationName(SourceLocationName);

            internal bool IsPhysicalSceneSource =>
                !string.IsNullOrEmpty(SceneName) &&
                !string.IsNullOrEmpty(PersistentBoolId);
        }

        internal static readonly Source[] Sources =
        {
            // Numbered source identities are stable protocol names. Their
            // player-facing names are the fixed sources in LocationSet.
            new Source(
                "Mask Shard Unlock #1",
                ItemType.MaskShard,
                playerDataBool: "PurchasedBonebottomHeartPiece"
            ),
            new Source(
                "Mask Shard Unlock #2",
                ItemType.MaskShard,
                "Crawl_02",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #3",
                ItemType.MaskShard,
                "Bone_East_20",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #4",
                ItemType.MaskShard,
                "Shellwood_14",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #5",
                ItemType.MaskShard,
                "Dock_08",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #6",
                ItemType.MaskShard,
                "Weave_05b",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #7",
                ItemType.MaskShard,
                questAssetName: "Beastfly Hunt"
            ),
            new Source(
                "Mask Shard Unlock #8",
                ItemType.MaskShard,
                "Song_09",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #9",
                ItemType.MaskShard,
                "Library_05",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #10",
                ItemType.MaskShard,
                "Shadow_13",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #11",
                ItemType.MaskShard,
                "Bone_East_LavaChallenge",
                "Heart Piece (1)"
            ),
            new Source(
                "Mask Shard Unlock #12",
                ItemType.MaskShard,
                "Slab_17",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #13",
                ItemType.MaskShard,
                "Peak_04c",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #14",
                ItemType.MaskShard,
                "Wisp_07",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #15",
                ItemType.MaskShard,
                playerDataBool: "MerchantEnclaveShellFragment"
            ),
            new Source(
                "Mask Shard Unlock #16",
                ItemType.MaskShard,
                "Coral_19b",
                "Heart Piece"
            ),
            new Source(
                "Mask Shard Unlock #17",
                ItemType.MaskShard,
                questAssetName: "Sprintmaster Race"
            ),
            new Source(
                "Mask Shard Unlock #18",
                ItemType.MaskShard,
                questAssetName: "Ant Trapper"
            ),
            new Source(
                "Mask Shard Unlock #19",
                ItemType.MaskShard,
                questAssetName: "Destroy Thread Cores"
            ),
            new Source(
                "Mask Shard Unlock #20",
                ItemType.MaskShard,
                "Peak_06",
                "Heart Piece"
            ),

            // Spool Fragments.
            new Source(
                "Spool Fragment Unlock #1",
                ItemType.SpoolFragment,
                "Bone_11b",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #2",
                ItemType.SpoolFragment,
                "Bone_East_13",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #3",
                ItemType.SpoolFragment,
                "Greymoor_02",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #4",
                ItemType.SpoolFragment,
                "Peak_01",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #5",
                ItemType.SpoolFragment,
                "Weave_11",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #6",
                ItemType.SpoolFragment,
                playerDataBool: "PurchasedBelltownSpoolSegment"
            ),
            new Source(
                "Spool Fragment Unlock #7",
                ItemType.SpoolFragment,
                playerDataBool: "MetCaravanTroupeLeaderJudge"
            ),
            new Source(
                "Spool Fragment Unlock #8",
                ItemType.SpoolFragment,
                "Cog_07",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #9",
                ItemType.SpoolFragment,
                "Library_11b",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #10",
                ItemType.SpoolFragment,
                "Song_19_entrance",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #11",
                ItemType.SpoolFragment,
                "Under_10",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #12",
                ItemType.SpoolFragment,
                "Ward_01",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #13",
                ItemType.SpoolFragment,
                questAssetName: "Save Sherma"
            ),
            new Source(
                "Spool Fragment Unlock #14",
                ItemType.SpoolFragment,
                "Dock_03c",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #15",
                ItemType.SpoolFragment,
                "Hang_03_top",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #16",
                ItemType.SpoolFragment,
                "Arborium_09",
                "Silk Spool"
            ),
            new Source(
                "Spool Fragment Unlock #17",
                ItemType.SpoolFragment,
                playerDataBool: "purchasedGrindleSpoolPiece"
            ),
            new Source(
                "Spool Fragment Unlock #18",
                ItemType.SpoolFragment,
                playerDataBool: "MerchantEnclaveSpoolPiece"
            ),
        };

        internal static bool TryGetQuestSource(
            string questAssetName,
            out string locationName,
            out ItemType type
        )
        {
            if (!string.IsNullOrWhiteSpace(questAssetName))
            {
                foreach (Source source in Sources)
                {
                    if (string.Equals(
                            source.QuestAssetName,
                            questAssetName,
                            StringComparison.Ordinal))
                    {
                        locationName = source.LocationName;
                        type = source.Type;
                        return true;
                    }
                }
            }

            locationName = null;
            type = ItemType.Unknown;
            return false;
        }

        internal static Location[] AppendTo(Location[] existingLocations)
        {
            List<Location> locations =
                new List<Location>(existingLocations);
            foreach (Source source in Sources)
            {
                Source capturedSource = source;
                locations.Add(new Location(
                    capturedSource.SourceLocationName,
                    capturedSource.Type,
                    () => IsCollected(capturedSource)
                ));
            }
            return locations.ToArray();
        }

        internal static bool TryGetPhysicalLocation(
            ItemType type,
            string sceneName,
            out string locationName)
        {
            return TryGetPhysicalLocation(
                type,
                sceneName,
                null,
                out locationName
            );
        }

        internal static bool TryGetPhysicalLocation(
            ItemType type,
            string sceneName,
            string persistentBoolId,
            out string locationName)
        {
            locationName = null;
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            foreach (Source source in Sources)
            {
                if (source.Type == type &&
                    source.IsPhysicalSceneSource &&
                    string.Equals(
                        source.SceneName,
                        sceneName,
                        StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(persistentBoolId) ||
                     string.Equals(
                         source.PersistentBoolId,
                         persistentBoolId,
                         StringComparison.Ordinal)))
                {
                    locationName = source.LocationName;
                    return true;
                }
            }
            return false;
        }

        private static bool IsCollected(Source source)
        {
            if (source == null)
            {
                return false;
            }

            if (source.IsPhysicalSceneSource)
            {
                SceneData sceneData = SceneData.instance;
                return sceneData != null &&
                       sceneData.PersistentBools.GetValueOrDefault(
                           source.SceneName,
                           source.PersistentBoolId
                       );
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(source.PlayerDataBool))
            {
                return playerData.GetBool(source.PlayerDataBool);
            }

            if (!string.IsNullOrEmpty(source.PlayerDataInt))
            {
                return playerData.GetInt(source.PlayerDataInt) >=
                       source.MinimumIntValue;
            }

            if (!string.IsNullOrEmpty(source.QuestAssetName) &&
                playerData.QuestCompletionData != null)
            {
                QuestCompletionData.Completion completion =
                    playerData.QuestCompletionData.GetData(
                        source.QuestAssetName
                    );
                return completion.IsCompleted ||
                       completion.WasEverCompleted;
            }

            return false;
        }
    }
}
