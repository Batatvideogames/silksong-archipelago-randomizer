using SilksongRandomizer.Patches;
using System;
using UnityEngine;
using HarmonyLib;
using GlobalEnums;

namespace SilksongRandomizer
{
    /// <summary>
    /// Owns the temporary Cloakless crest used by Naked Trap. The effect is
    /// kept out of native save data and yields to every native temporary or
    /// cursed crest sequence.
    /// </summary>
    internal static class NakedTrapManager
    {
        internal const float DurationSeconds = 120f;

        private const string CloaklessCrestName = "Cloakless";

        private static bool pending;
        private static bool active;
        private static bool suspendedForSave;
        private static bool internalCrestWrite;
        private static float deadline;
        private static float pendingDuration = DurationSeconds;

        private static string restoreCrestInternalName = string.Empty;
        private static string restorePreviousCrestInternalName = string.Empty;
        private static bool restoreCrestWasTemporary;

        internal static bool IsActive => active && !suspendedForSave;
        internal static bool HasState => active || pending || suspendedForSave;
        internal static bool SuppressesCloakAbilities =>
            IsActive && IsNativeCloaklessEquipped();

        internal static bool CanProcessReceivedItems(HeroController hero)
        {
            GameManager gameManager = GameManager.SilentInstance;
            PlayerData playerData = PlayerData.instance;
            return IsActive &&
                   hero != null &&
                   gameManager != null &&
                   playerData != null &&
                   gameManager.GameState == GameState.PLAYING &&
                   gameManager.IsGameplayScene() &&
                   !gameManager.isPaused &&
                   !gameManager.IsLoadingSceneTransition &&
                   !gameManager.IsInSceneTransition &&
                   !TransitionPoint.IsTransitionBlocked &&
                   !BossSceneController.IsTransitioning &&
                   !playerData.HasStoredMemoryState &&
                   hero.cState != null &&
                   !hero.cState.transitioning &&
                   !hero.cState.dead &&
                   !hero.cState.hazardDeath &&
                   !hero.cState.hazardRespawning &&
                   !hero.controlReqlinquished &&
                   hero.hero_state != ActorStates.no_input &&
                   hero.CanInput();
        }

        internal static void Trigger()
        {
            try
            {
                if (active)
                {
                    // A second copy restarts, rather than stacks, the
                    // two-minute duration.
                    deadline = Time.unscaledTime + DurationSeconds;
                    return;
                }

                LogicAuditCloakManager.Reset();
                pending = true;
                pendingDuration = DurationSeconds;
                TryStartPending();
            }
            catch (Exception ex)
            {
                Warn("Naked Trap is waiting for a safe crest state", ex);
            }
        }

        internal static void Update()
        {
            PlayerData playerData = PlayerData.instance;
            if (HasState && playerData != null && playerData.atBench)
            {
                // A real seated bench state cures both a live effect and one
                // waiting behind a native temporary/cursed crest owner.
                Reset();
                return;
            }

            if (active && !suspendedForSave)
            {
                UpdateActiveEffect();
            }

            if (pending && !active && !suspendedForSave)
            {
                TryStartPending();
            }
        }

        internal static bool TryDeferRandomizerCrestChange(
            ToolCrest crest,
            bool markTemporary)
        {
            if (!IsActive || internalCrestWrite || crest == null)
            {
                return false;
            }

            string crestName = crest.name;
            if (string.IsNullOrEmpty(crestName) ||
                string.Equals(
                    crestName,
                    CloaklessCrestName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            // A native temporary crest must take ownership immediately. The
            // next Update observes it and suspends Naked Trap until that
            // sequence has restored ordinary crest state.
            if (markTemporary)
            {
                PrepareForNativeTemporaryCrest(true);
                return false;
            }

            RememberRequestedRestoreCrest(crestName, false);
            return true;
        }

        internal static void PrepareForNativeTemporaryCrest(
            bool markTemporary)
        {
            if (!markTemporary || !IsActive)
            {
                return;
            }

            float remaining = Math.Max(0f, deadline - Time.unscaledTime);
            if (!TryRestorePhysicalCrest())
            {
                // The native sequence cannot snapshot Cloakless as its
                // return crest. Update will retry after the current frame.
                return;
            }

            active = false;
            pending = remaining > 0f;
            pendingDuration = remaining;
            ClearRestoreSnapshot();
        }

        internal static void PrepareForSave()
        {
            if (!active || suspendedForSave)
            {
                return;
            }

            float remaining = Math.Max(0f, deadline - Time.unscaledTime);
            if (!TryRestorePhysicalCrest())
            {
                ForceRestoreSnapshotFields();
            }

            active = false;
            suspendedForSave = remaining > 0f;
            pending = suspendedForSave;
            pendingDuration = remaining;
            ClearRestoreSnapshot();
        }

        internal static void ResumeAfterSave()
        {
            if (!suspendedForSave)
            {
                return;
            }

            suspendedForSave = false;
            TryStartPending();
        }

        internal static void Reset()
        {
            try
            {
                if (active && !TryRestorePhysicalCrest())
                {
                    ForceRestoreSnapshotFields();
                }
            }
            catch (Exception ex)
            {
                Warn("Naked Trap cleanup failed", ex);
                ForceRestoreSnapshotFields();
            }
            finally
            {
                ClearAllState();
            }
        }

        private static void UpdateActiveEffect()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return;
            }

            bool isCloakless = string.Equals(
                playerData.CurrentCrestID,
                CloaklessCrestName,
                StringComparison.Ordinal
            );
            if (!isCloakless)
            {
                if (playerData.IsAnyCursed ||
                    playerData.IsCurrentCrestTemp ||
                    TrapManager.IsCursedCrestActive)
                {
                    SuspendForNativeCrestOwner();
                    return;
                }

                // A non-temporary crest grant happened while the trap was
                // active. It becomes the intended post-trap crest, but the
                // player remains Cloakless for the rest of the timer.
                RememberRequestedRestoreCrest(
                    playerData.CurrentCrestID,
                    false
                );
            }

            if (Time.unscaledTime >= deadline)
            {
                FinishEffect();
                return;
            }

            if (!isCloakless)
            {
                if (!TryEquipCloakless())
                {
                    SuspendForNativeCrestOwner();
                }
                return;
            }

            // ToolItemManager.SetEquippedCrest clears this bit. The native save
            // guard remains intact even if another mod refreshes the
            // already-equipped Cloakless crest.
            playerData.IsCurrentCrestTemp = true;
        }

        private static void TryStartPending()
        {
            if (!pending || active || suspendedForSave ||
                TrapManager.IsCursedCrestActive)
            {
                return;
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData == null || playerData.IsAnyCursed ||
                playerData.IsCurrentCrestTemp ||
                string.IsNullOrEmpty(playerData.CurrentCrestID) ||
                string.Equals(
                    playerData.CurrentCrestID,
                    CloaklessCrestName,
                    StringComparison.Ordinal))
            {
                return;
            }

            ToolCrest currentCrest = ToolItemManager.GetCrestByName(
                playerData.CurrentCrestID
            );
            ToolCrest cloakless = GetCloaklessCrest();
            if (currentCrest == null || cloakless == null)
            {
                return;
            }

            restoreCrestInternalName = playerData.CurrentCrestID;
            restorePreviousCrestInternalName =
                playerData.PreviousCrestID ?? string.Empty;
            restoreCrestWasTemporary = playerData.IsCurrentCrestTemp;

            if (!TryEquipCloakless())
            {
                ClearRestoreSnapshot();
                return;
            }

            active = true;
            pending = false;
            deadline = Time.unscaledTime + Math.Max(0f, pendingDuration);
            pendingDuration = DurationSeconds;
        }

        private static bool TryEquipCloakless()
        {
            ToolCrest cloakless = GetCloaklessCrest();
            if (cloakless == null)
            {
                return false;
            }

            try
            {
                internalCrestWrite = true;
                bool changed = ToolPatches.SetRandomizerCrest(
                    cloakless,
                    true
                );
                return changed && IsNativeCloaklessEquipped();
            }
            finally
            {
                internalCrestWrite = false;
            }
        }

        private static void RememberRequestedRestoreCrest(
            string crestName,
            bool markTemporary)
        {
            if (string.IsNullOrEmpty(crestName) ||
                string.Equals(
                    crestName,
                    CloaklessCrestName,
                    StringComparison.Ordinal) ||
                string.Equals(
                    crestName,
                    restoreCrestInternalName,
                    StringComparison.Ordinal))
            {
                return;
            }

            restorePreviousCrestInternalName = restoreCrestInternalName;
            restoreCrestInternalName = crestName;
            restoreCrestWasTemporary = markTemporary;
        }

        private static void FinishEffect()
        {
            if (!TryRestorePhysicalCrest())
            {
                // The snapshot remains available for a retry on the next
                // frame, avoiding a saveable Cloakless crest.
                return;
            }

            ClearAllState();
        }

        private static bool TryRestorePhysicalCrest()
        {
            if (string.IsNullOrEmpty(restoreCrestInternalName))
            {
                return true;
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return false;
            }

            if (!IsNativeCloaklessEquipped())
            {
                // A native temporary or cursed sequence owns the crest now.
                // Trap cleanup does not overwrite it.
                return !playerData.IsCurrentCrestTemp &&
                       !playerData.IsAnyCursed;
            }

            ToolCrest restoreCrest = ToolItemManager.GetCrestByName(
                restoreCrestInternalName
            );
            if (restoreCrest == null)
            {
                return false;
            }

            try
            {
                internalCrestWrite = true;
                if (!ToolPatches.SetRandomizerCrest(
                        restoreCrest,
                        restoreCrestWasTemporary))
                {
                    return false;
                }
            }
            finally
            {
                internalCrestWrite = false;
            }

            playerData.PreviousCrestID =
                restorePreviousCrestInternalName;
            playerData.IsCurrentCrestTemp = restoreCrestWasTemporary;
            return string.Equals(
                playerData.CurrentCrestID,
                restoreCrestInternalName,
                StringComparison.Ordinal
            );
        }

        private static void SuspendForNativeCrestOwner()
        {
            float remaining = Math.Max(0f, deadline - Time.unscaledTime);
            active = false;
            pending = remaining > 0f;
            pendingDuration = remaining;
            ClearRestoreSnapshot();
        }

        private static void ForceRestoreSnapshotFields()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null ||
                string.IsNullOrEmpty(restoreCrestInternalName) ||
                !IsNativeCloaklessEquipped())
            {
                return;
            }

            try
            {
                internalCrestWrite = true;
                ToolPatches.PrepareHeroForCrestChange();
                playerData.CurrentCrestID = restoreCrestInternalName;
                playerData.PreviousCrestID =
                    restorePreviousCrestInternalName;
                playerData.IsCurrentCrestTemp = restoreCrestWasTemporary;
                ToolItemManager.RefreshEquippedState();
                ToolItemManager.SendEquippedChangedEvent(true);
                ToolPatches.ResetHeroInputAfterCrestChange();
            }
            catch (Exception ex)
            {
                Warn("Naked Trap save fallback could not refresh the crest", ex);
            }
            finally
            {
                internalCrestWrite = false;
            }
        }

        private static ToolCrest GetCloaklessCrest()
        {
            ToolCrest cloakless = GlobalSettings.Gameplay.CloaklessCrest;
            return cloakless != null
                ? cloakless
                : ToolItemManager.GetCrestByName(CloaklessCrestName);
        }

        private static bool IsNativeCloaklessEquipped()
        {
            PlayerData playerData = PlayerData.instance;
            return playerData != null && string.Equals(
                playerData.CurrentCrestID,
                CloaklessCrestName,
                StringComparison.Ordinal
            );
        }

        private static void ClearAllState()
        {
            pending = false;
            active = false;
            suspendedForSave = false;
            internalCrestWrite = false;
            deadline = 0f;
            pendingDuration = DurationSeconds;
            ClearRestoreSnapshot();
        }

        private static void ClearRestoreSnapshot()
        {
            restoreCrestInternalName = string.Empty;
            restorePreviousCrestInternalName = string.Empty;
            restoreCrestWasTemporary = false;
        }

        private static void Warn(string message, Exception exception = null)
        {
            string detail = exception == null
                ? message
                : message + ": " + exception.Message;
            RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] " + detail);
        }
    }

    [HarmonyPatch(
        typeof(ToolItemManager),
        "AutoEquip",
        new[] { typeof(ToolCrest), typeof(bool), typeof(bool) }
    )]
    internal static class NakedTrapNativeTemporaryCrestPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(bool markTemp)
        {
            // AutoEquip snapshots CurrentCrestID into PreviousCrestID. Give
            // it the real pre-trap crest, never the temporary Cloakless one.
            NakedTrapManager.PrepareForNativeTemporaryCrest(markTemp);
        }
    }
}
