using System;

namespace SilksongRandomizer
{
    /// <summary>
    /// Pure accounting rules for native Rosary death/cocoon transitions.
    /// Keeping these rules independent of Unity and Archipelago makes the
    /// timing-sensitive integration code directly testable.
    /// </summary>
    internal static class CurrencyLinkAccounting
    {
        internal static int CalculateNativeDeathLoss(
            int before,
            int after,
            int nativeCocoonAmount)
        {
            int availableBefore = Math.Max(0, before);
            int observedLoss = Math.Max(
                0,
                availableBefore - Math.Max(0, after)
            );
            int cocoonLoss = Math.Min(
                availableBefore,
                Math.Max(0, nativeCocoonAmount)
            );

            // HeroController.Die writes HeroCorpseMoneyPool from the exact
            // amount removed, including the Dead Purses retained portion.
            // The field delta is the fallback for unusual or no-cocoon deaths.
            return cocoonLoss > 0 ? cocoonLoss : observedLoss;
        }

        internal static int CalculateDeathDebit(
            int predictedShared,
            int nativeDeathLoss)
        {
            return CalculateDeathDebit(
                predictedShared,
                nativeDeathLoss,
                false
            );
        }

        internal static int CalculateDeathDebit(
            int predictedShared,
            int nativeDeathLoss,
            bool suppressSharedMutation)
        {
            if (suppressSharedMutation)
            {
                return 0;
            }

            return Math.Min(
                Math.Max(0, predictedShared),
                Math.Max(0, nativeDeathLoss)
            );
        }

        internal static int CalculateConfirmedDeathDebit(
            int requestedDebit,
            int originalShared,
            int resultingShared)
        {
            int availableBefore = Math.Max(0, originalShared);
            int availableAfter = Math.Max(0, resultingShared);
            int appliedDebit = Math.Max(
                0,
                availableBefore - availableAfter
            );
            return Math.Min(
                Math.Max(0, requestedDebit),
                appliedDebit
            );
        }
    }
}
