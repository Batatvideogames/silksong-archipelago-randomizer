using System;
using System.Collections;

namespace SilksongRandomizer.Patches
{
    internal static class ConsumableToolPatches
    {
        // receivedItems stores ItemSet canonical names, which
        // omit the protocol-facing "Tool: " prefix.
        internal const string FleaBrewItemName = "Flea Brew";
        internal const string PlasmiumPhialItemName =
            "Plasmium Phial";

        private const string FleaBrewToolName = "Flea Brew";
        private const string PlasmiumPhialToolName =
            "Lifeblood Syringe";

        internal static bool RequiresInitialFill(
            SaveState state,
            string itemName)
        {
            if (state == null ||
                !state.IsRandomized(ItemType.Tool))
            {
                return false;
            }

            string canonicalName = ItemSet.GetCanonicalItemName(itemName);
            if (string.Equals(
                    canonicalName,
                    FleaBrewItemName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return !state.fleaBrewInitialFillApplied;
            }

            return string.Equals(
                       canonicalName,
                       PlasmiumPhialItemName,
                       StringComparison.OrdinalIgnoreCase) &&
                   !state.plasmiumPhialInitialFillApplied;
        }

        internal static bool TryInitializeReceivedConsumable(
            SaveState state,
            string itemName)
        {
            if (!RequiresInitialFill(state, itemName))
            {
                return true;
            }

            string canonicalName = ItemSet.GetCanonicalItemName(itemName);
            if (state.receivedItems == null ||
                !state.receivedItems.Contains(canonicalName))
            {
                // A queued receipt must be committed before native liquid
                // state can be changed. Receipt ownership controls this state.
                return false;
            }

            string nativeToolName = string.Equals(
                canonicalName,
                FleaBrewItemName,
                StringComparison.OrdinalIgnoreCase)
                    ? FleaBrewToolName
                    : PlasmiumPhialToolName;

            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return false;
            }

            ToolItemStatesLiquid tool =
                ToolItemManager.GetToolByName(nativeToolName)
                    as ToolItemStatesLiquid;
            if (tool == null)
            {
                return false;
            }

            try
            {
                // This reproduces the state written by ToolItem.Unlock and
                // ToolItemStatesLiquid.OnUnlocked without calling Unlock:
                // the physical vanilla source must remain a separate AP
                // check and native IsUnlocked is not AP ownership.
                int storageAmount =
                    ToolItemManager.GetToolStorageAmount(tool);

                // A native memory sequence redirects SavedData.AmountLeft to
                // a temporary, nonserialized override. Fill both the raw
                // persistent entry and that live override so the received
                // charge is usable now and is not discarded on memory exit.
                ToolItemsData.Data persistentData =
                    playerData.Tools.GetData(tool.name);
                persistentData.AmountLeft = storageAmount;
                playerData.Tools.SetData(tool.name, persistentData);

                ToolItemsData.Data savedData = tool.SavedData;
                savedData.AmountLeft = storageAmount;
                tool.SavedData = savedData;

                // The unlock matches native behavior. Flea Brew owns a finite
                // liquid reserve and an intentional empty-bottle state.
                // Neither is an AP-specific refill mechanic. In particular,
                // do not override ToolItemStatesLiquid.HasInfiniteRefills:
                // doing so makes every later bench refill free forever.
                tool.RefillRefills(false);

                AttackToolBinding? binding =
                    ToolItemManager.GetAttackToolBinding(tool);
                if (binding.HasValue)
                {
                    ToolItemManager.ReportBoundAttackToolUpdated(
                        binding.Value
                    );
                }

                if (string.Equals(
                        canonicalName,
                        FleaBrewItemName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    state.fleaBrewInitialFillApplied = true;
                }
                else
                {
                    state.plasmiumPhialInitialFillApplied = true;
                }

                return true;
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Native liquid state for " +
                    canonicalName + " was not ready; initialization will " +
                    "be retried: " + ex.Message
                );
                return false;
            }
        }

        internal static IEnumerator SynchronizeReceivedConsumables(
            SaveState expectedState)
        {
            if (expectedState == null)
            {
                yield break;
            }

            while (SaveState.Instance == expectedState)
            {
                bool fleaBrewReady =
                    !expectedState.receivedItems.Contains(
                        FleaBrewItemName
                    ) ||
                    TryInitializeReceivedConsumable(
                        expectedState,
                        FleaBrewItemName
                    );
                bool plasmiumPhialReady =
                    !expectedState.receivedItems.Contains(
                        PlasmiumPhialItemName
                    ) ||
                    TryInitializeReceivedConsumable(
                        expectedState,
                        PlasmiumPhialItemName
                    );

                if (fleaBrewReady && plasmiumPhialReady)
                {
                    yield break;
                }

                yield return null;
            }
        }

    }
}
