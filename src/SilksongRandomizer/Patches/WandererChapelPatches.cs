using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;
using UnityEngine;

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
            internal readonly string CrestInternalName;

            internal ChapelSource(
                string locationName,
                string rewardSceneName,
                string crestInternalName,
                string doorSceneName = null,
                string closedFlag = null
            )
            {
                LocationName = locationName;
                RewardSceneName = rewardSceneName;
                DoorSceneName = doorSceneName;
                ClosedFlag = closedFlag;
                CrestInternalName = crestInternalName;
            }
        }

        private static string suppressedMemorySource;

        private static readonly ChapelSource[] StandardChapelSources =
        {
            new ChapelSource(
                "Crest: Beast",
                "Ant_19",
                "Warrior",
                "Ant_20",
                BeastClosedFlag
            ),
            new ChapelSource(
                "Crest: Reaper",
                "Greymoor_20c",
                "Reaper",
                "Greymoor_20b",
                ReaperClosedFlag
            ),
            new ChapelSource(
                "Crest: Wanderer",
                "Chapel_Wanderer",
                "Wanderer",
                "Bonegrave",
                WandererClosedFlag
            ),
            new ChapelSource(
                "Crest: Architect",
                "Under_20",
                "Toolmaster",
                "Under_17"
            ),
            new ChapelSource("Crest: Shaman", "Tut_04", "Spell"),
            new ChapelSource("Crest: Shaman", "Tut_05", "Spell"),
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
                            "Bonegrave - Rosary Cache #1",
                            "Bonegrave - Rosary Cache #2",
                            "Bonegrave - Rosary Cache #3",
                            "Bonegrave - Rosary Cache #4",
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

        private static ChapelSource FindSourceByMemoryState(
            string stateName
        )
        {
            string crestInternalName;
            switch (stateName)
            {
                case "Reaper":
                    crestInternalName = "Reaper";
                    break;
                case "Wanderer":
                    crestInternalName = "Wanderer";
                    break;
                case "Beast":
                    crestInternalName = "Warrior";
                    break;
                case "Shaman":
                    crestInternalName = "Spell";
                    break;
                case "Toolmaster":
                    crestInternalName = "Toolmaster";
                    break;
                default:
                    return null;
            }

            foreach (ChapelSource source in StandardChapelSources)
            {
                if (string.Equals(
                        source.CrestInternalName,
                        crestInternalName,
                        StringComparison.Ordinal
                    ))
                {
                    return source;
                }
            }

            return null;
        }

        private static bool MatchesCrest(
            ToolCrest crest,
            ChapelSource source
        )
        {
            return crest != null && source != null &&
                   string.Equals(
                       crest.name,
                       source.CrestInternalName,
                       StringComparison.Ordinal
                   );
        }

        private static bool ShouldSuppressSourceAutoEquip(
            AutoEquipCrestV2 action
        )
        {
            if (action == null || action.Owner == null ||
                action.Fsm == null || action.State == null ||
                !string.Equals(
                    action.Owner.name,
                    "Crest Get Shrine",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    action.Fsm.Name,
                    "Control",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    action.State.Name,
                    "Crest Change",
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            ChapelSource source = FindSourceByRewardScene(
                GameManager.GetBaseSceneName(action.Owner.scene.name)
            );
            return source != null &&
                   IsManagedLocation(source.LocationName) &&
                   MatchesCrest(
                       action.Crest?.Value as ToolCrest,
                       source
                   );
        }

        private static bool ShouldSuppressMemoryAutoEquip(
            AutoEquipCrestV3 action
        )
        {
            if (action == null || action.Owner == null ||
                action.Fsm == null || action.State == null ||
                !string.Equals(
                    GameManager.GetBaseSceneName(
                        action.Owner.scene.name
                    ),
                    "Tut_05",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                !string.Equals(
                    action.Owner.name,
                    "Memory Control",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    action.Fsm.Name,
                    "Memory Control",
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            if (string.Equals(
                    action.State.Name,
                    "End Scene",
                    StringComparison.Ordinal
                ))
            {
                bool suppress = !string.IsNullOrEmpty(
                    suppressedMemorySource
                );
                suppressedMemorySource = null;
                return suppress;
            }

            ChapelSource source = FindSourceByMemoryState(
                action.State.Name
            );
            suppressedMemorySource = null;
            if (source == null ||
                !IsManagedLocation(source.LocationName) ||
                !MatchesCrest(
                    action.Crest?.Value as ToolCrest,
                    source
                ))
            {
                return false;
            }

            suppressedMemorySource = source.LocationName;
            return true;
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
            GameManager gameManager = GameManager.instance;
            if (!string.IsNullOrEmpty(suppressedMemorySource) &&
                gameManager != null &&
                !string.Equals(
                    GameManager.GetBaseSceneName(
                        gameManager.sceneName ?? string.Empty
                    ),
                    "Tut_05",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                suppressedMemorySource = null;
            }

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

        [HarmonyPatch(typeof(AutoEquipCrestV2), nameof(AutoEquipCrestV2.OnEnter))]
        private static class ChapelSourceAutoEquipPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(AutoEquipCrestV2 __instance)
            {
                if (!ShouldSuppressSourceAutoEquip(__instance))
                {
                    return true;
                }

                BindOrbHudFrame.SkipToNextAppear = false;
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(AutoEquipCrestV3), nameof(AutoEquipCrestV3.OnEnter))]
        private static class ChapelMemoryAutoEquipPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(AutoEquipCrestV3 __instance)
            {
                if (!ShouldSuppressMemoryAutoEquip(__instance))
                {
                    return true;
                }

                BindOrbHudFrame.SkipToNextAppear = false;
                __instance.Finish();
                return false;
            }
        }
    }
}
