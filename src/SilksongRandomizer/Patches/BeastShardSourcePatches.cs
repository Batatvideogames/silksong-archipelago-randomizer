using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class BeastShardSourcePatches
    {
        [ThreadStatic]
        private static int cragglerCorpseEmissionDepth;

        private static readonly FieldInfo SprintRaceEndEventField =
            AccessTools.Field(
                typeof(SprintRaceController),
                "raceEndEvent"
            );

        private static readonly FieldInfo SprintRaceEndCompleteEventField =
            AccessTools.Field(
                typeof(SprintRaceController),
                "raceEndCompleteEvent"
            );

        private static readonly FieldInfo CragglerInstantiatedCorpsesField =
            AccessTools.Field(
                typeof(EnemyDeathEffects),
                "instantiatedCorpses"
            );

        private static bool IsActive(string locationName)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsRandomized(ItemType.Resource) &&
                   state.IsLocationEnabled(locationName) &&
                   state.IsLocationInSeed(locationName);
        }

        private static SavedItem GetProxy(string locationName)
        {
            return MinorPickupPatches.GetProxyItem(
                locationName,
                ItemType.Resource
            );
        }

        [HarmonyPatch(
            typeof(HealthManager),
            "SpawnItemDrop",
            new Type[]
            {
                typeof(SavedItem),
                typeof(int),
                typeof(CollectableItemPickup),
                typeof(Transform),
                typeof(int),
            }
        )]
        private static class ExactEnemyDropPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.Last)]
            private static void Prefix(
                HealthManager __instance,
                ref SavedItem dropItem,
                int count,
                CollectableItemPickup prefab,
                int limit)
            {
                if (!BeastShardSourceManifest.TryGetEnemyDropLocation(
                        __instance,
                        dropItem,
                        count,
                        prefab,
                        limit,
                        out string locationName) ||
                    !IsActive(locationName))
                {
                    return;
                }

                // Replace only the selected Great Shard argument. Currency,
                // other item-drop groups, death events and persistence all
                // continue through the unmodified native method.
                dropItem = GetProxy(locationName);
            }
        }

        [HarmonyPatch(typeof(EnemyDeathEffects), "EmitCorpse")]
        private static class CragglerCorpseEmissionPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                EnemyDeathEffects __instance,
                out bool __state)
            {
                __state =
                    IsActive(BeastShardSourceManifest.CragglerLocation) &&
                    BeastShardSourceManifest.IsExpectedCraggler(
                        __instance
                    );
                if (__state)
                {
                    cragglerCorpseEmissionDepth++;
                    TryReplaceCragglerCorpsePickup(
                        FindPreinstantiatedCragglerCorpsePickup(
                            __instance
                        )
                    );
                }
            }

            [HarmonyPostfix]
            private static void Postfix(
                GameObject __result,
                bool __state)
            {
                if (!__state || __result == null)
                {
                    return;
                }

                CollectableItemPickup pickup =
                    BeastShardSourceManifest.FindCragglerCorpsePickup(
                        __result
                    );
                TryReplaceCragglerCorpsePickup(pickup);
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(
                Exception __exception,
                bool __state)
            {
                if (__state)
                {
                    cragglerCorpseEmissionDepth = Math.Max(
                        0,
                        cragglerCorpseEmissionDepth - 1
                    );
                }
                return __exception;
            }
        }

        [HarmonyPatch(typeof(CollectableItemPickup), "Awake")]
        private static class CragglerCorpsePickupAwakePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                CollectableItemPickup __instance)
            {
                if (cragglerCorpseEmissionDepth <= 0)
                {
                    return;
                }

                TryReplaceCragglerCorpsePickup(__instance);
            }
        }

        private static void TryReplaceCragglerCorpsePickup(
            CollectableItemPickup pickup)
        {
            SavedItem nativeItem = pickup == null ? null : pickup.Item;
            if (!IsActive(BeastShardSourceManifest.CragglerLocation) ||
                !BeastShardSourceManifest.
                    IsExpectedCragglerCorpsePickup(
                        pickup,
                        nativeItem
                    ))
            {
                return;
            }

            pickup.SetItem(
                GetProxy(BeastShardSourceManifest.CragglerLocation),
                keepPersistence: true
            );
        }

        private static CollectableItemPickup
            FindPreinstantiatedCragglerCorpsePickup(
                EnemyDeathEffects deathEffects)
        {
            if (deathEffects == null ||
                CragglerInstantiatedCorpsesField == null)
            {
                return null;
            }

            GameObject[] corpses =
                CragglerInstantiatedCorpsesField.GetValue(deathEffects)
                as GameObject[];
            if (corpses == null || corpses.Length == 0)
            {
                return null;
            }

            return BeastShardSourceManifest.FindCragglerCorpsePickup(
                corpses[0]
            );
        }

        [HarmonyPatch(typeof(SprintRaceController), "get_Reward")]
        private static class SprintmasterTrackTwoRewardPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.First)]
            private static void Postfix(
                SprintRaceController __instance,
                ref SavedItem __result)
            {
                // Both private event names must match so a changed controller
                // layout cannot redirect this patch to another race reward.
                if (SprintRaceEndEventField == null ||
                    SprintRaceEndCompleteEventField == null)
                {
                    return;
                }

                string raceEndEvent = SprintRaceEndEventField.GetValue(
                    __instance
                ) as string;
                string raceEndCompleteEvent =
                    SprintRaceEndCompleteEventField.GetValue(
                        __instance
                    ) as string;
                if (!BeastShardSourceManifest.
                        IsExpectedSprintmasterTrackTwo(
                            __instance,
                            __result,
                            raceEndEvent,
                            raceEndCompleteEvent
                        ) ||
                    !IsActive(
                        BeastShardSourceManifest.SprintmasterLocation
                    ))
                {
                    return;
                }

                __result = GetProxy(
                    BeastShardSourceManifest.SprintmasterLocation
                );
            }
        }
    }
}
