using GlobalEnums;
using SilksongRandomizer.Patches;
using System;
using UnityEngine;

namespace SilksongRandomizer
{
    internal enum LogicAuditCloakStage
    {
        Cloakless = 0,
        Normal = 1,
        Drifters = 2,
        Faydown = 3,
    }

    /// <summary>
    /// Transient movement controls for logic-audit saves. Ownership remains in
    /// SaveState. This class only changes the effective runtime capabilities.
    /// </summary>
    internal static class LogicAuditCloakManager
    {
        private const string CloaklessCrestName = "Cloakless";

        private static bool overrideActive;
        private static LogicAuditCloakStage currentStage;
        private static bool hasCrestSnapshot;
        private static string snapshotCurrentCrest = string.Empty;
        private static string snapshotPreviousCrest = string.Empty;
        private static bool snapshotWasTemporary;
        private static bool resumeCloaklessWhenSafe;

        internal static bool IsOverrideActive =>
            overrideActive &&
            SaveState.Instance != null &&
            SaveState.Instance.logicAuditMode;

        internal static LogicAuditCloakStage GetNextStage(
            LogicAuditCloakStage stage
        )
        {
            switch (stage)
            {
                case LogicAuditCloakStage.Cloakless:
                    return LogicAuditCloakStage.Normal;
                case LogicAuditCloakStage.Normal:
                    return LogicAuditCloakStage.Drifters;
                case LogicAuditCloakStage.Drifters:
                    return LogicAuditCloakStage.Faydown;
                default:
                    return LogicAuditCloakStage.Cloakless;
            }
        }

        internal static bool AllowsBrolly(LogicAuditCloakStage stage)
        {
            return stage == LogicAuditCloakStage.Drifters ||
                   stage == LogicAuditCloakStage.Faydown;
        }

        internal static bool AllowsDoubleJump(LogicAuditCloakStage stage)
        {
            return stage == LogicAuditCloakStage.Faydown;
        }

        internal static bool ResolveBrollyOwnership(bool owned)
        {
            return IsOverrideActive && !resumeCloaklessWhenSafe
                ? AllowsBrolly(currentStage)
                : owned;
        }

        internal static bool ResolveDoubleJumpOwnership(bool owned)
        {
            return IsOverrideActive && !resumeCloaklessWhenSafe
                ? AllowsDoubleJump(currentStage)
                : owned;
        }

        internal static string GetDisplayName()
        {
            LogicAuditCloakStage stage = IsOverrideActive
                ? currentStage
                : InferOwnedStage();
            switch (stage)
            {
                case LogicAuditCloakStage.Cloakless:
                    return "Cloakless";
                case LogicAuditCloakStage.Normal:
                    return "Normal";
                case LogicAuditCloakStage.Drifters:
                    return "Drifter's";
                default:
                    return "Faydown";
            }
        }

        internal static string GetOverlayText()
        {
            return "F8 Cloak: " + GetDisplayName();
        }

        internal static bool CanCycle(
            bool logicAuditMode,
            bool connectionGuiOpen,
            bool gameplayReady
        )
        {
            return logicAuditMode && !connectionGuiOpen && gameplayReady;
        }

        internal static bool TryCycle()
        {
            SaveState state = SaveState.Instance;
            if (state == null || !state.logicAuditMode)
            {
                return false;
            }

            UpdateIntegrity();
            if (!CanUseHotkey(out string reason))
            {
                Warn("F8 cloak cycle is unavailable: " + reason);
                return false;
            }

            LogicAuditCloakStage previousStage = IsOverrideActive
                ? currentStage
                : InferOwnedStage();
            LogicAuditCloakStage nextStage = GetNextStage(previousStage);

            if (nextStage == LogicAuditCloakStage.Cloakless)
            {
                if (!TryEnterCloakless())
                {
                    return false;
                }
            }
            else
            {
                if (previousStage == LogicAuditCloakStage.Cloakless &&
                    hasCrestSnapshot &&
                    !TryRestoreCrest(false))
                {
                    Warn(
                        "F8 cloak cycle is waiting for the previous crest " +
                        "to become available."
                    );
                    return false;
                }

                overrideActive = true;
                currentStage = nextStage;
                resumeCloaklessWhenSafe = false;
            }

            RandomizerPlugin.Log?.LogMessage(
                "[RANDOMIZER] Logic Audit cloak: " + GetDisplayName()
            );
            return true;
        }

        internal static void UpdateIntegrity()
        {
            if (!overrideActive)
            {
                return;
            }

            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null || !state.logicAuditMode || playerData == null)
            {
                Reset();
                return;
            }

            bool ownsTemporaryCloakless =
                currentStage == LogicAuditCloakStage.Cloakless &&
                hasCrestSnapshot;
            if (TrapManager.IsCursedCrestActive || playerData.IsAnyCursed ||
                (playerData.IsCurrentCrestTemp &&
                 !ownsTemporaryCloakless))
            {
                // A trap or story sequence has taken ownership of the crest.
                // Drop only the audit ability override and leave that native
                // state untouched.
                ClearTransientFields();
                return;
            }

            if (resumeCloaklessWhenSafe)
            {
                if (CanUseHotkey(out _) &&
                    TryResumeCloakless())
                {
                    resumeCloaklessWhenSafe = false;
                }
                return;
            }

            bool isNativeCloakless = string.Equals(
                playerData.CurrentCrestID,
                CloaklessCrestName,
                StringComparison.Ordinal
            );
            if (currentStage == LogicAuditCloakStage.Cloakless)
            {
                if (!hasCrestSnapshot || !isNativeCloakless)
                {
                    // A story sequence has taken ownership, so audit abilities
                    // do not replace its crest.
                    ClearTransientFields();
                }
            }
            else if (isNativeCloakless)
            {
                ClearTransientFields();
            }
        }

        internal static void PrepareForSave()
        {
            try
            {
                if (!overrideActive ||
                    currentStage != LogicAuditCloakStage.Cloakless ||
                    !hasCrestSnapshot)
                {
                    return;
                }

                // CurrentCrestID is native save data. The exact pre-audit crest
                // returns before GameManager constructs the save, followed by
                // the test cloak on the next safe gameplay frame.
                if (TryRestoreCrest(true))
                {
                    if (overrideActive && hasCrestSnapshot)
                    {
                        resumeCloaklessWhenSafe = true;
                    }
                    return;
                }

                ForceRestoreSnapshotFields();
                ClearTransientFields();
            }
            catch (Exception ex)
            {
                // A Harmony save prefix cannot abort native saving.
                Warn("F8 cloak save preparation failed: " + ex.Message);
                ForceRestoreSnapshotFields();
                ClearTransientFields();
            }
        }

        internal static void ResumeAfterSave()
        {
            // PrepareForSave leaves Cloakless suspended with its exact crest
            // snapshot. Reapply now when safe or let UpdateIntegrity retry.
            UpdateIntegrity();
        }

        internal static void Reset()
        {
            try
            {
                if (hasCrestSnapshot && !TryRestoreCrest(false))
                {
                    ForceRestoreSnapshotFields();
                }
            }
            catch (Exception ex)
            {
                Warn("F8 cloak cleanup failed: " + ex.Message);
                ForceRestoreSnapshotFields();
            }
            finally
            {
                ClearTransientFields();
            }
        }

        private static LogicAuditCloakStage InferOwnedStage()
        {
            PlayerData playerData = PlayerData.instance;
            bool isCloakless = playerData != null &&
                string.Equals(
                    playerData.CurrentCrestID,
                    CloaklessCrestName,
                    StringComparison.Ordinal);

            SaveState state = SaveState.Instance;
            bool randomizedSkills =
                state != null && state.IsRandomized(ItemType.Skill);
            bool hasDoubleJump = randomizedSkills
                ? state.canDoubleJump
                : playerData != null && playerData.hasDoubleJump;
            bool hasBrolly = randomizedSkills
                ? state.canBrolly
                : playerData != null && playerData.hasBrolly;

            return InferOwnedStage(isCloakless, hasBrolly, hasDoubleJump);
        }

        internal static LogicAuditCloakStage InferOwnedStage(
            bool isCloakless,
            bool hasBrolly,
            bool hasDoubleJump)
        {
            if (isCloakless)
            {
                return LogicAuditCloakStage.Cloakless;
            }
            if (hasDoubleJump)
            {
                return LogicAuditCloakStage.Faydown;
            }
            return hasBrolly
                ? LogicAuditCloakStage.Drifters
                : LogicAuditCloakStage.Normal;
        }

        private static bool CanUseHotkey(out string reason)
        {
            GameManager gameManager = GameManager.SilentInstance;
            HeroController hero = HeroController.instance;
            PlayerData playerData = PlayerData.instance;
            if (gameManager == null || hero == null || playerData == null)
            {
                reason = "load into gameplay first";
                return false;
            }

            bool gameplayReady =
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
            if (!CanCycle(
                    SaveState.Instance != null &&
                    SaveState.Instance.logicAuditMode,
                    false,
                    gameplayReady))
            {
                reason = "finish the current menu, transition, or cutscene";
                return false;
            }
            if (TrapManager.IsCursedCrestActive || playerData.IsAnyCursed)
            {
                reason = "a cursed crest currently owns the crest state";
                return false;
            }
            if (playerData.IsCurrentCrestTemp &&
                !(hasCrestSnapshot &&
                  currentStage == LogicAuditCloakStage.Cloakless))
            {
                reason = "a native temporary crest sequence is active";
                return false;
            }
            if (!overrideActive &&
                string.Equals(
                    playerData.CurrentCrestID,
                    CloaklessCrestName,
                    StringComparison.Ordinal))
            {
                reason = "a native Cloakless story sequence is active";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool TryEnterCloakless()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null || playerData.IsCurrentCrestTemp ||
                playerData.IsAnyCursed || TrapManager.IsCursedCrestActive)
            {
                return false;
            }

            try
            {
                ToolCrest currentCrest = ToolItemManager.GetCrestByName(
                    playerData.CurrentCrestID
                );
                ToolCrest cloakless = ToolItemManager.GetCrestByName(
                    CloaklessCrestName
                );
                if (currentCrest == null || cloakless == null)
                {
                    Warn(
                        "F8 cloak cycle could not resolve native crest assets."
                    );
                    return false;
                }

                snapshotCurrentCrest = playerData.CurrentCrestID;
                snapshotPreviousCrest = playerData.PreviousCrestID ?? string.Empty;
                snapshotWasTemporary = playerData.IsCurrentCrestTemp;
                hasCrestSnapshot = true;

                if (!ToolPatches.SetRandomizerCrest(cloakless, true) ||
                    !string.Equals(
                        playerData.CurrentCrestID,
                        CloaklessCrestName,
                        StringComparison.Ordinal))
                {
                    ForceRestoreSnapshotFields();
                    ClearTransientFields();
                    Warn(
                        "F8 cloak cycle could not enter the native " +
                        "Cloakless state."
                    );
                    return false;
                }

                overrideActive = true;
                currentStage = LogicAuditCloakStage.Cloakless;
                resumeCloaklessWhenSafe = false;
                return true;
            }
            catch (Exception ex)
            {
                Warn("F8 cloak cycle could not enter Cloakless: " + ex.Message);
                ForceRestoreSnapshotFields();
                ClearTransientFields();
                return false;
            }
        }

        private static bool TryResumeCloakless()
        {
            PlayerData playerData = PlayerData.instance;
            if (!hasCrestSnapshot || playerData == null)
            {
                ClearTransientFields();
                return false;
            }
            if (playerData.IsAnyCursed || TrapManager.IsCursedCrestActive)
            {
                return false;
            }
            if (string.Equals(
                    playerData.CurrentCrestID,
                    CloaklessCrestName,
                    StringComparison.Ordinal))
            {
                return true;
            }
            if (!string.Equals(
                    playerData.CurrentCrestID,
                    snapshotCurrentCrest,
                    StringComparison.Ordinal) ||
                playerData.IsCurrentCrestTemp)
            {
                ClearTransientFields();
                return false;
            }

            ToolCrest cloakless = ToolItemManager.GetCrestByName(
                CloaklessCrestName
            );
            if (cloakless == null)
            {
                return false;
            }
            try
            {
                if (!ToolPatches.SetRandomizerCrest(cloakless, true) ||
                    !string.Equals(
                        playerData.CurrentCrestID,
                        CloaklessCrestName,
                        StringComparison.Ordinal))
                {
                    ForceRestoreSnapshotFields();
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Warn("F8 cloak resume failed: " + ex.Message);
                ForceRestoreSnapshotFields();
                return false;
            }
        }

        private static bool TryRestoreCrest(bool preserveSnapshot)
        {
            if (!hasCrestSnapshot)
            {
                return true;
            }

            try
            {
                PlayerData playerData = PlayerData.instance;
                if (playerData == null)
                {
                    return false;
                }
                if (!string.Equals(
                        playerData.CurrentCrestID,
                        CloaklessCrestName,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        playerData.CurrentCrestID,
                        snapshotCurrentCrest,
                        StringComparison.Ordinal))
                {
                    // A story or reward changed the crest. Its new state wins.
                    ClearTransientFields();
                    return false;
                }

                ToolCrest previousCrest = ToolItemManager.GetCrestByName(
                    snapshotCurrentCrest
                );
                if (previousCrest == null ||
                    !ToolPatches.SetRandomizerCrest(
                        previousCrest,
                        snapshotWasTemporary))
                {
                    return false;
                }

                playerData.PreviousCrestID = snapshotPreviousCrest;
                playerData.IsCurrentCrestTemp = snapshotWasTemporary;
                ToolItemManager.RefreshEquippedState();
                if (!preserveSnapshot)
                {
                    ClearCrestSnapshot();
                }
                return true;
            }
            catch (Exception ex)
            {
                Warn("F8 cloak restoration failed: " + ex.Message);
                return false;
            }
        }

        private static bool ForceRestoreSnapshotFields()
        {
            PlayerData playerData = PlayerData.instance;
            if (!hasCrestSnapshot || playerData == null)
            {
                return false;
            }

            try
            {
                if (!string.Equals(
                        playerData.CurrentCrestID,
                        snapshotCurrentCrest,
                        StringComparison.Ordinal))
                {
                    ToolPatches.PrepareHeroForCrestChange();
                }
                playerData.CurrentCrestID = snapshotCurrentCrest;
                playerData.PreviousCrestID = snapshotPreviousCrest;
                playerData.IsCurrentCrestTemp = snapshotWasTemporary;
                ToolItemManager.RefreshEquippedState();
                ToolItemManager.SendEquippedChangedEvent(true);
                ToolPatches.ResetHeroInputAfterCrestChange();
                return string.Equals(
                    playerData.CurrentCrestID,
                    snapshotCurrentCrest,
                    StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                Warn("F8 cloak save repair restored fields but not visuals: " +
                     ex.Message);
                return string.Equals(
                    playerData.CurrentCrestID,
                    snapshotCurrentCrest,
                    StringComparison.Ordinal);
            }
        }

        private static void ClearTransientFields()
        {
            overrideActive = false;
            currentStage = LogicAuditCloakStage.Normal;
            resumeCloaklessWhenSafe = false;
            ClearCrestSnapshot();
        }

        private static void ClearCrestSnapshot()
        {
            hasCrestSnapshot = false;
            snapshotCurrentCrest = string.Empty;
            snapshotPreviousCrest = string.Empty;
            snapshotWasTemporary = false;
        }

        private static void Warn(string message)
        {
            RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] " + message);
        }
    }
}
