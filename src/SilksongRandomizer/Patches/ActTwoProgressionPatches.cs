using HarmonyLib;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(HeroController), "SendHeroInPosition")]
    internal static class ActTwoProgressionPatches
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            GameManager gameManager = GameManager.SilentInstance;
            if (state == null ||
                !state.IsRoomBound ||
                playerData == null ||
                gameManager == null ||
                gameManager.profileID < 0 ||
                playerData.act2Started ||
                !playerData.visitedCitadel ||
                !playerData.laceMeetCitadel ||
                !playerData.citadelWoken)
            {
                return;
            }

            playerData.act2Started = true;
            gameManager.QueueSaveGame();
            BellShrinePatches.ReconcileGrandGateQuest();
        }
    }
}
