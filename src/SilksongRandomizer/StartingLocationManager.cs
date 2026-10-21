using GlobalEnums;
using HarmonyLib;
using SilksongRandomizer.Patches;
using System;
using System.Collections;
using static GameManager;

namespace SilksongRandomizer
{
    /// <summary>
    /// Applies a room-bound starting location once after Silksong's
    /// native new-game initialization has produced a controllable hero.
    /// </summary>
    internal static class StartingLocationManager
    {
        private const string TutorialSceneName = "Tut_01";
        private const string BenchMarkerName = "RestBench";
        private const int BenchRespawnType = 1;
        private const string SlabEntranceScene = "Slab_02";
        private const string SlabEntranceSwitch = "slab_jail_lever";
        private static bool isApplying;

        internal static bool TryGetAlternateStartRespawn(
            out string scene, out string marker, out int type, out MapZone zone)
        {
            scene = marker = null;
            type = BenchRespawnType;
            zone = MapZone.BONETOWN;
            SaveState state = SaveState.Instance;
            if (state?.IsRoomBound != true ||
                !TryGetStart(state.startingLocation, out scene, out marker, out type, out zone))
                return false;

            return !string.IsNullOrEmpty(scene);
        }

        internal static bool TryGetStart(string key, out string scene, out string marker,
            out int type, out MapZone zone)
        {
            scene = null;
            marker = BenchMarkerName;
            type = BenchRespawnType;
            zone = MapZone.NONE;
            switch (key)
            {
                case Archipelago.StartingLocationBoneBottom:
                    scene = ResolveBoneBottomSceneName();
                    zone = MapZone.BONETOWN;
                    break;
                case Archipelago.StartingLocationBellhart:
                    scene = "Belltown";
                    zone = MapZone.BELLTOWN;
                    break;
                case Archipelago.StartingLocationSongclave:
                    scene = "Bellshrine_Enclave";
                    zone = MapZone.CITY_OF_SONG;
                    break;
                case Archipelago.StartingLocationSlab:
                    scene = "Slab_03";
                    marker = "Cage Broken Respawn Marker";
                    type = 0;
                    zone = MapZone.THE_SLAB;
                    break;
                case Archipelago.StartingLocationWormways:
                    scene = "Crawl_08";
                    zone = MapZone.CRAWLSPACE;
                    break;
                case Archipelago.StartingLocationPilgrimsRest:
                    scene = "Bone_East_10_Room";
                    zone = MapZone.PILGRIMS_REST;
                    break;
                case Archipelago.StartingLocationDeepDocks:
                    scene = "Dock_01";
                    zone = MapZone.DOCKS;
                    break;
                case Archipelago.StartingLocationShellwood:
                    scene = "Bellshrine_03";
                    zone = MapZone.SHELLWOOD_THICKET;
                    break;
                case Archipelago.StartingLocationBlastedSteps:
                    scene = "Bellway_08";
                    zone = MapZone.JUDGE_STEPS;
                    break;
                default:
                    return false;
            }
            return true;
        }

        internal static bool RequiresBoneBottomVisit(string start) =>
            start != Archipelago.StartingLocationBoneBottom &&
            TryGetStart(start, out _, out _, out _, out _);

        internal static string GetStartName(string start)
        {
            switch (start)
            {
                case Archipelago.StartingLocationWormways: return "Wormways Laboratory";
                case Archipelago.StartingLocationPilgrimsRest: return "Pilgrim's Rest";
                case Archipelago.StartingLocationDeepDocks: return "Deep Docks";
                case Archipelago.StartingLocationShellwood: return "Shellwood Bellshrine";
                case Archipelago.StartingLocationBlastedSteps: return "Blasted Steps Bellway";
                default: return start;
            }
        }

        [HarmonyPatch(typeof(BellBench), "Awake")]
        private static class StartingBenchPatch
        {
            private static void Prefix(BellBench __instance, ref bool ___startUnlocked)
            {
                SaveState state = SaveState.Instance;
                if (state?.IsRoomBound != true) return;
                string scene = __instance.gameObject.scene.name;
                string name = __instance.gameObject.name;
                if ((state.startingLocation == Archipelago.StartingLocationDeepDocks &&
                     scene == "Dock_01" && name == "bell_bench (1)") ||
                    (state.startingLocation == Archipelago.StartingLocationBlastedSteps &&
                     scene == "Bellway_08" && name == "bell_bench"))
                    ___startUnlocked = true;
            }
        }

        internal static void ScheduleIfNeeded()
        {
            SaveState state = SaveState.Instance;
            if (state == null || state.startingLocationApplied || isApplying)
            {
                return;
            }

            if (string.Equals(
                    state.startingLocation,
                    Archipelago.StartingLocationVanilla,
                    StringComparison.Ordinal
                ))
            {
                state.startingLocationApplied = true;
                return;
            }

            if (!TryGetStart(state.startingLocation, out _, out _, out _, out _))
            {
                RandomizerPlugin.Log?.LogError(
                    "[RANDOMIZER] Refusing unknown starting location '" +
                    state.startingLocation + "'."
                );
                return;
            }

            isApplying = true;
            RandomizerPlugin.Instance.StartCoroutine(
                ApplyStartWhenReady(state)
            );
        }

        private static IEnumerator ApplyStartWhenReady(
            SaveState expectedState
        )
        {
            try
            {
                if (!TryGetStart(expectedState.startingLocation, out string targetScene,
                        out string marker, out int type, out MapZone zone))
                    yield break;
                while (ReferenceEquals(SaveState.Instance, expectedState) &&
                       !expectedState.startingLocationApplied)
                {
                    GameManager gameManager = GameManager.SilentInstance;
                    string sceneName = gameManager == null
                        ? string.Empty
                        : gameManager.GetSceneNameString();

                    // A crash after the native scene transition but before
                    // the randomizer metadata save must not warp the player a
                    // second time. Rebind the starting checkpoint and
                    // finish the persisted latch instead.
                    if (string.Equals(
                            sceneName,
                            targetScene,
                            StringComparison.Ordinal
                        ))
                    {
                        if (expectedState.startingLocation == Archipelago.StartingLocationBoneBottom)
                            MossMotherWarpSafety.RecoverInterruptedBoneBottomWarp();
                        if (expectedState.startingLocation == Archipelago.StartingLocationSlab)
                            OpenSlabEntrance();
                        BindStartRespawn(sceneName, marker, type, zone);
                        SetInitialHub(expectedState);
                        expectedState.startingLocationApplied = true;
                        yield break;
                    }

                    if (!string.Equals(
                            sceneName,
                            TutorialSceneName,
                            StringComparison.Ordinal
                        ) ||
                        !FastTravelUtil.CanTeleportToPreferredHub(out _))
                    {
                        yield return null;
                        continue;
                    }

                    ApplyStart(expectedState, targetScene, marker, type, zone);
                    yield break;
                }
            }
            finally
            {
                isApplying = false;
            }
        }

        private static void ApplyStart(SaveState expectedState, string sceneName,
            string marker, int type, MapZone zone)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new InvalidOperationException(
                    "Silksong could not resolve the starting scene."
                );
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                throw new InvalidOperationException(
                    "Silksong player data was unavailable for the start warp."
                );
            }

            string previousRespawnScene = playerData.respawnScene;
            string previousRespawnMarker = playerData.respawnMarkerName;
            int previousRespawnType = playerData.respawnType;
            MapZone previousMapZone = playerData.mapZone;
            bool previousAtBench = playerData.atBench;
            bool wasRespawning = GameManager.instance.RespawningHero;
            bool slabStart = expectedState.startingLocation == Archipelago.StartingLocationSlab;
            PersistentItemData<bool> previousSlabGate = null;
            if (slabStart)
                SceneData.instance.PersistentBools.TryGetValue(
                    SlabEntranceScene, SlabEntranceSwitch, out previousSlabGate);

            BindStartRespawn(sceneName, marker, type, zone);
            bool protectedOpeningBoss =
                expectedState.startingLocation == Archipelago.StartingLocationBoneBottom &&
                MossMotherWarpSafety.PrepareForBoneBottomWarp();
            try
            {
                if (slabStart)
                    OpenSlabEntrance();
                if (RequiresBoneBottomVisit(expectedState.startingLocation))
                    GameManager.instance.RespawningHero = true;
                GameManager.instance.BeginSceneTransition(new SceneLoadInfo
                {
                    SceneName = sceneName,
                    EntryGateName = marker
                });
                SetInitialHub(expectedState);
                expectedState.startingLocationApplied = true;
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] Applied " + expectedState.startingLocation +
                    " start at its native respawn marker."
                );
            }
            catch
            {
                playerData.respawnScene = previousRespawnScene;
                playerData.respawnMarkerName = previousRespawnMarker;
                playerData.respawnType = previousRespawnType;
                playerData.mapZone = previousMapZone;
                playerData.atBench = previousAtBench;
                GameManager.instance.RespawningHero = wasRespawning;
                if (slabStart)
                {
                    if (previousSlabGate == null)
                        SceneData.instance.PersistentBools.Remove(SlabEntranceScene, SlabEntranceSwitch);
                    else
                        SceneData.instance.PersistentBools.SetValue(previousSlabGate);
                }
                if (protectedOpeningBoss)
                {
                    MossMotherWarpSafety.CancelPreparedWarp();
                }
                throw;
            }
        }

        private static void SetInitialHub(SaveState state)
        {
            if (RequiresBoneBottomVisit(state.startingLocation) &&
                string.IsNullOrEmpty(state.preferredF4Hub))
                state.preferredF4Hub = state.startingLocation;
        }

        private static void OpenSlabEntrance()
        {
            SceneData.instance.PersistentBools.SetValue(new PersistentItemData<bool>
            {
                SceneName = SlabEntranceScene,
                ID = SlabEntranceSwitch,
                Value = true
            });
        }

        private static void BindStartRespawn(string sceneName, string marker, int type, MapZone zone)
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null || string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            playerData.respawnScene = sceneName;
            playerData.respawnMarkerName = marker;
            playerData.respawnType = type;
            playerData.mapZone = zone;
            playerData.atBench = false;
        }

        private static string ResolveBoneBottomSceneName()
        {
            return FastTravelScenes.GetSceneName(
                FastTravelLocations.Bonetown
            );
        }
    }
}
