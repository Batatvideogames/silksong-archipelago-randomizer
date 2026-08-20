using HarmonyLib;
using System;

namespace SilksongRandomizer.Patches
{
    internal static class QuestRequirementPatches
    {
        private const string SoulSnareQuest = "Soul Snare";
        private const string ShakraFinalQuest = "Shakra Final Quest";
        private const string SnareSetterItem = "Tool: Snare Setter";
        private const string BelltownScene = "Belltown";
        private const string GroalLocation = "Boss: Groal the Great";

        private static readonly string[] ShakraMapItems =
        {
            "Map: Mosslands",
            "Map: The Marrow",
            "Map: Deep Docks",
            "Map: Far Fields",
            "Map: Wormways",
            "Map: Hunter's March",
            "Map: Greymoor",
            "Map: Bellhart",
            "Map: Shellwood",
            "Map: Blasted Steps",
            "Map: Sinner's Road",
            "Map: Mount Fay",
            "Map: Sands of Karak",
            "Map: Bilewater",
        };

        private static readonly string[] ThreefoldMelodyItems =
        {
            "Architect's Melody",
            "Conductor's Melody",
            "Vaultkeeper's Melody",
        };

        private sealed class ShakraTimePassesState
        {
            internal SaveState SaveState;
            internal PlayerData PlayerData;
            internal string SceneName;
            internal bool NativeStockGateSuppressed;
        }

        internal static bool ApplySoulSnareRequirement(
            FullQuestBase quest,
            bool nativeCanComplete,
            SaveState state)
        {
            if (!nativeCanComplete ||
                quest == null ||
                state == null ||
                !state.IsRandomized(ItemType.Tool) ||
                !string.Equals(
                    quest.name,
                    SoulSnareQuest,
                    StringComparison.Ordinal))
            {
                return nativeCanComplete;
            }

            return state.receivedItems != null &&
                   state.receivedItems.Contains(
                       ItemSet.GetCanonicalItemName(SnareSetterItem));
        }

        internal static bool ApplyShakraFinalQuestRequirement(
            FullQuestBase quest,
            bool nativeIsAvailable,
            SaveState state,
            PlayerData playerData)
        {
            if (quest == null ||
                state == null ||
                !string.Equals(
                    quest.name,
                    ShakraFinalQuest,
                    StringComparison.Ordinal))
            {
                return nativeIsAvailable;
            }

            if (!state.IsRandomized(ItemType.Skill))
            {
                return nativeIsAvailable;
            }

            if (playerData == null)
            {
                return false;
            }

            // ShakraFinalQuestAppear is the shipped story/stock gate. Only
            // replace the physical Fayforn pickup flag with AP ownership.
            return playerData.ShakraFinalQuestAppear &&
                   state.canDoubleJump;
        }

        internal static bool TryApplyShakraOwnedMapTransition(
            SaveState state,
            PlayerData playerData,
            string sceneName)
        {
            if (state == null ||
                !state.TrailsEndUsesOwnedMaps ||
                playerData == null ||
                playerData.ShakraFinalQuestAppear ||
                string.Equals(
                    sceneName,
                    BelltownScene,
                    StringComparison.Ordinal) ||
                !HasEveryShakraMap(state, playerData) ||
                !HasGroalOrTwoMelodies(state, playerData))
            {
                return false;
            }

            playerData.ShakraFinalQuestAppear = true;
            playerData.MapperLeaveAll();
            return true;
        }

        private static bool HasEveryShakraMap(
            SaveState state,
            PlayerData playerData)
        {
            if (state == null)
            {
                return false;
            }

            // Randomized maps are owned through AP, so native source flags do
            // not count. start_with_maps records its precollected items the
            // same way.
            if (state.IsRandomized(ItemType.Map))
            {
                if (state.receivedItems == null)
                {
                    return false;
                }

                foreach (string itemName in ShakraMapItems)
                {
                    if (!state.receivedItems.Contains(
                            ItemSet.GetCanonicalItemName(itemName)))
                    {
                        return false;
                    }
                }

                return true;
            }

            // Vanilla maps are locked AP events and never reach the client.
            // Use the native map flags in that mode.
            return playerData != null &&
                   playerData.HasMossGrottoMap &&
                   playerData.HasBoneforestMap &&
                   playerData.HasDocksMap &&
                   playerData.HasWildsMap &&
                   playerData.HasCrawlMap &&
                   playerData.HasHuntersNestMap &&
                   playerData.HasGreymoorMap &&
                   playerData.HasBellhartMap &&
                   playerData.HasShellwoodMap &&
                   playerData.HasJudgeStepsMap &&
                   playerData.HasDustpensMap &&
                   playerData.HasPeakMap &&
                   playerData.HasCoralMap &&
                   playerData.HasSwampMap;
        }

        private static bool HasGroalOrTwoMelodies(
            SaveState state,
            PlayerData playerData)
        {
            if (playerData == null)
            {
                return false;
            }

            if (playerData.DefeatedSwampShaman ||
                (state != null &&
                 state.IsRandomized(ItemType.Boss) &&
                 state.IsLocationInSeed(GroalLocation) &&
                 state.IsLocationChecked(GroalLocation)))
            {
                return true;
            }

            int melodyCount = 0;
            if (state != null && state.IsRandomized(ItemType.Melody))
            {
                if (state.receivedItems == null)
                {
                    return false;
                }

                foreach (string itemName in ThreefoldMelodyItems)
                {
                    if (state.receivedItems.Contains(
                            ItemSet.GetCanonicalItemName(itemName)))
                    {
                        melodyCount++;
                    }
                }
            }
            else
            {
                // Vanilla Melodies are locked AP events and never reach the
                // client. Use the learned-melody flags in that mode.
                if (playerData.HasMelodyArchitect)
                {
                    melodyCount++;
                }
                if (playerData.HasMelodyConductor)
                {
                    melodyCount++;
                }
                if (playerData.HasMelodyLibrarian)
                {
                    melodyCount++;
                }
            }

            return melodyCount >= 2;
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_CanComplete")]
        private static class SoulSnareCanCompletePatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                FullQuestBase __instance,
                ref bool __result)
            {
                __result = ApplySoulSnareRequirement(
                    __instance,
                    __result,
                    SaveState.Instance);
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_IsAvailable")]
        private static class ShakraFinalQuestAvailablePatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                FullQuestBase __instance,
                ref bool __result)
            {
                __result = ApplyShakraFinalQuestRequirement(
                    __instance,
                    __result,
                    SaveState.Instance,
                    PlayerData.instance);
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.TimePasses))]
        private static class ShakraFinalQuestTimePassesPatch
        {
            [HarmonyPrefix]
            private static void Prefix(
                GameManager __instance,
                out ShakraTimePassesState __state)
            {
                __state = null;
                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null ||
                    !state.TrailsEndUsesOwnedMaps ||
                    playerData == null ||
                    playerData.ShakraFinalQuestAppear)
                {
                    return;
                }

                __state = new ShakraTimePassesState
                {
                    SaveState = state,
                    PlayerData = playerData,
                    SceneName = __instance.GetSceneNameString(),
                    NativeStockGateSuppressed = true,
                };

                // owned_maps replaces only the stock gate. Set it for the
                // native check, then restore it in the postfix.
                playerData.ShakraFinalQuestAppear = true;
            }

            [HarmonyPostfix]
            private static void Postfix(ShakraTimePassesState __state)
            {
                if (__state == null ||
                    !__state.NativeStockGateSuppressed ||
                    __state.PlayerData == null)
                {
                    return;
                }

                __state.PlayerData.ShakraFinalQuestAppear = false;
                __state.NativeStockGateSuppressed = false;
                TryApplyShakraOwnedMapTransition(
                    __state.SaveState,
                    __state.PlayerData,
                    __state.SceneName
                );
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(
                Exception __exception,
                ShakraTimePassesState __state)
            {
                if (__state != null &&
                    __state.NativeStockGateSuppressed &&
                    __state.PlayerData != null)
                {
                    __state.PlayerData.ShakraFinalQuestAppear = false;
                }

                return __exception;
            }
        }
    }
}
