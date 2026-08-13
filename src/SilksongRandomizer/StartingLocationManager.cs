using GlobalEnums;
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
        private const string BoneBottomBenchMarkerName = "RestBench";
        private const int BenchRespawnType = 1;
        private static bool isApplying;

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

            if (!string.Equals(
                    state.startingLocation,
                    Archipelago.StartingLocationBoneBottom,
                    StringComparison.Ordinal
                ))
            {
                RandomizerPlugin.Log?.LogError(
                    "[RANDOMIZER] Refusing unknown starting location '" +
                    state.startingLocation + "'."
                );
                return;
            }

            isApplying = true;
            RandomizerPlugin.Instance.StartCoroutine(
                ApplyBoneBottomWhenReady(state)
            );
        }

        private static IEnumerator ApplyBoneBottomWhenReady(
            SaveState expectedState
        )
        {
            try
            {
                while (ReferenceEquals(SaveState.Instance, expectedState) &&
                       !expectedState.startingLocationApplied)
                {
                    GameManager gameManager = GameManager.instance;
                    string sceneName = gameManager == null
                        ? string.Empty
                        : gameManager.GetSceneNameString();

                    // A crash after the native scene transition but before
                    // the randomizer metadata save must not warp the player a
                    // second time. Rebind the Bone Bottom bench spawn and
                    // finish the persisted latch instead.
                    if (string.Equals(
                            sceneName,
                            ResolveBoneBottomSceneName(),
                            StringComparison.Ordinal
                        ))
                    {
                        MossMotherWarpSafety.RecoverInterruptedBoneBottomWarp();
                        BindBoneBottomBenchRespawn(sceneName);
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

                    ApplyBoneBottomStart(expectedState);
                    yield break;
                }
            }
            finally
            {
                isApplying = false;
            }
        }

        private static void ApplyBoneBottomStart(SaveState expectedState)
        {
            string sceneName = ResolveBoneBottomSceneName();
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new InvalidOperationException(
                    "Silksong could not resolve the Bone Bottom scene."
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

            BindBoneBottomBenchRespawn(sceneName);
            bool protectedOpeningBoss =
                MossMotherWarpSafety.PrepareForBoneBottomWarp();
            try
            {
                GameManager.instance.BeginSceneTransition(new SceneLoadInfo
                {
                    SceneName = sceneName,
                    EntryGateName = BoneBottomBenchMarkerName
                });
                expectedState.startingLocationApplied = true;
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] Applied Bone Bottom starting location at " +
                    "its native RestBench respawn marker."
                );
            }
            catch
            {
                playerData.respawnScene = previousRespawnScene;
                playerData.respawnMarkerName = previousRespawnMarker;
                playerData.respawnType = previousRespawnType;
                playerData.mapZone = previousMapZone;
                playerData.atBench = previousAtBench;
                if (protectedOpeningBoss)
                {
                    MossMotherWarpSafety.CancelPreparedWarp();
                }
                throw;
            }
        }

        private static void BindBoneBottomBenchRespawn(string sceneName)
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null || string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            playerData.respawnScene = sceneName;
            playerData.respawnMarkerName = BoneBottomBenchMarkerName;
            playerData.respawnType = BenchRespawnType;
            playerData.mapZone = MapZone.BONETOWN;
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
