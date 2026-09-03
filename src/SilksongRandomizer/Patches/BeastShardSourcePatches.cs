using System;
using System.Collections;
using System.Collections.Generic;
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

        private static readonly FieldInfo ItemDropGroupsField =
            AccessTools.Field(
                typeof(HealthManager),
                "itemDropGroups"
            );

        private static readonly MethodInfo SpawnItemDropMethod =
            AccessTools.Method(
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
            );

        private static readonly Dictionary<string, int>
            PendingDropSourceInstances =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase
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
            "Die",
            new Type[]
            {
                typeof(float?),
                typeof(AttackTypes),
                typeof(NailElements),
                typeof(GameObject),
                typeof(bool),
                typeof(float),
                typeof(bool),
                typeof(bool),
            }
        )]
        private static class ExactEnemyPreDeathPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(HealthManager __instance)
            {
                if (!BeastShardSourceManifest.TryGetEnemyDropLocation(
                        __instance,
                        out string locationName) ||
                    !IsActive(locationName))
                {
                    return;
                }

                if (!TryReplaceConfiguredDrop(
                        __instance,
                        locationName))
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Could not arm scripted Beast Shard " +
                        "check: " + locationName
                    );
                }
            }
        }

        private static bool TryReplaceConfiguredDrop(
            HealthManager healthManager,
            string locationName)
        {
            if (healthManager == null || ItemDropGroupsField == null)
            {
                return false;
            }

            IEnumerable groups = ItemDropGroupsField.GetValue(
                healthManager
            ) as IEnumerable;
            if (groups == null)
            {
                return false;
            }

            List<Tuple<object, FieldInfo>> matches =
                new List<Tuple<object, FieldInfo>>();
            foreach (object group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                FieldInfo dropsField = AccessTools.Field(
                    group.GetType(),
                    "Drops"
                );
                IEnumerable drops = dropsField == null
                    ? null
                    : dropsField.GetValue(group) as IEnumerable;
                if (drops == null)
                {
                    continue;
                }

                foreach (object drop in drops)
                {
                    if (TryGetExpectedConfiguredDrop(
                            drop,
                            locationName,
                            out FieldInfo itemField))
                    {
                        matches.Add(Tuple.Create(drop, itemField));
                    }
                }
            }

            if (matches.Count != 1)
            {
                return false;
            }

            matches[0].Item2.SetValue(
                matches[0].Item1,
                GetProxy(locationName)
            );
            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Armed scripted Beast Shard check: " +
                locationName
            );
            return true;
        }

        private static bool TryGetExpectedConfiguredDrop(
            object drop,
            string locationName,
            out FieldInfo itemField)
        {
            itemField = null;
            if (drop == null)
            {
                return false;
            }

            Type dropType = drop.GetType();
            FieldInfo nativeItemField = AccessTools.Field(
                dropType,
                "item"
            );
            FieldInfo amountField = AccessTools.Field(
                dropType,
                "Amount"
            );
            FieldInfo customPickupField = AccessTools.Field(
                dropType,
                "CustomPickupPrefab"
            );
            FieldInfo limitField = AccessTools.Field(
                dropType,
                "LimitActiveInScene"
            );
            if (nativeItemField == null ||
                amountField == null ||
                customPickupField == null ||
                limitField == null)
            {
                return false;
            }

            SavedItem item = nativeItemField.GetValue(drop) as SavedItem;
            object amount = amountField.GetValue(drop);
            if (item == null || amount == null)
            {
                return false;
            }

            FieldInfo startField = AccessTools.Field(
                amount.GetType(),
                "Start"
            );
            FieldInfo endField = AccessTools.Field(
                amount.GetType(),
                "End"
            );
            if (startField == null || endField == null)
            {
                return false;
            }

            bool matches =
                IsExpectedEnemyDropItem(item, locationName) &&
                startField.GetValue(amount) is int start &&
                start == 1 &&
                endField.GetValue(amount) is int end &&
                end == 1 &&
                customPickupField.GetValue(drop) == null &&
                limitField.GetValue(drop) is int limit &&
                limit == 0;
            if (matches)
            {
                itemField = nativeItemField;
            }
            return matches;
        }

        private static bool IsExpectedEnemyDropItem(
            SavedItem item,
            string locationName)
        {
            return item != null &&
                (
                    string.Equals(
                        item.name,
                        BeastShardSourceManifest.NativeItemName,
                        StringComparison.Ordinal
                    ) ||
                    ReferenceEquals(item, GetProxy(locationName))
                );
        }

        private static void ArmPendingEnemyDrop(
            HealthManager healthManager,
            string locationName)
        {
            SaveState state = SaveState.Instance;
            if (state == null || healthManager == null)
            {
                return;
            }

            if (state.pendingBeastShardDrops == null)
            {
                state.pendingBeastShardDrops =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            state.pendingBeastShardDrops.Add(locationName);
            PendingDropSourceInstances[locationName] =
                healthManager.GetInstanceID();
        }

        private static bool TryRespawnPendingEnemyDrop(
            HealthManager healthManager,
            string locationName)
        {
            if (SpawnItemDropMethod == null ||
                GlobalSettings.Gameplay.CollectableItemPickupInstantPrefab == null)
            {
                return false;
            }

            try
            {
                SpawnItemDropMethod.Invoke(
                    healthManager,
                    new object[]
                    {
                        GetProxy(locationName),
                        1,
                        null,
                        healthManager.transform,
                        0,
                    }
                );
                return true;
            }
            catch (Exception exception)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Could not restore loose Beast Shard " +
                    "check " + locationName + ": " + exception
                );
                return false;
            }
        }

        private static bool TryRecoverConsumedEnemySource(
            HealthManager healthManager)
        {
            SaveState state = SaveState.Instance;
            if (healthManager == null ||
                state == null ||
                !healthManager.isDead ||
                !BeastShardSourceManifest.TryGetEnemyDropLocation(
                    healthManager,
                    out string locationName) ||
                !IsActive(locationName))
            {
                return false;
            }

            if (state.IsLocationChecked(locationName))
            {
                state.pendingBeastShardDrops?.Remove(locationName);
                PendingDropSourceInstances.Remove(locationName);
                return false;
            }

            if (state.pendingBeastShardDrops != null &&
                state.pendingBeastShardDrops.Contains(locationName))
            {
                int sourceInstance = healthManager.GetInstanceID();
                if (PendingDropSourceInstances.TryGetValue(
                        locationName,
                        out int restoredInstance) &&
                    restoredInstance == sourceInstance)
                {
                    return false;
                }

                if (!TryRespawnPendingEnemyDrop(
                        healthManager,
                        locationName))
                {
                    return false;
                }

                PendingDropSourceInstances[locationName] = sourceInstance;
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] Restored loose Beast Shard check: " +
                    locationName
                );
                return true;
            }

            state.CheckLocation(locationName);
            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Recovered legacy Beast Shard check: " +
                locationName
            );
            return true;
        }

        private static void TryRecoverCompletedFlagSource(
            string locationName,
            bool completed)
        {
            SaveState state = SaveState.Instance;
            if (!completed ||
                state == null ||
                !IsActive(locationName) ||
                state.IsLocationChecked(locationName))
            {
                return;
            }

            state.CheckLocation(locationName);
            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Recovered consumed Beast Shard check: " +
                locationName
            );
        }

        private static void TryRecoverCompletedFlagSources()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return;
            }

            TryRecoverCompletedFlagSource(
                BeastShardSourceManifest.CragglerLocation,
                playerData.roofCrabDefeated
            );
            TryRecoverCompletedFlagSource(
                BeastShardSourceManifest.SprintmasterLocation,
                playerData.SprintMasterExtraRaceWon
            );
        }

        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.OnAwake))]
        private static class ExactEnemyRecoveryPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(HealthManager __instance)
            {
                TryRecoverConsumedEnemySource(__instance);
            }
        }

        [HarmonyPatch(
            typeof(Archipelago),
            nameof(Archipelago.SynchronizeSaveState)
        )]
        private static class ExactEnemyConnectionRecoveryPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                HealthManager[] healthManagers =
                    Resources.FindObjectsOfTypeAll<HealthManager>();
                foreach (HealthManager healthManager in healthManagers)
                {
                    if (healthManager == null ||
                        !healthManager.gameObject.scene.IsValid())
                    {
                        continue;
                    }

                    TryRecoverConsumedEnemySource(healthManager);
                }

                TryRecoverCompletedFlagSources();
            }
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
                        out string locationName) ||
                    !IsActive(locationName) ||
                    count != 1 ||
                    prefab != null ||
                    limit != 0 ||
                    !IsExpectedEnemyDropItem(dropItem, locationName))
                {
                    return;
                }

                ArmPendingEnemyDrop(__instance, locationName);
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
