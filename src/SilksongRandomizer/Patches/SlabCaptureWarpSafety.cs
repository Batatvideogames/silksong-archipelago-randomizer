using HarmonyLib;
using System;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Restores the equipment and currency state confiscated by The Slab
    /// when F4 or any bench skips the prison's native restoration fight.
    /// </summary>
    internal static class SlabCaptureWarpSafety
    {
        private const string CloaklessCrestName = "Cloakless";
        private const string SlabCaptureRespawnScene = "Slab_03";
        private const string SlabScenePrefix = "Slab_";
        private const int NativeRestoredCloakOdour = 100;

        private static bool benchRestoreFailureReported;

        internal static void Update()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null || !playerData.atBench)
            {
                benchRestoreFailureReported = false;
                return;
            }

            // The shared native bench FSM sets atBench only after the sit
            // interaction reaches its real At Bench/Resting state. By then it
            // has already replaced respawnScene with this bench's scene, so a
            // bench outside The Slab cannot retain the usual Slab context.
            // Accept that seated lifecycle as an alternate context only for
            // this recovery path. Other callers still require the Slab state.
            // Sitting at any real bench already cures Naked Trap. Do this
            // before testing the native capture so a same-frame save cannot
            // preserve the AP Cloakless crest either.
            NakedTrapManager.Reset();

            if (TryRestoreCapturedState(
                    acceptAnySeatedBench: true,
                    out string error))
            {
                benchRestoreFailureReported = false;
                return;
            }

            if (!benchRestoreFailureReported)
            {
                benchRestoreFailureReported = true;
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] " + error
                );
            }
        }

        internal static void PrepareForSave()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null || !playerData.atBench)
            {
                return;
            }

            NakedTrapManager.Reset();
            if (!TryRestoreCapturedState(
                    acceptAnySeatedBench: true,
                    out string error))
            {
                // A bench save cannot serialize the Slab's non-temporary
                // Cloakless crest. GameManager.FixUpSaveState uses the same
                // direct PlayerData fallback when crest assets are not ready.
                if (!ForceRestoreCapturedStateForSave(
                        acceptAnySeatedBench: true,
                        out string fallback))
                {
                    throw new InvalidOperationException(
                        error + " " + fallback
                    );
                }
            }
        }

        internal static bool TryRestoreBeforeRecoveryWarp(
            out string error)
        {
            error = string.Empty;

            // A Naked Trap received while the native Slab capture owns
            // Cloakless remains pending. F4 is an explicit cure for the Slab
            // naked state, so consume that pending trap before the destination
            // scene can start it again.
            if (IsActiveSlabCaptureCrest(PlayerData.instance))
            {
                NakedTrapManager.Reset();
            }

            return TryRestoreCapturedState(
                acceptAnySeatedBench: false,
                out error
            );
        }

        private static bool TryRestoreCapturedState(
            bool acceptAnySeatedBench,
            out string error)
        {
            error = string.Empty;
            PlayerData playerData = PlayerData.instance;
            if (!IsRestorableSlabCaptureCrest(
                    playerData,
                    acceptAnySeatedBench))
            {
                return true;
            }

            string restoreCrestName = playerData.PreviousCrestID;
            ToolCrest restoreCrest = ToolItemManager.GetCrestByName(
                restoreCrestName
            );
            if (restoreCrest == null)
            {
                error =
                    "The Slab recovery could not resolve the crest it " +
                    "confiscated. Finish the native recovery fight.";
                return false;
            }

            if (!ToolPatches.SetRandomizerCrest(restoreCrest, false) ||
                !string.Equals(
                    playerData.CurrentCrestID,
                    restoreCrestName,
                    StringComparison.Ordinal))
            {
                error =
                    "The Slab recovery could not safely restore the crest " +
                    "taken by The Slab.";
                return false;
            }

            // Slab_16/Start Inspect/Control/Attack End performs these native
            // state changes after re-equipping a usable crest. The AP-safe
            // direct crest refresh above preserves the player's crest and its
            // saved tool layout.
            playerData.PreviousCrestID = string.Empty;
            playerData.IsCurrentCrestTemp = false;
            playerData.cloakOdour_slabFly = NativeRestoredCloakOdour;
            CurrencyManager.RestoreTempStoredCurrency();

            // The capture/battle story flags remain untouched. Returning to
            // The Slab can therefore resume the unfinished encounter.
            return true;
        }

        private static bool ForceRestoreCapturedStateForSave(
            bool acceptAnySeatedBench,
            out string error)
        {
            error = string.Empty;
            PlayerData playerData = PlayerData.instance;
            if (!IsRestorableSlabCaptureCrest(
                    playerData,
                    acceptAnySeatedBench))
            {
                return true;
            }

            string restoreCrestName = playerData.PreviousCrestID;
            if (string.IsNullOrEmpty(restoreCrestName) ||
                string.Equals(
                    restoreCrestName,
                    CloaklessCrestName,
                    StringComparison.Ordinal))
            {
                error =
                    "The Slab bench save had no valid captured crest ID.";
                return false;
            }

            try
            {
                ToolPatches.PrepareHeroForCrestChange();
                playerData.CurrentCrestID = restoreCrestName;
                playerData.PreviousCrestID = string.Empty;
                playerData.IsCurrentCrestTemp = false;
                playerData.cloakOdour_slabFly = NativeRestoredCloakOdour;
                CurrencyManager.RestoreTempStoredCurrency();
                ToolItemManager.RefreshEquippedState();
                ToolItemManager.SendEquippedChangedEvent(true);
                ToolPatches.ResetHeroInputAfterCrestChange();
                return true;
            }
            catch (Exception ex)
            {
                error =
                    "The Slab bench-save fallback failed: " + ex.Message;
                return false;
            }
        }

        internal static bool IsActiveSlabCaptureCrest(
            PlayerData playerData)
        {
            return HasSlabCaptureCrestSignature(playerData) &&
                   IsSlabCaptureContext(playerData);
        }

        private static bool IsRestorableSlabCaptureCrest(
            PlayerData playerData,
            bool acceptAnySeatedBench)
        {
            return HasSlabCaptureCrestSignature(playerData) &&
                   (IsSlabCaptureContext(playerData) ||
                    (acceptAnySeatedBench && playerData.atBench));
        }

        private static bool HasSlabCaptureCrestSignature(
            PlayerData playerData)
        {
            return playerData != null &&
                   string.Equals(
                       playerData.CurrentCrestID,
                       CloaklessCrestName,
                       StringComparison.Ordinal) &&
                   !playerData.IsCurrentCrestTemp &&
                   !string.IsNullOrEmpty(playerData.PreviousCrestID) &&
                   !string.Equals(
                       playerData.PreviousCrestID,
                       CloaklessCrestName,
                   StringComparison.Ordinal) &&
                   !TrapManager.IsCursedCrestActive &&
                   !NakedTrapManager.IsActive;
        }

        private static bool IsSlabCaptureContext(PlayerData playerData)
        {
            return string.Equals(
                       playerData.respawnScene,
                       SlabCaptureRespawnScene,
                       StringComparison.Ordinal) ||
                   IsCurrentSceneInSlab();
        }

        private static bool IsCurrentSceneInSlab()
        {
            GameManager gameManager = GameManager.SilentInstance;
            string sceneName = gameManager?.GetSceneNameString();
            return !string.IsNullOrEmpty(sceneName) &&
                   sceneName.StartsWith(
                       SlabScenePrefix,
                       StringComparison.OrdinalIgnoreCase
                   );
        }
    }

    [HarmonyPatch(typeof(HeroSlabCapture), "ApplyCaptured")]
    internal static class SlabCaptureTrapOwnershipPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix()
        {
            // HeroSlabCapture snapshots CurrentCrestID into PreviousCrestID.
            // End temporary randomizer crest ownership first so the saved
            // return crest is always the player's genuine pre-capture crest.
            TrapManager.PrepareForNativeSlabCapture();
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.FixUpSaveState))]
    internal static class SlabCaptureBeforeNativeSaveFixupPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix()
        {
            // FixUpSaveState can replace a completed capture's Cloakless crest
            // before CreateSaveGameData runs but it does not restore TempGeoStore
            // or TempShellShardStore. The complete capture state returns while
            // PreviousCrestID still identifies it.
            SlabCaptureWarpSafety.PrepareForSave();
        }
    }
}
