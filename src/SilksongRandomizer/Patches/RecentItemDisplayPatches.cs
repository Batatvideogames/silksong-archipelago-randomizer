using System.Linq;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using SilksongRandomizer;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    // Just a bunch of things taken from RecentItemsDisplay rewrite to work with ItemIcons, 
    // some stuff can probably be changed here since we can more natively pull from the randomizer
    // but this seems to work so I haven't rewrote much of this
    [HarmonyPatch]
    public class RecentItemDisplayPatches
    {
        [HarmonyPatch(typeof(Archipelago), MethodType.Constructor, new System.Type[] { typeof(string) })]
        [HarmonyPostfix]
        public static void CtorPatch(Archipelago __instance)
        {
            __instance.OnItemSent += (item, flag) =>
            {
                string sentString = item.StartsWith("Sent ") ? item.Substring(5) : item;

                string[] itemSplitMsg = sentString.Split(" to ");
                
                if (itemSplitMsg.Length < 2)
                    return;

                string itemName = string.Join(" to ", itemSplitMsg.Take(itemSplitMsg.Length - 1));

                string itemReceiver = itemSplitMsg.Last();

                if (itemReceiver == __instance.SlotName)
                    return;

                Sprite itemSpriteFallback = RandomizerPlugin.Instance.GetItemIcon(item, flag);

                Sprite itemSprite = ItemIcons.GetSprite(itemName, itemSpriteFallback);

                string itemMessage = $"Sent {itemName.Replace("_", " ")}\nto {itemReceiver}";

                RecentItemsDisplay.QueueItem(itemSprite, itemMessage);
            };
        }
        
        [HarmonyPatch(typeof(Archipelago), nameof(Archipelago.MarkItemUnlocked)), HarmonyPrefix]
        public static void MarkItemUnlocked(Archipelago __instance, ItemInfo item)
        {
            string itemName = __instance.GetItemName(item);
            string playerName = item.Player.Name == "Server" ? "Start" : item.Player.Name;

            if (playerName == __instance.SlotName) {
                playerName = "";
            }
            else
            {
                playerName = $"\nfrom {playerName}";   
            }

            Sprite fallback = RandomizerPlugin.Instance.FillerIcon;
            Sprite sprite = ItemIcons.GetSprite(itemName, fallback);

            string message = $"{itemName.Replace("_", " ")}{playerName}";

            RecentItemsDisplay.QueueItem(sprite, message);
        }
    }
}