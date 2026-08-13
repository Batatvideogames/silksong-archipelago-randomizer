using System;
using System.Collections.Generic;
using System.Linq;

namespace SilksongRandomizer
{
    internal static class CrestNames
    {
        internal const string CrestItemPrefix = "Crest: ";
        internal const string CrestLocationPrefix = "Crest Unlock: ";

        internal static readonly string[] SupportedStartingCrestKeys =
        {
            "hunter",
            "wanderer",
            "reaper",
            "beast",
            "architect",
            "witch",
            "shaman",
        };

        internal static bool IsSupportedStartingCrestKey(string key)
        {
            return SupportedStartingCrestKeys.Contains(key, StringComparer.Ordinal);
        }

        internal static string GetInternalCrestName(string key)
        {
            switch (key)
            {
                case "hunter":
                    return "Hunter";
                case "wanderer":
                    return "Wanderer";
                case "reaper":
                    return "Reaper";
                case "beast":
                    return "Warrior";
                case "architect":
                    return "Toolmaster";
                case "witch":
                    return "Witch";
                case "shaman":
                    return "Spell";
                default:
                    return null;
            }
        }

        internal static string GetPublicCrestName(string internalName)
        {
            if (string.IsNullOrWhiteSpace(internalName))
            {
                return internalName;
            }

            if (IsHunterInternalName(internalName))
            {
                return "Hunter";
            }

            switch (internalName)
            {
                case "Warrior":
                    return "Beast";
                case "Toolmaster":
                    return "Architect";
                case "Spell":
                    return "Shaman";
                default:
                    return internalName;
            }
        }

        internal static bool IsHunterInternalName(string internalName)
        {
            return !string.IsNullOrEmpty(internalName) &&
                   internalName.StartsWith("Hunter", StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetItemNameFromInternal(string internalName)
        {
            return CrestItemPrefix + GetPublicCrestName(internalName);
        }

        internal static string GetLocationNameFromInternal(string internalName)
        {
            return CrestLocationPrefix + GetPublicCrestName(internalName);
        }

        internal static bool HasReceivedAnyCrest(IEnumerable<string> receivedItems)
        {
            return receivedItems != null && receivedItems.Any(
                itemName => !string.IsNullOrEmpty(itemName) &&
                            itemName.StartsWith(CrestItemPrefix, StringComparison.OrdinalIgnoreCase)
            );
        }
    }
}
