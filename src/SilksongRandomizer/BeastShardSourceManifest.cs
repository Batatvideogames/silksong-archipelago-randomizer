using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class BeastShardSourceManifest
    {
        internal const string NativeItemName = "Great Shard";
        internal const string MoorwingLocation = "Beast Shard: Moorwing";
        internal const string CragglerLocation = "Beast Shard: Craggler";
        internal const string SprintmasterLocation =
            "Beast Shard: Sprintmaster";

        internal static readonly string[] LocationNames =
        {
            "Beast Shard: Marrowmaw",
            CragglerLocation,
            "Beast Shard: Pilgrim's Rest",
            "Beast Shard: Memorium",
            SprintmasterLocation,
            MoorwingLocation,
        };

        internal static IEnumerable<Location> AppendTo(
            IEnumerable<Location> existing)
        {
            HashSet<string> names = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );
            if (existing != null)
            {
                foreach (Location location in existing)
                {
                    if (location == null)
                    {
                        continue;
                    }

                    names.Add(location.Name);
                    yield return location;
                }
            }

            foreach (string locationName in LocationNames)
            {
                if (names.Add(locationName))
                {
                    yield return new Location(
                        locationName,
                        ItemType.Resource,
                        () => false
                    );
                }
            }
        }

        internal sealed class EnemyDropSource
        {
            internal readonly string LocationName;
            internal readonly string SceneName;
            internal readonly string HierarchyPath;

            internal EnemyDropSource(
                string locationName,
                string sceneName,
                string hierarchyPath)
            {
                LocationName = locationName;
                SceneName = sceneName;
                HierarchyPath = hierarchyPath;
            }
        }

        // These are the four HealthManager-backed source objects. The two
        // Pilgrim's Rest objects are mutually exclusive world-state aliases
        // for one physical reward and therefore share one AP location.
        internal static readonly EnemyDropSource[] EnemyDropSources =
        {
            new EnemyDropSource(
                "Beast Shard: Marrowmaw",
                "Tut_01",
                "Black Thread States Thread Only Variant/Normal World/" +
                    "Bone Thumper"
            ),
            new EnemyDropSource(
                "Beast Shard: Pilgrim's Rest",
                "Bone_East_10_Church",
                "Rhino Scene/Rhino"
            ),
            new EnemyDropSource(
                "Beast Shard: Pilgrim's Rest",
                "Bone_East_10_Room",
                "Black Thread States/Normal World/Rhino"
            ),
            new EnemyDropSource(
                "Beast Shard: Memorium",
                "Arborium_02",
                "Rhino Scene/Rhino"
            ),
        };

        internal static bool TryGetEnemyDropLocation(
            HealthManager healthManager,
            SavedItem dropItem,
            int count,
            CollectableItemPickup customPickupPrefab,
            int limit,
            out string locationName)
        {
            locationName = null;
            if (healthManager == null ||
                dropItem == null ||
                !string.Equals(
                    dropItem.name,
                    NativeItemName,
                    StringComparison.Ordinal) ||
                count != 1 ||
                customPickupPrefab != null ||
                limit != 0)
            {
                return false;
            }

            return TryGetEnemyDropLocation(
                healthManager,
                out locationName
            );
        }

        internal static bool TryGetEnemyDropLocation(
            HealthManager healthManager,
            out string locationName)
        {
            locationName = null;
            if (healthManager == null)
            {
                return false;
            }

            string sceneName = healthManager.gameObject.scene.name;
            string hierarchyPath = Utils.GetHierarchyPath(
                healthManager.transform
            );
            EnemyDropSource match = null;
            foreach (EnemyDropSource source in EnemyDropSources)
            {
                if (!string.Equals(
                        sceneName,
                        source.SceneName,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        hierarchyPath,
                        source.HierarchyPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                // An ambiguous identity is less safe than leaving the native
                // reward alone.
                if (match != null &&
                    !string.Equals(
                        match.LocationName,
                        source.LocationName,
                        StringComparison.Ordinal))
                {
                    return false;
                }
                match = source;
            }

            if (match == null)
            {
                return false;
            }

            locationName = match.LocationName;
            return true;
        }

        internal static bool IsExpectedCraggler(
            EnemyDeathEffects deathEffects)
        {
            if (deathEffects == null ||
                !string.Equals(
                    deathEffects.gameObject.scene.name,
                    "Crawl_04",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    Utils.GetHierarchyPath(deathEffects.transform),
                    "Roof Crab",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    deathEffects.setPlayerDataBool,
                    "roofCrabDefeated",
                    StringComparison.Ordinal))
            {
                return false;
            }

            GameObject corpsePrefab = deathEffects.CorpsePrefab;
            if (corpsePrefab == null ||
                !string.Equals(
                    NormalizeCloneName(corpsePrefab.name),
                    "Corpse Roof Crab",
                    StringComparison.Ordinal))
            {
                return false;
            }

            CollectableItemPickup pickup = FindCragglerCorpsePickup(
                corpsePrefab
            );
            return pickup != null &&
                   pickup.Item != null &&
                   string.Equals(
                       pickup.Item.name,
                       NativeItemName,
                       StringComparison.Ordinal
                   );
        }

        internal static CollectableItemPickup FindCragglerCorpsePickup(
            GameObject corpseRoot)
        {
            if (corpseRoot == null ||
                !string.Equals(
                    NormalizeCloneName(corpseRoot.name),
                    "Corpse Roof Crab",
                    StringComparison.Ordinal))
            {
                return null;
            }

            Transform pickupTransform = corpseRoot.transform.Find(
                "Chunks/Collectable Item Pickup Instant"
            );
            if (pickupTransform == null)
            {
                return null;
            }

            return pickupTransform.GetComponent<CollectableItemPickup>();
        }

        internal static bool IsExpectedCragglerCorpsePickup(
            CollectableItemPickup pickup,
            SavedItem item)
        {
            if (pickup == null ||
                item == null ||
                !string.Equals(
                    pickup.gameObject.scene.name,
                    "Crawl_04",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    item.name,
                    NativeItemName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            Transform pickupTransform = pickup.transform;
            Transform chunks = pickupTransform.parent;
            Transform corpseRoot = chunks == null ? null : chunks.parent;
            return string.Equals(
                       pickupTransform.name,
                       "Collectable Item Pickup Instant",
                       StringComparison.Ordinal) &&
                   chunks != null &&
                   string.Equals(
                       chunks.name,
                       "Chunks",
                       StringComparison.Ordinal) &&
                   corpseRoot != null &&
                   string.Equals(
                       NormalizeCloneName(corpseRoot.name),
                       "Corpse Roof Crab",
                       StringComparison.Ordinal
                   );
        }

        internal static bool TryGetCorpseLocation(EnemyDeathEffects deathEffects, out string locationName)
        {
            locationName = null;
            if (IsExpectedCraggler(deathEffects))
            {
                locationName = CragglerLocation;
                return true;
            }
            if (deathEffects == null ||
                deathEffects.setPlayerDataBool != "defeatedVampireGnatBoss") return false;
            string scene = deathEffects.gameObject.scene.name;
            string path = Utils.GetHierarchyPath(deathEffects.transform);
            if (!((scene == "Greymoor_05_boss" && path == "Vampire Gnat Boss Scene/Vampire Gnat") ||
                  (scene == "Greymoor_08_boss" && path == "Vampire Gnat Scene/Vampire Gnat"))) return false;
            GameObject corpse = deathEffects.CorpsePrefab;
            if (corpse == null || NormalizeCloneName(corpse.name) != "Vampire Gnat Corpse") return false;
            CollectableItemPickup pickup = FindRewardCorpsePickup(corpse);
            if (pickup == null || pickup.Item == null || pickup.Item.name != NativeItemName) return false;
            locationName = MoorwingLocation;
            return true;
        }

        internal static CollectableItemPickup FindRewardCorpsePickup(GameObject corpseRoot)
        {
            if (corpseRoot != null && NormalizeCloneName(corpseRoot.name) == "Vampire Gnat Corpse")
                return corpseRoot.transform.Find("Collectable Item Pickup Instant")?.GetComponent<CollectableItemPickup>();
            return FindCragglerCorpsePickup(corpseRoot);
        }

        internal static bool TryGetCorpsePickupLocation(CollectableItemPickup pickup, SavedItem item, out string locationName)
        {
            locationName = null;
            if (IsExpectedCragglerCorpsePickup(pickup, item))
            {
                locationName = CragglerLocation;
                return true;
            }
            if (pickup == null || item == null || item.name != NativeItemName ||
                pickup.name != "Collectable Item Pickup Instant" || pickup.transform.parent == null ||
                NormalizeCloneName(pickup.transform.parent.name) != "Vampire Gnat Corpse") return false;
            string scene = pickup.gameObject.scene.name;
            if (scene != "Greymoor_05" && scene != "Greymoor_08" &&
                scene != "Greymoor_05_boss" && scene != "Greymoor_08_boss") return false;
            locationName = MoorwingLocation;
            return true;
        }

        internal static bool IsExpectedSprintmasterTrackTwo(
            SprintRaceController controller,
            SavedItem reward,
            string raceEndEvent,
            string raceEndCompleteEvent)
        {
            return controller != null &&
                   reward != null &&
                   string.Equals(
                       reward.name,
                       NativeItemName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       controller.gameObject.scene.name,
                       "Sprintmaster_Cave",
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       Utils.GetHierarchyPath(controller.transform),
                       "Race Group/Tracks/Track 2/" +
                           "Track 2 Race Control",
                       StringComparison.Ordinal) &&
                   controller.LapCount == 3 &&
                   string.Equals(
                       raceEndEvent,
                       "RACE ENDED",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       raceEndCompleteEvent,
                       "RACE END COMPLETE",
                       StringComparison.Ordinal);
        }

        private static string NormalizeCloneName(string value)
        {
            const string cloneSuffix = "(Clone)";
            if (string.IsNullOrEmpty(value) ||
                !value.EndsWith(
                    cloneSuffix,
                    StringComparison.Ordinal))
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, value.Length - cloneSuffix.Length);
        }
    }
}
