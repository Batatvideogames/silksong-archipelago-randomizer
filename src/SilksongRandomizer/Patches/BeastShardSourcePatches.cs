using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

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
                LocationSet.GetCanonicalLocationName(locationName),
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

        private static bool HasEnemyDropPickup(
            string locationName,
            string sceneName)
        {
            SavedItem proxy = GetProxy(locationName);
            CollectableItemPickup[] pickups =
                Resources.FindObjectsOfTypeAll<CollectableItemPickup>();
            foreach (CollectableItemPickup pickup in pickups)
            {
                if (pickup == null ||
                    !pickup.gameObject.scene.IsValid() ||
                    !pickup.gameObject.activeInHierarchy ||
                    !string.Equals(
                        pickup.gameObject.scene.name,
                        sceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ReferenceEquals(pickup.Item, proxy))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryRespawnEnemyDrop(
            HealthManager healthManager,
            string locationName)
        {
            CollectableItemPickup prefab =
                GlobalSettings.Gameplay.CollectableItemPickupInstantPrefab;
            if (prefab == null)
            {
                return false;
            }

            CollectableItemPickup pickup = null;
            try
            {
                pickup = UnityEngine.Object.Instantiate(prefab);
                Vector3 position = healthManager.transform.TransformPoint(
                    healthManager.EffectOrigin
                );
                position.z = UnityEngine.Random.Range(0.003f, 0.0039f);
                pickup.transform.position = position;
                pickup.SetItem(GetProxy(locationName));
                return true;
            }
            catch (Exception exception)
            {
                if (pickup != null)
                {
                    UnityEngine.Object.Destroy(pickup.gameObject);
                }
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Could not restore loose Beast Shard " +
                    "check " + locationName + ": " + exception
                );
                return false;
            }
        }

        private static bool TryGetConsumedEnemyDropLocation(
            HealthManager healthManager,
            out string locationName)
        {
            locationName = null;
            if (healthManager == null)
            {
                return false;
            }

            if (healthManager.isDead &&
                BeastShardSourceManifest.TryGetEnemyDropLocation(
                    healthManager,
                    out locationName))
            {
                return true;
            }

            PlayerData playerData = PlayerData.instance;
            EnemyDeathEffects deathEffects =
                healthManager.GetComponent<EnemyDeathEffects>();
            if (playerData != null &&
                playerData.roofCrabDefeated &&
                BeastShardSourceManifest.IsExpectedCraggler(deathEffects))
            {
                locationName = BeastShardSourceManifest.CragglerLocation;
                return true;
            }

            return false;
        }

        private static bool TryRecoverConsumedEnemySource(
            HealthManager healthManager)
        {
            SaveState state = SaveState.Instance;
            if (healthManager == null ||
                state == null ||
                !TryGetConsumedEnemyDropLocation(
                    healthManager,
                    out string locationName) ||
                !IsActive(locationName))
            {
                return false;
            }

            string canonicalName =
                LocationSet.GetCanonicalLocationName(locationName);
            if (string.IsNullOrWhiteSpace(canonicalName) ||
                state.IsLocationChecked(canonicalName))
            {
                return false;
            }

            if (HasEnemyDropPickup(
                    canonicalName,
                    healthManager.gameObject.scene.name))
            {
                return false;
            }

            if (!TryRespawnEnemyDrop(healthManager, canonicalName))
            {
                return false;
            }

            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Restored loose Beast Shard check: " +
                canonicalName
            );
            return true;
        }

        private static void TryRecoverActiveSceneEnemyDrops()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return;
            }

            HealthManager[] healthManagers =
                Resources.FindObjectsOfTypeAll<HealthManager>();
            foreach (HealthManager healthManager in healthManagers)
            {
                if (healthManager == null ||
                    !healthManager.gameObject.scene.IsValid() ||
                    !string.Equals(
                        healthManager.gameObject.scene.name,
                        scene.name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TryRecoverConsumedEnemySource(healthManager);
            }
        }

        private static void WatchForPersistedEnemyDeath(
            HealthManager healthManager)
        {
            if (healthManager == null ||
                !BeastShardSourceManifest.TryGetEnemyDropLocation(
                    healthManager,
                    out string locationName) ||
                !IsActive(locationName))
            {
                return;
            }

            Action recover = null;
            recover = delegate
            {
                healthManager.StartedDead -= recover;
                TryRecoverConsumedEnemySource(healthManager);
            };
            healthManager.StartedDead += recover;

            if (healthManager.isDead)
            {
                recover();
            }
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
                BeastShardSourceManifest.SprintmasterLocation,
                playerData.SprintMasterExtraRaceWon
            );
        }

        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.OnAwake))]
        private static class ExactEnemyRecoveryPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(
                HealthManager __instance,
                bool __result)
            {
                if (__result)
                {
                    WatchForPersistedEnemyDeath(__instance);
                }
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
                TryRecoverActiveSceneEnemyDrops();
                TryRecoverCompletedFlagSources();
            }
        }

        [HarmonyPatch(
            typeof(GameManager),
            nameof(GameManager.FinishedEnteringScene)
        )]
        private static class ExactEnemySceneEntryRecoveryPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                TryRecoverActiveSceneEnemyDrops();
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
