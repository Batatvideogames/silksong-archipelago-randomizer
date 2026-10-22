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
        internal const string HalfwayHomeHubKey = "halfway_home";
        internal const string SlabHubKey = "slab";
        internal const string UnderworksReturnHubKey = "underworks_return";
        internal const string TerminusHubKey = "terminus";
        private static readonly string[] MainHubKeys =
        {
            EntranceHubKey,
            BoneBottomHubKey,
            GreymoorHubKey,
            BellhartHubKey,
            SongclaveHubKey,
            HalfwayHomeHubKey,
            SlabHubKey,
            Archipelago.StartingLocationWormways,
            Archipelago.StartingLocationPilgrimsRest,
            Archipelago.StartingLocationDeepDocks,
            Archipelago.StartingLocationShellwood,
            Archipelago.StartingLocationBlastedSteps,
            UnderworksReturnHubKey,
            TerminusHubKey,
        };

        private const string BellwayEntryGateName =
            "door_fastTravelExit";
        private const string BellhartSceneName = "Belltown";
        private const string BellhartEntryGateName = "door1";
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
        private const string UnderworksReturnSceneName = "Under_01b";
        private const string UnderworksReturnEntryGateName = "left1";

        private enum WarpDestination
        {
            RoomEntrance,
            BoneBottom,
            Bellhart,
            Songclave,
            HalfwayHome,
            Slab,
            StartingCheckpoint,
            Terminus,
            Greymoor,
            WidowShrine,
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

            if (!hero.CanInput() && !CanWarpDuringMovement(hero, gameManager, playerData))
            {
                reason =
                    "Finish the current scripted action before warping.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool CanWarpDuringMovement(
            HeroController hero,
            GameManager gameManager,
            PlayerData playerData
        )
        {
            if (hero.IsPaused() ||
                hero.IsInputBlocked() || hero.cState.dead ||
                hero.cState.hazardDeath || hero.cState.hazardRespawning ||
                hero.cState.transitioning || hero.cState.isInCutsceneMovement ||
                gameManager.RespawningHero ||
                hero.transitionState != HeroTransitionState.WAITING_TO_TRANSITION ||
                playerData == null || playerData.disablePause ||
                playerData.disableInventory ||
                InteractManager.BlockingInteractable != null ||
                GenericMessageCanvas.IsActive)
            {
                return false;
            }

            if (hero.umbrellaFSM?.FsmVariables?.FindFsmBool("Is Active")?.Value == true &&
                hero.umbrellaFSM.FsmVariables.FindFsmBool("Has Control")?.Value == true) return true;
            if (!hero.cState.swimming) return false;
            HeroWaterController water = hero.GetComponent<HeroWaterController>();
            return water != null && water.isActiveAndEnabled && water.IsInWater &&
                   water.CurrentState != HeroWaterController.States.Inactive &&
                   water.CurrentState != HeroWaterController.States.Entered;
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
                case WarpDestination.UnderworksReturn:
                    return "Underworks Return";
                case WarpDestination.WidowShrine:
                    return "Widow Shrine";
                case WarpDestination.Songclave:
                    return "Songclave";
                case WarpDestination.HalfwayHome:
                    return "Halfway Home";
                case WarpDestination.Slab:
                    return "The Slab";
                case WarpDestination.StartingCheckpoint:
                    return StartingLocationManager.GetStartName(SaveState.Instance?.startingLocation);
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
            string startingLocation = SaveState.Instance?.startingLocation;
            bool useStartingCheckpoint = false;
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
                case WarpDestination.WidowShrine:
                    sceneName = WidowSequenceSafety.WidowShrineSceneName;
                    entryGateName = WidowSequenceSafety.WidowWakeGateName;
                    break;
                case WarpDestination.Songclave:
                    sceneName = SongclaveBellSceneName;
                    entryGateName = SongclaveBellEntryGateName;
                    useStartingCheckpoint = startingLocation == SongclaveHubKey;
                    break;
                case WarpDestination.HalfwayHome:
                case WarpDestination.Slab:
                case WarpDestination.StartingCheckpoint:
                    sceneName = entryGateName = null;
                    useStartingCheckpoint = true;
                    break;
                case WarpDestination.Terminus:
                    sceneName = TerminusSceneName;
                    entryGateName = TerminusEntryGateName;
                    break;
                case WarpDestination.Bellhart:
                    sceneName = BellhartSceneName;
                    entryGateName = BellhartEntryGateName;
                    useStartingCheckpoint = startingLocation == BellhartHubKey;
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

            if (useStartingCheckpoint)
                StartingLocationManager.TryGetStart(startingLocation, out sceneName,
                    out entryGateName, out _, out _);

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
            bool wasRespawning = GameManager.instance.RespawningHero;
            try
            {
                DeliveryQuestItem.BreakAll();
                if (playerData != null)
                {
                    playerData.travelling = false;
                }
                if (useStartingCheckpoint)
                    GameManager.instance.RespawningHero = true;
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
                GameManager.instance.RespawningHero = wasRespawning;
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
                case UnderworksReturnHubKey:
                    return WarpDestination.UnderworksReturn;
                case TerminusHubKey:
                    return WarpDestination.Terminus;
                case SongclaveHubKey:
                    return WarpDestination.Songclave;
                case HalfwayHomeHubKey:
                    return WarpDestination.HalfwayHome;
                case SlabHubKey:
                    return WarpDestination.Slab;
                case Archipelago.StartingLocationWormways:
                case Archipelago.StartingLocationPilgrimsRest:
                case Archipelago.StartingLocationDeepDocks:
                case Archipelago.StartingLocationShellwood:
                case Archipelago.StartingLocationBlastedSteps:
                    return WarpDestination.StartingCheckpoint;
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

            destination = WarpDestination.BoneBottom;
            return false;
        }

        internal static IEnumerable<string> GetTrackerHubKeys()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null || WidowSequenceSafety.CanRecoverToWidowShrine() ||
                IsActThreeOpeningInProgress(playerData))
                yield break;
            foreach (string key in new[] { BoneBottomHubKey, BellhartHubKey, SongclaveHubKey, HalfwayHomeHubKey, SlabHubKey,
                Archipelago.StartingLocationWormways, Archipelago.StartingLocationPilgrimsRest,
                Archipelago.StartingLocationDeepDocks, Archipelago.StartingLocationShellwood,
                Archipelago.StartingLocationBlastedSteps })
                if (IsMainHubAvailable(key, playerData)) yield return key;
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
            string start = SaveState.Instance?.startingLocation;
            if (StartingLocationManager.RequiresBoneBottomVisit(start))
            {
                if (hubKey == start) return true;
                if (hubKey == BoneBottomHubKey)
                    return playerData?.scenesVisited?.Contains("Bonetown") == true;
            }
            switch (hubKey)
            {
                case HalfwayHomeHubKey:
                case SlabHubKey:
                    return false;
                case EntranceHubKey:
                    return EntranceRandomization.TryGetReturnPoint(out _, out _);
                case UnderworksReturnHubKey:
                    return IsUnderworksReturnAvailable(playerData);
                case TerminusHubKey:
                    return playerData != null && playerData.blackThreadWorld &&
                        !IsActThreeOpeningInProgress(playerData);
                case GreymoorHubKey:
                    return IsGreymoorHubAvailable(playerData);
                case BellhartHubKey:
                    return IsBellhartHubAvailable(playerData);
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
            SaveState state = SaveState.Instance;
            if (state?.IsRoomBound == true && state.entranceLayoutJson != "{}" &&
                playerData?.scenesVisited?.Contains(UnderworksReturnSceneName) != true)
                return false;
            return playerData != null && playerData.understoreLiftBroke &&
                !MapLogicEvaluator.CanReachUnderworksWithoutCrash(state);
        }

        private static bool IsBellhartHubAvailable(PlayerData playerData)
        {
            return playerData != null &&
                (playerData.visitedBellhartHaunted || playerData.visitedBellhartSaved);
        }

        private static bool IsGreymoorHubAvailable(PlayerData playerData)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                state.rodeFleaCaravanToGreymoor &&
                !IsBellhartHubAvailable(playerData);
        }

        private static bool IsSongclaveHubAvailable(PlayerData playerData)
        {
            return playerData != null &&
                (playerData.bellShrineEnclave || SaveState.Instance?.rangSongclaveBell == true);
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
                if (available[index] != UnderworksReturnHubKey)
                {
                    return available[index];
                }
            }
            string start = SaveState.Instance?.startingLocation;
            return StartingLocationManager.RequiresBoneBottomVisit(start) ? start : BoneBottomHubKey;
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
            return IsActThreeOpeningInProgress(playerData) ||
                   (playerData != null && playerData.blackThreadWorld &&
                    IsActThreeWakeEntry(gameManager, hero));
        }

        private static bool IsActThreeOpeningInProgress(PlayerData playerData)
        {
            return playerData != null && playerData.blackThreadWorld &&
                (!playerData.act3_wokeUp || !playerData.act3_enclaveWakeSceneCompleted);
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
