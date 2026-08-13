using GlobalEnums;
using SilksongRandomizer.Patches;
using System;
using static GameManager;

namespace SilksongRandomizer
{
    internal static class FastTravelUtil
    {
        private const string BellwayEntryGateName =
            "door_fastTravelExit";
        private const string BellhartSceneName = "Belltown";
        private const string BellhartEntryGateName = "door5";
        private const string SongclaveBellSceneName =
            "Bellshrine_Enclave";
        private const string SongclaveBellEntryGateName = "left1";
        private const string TerminusSceneName = "Tube_Hub";
        private const string TerminusEntryGateName = "door_tubeEnter";
        private const string ActThreeWakeSceneName = "Song_Enclave";
        private const string ActThreeWakeEntryGateName =
            "door_act3_wakeUp";
        private const string GreymoorCaravanSceneName = "Greymoor_08";
        private const string GreymoorCaravanEntryGateName = "left2";

        private enum WarpDestination
        {
            BoneBottom,
            Bellhart,
            Songclave,
            Terminus,
            Greymoor,
            WidowShrine,
        }

        internal static bool CanTeleportToPreferredHub(out string reason)
        {
            if (SaveState.Instance == null)
            {
                reason = "Load a randomizer save before warping.";
                return false;
            }

            GameManager gameManager = GameManager.instance;
            HeroController hero = HeroController.instance;
            if (gameManager == null || hero == null)
            {
                reason =
                    "The F4 warp is only available during gameplay.";
                return false;
            }

            if (gameManager.GameState != GameState.PLAYING ||
                !gameManager.IsGameplayScene())
            {
                reason =
                    "Finish the current menu or cutscene before warping.";
                return false;
            }

            PlayerData playerData = PlayerData.instance;
            bool isActThreeWakeEntry = IsActThreeWakeEntry(
                gameManager,
                hero
            );
            if (playerData != null &&
                playerData.blackThreadWorld &&
                (!playerData.act3_wokeUp ||
                 !playerData.act3_enclaveWakeSceneCompleted ||
                 isActThreeWakeEntry))
            {
                reason = isActThreeWakeEntry
                    ? "Leave the Act 3 wake-up room before using F4."
                    : "Finish the full Act 3 wake-up sequence before " +
                      "using F4.";
                return false;
            }

            if (gameManager.IsMemoryScene() &&
                !WidowSequenceSafety.CanRecoverToWidowShrine())
            {
                reason =
                    "The F4 warp is disabled inside memory sequences.";
                return false;
            }

            if (gameManager.IsInSceneTransition ||
                TransitionPoint.IsTransitionBlocked)
            {
                reason = "A scene transition is already in progress.";
                return false;
            }

            if (!hero.CanInput())
            {
                reason =
                    "Finish the current scripted action before warping.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal static string GetPreferredHubName()
        {
            switch (ResolveDestination())
            {
                case WarpDestination.WidowShrine:
                    return "Widow Shrine";
                case WarpDestination.Songclave:
                    return "Songclave";
                case WarpDestination.Terminus:
                    return "Terminus";
                case WarpDestination.Bellhart:
                    return "Bellhart";
                case WarpDestination.Greymoor:
                    return "Greymoor";
                default:
                    return "Bone Bottom";
            }
        }

        internal static bool TryTeleportToPreferredHub(out string error)
        {
            if (!CanTeleportToPreferredHub(out error))
            {
                return false;
            }

            if (!SlabCaptureWarpSafety.TryRestoreBeforeRecoveryWarp(
                    out error))
            {
                return false;
            }

            WarpDestination destination = ResolveDestination();
            string sceneName;
            string entryGateName;
            switch (destination)
            {
                case WarpDestination.WidowShrine:
                    sceneName = WidowSequenceSafety.WidowShrineSceneName;
                    entryGateName = WidowSequenceSafety.WidowWakeGateName;
                    break;
                case WarpDestination.Songclave:
                    sceneName = SongclaveBellSceneName;
                    entryGateName = SongclaveBellEntryGateName;
                    break;
                case WarpDestination.Terminus:
                    sceneName = TerminusSceneName;
                    entryGateName = TerminusEntryGateName;
                    break;
                case WarpDestination.Bellhart:
                    sceneName = BellhartSceneName;
                    entryGateName = BellhartEntryGateName;
                    break;
                case WarpDestination.Greymoor:
                    sceneName = GreymoorCaravanSceneName;
                    entryGateName = GreymoorCaravanEntryGateName;
                    break;
                default:
                    sceneName = FastTravelScenes.GetSceneName(
                        FastTravelLocations.Bonetown
                    );
                    entryGateName = BellwayEntryGateName;
                    break;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                error =
                    "Silksong could not resolve the F4 warp destination.";
                return false;
            }

            bool protectedOpeningBoss =
                destination == WarpDestination.BoneBottom &&
                MossMotherWarpSafety.PrepareForBoneBottomWarp();
            try
            {
                GameManager.instance.BeginSceneTransition(new SceneLoadInfo
                {
                    SceneName = sceneName,
                    EntryGateName = entryGateName
                });
            }
            catch
            {
                if (protectedOpeningBoss)
                {
                    MossMotherWarpSafety.CancelPreparedWarp();
                }
                throw;
            }
            return true;
        }

        private static WarpDestination ResolveDestination()
        {
            if (WidowSequenceSafety.CanRecoverToWidowShrine())
            {
                return WarpDestination.WidowShrine;
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData != null &&
                playerData.blackThreadWorld &&
                playerData.act3_wokeUp &&
                playerData.act3_enclaveWakeSceneCompleted)
            {
                return WarpDestination.Terminus;
            }
            if (playerData != null && playerData.bellShrineEnclave)
            {
                return WarpDestination.Songclave;
            }
            if (playerData != null && playerData.spinnerDefeated)
            {
                return WarpDestination.Bellhart;
            }
            SaveState state = SaveState.Instance;
            if (state != null && state.rodeFleaCaravanToGreymoor)
            {
                return WarpDestination.Greymoor;
            }

            return WarpDestination.BoneBottom;
        }

        private static bool IsActThreeWakeEntry(
            GameManager gameManager,
            HeroController hero
        )
        {
            return gameManager != null &&
                hero != null &&
                string.Equals(
                    gameManager.GetSceneNameString(),
                    ActThreeWakeSceneName,
                    StringComparison.OrdinalIgnoreCase
                ) &&
                string.Equals(
                    hero.GetEntryGateName(),
                    ActThreeWakeEntryGateName,
                    StringComparison.Ordinal
                );
        }
    }
}
