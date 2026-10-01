using HarmonyLib;
using SilksongRandomizer.AlphabetMode;
using System;
using System.Collections.Generic;
using System.Text;
using TMProOld;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class InventoryInfoPatches
    {
        private const string NativeSimpleKeyName = "Simple Key";
        private const string NativeQuillName = "Quill";
        private const string MapItemPrefix = "Map: ";

        private static readonly string[] SimpleKeyItemNames =
        {
            SimpleKeyDoorManager.WormwaysKey,
            SimpleKeyDoorManager.DeepDocksKey,
            SimpleKeyDoorManager.GreenPrinceKey,
            SimpleKeyDoorManager.RosaryBankKey,
        };

        private static readonly string[] SimpleKeyDisplayNames =
        {
            "Wormways",
            "Deep Docks",
            "Green Prince",
            "Rosary Bank",
        };


        private static bool TryGetSimpleKeyState(out SaveState state)
        {
            state = SaveState.Instance;
            return state != null &&
                state.IsRoomBound &&
                state.receivedItems != null &&
                state.IsRandomized(ItemType.SimpleKey);
        }

        private static bool IsNativeSimpleKey(CollectableItem item)
        {
            return item != null &&
                string.Equals(
                    item.name,
                    NativeSimpleKeyName,
                    StringComparison.Ordinal
                );
        }

        private static bool TryGetMapState(out SaveState state)
        {
            state = SaveState.Instance;
            return state != null &&
                state.IsRoomBound &&
                state.items?.items != null &&
                state.receivedItems != null &&
                state.IsRandomized(ItemType.Map);
        }

        private static bool IsNativeQuill(CollectableItem item)
        {
            return item != null &&
                string.Equals(
                    item.name,
                    NativeQuillName,
                    StringComparison.Ordinal
                );
        }

        private static void AppendMapSummary(
            StringBuilder builder,
            SaveState state
        )
        {
            StringBuilder names = new StringBuilder();
            int ownedCount = 0;
            int totalCount = 0;
            foreach (Item item in state.items.items)
            {
                if (item == null ||
                    item.Type != ItemType.Map)
                {
                    continue;
                }

                if (totalCount > 0)
                {
                    names.Append(", ");
                }

                bool owned = state.receivedItems.Contains(item.Name);
                if (owned)
                {
                    ownedCount++;
                }

                string displayName = item.Name.StartsWith(
                    MapItemPrefix,
                    StringComparison.Ordinal
                )
                    ? item.Name.Substring(MapItemPrefix.Length)
                    : item.Name;
                names.Append(
                    owned ? "<color=#FFFFFF>" : "<color=#777777>"
                );
                names.Append(displayName);
                names.Append("</color>");
                totalCount++;
            }

            builder.Append("AP maps owned: ");
            builder.Append(ownedCount);
            builder.Append('/');
            builder.Append(totalCount);
            if (totalCount == 0)
            {
                builder.Append("\nNone configured");
                return;
            }

            builder.Append('\n');
            builder.Append(names);
        }

        private static bool ShouldSuppressNativeMapPrompt(
            Component prompt
        )
        {
            if (prompt == null ||
                !TryGetMapState(out SaveState unusedState))
            {
                return false;
            }

            InventoryItemCollectable collectable =
                prompt.GetComponent<InventoryItemCollectable>();
            return collectable != null &&
                IsNativeQuill(collectable.Item);
        }

        private static int GetOwnedSimpleKeyCount(SaveState state)
        {
            int count = 0;
            foreach (string itemName in SimpleKeyItemNames)
            {
                if (state.receivedItems.Contains(itemName))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AppendSimpleKeyLine(
            StringBuilder builder,
            SaveState state,
            int index
        )
        {
            bool owned = state.receivedItems.Contains(
                SimpleKeyItemNames[index]
            );
            builder.Append(
                owned ? "<color=#FFFFFF>" : "<color=#777777>"
            );
            builder.Append(SimpleKeyDisplayNames[index]);
            builder.Append("</color>");
        }

        [HarmonyPatch(
            typeof(InventoryItemCollectableManager),
            "GetItems"
        )]
        private static class InventoryItemCollectableManagerGetItemsPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                ref List<CollectableItem> __result
            )
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRoomBound ||
                    CollectableItemManager.IsInHiddenMode())
                {
                    return;
                }

                List<CollectableItem> items = __result == null
                    ? new List<CollectableItem>()
                    : new List<CollectableItem>(__result);
                bool changed = false;

                if (state.IsRandomized(ItemType.SimpleKey))
                {
                    CollectableItem simpleKey =
                        CollectableItemManager.GetItemByName(
                            NativeSimpleKeyName
                        );
                    if (simpleKey != null &&
                        !items.Contains(simpleKey))
                    {
                        if (!simpleKey.IsSeen)
                        {
                            simpleKey.IsSeen = true;
                        }
                        items.Add(simpleKey);
                        changed = true;
                    }
                }

                if (changed)
                {
                    __result = items;
                }
            }
        }

        [HarmonyPatch(
            typeof(InventoryItemCollectable),
            "UpdateItemDisplay"
        )]
        private static class InventoryItemCollectableUpdateItemDisplayPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                InventoryItemCollectable __instance,
                TextMeshPro ___amountText,
                Color ___regularAmountTextColor
            )
            {
                if (!IsNativeSimpleKey(__instance.Item) ||
                    ___amountText == null ||
                    !TryGetSimpleKeyState(out SaveState state))
                {
                    return;
                }

                int count = GetOwnedSimpleKeyCount(state);
                ___amountText.text = count.ToString();
                ___amountText.color = ___regularAmountTextColor;
            }
        }

        [HarmonyPatch(
            typeof(InventoryItemCollectable),
            nameof(InventoryItemCollectable.Description),
            MethodType.Getter
        )]
        private static class InventoryItemCollectableDescriptionPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                InventoryItemCollectable __instance,
                ref string __result
            )
            {
                if (IsNativeSimpleKey(__instance.Item) &&
                    TryGetSimpleKeyState(out SaveState keyState))
                {
                    StringBuilder keyBuilder = new StringBuilder();
                    if (!string.IsNullOrWhiteSpace(__result))
                    {
                        keyBuilder.Append(__result);
                        keyBuilder.Append("\n\n");
                    }

                    for (
                        int index = 0;
                        index < SimpleKeyItemNames.Length;
                        index++
                    )
                    {
                        AppendSimpleKeyLine(
                            keyBuilder,
                            keyState,
                            index
                        );
                        if (index + 1 < SimpleKeyItemNames.Length)
                        {
                            keyBuilder.Append('\n');
                        }
                    }

                    __result = AlphabetModeManager.FilterDirectText(keyBuilder.ToString());
                    return;
                }

                if (!IsNativeQuill(__instance.Item) ||
                    !TryGetMapState(out SaveState mapState))
                {
                    return;
                }

                StringBuilder mapBuilder = new StringBuilder();
                AppendMapSummary(mapBuilder, mapState);
                __result = AlphabetModeManager.FilterDirectText(mapBuilder.ToString());
            }
        }

        [HarmonyPatch(
            typeof(CollectableItemStates),
            nameof(CollectableItemStates.GetButtonPromptData)
        )]
        private static class CollectableItemStatesGetButtonPromptDataPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                CollectableItemStates __instance,
                ref InventoryItemButtonPromptData[] __result
            )
            {
                if (IsNativeQuill(__instance) &&
                    TryGetMapState(out SaveState unusedState))
                {
                    __result = null;
                }
            }
        }

        [HarmonyPatch(
            typeof(InventoryItemButtonPrompt),
            "OnShow",
            new[]
            {
                typeof(InventoryItemButtonPromptDisplayList),
                typeof(InventoryItemButtonPromptData),
            }
        )]
        private static class InventoryItemButtonPromptOnShowPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                InventoryItemButtonPrompt __instance
            )
            {
                return !ShouldSuppressNativeMapPrompt(__instance);
            }
        }

        [HarmonyPatch(
            typeof(InventoryItemComboButtonPrompt),
            "OnShow",
            new[]
            {
                typeof(InventoryItemButtonPromptDisplayList),
                typeof(InventoryItemComboButtonPromptDisplay.Display),
            }
        )]
        private static class InventoryItemComboButtonPromptOnShowPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                InventoryItemComboButtonPrompt __instance
            )
            {
                return !ShouldSuppressNativeMapPrompt(__instance);
            }
        }
    }
}
