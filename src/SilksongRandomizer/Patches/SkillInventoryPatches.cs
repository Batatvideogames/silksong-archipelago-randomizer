using HarmonyLib;
using System;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Keeps randomized skill source completion separate from inventory
    /// ownership. Native skill sources must retain their PlayerData flags so
    /// they stay collected, while the inventory must reflect AP receipts.
    /// </summary>
    internal static class SkillInventoryPatches
    {
        private sealed class AbilitySnapshot
        {
            internal PlayerData PlayerData;
            internal bool NativeWallJump;
            internal bool NativeSuperJump;
            internal bool NativeHarpoonDash;
        }

        private sealed class DressesSnapshot
        {
            internal PlayerData PlayerData;
            internal bool NativeBrolly;
            internal bool NativeDoubleJump;
        }

        private static bool TestsRandomizedInventoryAbility(
            PlayerDataTest test)
        {
            if (test == null || test.TestGroups == null)
            {
                return false;
            }

            foreach (PlayerDataTest.TestGroup group in test.TestGroups)
            {
                if (group.Tests == null)
                {
                    continue;
                }

                foreach (PlayerDataTest.Test condition in group.Tests)
                {
                    if (condition.Type != PlayerDataTest.TestType.Bool)
                    {
                        continue;
                    }

                    string fieldName = condition.FieldName;
                    if (string.Equals(
                            fieldName,
                            nameof(PlayerData.hasWalljump),
                            StringComparison.Ordinal) ||
                        string.Equals(
                            fieldName,
                            nameof(PlayerData.hasSuperJump),
                            StringComparison.Ordinal) ||
                        string.Equals(
                            fieldName,
                            nameof(PlayerData.hasHarpoonDash),
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void PrefixDressesEvaluation(
            CollectableItemStates instance,
            out DressesSnapshot snapshot)
        {
            snapshot = null;

            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                playerData == null ||
                !state.IsRandomized(ItemType.Skill) ||
                instance == null ||
                !string.Equals(
                    instance.name,
                    "Dresses",
                    StringComparison.Ordinal))
            {
                return;
            }

            snapshot = new DressesSnapshot
            {
                PlayerData = playerData,
                NativeBrolly = playerData.hasBrolly,
                NativeDoubleJump = playerData.hasDoubleJump,
            };
            playerData.hasBrolly = state.canBrolly;
            playerData.hasDoubleJump = state.canDoubleJump;
        }

        private static Exception FinalizeDressesEvaluation(
            Exception exception,
            DressesSnapshot snapshot)
        {
            if (snapshot != null && snapshot.PlayerData != null)
            {
                snapshot.PlayerData.hasBrolly = snapshot.NativeBrolly;
                snapshot.PlayerData.hasDoubleJump =
                    snapshot.NativeDoubleJump;
            }

            return exception;
        }

        internal static int ResolveDressesStateIndex(
            bool hasDriftersCloak,
            bool hasFaydownCloak)
        {
            // The Dresses collectable has four states, in this
            // order: normal, Drifter's, Faydown and Faydown + Drifter's.
            // The visual comes directly from AP ownership so native story
            // source flags cannot make a Faydown-only player look normal.
            if (hasFaydownCloak)
            {
                return hasDriftersCloak ? 3 : 2;
            }

            return hasDriftersCloak ? 1 : 0;
        }

        private static bool TryResolveDressesStateIndex(
            CollectableItemStates instance,
            out int stateIndex)
        {
            stateIndex = 0;
            SaveState state = SaveState.Instance;
            if (state == null ||
                !state.IsRandomized(ItemType.Skill) ||
                instance == null ||
                !string.Equals(
                    instance.name,
                    "Dresses",
                    StringComparison.Ordinal))
            {
                return false;
            }

            stateIndex = ResolveDressesStateIndex(
                state.canBrolly,
                state.canDoubleJump
            );
            return true;
        }

        [HarmonyPatch(typeof(InventoryItemConditional), "Evaluate")]
        private static class RandomizedAbilityDisplayPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                InventoryItemConditional __instance,
                out AbilitySnapshot __state)
            {
                __state = null;

                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null ||
                    playerData == null ||
                    !state.IsRandomized(ItemType.Skill) ||
                    __instance == null ||
                    !TestsRandomizedInventoryAbility(__instance.Test))
                {
                    return;
                }

                __state = new AbilitySnapshot
                {
                    PlayerData = playerData,
                    NativeWallJump = playerData.hasWalljump,
                    NativeSuperJump = playerData.hasSuperJump,
                    NativeHarpoonDash = playerData.hasHarpoonDash,
                };
                playerData.hasWalljump = state.canWallJump;
                playerData.hasSuperJump = state.canSilkSoar;
                playerData.hasHarpoonDash = state.canUseHarpoon;
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static Exception Finalizer(
                Exception __exception,
                AbilitySnapshot __state)
            {
                if (__state != null && __state.PlayerData != null)
                {
                    __state.PlayerData.hasWalljump =
                        __state.NativeWallJump;
                    __state.PlayerData.hasSuperJump =
                        __state.NativeSuperJump;
                    __state.PlayerData.hasHarpoonDash =
                        __state.NativeHarpoonDash;
                }

                return __exception;
            }
        }

        [HarmonyPatch(typeof(CollectableItemStates), "GetCurrentStateIndex")]
        private static class RandomizedDressesStatePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(
                CollectableItemStates __instance,
                ref int __result)
            {
                if (TryResolveDressesStateIndex(
                        __instance,
                        out int stateIndex))
                {
                    __result = stateIndex;
                }
            }
        }

        [HarmonyPatch(
            typeof(CollectableItemStates),
            nameof(CollectableItemStates.CollectedAmount),
            MethodType.Getter)]
        private static class RandomizedDressesAmountPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                CollectableItemStates __instance,
                out DressesSnapshot __state)
            {
                PrefixDressesEvaluation(__instance, out __state);
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static Exception Finalizer(
                Exception __exception,
                DressesSnapshot __state)
            {
                return FinalizeDressesEvaluation(__exception, __state);
            }
        }
    }
}
