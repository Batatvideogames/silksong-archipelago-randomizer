
using System;
using System.Collections;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class StartingCrestFix
    {
        internal static bool IsNakedStart =>
            SaveState.Instance?.startingCrest == "naked" &&
            !CrestNames.HasReceivedAnyCrest(SaveState.Instance.receivedItems);

        internal static void UpdateNakedStart()
        {
            if (!IsNakedStart || !IsCrestRuntimeReady()) return;
            PlayerData data = PlayerData.instance;
            GameManager game = GameManager.instance;
            HeroController hero = HeroController.instance;
            if (!game.IsGameplayScene() || game.IsInSceneTransition ||
                data.HasStoredMemoryState || data.IsAnyCursed ||
                hero.cState == null || hero.cState.dead || hero.cState.hazardDeath) return;
            if (data.CurrentCrestID != "Cloakless")
            {
                ToolCrest crest = GlobalSettings.Gameplay.CloaklessCrest;
                if (crest == null || !ToolPatches.SetRandomizerCrest(crest, false)) return;
            }
            data.IsCurrentCrestTemp = false;
            data.PreviousCrestID = "Cloakless";
        }

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
            if (startingCrest == "naked" && SaveState.Instance != null)
            {
                foreach (string item in SaveState.Instance.receivedItems)
                {
                    if (!item.StartsWith(CrestNames.CrestItemPrefix, StringComparison.Ordinal)) continue;
                    string key = item.Substring(CrestNames.CrestItemPrefix.Length).ToLowerInvariant();
                    string owned = CrestNames.GetInternalCrestName(key);
                    if (owned != null && owned != "Cloakless") return owned;
                }
            }
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
                   !IsNakedStart &&
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

            UpdateNakedStart();
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

            if (IsNakedStart) yield break;

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
