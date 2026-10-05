using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using SilksongRandomizer.AlphabetMode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Intercepts collectible sources which do not use one uniform vanilla
    /// pickup implementation. Each patch keeps the source's own persistence
    /// and presentation while replacing only its native reward grant.
    /// </summary>
    internal static class CollectibleSourcePatches
    {
        private const string MossberryAssetName = "Mossberry";
        private const string PollipHeartAssetName = "Shell Flower";
        private const string SilkeaterAssetName = "Silk Grub";
        private const string RockRollersQuestName = "Rock Rollers";
        private const string PilgrimRagsQuestName = "Pilgrim Rags";
        private const string PilgrimRagsLocationName =
            "Wish: Garb of the Pilgrims";
        private const string PilgrimRagCollectableName = "Pilgrim Rag";

        [ThreadStatic]
        private static string activeSilkeaterLocation;

        private static readonly ConditionalWeakTable<
            PersistentBoolItem,
            CollectibleSourceManifest.CocoonEntry>
                SilkeaterCocoonByPersistence =
                    new ConditionalWeakTable<
                        PersistentBoolItem,
                        CollectibleSourceManifest.CocoonEntry>();

        private static readonly Dictionary<string, ArchipelagoSourceItem>
            ProxyByLocation =
                new Dictionary<string, ArchipelagoSourceItem>(
                    StringComparer.OrdinalIgnoreCase
                );

        private static readonly Dictionary<string, Tuple<string, ItemType>>
            QuestRewardSources =
                new Dictionary<string, Tuple<string, ItemType>>(
                    StringComparer.Ordinal
                )
                {
                    {
                        LocationSet.DriftersCloakQuestAssetName,
                        Tuple.Create(
                            LocationSet.DriftersCloakLocationName,
                            ItemType.Skill
                        )
                    },
                    {
                        "Crow Feathers",
                        Tuple.Create(
                            CoreLocationManifest.
                                CrowFeathersCraftingKitLocation,
                            ItemType.Upgrade
                        )
                    },
                    {
                        "Extractor Blue",
                        Tuple.Create(
                            "Tool Unlock: Lifeblood Syringe",
                            ItemType.Tool
                        )
                    },
                    {
                        "Great Gourmand",
                        Tuple.Create(
                            "Pale Oil: Great Taste of Pharloom",
                            ItemType.PaleOil
                        )
                    },
                    {
                        "Huntress Quest",
                        Tuple.Create(
                            "Tool Unlock: Longneedle",
                            ItemType.Tool
                        )
                    },
                    {
                        "Huntress Quest Runt",
                        Tuple.Create(
                            "Tool Unlock: Longneedle",
                            ItemType.Tool
                        )
                    },
                    {
                        "Journal",
                        Tuple.Create(
                            ToolPouchLocationManifest.
                                BugsOfPharloomLocation,
                            ItemType.ToolPouch
                        )
                    },
                    {
                        "Pinstress Battle",
                        Tuple.Create(
                            "Tool Unlock: Pinstress Tool",
                            ItemType.Tool
                        )
                    },
                    {
                        "Roach Killing",
                        Tuple.Create(
                            "Tool Unlock: Tack",
                            ItemType.Tool
                        )
                    },
                    {
                        "Rock Rollers",
                        Tuple.Create(
                            CollectibleSourceManifest.
                                VolatileFlintbeetlesLocket,
                            ItemType.MemoryLocket
                        )
                    },
                    {
                        "Save the Fleas",
                        Tuple.Create(
                            "Tool Unlock: Flea Brew",
                            ItemType.Tool
                        )
                    },
                    {
                        "Shakra Final Quest",
                        Tuple.Create(
                            "Tool Unlock: Shakra Ring",
                            ItemType.Tool
                        )
                    },
                    {
                        "Shell Flowers",
                        Tuple.Create(
                            "Tool Unlock: Poison Pouch",
                            ItemType.Tool
                        )
                    },
                };

        private struct SilkeaterCallState
        {
            internal string PreviousLocation;
        }

        private sealed class ArchipelagoSourceItem : SavedItem
        {
            internal string LocationName;
            internal ItemType Type;
            internal bool ShowGenericPresentation;

            public override bool CanGetMultipleAtOnce => false;

            public override void Get(bool showPopup = true)
            {
                SaveState state = SaveState.Instance;
                if (IsActive(state, LocationName, Type) &&
                    !state.IsLocationChecked(LocationName))
                {
                    state.CheckLocation(LocationName);
                    if (state.IsLocationChecked(LocationName))
                    {
                        MarkPhysicalSourceCollected(LocationName);
                    }
                }
            }

            public override bool CanGetMore()
            {
                SaveState state = SaveState.Instance;
                return IsActive(state, LocationName, Type) &&
                       !state.IsLocationChecked(LocationName);
            }

            public override Sprite GetPopupIcon()
            {
                return ShowGenericPresentation
                    ? ItemPreview.Get(LocationName, false, true)?.Icon ??
                        RandomizerPlugin.Instance?.MapCheckIcon ?? RandomizerPlugin.Instance?.ArchipelagoIcon
                    : null;
            }

            public override string GetPopupName()
            {
                return ShowGenericPresentation
                    ? AlphabetModeManager.FilterDirectText(
                        ItemPreview.Get(LocationName, false, true)?.Name ?? "Archipelago Item"
                    )
                    : null;
            }

            public override int GetSavedAmount()
            {
                SaveState state = SaveState.Instance;
                return state != null &&
                       state.IsLocationChecked(LocationName)
                    ? 1
                    : 0;
            }

            public override bool HasUpgradeIcon()
            {
                return false;
            }

            public override bool GetTakesHeroControl()
            {
                return false;
            }

            public override void SetupExtraDescription(GameObject obj)
            {
            }

            public override void SetHasNew(bool hasPopup)
            {
            }
        }

        private static bool IsActive(
            SaveState state,
            string locationName,
            ItemType type)
        {
            return state != null &&
                   state.IsRandomized(type) &&
                   state.IsLocationEnabled(locationName) &&
                   state.IsLocationInSeed(locationName);
        }

        internal static SavedItem GetProxyItem(
            string locationName,
            ItemType type,
            bool showGenericPresentation = false)
        {
            string proxyKey = locationName +
                              (showGenericPresentation
                                  ? "\0generic"
                                  : "\0silent");
            if (!ProxyByLocation.TryGetValue(
                    proxyKey,
                    out ArchipelagoSourceItem proxy) ||
                proxy == null)
            {
                proxy =
                    ScriptableObject.CreateInstance<
                        ArchipelagoSourceItem>();
                proxy.name = "Archipelago Location - " + locationName;
                proxy.LocationName = locationName;
                proxy.Type = type;
                proxy.ShowGenericPresentation =
                    showGenericPresentation;
                ProxyByLocation[proxyKey] = proxy;
            }
            return proxy;
        }

        [HarmonyPatch(typeof(CollectableItemBasic), "SetUniqueBool")]
        private static class MementoSourceFlag
        {
            [HarmonyPrefix]
            private static bool Prefix(CollectableItemBasic __instance)
            {
                string location = __instance.name == "Grey Memento" ? "Grey Memento" :
                    __instance.name == "Sprintmaster Memento" ? "Sprintmaster's Memento" : null;
                return location == null || !IsActive(SaveState.Instance, location, ItemType.Memento);
            }
        }

        [HarmonyPatch(typeof(CollectableItem), nameof(CollectableItem.Get))]
        private static class SprintmasterMementoReward
        {
            [HarmonyPrefix]
            private static bool Prefix(CollectableItem __instance)
            {
                if (__instance.name != "Sprintmaster Memento" ||
                    GameManager.UnsafeInstance?.GetSceneNameString() != "Sprintmaster_Cave" ||
                    !IsActive(SaveState.Instance, "Sprintmaster's Memento", ItemType.Memento))
                    return true;
                GetProxyItem("Sprintmaster's Memento", ItemType.Memento, true).Get();
                return false;
            }
        }

        private static bool IsSethMementoAction(FsmStateAction action)
        {
            return action.Fsm?.GameObject != null &&
                action.Fsm.GameObject.scene.name == "Aqueduct_05_festival" &&
                action.Fsm.GameObject.name == "Seth Sit NPC Fleatopia" &&
                action.Fsm.Name == "Dialogue" &&
                IsActive(SaveState.Instance, "Guardian's Memento", ItemType.Memento);
        }

        [HarmonyPatch(typeof(SavedItemCanGetMore), nameof(SavedItemCanGetMore.IsTrue), MethodType.Getter)]
        private static class SethMementoAvailability
        {
            [HarmonyPostfix]
            private static void Postfix(SavedItemCanGetMore __instance, ref bool __result)
            {
                if (IsSethMementoAction(__instance) && __instance.State?.Name == "Convo Choice" &&
                    (__instance.Item?.Value as SavedItem)?.name == "Memento Seth")
                    __result = !SaveState.Instance.IsLocationChecked("Guardian's Memento");
            }
        }

        [HarmonyPatch(typeof(CollectableItemCollect), "DoAction")]
        private static class SethMementoReward
        {
            [HarmonyPrefix]
            private static bool Prefix(CollectableItemCollect __instance, CollectableItem item)
            {
                if (!IsSethMementoAction(__instance) || __instance.State?.Name != "Award Memento?" ||
                    item?.name != "Memento Seth")
                    return true;
                GetProxyItem("Guardian's Memento", ItemType.Memento, true).Get();
                return false;
            }
        }

        private static void MarkPhysicalSourceCollected(
            string locationName)
        {
            if (PlayerData.instance != null)
            {
                if (locationName == "Grey Memento") PlayerData.instance.CollectedMementoGrey = true;
                if (locationName == "Sprintmaster's Memento") PlayerData.instance.CollectedMementoSprintmaster = true;
            }
            // White Key's Act 3 fallback uses this separate source flag to
            // suppress its duplicate copy. It is not the Ward Key inventory
            // count, so setting it does not grant the randomized key item.
            if (PlayerData.instance != null &&
                string.Equals(
                    locationName,
                    "White Key",
                    StringComparison.Ordinal))
            {
                PlayerData.instance.collectedWardKey = true;
            }
        }

        private static CollectibleSourceManifest.DirectPickupEntry
            FindDirectPickup(
                CollectableItemPickup pickup,
                SavedItem nativeItem)
        {
            if (pickup == null || nativeItem == null)
            {
                return null;
            }

            return CollectibleSourceManifest.FindDirectPickup(
                pickup.gameObject.scene.name,
                nativeItem.name,
                Utils.GetHierarchyPath(pickup.transform),
                pickup.transform.position
            );
        }

        private static CollectibleSourceManifest.PollipHeartEntry
            FindPollipHeartSource(Transform pickupTransform)
        {
            if (pickupTransform == null)
            {
                return null;
            }

            // Each listed scene contains one source at this hierarchy.
            // Transform.position is unsuitable as an identity because several
            // purple_flower_set roots apply rotation or negative
            // scale, which made coordinates derived by adding local positions
            // differ from Unity's real world positions and left all six
            // rewards vanilla.
            Transform flowerTransform = pickupTransform.parent;
            if (flowerTransform == null ||
                !string.Equals(
                    flowerTransform.name,
                    "Big Flower",
                    StringComparison.Ordinal))
            {
                return null;
            }

            return CollectibleSourceManifest.FindPollipHeartSource(
                pickupTransform.gameObject.scene.name,
                pickupTransform.name,
                Utils.GetHierarchyPath(pickupTransform)
            );
        }

        private static bool TryGetPollipHeartNativeAction(
            PlayMakerFSM fsm,
            out FsmState collectState,
            out CollectableItemCollect nativeAction)
        {
            collectState = null;
            nativeAction = null;
            if (fsm == null ||
                !string.Equals(
                    fsm.FsmName,
                    "Control",
                    StringComparison.Ordinal))
            {
                return false;
            }

            collectState = fsm.Fsm?.GetState("Collect");
            if (collectState?.Actions == null ||
                collectState.Actions.Length <= 6)
            {
                return false;
            }

            int matchCount = 0;
            int matchIndex = -1;
            foreach (FsmStateAction action in collectState.Actions)
            {
                if (!(action is CollectableItemCollect candidate) ||
                    !(candidate.Item?.Value is CollectableItem item) ||
                    !string.Equals(
                        item.name,
                        PollipHeartAssetName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                matchIndex = Array.IndexOf(
                    collectState.Actions,
                    action
                );
                nativeAction = candidate;
                matchCount++;
            }

            return matchCount == 1 && matchIndex == 6;
        }

        private static void TakePollipHeartPersistenceOwnership(
            Transform pickupTransform,
            string locationName)
        {
            if (pickupTransform == null ||
                pickupTransform.parent == null)
            {
                return;
            }

            Transform flowerTransform = pickupTransform.parent;
            SaveState state = SaveState.Instance;
            if (state != null &&
                state.IsLocationChecked(locationName))
            {
                flowerTransform.gameObject.SetActive(false);
            }

            // The source has one PersistentBoolItem on Big Flower
            // and another on Nectar Pickup, both using ID "Nectar Pickup".
            // Once the FSM and action match, disable both
            // immediately so neither native Start can restore an old vanilla
            // SceneData value, then destroy them. The AP sidecar is the sole
            // persistence owner for randomized sources.
            foreach (PersistentBoolItem persistence in
                     flowerTransform.GetComponents<PersistentBoolItem>())
            {
                if (persistence == null)
                {
                    continue;
                }
                persistence.enabled = false;
                UnityEngine.Object.Destroy(persistence);
            }
            foreach (PersistentBoolItem persistence in
                     pickupTransform.GetComponents<PersistentBoolItem>())
            {
                if (persistence == null)
                {
                    continue;
                }
                persistence.enabled = false;
                UnityEngine.Object.Destroy(persistence);
            }
        }

        [HarmonyPatch(typeof(PersistentBoolItem), "Awake")]
        private static class PollipHeartPersistencePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(PersistentBoolItem __instance)
            {
                if (__instance == null)
                {
                    return true;
                }

                Transform pickup;
                if (string.Equals(
                        __instance.gameObject.name,
                        "Big Flower",
                        StringComparison.Ordinal))
                {
                    pickup = __instance.transform.Find("Nectar Pickup");
                }
                else if (string.Equals(
                             __instance.gameObject.name,
                             "Nectar Pickup",
                             StringComparison.Ordinal) &&
                         __instance.transform.parent != null &&
                         string.Equals(
                             __instance.transform.parent.name,
                             "Big Flower",
                             StringComparison.Ordinal))
                {
                    pickup = __instance.transform;
                }
                else
                {
                    return true;
                }

                CollectibleSourceManifest.PollipHeartEntry entry =
                    FindPollipHeartSource(pickup);
                SaveState state = SaveState.Instance;
                if (entry == null ||
                    !IsActive(
                        state,
                        entry.LocationName,
                        ItemType.PollipHeart))
                {
                    return true;
                }


                PlayMakerFSM controlFsm =
                    pickup.GetComponent<PlayMakerFSM>();
                if (!TryGetPollipHeartNativeAction(
                        controlFsm,
                        out FsmState _,
                        out CollectableItemCollect _))
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Pollip Heart persistence patch " +
                        "failed closed at " +
                        __instance.gameObject.scene.name + "/" +
                        Utils.GetHierarchyPath(pickup) +
                        ": shipped Control/Collect action 6 did not " +
                        "match Shell Flower."
                    );
                    return true;
                }

                TakePollipHeartPersistenceOwnership(
                    pickup,
                    entry.LocationName
                );
                return false;
            }
        }

        [HarmonyPatch(typeof(CollectableItemPickup), "Awake")]
        private static class DirectPickupAwakePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            internal static void Prefix(CollectableItemPickup __instance)
            {
                SavedItem nativeItem = __instance == null
                    ? null
                    : __instance.Item;
                CollectibleSourceManifest.DirectPickupEntry entry =
                    FindDirectPickup(__instance, nativeItem);
                SaveState state = SaveState.Instance;
                if (entry == null ||
                    !IsActive(state, entry.LocationName, entry.Type))
                {
                    return;
                }

                if (string.Equals(
                        entry.LocationName,
                        "White Key",
                        StringComparison.Ordinal))
                {
                    // Receiving the randomized White Key sets this vanilla
                    // fallback flag. Decouple it from this physical source.
                    // The pickup's PersistentBoolItem now owns disappearance
                    // and the proxy sets the flag only when this source is
                    // actually collected.
                    __instance.SetPlayerDataBool(string.Empty);
                }

                __instance.SetItem(
                    GetProxyItem(
                        entry.LocationName,
                        entry.Type
                    ),
                    keepPersistence: true
                );
            }
        }

        [HarmonyPatch(typeof(DeactivateIfPlayerdataTrue), "ForceEvaluate")]
        private static class ApostateSourceOwnershipGatePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                DeactivateIfPlayerdataTrue __instance)
            {
                SaveState state = SaveState.Instance;
                if (__instance == null ||
                    !string.Equals(
                        __instance.gameObject.scene.name,
                        "Aqueduct_04",
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        __instance.boolName,
                        "HasSlabKeyC",
                        StringComparison.Ordinal) ||
                    !IsActive(
                        state,
                        "Key of Apostate",
                        ItemType.MajorKey) ||
                    state.IsLocationChecked("Key of Apostate"))
                {
                    return true;
                }

                Vector2 position = __instance.transform.position;
                float deltaX = position.x - 7.570004f;
                float deltaY = position.y - 38.651804f;
                if (deltaX * deltaX + deltaY * deltaY > 2f * 2f)
                {
                    return true;
                }

                // Key ownership must open Slab locks, but it must not erase
                // this still-unchecked AP source. Both deactivators
                // (the cage and its item dummy) sit within this radius.
                return false;
            }
        }

        [HarmonyPatch(typeof(HeroController), "SendHeroInPosition")]
        private static class WhiteKeyFloorPickupPatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                SaveState state = SaveState.Instance;
                if (state?.IsRoomBound != true || PlayerData.instance == null ||
                    GameManager.instance?.GetSceneNameString() != "Song_Enclave") return;
                Scene scene = SceneManager.GetSceneByName("Song_Enclave");
                if (!scene.IsValid() || !scene.isLoaded) return;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name != "Black Thread States") continue;
                    Transform source = root.transform.Find("Normal World/Enclave States/Ward Key Scene");
                    if (source == null) return;
                    CollectableItemPickup pickup = source.Find("Collectable Item Pickup")?.GetComponent<CollectableItemPickup>();
                    PlayerDataTestResponse gate = source.GetComponent<PlayerDataTestResponse>();
                    if (pickup == null || gate == null) return;
                    DirectPickupAwakePatch.Prefix(pickup);
                    bool available;
                    if (pickup.Item is ArchipelagoSourceItem item)
                    {
                        if (item.LocationName != "White Key") return;
                        available = !state.IsLocationChecked("White Key");
                    }
                    else
                    {
                        if (pickup.Item?.name != "Ward Key") return;
                        available = !PlayerData.instance.collectedWardKey;
                    }
                    gate.enabled = false;
                    source.SetParent(null, true);
                    source.gameObject.SetActive(available);
                    return;
                }
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_IsAvailable")]
        private static class WhiteKeyFallbackAvailabilityPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                ShopItem __instance,
                ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (__instance == null ||
                    !string.Equals(
                        __instance.name,
                        "City Merchant Ward Key",
                        StringComparison.Ordinal) ||
                    state?.IsRoomBound != true)
                {
                    return true;
                }

                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(QuestPlaymakerActions.QuestFsmAction), nameof(QuestPlaymakerActions.QuestFsmAction.OnEnter))]
        private static class TacksWishOfferPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(QuestPlaymakerActions.QuestFsmAction __instance)
            {
                if (!(__instance is QuestPlaymakerActions.CheckQuestStateV2 check) ||
                    !IsActive(SaveState.Instance, "Tool Unlock: Tack", ItemType.Tool) ||
                    __instance.Fsm?.GameObject == null ||
                    __instance.Fsm.GameObject.scene.name != "Dust_Shack" ||
                    __instance.Fsm.Name != "Dialogue" || __instance.State?.Name != "State?" ||
                    Utils.GetHierarchyPath(__instance.Fsm.GameObject.transform) !=
                        "Black Thread States Thread Only Variant/Normal World/_NPCs/Dust Traders" ||
                    (__instance.Quest?.Value as FullQuestBase)?.name != "Doctor Curse Cure" ||
                    check.IncompleteEvent?.Name != "PINS" ||
                    (CollectableItemManager.GetItemByName("Extractor Machine Pins")?.CollectedAmount ?? 0) < 1)
                    return true;
                FullQuestBase quest = QuestManager.GetQuest("Roach Killing");
                if (quest == null || quest.IsAccepted || quest.IsCompleted) return true;
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(
            typeof(SilkGrubCocoon),
            nameof(SilkGrubCocoon.WasHit)
        )]
        private static class SilkeaterCocoonPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                SilkGrubCocoon __instance,
                out SilkeaterCallState __state)
            {
                __state = new SilkeaterCallState
                {
                    PreviousLocation = activeSilkeaterLocation,
                };
                activeSilkeaterLocation = null;

                if (__instance == null)
                {
                    return;
                }

                CollectibleSourceManifest.CocoonEntry entry =
                    CollectibleSourceManifest.FindSilkeaterCocoon(
                        __instance.gameObject.scene.name,
                        __instance.gameObject.name,
                        __instance.transform.position
                    );
                if (entry != null &&
                    IsActive(
                        SaveState.Instance,
                        entry.LocationName,
                        ItemType.Silkeater
                    ))
                {
                    activeSilkeaterLocation = entry.LocationName;
                }
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(
                Exception __exception,
                SilkeaterCallState __state)
            {
                activeSilkeaterLocation = __state.PreviousLocation;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(SilkGrubCocoon), "Awake")]
        private static class SilkeaterCocoonPersistenceIdentityPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(
                SilkGrubCocoon __instance,
                PersistentBoolItem ___persistent)
            {
                if (__instance == null || ___persistent == null)
                {
                    return;
                }

                CollectibleSourceManifest.CocoonEntry entry =
                    CollectibleSourceManifest.FindSilkeaterCocoon(
                        __instance.gameObject.scene.name,
                        __instance.gameObject.name,
                        __instance.transform.position
                    );
                if (entry == null)
                {
                    return;
                }

                SilkeaterCocoonByPersistence.Remove(___persistent);
                SilkeaterCocoonByPersistence.Add(___persistent, entry);
            }
        }

        [HarmonyPatch(typeof(PersistentBoolItem), "TryGetValue")]
        private static class SilkeaterCocoonPersistenceLoadPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(
                PersistentBoolItem __instance,
                ref PersistentItemData<bool> newItemData,
                ref bool __result)
            {
                if (__instance == null ||
                    newItemData == null ||
                    !SilkeaterCocoonByPersistence.TryGetValue(
                        __instance,
                        out CollectibleSourceManifest.CocoonEntry entry
                    ))
                {
                    return;
                }

                SaveState state = SaveState.Instance;
                if (!IsActive(
                        state,
                        entry.LocationName,
                        ItemType.Silkeater))
                {
                    return;
                }

                newItemData.Value =
                    state.IsLocationChecked(entry.LocationName);
                __result = true;
            }
        }

        [HarmonyPatch(
            typeof(CollectableItemPickup),
            nameof(CollectableItemPickup.SetItem),
            new Type[] { typeof(SavedItem), typeof(bool) }
        )]
        private static class DynamicPickupRewardPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                CollectableItemPickup __instance,
                ref SavedItem newItem)
            {
                if (__instance == null || newItem == null)
                {
                    return;
                }

                // Some direct sources are assigned by an Item Placer after
                // their Awake rather than serialized on the pickup itself.
                CollectibleSourceManifest.DirectPickupEntry directEntry =
                    FindDirectPickup(__instance, newItem);
                if (directEntry != null &&
                    IsActive(
                        SaveState.Instance,
                        directEntry.LocationName,
                        directEntry.Type
                    ))
                {
                    newItem = GetProxyItem(
                        directEntry.LocationName,
                        directEntry.Type
                    );
                    return;
                }

                if (!string.IsNullOrEmpty(activeSilkeaterLocation) &&
                    string.Equals(
                        newItem.name,
                        SilkeaterAssetName,
                        StringComparison.Ordinal) &&
                    IsActive(
                        SaveState.Instance,
                        activeSilkeaterLocation,
                        ItemType.Silkeater
                    ))
                {
                    newItem = GetProxyItem(
                        activeSilkeaterLocation,
                        ItemType.Silkeater
                    );
                    return;
                }

                if (string.Equals(
                        newItem.name,
                        CollectibleSourceManifest.CrawSummons,
                        StringComparison.Ordinal) &&
                    IsCrawSummonsPickup(__instance) &&
                    IsActive(
                        SaveState.Instance,
                        CollectibleSourceManifest.CrawSummons,
                        ItemType.MajorKey
                    ))
                {
                    newItem = GetProxyItem(
                        CollectibleSourceManifest.CrawSummons,
                        ItemType.MajorKey
                    );
                }
            }
        }

        private static bool IsCrawSummonsPickup(
            CollectableItemPickup pickup)
        {
            string sceneName = pickup.gameObject.scene.name;
            for (Transform current = pickup.transform;
                 current != null;
                 current = current.parent)
            {
                if (CollectibleSourceManifest.IsCrawPin(
                        sceneName,
                        current.name,
                        current.position))
                {
                    return true;
                }
            }

            // The pin FSM can detach its generated pickup before assigning the
            // SavedItem. The unique item name and source-position tolerance
            // still identify it after detachment.
            const float toleranceSquared = 6f * 6f;
            Vector2 position = pickup.transform.position;
            foreach (CollectibleSourceManifest.CrawPinEntry entry in
                CollectibleSourceManifest.CrawPins)
            {
                if (!string.Equals(
                        sceneName,
                        entry.SceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                float deltaX = position.x - entry.X;
                float deltaY = position.y - entry.Y;
                if (deltaX * deltaX + deltaY * deltaY <=
                    toleranceSquared)
                {
                    return true;
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class CollectibleFsmPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.Last)]
            private static void Prefix(PlayMakerFSM __instance)
            {
                if (__instance == null ||
                    __instance.gameObject == null)
                {
                    return;
                }

                PatchMossberrySource(__instance);
                PatchPollipHeartSource(__instance);
                PatchCrawSummonsPin(__instance);
            }
        }

        private static void PatchMossberrySource(PlayMakerFSM fsm)
        {
            if (!CollectibleSourceManifest.TryGetMossberryLocation(
                    fsm.gameObject.scene.name,
                    out string locationName) ||
                !IsActive(
                    SaveState.Instance,
                    locationName,
                    ItemType.Mossberry
                ))
            {
                return;
            }

            FsmState collectState = fsm.Fsm?.GetState("Collect");
            if (collectState?.Actions == null)
            {
                return;
            }

            for (int i = 0; i < collectState.Actions.Length; i++)
            {
                if (collectState.Actions[i] is
                        CompleteMossberryLocation existing &&
                    string.Equals(
                        existing.LocationName,
                        locationName,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }

            int nativeActionIndex = -1;
            CollectableItemCollect nativeAction = null;
            for (int i = 0; i < collectState.Actions.Length; i++)
            {
                if (!(collectState.Actions[i] is
                        CollectableItemCollect candidate) ||
                    !(candidate.Item?.Value is CollectableItem item) ||
                    !string.Equals(
                        item.name,
                        MossberryAssetName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (nativeActionIndex >= 0)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Mossberry source patch found more " +
                        "than one native reward action at " +
                        fsm.gameObject.scene.name + "/" +
                        Utils.GetHierarchyPath(fsm.transform) + "."
                    );
                    return;
                }
                nativeActionIndex = i;
                nativeAction = candidate;
            }

            if (nativeActionIndex < 0)
            {
                return;
            }

            CompleteMossberryLocation replacement =
                new CompleteMossberryLocation(
                    locationName,
                    nativeAction
                );
            replacement.Init(collectState);
            collectState.Actions[nativeActionIndex] = replacement;
        }

        private static void PatchPollipHeartSource(PlayMakerFSM fsm)
        {
            CollectibleSourceManifest.PollipHeartEntry entry =
                FindPollipHeartSource(fsm.transform);
            if (entry == null ||
                !IsActive(
                    SaveState.Instance,
                    entry.LocationName,
                    ItemType.PollipHeart))
            {
                return;
            }

            FsmState collectState = fsm.Fsm?.GetState("Collect");
            if (collectState?.Actions != null &&
                collectState.Actions.Length > 6 &&
                collectState.Actions[6] is
                    CompletePollipHeartLocation existing &&
                string.Equals(
                    existing.LocationName,
                    entry.LocationName,
                    StringComparison.Ordinal))
            {
                return;
            }

            // The FSM grants the Shell Flower at action 6. A layout change
            // falls back to vanilla rather
            // than replacing an uncertain action.
            if (!TryGetPollipHeartNativeAction(
                    fsm,
                    out collectState,
                    out CollectableItemCollect nativeAction))
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Pollip Heart source patch failed " +
                    "closed at " + fsm.gameObject.scene.name + "/" +
                    Utils.GetHierarchyPath(fsm.transform) +
                    ": expected Shell Flower action 6."
                );
                return;
            }

            CompletePollipHeartLocation replacement =
                new CompletePollipHeartLocation(
                    entry.LocationName,
                    nativeAction
                );
            replacement.Init(collectState);
            collectState.Actions[6] = replacement;
            TakePollipHeartPersistenceOwnership(
                fsm.transform,
                entry.LocationName
            );
        }

        [HarmonyPatch(typeof(Fsm), nameof(Fsm.CreateSubFsm))]
        private static class CrawSummonsSpawnPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                FsmTemplateControl templateControl,
                Fsm __result)
            {
                if (templateControl?.fsmTemplate?.name !=
                    "craw_summons_spawn_check")
                {
                    return;
                }

                FsmState state = __result?.GetState("Can Appear?");
                FsmStateAction[] actions = state?.Actions;
                if (actions == null || actions.Length != 7 ||
                    !(actions[4] is CollectableItemGetData item) ||
                    !(item.Item?.Value is CollectableItem collectable) ||
                    collectable.name != CollectibleSourceManifest.CrawSummons ||
                    item.NeededAmount?.Value != 1 ||
                    item.IsCollected?.Name != "CANCEL" ||
                    !string.IsNullOrEmpty(item.NotCollected?.Name) ||
                    !(actions[5] is PlayerDataVariableTest door) ||
                    door.VariableName?.Value != "OpenedCrowSummonsDoor" ||
                    door.ExpectedValue == null ||
                    door.ExpectedValue.Type != VariableType.Bool ||
                    !door.ExpectedValue.boolValue ||
                    door.IsExpectedEvent?.Name != "CANCEL" ||
                    !string.IsNullOrEmpty(door.IsNotExpectedEvent?.Name) ||
                    !HasTransition(state, "CANCEL", "Send CANCEL"))
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Craw Summons spawn check has an unexpected layout."
                    );
                    return;
                }

                for (int index = 4; index <= 5; index++)
                {
                    var replacement = new CrawSpawnSourceGate(actions[index]);
                    replacement.Init(state);
                    actions[index] = replacement;
                }
            }
        }

        private sealed class CrawSpawnSourceGate : FsmStateAction
        {
            private readonly FsmStateAction nativeAction;

            internal CrawSpawnSourceGate(FsmStateAction nativeAction)
            {
                this.nativeAction = nativeAction;
            }

            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (!IsActive(state, CollectibleSourceManifest.CrawSummons,
                    ItemType.MajorKey))
                {
                    nativeAction.OnEnter();
                }
                else if (state.IsLocationChecked(CollectibleSourceManifest.CrawSummons))
                {
                    Fsm.Event("CANCEL");
                }
                Finish();
            }
        }

        private static void PatchCrawSummonsPin(PlayMakerFSM fsm)
        {
            if (!CollectibleSourceManifest.IsCrawPin(
                    fsm.gameObject.scene.name,
                    fsm.gameObject.name,
                    fsm.transform.position) ||
                !IsActive(
                    SaveState.Instance,
                    CollectibleSourceManifest.CrawSummons,
                    ItemType.MajorKey
                ))
            {
                return;
            }

            FsmState emptyState = fsm.Fsm?.GetState("Empty?");
            if (emptyState?.Actions == null)
            {
                return;
            }
            if (emptyState.Actions.Length == 1 &&
                emptyState.Actions[0] is CrawSourceGateAction)
            {
                return;
            }

            bool expectedActions =
                emptyState.Actions.Length == 2 &&
                emptyState.Actions[0] is PlayerDataVariableTest &&
                emptyState.Actions[1] is CollectableItemGetData;
            bool expectedTransitions =
                HasTransition(emptyState, "TRUE", "Set Empty") &&
                HasTransition(emptyState, "FINISHED", "Set Pickup");
            if (!expectedActions || !expectedTransitions)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Craw Summons pin patch failed closed " +
                    "at " + fsm.gameObject.scene.name + "/" +
                    Utils.GetHierarchyPath(fsm.transform) +
                    ". The Empty? state has an unexpected layout."
                );
                return;
            }

            CrawSourceGateAction replacement =
                new CrawSourceGateAction();
            replacement.Init(emptyState);
            emptyState.Actions = new FsmStateAction[] { replacement };
        }

        private static bool HasTransition(
            FsmState state,
            string eventName,
            string targetState)
        {
            if (state?.Transitions == null)
            {
                return false;
            }

            foreach (FsmTransition transition in state.Transitions)
            {
                if (transition != null &&
                    string.Equals(
                        transition.EventName,
                        eventName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        transition.ToState,
                        targetState,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private sealed class CompleteMossberryLocation : FsmStateAction
        {
            internal readonly string LocationName;
            private readonly CollectableItemCollect nativeAction;

            internal CompleteMossberryLocation(
                string locationName,
                CollectableItemCollect nativeAction)
            {
                LocationName = locationName;
                this.nativeAction = nativeAction;
            }

            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (IsActive(
                        state,
                        LocationName,
                        ItemType.Mossberry))
                {
                    if (!state.IsLocationChecked(LocationName))
                    {
                        state.CheckLocation(LocationName);
                    }
                }
                else if (nativeAction?.Item?.Value is
                    CollectableItem nativeItem)
                {
                    int amount = nativeAction.Amount == null ||
                                 nativeAction.Amount.IsNone
                        ? 1
                        : nativeAction.Amount.Value;
                    nativeItem.Collect(amount);
                }
                Finish();
            }
        }

        private sealed class CompletePollipHeartLocation : FsmStateAction
        {
            internal readonly string LocationName;
            private readonly CollectableItemCollect nativeAction;

            internal CompletePollipHeartLocation(
                string locationName,
                CollectableItemCollect nativeAction)
            {
                LocationName = locationName;
                this.nativeAction = nativeAction;
            }

            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (IsActive(
                        state,
                        LocationName,
                        ItemType.PollipHeart))
                {
                    if (!state.IsLocationChecked(LocationName))
                    {
                        state.CheckLocation(LocationName);
                    }
                }
                else if (nativeAction?.Item?.Value is
                    CollectableItem nativeItem)
                {
                    int amount = nativeAction.Amount == null ||
                                 nativeAction.Amount.IsNone
                        ? 1
                        : nativeAction.Amount.Value;
                    nativeItem.Collect(amount);
                }
                Finish();
            }
        }

        private sealed class CrawSourceGateAction : FsmStateAction
        {
            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                bool isChecked = state != null &&
                    state.IsLocationChecked(
                        CollectibleSourceManifest.CrawSummons
                    );
                Fsm.Event(isChecked ? "TRUE" : "FINISHED");
                Finish();
            }
        }

        private static bool TryGetActiveQuestLocation(
            FullQuestBase quest,
            out string locationName
        )
        {
            if (quest != null &&
                QuestLocationManifest.TryGetLocationName(
                    quest.name,
                    out locationName) &&
                IsActive(
                    SaveState.Instance,
                    locationName,
                    ItemType.Quest))
            {
                return true;
            }

            locationName = null;
            return false;
        }

        private static bool TryGetActiveQuestRewardSource(
            FullQuestBase quest,
            out string locationName,
            out ItemType type
        )
        {
            locationName = null;
            type = ItemType.Unknown;
            if (quest == null)
            {
                return false;
            }

            if (MaskAndSpoolLocationManifest.TryGetQuestSource(
                    quest.name,
                    out locationName,
                    out type))
            {
                return IsActive(
                    SaveState.Instance,
                    locationName,
                    type
                );
            }

            if (!QuestRewardSources.TryGetValue(
                    quest.name,
                    out Tuple<string, ItemType> source))
            {
                return false;
            }

            locationName = source.Item1;
            type = source.Item2;
            return IsActive(
                SaveState.Instance,
                locationName,
                type
            );
        }

        private static bool ShouldUseGenericQuestPresentation(
            FullQuestBase quest
        )
        {
            // Donation Wishes show only their native price and have no
            // vanilla reward icon to replace. Injecting the neutral AP icon
            // overlaps that price, so keep their cost-only layout.
            if (quest != null &&
                QuestLocationManifest.IsDonationWithoutVanillaReward(
                    quest.name
                ))
            {
                return false;
            }

            return TryGetActiveQuestLocation(quest, out string _) ||
                   TryGetActiveQuestRewardSource(
                       quest,
                       out string _,
                       out ItemType _
                   );
        }

        private static void ReportConsumedQuestChecks(
            FullQuestBase quest
        )
        {
            SaveState state = SaveState.Instance;
            if (state == null || quest == null)
            {
                return;
            }

            if (TryGetActiveQuestLocation(
                    quest,
                    out string questLocation
                ))
            {
                state.CheckLocation(questLocation);
            }

            if (TryGetActiveQuestRewardSource(
                    quest,
                    out string rewardLocation,
                    out ItemType _
                ) &&
                !string.Equals(
                    rewardLocation,
                    questLocation,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                state.CheckLocation(rewardLocation);
            }
        }

        private static void ClearQuestClothingAfterTurnIn(
            FullQuestBase quest
        )
        {
            if (quest == null || !TryGetActiveQuestLocation(quest, out string _))
            {
                return;
            }
            string itemName;
            switch (quest.name)
            {
                case PilgrimRagsQuestName:
                    itemName = PilgrimRagCollectableName;
                    break;
                case "Song Pilgrim Cloaks":
                    itemName = "Song Pilgrim Cloak";
                    break;
                default:
                    return;
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData?.Collectables == null)
            {
                return;
            }

            CollectableItemsData.Data data =
                playerData.Collectables.GetData(
                    itemName
                );
            if (data.Amount == 0 && data.AmountWhileHidden == 0)
            {
                return;
            }

            data.Amount = 0;
            data.AmountWhileHidden = 0;
            playerData.Collectables.SetData(
                itemName,
                data
            );
            CollectableItemManager.IncrementVersion();
        }

        [HarmonyPatch(
            typeof(FullQuestBase),
            nameof(FullQuestBase.ConsumeTarget)
        )]
        private static class QuestTurnInConsumptionPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                FullQuestBase __instance,
                bool __result
            )
            {
                if (__result)
                {
                    ClearQuestClothingAfterTurnIn(__instance);
                    ReportConsumedQuestChecks(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_RewardItem")]
        private static class QuestRewardItemPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.First)]
            private static void Postfix(
                FullQuestBase __instance,
                ref SavedItem __result)
            {
                if (__instance == null)
                {
                    return;
                }

                if (MaskAndSpoolLocationManifest.TryGetQuestSource(
                        __instance.name,
                        out string spoolLocation,
                        out ItemType spoolType) &&
                    spoolType == ItemType.SpoolFragment &&
                    IsActive(
                        SaveState.Instance,
                        spoolLocation,
                        spoolType
                    ))
                {
                    __result = GetProxyItem(
                        spoolLocation,
                        spoolType,
                        showGenericPresentation: true
                    );
                    return;
                }

                if (string.Equals(
                        __instance.name,
                        "Journal",
                        StringComparison.Ordinal) &&
                    IsActive(
                        SaveState.Instance,
                        ToolPouchLocationManifest.
                            BugsOfPharloomLocation,
                        ItemType.ToolPouch
                    ))
                {
                    __result = GetProxyItem(
                        ToolPouchLocationManifest.
                            BugsOfPharloomLocation,
                        ItemType.ToolPouch,
                        showGenericPresentation: true
                    );
                    return;
                }

                if (string.Equals(
                        __instance.name,
                        RockRollersQuestName,
                        StringComparison.Ordinal) &&
                    IsActive(
                        SaveState.Instance,
                        CollectibleSourceManifest.
                            VolatileFlintbeetlesLocket,
                        ItemType.MemoryLocket
                    ))
                {
                    __result = GetProxyItem(
                        CollectibleSourceManifest.
                            VolatileFlintbeetlesLocket,
                        ItemType.MemoryLocket,
                        showGenericPresentation: true
                    );
                    return;
                }

                // Quest Sanity replaces twelve generic currency rewards.
                // The FullQuestBase asset and its SavedItem jointly identify
                // each reward so a game update cannot suppress an unrelated one.
                //
                // Unique vanilla rewards which are not randomized elsewhere
                // (Plasmium Gland and Tool Pouch)
                // remain untouched. Pale Oil, Memory Locket and Spool
                // Fragment quest rewards have their own physical AP source
                // paths and are not handled by this QuestSanity proxy.
                string nativeRewardAssetName =
                    __result == null ? null : __result.name;
                if (TryGetActiveQuestLocation(
                        __instance,
                        out string questLocation) &&
                    QuestLocationManifest.
                        TryGetReplaceableVanillaRewardLocation(
                            __instance.name,
                            nativeRewardAssetName,
                            out string matchedLocation) &&
                    string.Equals(
                        questLocation,
                        matchedLocation,
                        StringComparison.OrdinalIgnoreCase))
                {
                    __result = GetProxyItem(
                        questLocation,
                        ItemType.Quest,
                        showGenericPresentation: true
                    );
                }
            }
        }

        private static string GetQuestPreviewLocation(FullQuestBase quest)
        {
            if (TryGetActiveQuestRewardSource(quest, out string location, out ItemType _)) return location;
            return TryGetActiveQuestLocation(quest, out location) ? location : null;
        }

        private static FullQuestBase.IconTypes GetQuestPreviewIconType(FullQuestBase quest)
        {
            FullQuestBase.IconTypes type = quest.RewardIconType;
            return ShouldUseGenericQuestPresentation(quest) || type == FullQuestBase.IconTypes.None
                ? FullQuestBase.IconTypes.Image : type;
        }

        [HarmonyPatch(typeof(QuestItemDescription), nameof(QuestItemDescription.SetDisplay))]
        private static class QuestPreviewRefreshPatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var getter = AccessTools.PropertyGetter(typeof(FullQuestBase), nameof(FullQuestBase.RewardIconType));
                var replacement = AccessTools.Method(typeof(CollectibleSourcePatches), nameof(GetQuestPreviewIconType));
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.Calls(getter))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = replacement;
                    }
                    yield return instruction;
                }
            }

            private static void Postfix(QuestItemDescription __instance, BasicQuestBase quest, SpriteRenderer ___rewardIcon)
            {
                var refresh = __instance.GetComponent<WishPreviewRefresh>();
                if (quest is FullQuestBase fullQuest && ShouldUseGenericQuestPresentation(fullQuest))
                {
                    if (refresh == null) refresh = __instance.gameObject.AddComponent<WishPreviewRefresh>();
                    refresh.Bind(GetQuestPreviewLocation(fullQuest), ___rewardIcon);
                }
                else if (refresh != null) refresh.Bind(null, ___rewardIcon);
            }
        }

        private sealed class WishPreviewRefresh : MonoBehaviour
        {
            private string location;
            private SpriteRenderer icon;
            private Vector3 originalScale;
            private bool captured;
            private float nextUpdate;

            internal void Bind(string nextLocation, SpriteRenderer renderer)
            {
                if (captured && icon != null) icon.transform.localScale = originalScale;
                if (renderer != icon) captured = false;
                icon = renderer;
                location = nextLocation;
                if (icon != null && !captured)
                {
                    originalScale = icon.transform.localScale;
                    captured = true;
                }
                nextUpdate = 0f;
                Update();
            }

            private void Update()
            {
                if (location == null || icon == null || Time.unscaledTime < nextUpdate) return;
                nextUpdate = Time.unscaledTime + 0.25f;
                ItemPreview.Presentation preview = ItemPreview.Get(location, false, true);
                if (preview?.Icon == null) return;
                icon.sprite = preview.Icon;
                icon.transform.localScale = Vector3.Scale(originalScale, new Vector3(preview.Scale, preview.Scale, 1f));
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_RewardIcon")]
        private static class QuestRewardIconPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                FullQuestBase __instance,
                ref Sprite __result
            )
            {
                if (ShouldUseGenericQuestPresentation(__instance))
                {
                    string location = GetQuestPreviewLocation(__instance);
                    __result = ItemPreview.Get(location, false, true)?.Icon ??
                        RandomizerPlugin.Instance?.MapCheckIcon ?? RandomizerPlugin.Instance?.ArchipelagoIcon;
                }
            }
        }

    }
}
