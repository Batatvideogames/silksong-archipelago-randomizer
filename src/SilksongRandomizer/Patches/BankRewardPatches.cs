using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongRandomizer.Patches
{
    internal static class BankRewardPatches
    {
        private const string SceneName = "hang_06_bank";
        private const string SourceRoot = "Thief Scene Control";
        private const string SourceBranch = "Thieves Not Here/";
        private sealed class Reward
        {
            internal readonly string Location;
            internal readonly string Path;
            internal readonly bool LostInActThree;
            internal Transform Source;
            internal GameObject Pickup;

            internal Reward(string location, string path, bool lostInActThree = false)
            {
                Location = location;
                Path = SourceBranch + path;
                LostInActThree = lostInActThree;
            }
        }

        private static readonly Reward[] Rewards =
        {
            new Reward("High Halls - Rosary Cache #1", "rosary_string_small", true),
            new Reward("High Halls - Rosary Cache #2", "rosary_string_large_mixed Variant", true),
            new Reward("High Halls - Rosary Cache #3", "rosary_string_small_half", true),
            new Reward("High Halls - Rosary Cache #4", "rosary_string_medium", true),
            new Reward("High Halls - Rosary Cache #5", "rosary_string_small_half (1)"),
            new Reward("High Halls - Rosary Cache #6", "rosary_string_large_full Variant"),
            new Reward("High Halls - Pale Rosary Necklace", "Chest/Item/Collectable Item Pickup"),
        };
        private static int sceneHandle = -1;
        private static Transform sourceRoot;
        private static bool loggedFailure;

        private static bool MatchesSource(Reward reward, Transform source)
        {
            if (source == null) return false;
            string location;
            if (source.GetComponent<RosaryCacheString>() != null)
            {
                location = MinorCachePatches.FindExactSource(SceneName, source.name,
                    source.position, MinorCacheManifest.SourceKind.RosaryCache)?.LocationName;
            }
            else if (source.GetComponent<CollectableItemPickup>() != null)
            {
                location = MinorPickupPatches.FindExactSource(SceneName,
                    "Rosary_Set_Huge_White", source.position)?.LocationName;
            }
            else return false;
            return location == reward.Location;
        }

        private static void RemovePickup(Reward reward)
        {
            if (reward.Pickup != null)
            {
                reward.Pickup.SetActive(false);
                UnityEngine.Object.Destroy(reward.Pickup);
            }
            reward.Pickup = null;
        }

        private static void CreatePickup(Reward reward, int index, Scene scene)
        {
            if (reward.Pickup != null) return;
            GameObject replacement = null;
            try
            {
                replacement = UnityEngine.Object.Instantiate(
                    GlobalSettings.Gameplay.CollectableItemPickupPrefab.gameObject);
                replacement.name = "AP Bank Reward - " + reward.Location;
                SceneManager.MoveGameObjectToScene(replacement, scene);
                // The vault floor left of the central shaft survives both acts.
                replacement.transform.position = new Vector3(71.5f + index * 1.0f, 25.5f, 0f);
                CollectableItemPickup pickup = replacement.GetComponent<CollectableItemPickup>();
                if (pickup == null)
                {
                    UnityEngine.Object.Destroy(replacement);
                    return;
                }
                pickup.SetItem(MinorPickupPatches.GetProxyItem(reward.Location));
                reward.Pickup = replacement;
            }
            catch (Exception ex)
            {
                if (replacement != null) UnityEngine.Object.Destroy(replacement);
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Could not restore a bank reward: " + ex.Message);
                }
            }
        }

        internal static void Update()
        {
            Scene scene = SceneManager.GetActiveScene();
            SaveState state = SaveState.Instance;
            PlayerData player = PlayerData.instance;
            if (!scene.IsValid() || !string.Equals(scene.name, SceneName, StringComparison.OrdinalIgnoreCase) ||
                state?.IsRoomBound != true || !state.IsRandomized(ItemType.Resource) ||
                player == null || !player.bankOpened)
            {
                if (sceneHandle != -1) Reset();
                return;
            }
            if (sceneHandle != scene.handle)
            {
                Reset();
                sceneHandle = scene.handle;
            }
            if (sourceRoot == null)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == SourceRoot) { sourceRoot = root.transform; break; }
            }
            for (int i = 0; i < Rewards.Length; i++)
            {
                Reward reward = Rewards[i];
                bool unavailable = player.rosaryThievesInBank ||
                    (player.blackThreadWorld && reward.LostInActThree);
                if (!unavailable || !state.IsLocationEnabled(reward.Location) ||
                    !state.IsLocationInSeed(reward.Location) || state.IsLocationChecked(reward.Location))
                {
                    RemovePickup(reward);
                    continue;
                }
                if (reward.Source == null && sourceRoot != null)
                {
                    Transform candidate = sourceRoot.Find(reward.Path);
                    if (MatchesSource(reward, candidate)) reward.Source = candidate;
                }
                if (reward.Source != null && !reward.Source.gameObject.activeInHierarchy)
                    CreatePickup(reward, i, scene);
                else RemovePickup(reward);
            }
        }

        internal static void Reset()
        {
            foreach (Reward reward in Rewards)
            {
                RemovePickup(reward);
                reward.Source = null;
            }
            sourceRoot = null;
            sceneHandle = -1;
        }
    }
}
