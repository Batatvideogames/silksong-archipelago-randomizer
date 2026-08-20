using GlobalEnums;
using HarmonyLib;

namespace SilksongRandomizer.Patches
{
    internal static class FleaFestivalPatches
    {
        private const string EggOfFlealiaItem =
            "Tool: Egg of Flealia";
        private const string FleatopiaScene = "Aqueduct_05";

        private struct FleaGamesStartState
        {
            internal bool Apply;
            internal bool ShouldStart;
            internal PlayerData PlayerData;
        }

        internal static bool ShouldStartFleaGames(
            SaveState state,
            PlayerData playerData,
            MapZone currentMapZone)
        {
            return state != null &&
                   state.IsRoomBound &&
                   state.IsRandomized(ItemType.Tool) &&
                   state.receivedItems != null &&
                   state.receivedItems.Contains(EggOfFlealiaItem) &&
                   playerData != null &&
                   playerData.blackThreadWorld &&
                   playerData.CaravanTroupeLocation ==
                       CaravanTroupeLocations.Aqueduct &&
                   currentMapZone != MapZone.AQUEDUCT &&
                   playerData.respawnScene != FleatopiaScene;
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.TimePasses))]
        private static class FleaGamesStartPatch
        {
            private static void Prefix(
                GameManager __instance,
                out FleaGamesStartState __state)
            {
                __state = default;
                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null ||
                    !state.IsRoomBound ||
                    !state.IsRandomized(ItemType.Tool) ||
                    playerData == null ||
                    playerData.FleaGamesCanStart)
                {
                    return;
                }

                __state.Apply = true;
                __state.PlayerData = playerData;
                __state.ShouldStart = ShouldStartFleaGames(
                    state,
                    playerData,
                    __instance.GetCurrentMapZoneEnum()
                );
            }

            private static void Postfix(FleaGamesStartState __state)
            {
                if (__state.Apply && __state.PlayerData != null)
                {
                    // Replace only the Egg condition in TimePasses. The
                    // physical tool stays separate from received AP ownership.
                    __state.PlayerData.FleaGamesCanStart =
                        __state.ShouldStart;
                }
            }
        }
    }
}
