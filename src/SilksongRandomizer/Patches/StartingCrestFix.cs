
using System;
using System.Collections;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class StartingCrestFix
    {
        internal static bool NeedsRepair(string currentCrestId)
        {
            return string.Equals(
                currentCrestId,
                "Cloakless",
                StringComparison.OrdinalIgnoreCase
            );
        }

        internal static bool NeedsOwnershipRepair(string currentCrestId)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsRandomized(ItemType.Crest) &&
                   CrestNames.IsHunterInternalName(currentCrestId) &&
                   !string.Equals(
                       currentCrestId,
                       "Hunter",
                       StringComparison.OrdinalIgnoreCase
                   ) &&
                   !state.receivedItems.Contains("Crest: Hunter");
        }

        private static bool NeedsRandomizerRepair(string currentCrestId)
        {
            return NeedsRepair(currentCrestId) ||
                   NeedsOwnershipRepair(currentCrestId);
        }

        internal static string GetRepairCrest(string startingCrest)
        {
            string internalName = CrestNames.GetInternalCrestName(startingCrest);
            return string.IsNullOrWhiteSpace(internalName) ? "Hunter" : internalName;
        }

        internal static bool IsCrestRuntimeReady()
        {
            return HeroController.instance != null &&
                   PlayerData.instance != null &&
                   GameManager.instance != null &&
                   GameManager.instance.gameMap != null;
        }

        public static IEnumerator EnsureUsableStartingCrest()
        {
            while (PlayerData.instance == null ||
                   SaveState.Instance == null)
            {
                yield return null;
            }

            while (NeedsRepair(PlayerData.instance.CurrentCrestID) &&
                   PlayerData.instance.IsCurrentCrestTemp &&
                   !NakedTrapManager.IsActive &&
                   !SlabCaptureWarpSafety.IsActiveSlabCaptureCrest(
                       PlayerData.instance))
            {
                string previousCrest = PlayerData.instance.PreviousCrestID;
                string repairCrestName =
                    !string.IsNullOrWhiteSpace(previousCrest) &&
                    !NeedsRepair(previousCrest)
                        ? previousCrest
                        : SaveState.Instance.IsRandomized(ItemType.Crest)
                            ? GetRepairCrest(
                                SaveState.Instance.startingCrest
                            )
                            : "Hunter";
                try
                {
                    if (Utils.ForceCrest(repairCrestName) &&
                        !NeedsRepair(
                            PlayerData.instance.CurrentCrestID
                        ))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[RANDOMIZER] Temporary crest repair is waiting: " +
                        ex.Message
                    );
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            while (!IsCrestRuntimeReady() ||
                   SaveState.Instance == null ||
                   string.IsNullOrWhiteSpace(PlayerData.instance.CurrentCrestID))
            {
                yield return null;
            }

            ToolPatches.EnsureReceivedBaseCrestsUnlocked();
            if (ReceivedItemReconciliation.ReconcilePermanentState())
            {
                ToolItemManager.SendEquippedChangedEvent(force: true);
            }
            ToolPatches.RemoveUnreceivedSilkspearFromCrests();
            ToolPatches.RemoveAutomaticCompassFromCrests();
            TravelPatches.ApplyBellwayAccessOption();
            while (!SimpleKeyDoorManager.TrySynchronizeReceivedKeys())
            {
                yield return null;
            }
            while (SaveState.Instance.IsRandomized(ItemType.Tool) &&
                   (
                       !ItemGrants.TrySynchronizeProgressiveDruidsEyeEquips() ||
                       !ItemGrants.TrySynchronizeProgressiveToolEquips()
                   ))
            {
                yield return null;
            }

            MaskShardsPatches.SynchronizeReceivedMaskShards(false);
            while (SaveState.Instance.IsRandomized(ItemType.Relic) &&
                   !CoreLocationPatches.TrySynchronizeReceivedRelics())
            {
                yield return null;
            }
            while (!RuinedToolPatches.TryReconcileReceivedOwnership())
            {
                yield return null;
            }

            // A saved Slab capture is Cloakless
            // and PreviousCrestID is the only exact return-crest snapshot.
            // Starting-save repair must not overwrite either value or strand
            // the matching temporary Rosary/Shell Shard stores.
            if (SlabCaptureWarpSafety.IsActiveSlabCaptureCrest(
                    PlayerData.instance))
            {
                yield break;
            }

            string currentCrestId = PlayerData.instance.CurrentCrestID;
            if (!NeedsRandomizerRepair(currentCrestId))
            {
                yield break;
            }

            string crestName = SaveState.Instance.IsRandomized(ItemType.Crest)
                ? GetRepairCrest(SaveState.Instance.startingCrest)
                : "Hunter";
            while (IsCrestRuntimeReady() &&
                   NeedsRandomizerRepair(PlayerData.instance.CurrentCrestID))
            {
                try
                {
                    if (Utils.ForceCrest(crestName) &&
                        !NeedsRandomizerRepair(
                            PlayerData.instance.CurrentCrestID
                        ))
                    {
                        Debug.Log(
                            "[RANDOMIZER] Repaired unusable or unowned crest with: " +
                            crestName
                        );
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[RANDOMIZER] Starting crest is not ready yet; retrying: " +
                        ex.Message
                    );
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }
        }
    }
}
