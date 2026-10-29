using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TeamCherry.Localization;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.U2D;

namespace SilksongRandomizer
{
    internal static class BellhomeItems
    {
        private const string PaintShop = "Bellhart Furnishing Paint";
        private const string ColourShop = "Bellhome Colour Selection";
        private static ShopItem colourChoice;
        private static AsyncOperationHandle<SpriteAtlas> iconAtlas;
        private static Sprite[] icons;

        private static readonly Dictionary<string, string> Furnishings = new Dictionary<string, string>
        {
            { "Desk", "BelltownFurnishingDesk" },
            { "Gleamlights", "BelltownFurnishingFairyLights" },
            { "Personal Spa", "BelltownFurnishingSpa" },
            { "Gramophone", "BelltownFurnishingGramaphone" },
        };

        private static bool Enabled => SaveState.Instance?.IsRoomBound == true &&
            SaveState.Instance.IsRandomized(ItemType.Bellhome);

        private static bool OwnsPaint => Enabled &&
            SaveState.Instance.receivedItems.Contains("Bell Lacquer");

        private static bool IsPaintCheck(ShopItem item) => Enabled && item?.name == PaintShop &&
            SaveState.Instance.IsLocationInSeed("Bell Lacquer") &&
            SaveState.Instance.IsLocationEnabled("Bell Lacquer");

        internal static void Grant(string name)
        {
            if (PlayerData.instance == null) throw new InvalidOperationException("Bellhome furnishings are not ready.");
            if (Furnishings.TryGetValue(name, out string flag))
                PlayerData.instance.SetBool(flag, true);
            else if (name != "Bell Lacquer")
                throw new ArgumentException("Unknown Bellhome furnishing: " + name);
            CollectableItemManager.IncrementVersion();
        }

        internal static IEnumerator LoadIcons()
        {
            if (iconAtlas.IsValid()) yield break;
            var init = Addressables.InitializeAsync(false);
            yield return init;
            bool ready = init.Status == AsyncOperationStatus.Succeeded;
            Addressables.Release(init);
            if (!ready) yield break;
            IResourceLocation bundle = null;
            foreach (var locator in Addressables.ResourceLocators)
            {
                foreach (object key in locator.Keys)
                {
                    if (!locator.Locate(key, typeof(IAssetBundleResource), out var locations)) continue;
                    bundle = locations.FirstOrDefault(location => location.InternalId.Replace('\\', '/')
                        .EndsWith("/inventory_furniture_items.spriteatlas.bundle", StringComparison.OrdinalIgnoreCase));
                    if (bundle != null) break;
                }
                if (bundle != null) break;
            }
            if (bundle == null)
            {
                RandomizerPlugin.Log?.LogWarning("Bellhome icon atlas was not found.");
                yield break;
            }
            const string asset = "Assets/Sprites/_Atlases/Inventory_Furniture_Items.spriteatlas";
            var address = new ResourceLocationBase(asset, asset, typeof(BundledAssetProvider).FullName,
                typeof(SpriteAtlas), bundle);
            iconAtlas = Addressables.LoadAssetAsync<SpriteAtlas>(address);
            yield return iconAtlas;
            if (iconAtlas.Status != AsyncOperationStatus.Succeeded)
            {
                RandomizerPlugin.Log?.LogWarning("Bellhome icons could not be loaded.");
                Addressables.Release(iconAtlas);
                yield break;
            }
            icons = new Sprite[iconAtlas.Result.spriteCount];
            iconAtlas.Result.GetSprites(icons);
            foreach (Sprite icon in icons)
                if (icon != null) icon.name = icon.name.Replace("(Clone)", "");
        }

        [HarmonyPatch(typeof(ShopMenuStock), nameof(ShopMenuStock.SetStock))]
        private static class PaintChoiceStock
        {
            private static void Prefix(ref ShopItem[] newStock)
            {
                if (!Enabled || newStock == null || newStock.Any(item => item?.name == ColourShop)) return;
                ShopItem paint = newStock.FirstOrDefault(item => item?.name == PaintShop);
                if (!IsPaintCheck(paint)) return;
                if (colourChoice == null)
                {
                    colourChoice = UnityEngine.Object.Instantiate(paint);
                    colourChoice.name = ColourShop;
                    colourChoice.hideFlags = HideFlags.HideAndDontSave;
                    colourChoice.OverrideCostDelegate = () => 0;
                    Traverse.Create(colourChoice).Field("displayName").SetValue(new LocalisedString { Sheet = "TEMP", Key = "Bellhome Colour" });
                    Traverse.Create(colourChoice).Field("description").SetValue(new LocalisedString { Sheet = "TEMP", Key = "Choose a colour for your Bellhome." });
                }
                newStock = newStock.Concat(new[] { colourChoice }).ToArray();
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_IsAvailable")]
        private static class PaintChoiceAvailable
        {
            private static bool Prefix(ShopItem __instance, ref bool __result)
            {
                if (__instance.name != ColourShop) return true;
                __result = OwnsPaint && PlayerData.instance?.BelltownHouseVisited == true;
                return false;
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_IsPurchased")]
        private static class PaintChoicePurchased
        {
            private static bool Prefix(ShopItem __instance, ref bool __result)
            {
                if (__instance.name != ColourShop) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_HasSubItems")]
        private static class PaintCheckChoice
        {
            private static bool Prefix(ShopItem __instance, ref bool __result)
            {
                if (!IsPaintCheck(__instance)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_EventAfterPurchase")]
        private static class PaintCheckEvent
        {
            private static bool Prefix(ShopItem __instance, ref string __result)
            {
                if (!IsPaintCheck(__instance)) return true;
                __result = "PURCHASED FURNISHING";
                return false;
            }
        }

        [HarmonyPatch(typeof(ShopItem), nameof(ShopItem.SetPurchased), new[] { typeof(Action), typeof(int) })]
        private static class ChoosePaint
        {
            private static bool Prefix(ShopItem __instance, int subItemIndex)
            {
                if (__instance.name != ColourShop) return true;
                if (!OwnsPaint || subItemIndex < 0 || subItemIndex >= __instance.SubItemsCount) return false;
                PlayerData.instance.BelltownHousePaintComplete = false;
                return true;
            }
        }
    }
}
