using System;
using System.Collections.Generic;

namespace SilksongRandomizer
{
    internal sealed class CardiniusCylinderTurnInEntry
    {
        internal readonly string AssetName;
        internal readonly string LocationName;

        internal CardiniusCylinderTurnInEntry(
            string assetName,
            string relicDisplayName
        )
        {
            AssetName = assetName;
            LocationName = "Relic Turn-in: " + relicDisplayName;
        }
    }

    internal static class CardiniusCylinderTurnInManifest
    {
        internal const string SceneName = "Library_08";
        internal const string OwnerObjectName = "Librarian";

        internal static readonly CardiniusCylinderTurnInEntry[] Entries =
        {
            new CardiniusCylinderTurnInEntry(
                "Psalm Cylinder Library Roof",
                "Psalm Cylinder (East Whispering Vaults)"
            ),
            new CardiniusCylinderTurnInEntry(
                "Librarian Melody Cylinder",
                "Sacred Cylinder"
            ),
            new CardiniusCylinderTurnInEntry(
                "Psalm Cylinder Ward",
                "Psalm Cylinder (Underworks)"
            ),
            new CardiniusCylinderTurnInEntry(
                "Psalm Cylinder Librarian",
                "Psalm Cylinder (Vaultkeeper Cardinius)"
            ),
            new CardiniusCylinderTurnInEntry(
                "Psalm Cylinder Hang",
                "Psalm Cylinder (High Halls)"
            ),
            new CardiniusCylinderTurnInEntry(
                "Psalm Cylinder Grindle",
                "Psalm Cylinder (Grindle)"
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
            foreach (CardiniusCylinderTurnInEntry entry in Entries)
            {
                CardiniusCylinderTurnInEntry capturedEntry = entry;
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
            foreach (CardiniusCylinderTurnInEntry entry in Entries)
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
            foreach (CardiniusCylinderTurnInEntry entry in Entries)
            {
                names.Add(entry.LocationName);
            }

            return names;
        }
    }
}
