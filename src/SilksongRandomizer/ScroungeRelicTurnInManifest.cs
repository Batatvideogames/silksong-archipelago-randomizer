using System;
using System.Collections.Generic;

namespace SilksongRandomizer
{
    internal sealed class ScroungeRelicTurnInEntry
    {
        internal readonly string AssetName;
        internal readonly string LocationName;

        internal ScroungeRelicTurnInEntry(
            string assetName,
            string relicDisplayName
        )
        {
            AssetName = assetName;
            LocationName = "Relic Turn-in: " + relicDisplayName;
        }
    }

    internal static class ScroungeRelicTurnInManifest
    {
        internal const string SceneName = "Belltown_Room_Relic";
        internal const string OwnerObjectName = "Relic Dealer NPC";

        internal static readonly ScroungeRelicTurnInEntry[] Entries =
        {
            new ScroungeRelicTurnInEntry(
                "Weaver Totem Witch",
                "Weaver Effigy (Keelal, Shellwood)"
            ),
            new ScroungeRelicTurnInEntry(
                "Bone Record Wisp Top",
                "Bone Scroll (Wisp Thicket)"
            ),
            new ScroungeRelicTurnInEntry(
                "Weaver Totem Bonetown_upper_room",
                "Weaver Effigy (Camora, Moss Grotto)"
            ),
            new ScroungeRelicTurnInEntry(
                "Seal Chit City Merchant",
                "Choral Commandment (Jubilana)"
            ),
            new ScroungeRelicTurnInEntry(
                "Weaver Record Conductor",
                "Rune Harp (High Halls)"
            ),
            new ScroungeRelicTurnInEntry(
                "Seal Chit Ward Corpse",
                "Choral Commandment (Western Whiteward)"
            ),
            new ScroungeRelicTurnInEntry(
                "Weaver Record Sprint_Challenge",
                "Rune Harp (Weavenest Cindril)"
            ),
            new ScroungeRelicTurnInEntry(
                "Weaver Record Weave_08",
                "Rune Harp (Weavenest Atla)"
            ),
            new ScroungeRelicTurnInEntry(
                "Bone Record Understore_Map_Room",
                "Bone Scroll (Underworks)"
            ),
            new ScroungeRelicTurnInEntry(
                "Bone Record Bone_East_14",
                "Bone Scroll (Far Fields)"
            ),
            new ScroungeRelicTurnInEntry(
                "Seal Chit Aspid_01",
                "Choral Commandment (Moss Grotto)"
            ),
            new ScroungeRelicTurnInEntry(
                "Seal Chit Silk Siphon",
                "Choral Commandment (Eastern Whiteward)"
            ),
            new ScroungeRelicTurnInEntry(
                "Bone Record Greymoor_flooded_corridor",
                "Bone Scroll (Greymoor)"
            ),
            new ScroungeRelicTurnInEntry(
                "Weaver Totem Slab_Bottom",
                "Weaver Effigy (Atla, The Slab)"
            ),
            new ScroungeRelicTurnInEntry(
                "Ancient Egg Abyss Middle",
                "Arcane Egg"
            ),
        };

        private static readonly Dictionary<string, string>
            LocationsByAsset = BuildLocationsByAsset();

        private static readonly HashSet<string> LocationNames =
            BuildLocationNames();

        internal static IEnumerable<Location> AppendTo(
            IEnumerable<Location> existingLocations
        )
        {
            List<Location> locations = new List<Location>(existingLocations);
            foreach (ScroungeRelicTurnInEntry entry in Entries)
            {
                ScroungeRelicTurnInEntry capturedEntry = entry;
                locations.Add(new Location(
                    capturedEntry.LocationName,
                    ItemType.Event,
                    () => IsDeposited(capturedEntry.AssetName)
                ));
            }

            return locations.ToArray();
        }

        internal static bool IsTurnInLocation(string locationName)
        {
            return !string.IsNullOrWhiteSpace(locationName) &&
                   LocationNames.Contains(
                       LocationSet.GetCanonicalLocationName(locationName)
                   );
        }

        internal static bool TryGetLocationName(
            string assetName,
            out string locationName
        )
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                locationName = null;
                return false;
            }

            return LocationsByAsset.TryGetValue(assetName, out locationName);
        }

        internal static bool IsDeposited(string assetName)
        {
            SaveState state = SaveState.Instance;
            if (state == null || !state.individualRelicTurnIns ||
                CollectableRelicManager.Instance == null ||
                !LocationsByAsset.ContainsKey(assetName))
            {
                return false;
            }

            CollectableRelic relic =
                CollectableRelicManager.GetRelic(assetName);
            return relic != null && relic.SavedData.IsDeposited;
        }

        private static Dictionary<string, string> BuildLocationsByAsset()
        {
            Dictionary<string, string> locations =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ScroungeRelicTurnInEntry entry in Entries)
            {
                locations.Add(entry.AssetName, entry.LocationName);
            }

            return locations;
        }

        private static HashSet<string> BuildLocationNames()
        {
            HashSet<string> names = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );
            foreach (ScroungeRelicTurnInEntry entry in Entries)
            {
                names.Add(entry.LocationName);
            }

            return names;
        }
    }
}
