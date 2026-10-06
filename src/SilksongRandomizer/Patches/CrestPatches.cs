using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Collections;

namespace SilksongRandomizer.Patches
{
    public class CrestPatches
    {
        [HarmonyPatch(typeof(InventoryToolCrestList), "CanChangeCrests", new Type[0])]
        internal static class InventoryToolCrestList_CanChangeCrests_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (!TrapManager.IsCursedCrestActive &&
                    (state == null || !state.IsRandomized(ItemType.Crest)))
                {
                    return true;
                }

                __result = !TrapManager.IsCursedCrestActive;
                return false;
            }
        }

        [HarmonyPatch(typeof(InventoryPaneList), nameof(InventoryPaneList.SetNextOpen))]
        private static class PreservePaneDuringRandomizerUnlock
        {
            [HarmonyPrefix]
            private static bool Prefix(string paneName) =>
                !ToolPatches.canCrestBeUnlockedByRandomizer ||
                !string.Equals(paneName, "Tools", StringComparison.Ordinal);
        }

        [HarmonyPatch(typeof(ToolCrest), "Unlock", new Type[0])]
        internal static class ToolCrest_Unlock_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(ToolCrest __instance)
            {
                if (__instance == null)
                {
                    return false;
                }

                if (SaveState.Instance == null ||
                    !SaveState.Instance.IsRandomized(ItemType.Crest) ||
                    ToolPatches.canCrestBeUnlockedByRandomizer)
                {
                    return true;
                }

                string toolName = "<null>";

                if (!string.IsNullOrEmpty(__instance.name))
                {
                    toolName = __instance.name;
                }

                Debug.Log("[RANDOMIZER] Tried to Unlock: " + toolName);

                if (CrestNames.IsHunterInternalName(toolName) &&
                    !__instance.IsBaseVersion)
                {
                    return !SaveState.Instance.IsRandomized(ItemType.Eva) &&
                        SaveState.Instance.receivedItems.Contains("Crest: Hunter");
                }

                SaveState.Instance.CheckLocation(
                    CrestNames.GetLocationNameFromInternal(toolName)
                );
                return false;
            }
        }
    }

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

            // Slab capture needs PreviousCrestID and the confiscated currency left alone.
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

    [HarmonyPatch(typeof(HeroAnimationController), nameof(HeroAnimationController.GetClip))]
    internal static class CloaklessAnimationPatches
    {
        private static tk2dSpriteAnimationClip cachedOriginal;
        private static tk2dSpriteAnimationClip cachedStandard;
        private static tk2dSpriteAnimationClip cachedFixed;

        private static bool IsWakeAnimation(string name) =>
            name == "Prostrate Rise" || name == "Prostrate Rise Slow";

        [HarmonyPostfix]
        private static void Postfix(HeroAnimationController __instance, string clipName,
            ref tk2dSpriteAnimationClip __result)
        {
            if (!IsWakeAnimation(clipName) || SaveState.Instance?.IsRoomBound != true ||
                PlayerData.instance?.CurrentCrestID != "Cloakless" ||
                __instance.animator == null || __result == null) return;

            var standard = __instance.animator.GetClipByName(clipName);
            if (ReferenceEquals(__result, cachedOriginal) && ReferenceEquals(standard, cachedStandard))
            {
                __result = cachedFixed;
                return;
            }
            cachedOriginal = __result;
            cachedStandard = standard;
            cachedFixed = RestoreWakeEvents(__result, standard);
            __result = cachedFixed;
        }

        internal static tk2dSpriteAnimationClip RestoreWakeEvents(
            tk2dSpriteAnimationClip original, tk2dSpriteAnimationClip standard)
        {
            if (original == null || standard == null || ReferenceEquals(original, standard) ||
                !IsWakeAnimation(original.name) || standard.name != original.name ||
                original.wrapMode != tk2dSpriteAnimationClip.WrapMode.Once ||
                standard.wrapMode != tk2dSpriteAnimationClip.WrapMode.Once ||
                original.frames == null || original.frames.Length < 3 ||
                standard.frames == null || standard.frames.Length < 3) return original;

            foreach (var frame in original.frames)
                if (frame == null || frame.triggerEvent) return original;
            int eventCount = 0;
            foreach (var frame in standard.frames)
            {
                if (frame == null) return original;
                if (frame.triggerEvent) eventCount++;
            }
            if (eventCount != 2) return original;

            var result = new tk2dSpriteAnimationClip(original);
            int previousFrame = -1;
            for (int i = 0; i < standard.frames.Length; i++)
            {
                var source = standard.frames[i];
                if (!source.triggerEvent) continue;
                int index = (int)Math.Round((double)i * (result.frames.Length - 1) /
                    (standard.frames.Length - 1));
                if (index <= previousFrame) return original;
                var target = result.frames[index];
                target.triggerEvent = true;
                target.eventInfo = source.eventInfo;
                target.eventInt = source.eventInt;
                target.eventFloat = source.eventFloat;
                previousFrame = index;
            }
            return result;
        }
    }
}
