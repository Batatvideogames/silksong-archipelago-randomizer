using GlobalEnums;
using SilksongRandomizer.Patches;
using System;
using System.Collections.Generic;
using static GameManager;

namespace SilksongRandomizer
{
    internal static class FastTravelUtil
    {
        internal const string BoneBottomHubKey = "bone_bottom";
        internal const string GreymoorHubKey = "greymoor";
        internal const string BellhartHubKey = "bellhart";
        internal const string SongclaveHubKey = "songclave";
        private static readonly string[] MainHubKeys =
        {
            BoneBottomHubKey,
            GreymoorHubKey,
            BellhartHubKey,
            SongclaveHubKey,
        };

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
        private const string FirstAbyssEscapeQuestName =
            "Black Thread Pt3 Escape";

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

            GameManager gameManager = GameManager.SilentInstance;
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
            if (IsActThreeWakeSequenceUnsafe(
                    playerData,
                    gameManager,
                    hero
                ))
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

            if (hero.transform.parent != null &&
                hero.GetComponentInParent<HeroPlatformStick>() != null)
            {
                reason =
                    "Wait for the lift or moving platform to stop before " +
                    "warping.";
                return false;
            }

            if (!hero.CanInput())
            {
                reason =
                    "Finish the current scripted action before warping.";
                return false;
            }

            FullQuestBase firstAbyssEscape = QuestManager.GetQuest(
                FirstAbyssEscapeQuestName
            );
            if (firstAbyssEscape != null &&
                firstAbyssEscape.IsAccepted &&
                !firstAbyssEscape.IsCompleted)
            {
                reason = "Finish escaping the Abyss before warping.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal static string GetPreferredHubName()
        {
            return GetDestinationName(ResolveDestination());
        }

        internal static bool CanCyclePreferredHub()
        {
            if (SaveState.Instance == null ||
                TryResolveStoryDestination(out _))
            {
                return false;
            }

            return GetAvailableMainHubKeys(PlayerData.instance).Count > 1;
        }

        internal static bool TryCyclePreferredHub(int direction)
        {
            SaveState state = SaveState.Instance;
            if (state == null || direction == 0 ||
                TryResolveStoryDestination(out _))
            {
                return false;
            }

            List<string> available = GetAvailableMainHubKeys(
                PlayerData.instance
            );
            if (available.Count < 2)
            {
                return false;
            }

            string current = ResolveMainHubKey(
                state.preferredF4Hub,
                available
            );
            int currentIndex = available.IndexOf(current);
            int step = direction < 0 ? -1 : 1;
            int nextIndex =
                (currentIndex + step + available.Count) % available.Count;
            state.preferredF4Hub = available[nextIndex];

            GameManager gameManager = GameManager.SilentInstance;
            if (gameManager != null)
            {
                gameManager.QueueSaveGame();
            }

            return true;
        }

        internal static string NormalizePreferredHubKey(string hubKey)
        {
            foreach (string candidate in MainHubKeys)
            {
                if (string.Equals(
                        candidate,
                        (hubKey ?? string.Empty).Trim(),
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static string GetDestinationName(WarpDestination destination)
        {
            switch (destination)
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
            if (TryResolveStoryDestination(out WarpDestination destination))
            {
                return destination;
            }

            List<string> available = GetAvailableMainHubKeys(
                PlayerData.instance
            );
            SaveState state = SaveState.Instance;
            string selectedHub = ResolveMainHubKey(
                state == null ? BoneBottomHubKey : state.preferredF4Hub,
                available
            );
            switch (selectedHub)
            {
                case SongclaveHubKey:
                    return WarpDestination.Songclave;
                case BellhartHubKey:
                    return WarpDestination.Bellhart;
                case GreymoorHubKey:
                    return WarpDestination.Greymoor;
                default:
                    return WarpDestination.BoneBottom;
            }
        }

        private static bool TryResolveStoryDestination(
            out WarpDestination destination
        )
        {
            if (WidowSequenceSafety.CanRecoverToWidowShrine())
            {
                destination = WarpDestination.WidowShrine;
                return true;
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData != null &&
                playerData.blackThreadWorld &&
                playerData.act3_wokeUp &&
                playerData.act3_enclaveWakeSceneCompleted)
            {
                destination = WarpDestination.Terminus;
                return true;
            }

            destination = WarpDestination.BoneBottom;
            return false;
        }

        private static List<string> GetAvailableMainHubKeys(
            PlayerData playerData
        )
        {
            List<string> available = new List<string>();
            foreach (string hubKey in MainHubKeys)
            {
                if (IsMainHubAvailable(hubKey, playerData))
                {
                    available.Add(hubKey);
                }
            }
            return available;
        }

        private static bool IsMainHubAvailable(
            string hubKey,
            PlayerData playerData
        )
        {
            switch (hubKey)
            {
                case GreymoorHubKey:
                    return IsGreymoorHubAvailable(playerData);
                case BellhartHubKey:
                    return playerData != null &&
                        playerData.spinnerDefeated;
                case SongclaveHubKey:
                    return IsSongclaveHubAvailable(playerData);
                default:
                    return string.Equals(
                        hubKey,
                        BoneBottomHubKey,
                        StringComparison.Ordinal
                    );
            }
        }

        private static bool IsGreymoorHubAvailable(PlayerData playerData)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                state.rodeFleaCaravanToGreymoor &&
                (playerData == null || !playerData.spinnerDefeated);
        }

        private static bool IsSongclaveHubAvailable(PlayerData playerData)
        {
            return playerData != null &&
                playerData.bellShrineEnclave;
        }

        private static string ResolveMainHubKey(
            string preferredHubKey,
            List<string> available
        )
        {
            string normalized = NormalizePreferredHubKey(preferredHubKey);
            if (string.Equals(
                    normalized,
                    GreymoorHubKey,
                    StringComparison.Ordinal
                ) && available.Contains(BellhartHubKey))
            {
                return BellhartHubKey;
            }

            if (normalized.Length > 0 && available.Contains(normalized))
            {
                return normalized;
            }

            return available.Count == 0
                ? BoneBottomHubKey
                : available[available.Count - 1];
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

        internal static bool IsActThreeWakeSequenceUnsafe()
        {
            return IsActThreeWakeSequenceUnsafe(
                PlayerData.instance,
                GameManager.SilentInstance,
                HeroController.instance
            );
        }

        private static bool IsActThreeWakeSequenceUnsafe(
            PlayerData playerData,
            GameManager gameManager,
            HeroController hero
        )
        {
            return playerData != null &&
                   playerData.blackThreadWorld &&
                   (
                       !playerData.act3_wokeUp ||
                       !playerData.act3_enclaveWakeSceneCompleted ||
                       IsActThreeWakeEntry(gameManager, hero)
                   );
        }
    }
}
