using GlobalEnums;
using SilksongRandomizer.Patches;
using System;
using System.Collections.Generic;
using static GameManager;

namespace SilksongRandomizer
{
    internal static class FastTravelUtil
    {
        internal const string EntranceHubKey = "room_entrance";
        internal const string BoneBottomHubKey = "bone_bottom";
        internal const string GreymoorHubKey = "greymoor";
        internal const string BellhartHubKey = "bellhart";
        internal const string SongclaveHubKey = "songclave";
        internal const string SlabReturnHubKey = "slab_return";
        internal const string UnderworksReturnHubKey = "underworks_return";
        internal const string TerminusHubKey = "terminus";
        private static readonly string[] MainHubKeys =
        {
            EntranceHubKey,
            BoneBottomHubKey,
            GreymoorHubKey,
            BellhartHubKey,
            SongclaveHubKey,
            SlabReturnHubKey,
            UnderworksReturnHubKey,
            TerminusHubKey,
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
        private const string SlabReturnSceneName = "Slab_03";
        private const string SlabReturnEntryGateName = "left2";
        private const string UnderworksReturnSceneName = "Under_01b";
        private const string UnderworksReturnEntryGateName = "left1";

        private enum WarpDestination
        {
            RoomEntrance,
            BoneBottom,
            Bellhart,
            Songclave,
            Terminus,
            Greymoor,
            WidowShrine,
            SlabReturn,
            UnderworksReturn,
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

            string sceneName = gameManager.GetSceneNameString();
            if (playerData != null && playerData.gainedCurse &&
                (string.Equals(sceneName, "Shellwood_25b", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sceneName, "Shellwood_25", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sceneName, "Mosstown_03", StringComparison.OrdinalIgnoreCase)))
            {
                reason = "Leave the Chapel of the Witch on foot before warping.";
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
                case WarpDestination.RoomEntrance:
                    return "Room Entrance";
                case WarpDestination.SlabReturn:
                    return "Slab Return";
                case WarpDestination.UnderworksReturn:
                    return "Underworks Return";
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
                case WarpDestination.RoomEntrance:
                    if (!EntranceRandomization.TryGetReturnPoint(out sceneName, out entryGateName))
                    {
                        error = "No entrance return point is available in this room.";
                        return false;
                    }
                    break;
                case WarpDestination.UnderworksReturn:
                    sceneName = UnderworksReturnSceneName;
                    entryGateName = UnderworksReturnEntryGateName;
                    break;
                case WarpDestination.SlabReturn:
                    sceneName = SlabReturnSceneName;
                    entryGateName = SlabReturnEntryGateName;
                    break;
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
            PlayerData playerData = PlayerData.instance;
            bool wasTravelling = playerData != null && playerData.travelling;
            try
            {
                DeliveryQuestItem.BreakAll();
                if (playerData != null)
                {
                    playerData.travelling = false;
                }
                GameManager.instance.BeginSceneTransition(new SceneLoadInfo
                {
                    SceneName = sceneName,
                    EntryGateName = entryGateName
                });
            }
            catch
            {
                if (playerData != null)
                {
                    playerData.travelling = wasTravelling;
                }
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
                case EntranceHubKey:
                    return WarpDestination.RoomEntrance;
                case SlabReturnHubKey:
                    return WarpDestination.SlabReturn;
                case UnderworksReturnHubKey:
                    return WarpDestination.UnderworksReturn;
                case TerminusHubKey:
                    return WarpDestination.Terminus;
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
                playerData.act3_enclaveWakeSceneCompleted &&
                !IsSlabReturnAvailable() &&
                !IsUnderworksReturnAvailable(playerData) &&
                !EntranceRandomization.TryGetReturnPoint(out _, out _))
            {
                destination = WarpDestination.Terminus;
                return true;
            }

            destination = WarpDestination.BoneBottom;
            return false;
        }

        internal static IEnumerable<string> GetTrackerHubKeys()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null || TryResolveStoryDestination(out _)) yield break;
            foreach (string key in GetAvailableMainHubKeys(playerData))
                if (key == BoneBottomHubKey || key == BellhartHubKey || key == SongclaveHubKey)
                    yield return key;
        }

        private static List<string> GetAvailableMainHubKeys(
            PlayerData playerData
        )
        {
            List<string> available = new List<string>();
            if (playerData != null && playerData.blackThreadWorld &&
                playerData.act3_wokeUp && playerData.act3_enclaveWakeSceneCompleted)
            {
                if (EntranceRandomization.TryGetReturnPoint(out _, out _))
                    available.Add(EntranceHubKey);
                available.Add(TerminusHubKey);
                if (IsSlabReturnAvailable())
                {
                    available.Add(SlabReturnHubKey);
                }
                if (IsUnderworksReturnAvailable(playerData))
                {
                    available.Add(UnderworksReturnHubKey);
                }
                return available;
            }
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
                case EntranceHubKey:
                    return EntranceRandomization.TryGetReturnPoint(out _, out _);
                case SlabReturnHubKey:
                    return IsSlabReturnAvailable();
                case UnderworksReturnHubKey:
                    return IsUnderworksReturnAvailable(playerData);
                case GreymoorHubKey:
                    return IsGreymoorHubAvailable(playerData);
                case BellhartHubKey:
                    return playerData != null &&
                        ProgressionShufflePatches.StoryCredit("Boss: Widow", playerData.spinnerDefeated);
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

        private static bool IsUnderworksReturnAvailable(PlayerData playerData)
        {
            return playerData != null && playerData.understoreLiftBroke &&
                !MapLogicEvaluator.CanReachUnderworksWithoutCrash(SaveState.Instance);
        }

        private static bool IsSlabReturnAvailable()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null || !state.slabCaptureReturnUnlocked)
            {
                return false;
            }

            bool randomizedSkills = state.IsRandomized(ItemType.Skill);
            bool clingGrip = randomizedSkills
                ? state.canWallJump : playerData != null && playerData.hasWalljump;
            bool faydown = randomizedSkills
                ? state.canDoubleJump : playerData != null && playerData.hasDoubleJump;
            if (playerData == null || !playerData.slab_cloak_battle_completed ||
                !clingGrip || !faydown ||
                !(playerData.HasSlabKeyC || playerData.slab_05_gateOpen))
            {
                return true;
            }

            bool bellwayUnlocked = state.IsRandomized(ItemType.Bellway)
                ? state.UnlockedPeakStation
                : playerData.UnlockedPeakStation;
            bool bellwayUsable = playerData.UnlockedFastTravel && bellwayUnlocked;
            bool choralRouteOpen = state.slabChoralApproachVisited &&
                SceneData.instance != null &&
                SceneData.instance.PersistentBools.GetValueOrDefault(
                    "Slab_02", "slab_jail_lever");
            return !bellwayUsable && !choralRouteOpen;
        }

        private static bool IsGreymoorHubAvailable(PlayerData playerData)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                state.rodeFleaCaravanToGreymoor &&
                (playerData == null || !ProgressionShufflePatches.StoryCredit("Boss: Widow", playerData.spinnerDefeated));
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

            if (available.Contains(EntranceHubKey))
                return EntranceHubKey;
            if (available.Contains(TerminusHubKey))
            {
                return TerminusHubKey;
            }
            for (int index = available.Count - 1; index >= 0; index--)
            {
                if (available[index] != SlabReturnHubKey &&
                    available[index] != UnderworksReturnHubKey)
                {
                    return available[index];
                }
            }
            return BoneBottomHubKey;
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

    internal sealed class EntranceReturnPoint
    {
        private object owner;
        private string scene;
        private string gate;
        private int sceneHandle;

        internal void Clear()
        {
            owner = null;
            scene = gate = null;
        }

        internal void Record(object state, string sceneName, string gateName, int handle)
        {
            Clear();
            if (state == null || string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(gateName)) return;
            owner = state;
            scene = sceneName;
            gate = gateName;
            sceneHandle = handle;
        }

        internal bool TryGet(object state, string sceneName, int handle, out string gateName)
        {
            gateName = null;
            if (owner == null || !ReferenceEquals(owner, state) || scene != sceneName || sceneHandle != handle) return false;
            gateName = gate;
            return true;
        }
    }
}
