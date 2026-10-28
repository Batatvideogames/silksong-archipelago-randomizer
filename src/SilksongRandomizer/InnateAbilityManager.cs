using System;
using System.Reflection;
using GlobalEnums;
using HarmonyLib;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class InnateAbilityManager
    {
        internal const string LedgeGrabItemName = "Ledge Grab";
        internal const string SwimItemName = "Swim";

        private static SaveState trackedSwimState;
        private static bool trackedSwimEnabled = true;
        private static bool swimReconciliationPending;
        private static SaveState bilewaterApproachState;
        private static int bilewaterApproachScene;
        private static Vector3 bilewaterApproachPoint;
        private static bool bilewaterApproachFacing;
        private static bool bilewaterReturnAvailable;

        private static readonly FieldInfo HazardRespawningHeroField =
            AccessTools.Field(typeof(GameManager), "hazardRespawningHero");

        private static readonly FieldInfo WaterIsInWaterField =
            AccessTools.Field(typeof(HeroWaterController), "isInWater");

        private static readonly MethodInfo WaterExitedMethod =
            AccessTools.Method(
                typeof(HeroWaterController),
                "ExitedWater",
                new[] { typeof(bool) }
            );

        private static readonly MethodInfo FrostSpeedMethod =
            AccessTools.Method(typeof(HeroController), "GetTotalFrostSpeed");

        private static readonly FieldInfo WaterEnteredEvent =
            AccessTools.Field(typeof(SurfaceWaterRegion), "HeroEntered");

        private static readonly MethodInfo MaggotWaterExitedMethod =
            AccessTools.Method(typeof(MaggotRegion), "OnHeroExitedWater");

        internal static bool LedgeGrabEnabled
        {
            get
            {
                SaveState saveState = SaveState.Instance;
                return saveState == null ||
                       !saveState.IsRoomBound ||
                       !saveState.ledgegrabAbilityRando ||
                       (saveState.directionalLedgeGrab
                           ? HeroController.instance != null && DirectionalAbilities.Owns(saveState, "Ledge Grab", HeroController.instance.cState.facingRight)
                           : HasReceivedItem(saveState, LedgeGrabItemName));
            }
        }

        internal static bool SwimEnabled
        {
            get
            {
                SaveState saveState = SaveState.Instance;
                return saveState == null ||
                       !saveState.IsRoomBound ||
                       !saveState.swimAbilityRando ||
                       HasReceivedItem(saveState, SwimItemName);
            }
        }

        private static bool HasReceivedItem(
            SaveState saveState,
            string itemName
        )
        {
            return saveState.receivedItems != null &&
                   saveState.receivedItems.Contains(itemName);
        }

        internal static void Update()
        {
            SaveState saveState = SaveState.Instance;
            bool swimEnabled = SwimEnabled;

            if (!ReferenceEquals(trackedSwimState, saveState))
            {
                trackedSwimState = saveState;
                trackedSwimEnabled = true;
                swimReconciliationPending = !swimEnabled;
            }
            else if (trackedSwimEnabled && !swimEnabled)
            {
                swimReconciliationPending = true;
            }
            else if (swimEnabled)
            {
                swimReconciliationPending = false;
            }

            trackedSwimEnabled = swimEnabled;
            if (!swimReconciliationPending || swimEnabled)
            {
                return;
            }

            GameManager gameManager = GameManager.SilentInstance;
            HeroController hero = HeroController.instance;
            if (gameManager == null ||
                gameManager.GameState != GameState.PLAYING ||
                !gameManager.IsGameplayScene() ||
                gameManager.isPaused ||
                hero == null ||
                !hero.isHeroInPosition ||
                hero.cState.dead ||
                hero.cState.hazardDeath ||
                hero.cState.hazardRespawning ||
                hero.cState.transitioning ||
                ToolItemManager.IsInCutscene)
            {
                return;
            }

            HeroWaterController water =
                hero.GetComponent<HeroWaterController>();
            bool isSwimming =
                hero.cState.swimming ||
                (water != null &&
                 (water.IsInWater ||
                  water.CurrentState !=
                      HeroWaterController.States.Inactive));

            if (isSwimming)
            {
                RespawnFromDisabledWater(hero);
            }

            swimReconciliationPending = false;
        }

        internal static void RememberBilewaterGround(HeroController hero)
        {
            SaveState state = SaveState.Instance;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (state?.IsRoomBound != true || scene.name != "Library_07" ||
                !hero.isHeroInPosition || !hero.cState.onGround || hero.cState.swimming ||
                hero.cState.dead || hero.cState.hazardDeath || hero.cState.hazardRespawning ||
                hero.cState.transitioning || hero.controlReqlinquished) return;
            if (!HasBilewaterGround(hero.transform.position)) return;
            bilewaterApproachState = state;
            bilewaterApproachScene = scene.handle;
            bilewaterApproachPoint = hero.transform.position;
            bilewaterApproachFacing = hero.cState.facingRight;
        }

        private static bool HasBilewaterGround(Vector3 point)
        {
            for (int sample = -1; sample <= 1; sample++)
            {
                Vector2 origin = point + Vector3.right * (sample * 0.3f);
                if (!Helper.IsRayHittingNoTriggers(origin, Vector2.down, 2.5f, 8448, out var hit) ||
                    hit.normal.y < 0.7f || hit.collider.GetComponentInParent<DamageHero>() != null) return false;
            }
            return true;
        }

        internal static void RecordBilewaterDeparture(GameManager.SceneLoadInfo info)
        {
            HeroController hero = HeroController.instance;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bilewaterReturnAvailable = false;
            if (info == null || hero == null || SaveState.Instance?.IsRoomBound != true ||
                scene.name != "Library_07" || info.SceneName != "Shadow_22" ||
                (info.EntryGateName != "top1" && info.EntryGateName != "top2" && info.EntryGateName != "top3")) return;
            if (!ReferenceEquals(bilewaterApproachState, SaveState.Instance) || scene.handle != bilewaterApproachScene)
            {
                Vector3 point = hero.playerData.hazardRespawnLocation;
                bool nativeMarker = false;
                foreach (var marker in UnityEngine.Object.FindObjectsByType<HazardRespawnMarker>(FindObjectsSortMode.None))
                    if (marker.gameObject.scene == scene && Vector3.Distance(marker.transform.position, point) < 0.1f)
                    {
                        nativeMarker = true;
                    }
                if (!nativeMarker || !HasBilewaterGround(point)) return;
                bilewaterApproachState = SaveState.Instance;
                bilewaterApproachScene = scene.handle;
                bilewaterApproachPoint = point;
                bilewaterApproachFacing = hero.playerData.hazardRespawnFacing == HazardRespawnMarker.FacingDirection.Right;
            }
            bilewaterReturnAvailable = true;
        }

        internal static bool RestoreBilewaterApproach(GameManager manager)
        {
            HeroController hero = HeroController.instance;
            if (!bilewaterReturnAvailable || SwimEnabled || hero == null ||
                !ReferenceEquals(bilewaterApproachState, SaveState.Instance) ||
                manager.sceneName != "Shadow_22" || hero.playerData.hazardRespawnLocation.y >= -8f) return false;
            Vector3 point = bilewaterApproachPoint;
            bool facing = bilewaterApproachFacing;
            bilewaterReturnAvailable = false;
            hero.SetHazardRespawn(point, facing);
            HazardRespawningHeroField.SetValue(manager, true);
            manager.BeginSceneTransition(new GameManager.SceneLoadInfo {
                SceneName = "Library_07", EntryGateName = "",
                PreventCameraFadeOut = true, WaitForSceneTransitionCameraFade = false
            });
            return true;
        }

        internal static void RespawnFromDisabledWater(
            HeroController hero,
            SurfaceWaterRegion surfaceWater = null
        )
        {
            if (hero == null ||
                !hero.isHeroInPosition ||
                hero.cState.dead ||
                hero.cState.hazardDeath ||
                hero.cState.hazardRespawning ||
                hero.cState.transitioning)
            {
                return;
            }

            if (surfaceWater != null)
            {
                ApplyWaterEntryEffects(hero, surfaceWater);
                if (hero.cState.dead || hero.playerData.health <= 0)
                {
                    return;
                }
            }

            CleanActiveWaterState(hero);
            KeepGroalWaterRespawnInside(hero, surfaceWater);
            DamageHero soupBurner = null;
            if (surfaceWater != null && surfaceWater.gameObject.scene.name == "Mosstown_02c" &&
                surfaceWater.transform.parent?.name == "moss_hut_set")
            {
                soupBurner = surfaceWater.transform.parent.Find("NPC Scene/moss pot_large/Hero Burner")
                    ?.GetComponent<DamageHero>();
            }
            bool hotSoup = soupBurner != null && soupBurner.isActiveAndEnabled &&
                soupBurner.hazardType == HazardType.ACID;
            hero.TakeDamage(
                hotSoup ? soupBurner.gameObject : hero.gameObject,
                CollisionSide.other,
                1,
                hotSoup ? HazardType.ACID : HazardType.RESPAWN_PIT
            );
        }

        private static void KeepGroalWaterRespawnInside(HeroController hero, SurfaceWaterRegion water)
        {
            if (water == null || water.gameObject.scene.name != "Shadow_18") return;
            GameObject owner = GameObject.Find("Battle Scene");
            BattleScene battle = owner == null ? null : owner.GetComponent<BattleScene>();
            if (battle == null || battle.gameObject.scene != water.gameObject.scene ||
                !Patches.ArenaSoulGatePatches.Started(battle) || Patches.ArenaSoulGatePatches.Completed(battle)) return;
            Collider2D trigger = battle.GetComponent<Collider2D>();
            if (trigger == null || !Patches.ArenaSoulGatePatches.TryGetInterior(battle, trigger, out Bounds interior) ||
                hero.transform.position.x <= interior.min.x || hero.transform.position.x >= interior.max.x) return;

            Vector3 point = default;
            float distance = float.PositiveInfinity;
            foreach (string name in new[] { "terrain collider (10)", "terrain collider (9)" })
            {
                GameObject platform = GameObject.Find(name);
                Collider2D ground = platform == null ? null : platform.GetComponent<Collider2D>();
                if (ground == null || !ground.enabled || ground.isTrigger ||
                    ground.gameObject.scene != water.gameObject.scene || ground.bounds.max.y <= water.transform.position.y ||
                    ground.bounds.min.x <= interior.min.x || ground.bounds.max.x >= interior.max.x) continue;
                for (int i = 1; i < 8; i++)
                {
                    float x = Mathf.Lerp(ground.bounds.min.x, ground.bounds.max.x, i / 8f);
                    float y = ground.bounds.max.y + 2f;
                    RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, y), Vector2.down, 4f, 8448);
                    if (hit.collider != ground || hit.normal.y < 0.9f || hit.point.y <= water.transform.position.y + 0.5f) continue;
                    bool supported = true;
                    foreach (float offset in new[] { -0.6f, 0.6f })
                    {
                        RaycastHit2D edge = Physics2D.Raycast(new Vector2(x + offset, y), Vector2.down, 4f, 8448);
                        supported &= edge.collider == ground && Mathf.Abs(edge.point.y - hit.point.y) < 0.1f;
                    }
                    float candidateDistance = Mathf.Abs(x - hero.transform.position.x);
                    if (!supported || candidateDistance >= distance) continue;
                    point = new Vector3(x, hit.point.y + 2f, 0f);
                    distance = candidateDistance;
                }
            }
            if (float.IsPositiveInfinity(distance)) return;
            hero.SetHazardRespawn(point, point.x < interior.center.x);
        }

        private static void ApplyWaterEntryEffects(
            HeroController hero,
            SurfaceWaterRegion surfaceWater
        )
        {
            // A quick dip still counts for muckmaggots.
            if (WaterEnteredEvent.GetValue(surfaceWater) is Action<HeroController> entered)
            {
                foreach (Action<HeroController> handler in entered.GetInvocationList())
                {
                    if (!(handler.Target is MaggotRegion region) ||
                        !region.isActiveAndEnabled || !region.IsActive)
                    {
                        continue;
                    }

                    try
                    {
                        handler(hero);
                    }
                    finally
                    {
                        MaggotWaterExitedMethod.Invoke(region, new object[] { hero });
                    }
                }
            }

            if ((float)FrostSpeedMethod.Invoke(hero, null) > Mathf.Epsilon)
            {
                hero.TakeFrostDamage(1);
            }
        }

        private static void CleanActiveWaterState(HeroController hero)
        {
            HeroWaterController water =
                hero.GetComponent<HeroWaterController>();
            if (water == null)
            {
                return;
            }

            bool hasActiveWaterState =
                water.IsInWater ||
                water.CurrentState != HeroWaterController.States.Inactive ||
                hero.cState.swimming;

            WaterIsInWaterField?.SetValue(water, false);
            if (!hasActiveWaterState)
            {
                return;
            }

            if (WaterExitedMethod != null)
            {
                try
                {
                    WaterExitedMethod.Invoke(
                        water,
                        new object[] { false }
                    );
                    return;
                }
                catch
                {
                }
            }

            water.ExitWaterRegion(vibrate: false);
        }
    }
}
