using SilksongRandomizer.Patches;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SilksongRandomizer
{
    /// <summary>
    /// Owns short-lived trap effects. Every public trigger is
    /// no-throw so a cosmetic trap can never block Archipelago's ordered item
    /// queue.
    /// </summary>
    internal static class TrapManager
    {
        internal const int RosarySpillAmount = 60;
        internal const float DarknessDurationSeconds = 20f;
        internal const float CursedCrestDurationSeconds = 120f;

        // Level 2 is the game's full native darkness vignette. Level 0 is
        // ordinary lighting and level 1 is the lighter darkness variant.
        private const int DarknessTrapLevel = 2;

        // TakeDamage ultimately enters this private native coroutine after
        // health/accounting has already happened. Calling the recoil stage
        // directly gives the trap Hornet's real hit-freeze, animation, control
        // loss, knockback and recovery without touching health or firing the
        // wider damage pipeline.
        private static readonly MethodInfo StartRecoilMethod =
            typeof(HeroController).GetMethod(
                "StartRecoil",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(GlobalEnums.CollisionSide),
                    typeof(int)
                },
                null
            );
        private static readonly FieldInfo RecoilRoutineField =
            typeof(HeroController).GetField(
                "recoilRoutine",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        private static int pendingStaggerCount;

        private static bool darknessActive;
        private static bool darknessApplied;
        private static bool darknessWriteInProgress;
        private static float darknessDeadline;
        private static int darknessRestoreLevel;

        private static float cursedCrestDeadline;
        private static string cursedCrestInternalName = string.Empty;
        private static string previousCrestInternalName = string.Empty;
        private static string previousPreviousCrestInternalName = string.Empty;
        private static bool previousCrestWasTemporary;
        private static bool cursedCrestEquipmentRefreshPending;
        private static bool cursedCrestSilkRefreshPending;
        private static bool cursedCrestSpoolRefreshPending;
        private static bool pendingCursedCrest;

        private static bool muckmaggotActive;
        private static bool muckmaggotApplied;
        private static bool muckmaggotWriteInProgress;

        internal static bool IsCursedCrestActive { get; private set; }
        private static bool HasCursedCrestState =>
            IsCursedCrestActive ||
            !string.IsNullOrEmpty(previousCrestInternalName);

        internal static void TriggerStagger()
        {
            if (pendingStaggerCount < int.MaxValue)
            {
                pendingStaggerCount++;
            }

            TryApplyPendingStagger();
        }

        private static void TryApplyPendingStagger()
        {
            if (pendingStaggerCount <= 0)
            {
                return;
            }

            try
            {
                GameManager gameManager = GameManager.instance;
                HeroController hero = HeroController.instance;
                if (gameManager == null ||
                    hero == null ||
                    !hero.CanCustomRecoil())
                {
                    return;
                }

                if (TryReleaseSprintForStagger(gameManager, hero) ||
                    !hero.CanTakeControl())
                {
                    return;
                }

                if (StartRecoilMethod == null || RecoilRoutineField == null)
                {
                    pendingStaggerCount--;
                    Warn(
                        "Stagger Trap skipped because Hornet's native " +
                        "damage-recoil entry point is unavailable"
                    );
                    return;
                }

                GlobalEnums.CollisionSide impactSide =
                    HeroControllerAbilityPatchUtil.CState(
                        hero,
                        "facingRight"
                    )
                        ? GlobalEnums.CollisionSide.right
                        : GlobalEnums.CollisionSide.left;
                IEnumerator recoil = StartRecoilMethod.Invoke(
                    hero,
                    new object[] { impactSide, 1 }
                ) as IEnumerator;
                if (recoil == null)
                {
                    pendingStaggerCount--;
                    Warn(
                        "Stagger Trap skipped because Hornet's native " +
                        "damage-recoil routine could not be created"
                    );
                    return;
                }

                // The native routine handle remains available so an ordinary scene,
                // control or damage cancellation can end the trap recoil by
                // the same path as a real hit.
                Coroutine routine = hero.StartCoroutine(recoil);
                RecoilRoutineField.SetValue(hero, routine);
                CameraShake.Shake(CameraShakeCues.BigRumble);
                pendingStaggerCount--;
            }
            catch (Exception ex)
            {
                // A broken effect is discarded so it cannot block later AP
                // items. A temporarily unsafe hero state returns above and is
                // retried by Update() instead.
                pendingStaggerCount--;
                Warn("Stagger Trap could not be applied", ex);
            }
        }

        private static bool TryReleaseSprintForStagger(
            GameManager gameManager,
            HeroController hero)
        {
            if (hero.cState == null || hero.sprintFSM == null)
            {
                return false;
            }

            var nativeSprintFlag =
                hero.sprintFSM.FsmVariables?.FindFsmBool("Is Sprinting");
            if (!hero.cState.isSprinting &&
                (nativeSprintFlag == null || !nativeSprintFlag.Value))
            {
                return false;
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData == null ||
                gameManager.GameState != GlobalEnums.GameState.PLAYING ||
                !gameManager.IsGameplayScene() ||
                gameManager.isPaused ||
                gameManager.IsLoadingSceneTransition ||
                gameManager.IsInSceneTransition ||
                TransitionPoint.IsTransitionBlocked ||
                BossSceneController.IsTransitioning ||
                playerData.HasStoredMemoryState ||
                hero.cState.transitioning ||
                hero.cState.dead ||
                hero.cState.hazardDeath ||
                hero.cState.hazardRespawning)
            {
                return true;
            }

            hero.sprintFSM.SendEvent("SPRINT CANCEL");
            hero.RegainControl();
            hero.StartAnimationControlToIdle();
            return true;
        }

        internal static void TriggerRosarySpill()
        {
            try
            {
                PlayerData playerData = PlayerData.instance;
                HeroController hero = HeroController.instance;
                if (playerData == null || hero == null)
                {
                    return;
                }

                int amount = Math.Min(
                    RosarySpillAmount,
                    Math.Max(0, playerData.geo)
                );
                if (amount <= 0)
                {
                    return;
                }

                GameObject smallRosaryPrefab =
                    GlobalSettings.Gameplay.SmallGeoPrefab;
                if (smallRosaryPrefab == null)
                {
                    Warn(
                        "Rosary Spill Trap skipped because the native Rosary " +
                        "pickup prefab is not ready"
                    );
                    return;
                }

                // This is the game's own player-spill setup from the Thief
                // Charm damage path. SmallGeoPrefab is the one-Rosary pickup,
                // so each spawned object can be recovered normally.
                FlingUtils.Config config = new FlingUtils.Config
                {
                    Prefab = smallRosaryPrefab,
                    AmountMin = amount,
                    AmountMax = amount,
                    SpeedMin = 10f,
                    SpeedMax = 40f,
                    AngleMin = 65f,
                    AngleMax = 115f
                };
                List<GameObject> spilledRosaries =
                    new List<GameObject>(amount);
                FlingUtils.SpawnAndFling(
                    config,
                    hero.transform,
                    Vector3.zero,
                    spilledRosaries,
                    -1f
                );

                // FlingUtils normally spawns the requested amount exactly.
                // Count the actual pooled objects anyway so a failed/limited
                // spawn can never remove more Rosaries than it produced.
                int spawnedAmount = Math.Min(
                    amount,
                    spilledRosaries.Count
                );
                if (spawnedAmount > 0)
                {
                    hero.TakeGeo(spawnedAmount);
                }
            }
            catch (Exception ex)
            {
                Warn("Rosary Spill Trap could not be applied", ex);
            }
        }

        internal static void TriggerDarkness()
        {
            try
            {
                float proposedDeadline =
                    Time.unscaledTime + DarknessDurationSeconds;
                darknessDeadline = Math.Max(darknessDeadline, proposedDeadline);
                darknessActive = true;
                ApplyDarkness();
            }
            catch (Exception ex)
            {
                // The timer remains active. Update() retries when the hero's
                // vignette FSM is ready.
                Warn("Darkness Trap is waiting for the scene to become ready", ex);
            }
        }

        internal static void TriggerCursedCrest()
        {
            try
            {
                if (IsCursedCrestActive)
                {
                    cursedCrestDeadline = Math.Max(
                        cursedCrestDeadline,
                        Time.unscaledTime + CursedCrestDurationSeconds
                    );
                    return;
                }

                // Naked and Cursed Crest both own CurrentCrestID. Serialization
                // preserves both received traps so neither temporary crest
                // overwrites the other.
                if (NakedTrapManager.HasState)
                {
                    pendingCursedCrest = true;
                    return;
                }

                PlayerData playerData = PlayerData.instance;
                ToolCrest cursedCrest = GlobalSettings.Gameplay.CursedCrest;
                if (playerData == null || cursedCrest == null)
                {
                    Warn("Cursed Crest Trap skipped because crest data is not ready");
                    return;
                }

                if (SlabCaptureWarpSafety.IsActiveSlabCaptureCrest(
                        playerData))
                {
                    // The native Slab capture owns Cloakless with markTemp:false.
                    // A temporary AP crest cannot replace it and turn the Slab's
                    // saved return crest into Cloakless or make the trap crest
                    // permanent on F4.
                    Warn(
                        "Cursed Crest Trap skipped during The Slab's " +
                        "equipment-confiscation sequence"
                    );
                    return;
                }

                // Real curse quests and other vanilla temporary-crest sequences
                // remain unchanged.
                if (playerData.IsAnyCursed || playerData.IsCurrentCrestTemp)
                {
                    Warn(
                        "Cursed Crest Trap skipped because a vanilla temporary " +
                        "or cursed crest is already active"
                    );
                    return;
                }

                ToolCrest previousCrest = ToolItemManager.GetCrestByName(
                    playerData.CurrentCrestID
                );
                if (previousCrest == null)
                {
                    Warn(
                        "Cursed Crest Trap skipped because the current crest " +
                        "could not be resolved"
                    );
                    return;
                }

                cursedCrestInternalName = cursedCrest.name;
                previousCrestInternalName = playerData.CurrentCrestID;
                previousPreviousCrestInternalName =
                    playerData.PreviousCrestID ?? string.Empty;
                previousCrestWasTemporary = playerData.IsCurrentCrestTemp;

                SetCrest(cursedCrest, true);
                if (!string.Equals(
                    playerData.CurrentCrestID,
                    cursedCrestInternalName,
                    StringComparison.Ordinal
                ))
                {
                    ClearCursedCrestState();
                    Warn("Cursed Crest Trap was rejected by the crest runtime");
                    return;
                }

                IsCursedCrestActive = true;
                cursedCrestDeadline =
                    Time.unscaledTime + CursedCrestDurationSeconds;
                RequestCursedCrestRuntimeRefresh();
            }
            catch (Exception ex)
            {
                Warn("Cursed Crest Trap could not be applied", ex);
                TryRestoreCursedCrest();
            }
        }

        /// <summary>
        /// Gives the native Slab capture a real, non-trap crest to snapshot.
        /// HeroSlabCapture.ApplyCaptured calls this immediately before its
        /// AutoEquip(Cloakless, markTemp:false, swapTools:true).
        /// </summary>
        internal static bool PrepareForNativeSlabCapture()
        {
            pendingCursedCrest = false;
            if (HasCursedCrestState && !TryRestoreCursedCrest())
            {
                if (!ForceRestoreCursedCrestFieldsForSave())
                {
                    return false;
                }
                ClearCursedCrestState();
            }

            return NakedTrapManager.Reset();
        }

        /// <summary>
        /// Gives the native Rite of Rebirth sequence permanent ownership of
        /// the already-equipped Cursed Crest. This is not a restore:
        /// Shellwood_25b's Set Cursed state immediately
        /// follows with its own non-temporary AutoEquipCrest action and story
        /// bookkeeping.
        /// </summary>
        internal static void RelinquishCursedCrestForNativeCurse()
        {
            pendingCursedCrest = false;
            if (!HasCursedCrestState)
            {
                return;
            }

            // A late refresh requested by the AP trap must not race the
            // native Set Cursed state's own HUD and silk refresh actions.
            cursedCrestEquipmentRefreshPending = false;
            cursedCrestSilkRefreshPending = false;
            cursedCrestSpoolRefreshPending = false;
            ClearCursedCrestState();
        }

        internal static void TriggerMuckmaggotStatus()
        {
            try
            {
                if (IsWreathOfPurityEquipped())
                {
            // Gameplay.MaggotCharm is the native asset
                    // for Wreath of Purity. IsEquipped checks the live crest,
                    // not merely whether the AP item has been received.
                    return;
                }

                HeroController hero = HeroController.instance;
                if (hero == null)
                {
                    Warn(
                        "Muckmaggot Status Trap skipped because the hero is " +
                        "not ready"
                    );
                    return;
                }

                if (!muckmaggotActive &&
                    HeroControllerAbilityPatchUtil.CState(
                        hero,
                        "isMaggoted"
                    ))
                {
                    // Real quest and status effects remain under native ownership.
                    // Randomizer lifecycle cleanup therefore leaves a native
                    // infection intact.
                    Warn(
                        "Muckmaggot Status Trap skipped because Hornet is " +
                        "already natively maggoted"
                    );
                    return;
                }

                muckmaggotActive = true;
                ApplyMuckmaggotStatus();
            }
            catch (Exception ex)
            {
                Warn("Muckmaggot Status Trap could not be applied", ex);
            }
        }

        internal static void Update()
        {
            TryApplyPendingStagger();
            TryCompleteCursedCrestRuntimeRefresh();
            NakedTrapManager.Update();

            if (darknessActive)
            {
                if (Time.unscaledTime >= darknessDeadline)
                {
                    RestoreDarkness();
                }
                else
                {
                    ApplyDarkness();
                }
            }

            if (muckmaggotActive)
            {
                if (IsWreathOfPurityEquipped())
                {
                    RestoreMuckmaggotStatus();
                }
                else if (!muckmaggotApplied)
                {
                    ApplyMuckmaggotStatus();
                }
                else
                {
                    HeroController hero = HeroController.instance;
                    if (hero != null &&
                        !HeroControllerAbilityPatchUtil.CState(
                            hero,
                            "isMaggoted"
                        ))
                    {
                        ClearMuckmaggotState();
                    }
                }
            }

            if (!IsCursedCrestActive)
            {
                if (HasCursedCrestState)
                {
                    TryRestoreCursedCrest();
                }
                TryApplyPendingCursedCrest();
                return;
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData != null && playerData.atBench)
            {
                if (TryRestoreCursedCrest())
                {
                    RegenerateSilkAtBench();
                }
                TryApplyPendingCursedCrest();
                return;
            }

            if (playerData != null &&
                !string.Equals(
                    playerData.CurrentCrestID,
                    cursedCrestInternalName,
                    StringComparison.Ordinal
                ))
            {
                // A story transition changed the crest, so the new state takes
                // ownership.
                RequestCursedCrestRuntimeRefresh();
                ClearCursedCrestState();
                TryApplyPendingCursedCrest();
                return;
            }

            if (Time.unscaledTime >= cursedCrestDeadline)
            {
                TryRestoreCursedCrest();
            }
            TryApplyPendingCursedCrest();
        }

        internal static void PrepareForSave()
        {
            bool regenerateSilk =
                HasCursedCrestState && PlayerData.instance?.atBench == true;
            if (HasCursedCrestState && !TryRestoreCursedCrest())
            {
                if (!ForceRestoreCursedCrestFieldsForSave())
                {
                    throw new InvalidOperationException(
                        "Cursed Crest Trap could not restore the saved crest."
                    );
                }
                ClearCursedCrestState();
            }
            if (regenerateSilk) RegenerateSilkAtBench();
            NakedTrapManager.PrepareForSave();
        }

        internal static void ResumeAfterSave()
        {
            NakedTrapManager.ResumeAfterSave();
        }

        internal static void ResetTransientEffects()
        {
            pendingStaggerCount = 0;
            RestoreDarkness();
            RestoreMuckmaggotStatus();
            pendingCursedCrest = false;
            if (HasCursedCrestState)
            {
                TryRestoreCursedCrest();
            }
            NakedTrapManager.Reset();
        }

        internal static void ObserveNativeDarknessRequest(
            ref int requestedLevel
        )
        {
            if (!darknessActive || darknessWriteInProgress)
            {
                return;
            }

            // DarknessRegion is also used by the game when entering/leaving
            // native dark rooms. Remember even a request for level 2. Polling
            // alone cannot distinguish that from our own level-2 override.
            darknessRestoreLevel = requestedLevel;
            darknessApplied = true;
            requestedLevel = DarknessTrapLevel;
        }

        internal static void ObserveNativeMuckmaggotRequest(bool requested)
        {
            if (!muckmaggotActive || muckmaggotWriteInProgress)
            {
                return;
            }

            // A native apply or cleanse owns the status from this point on.
            // Relinquish trap ownership so later lifecycle cleanup cannot
            // cleanse real quest/status state.
            ClearMuckmaggotState();
        }

        private static void ApplyDarkness()
        {
            try
            {
                int currentLevel = DarknessRegion.GetDarknessLevel();

                // If a scene or native DarknessRegion changes the lighting
                // during the trap, remember that new native level so expiry
                // restores the correct scene state.
                if (!darknessApplied || currentLevel != DarknessTrapLevel)
                {
                    darknessRestoreLevel = currentLevel;
                }

                darknessApplied = true;
                if (currentLevel != DarknessTrapLevel)
                {
                    SetDarknessLevel(DarknessTrapLevel);
                }
            }
            catch (Exception ex)
            {
                Warn("Darkness Trap could not update the native vignette", ex);
            }
        }

        private static void RestoreDarkness()
        {
            try
            {
                if (darknessApplied &&
                    DarknessRegion.GetDarknessLevel() == DarknessTrapLevel)
                {
                    SetDarknessLevel(darknessRestoreLevel);
                }
            }
            catch (Exception ex)
            {
                Warn("Darkness Trap could not restore the native vignette", ex);
            }
            finally
            {
                darknessActive = false;
                darknessApplied = false;
                darknessDeadline = 0f;
                darknessRestoreLevel = 0;
            }
        }

        private static void ApplyMuckmaggotStatus()
        {
            try
            {
                HeroController hero = HeroController.instance;
                if (hero == null)
                {
                    return;
                }

                if (HeroControllerAbilityPatchUtil.CState(
                    hero,
                    "isMaggoted"
                ))
                {
                    // This can only be ours if we already applied it. A native
                    // status arriving before our delayed apply wins instead.
                    if (!muckmaggotApplied)
                    {
                        ClearMuckmaggotState();
                    }
                    return;
                }

                SetMuckmaggotStatus(hero, true);
                muckmaggotApplied = true;
            }
            catch (Exception ex)
            {
                Warn(
                    "Muckmaggot Status Trap is waiting for the hero status " +
                    "runtime",
                    ex
                );
            }
        }

        private static void RestoreMuckmaggotStatus()
        {
            try
            {
                HeroController hero = HeroController.instance;
                if (muckmaggotApplied &&
                    hero != null &&
                    HeroControllerAbilityPatchUtil.CState(
                        hero,
                        "isMaggoted"
                    ))
                {
                    SetMuckmaggotStatus(hero, false);
                }
            }
            catch (Exception ex)
            {
                Warn(
                    "Muckmaggot Status Trap could not restore the native " +
                    "status",
                    ex
                );
            }
            finally
            {
                ClearMuckmaggotState();
            }
        }

        private static void SetMuckmaggotStatus(
            HeroController hero,
            bool value
        )
        {
            muckmaggotWriteInProgress = true;
            try
            {
                hero.SetIsMaggoted(value);
            }
            finally
            {
                muckmaggotWriteInProgress = false;
            }
        }

        private static bool TryRestoreCursedCrest()
        {
            if (!IsCursedCrestActive &&
                string.IsNullOrEmpty(previousCrestInternalName))
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

                if (playerData.IsAnyCursed &&
                    !playerData.IsCurrentCrestTemp)
                {
                    RelinquishCursedCrestForNativeCurse();
                    return true;
                }

                if (!string.Equals(
                    playerData.CurrentCrestID,
                    cursedCrestInternalName,
                    StringComparison.Ordinal
                ))
                {
                    RequestCursedCrestRuntimeRefresh();
                    ClearCursedCrestState();
                    return true;
                }

                ToolCrest previousCrest = ToolItemManager.GetCrestByName(
                    previousCrestInternalName
                );
                if (previousCrest == null)
                {
                    Warn(
                        "Cursed Crest Trap restore is waiting for the previous " +
                        "crest asset"
                    );
                    return false;
                }

                SetCrest(previousCrest, previousCrestWasTemporary);
                if (!string.Equals(
                    playerData.CurrentCrestID,
                    previousCrestInternalName,
                    StringComparison.Ordinal
                ))
                {
                    return false;
                }

                // AutoEquip updates PreviousCrestID as normal. Put the exact
                // pre-trap temporary-crest bookkeeping back afterwards.
                playerData.PreviousCrestID =
                    previousPreviousCrestInternalName;
                playerData.IsCurrentCrestTemp =
                    previousCrestWasTemporary;
                RequestCursedCrestRuntimeRefresh();
                ClearCursedCrestState();
                return true;
            }
            catch (Exception ex)
            {
                Warn("Cursed Crest Trap restore failed; it will retry", ex);
                return false;
            }
        }

        private static void RegenerateSilkAtBench()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null ||
                !playerData.atBench ||
                playerData.IsAnyCursed)
            {
                return;
            }

            HeroController hero = HeroController.instance;
            if (hero != null)
            {
                hero.MaxRegenSilk();
            }
        }

        private static void SetCrest(ToolCrest crest, bool markTemporary)
        {
            // Trap transitions finalize their exact PlayerData bookkeeping
            // before broadcasting one equipment/silk/HUD refresh. Other AP
            // crest grants keep SetRandomizerCrest's immediate behaviour.
            if (!ToolPatches.SetRandomizerCrest(
                    crest,
                    markTemporary,
                    deferRuntimeRefresh: true))
            {
                throw new InvalidOperationException(
                    "The native crest data could not be switched."
                );
            }
        }

        private static bool ForceRestoreCursedCrestFieldsForSave()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return false;
            }
            if (!string.Equals(
                    playerData.CurrentCrestID,
                    cursedCrestInternalName,
                    StringComparison.Ordinal))
            {
                return true;
            }
            if (string.IsNullOrEmpty(previousCrestInternalName))
            {
                return false;
            }

            try
            {
                // Save safety takes precedence over keeping the visual trap
                // alive. These are the exact three values snapshotted before
                // Cursed Crest was equipped.
                playerData.CurrentCrestID = previousCrestInternalName;
                playerData.PreviousCrestID =
                    previousPreviousCrestInternalName;
                playerData.IsCurrentCrestTemp =
                    previousCrestWasTemporary;
                if (string.Equals(
                        playerData.CurrentCrestID,
                        cursedCrestInternalName,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                RequestCursedCrestRuntimeRefresh();
                return true;
            }
            catch (Exception ex)
            {
                Warn("Cursed Crest save fallback failed", ex);
                return !string.Equals(
                    playerData.CurrentCrestID,
                    cursedCrestInternalName,
                    StringComparison.Ordinal
                );
            }
        }

        private static void SetDarknessLevel(int level)
        {
            darknessWriteInProgress = true;
            try
            {
                DarknessRegion.SetDarknessLevel(level);
            }
            finally
            {
                darknessWriteInProgress = false;
            }
        }

        private static void RequestCursedCrestRuntimeRefresh()
        {
            cursedCrestEquipmentRefreshPending = true;
            cursedCrestSilkRefreshPending = true;
            cursedCrestSpoolRefreshPending = true;
            TryCompleteCursedCrestRuntimeRefresh();
        }

        private static void TryCompleteCursedCrestRuntimeRefresh()
        {
            if (!cursedCrestEquipmentRefreshPending &&
                !cursedCrestSilkRefreshPending &&
                !cursedCrestSpoolRefreshPending)
            {
                return;
            }

            // The equipment event updates Hornet's crest config, including
            // whether healing is interrupted. Wait for the live hero before
            // consuming this refresh so a transition cannot silently lose it.
            HeroController hero = HeroController.instance;
            if (cursedCrestEquipmentRefreshPending && hero != null)
            {
                try
                {
                    // SendEquippedChangedEvent(force: true) performs the
                    // native RefreshEquippedState exactly once before it
                    // broadcasts the regular and post-equipment events.
                    ToolItemManager.SendEquippedChangedEvent(true);
                    cursedCrestEquipmentRefreshPending = false;
                }
                catch (Exception ex)
                {
                    Warn(
                        "Cursed Crest Trap is waiting to refresh equipment state",
                        ex
                    );
                    return;
                }
            }

            // The silk-state broadcast follows the crest-config
            // event. This is the game's native curse-transition path.
            if (cursedCrestSilkRefreshPending && hero != null)
            {
                try
                {
                    hero.UpdateSilkCursed();
                    cursedCrestSilkRefreshPending = false;
                }
                catch (Exception ex)
                {
                    Warn(
                        "Cursed Crest Trap is waiting to refresh silk state",
                        ex
                    );
                    return;
                }
            }

            // SilkSpool is scene-owned and can briefly be absent during a
            // transition. This last step remains pending until the HUD exists
            // so the cursed spool does not stay visible until the player next
            // changes crest or sits at a bench.
            try
            {
                SilkSpool spool = SilkSpool.Instance;
                if (cursedCrestSpoolRefreshPending && spool != null)
                {
                    spool.DrawSpool();
                    cursedCrestSpoolRefreshPending = false;
                }
            }
            catch (Exception ex)
            {
                Warn("Cursed Crest Trap is waiting to redraw the silk spool", ex);
            }
        }

        private static void ClearCursedCrestState()
        {
            IsCursedCrestActive = false;
            cursedCrestDeadline = 0f;
            cursedCrestInternalName = string.Empty;
            previousCrestInternalName = string.Empty;
            previousPreviousCrestInternalName = string.Empty;
            previousCrestWasTemporary = false;
        }

        private static void ClearMuckmaggotState()
        {
            muckmaggotActive = false;
            muckmaggotApplied = false;
        }

        private static bool IsWreathOfPurityEquipped()
        {
            ToolItem wreath = GlobalSettings.Gameplay.MaggotCharm;
            return wreath != null && wreath.IsEquipped;
        }

        private static void TryApplyPendingCursedCrest()
        {
            if (!pendingCursedCrest || IsCursedCrestActive ||
                HasCursedCrestState || NakedTrapManager.HasState)
            {
                return;
            }

            pendingCursedCrest = false;
            TriggerCursedCrest();
        }

        private static void Warn(string message, Exception exception = null)
        {
            string detail = exception == null
                ? message
                : message + ": " + exception.Message;
            if (RandomizerPlugin.Log != null)
            {
                RandomizerPlugin.Log.LogWarning("[RANDOMIZER] " + detail);
            }
            else
            {
                Debug.LogWarning("[RANDOMIZER] " + detail);
            }
        }
    }
}
