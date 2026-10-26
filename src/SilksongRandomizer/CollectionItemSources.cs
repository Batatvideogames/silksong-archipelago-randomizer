using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SilksongRandomizer.Patches;
using TeamCherry.Localization;

namespace SilksongRandomizer
{
    internal static class CollectionItemSources
    {
        internal static MateriumItem FindSample(string name)
        {
            var manager = MateriumItemManager.Instance;
            return manager == null ? null : manager.MasterList.FirstOrDefault(item => item.name == name);
        }

        internal static void GrantSample(string name)
        {
            var item = FindSample(name);
            if (item == null) throw new InvalidOperationException("Materium item is not ready: " + name);
            item.Get(false);
        }

        [HarmonyPatch(typeof(BasicNPC), "OnEndDialogue")]
        private static class SampleInspection
        {
            private static void Prefix(BasicNPC __instance, LocalisedString[] ___talkText,
                ref List<SavedItem> ___giveOnFirstTalkItems, out List<SavedItem> __state)
            {
                __state = null;
                var state = SaveState.Instance;
                if (state?.IsRoomBound != true || !state.IsRandomized(ItemType.Materium) ||
                    ___talkText == null || ___talkText.Length != 1 || ___talkText[0].Sheet != "Inspect" ||
                    Utils.GetHierarchyPath(__instance.transform) != "Inspect Region") return;
                string scene = __instance.gameObject.scene.name;
                string key = ___talkText[0].Key;
                string location, asset;
                if (scene == "Dock_02" && key == "DOCKS_FLINTSTONE_BUCKET")
                { location = "Flintstone"; asset = "Smeltstone"; }
                else if (scene == "Peak_05e" && key == "MAGNETITE_OUTCROP")
                { location = "Magnetite Outcrop"; asset = "Magnetite"; }
                else return;
                if (!state.IsLocationEnabled(location) || !state.IsLocationInSeed(location) ||
                    ___giveOnFirstTalkItems == null || !___giveOnFirstTalkItems.Any(item => item != null && item.name == asset)) return;
                __state = ___giveOnFirstTalkItems;
                ___giveOnFirstTalkItems = __state.Select(item => item != null && item.name == asset
                    ? CollectibleSourcePatches.GetProxyItem(location, ItemType.Materium) : item).ToList();
            }

            private static void Finalizer(ref List<SavedItem> ___giveOnFirstTalkItems, List<SavedItem> __state)
            {
                if (__state != null) ___giveOnFirstTalkItems = __state;
            }
        }
    }
}
