using System;
using GlobalEnums;
using System.Linq;

namespace SilksongRandomizer
{
    internal static class SteelSoulSettings
    {
        internal const string Classic = "classic";
        internal const string SteelSoul = "steel_soul";
        private static readonly string[] EarlySites = { "Shellwood_26", "Bone_East_14", "Aspid_01" };
        private static readonly string[] LaterSites = { "Hang_08", "Coral_28", "Aqueduct_05" };

        internal static void Validate(string mode, string[] sites)
        {
            if (mode != Classic && mode != SteelSoul)
                throw new FormatException("Unsupported game mode: " + mode);
            if (sites == null || (mode == Classic ? sites.Length != 0 :
                sites.Length != 3 || sites.Distinct(StringComparer.Ordinal).Count() != 3 ||
                sites.Any(site => !EarlySites.Contains(site) && !LaterSites.Contains(site)) ||
                !sites.Any(EarlySites.Contains) || !sites.Any(LaterSites.Contains)))
                throw new FormatException("The selected Steel Soul resting sites are invalid.");
        }

        internal static bool AllSitesVisited(SaveState state, PlayerData playerData) =>
            state.steelSoulSites.Length == 3 && playerData?.SteelQuestSpots != null &&
            state.steelSoulSites.All(scene => playerData.SteelQuestSpots.Any(spot =>
                spot != null && spot.SceneName == scene && spot.IsSeen));

        internal static void ApplyNewGame(SaveState state, PlayerData playerData)
        {
            Validate(state.gameMode, state.steelSoulSites);
            if (state.gameMode != SteelSoul) return;
            playerData.SteelQuestSpots = state.steelSoulSites.Select(scene =>
                new SteelSoulQuestSpot.Spot { SceneName = scene, IsSeen = false }).ToArray();
        }

        internal static bool ValidatePlayerData(SaveState state, PlayerData playerData, out string error)
        {
            error = string.Empty;
            if (playerData == null || (playerData.permadeathMode != PermadeathModes.Off) !=
                (state.gameMode == SteelSoul))
            {
                error = "This save's game mode does not match its Archipelago settings.";
                return false;
            }
            if (playerData.permadeathMode == PermadeathModes.Dead)
            {
                error = "This Steel Soul save has ended.";
                return false;
            }
            if (state.gameMode == SteelSoul && (playerData.SteelQuestSpots == null ||
                !state.steelSoulSites.SequenceEqual(playerData.SteelQuestSpots.Select(spot => spot?.SceneName))))
            {
                error = "This save's resting sites do not match its Archipelago settings.";
                return false;
            }
            return true;
        }
    }
}
