using Archipelago.MultiClient.Net.Enums;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMProOld;
using UnityEngine;
using static AntRegion;

namespace SilksongRandomizer.Patches
{
    internal class CrestSlotPatches
    {
        private readonly struct TextLayout
        {
            internal readonly bool AutoSizing;
            internal readonly bool WordWrapping;
            internal readonly float FontSize;
            internal readonly float FontSizeMin;
            internal readonly float FontSizeMax;
            internal readonly TextOverflowModes OverflowMode;
            internal readonly int MaxVisibleLines;

            internal TextLayout(TextMeshPro text)
            {
                AutoSizing = text.enableAutoSizing;
                WordWrapping = text.enableWordWrapping;
                FontSize = text.fontSize;
                FontSizeMin = text.fontSizeMin;
                FontSizeMax = text.fontSizeMax;
                OverflowMode = text.OverflowMode;
                MaxVisibleLines = text.maxVisibleLines;
            }

            internal void Restore(TextMeshPro text)
            {
                text.enableAutoSizing = AutoSizing;
                text.enableWordWrapping = WordWrapping;
                text.fontSizeMin = FontSizeMin;
                text.fontSizeMax = FontSizeMax;
                text.fontSize = FontSize;
                text.OverflowMode = OverflowMode;
                text.maxVisibleLines = MaxVisibleLines;
            }
        }

        private readonly struct CrestSlotKey : IEquatable<CrestSlotKey>
        {
            private readonly string crestName;
            private readonly int type;
            private readonly int x;
            private readonly int y;

            internal CrestSlotKey(
                string crestName,
                ToolCrest.SlotInfo slotInfo)
            {
                this.crestName = crestName;
                type = (int)slotInfo.Type;
                x = (int)slotInfo.Position.x;
                y = (int)slotInfo.Position.y;
            }

            public bool Equals(CrestSlotKey other)
            {
                return type == other.type &&
                       x == other.x &&
                       y == other.y &&
                       string.Equals(
                           crestName,
                           other.crestName,
                           StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is CrestSlotKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = crestName == null
                        ? 0
                        : StringComparer.Ordinal.GetHashCode(crestName);
                    hash = (hash * 397) ^ type;
                    hash = (hash * 397) ^ x;
                    return (hash * 397) ^ y;
                }
            }
        }

        private readonly struct CrestSlotNames
        {
            internal readonly string ItemName;
            internal readonly string LocationName;

            internal CrestSlotNames(
                string itemName,
                string locationName)
            {
                ItemName = itemName;
                LocationName = locationName;
            }
        }

        private sealed class DescriptionRenderState
        {
            internal TextLayout OriginalDescriptionLayout;
            internal TextLayout OriginalNameLayout;
            internal bool HasOriginalDescriptionLayout;
            internal bool HasOriginalNameLayout;
            internal bool HintLayoutApplied;
            internal InventoryItemSelectable Selectable;
            internal SaveState OwnerState;
            internal string LocationName;
            internal string ItemName;
            internal string BaseText;
            internal string RenderedText;
            internal string RenderedName;
            internal string HintDisplayName;
            internal string HintClassification;
            internal string LocketStatusDetail;
            internal string LocketStatusText;
            internal bool LocationChecked;
            internal bool ItemReceived;
            internal ToolItem EquippedItem;
            internal bool CanUnlockSlot;
            internal bool HintResolved;
            internal int NextHintPollFrame;
        }

        private const int ConnectedHintPollFrames = 15;
        private const int DisconnectedHintPollFrames = 60;
        private const string MemoryLocketUsedText = "Memory Locket used";

        private static readonly int FilledSlotAnimation =
            Animator.StringToHash("Filled");

        private static readonly Dictionary<CrestSlotKey, CrestSlotNames>
            CrestSlotNameCache =
            new Dictionary<CrestSlotKey, CrestSlotNames>();

        private static readonly ConditionalWeakTable<
                TextMeshPro,
                DescriptionRenderState>
            DescriptionRenderStates =
            new ConditionalWeakTable<TextMeshPro, DescriptionRenderState>();

        private static SaveState cachedLocationOwner;
        private static LocationSet cachedLocationSet;
        private static Location[] cachedLocationArray;
        private static HashSet<string> cachedLocationNames;

        [HarmonyPatch(typeof(InventoryItemManager), "SetDisplay", new Type[] { typeof(InventoryItemSelectable) })]
        internal static class InventoryItemManager_SetDisplay_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(
                InventoryItemManager __instance,
                InventoryItemSelectable selectable,
                TextMeshPro ___descriptionText,
                TextMeshPro ___nameText,
                out bool __state)
            {
                __state = true;
                if ((object)___descriptionText == null ||
                    !TryGetRandomizedApSlot(
                        selectable,
                        out InventoryToolCrestSlot slot,
                        out CrestSlotNames slotNames))
                {
                    return true;
                }

                DescriptionRenderState renderState =
                    GetDescriptionRenderState(
                        ___descriptionText,
                        ___nameText);
                SaveState state = SaveState.Instance;
                bool locationChecked = IsLocationChecked(
                    state,
                    slotNames.LocationName);
                bool itemReceived = HasReceivedItem(
                    state,
                    slotNames.ItemName);
                ToolItem equippedItem = slot.EquippedItem;
                bool canUnlockSlot =
                    __instance is InventoryItemToolManager toolManager &&
                    toolManager.CanUnlockSlot;

                if (MatchesDisplayState(
                        renderState,
                        selectable,
                        state,
                        slotNames,
                        locationChecked,
                        itemReceived,
                        equippedItem,
                        canUnlockSlot) &&
                    string.Equals(
                        ___descriptionText.text,
                        renderState.RenderedText,
                        StringComparison.Ordinal) &&
                    ((object)___nameText == null ||
                     string.Equals(
                         ___nameText.text,
                         renderState.RenderedName,
                         StringComparison.Ordinal)))
                {
                    // Vanilla asks for a display refresh every frame while an
                    // unlock prompt pulses. The derived tool-manager overload
                    // continues after this patched base call, so it can keep
                    // the prompt current without clearing and rebuilding the
                    // cached AP text. A real selection or slot-state change
                    // misses this cache and runs the vanilla reset normally.
                    __state = false;
                    return false;
                }

                return true;
            }

            [HarmonyPostfix]
            public static void Postfix(
                InventoryItemManager __instance,
                InventoryItemSelectable selectable,
                TextMeshPro ___descriptionText,
                TextMeshPro ___nameText,
                bool __state)
            {
                if ((object)___descriptionText == null)
                {
                    return;
                }

                DescriptionRenderState renderState =
                    GetDescriptionRenderState(
                        ___descriptionText,
                        ___nameText);
                if (!TryGetRandomizedApSlot(
                        selectable,
                        out InventoryToolCrestSlot slot,
                        out CrestSlotNames slotNames))
                {
                    RestoreHintLayouts(
                        ___descriptionText,
                        ___nameText,
                        renderState);
                    ResetDescriptionIdentity(renderState);
                    return;
                }

                SaveState state = SaveState.Instance;
                bool locationChecked = IsLocationChecked(
                    state,
                    slotNames.LocationName);
                bool itemReceived = HasReceivedItem(
                    state,
                    slotNames.ItemName);
                ToolItem equippedItem = slot.EquippedItem;
                bool canUnlockSlot =
                    __instance is InventoryItemToolManager toolManager &&
                    toolManager.CanUnlockSlot;
                bool sameIdentity =
                    ReferenceEquals(renderState.Selectable, selectable) &&
                    ReferenceEquals(renderState.OwnerState, state) &&
                    string.Equals(
                        renderState.LocationName,
                        slotNames.LocationName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        renderState.ItemName,
                        slotNames.ItemName,
                        StringComparison.Ordinal);
                bool sameLiveState = sameIdentity &&
                    renderState.LocationChecked == locationChecked &&
                    renderState.ItemReceived == itemReceived &&
                    ReferenceEquals(
                        renderState.EquippedItem,
                        equippedItem) &&
                    renderState.CanUnlockSlot == canUnlockSlot;

                if (!sameIdentity)
                {
                    RestoreHintLayouts(
                        ___descriptionText,
                        ___nameText,
                        renderState);
                    renderState.Selectable = selectable;
                    renderState.OwnerState = state;
                    renderState.LocationName = slotNames.LocationName;
                    renderState.ItemName = slotNames.ItemName;
                    renderState.HintDisplayName = null;
                    renderState.HintClassification = null;
                    renderState.HintResolved = false;
                    renderState.NextHintPollFrame = 0;
                }
                else if (!sameLiveState)
                {
                    RestoreHintLayouts(
                        ___descriptionText,
                        ___nameText,
                        renderState);
                }

                renderState.LocationChecked = locationChecked;
                renderState.ItemReceived = itemReceived;
                renderState.EquippedItem = equippedItem;
                renderState.CanUnlockSlot = canUnlockSlot;

                if (__state)
                {
                    // Vanilla just restored the slot's own display. Its clean
                    // values allow both text objects to be restored when
                    // the selection moves away from this AP slot.
                    RestoreHintLayouts(
                        ___descriptionText,
                        ___nameText,
                        renderState);
                    renderState.BaseText = ___descriptionText.text ?? string.Empty;
                    renderState.RenderedName = (object)___nameText == null
                        ? null
                        : ___nameText.text;
                }
                else if (renderState.BaseText == null)
                {
                    renderState.BaseText = ___descriptionText.text ?? string.Empty;
                }

                if (!renderState.HintResolved &&
                    Time.frameCount >= renderState.NextHintPollFrame)
                {
                    if (state.GetHint(
                            slotNames.LocationName,
                            out string user,
                            out string item,
                            out ItemFlags flags))
                    {
                        renderState.HintDisplayName =
                            BuildHintDisplayName(user, item);
                        renderState.HintClassification =
                            BuildHintClassification(flags);
                        renderState.HintResolved = true;
                    }
                    else
                    {
                        bool connected = Archipelago.Instance != null &&
                                         Archipelago.Instance.Connected;
                        renderState.NextHintPollFrame = Time.frameCount +
                            (connected
                                ? ConnectedHintPollFrames
                                : DisconnectedHintPollFrames);
                    }
                }

                bool slotIsEmpty =
                    (object)renderState.EquippedItem == null;
                bool showHint = renderState.HintResolved && slotIsEmpty;
                bool showLocketStatus =
                    renderState.LocationChecked && slotIsEmpty;
                bool unlockPromptVisible =
                    IsUnlockPromptVisible(renderState);
                if (showHint || showLocketStatus || unlockPromptVisible)
                {
                    ApplyHintLayouts(
                        ___descriptionText,
                        ___nameText,
                        renderState,
                        unlockPromptVisible);
                }

                string detailText = showHint
                    ? renderState.HintClassification
                    : renderState.BaseText;
                string renderedText = unlockPromptVisible
                    ? string.Empty
                    : showLocketStatus
                        ? GetLocketStatusText(
                            renderState,
                            detailText)
                        : detailText;
                string renderedName = showHint
                    ? renderState.HintDisplayName
                    : renderState.RenderedName;
                if (!string.Equals(
                        ___descriptionText.text,
                        renderedText,
                        StringComparison.Ordinal))
                {
                    ___descriptionText.text = renderedText;
                }
                if ((object)___nameText != null &&
                    !string.Equals(
                        ___nameText.text,
                        renderedName,
                        StringComparison.Ordinal))
                {
                    ___nameText.text = renderedName;
                }
                renderState.RenderedText = renderedText;
                renderState.RenderedName = renderedName;
            }
        }

        [HarmonyPatch(
            typeof(InventoryItemToolManager),
            "SetDisplay",
            new Type[] { typeof(InventoryItemSelectable) })]
        internal static class InventoryItemToolManager_SetDisplay_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(
                InventoryItemSelectable selectable,
                CrestSocketUnlockInventoryDescription
                    ___slotUnlockDescExtra)
            {
                if (!TryGetRandomizedApSlot(
                        selectable,
                        out InventoryToolCrestSlot _,
                        out CrestSlotNames slotNames) ||
                    !IsLocationChecked(
                        SaveState.Instance,
                        slotNames.LocationName) ||
                    (object)___slotUnlockDescExtra == null)
                {
                    return;
                }

                // The native manager offers "Expand Crest" whenever the
                // selected slot is visually locked and another Locket is in
                // inventory. In AP, the slot may still be waiting for its
                // randomized unlock after its physical Locket check is
                // complete, so that prompt would falsely suggest a second
                // Locket can be spent.
                ___slotUnlockDescExtra.gameObject.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(InventoryToolCrestSlot), "PlayAnimSmall")]
        internal static class InventoryToolCrestSlot_PlayAnimSmall_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(
                InventoryToolCrestSlot __instance,
                ref int animId)
            {
                if (TryGetRandomizedApSlot(
                        __instance,
                        out CrestSlotNames slotNames) &&
                    IsLocationChecked(
                        SaveState.Instance,
                        slotNames.LocationName))
                {
                    // The native filled inset remains a persistent,
                    // allocation-free visual record that this slot already
                    // consumed a Memory Locket. The outer slot can remain
                    // locked while it waits for its randomized AP unlock.
                    animId = FilledSlotAnimation;
                }
            }
        }

        [HarmonyPatch(typeof(InventoryToolCrestSlot), "get_IsLocked", new Type[0])]
        internal static class InventoryToolCrestSlot_IsLocked_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                InventoryToolCrestSlot __instance,
                ref bool __result,
                bool ___isSelected)
            {
                if (!TryGetRandomizedApSlot(
                        __instance,
                        out CrestSlotNames slotNames))
                {
                    return true;
                }

                bool hasTool = __instance.EquippedItem;
                SaveState state = SaveState.Instance;
                bool locationChecked = IsLocationChecked(
                    state,
                    slotNames.LocationName);

                if (!___isSelected || hasTool || locationChecked)
                {
                    __result = !HasReceivedItem(
                        state,
                        slotNames.ItemName);
                    return false;
                }

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(InventoryToolCrestSlot), "UnlockHoldRoutine")]
        internal static class InventoryToolCrestSlot_UnlockHoldRoutine_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(
                InventoryToolCrestSlot __instance,
                ref IEnumerator __result,
                bool ___isSelected,
                InventoryItemToolManager ___manager)
            {
                if (!TryGetRandomizedApSlot(
                        __instance,
                        out CrestSlotNames slotNames))
                {
                    return;
                }

                SaveState state = SaveState.Instance;
                bool locationChecked = IsLocationChecked(
                    state,
                    slotNames.LocationName);
                bool shouldCheckLocation =
                    ___isSelected &&
                    !__instance.EquippedItem &&
                    !locationChecked;

                // UnlockHoldRoutine is an iterator, so none of its vanilla
                // Memory Locket consumption has run when this postfix sees
                // it. Discard a repeated iterator after the physical AP check
                // is complete. The slot remains governed by its received AP
                // item in IsLocked instead of being temporarily usable.
                if (___isSelected &&
                    !__instance.EquippedItem &&
                    locationChecked)
                {
                    __result = EmptyUnlockRoutine();
                    return;
                }

                __result = CheckLocationAfterUnlock(
                    __result,
                    slotNames.LocationName,
                    shouldCheckLocation,
                    ___manager);
            }

            private static IEnumerator EmptyUnlockRoutine()
            {
                yield break;
            }

            private static IEnumerator CheckLocationAfterUnlock(
                IEnumerator original,
                string locationName,
                bool shouldCheckLocation,
                InventoryItemToolManager manager)
            {
                while (original.MoveNext())
                {
                    yield return original.Current;
                }

                if (shouldCheckLocation &&
                    !IsLocationChecked(SaveState.Instance, locationName))
                {
                    SaveState.Instance.CheckLocation(locationName);
                    if (IsLocationChecked(
                            SaveState.Instance,
                            locationName) &&
                        (object)manager != null)
                    {
                        // The native routine redraws before the AP location is
                        // recorded. Refresh once after that state change so
                        // the filled inset and explicit status appear without
                        // requiring the player to move the cursor away.
                        manager.RefreshTools();
                    }
                }
            }
        }

        private static DescriptionRenderState GetDescriptionRenderState(
            TextMeshPro descriptionText,
            TextMeshPro nameText)
        {
            DescriptionRenderState renderState =
                DescriptionRenderStates.GetValue(
                    descriptionText,
                    _ => new DescriptionRenderState());
            if (!renderState.HasOriginalDescriptionLayout)
            {
                renderState.OriginalDescriptionLayout =
                    new TextLayout(descriptionText);
                renderState.HasOriginalDescriptionLayout = true;
            }
            if ((object)nameText != null &&
                !renderState.HasOriginalNameLayout)
            {
                renderState.OriginalNameLayout =
                    new TextLayout(nameText);
                renderState.HasOriginalNameLayout = true;
            }
            return renderState;
        }

        private static bool IsUnlockPromptVisible(
            DescriptionRenderState renderState)
        {
            return renderState.CanUnlockSlot &&
                   (object)renderState.EquippedItem == null &&
                   !renderState.LocationChecked;
        }

        private static string GetLocketStatusText(
            DescriptionRenderState renderState,
            string detailText)
        {
            detailText = detailText ?? string.Empty;
            if (!string.Equals(
                    renderState.LocketStatusDetail,
                    detailText,
                    StringComparison.Ordinal) ||
                renderState.LocketStatusText == null)
            {
                renderState.LocketStatusDetail = detailText;
                renderState.LocketStatusText =
                    string.IsNullOrWhiteSpace(detailText)
                        ? MemoryLocketUsedText
                        : string.Concat(
                            MemoryLocketUsedText,
                            ": ",
                            detailText);
            }

            return renderState.LocketStatusText;
        }

        private static string BuildHintDisplayName(
            string playerName,
            string itemName)
        {
            // The native title rectangle is only one line tall. Put the item
            // first so ellipsis can shorten a long multiworld player name
            // without hiding the reward the player is deciding whether to
            // spend a Memory Locket on.
            string displayItem = string.IsNullOrWhiteSpace(itemName)
                ? "AP Item"
                : itemName.Trim();
            string displayPlayer = (playerName ?? string.Empty).Trim();
            return string.IsNullOrEmpty(displayPlayer)
                ? displayItem
                : string.Concat(displayItem, " (", displayPlayer, ")");
        }

        private static void ApplyHintLayouts(
            TextMeshPro descriptionText,
            TextMeshPro nameText,
            DescriptionRenderState renderState,
            bool unlockPromptVisible)
        {
            if (renderState.HintLayoutApplied)
            {
                return;
            }

            if ((object)nameText != null &&
                renderState.HasOriginalNameLayout)
            {
                // This is a physically one-line native title box. The randomized
                // item appears first at a readable minimum size. The trailing
                // player name is ellipsized if the full title still cannot fit.
                nameText.enableWordWrapping = false;
                nameText.enableAutoSizing = true;
                nameText.fontSizeMax =
                    renderState.OriginalNameLayout.FontSize;
                nameText.fontSizeMin = Math.Max(
                    10f,
                    renderState.OriginalNameLayout.FontSize * 0.55f);
                nameText.OverflowMode = TextOverflowModes.Ellipsis;
                nameText.maxVisibleLines = 1;
            }

            descriptionText.enableWordWrapping = false;
            descriptionText.enableAutoSizing = true;
            if (unlockPromptVisible)
            {
                // The fixed Expand Crest / HOLD prompt occupies the
                // description row. The AP classification is already present
                // in the network log, so suppress it here instead of drawing
                // through those controls.
                descriptionText.maxVisibleLines = 0;
            }
            else
            {
                descriptionText.fontSizeMax =
                    renderState.OriginalDescriptionLayout.FontSize;
                descriptionText.fontSizeMin = Math.Max(
                    8f,
                    renderState.OriginalDescriptionLayout.FontSize * 0.55f);
                descriptionText.OverflowMode =
                    TextOverflowModes.Ellipsis;
                descriptionText.maxVisibleLines = 1;
            }
            renderState.HintLayoutApplied = true;
        }

        private static void RestoreHintLayouts(
            TextMeshPro descriptionText,
            TextMeshPro nameText,
            DescriptionRenderState renderState)
        {
            if (!renderState.HintLayoutApplied)
            {
                return;
            }

            renderState.OriginalDescriptionLayout.Restore(descriptionText);
            if ((object)nameText != null &&
                renderState.HasOriginalNameLayout)
            {
                renderState.OriginalNameLayout.Restore(nameText);
            }
            renderState.HintLayoutApplied = false;
        }

        private static void ResetDescriptionIdentity(
            DescriptionRenderState renderState)
        {
            renderState.Selectable = null;
            renderState.OwnerState = null;
            renderState.LocationName = null;
            renderState.ItemName = null;
            renderState.BaseText = null;
            renderState.RenderedText = null;
            renderState.RenderedName = null;
            renderState.HintDisplayName = null;
            renderState.HintClassification = null;
            renderState.LocketStatusDetail = null;
            renderState.LocketStatusText = null;
            renderState.LocationChecked = false;
            renderState.ItemReceived = false;
            renderState.EquippedItem = null;
            renderState.CanUnlockSlot = false;
            renderState.HintResolved = false;
            renderState.NextHintPollFrame = 0;
        }

        private static bool MatchesDisplayState(
            DescriptionRenderState renderState,
            InventoryItemSelectable selectable,
            SaveState state,
            CrestSlotNames slotNames,
            bool locationChecked,
            bool itemReceived,
            ToolItem equippedItem,
            bool canUnlockSlot)
        {
            return ReferenceEquals(renderState.Selectable, selectable) &&
                   ReferenceEquals(renderState.OwnerState, state) &&
                   string.Equals(
                       renderState.LocationName,
                       slotNames.LocationName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       renderState.ItemName,
                       slotNames.ItemName,
                       StringComparison.Ordinal) &&
                   renderState.LocationChecked == locationChecked &&
                   renderState.ItemReceived == itemReceived &&
                   ReferenceEquals(
                       renderState.EquippedItem,
                       equippedItem) &&
                   renderState.CanUnlockSlot == canUnlockSlot;
        }

        private static string BuildHintClassification(ItemFlags flags)
        {
            string classification;
            if ((flags & ItemFlags.Advancement) != 0)
            {
                classification = "Very important";
            }
            else if ((flags & ItemFlags.NeverExclude) != 0)
            {
                classification = "Useful";
            }
            else if ((flags & ItemFlags.Trap) != 0)
            {
                classification = "Trap";
            }
            else
            {
                classification = "Not important";
            }

            return classification;
        }

        private static bool TryGetRandomizedApSlot(
            InventoryItemSelectable selectable,
            out InventoryToolCrestSlot slot,
            out CrestSlotNames slotNames)
        {
            slot = selectable as InventoryToolCrestSlot;
            if ((object)slot == null)
            {
                slotNames = default;
                return false;
            }

            return TryGetRandomizedApSlot(slot, out slotNames);
        }

        private static bool TryGetRandomizedApSlot(
            InventoryToolCrestSlot slot,
            out CrestSlotNames slotNames)
        {
            slotNames = default;
            SaveState state = SaveState.Instance;
            if ((object)slot == null ||
                slot.Crest == null ||
                slot.Crest.CrestData == null ||
                state == null ||
                !state.IsRandomized(ItemType.CrestSlot))
            {
                return false;
            }

            slotNames = GetSlotNames(slot.Crest, slot.SlotInfo);
            return HasApLocation(state, slotNames.LocationName);
        }

        private static bool HasApLocation(
            SaveState state,
            string locationName)
        {
            if (state == null ||
                state.locations == null ||
                state.locations.Locations == null ||
                string.IsNullOrWhiteSpace(locationName))
            {
                return false;
            }

            LocationSet locationSet = state.locations;
            Location[] locations = locationSet.Locations;
            if (!ReferenceEquals(cachedLocationOwner, state) ||
                !ReferenceEquals(cachedLocationSet, locationSet) ||
                !ReferenceEquals(cachedLocationArray, locations) ||
                cachedLocationNames == null)
            {
                cachedLocationOwner = state;
                cachedLocationSet = locationSet;
                cachedLocationArray = locations;
                cachedLocationNames = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (Location location in locations)
                {
                    if (location != null &&
                        !string.IsNullOrWhiteSpace(location.Name))
                    {
                        cachedLocationNames.Add(location.Name);
                    }
                }
            }

            return cachedLocationNames.Contains(locationName);
        }

        private static bool IsLocationChecked(
            SaveState state,
            string canonicalLocationName)
        {
            return state != null &&
                   state.checkedLocations != null &&
                   state.checkedLocations.Contains(canonicalLocationName);
        }

        private static bool HasReceivedItem(
            SaveState state,
            string canonicalItemName)
        {
            return state != null &&
                   state.receivedItems != null &&
                   state.receivedItems.Contains(canonicalItemName);
        }

        private static CrestSlotNames GetSlotNames(
            InventoryToolCrest crest,
            ToolCrest.SlotInfo slotInfo)
        {
            string crestName = CrestNames.GetPublicCrestName(crest.name);
            CrestSlotKey key = new CrestSlotKey(crestName, slotInfo);
            if (CrestSlotNameCache.TryGetValue(
                    key,
                    out CrestSlotNames slotNames))
            {
                return slotNames;
            }

            string coordinates = string.Concat(
                slotInfo.Type,
                " ",
                (int)slotInfo.Position.x,
                " ",
                (int)slotInfo.Position.y);
            string itemName = ItemSet.GetCanonicalItemName(
                string.Concat(
                    crestName,
                    " Slot: ",
                    coordinates));
            string locationName = LocationSet.GetCanonicalLocationName(
                string.Concat(
                    crestName,
                    " Slot Unlock: ",
                    coordinates));
            slotNames = new CrestSlotNames(itemName, locationName);
            CrestSlotNameCache[key] = slotNames;
            return slotNames;
        }

        public static string GetSlotNameAsItem(InventoryToolCrest crest, ToolCrest.SlotInfo slotInfo)
        {
            return GetSlotNames(crest, slotInfo).ItemName;
        }

        public static string GetSlotNameAsLocation(InventoryToolCrest crest, ToolCrest.SlotInfo slotInfo)
        {
            return GetSlotNames(crest, slotInfo).LocationName;
        }
    }
}
