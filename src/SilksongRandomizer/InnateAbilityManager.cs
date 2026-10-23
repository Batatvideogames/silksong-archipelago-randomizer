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
            hero.TakeDamage(
                hero.gameObject,
                CollisionSide.other,
                1,
                HazardType.RESPAWN_PIT
            );
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
