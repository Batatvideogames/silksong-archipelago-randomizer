using System;

namespace SilksongRandomizer
{
    /// <summary>
    /// Keeps an out-of-logic Widow clear recoverable without changing the
    /// player's AP inventory or permanently granting a movement ability.
    /// </summary>
    internal static class WidowSequenceSafety
    {
        internal const string WidowShrineSceneName = "Belltown_Shrine";
        internal const string NeedolinMemorySceneName = "Memory_Needolin";
        internal const string WidowWakeGateName = "door_wakeOnGround";

        internal static bool IsInNeedolinMemory()
        {
            SaveState state = SaveState.Instance;
            GameManager gameManager = GameManager.instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                !state.IsRandomized(ItemType.Skill) ||
                gameManager == null ||
                playerData == null ||
                !playerData.spinnerDefeated ||
                playerData.bellShrineBellhart)
            {
                return false;
            }

            string sceneName = gameManager.sceneName ?? string.Empty;
            return string.Equals(
                       sceneName,
                       NeedolinMemorySceneName,
                       StringComparison.Ordinal
                   ) ||
                   sceneName.StartsWith(
                       NeedolinMemorySceneName + "_",
                       StringComparison.Ordinal
                   );
        }

        internal static bool CanRecoverToWidowShrine()
        {
            return IsInNeedolinMemory();
        }

        internal static bool CanUseEmergencySwiftStep(
            SaveState state
        )
        {
            return state != null &&
                   state.IsRandomized(ItemType.Skill) &&
                   IsInNeedolinMemory();
        }
    }
}
