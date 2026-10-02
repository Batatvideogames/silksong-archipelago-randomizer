using System;
using System.Linq;
using HarmonyLib;
using TMProOld;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class ArchipelagoInventoryPatches
    {
        [HarmonyPatch(typeof(InventoryPaneList), "Awake")]
        private static class RegisterPage
        {
            [HarmonyPostfix]
            private static void Postfix(InventoryPaneList __instance, ref InventoryPane[] ___panes)
            {
                if (___panes == null || ___panes.Any(pane => pane is ArchipelagoInventoryPage)) return;
                GameObject root = null;
                try
                {
                    var template = ___panes.FirstOrDefault(pane => pane != null && pane.name == "Inv")?
                        .transform.Find("Description Pane/Text Desc")?.GetComponent<TextMeshPro>();
                    if (template == null) throw new InvalidOperationException("Native inventory text template was not found.");
                    root = new GameObject("Archipelago");
                    root.SetActive(false);
                    root.layer = __instance.gameObject.layer;
                    root.transform.SetParent(__instance.transform, false);
                    var page = root.AddComponent<ArchipelagoInventoryPage>();
                    page.Initialize(__instance, template);
                    ___panes = ___panes.Concat(new InventoryPane[] { page }).ToArray();
                }
                catch (Exception error)
                {
                    if (root != null) UnityEngine.Object.Destroy(root);
                    RandomizerPlugin.Log?.LogError("[RANDOMIZER] Could not create the Archipelago inventory page: " + error);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryPaneList), "IsPaneAvailable")]
        private static class PageAvailability
        {
            [HarmonyPrefix]
            private static bool Prefix(InventoryPane pane, ref bool __result)
            {
                if (!(pane is ArchipelagoInventoryPage)) return true;
                __result = pane.IsAvailable;
                return false;
            }
        }

        [HarmonyPatch(typeof(InventoryPane), "get_DisplayName")]
        private static class PageTitle
        {
            [HarmonyPrefix]
            private static bool Prefix(InventoryPane __instance, ref string __result)
            {
                if (!(__instance is ArchipelagoInventoryPage)) return true;
                __result = "<size=80%>Archipelago</size>";
                return false;
            }
        }

        [HarmonyPatch(typeof(InventoryPane), "get_ListIcon")]
        private static class PageIcon
        {
            [HarmonyPrefix]
            private static bool Prefix(InventoryPane __instance, ref Sprite __result)
            {
                if (!(__instance is ArchipelagoInventoryPage)) return true;
                __result = ((ArchipelagoInventoryPage)__instance).TabIcon;
                return false;
            }
        }
    }
}
