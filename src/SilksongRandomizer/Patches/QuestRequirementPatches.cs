using HarmonyLib;
using System;

namespace SilksongRandomizer.Patches
{
    internal static class QuestRequirementPatches
    {
        private const string LiquidLacquerQuest =
            "Courier Delivery Mask Maker";
        private const string PinstressBattleQuest = "Pinstress Battle Pre";
        private const string SkullKingQuest = "Skull King";
        private const string SoulSnareQuest = "Soul Snare";
        private const string SprintmasterQuest = "Sprintmaster Pre";
        private const string ShakraFinalQuest = "Shakra Final Quest";
        private const string TormentedTrobbioQuest = "Tormented Trobbio";
        private const string SnareSetterItem = "Tool: Snare Setter";
        private const string SoulSnareNativeTool = "Silk Snare";
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

        private sealed class TimePassesAbilitySnapshot
        {
            internal PlayerData PlayerData;
            internal bool NativeChargeSlash;
            internal bool NativeDoubleJump;
            internal bool NativeHarpoonDash;
            internal bool Applied;
        }

        private enum MirroredAbility
        {
            DoubleJump,
            WallJump,
            SuperJump,
        }

        private sealed class AbilitySnapshot
        {
            internal PlayerData PlayerData;
            internal MirroredAbility Ability;
            internal bool NativeValue;
        }

        private static AbilitySnapshot MirrorAbility(
            PlayerData playerData,
            MirroredAbility ability,
            bool effectiveValue)
        {
            AbilitySnapshot snapshot = new AbilitySnapshot
            {
                PlayerData = playerData,
                Ability = ability,
            };
            switch (ability)
            {
                case MirroredAbility.DoubleJump:
                    snapshot.NativeValue = playerData.hasDoubleJump;
                    playerData.hasDoubleJump =
                        snapshot.NativeValue || effectiveValue;
                    break;
                case MirroredAbility.WallJump:
                    snapshot.NativeValue = playerData.hasWalljump;
                    playerData.hasWalljump =
                        snapshot.NativeValue || effectiveValue;
                    break;
                case MirroredAbility.SuperJump:
                    snapshot.NativeValue = playerData.hasSuperJump;
                    playerData.hasSuperJump =
                        snapshot.NativeValue || effectiveValue;
                    break;
            }
            return snapshot;
        }

        private static AbilitySnapshot BeginSoulSnareFaydownCheck(
            QuestCompleteTotalGroup group)
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (group == null ||
                state == null ||
                playerData == null ||
                !state.IsRandomized(ItemType.Skill) ||
                !string.Equals(
                    group.name,
                    SoulSnareQuest,
                    StringComparison.Ordinal))
            {
                return null;
            }

            return MirrorAbility(
                playerData,
                MirroredAbility.DoubleJump,
                state.canDoubleJump);
        }

        private static AbilitySnapshot BeginWishAbilityCheck(
            FullQuestBase quest)
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (quest == null ||
                state == null ||
                playerData == null ||
                !state.IsRandomized(ItemType.Skill))
            {
                return null;
            }

            switch (quest.name)
            {
                case LiquidLacquerQuest:
                    return MirrorAbility(
                        playerData,
                        MirroredAbility.DoubleJump,
                        state.canDoubleJump);
                case SkullKingQuest:
                    return MirrorAbility(
                        playerData,
                        MirroredAbility.WallJump,
                        state.canWallJump);
                case PinstressBattleQuest:
                case SprintmasterQuest:
                case TormentedTrobbioQuest:
                    return MirrorAbility(
                        playerData,
                        MirroredAbility.SuperJump,
                        state.canSilkSoar);
                default:
                    return null;
            }
        }

        private static TimePassesAbilitySnapshot BeginTimePassesAbilityChecks()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                playerData == null ||
                !state.IsRandomized(ItemType.Skill))
            {
                return null;
            }

            TimePassesAbilitySnapshot snapshot =
                new TimePassesAbilitySnapshot
                {
                    PlayerData = playerData,
                    NativeChargeSlash = playerData.hasChargeSlash,
                    NativeDoubleJump = playerData.hasDoubleJump,
                    NativeHarpoonDash = playerData.hasHarpoonDash,
                    Applied = true,
                };
            playerData.hasChargeSlash =
                snapshot.NativeChargeSlash || state.canChargeSlash;
            playerData.hasDoubleJump =
                snapshot.NativeDoubleJump || state.canDoubleJump;
            playerData.hasHarpoonDash =
                snapshot.NativeHarpoonDash || state.canUseHarpoon;
            return snapshot;
        }

        private static void RestoreTimePassesAbilities(
            TimePassesAbilitySnapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.PlayerData == null ||
                !snapshot.Applied)
            {
                return;
            }

            snapshot.PlayerData.hasChargeSlash =
                snapshot.NativeChargeSlash;
            snapshot.PlayerData.hasDoubleJump =
                snapshot.NativeDoubleJump;
            snapshot.PlayerData.hasHarpoonDash =
                snapshot.NativeHarpoonDash;
            snapshot.Applied = false;
        }

        private static void RestoreAbility(AbilitySnapshot snapshot)
        {
            if (snapshot == null || snapshot.PlayerData == null)
            {
                return;
            }

            switch (snapshot.Ability)
            {
                case MirroredAbility.DoubleJump:
                    snapshot.PlayerData.hasDoubleJump =
                        snapshot.NativeValue;
                    break;
                case MirroredAbility.WallJump:
                    snapshot.PlayerData.hasWalljump =
                        snapshot.NativeValue;
                    break;
                case MirroredAbility.SuperJump:
                    snapshot.PlayerData.hasSuperJump =
                        snapshot.NativeValue;
                    break;
            }
        }

        internal static bool ApplySoulSnareRequirement(
            FullQuestBase quest,
            bool nativeCanComplete,
            SaveState state)
        {
            if (quest == null ||
                state == null ||
                !state.IsRandomized(ItemType.Tool) ||
                !string.Equals(
                    quest.name,
                    SoulSnareQuest,
                    StringComparison.Ordinal))
            {
                return nativeCanComplete;
            }

            if (state.receivedItems == null ||
                !state.receivedItems.Contains(
                    ItemSet.GetCanonicalItemName(SnareSetterItem)))
            {
                return false;
            }

            foreach (var pair in quest.TargetsAndCounters)
            {
                ToolItem nativeTool = pair.target.Counter as ToolItem;
                if (nativeTool != null &&
                    string.Equals(
                        nativeTool.name,
                        SoulSnareNativeTool,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (pair.count < pair.target.Count)
                {
                    return false;
                }
            }

            return true;
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

        [HarmonyPatch(typeof(QuestCompleteTotalGroup), "get_IsFulfilled")]
        private static class SoulSnareFaydownPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                QuestCompleteTotalGroup __instance,
                out AbilitySnapshot __state)
            {
                __state = BeginSoulSnareFaydownCheck(__instance);
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static Exception Finalizer(
                Exception __exception,
                AbilitySnapshot __state)
            {
                RestoreAbility(__state);
                return __exception;
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_IsAvailable")]
        private static class RandomizedWishAbilityPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                FullQuestBase __instance,
                out AbilitySnapshot __state)
            {
                __state = BeginWishAbilityCheck(__instance);
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static Exception Finalizer(
                Exception __exception,
                AbilitySnapshot __state)
            {
                RestoreAbility(__state);
                return __exception;
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
        private static class RandomizedWishTimePassesPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                out TimePassesAbilitySnapshot __state)
            {
                __state = BeginTimePassesAbilityChecks();
            }

            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(
                TimePassesAbilitySnapshot __state)
            {
                RestoreTimePassesAbilities(__state);
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static Exception Finalizer(
                Exception __exception,
                TimePassesAbilitySnapshot __state)
            {
                RestoreTimePassesAbilities(__state);
                return __exception;
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
