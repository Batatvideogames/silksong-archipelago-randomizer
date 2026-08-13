using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;

namespace SilksongRandomizer.Patches
{
    internal static class WandererChapelPatches
    {
        private const string WandererClosedFlag =
            "chapelClosed_wanderer";
        private const string ReaperClosedFlag = "chapelClosed_reaper";
        private const string BeastClosedFlag = "chapelClosed_beast";

        private sealed class ChapelSource
        {
            internal readonly string LocationName;
            internal readonly string RewardSceneName;
            internal readonly string DoorSceneName;
            internal readonly string ClosedFlag;

            internal ChapelSource(
                string locationName,
                string rewardSceneName,
                string doorSceneName = null,
                string closedFlag = null
            )
            {
                LocationName = locationName;
                RewardSceneName = rewardSceneName;
                DoorSceneName = doorSceneName;
                ClosedFlag = closedFlag;
            }
        }

        private static readonly ChapelSource[] StandardChapelSources =
        {
            new ChapelSource(
                "Crest: Beast",
                "Ant_19",
                "Ant_20",
                BeastClosedFlag
            ),
            new ChapelSource(
                "Crest: Reaper",
                "Greymoor_20c",
                "Greymoor_20b",
                ReaperClosedFlag
            ),
            new ChapelSource(
                "Crest: Wanderer",
                "Chapel_Wanderer",
                "Bonegrave",
                WandererClosedFlag
            ),
            new ChapelSource(
                "Crest: Architect",
                "Under_20",
                "Under_17"
            ),
            new ChapelSource("Crest: Shaman", "Tut_04"),
            new ChapelSource("Crest: Shaman", "Tut_05"),
        };

        private static readonly Dictionary<string, string[]>
            ProtectedLocationsByClosedFlag =
                new Dictionary<string, string[]>(
                    StringComparer.OrdinalIgnoreCase
                )
                {
                    {
                        WandererClosedFlag,
                        new[]
                        {
                            "Crest: Wanderer",
                            "Rosary Cache: Bonegrave #1",
                            "Rosary Cache: Bonegrave #2",
                            "Rosary Cache: Bonegrave #3",
                            "Rosary Cache: Bonegrave #4",
                        }
                    },
                    {
                        ReaperClosedFlag,
                        new[] { "Crest: Reaper" }
                    },
                    {
                        BeastClosedFlag,
                        new[] { "Crest: Beast" }
                    },
                };

        private static bool IsManagedLocation(string locationName)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsLocationEnabled(locationName) &&
                   state.IsLocationInSeed(locationName);
        }

        private static bool IsPendingLocation(string locationName)
        {
            SaveState state = SaveState.Instance;
            return IsManagedLocation(locationName) &&
                   !state.IsLocationChecked(locationName);
        }

        private static bool TryGetLocationCompletion(
            string locationName,
            out bool isChecked
        )
        {
            isChecked = false;
            if (!IsManagedLocation(locationName))
            {
                return false;
            }

            isChecked = SaveState.Instance.IsLocationChecked(locationName);
            return true;
        }

        private static bool ShouldKeepChapelOpen(string closedFlag)
        {
            if (string.IsNullOrWhiteSpace(closedFlag) ||
                !ProtectedLocationsByClosedFlag.TryGetValue(
                    closedFlag,
                    out string[] protectedLocationNames
                ))
            {
                return false;
            }

            foreach (string locationName in protectedLocationNames)
            {
                if (IsPendingLocation(locationName))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetSceneName(GetIsCrestUnlocked action)
        {
            if (action == null || action.Owner == null)
            {
                return null;
            }

            return GameManager.GetBaseSceneName(action.Owner.scene.name);
        }

        private static bool MatchesContext(
            GetIsCrestUnlocked action,
            string ownerName,
            string fsmName,
            params string[] stateNames
        )
        {
            if (action == null ||
                action.Owner == null ||
                action.Fsm == null ||
                action.State == null ||
                !string.Equals(
                    action.Owner.name,
                    ownerName,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    action.Fsm.Name,
                    fsmName,
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            foreach (string stateName in stateNames)
            {
                if (string.Equals(
                        action.State.Name,
                        stateName,
                        StringComparison.Ordinal
                    ))
                {
                    return true;
                }
            }

            return false;
        }

        private static ChapelSource FindSourceByRewardScene(
            string sceneName
        )
        {
            foreach (ChapelSource source in StandardChapelSources)
            {
                if (string.Equals(
                        source.RewardSceneName,
                        sceneName,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    return source;
                }
            }

            return null;
        }

        private static ChapelSource FindSourceByDoorScene(
            string sceneName
        )
        {
            foreach (ChapelSource source in StandardChapelSources)
            {
                if (!string.IsNullOrWhiteSpace(source.DoorSceneName) &&
                    string.Equals(
                        source.DoorSceneName,
                        sceneName,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    return source;
                }
            }

            return null;
        }

        private static bool TryGetSourceCompletion(
            GetIsCrestUnlocked action,
            out bool sourceCompleted
        )
        {
            sourceCompleted = false;
            string sceneName = GetSceneName(action);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            if (MatchesContext(
                    action,
                    "Crest Get Shrine",
                    "Control",
                    "Check Unlocked"
                ))
            {
                ChapelSource rewardSource =
                    FindSourceByRewardScene(sceneName);
                return rewardSource != null &&
                       TryGetLocationCompletion(
                           rewardSource.LocationName,
                           out sourceCompleted
                       );
            }

            if (MatchesContext(
                    action,
                    "Chapel Door Control",
                    "chapel_door_control",
                    "State Check"
                ))
            {
                ChapelSource doorSource = FindSourceByDoorScene(sceneName);
                if (doorSource == null)
                {
                    return false;
                }

                if (ShouldKeepChapelOpen(doorSource.ClosedFlag))
                {
                    sourceCompleted = false;
                    return true;
                }

                return TryGetLocationCompletion(
                    doorSource.LocationName,
                    out sourceCompleted
                );
            }

            if (string.Equals(
                    sceneName,
                    "Under_17",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                MatchesContext(
                    action,
                    "Architect Shrine Door",
                    "FSM",
                    "Got Crest?",
                    "Got Crest? 2"
                ))
            {
                return TryGetLocationCompletion(
                    "Crest: Architect",
                    out sourceCompleted
                );
            }

            if (string.Equals(
                    sceneName,
                    "Under_17",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                MatchesContext(
                    action,
                    "Architect NPC",
                    "Behaviour",
                    "Do Toolmaster Convo?",
                    "Will Leave?"
                ))
            {
                return TryGetLocationCompletion(
                    "Crest: Architect",
                    out sourceCompleted
                );
            }

            if (string.Equals(
                    sceneName,
                    "Tut_04",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                MatchesContext(
                    action,
                    "Snail Shamans Set",
                    "Dialogue",
                    "State",
                    "Talk?",
                    "Can Complete?"
                ))
            {
                return TryGetLocationCompletion(
                    "Crest: Shaman",
                    out sourceCompleted
                );
            }

            return false;
        }

        internal static void Update()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return;
            }

            if (playerData.chapelClosed_wanderer &&
                ShouldKeepChapelOpen(WandererClosedFlag))
            {
                playerData.chapelClosed_wanderer = false;
            }

            if (playerData.chapelClosed_reaper &&
                ShouldKeepChapelOpen(ReaperClosedFlag))
            {
                playerData.chapelClosed_reaper = false;
            }

            if (playerData.chapelClosed_beast &&
                ShouldKeepChapelOpen(BeastClosedFlag))
            {
                playerData.chapelClosed_beast = false;
            }

        }

        [HarmonyPatch(
            typeof(PlayerData),
            nameof(PlayerData.SetBool),
            new Type[] { typeof(string), typeof(bool) })]
        private static class PlayerData_SetBool_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(string __0, ref bool __1)
            {
                if (__1 && ShouldKeepChapelOpen(__0))
                {
                    __1 = false;
                }
            }
        }

        [HarmonyPatch(
            typeof(GetIsCrestUnlocked),
            "get_IsTrue",
            new Type[0])]
        private static class GetIsCrestUnlocked_IsTrue_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(
                GetIsCrestUnlocked __instance,
                ref bool __result
            )
            {
                if (TryGetSourceCompletion(
                        __instance,
                        out bool sourceCompleted
                    ))
                {
                    // These exact chapel FSMs use crest ownership as though
                    // it meant their physical source was already collected.
                    // Project the shuffled check's completion here instead:
                    // pending stays available, checked stays collected.
                    __result = sourceCompleted;
                }
            }
        }
    }
}
