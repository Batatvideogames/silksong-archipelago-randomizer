using HarmonyLib;
using System;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(ToolItem), "get_IsEquipped")]
    internal static class AutomaticCompassPatch
    {
        [ThreadStatic]
        private static int automaticCompassMapReadDepth;

        private static void EnterMapCompassRead(out bool __state)
        {
            SaveState state = SaveState.Instance;
            __state = state != null && state.automaticCompass;
            if (__state)
            {
                automaticCompassMapReadDepth++;
            }
        }

        private static Exception ExitMapCompassRead(
            Exception __exception,
            bool __state)
        {
            if (__state)
            {
                automaticCompassMapReadDepth = Math.Max(
                    0,
                    automaticCompassMapReadDepth - 1
                );
            }

            return __exception;
        }

        private static bool Prefix(
            ToolItem __instance,
            ref bool __result
        )
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                !state.automaticCompass ||
                automaticCompassMapReadDepth <= 0)
            {
                return true;
            }

            ToolItem compass = GlobalSettings.Gameplay.CompassTool;
            if (compass == null ||
                !ReferenceEquals(__instance, compass))
            {
                return true;
            }

            // Only the two native map renderers enter this scoped context.
            // Other inventory and FSM callers continue to see the real
            // unequipped state.
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(GameMap), "PositionCompassAndCorpse")]
        private static class QuickMapCompassReadPatch
        {
            [HarmonyPrefix]
            private static void Prefix(out bool __state)
            {
                EnterMapCompassRead(out __state);
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(
                Exception __exception,
                bool __state)
            {
                return ExitMapCompassRead(__exception, __state);
            }
        }

        [HarmonyPatch(typeof(InventoryWideMap), "UpdatePositions")]
        private static class WideMapCompassReadPatch
        {
            [HarmonyPrefix]
            private static void Prefix(out bool __state)
            {
                EnterMapCompassRead(out __state);
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(
                Exception __exception,
                bool __state)
            {
                return ExitMapCompassRead(__exception, __state);
            }
        }
    }

    [HarmonyPatch(
        typeof(HealthManager),
        "SpawnCurrency",
        new Type[]
        {
            typeof(Transform),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(int),
            typeof(bool),
        }
    )]
    internal static class EnemyRosaryMultiplierPatch
    {
        private const int ScaleDenominator = 2;

        private static SaveState roundingState;
        private static string roundingMultiplier =
            Archipelago.RosaryMultiplierVanilla;
        private static int smallRemainder;
        private static int mediumRemainder;
        private static int largeRemainder;
        private static int largeSmoothRemainder;

        private static void Prefix(
            ref int smallGeoCount,
            ref int mediumGeoCount,
            ref int largeGeoCount,
            ref int largeSmoothGeoCount
        )
        {
            SaveState state = SaveState.Instance;
            if (state == null)
            {
                return;
            }

            string multiplier = state.enemyRosaryMultiplier;
            if (!ReferenceEquals(roundingState, state) ||
                !string.Equals(
                    roundingMultiplier,
                    multiplier,
                    StringComparison.Ordinal
                ))
            {
                ResetRounding(state, multiplier);
            }

            int numerator = GetScaleNumerator(multiplier);
            if (numerator == ScaleDenominator)
            {
                return;
            }

            smallGeoCount = ScaleCountDeterministically(
                smallGeoCount,
                numerator,
                ref smallRemainder
            );
            mediumGeoCount = ScaleCountDeterministically(
                mediumGeoCount,
                numerator,
                ref mediumRemainder
            );
            largeGeoCount = ScaleCountDeterministically(
                largeGeoCount,
                numerator,
                ref largeRemainder
            );
            largeSmoothGeoCount = ScaleCountDeterministically(
                largeSmoothGeoCount,
                numerator,
                ref largeSmoothRemainder
            );
        }

        internal static int GetScaleNumerator(string multiplier)
        {
            switch (multiplier)
            {
                case Archipelago.RosaryMultiplierOneAndHalf:
                    return 3;
                case Archipelago.RosaryMultiplierDouble:
                    return 4;
                case Archipelago.RosaryMultiplierTriple:
                    return 6;
                case Archipelago.RosaryMultiplierQuintuple:
                    return 10;
                case Archipelago.RosaryMultiplierTenfold:
                    return 20;
                default:
                    return ScaleDenominator;
            }
        }

        internal static int ScaleCountDeterministically(
            int count,
            int numerator,
            ref int remainder
        )
        {
            if (count <= 0 || numerator <= 0)
            {
                return count;
            }

            long scaledUnits =
                ((long)count * numerator) + remainder;
            long scaledCount = scaledUnits / ScaleDenominator;
            remainder = (int)(scaledUnits % ScaleDenominator);
            return scaledCount >= int.MaxValue
                ? int.MaxValue
                : (int)scaledCount;
        }

        private static void ResetRounding(
            SaveState state,
            string multiplier
        )
        {
            roundingState = state;
            roundingMultiplier = multiplier ??
                Archipelago.RosaryMultiplierVanilla;
            smallRemainder = 0;
            mediumRemainder = 0;
            largeRemainder = 0;
            largeSmoothRemainder = 0;
        }
    }

    [HarmonyPatch(
        typeof(HealthManager),
        "SpawnCurrency",
        new Type[]
        {
            typeof(Transform),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(int),
            typeof(bool),
        }
    )]
    internal static class EnemyShardMultiplierPatch
    {
        private static SaveState roundingState;
        private static string roundingMultiplier =
            Archipelago.RosaryMultiplierVanilla;
        private static int remainder;

        private static void Prefix(ref int shellShardCount)
        {
            SaveState state = SaveState.Instance;
            if (state == null)
            {
                return;
            }

            string multiplier = state.enemyShardMultiplier;
            if (!ReferenceEquals(roundingState, state) ||
                !string.Equals(
                    roundingMultiplier,
                    multiplier,
                    StringComparison.Ordinal
                ))
            {
                roundingState = state;
                roundingMultiplier = multiplier ??
                    Archipelago.RosaryMultiplierVanilla;
                remainder = 0;
            }

            int numerator =
                EnemyRosaryMultiplierPatch.GetScaleNumerator(multiplier);
            shellShardCount =
                EnemyRosaryMultiplierPatch.ScaleCountDeterministically(
                    shellShardCount,
                    numerator,
                    ref remainder
                );
        }
    }

    [HarmonyPatch(typeof(DialogueBox), "Start")]
    internal static class FasterDialoguePatch
    {
        private const float RevealSpeedMultiplier = 2f;

        private static void Prefix(
            ref float ___regularRevealSpeed,
            ref float ___fastRevealSpeed
        )
        {
            SaveState state = SaveState.Instance;
            if (state == null || !state.fasterDialogue)
            {
                return;
            }

            // DialogueBox.Start copies regularRevealSpeed into the active
            // currentRevealSpeed after this prefix. The native print
            // coroutine therefore remains in charge of choices and line
            // advancement. Only its letter reveal rate changes.
            ___regularRevealSpeed *= RevealSpeedMultiplier;
            ___fastRevealSpeed *= RevealSpeedMultiplier;
        }
    }

}
