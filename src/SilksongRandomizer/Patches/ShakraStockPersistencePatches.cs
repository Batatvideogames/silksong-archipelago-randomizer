using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class ShakraStockPersistencePatches
    {
        private const int ExpectedShakraMapSourceCount = 14;

        private static readonly NativeShopLocation[] ShakraMapSources =
            GetShakraMapSources();

        private static NativeShopLocation[] GetShakraMapSources()
        {
            List<NativeShopLocation> sources =
                new List<NativeShopLocation>();
            foreach (NativeShopLocation source in
                     CoreLocationManifest.ShopLocations)
            {
                if (source != null && source.Type == ItemType.Map)
                {
                    sources.Add(source);
                }
            }

            return sources.ToArray();
        }

        private static bool TryGetActiveShakraMapSources(
            SaveState state,
            PlayerData playerData,
            out List<NativeShopLocation> sourcesInSeed)
        {
            sourcesInSeed = new List<NativeShopLocation>();
            if (state == null ||
                !state.IsRoomBound ||
                !state.TrailsEndUsesOwnedMaps ||
                !state.IsRandomized(ItemType.Map) ||
                state.checkedLocations == null ||
                playerData == null ||
                !playerData.ShakraFinalQuestAppear ||
                ShakraMapSources.Length != ExpectedShakraMapSourceCount)
            {
                return false;
            }

            HashSet<string> locations =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (NativeShopLocation source in ShakraMapSources)
            {
                string locationName = source == null
                    ? null
                    : LocationSet.GetCanonicalLocationName(
                        source.LocationName
                    );
                if (string.IsNullOrWhiteSpace(locationName) ||
                    !locations.Add(locationName))
                {
                    return false;
                }

                if (state.IsLocationEnabled(locationName) &&
                    state.IsLocationInSeed(locationName))
                {
                    sourcesInSeed.Add(source);
                }
            }

            return locations.Count == ExpectedShakraMapSourceCount &&
                   sourcesInSeed.Count > 0;
        }

        private static bool HasUncheckedShakraMapSource(
            SaveState state,
            IEnumerable<NativeShopLocation> sourcesInSeed)
        {
            foreach (NativeShopLocation source in sourcesInSeed)
            {
                if (!state.IsLocationChecked(source.LocationName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFinalDepartureFlags(PlayerData playerData)
        {
            return playerData.MapperLeftBellhart &&
                   playerData.MapperLeftBoneForest &&
                   playerData.MapperLeftBonetown &&
                   playerData.MapperLeftCoralCaverns &&
                   playerData.MapperLeftCrawl &&
                   playerData.MapperLeftDocks &&
                   playerData.MapperLeftDustpens &&
                   playerData.MapperLeftGreymoor &&
                   playerData.MapperLeftHuntersNest &&
                   playerData.MapperLeftJudgeSteps &&
                   playerData.MapperLeftPeak &&
                   playerData.MapperLeftShadow &&
                   playerData.MapperLeftShellwood &&
                   playerData.MapperLeftWilds;
        }

        private static void SetDepartureFlags(
            PlayerData playerData,
            bool value)
        {
            playerData.mapperAway = value;
            playerData.MapperLeftBellhart = value;
            playerData.MapperLeftBoneForest = value;
            playerData.MapperLeftBonetown = value;
            playerData.MapperLeftCoralCaverns = value;
            playerData.MapperLeftCrawl = value;
            playerData.MapperLeftDocks = value;
            playerData.MapperLeftDustpens = value;
            playerData.MapperLeftGreymoor = value;
            playerData.MapperLeftHuntersNest = value;
            playerData.MapperLeftJudgeSteps = value;
            playerData.MapperLeftPeak = value;
            playerData.MapperLeftShadow = value;
            playerData.MapperLeftShellwood = value;
            playerData.MapperLeftWilds = value;
        }

        private static bool IsMapperDepartureField(string boolName)
        {
            switch (boolName)
            {
                case "MapperLeftBellhart":
                case "MapperLeftBoneForest":
                case "MapperLeftBonetown":
                case "MapperLeftCoralCaverns":
                case "MapperLeftCrawl":
                case "MapperLeftDocks":
                case "MapperLeftDustpens":
                case "MapperLeftGreymoor":
                case "MapperLeftHuntersNest":
                case "MapperLeftJudgeSteps":
                case "MapperLeftPeak":
                case "MapperLeftShadow":
                case "MapperLeftShellwood":
                case "MapperLeftWilds":
                    return true;
                default:
                    return false;
            }
        }

        internal static bool TryPreserveShakraStock(
            SaveState state,
            PlayerData playerData)
        {
            if (!TryGetActiveShakraMapSources(
                    state,
                    playerData,
                    out List<NativeShopLocation> sourcesInSeed) ||
                !HasUncheckedShakraMapSource(state, sourcesInSeed))
            {
                return false;
            }

            // Keep the final quest gate active, but undo only Shakra's native
            // departure flags while physical randomized map checks remain.
            SetDepartureFlags(playerData, false);
            return true;
        }

        internal static bool TryFinalizeShakraDeparture(
            SaveState state,
            PlayerData playerData)
        {
            if (!TryGetActiveShakraMapSources(
                    state,
                    playerData,
                    out List<NativeShopLocation> sourcesInSeed) ||
                HasUncheckedShakraMapSource(state, sourcesInSeed) ||
                HasFinalDepartureFlags(playerData))
            {
                return false;
            }

            playerData.MapperLeaveAll();
            return true;
        }

        internal static bool ReconcileShakraStock(
            SaveState state,
            PlayerData playerData)
        {
            return TryPreserveShakraStock(state, playerData) ||
                   TryFinalizeShakraDeparture(state, playerData);
        }

        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.MapperLeaveAll))]
        private static class MapperLeaveAllPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(PlayerData __instance)
            {
                TryPreserveShakraStock(SaveState.Instance, __instance);
            }
        }

        [HarmonyPatch(
            typeof(GameManager),
            nameof(GameManager.MapperLeavePreviousLocations),
            new Type[] { typeof(string) })]
        private static class MapperLeavePreviousLocationsPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                TryPreserveShakraStock(
                    SaveState.Instance,
                    PlayerData.instance
                );
            }
        }

        [HarmonyPatch(
            typeof(GameManager),
            nameof(GameManager.FinishedEnteringScene))]
        private static class FinishedEnteringScenePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                TryPreserveShakraStock(
                    SaveState.Instance,
                    PlayerData.instance
                );
            }
        }

        [HarmonyPatch(
            typeof(DeactivateIfPlayerdataTrue),
            "ForceEvaluate")]
        private static class MapperDepartureVisibilityPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                DeactivateIfPlayerdataTrue __instance)
            {
                if (__instance == null ||
                    !IsMapperDepartureField(__instance.boolName) ||
                    !TryPreserveShakraStock(
                        SaveState.Instance,
                        PlayerData.instance
                    ))
                {
                    return true;
                }

                GameObject target =
                    __instance.objectToDeactivate ?? __instance.gameObject;
                if (target != null)
                {
                    target.SetActive(true);
                }

                return false;
            }
        }
    }
}
