using SilksongRandomizer.Patches;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TeamCherry.Localization;
using UnityEngine;
using System.IO;
using System.Linq;

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
        internal const float CursedCrestDurationSeconds = 90f;
        internal const float ShaderTrapDurationSeconds = 120f;

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
        private static PlayerData cursedCrestOwner;
        private static bool cursedCrestEquipmentRefreshPending;
        private static bool cursedCrestSilkRefreshPending;
        private static bool cursedCrestSpoolRefreshPending;
        private static bool pendingCursedCrest;
        private static float cursedCrestSaveRemaining;
        private static PlayerData cursedCrestSaveOwner;

        internal static bool HasCursedCrestSaveSnapshot =>
            cursedCrestSaveOwner != null && cursedCrestSaveRemaining > 0f;

        private static bool muckmaggotActive;
        private static bool muckmaggotApplied;
        private static bool muckmaggotWriteInProgress;

        private static bool shaderTrapActive;
        private static float shaderTrapDeadline;
        private static Camera shaderTrapCamera;
        private static ShaderTrapEffect shaderTrapEffect;
        private static Material shaderTrapMaterial;
        private static Shader[] shaderTrapShaders;
        private const string ShaderTrapBundleResourceWindows = "SilksongRandomizer.ShaderTrapShaders.Windows";
        private const string ShaderTrapBundleResourceLinux = "SilksongRandomizer.ShaderTrapShaders.Linux";
        private const string ShaderTrapBundleResourceMac = "SilksongRandomizer.ShaderTrapShaders.Mac";
        private static string ShaderTrapBundleResourceName =>
            Application.platform == RuntimePlatform.WindowsPlayer ? ShaderTrapBundleResourceWindows
            : Application.platform == RuntimePlatform.LinuxPlayer ? ShaderTrapBundleResourceLinux
            : Application.platform == RuntimePlatform.OSXPlayer ? ShaderTrapBundleResourceMac : null;
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
                TryStartStagger(hero, impactSide, null);
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

        internal static bool TryApplyLinkedStagger(Vector2 direction)
        {
            GameManager gameManager = GameManager.instance;
            HeroController hero = HeroController.instance;
            if (gameManager == null || hero == null || !hero.CanCustomRecoil() ||
                TryReleaseSprintForStagger(gameManager, hero) || !hero.CanTakeControl())
            {
                return false;
            }

            var side = direction.x < 0f
                ? GlobalEnums.CollisionSide.right
                : GlobalEnums.CollisionSide.left;
            return TryStartStagger(hero, side, direction);
        }

        private static bool TryStartStagger(
            HeroController hero, GlobalEnums.CollisionSide impactSide,
            Vector2? direction)
        {
            if (StartRecoilMethod == null || RecoilRoutineField == null ||
                (direction.HasValue && KnockbackLinkManager.RecoilVectorField == null))
            {
                return false;
            }

            IEnumerator recoil = StartRecoilMethod.Invoke(
                hero, new object[] { impactSide, 1 }) as IEnumerator;
            if (recoil == null)
            {
                return false;
            }

            Coroutine routine = hero.StartCoroutine(recoil);
            RecoilRoutineField.SetValue(hero, routine);
            if (direction.HasValue)
            {
                Vector2 native = (Vector2)KnockbackLinkManager.RecoilVectorField.GetValue(hero);
                KnockbackLinkManager.RecoilVectorField.SetValue(
                    hero, direction.Value * native.magnitude);
            }
            CameraShake.Shake(CameraShakeCues.BigRumble);
            return true;
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
            ApplyCursedCrest(false);
        }

        internal static void TriggerShaderTrap()
        {
            try
            {
                if (shaderTrapShaders == null)
                {
                    shaderTrapShaders = LoadTrapShaders();
                }

                if (shaderTrapShaders.Length == 0)
                {
                    Warn("Shader Trap has no shaders available.");
                    return;
                }

                float proposedDeadline =
                    Time.unscaledTime + ShaderTrapDurationSeconds;
                shaderTrapDeadline = Math.Max(shaderTrapDeadline, proposedDeadline);
                shaderTrapActive = true;
                Shader shader = shaderTrapShaders[
                    UnityEngine.Random.Range(0, shaderTrapShaders.Length)
                ];
                if (shaderTrapMaterial == null)
                {
                    shaderTrapMaterial = new Material(shader);
                }
                else
                {
                    shaderTrapMaterial.shader = shader;
                }
                ApplyShaderTrapEffect();
            }
            catch (Exception ex)
            {
                Warn("Shader Trap is waiting for the camera to become ready", ex);
            }
        }

        private static void ApplyCursedCrest(bool resumingAfterSave)
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
                if (NakedTrapManager.HasState && !resumingAfterSave)
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
                cursedCrestOwner = playerData;

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
                HeroController hero = HeroController.instance;
                if (hero == null)
                {
                    Warn(
                        "Muckmaggot Status Trap skipped because the hero is " +
                        "not ready"
                    );
                    return;
                }

                if (TryUseWreathOfPurity(hero))
                {
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
            LiteracyTrap.Update();
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

            if (shaderTrapActive)
            {
                if(Time.unscaledTime >= shaderTrapDeadline)
                {
                    RestoreShaderTrap();
                }
                else
                {
                    ApplyShaderTrapEffect();
                }
            }

            if (muckmaggotActive)
            {
                HeroController hero = HeroController.instance;
                if (TryUseWreathOfPurity(hero))
                {
                    RestoreMuckmaggotStatus();
                }
                else if (!muckmaggotApplied)
                {
                    ApplyMuckmaggotStatus();
                }
                else
                {
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
            if (playerData != null &&
                !ReferenceEquals(playerData, cursedCrestOwner))
            {
                RequestCursedCrestRuntimeRefresh();
                ClearCursedCrestState();
                return;
            }
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

            if (playerData != null)
            {
                playerData.IsCurrentCrestTemp = true;
            }

            if (Time.unscaledTime >= cursedCrestDeadline && CanExpireCursedCrest())
            {
                TryRestoreCursedCrest();
            }
            TryApplyPendingCursedCrest();
        }

        internal static void PrepareForSave()
        {
            bool regenerateSilk =
                HasCursedCrestState && PlayerData.instance?.atBench == true;
            float remaining = IsCursedCrestActive && !regenerateSilk &&
                cursedCrestOwner != null &&
                ReferenceEquals(PlayerData.instance, cursedCrestOwner) &&
                PlayerData.instance.CurrentCrestID == cursedCrestInternalName
                    ? Math.Max(0f, cursedCrestDeadline - Time.unscaledTime)
                    : 0f;
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
            cursedCrestSaveRemaining = remaining;
            cursedCrestSaveOwner = remaining > 0f ? PlayerData.instance : null;
            if (regenerateSilk) RegenerateSilkAtBench();
            NakedTrapManager.PrepareForSave();
        }

        internal static void ResumeAfterSave()
        {
            float remaining = cursedCrestSaveRemaining;
            PlayerData owner = cursedCrestSaveOwner;
            cursedCrestSaveRemaining = 0f;
            cursedCrestSaveOwner = null;
            if (remaining > 0f && ReferenceEquals(owner, PlayerData.instance) &&
                owner.atBench == false)
            {
                ApplyCursedCrest(true);
                if (IsCursedCrestActive)
                {
                    cursedCrestDeadline = Time.unscaledTime + remaining;
                }
            }
            NakedTrapManager.ResumeAfterSave();
        }

        internal static void ResetTransientEffects()
        {
            cursedCrestSaveRemaining = 0f;
            cursedCrestSaveOwner = null;
            pendingStaggerCount = 0;
            LiteracyTrap.Reset();
            RestoreDarkness();
            RestoreShaderTrap();
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

        private static void ApplyShaderTrapEffect()
        {
            try
            {
                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    return;
                }

                if (shaderTrapEffect == null || shaderTrapCamera != mainCamera)
                {
                    RemoveShaderTrapEffect();
                    shaderTrapCamera = mainCamera;
                    shaderTrapEffect = mainCamera.gameObject.AddComponent<ShaderTrapEffect>();
                }
                
                shaderTrapEffect.SetMaterial(shaderTrapMaterial);
            }
            catch (Exception ex)
            {
                Warn("Shader Trap could not attach to the camera", ex);
            }
        }

        private static void RestoreShaderTrap()
        {
            try
            {
                RemoveShaderTrapEffect();
            }
            catch (Exception ex)
            {
                Warn("Shader Trap could not attach to the camera", ex);
            }
            finally
            {
                shaderTrapActive = false;
                shaderTrapDeadline = 0f;
                if (shaderTrapMaterial != null)
                    UnityEngine.Object.Destroy(shaderTrapMaterial);
                shaderTrapMaterial = null;
            }
        }

        private static void RemoveShaderTrapEffect()
        {
            if (shaderTrapEffect != null)
            {
                shaderTrapEffect.SetMaterial(null);
                shaderTrapEffect.enabled = false;
                UnityEngine.Object.Destroy(shaderTrapEffect);
                shaderTrapEffect = null;
            }
            shaderTrapCamera = null;
        }

        private static Shader[] LoadTrapShaders()
        {
            string resourceName = ShaderTrapBundleResourceName;
            if (resourceName == null) return Array.Empty<Shader>();
            using (Stream stream = typeof(TrapManager).Assembly.GetManifestResourceStream(resourceName))
            using (MemoryStream buffer = new MemoryStream())
            {
                if (stream == null)
                {
                    return new Shader[0];
                }
                stream.CopyTo(buffer);
                AssetBundle bundle = AssetBundle.LoadFromMemory(buffer.ToArray());
                if (bundle == null)
                {
                    return new Shader[0];
                }
                try
                {
                    return bundle.LoadAllAssets<Shader>()
                        .Where(shader => shader != null && shader.isSupported).ToArray();
                }
                finally
                {
                    bundle.Unload(false);
                }
            }
        }

        private sealed class ShaderTrapEffect : MonoBehaviour
        {
            private Material effectMaterial;

            internal void SetMaterial(Material material)
            {
                effectMaterial = material;
            }

            private void OnRenderImage(
                RenderTexture source,
                RenderTexture destination
            )
            {
                if (effectMaterial == null)
                {
                    Graphics.Blit(source, destination);
                    return;
                }

                Graphics.Blit(source, destination, effectMaterial);
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

        private static bool CanExpireCursedCrest()
        {
            HeroController hero = HeroController.SilentInstance;
            GameManager manager = GameManager.SilentInstance;
            if (hero == null || hero.cState == null || manager == null ||
                manager.isPaused || !manager.IsGameplayScene() ||
                manager.IsInSceneTransition || manager.IsLoadingSceneTransition ||
                manager.inputHandler?.inputActions?.Dash.IsPressed == true ||
                hero.controlReqlinquished || !hero.CanInput() ||
                hero.cState.dead || hero.cState.hazardDeath || hero.cState.hazardRespawning ||
                hero.cState.dashing || hero.cState.isSprinting ||
                hero.sprintFSM?.FsmVariables.FindFsmBool("Is Sprinting")?.Value == true)
                return false;

            return hero.sprintFSM == null || hero.sprintFSM.ActiveStateName == "Idle";
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

                if (!ReferenceEquals(playerData, cursedCrestOwner))
                {
                    RequestCursedCrestRuntimeRefresh();
                    ClearCursedCrestState();
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
            if (!ReferenceEquals(playerData, cursedCrestOwner))
            {
                return true;
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
            cursedCrestOwner = null;
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

        private static bool TryUseWreathOfPurity(HeroController hero)
        {
            PlayerData playerData = PlayerData.instance;
            if (hero == null || playerData == null ||
                !IsWreathOfPurityEquipped() ||
                playerData.MaggotCharmHits >= 3)
            {
                return false;
            }

            hero.DidMaggotCharmHit();
            return true;
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

    internal static class LiteracyTrap
    {
        private static readonly FieldInfo DialogueInstance = AccessTools.Field(typeof(DialogueBox), "_instance");
        private static readonly FieldInfo DialogueRunning = AccessTools.Field(typeof(DialogueBox), "isDialogueRunning");
        private static readonly List<string[]> Dialogues = LoadDialogues();
        private static int pending;
        private static HeroController owner;
        private static SaveState saveOwner;
        private static int controlVersion;
        private static int sceneHandle;
        private static List<string> pages;
        private static int page;
        private static float openedAt;
        private static float pageOpenedAt;
        private static float nextStart;
        private static bool revealed;
        private static bool recoveringFromHit;
        private static GameObject displayRoot;
        private static TMProOld.TextMeshPro displayText;

        internal static void Trigger()
        {
            if (pending < int.MaxValue) pending++;
        }

        private static bool HasConversation()
        {
            if (DialogueInstance == null || DialogueRunning == null) return true;
            var box = DialogueInstance.GetValue(null) as DialogueBox;
            return box == null || (bool)DialogueRunning.GetValue(box);
        }

        private static bool IsGameplayReady(HeroController hero)
        {
            GameManager manager = GameManager.SilentInstance;
            PlayerData data = PlayerData.instance;
            return hero != null && hero.cState != null && manager != null && data != null &&
                manager.GameState == GlobalEnums.GameState.PLAYING && manager.IsGameplayScene() &&
                !manager.IsMemoryScene() && !manager.isPaused && !manager.RespawningHero &&
                !manager.IsLoadingSceneTransition && !manager.IsInSceneTransition &&
                !TransitionPoint.IsTransitionBlocked && !BossSceneController.IsTransitioning &&
                !data.HasStoredMemoryState && !data.isInventoryOpen && !data.atBench &&
                !data.disablePause && !hero.cState.transitioning && !hero.cState.dead &&
                !hero.cState.hazardDeath && !hero.cState.hazardRespawning &&
                !RandomizerPlugin.IsConnectionGuiOpen && !GenericMessageCanvas.IsActive &&
                InteractManager.BlockingInteractable == null && !HasConversation();
        }

        internal static bool CanProcessReceivedItems(HeroController hero)
        {
            return pages != null && hero == owner && HeroController.ControlVersion == controlVersion &&
                hero.controlReqlinquished && saveOwner == SaveState.Instance && IsGameplayReady(hero);
        }

        internal static List<string[]> ParseDialogues(string text)
        {
            var result = new List<string[]>();
            var passage = new List<string>();
            using (var reader = new StringReader(text ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        passage.Add(line);
                    else if (passage.Count > 0)
                    {
                        result.Add(passage.ToArray());
                        passage.Clear();
                    }
                }
            }
            if (passage.Count > 0) result.Add(passage.ToArray());
            return result;
        }

        internal static void Update()
        {
            try
            {
                HeroController hero = HeroController.SilentInstance;
                if (pages != null)
                {
                    if (owner == null || hero != owner || saveOwner != SaveState.Instance ||
                        owner.gameObject.scene.handle != sceneHandle ||
                        HeroController.ControlVersion != controlVersion ||
                        (!owner.controlReqlinquished && !recoveringFromHit) ||
                        !IsGameplayReady(owner) || Time.unscaledTime - openedAt >= 60f)
                    {
                        Close();
                        return;
                    }
                    if (recoveringFromHit && owner.CanTakeControl())
                    {
                        owner.RelinquishControl();
                        controlVersion = HeroController.ControlVersion;
                        recoveringFromHit = false;
                    }
                    InputHandler input = GameManager.SilentInstance.inputHandler;
                    if (Time.unscaledTime - pageOpenedAt >= 0.2f && input.WasSkipButtonPressed)
                    {
                        if (!revealed && (Time.unscaledTime - pageOpenedAt) * RevealSpeed < pages[page].Length)
                            revealed = true;
                        else if (++page >= pages.Count)
                            Close();
                        else
                        {
                            pageOpenedAt = Time.unscaledTime;
                            revealed = false;
                        }
                    }
                    return;
                }
                if (pending <= 0 || Time.unscaledTime < nextStart || !IsGameplayReady(hero) ||
                    !hero.CanTakeControl() || !hero.cState.onGround || hero.cState.isSprinting ||
                    hero.cState.isToolThrowing || hero.cState.swimming || hero.cState.superDashing ||
                    hero.sprintFSM?.FsmVariables.FindFsmBool("Is Sprinting")?.Value == true)
                    return;

                pending--;
                if (Dialogues.Count == 0) return;
                var dialogue = new List<string>(Dialogues[UnityEngine.Random.Range(0, Dialogues.Count)]);
                owner = hero;
                saveOwner = SaveState.Instance;
                sceneHandle = hero.gameObject.scene.handle;
                pages = dialogue;
                CreateDisplay();
                try { hero.RelinquishControl(); }
                finally { controlVersion = HeroController.ControlVersion; }
                recoveringFromHit = false;
                page = 0;
                openedAt = pageOpenedAt = Time.unscaledTime;
                revealed = false;
            }
            catch (Exception ex)
            {
                pending = 0;
                Close();
                RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Literacy Trap ended: " + ex.Message);
            }
        }

        private const float RevealSpeed = 65f;

        internal static void Close()
        {
            HeroController hero = owner;
            bool hadPages = pages != null;
            owner = null;
            saveOwner = null;
            pages = null;
            if (displayRoot != null)
            {
                displayRoot.SetActive(false);
                UnityEngine.Object.Destroy(displayRoot);
            }
            displayRoot = null;
            displayText = null;
            recoveringFromHit = false;
            if (!hadPages) return;
            nextStart = Time.unscaledTime + 3f;
            try
            {
                if (hero != null && hero == HeroController.SilentInstance &&
                    HeroController.ControlVersion == controlVersion && hero.controlReqlinquished &&
                    hero.cState != null && !hero.cState.dead && !hero.cState.hazardDeath &&
                    !hero.cState.hazardRespawning && !hero.cState.transitioning &&
                    hero.gameObject.scene.handle == sceneHandle &&
                    GameManager.SilentInstance != null && !GameManager.SilentInstance.RespawningHero &&
                    !GameManager.SilentInstance.IsInSceneTransition &&
                    !GameManager.SilentInstance.IsLoadingSceneTransition && !BossSceneController.IsTransitioning)
                    hero.RegainControl();
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Literacy Trap control cleanup failed: " + ex.Message);
            }
        }

        internal static void Reset()
        {
            pending = 0;
            Close();
        }

        private static List<string[]> LoadDialogues()
        {
            using (Stream stream = typeof(LiteracyTrap).Assembly.GetManifestResourceStream(
                "SilksongRandomizer.LiteracyTrap.txt"))
            {
                if (stream == null) return new List<string[]>();
                using (var reader = new StreamReader(stream))
                    return ParseDialogues(reader.ReadToEnd());
            }
        }

        private static void CreateDisplay()
        {
            var box = DialogueInstance.GetValue(null) as DialogueBox;
            var template = AccessTools.Field(typeof(DialogueBox), "textMesh").GetValue(box) as TMProOld.TextMeshPro;
            var playerAppearance = AccessTools.Field(typeof(DialogueBox), "playerAppearance").GetValue(box) as GameObject;
            var stop = AccessTools.Field(typeof(DialogueBox), "stopAnimator").GetValue(box) as Animator;
            if (box == null || template == null) throw new InvalidOperationException("Dialogue visuals are unavailable.");
            displayRoot = new GameObject("Literacy Trap Dialogue");
            displayRoot.SetActive(false);
            displayRoot.layer = box.gameObject.layer;
            displayRoot.transform.SetParent(box.transform.parent, false);
            displayRoot.transform.localPosition = (Vector3)AccessTools.Field(typeof(DialogueBox), "initialPos").GetValue(box);
            displayRoot.transform.localRotation = box.transform.localRotation;
            displayRoot.transform.localScale = box.transform.localScale;
            var sourceSorting = box.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (sourceSorting != null)
            {
                var sorting = displayRoot.AddComponent<UnityEngine.Rendering.SortingGroup>();
                sorting.sortingLayerID = sourceSorting.sortingLayerID;
                sorting.sortingOrder = sourceSorting.sortingOrder + 1;
            }
            foreach (var source in box.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if ((playerAppearance != null && source.transform.IsChildOf(playerAppearance.transform)) ||
                    (stop != null && source.transform.IsChildOf(stop.transform))) continue;
                var visual = new GameObject(source.name);
                visual.layer = source.gameObject.layer;
                visual.transform.SetParent(displayRoot.transform, false);
                visual.transform.localPosition = box.transform.InverseTransformPoint(source.transform.position);
                visual.transform.localRotation = Quaternion.Inverse(box.transform.rotation) * source.transform.rotation;
                Vector3 scale = source.transform.lossyScale;
                Vector3 parentScale = box.transform.lossyScale;
                visual.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
                var sprite = visual.AddComponent<SpriteRenderer>();
                sprite.sprite = source.sprite;
                sprite.sharedMaterial = source.sharedMaterial;
                sprite.color = new Color(source.color.r, source.color.g, source.color.b, 1f);
                sprite.sortingLayerID = source.sortingLayerID;
                sprite.sortingOrder = source.sortingOrder;
                sprite.drawMode = source.drawMode;
                sprite.size = source.size;
                sprite.flipX = source.flipX;
                sprite.flipY = source.flipY;
            }
            var textObject = UnityEngine.Object.Instantiate(template.gameObject, displayRoot.transform, false);
            foreach (var behaviour in textObject.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is TMProOld.TMP_Text) && !(behaviour is TMProOld.TextContainer))
                    UnityEngine.Object.DestroyImmediate(behaviour);
            displayText = textObject.GetComponent<TMProOld.TextMeshPro>();
            textObject.SetActive(true);
            displayRoot.SetActive(true);
            displayText.color = Color.white;
            displayText.richText = false;
            displayText.text = string.Empty;
            displayText.maxVisibleCharacters = 0;
            displayText.pageToDisplay = 1;
            displayText.enabled = true;
            displayText.renderer.enabled = true;
        }

        internal static void Draw()
        {
            if (pages == null || owner == null) return;
            try
            {
                if (displayRoot == null || displayText == null)
                {
                    Close();
                    return;
                }
                string text = pages[page];
                if (displayText.text != text) displayText.text = text;
                displayText.maxVisibleCharacters = revealed ? int.MaxValue :
                    (int)((Time.unscaledTime - pageOpenedAt) * RevealSpeed);
            }
            catch (Exception ex)
            {
                Close();
                RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Literacy Trap display closed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        private static class DamageRecovery
        {
            private static void Postfix(HeroController __instance)
            {
                if (pages == null || __instance != owner) return;
                if (__instance.cState.dead || __instance.cState.hazardDeath ||
                    __instance.cState.hazardRespawning || PlayerData.instance.health <= 0)
                    Close();
                else if (HeroController.ControlVersion == controlVersion && !__instance.controlReqlinquished)
                    recoveringFromHit = true;
            }
        }

        [HarmonyPatch(typeof(DialogueBox), nameof(DialogueBox.StartConversation),
            new Type[] { typeof(string), typeof(NPCControlBase), typeof(bool),
                typeof(DialogueBox.DisplayOptions), typeof(Action), typeof(Action) })]
        private static class ConversationCleanup
        {
            private static void Prefix() => Close();
        }

        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanOpenInventory))]
        private static class InventoryGuard
        {
            private static void Postfix(HeroController __instance, ref bool __result)
            {
                if (__instance == owner && pages != null) __result = false;
            }
        }
    }

}
