using Archipelago.MultiClient.Net.Enums;
using HarmonyLib;
using SilksongRandomizer.AlphabetMode;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class CoreLocationPatches
    {
        [ThreadStatic]
        private static int relicGrantBypassDepth;

        [ThreadStatic]
        private static string craftingKitShopLocationContext;

        private static readonly FieldInfo SpawnConditionalsField =
            AccessTools.Field(typeof(ShopItem), "spawnOnPurchaseConditionals");

        private static readonly PropertyInfo CurrencyTypeProperty =
            AccessTools.Property(typeof(ShopItem), "CurrencyType");

        private static readonly FieldInfo PlayerDataBoolNameField =
            AccessTools.Field(typeof(ShopItem), "playerDataBoolName");

        private const string GrindleSpoolPlayerDataBool =
            "purchasedGrindleSpoolPiece";
        private const string GrindleSpoolItemAsset = "Silk Spool";
        private const string GrindleSpoolSourceLocation =
            "Spool Fragment Unlock #17";
        private const string FreySpoolShopAsset =
            "Belltown Spool Segment";
        private const string FreySpoolPlayerDataBool =
            "PurchasedBelltownSpoolSegment";
        private const string FreySpoolSourceLocation =
            "Spool Fragment Unlock #6";

        // The shop assets map to two stable physical sources. Pebb's
        // unsold shard moves to Grindle in Act 3, while Jubilana owns a
        // separate source. The heartPieces counter describes acquisition order
        // rather than which shop was purchased, so it cannot identify this preview.
        private static readonly Dictionary<string, string>
            MaskShardShopLocations =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {
                    "Bonebottom Mask Shard",
                    "Mask Shard Unlock #1"
                },
                {
                    "Grindle Mask Shard",
                    "Mask Shard Unlock #1"
                },
                {
                    "City Merchant Heart Piece",
                    "Mask Shard Unlock #15"
                },
            };

        private static bool IsActiveLocation(
            SaveState state,
            ref string locationName)
        {
            if (state == null ||
                string.IsNullOrWhiteSpace(locationName))
            {
                locationName = null;
                return false;
            }

            locationName = LocationSet.GetCanonicalLocationName(
                locationName
            );
            if (!state.IsLocationEnabled(locationName) ||
                !state.IsLocationInSeed(locationName))
            {
                locationName = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Applies an AP-received relic through the game's real CollectableRelic API
        /// without treating that delivery as its vanilla-world location check.
        /// </summary>
        internal static void GrantRelicWithoutChecking(
            CollectableRelic relic,
            bool showPopup = true)
        {
            if (relic == null)
            {
                throw new ArgumentNullException(nameof(relic));
            }

            if (!CoreLocationManifest.TryGetRelicLocation(relic.name, out _))
            {
                throw new ArgumentException(
                    "The CollectableRelic is not a registered relic asset: " + relic.name,
                    nameof(relic)
                );
            }

            relicGrantBypassDepth++;
            try
            {
                relic.Get(showPopup);
            }
            finally
            {
                relicGrantBypassDepth--;
            }
        }

        private static bool IsUncheckedRelic(CollectableRelic relic)
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                !state.IsRandomized(ItemType.Relic) ||
                relic == null ||
                !CoreLocationManifest.TryGetRelicLocation(
                    relic.name,
                    out string locationName) ||
                !IsActiveLocation(state, ref locationName))
            {
                return false;
            }

            return !state.IsLocationChecked(locationName);
        }

        /// <summary>
        /// Reconciles native relic inventory with AP receipts and
        /// removes vanilla relics leaked by a mismatched source hook.
        /// </summary>
        internal static bool TrySynchronizeReceivedRelics()
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                PlayerData.instance == null ||
                CollectableRelicManager.Instance == null)
            {
                return false;
            }

            if (!state.IsRandomized(ItemType.Relic))
            {
                return true;
            }

            foreach (string assetName in CoreLocationManifest.RelicAssetNames)
            {
                CollectableRelic relic =
                    CollectableRelicManager.GetRelic(assetName);
                if (relic == null ||
                    !CoreLocationManifest.TryGetRelicLocation(
                        assetName,
                        out string locationName))
                {
                    return false;
                }

                string itemName =
                    LocationSet.GetCanonicalLocationName(locationName);
                bool shouldOwn =
                    state.receivedItems != null &&
                    state.receivedItems.Contains(itemName);
                bool isActiveSource =
                    state.IsLocationEnabled(itemName) &&
                    state.IsLocationInSeed(itemName);
                CollectableRelicsData.Data data = relic.SavedData;

                if (shouldOwn)
                {
                    if (!data.IsCollected)
                    {
                        data.IsCollected = true;
                        relic.SavedData = data;
                    }
                }
                else if (isActiveSource &&
                         (data.IsCollected ||
                         data.IsDeposited ||
                         data.HasSeenInRelicBoard))
                {
                    data.IsCollected = false;
                    data.IsDeposited = false;
                    data.HasSeenInRelicBoard = false;
                    relic.SavedData = data;
                }
            }

            return true;
        }

        private static bool TryResolveShopLocation(
            ShopItem shopItem,
            out string locationName)
        {
            locationName = null;
            SaveState state = SaveState.Instance;
            if (state == null || shopItem == null)
            {
                return false;
            }

            if (CoreLocationManifest.TryGetShopLocation(
                    shopItem.name,
                    out NativeShopLocation shopLocation))
            {
                if (!state.IsRandomized(shopLocation.Type))
                {
                    return false;
                }

                locationName = shopLocation.LocationName;
                return IsActiveLocation(state, ref locationName);
            }

            if (CoreLocationManifest.TryGetCraftingKitShopLocation(
                    shopItem.name,
                    out locationName))
            {
                return state.IsRandomized(ItemType.Upgrade) &&
                       IsActiveLocation(state, ref locationName);
            }

            CollectableRelic relic = shopItem.Item as CollectableRelic;
            if (relic == null ||
                !state.IsRandomized(ItemType.Relic) ||
                !CoreLocationManifest.TryGetRelicLocation(
                    relic.name,
                    out locationName))
            {
                return false;
            }

            return IsActiveLocation(state, ref locationName);
        }

        private static bool TryResolveShopPreviewLocation(
            ShopItem shopItem,
            out string locationName)
        {
            locationName = null;

            SaveState state = SaveState.Instance;
            if (state != null && shopItem != null)
            {
                string itemAssetName =
                    shopItem.Item == null ? null : shopItem.Item.name;
                string playerDataBoolName =
                    PlayerDataBoolNameField == null
                        ? null
                        : PlayerDataBoolNameField.GetValue(shopItem) as string;

                if (TryResolveStableShopPreviewIdentity(
                        shopItem.name,
                        itemAssetName,
                        playerDataBoolName,
                        state.IsRandomized(ItemType.MaskShard),
                        state.IsRandomized(ItemType.SpoolFragment),
                        out locationName))
                {
                    return IsShopPreviewLocationInSeed(
                        state,
                        ref locationName
                    );
                }
            }

            return TryResolveShopLocation(shopItem, out locationName) &&
                   IsShopPreviewLocationInSeed(
                       state,
                       ref locationName
                   );
        }

        private static bool IsShopPreviewLocationInSeed(
            SaveState state,
            ref string locationName)
        {
            return IsActiveLocation(state, ref locationName);
        }

        internal static bool TryResolveShopHintLocation(
            ShopItem shopItem,
            out string locationName)
        {
            return TryResolveShopPreviewLocation(
                shopItem,
                out locationName
            );
        }

        private static bool TryResolveStableShopPreviewIdentity(
            string shopAssetName,
            string itemAssetName,
            string playerDataBoolName,
            bool maskShardsRandomized,
            bool spoolFragmentsRandomized,
            out string locationName)
        {
            locationName = null;

            if (maskShardsRandomized &&
                MaskShardShopLocations.TryGetValue(
                    shopAssetName ?? string.Empty,
                    out string sourceLocationName) &&
                string.Equals(
                    itemAssetName,
                    "Heart Piece",
                    StringComparison.Ordinal))
            {
                locationName =
                    LocationSet.GetCanonicalLocationName(sourceLocationName);
                return true;
            }

            if (spoolFragmentsRandomized &&
                string.Equals(
                    shopAssetName,
                    FreySpoolShopAsset,
                    StringComparison.Ordinal) &&
                string.Equals(
                    playerDataBoolName,
                    FreySpoolPlayerDataBool,
                    StringComparison.Ordinal) &&
                string.Equals(
                    itemAssetName,
                    GrindleSpoolItemAsset,
                    StringComparison.Ordinal))
            {
                locationName = LocationSet.GetCanonicalLocationName(
                    FreySpoolSourceLocation
                );
                return true;
            }

            if (spoolFragmentsRandomized &&
                string.Equals(
                    playerDataBoolName,
                    GrindleSpoolPlayerDataBool,
                    StringComparison.Ordinal) &&
                string.Equals(
                    itemAssetName,
                    GrindleSpoolItemAsset,
                    StringComparison.Ordinal))
            {
                locationName = LocationSet.GetCanonicalLocationName(
                    GrindleSpoolSourceLocation
                );
                return true;
            }

            return false;
        }

        private static bool TryGetPurchaseConditionals(
            ShopItem shopItem,
            out Array conditionals,
            out MethodInfo tryInstantiateMethod,
            out string error)
        {
            conditionals = null;
            tryInstantiateMethod = null;
            error = null;

            if (SpawnConditionalsField == null)
            {
                error = "ShopItem.spawnOnPurchaseConditionals was not found.";
                return false;
            }

            conditionals = SpawnConditionalsField.GetValue(shopItem) as Array;
            if (conditionals == null)
            {
                error = "ShopItem.spawnOnPurchaseConditionals was null.";
                return false;
            }

            if (conditionals.Length == 0)
            {
                return true;
            }

            Type conditionalType = conditionals.GetType().GetElementType();
            tryInstantiateMethod = conditionalType == null
                ? null
                : AccessTools.Method(conditionalType, "TryInstantiate");

            if (tryInstantiateMethod == null)
            {
                error = "ShopItem.ConditionalSpawn.TryInstantiate was not found.";
                return false;
            }

            foreach (object conditional in conditionals)
            {
                if (conditional == null)
                {
                    error = "A ShopItem purchase conditional was null.";
                    return false;
                }
            }

            return true;
        }

        private static bool UsesRosaries(ShopItem shopItem, out string error)
        {
            error = null;
            if (CurrencyTypeProperty == null)
            {
                error = "ShopItem.CurrencyType was not found.";
                return false;
            }

            object value = CurrencyTypeProperty.GetValue(shopItem, null);
            if (value == null || Convert.ToInt32(value) != 0)
            {
                error = "The registered map or pin shop asset no longer uses Rosaries.";
                return false;
            }

            return true;
        }

        private static void ReportBlockingPurchaseError(
            string locationName,
            string detail,
            Exception exception = null)
        {
            string message =
                "Could not safely randomize '" + locationName +
                "'. No vanilla map or pin was granted. " + detail;

            if (exception != null)
            {
                message += " " + exception.Message;
            }

            if (RandomizerPlugin.Instance != null)
            {
                RandomizerPlugin.Instance.ReportBlockingError(message);
            }
            else
            {
                Debug.LogError("[RANDOMIZER] " + message);
            }
        }

        [HarmonyPatch(typeof(CollectableRelic), nameof(CollectableRelic.CanGetMore))]
        internal static class CollectableRelicCanGetMorePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(CollectableRelic __instance, ref bool __result)
            {
                if (relicGrantBypassDepth == 0 && IsUncheckedRelic(__instance))
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(
            typeof(CollectableRelic),
            nameof(CollectableRelic.Get),
            new Type[] { typeof(bool) })]
        internal static class CollectableRelicGetPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(CollectableRelic __instance)
            {
                if (relicGrantBypassDepth > 0)
                {
                    return true;
                }

                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.Relic) ||
                    !CoreLocationManifest.TryGetRelicLocation(
                        __instance.name,
                        out string locationName) ||
                    !IsActiveLocation(state, ref locationName))
                {
                    return true;
                }

                state.CheckLocation(locationName);
                return false;
            }
        }

        [HarmonyPatch(typeof(CollectableItemPickup), "CheckActivation")]
        internal static class CollectableItemPickupCheckActivationPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(CollectableItemPickup __instance)
            {
                SaveState state = SaveState.Instance;
                CollectableRelic relic =
                    __instance == null ? null : __instance.Item as CollectableRelic;

                if (state == null ||
                    !state.IsRandomized(ItemType.Relic) ||
                    relic == null ||
                    !CoreLocationManifest.TryGetRelicLocation(
                        relic.name,
                        out string locationName) ||
                    !IsActiveLocation(state, ref locationName))
                {
                    return true;
                }

                bool isChecked = state.IsLocationChecked(locationName);
                __instance.SetActivation(isChecked);

                if (isChecked)
                {
                    // This follows the checked branch of the game's private
                    // CheckActivation method, including its persistence events.
                    __instance.OnPickedUp?.Invoke();
                    __instance.OnPreviouslyPickedUp?.Invoke();
                    __instance.gameObject.SetActive(false);
                }

                // AP location state controls source visibility. A received
                // relic must not make its unchecked source vanish.
                return false;
            }
        }

        [HarmonyPatch(
            typeof(ShopItem),
            nameof(ShopItem.SetPurchased),
            new Type[] { typeof(Action), typeof(int) })]
        internal static class CraftingKitShopContextPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(ShopItem __instance, out string __state)
            {
                __state = craftingKitShopLocationContext;

                SaveState state = SaveState.Instance;
                if (state != null &&
                    state.IsRandomized(ItemType.Upgrade) &&
                    __instance != null &&
                    CoreLocationManifest.TryGetCraftingKitShopLocation(
                        __instance.name,
                        out string locationName))
                {
                    craftingKitShopLocationContext =
                        IsActiveLocation(state, ref locationName)
                            ? locationName
                            : string.Empty;
                }
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(
                Exception __exception,
                string __state)
            {
                craftingKitShopLocationContext = __state;
                return __exception;
            }
        }

        [HarmonyPatch(
            typeof(PlayerDataCollectable),
            nameof(PlayerDataCollectable.Get),
            new Type[] { typeof(bool) })]
        internal static class CraftingKitPickupPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(PlayerDataCollectable __instance)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.Upgrade) ||
                    __instance == null ||
                    !string.Equals(
                        __instance.name,
                        CoreLocationManifest.CraftingKitPickupAssetName,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                string locationName =
                    craftingKitShopLocationContext == null
                        ? CoreLocationManifest.CrowFeathersCraftingKitLocation
                        : craftingKitShopLocationContext;

                if (!IsActiveLocation(state, ref locationName))
                {
                    return true;
                }

                state.CheckLocation(locationName);

                // ShopItem.SetPurchased has already handled payment, required
                // items, purchase flags, callbacks and its save-facing state.
                // SavedItem.TryGet also treats this void Get call as successful,
                // so Crow Feathers can finish its normal reward flow. Skipping
                // this reward body suppresses the vanilla upgrade and its
                // misleading Tool Kit popup. AP supplies the actual reward.
                return false;
            }
        }

        [HarmonyPatch(
            typeof(ShopItem),
            nameof(ShopItem.SetPurchased),
            new Type[] { typeof(Action), typeof(int) })]
        internal static class ShopItemSetPurchasedPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                ShopItem __instance,
                Action onComplete)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    __instance == null ||
                    !CoreLocationManifest.TryGetShopLocation(
                        __instance.name,
                        out NativeShopLocation shopLocation))
                {
                    return true;
                }

                if (!state.IsRandomized(shopLocation.Type))
                {
                    return true;
                }

                string locationName = shopLocation.LocationName;
                if (!IsActiveLocation(state, ref locationName))
                {
                    return true;
                }

                if (state.IsLocationChecked(locationName))
                {
                    onComplete?.Invoke();
                    return false;
                }

                if (!UsesRosaries(__instance, out string currencyError))
                {
                    ReportBlockingPurchaseError(
                        locationName,
                        currencyError
                    );
                    return false;
                }

                if (!TryGetPurchaseConditionals(
                        __instance,
                        out Array conditionals,
                        out MethodInfo tryInstantiateMethod,
                        out string conditionalError))
                {
                    ReportBlockingPurchaseError(
                        locationName,
                        conditionalError
                    );
                    return false;
                }

                try
                {
                    // Exact vanilla conditional spawns run before charging so a
                    // reflection failure cannot consume currency.
                    if (tryInstantiateMethod != null)
                    {
                        foreach (object conditional in conditionals)
                        {
                            tryInstantiateMethod.Invoke(conditional, null);
                        }
                    }

                    // Every exact shop asset in this manifest uses Rosaries.
                    CurrencyManager.TakeGeo(__instance.Cost);
                    state.CheckLocation(locationName);
                    if (shopLocation.Type == ItemType.Map)
                    {
                        ShakraStockPersistencePatches
                            .TryFinalizeShakraDeparture(
                                state,
                                PlayerData.instance
                            );
                    }
                    CollectableItemManager.IncrementVersion();
                    onComplete?.Invoke();
                }
                catch (Exception exception)
                {
                    ReportBlockingPurchaseError(
                        locationName,
                        "The purchase hook failed.",
                        exception is TargetInvocationException &&
                        exception.InnerException != null
                            ? exception.InnerException
                            : exception
                    );
                }

                // Charging always ends this path. Falling through would leak the vanilla
                // map/pin reward and could charge the player twice.
                return false;
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_IsPurchased")]
        internal static class ShopItemIsPurchasedPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(ShopItem __instance, ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !TryResolveShopLocation(__instance, out string locationName))
                {
                    return true;
                }

                __result = state.IsLocationChecked(locationName);
                return false;
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_DisplayName")]
        internal static class ShopItemDisplayNamePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(ShopItem __instance, ref string __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !TryResolveShopPreviewLocation(
                        __instance,
                        out string locationName))
                {
                    return true;
                }

                if (ShopPatches.TryGetPresentationHint(
                        locationName,
                        out string user,
                        out string item,
                        out ItemFlags flags))
                {
                    __result = user + "'s " + item;
                }
                else
                {
                    __result = "AP Item";
                }

                __result = AlphabetModeManager.FilterDirectText(__result);
                return false;
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_Description")]
        internal static class ShopItemDescriptionPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(ShopItem __instance, ref string __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !TryResolveShopPreviewLocation(
                        __instance,
                        out string locationName))
                {
                    return true;
                }

                if (!ShopPatches.TryGetPresentationHint(
                        locationName,
                        out string user,
                        out string item,
                        out ItemFlags flags))
                {
                    __result = AlphabetModeManager.FilterDirectText(
                        "Something for someone else, maybe..."
                    );
                    return false;
                }

                __result = user + "'s " + item + ".\r\n";
                switch (flags)
                {
                    case ItemFlags.Advancement:
                        __result += "It is very important!";
                        break;
                    case ItemFlags.NeverExclude:
                        __result += "Seems useful.";
                        break;
                    case ItemFlags.Trap:
                        __result += "Seems fun...";
                        break;
                    default:
                        __result += "Seems not important.";
                        break;
                }

                __result = AlphabetModeManager.FilterDirectText(__result);
                return false;
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_ItemSprite")]
        internal static class ShopItemItemSpritePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(ShopItem __instance, ref Sprite __result)
            {
                SaveState state = SaveState.Instance;
                RandomizerPlugin plugin = RandomizerPlugin.Instance;
                if (state == null ||
                    plugin == null ||
                    !TryResolveShopPreviewLocation(
                        __instance,
                        out string locationName))
                {
                    return true;
                }

                if (ShopPatches.TryGetPresentationHint(
                        locationName,
                        out _,
                        out _,
                        out ItemFlags flags))
                {
                    __result = plugin.GetItemClassificationIcon(flags);
                }
                else
                {
                    __result =
                        plugin.MapCheckIcon ?? plugin.ArchipelagoIcon;
                }
                return false;
            }
        }
    }
}
