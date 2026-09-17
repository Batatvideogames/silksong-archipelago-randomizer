using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SilksongRandomizer.Patches
{
    internal static class VogHintManager
    {
        private static readonly FieldInfo CurrentListGroupsField =
            AccessTools.Field(typeof(CaravanTroupeHunter), "currentListGroups");
        private static readonly object CompletedLock = new object();
        private static readonly Queue<Action> CompletedActions = new Queue<Action>();

        private static bool IsHintShopActive()
        {
            return SaveState.Instance?.vogHintSettingsBound == true &&
                   Archipelago.Instance?.Connected == true;
        }

        internal static bool HasRemainingHints() =>
            IsHintShopActive() && VogAreaHintManager.HasRemainingHints();

        internal static bool HasHintHistory() =>
            IsHintShopActive() && VogAreaHintManager.HasHistory();

        internal static bool ShouldOpenHintShop() =>
            HasRemainingHints() || (HasHintHistory() && !ShouldHideNativeStock());

        internal static bool ShouldHideNativeStock() =>
            IsHintShopActive() && SaveState.Instance.checkMapMarkers != CheckMapMarkerMode.Off;

        internal static void Update()
        {
            Archipelago.Instance?.ImportTrackedHints();
            while (true)
            {
                Action action;
                lock (CompletedLock)
                {
                    if (CompletedActions.Count == 0)
                    {
                        break;
                    }
                    action = CompletedActions.Dequeue();
                }
                action();
            }
        }

        internal static void EnqueueCompleted(Action action)
        {
            lock (CompletedLock)
            {
                CompletedActions.Enqueue(action);
            }
        }

        internal static void Reset()
        {
            VogAreaHintManager.Reset();
            lock (CompletedLock)
            {
                CompletedActions.Clear();
            }
        }

        internal static void AddHintStock(CaravanTroupeHunter owner,
            List<ISimpleShopItem> stock, bool hideNativeStock)
        {
            if (owner == null || stock == null || !IsHintShopActive())
            {
                return;
            }
            if (hideNativeStock)
            {
                stock.Clear();
                if (CurrentListGroupsField?.GetValue(owner) is System.Collections.IList groups)
                {
                    groups.Clear();
                }
            }
            VogAreaHintManager.AddStock(owner, stock);
        }

        internal static bool TryPurchase(CaravanTroupeHunter owner,
            int itemIndex, int nativeItemCount)
        {
            return owner != null &&
                   VogAreaHintManager.TryPurchase(owner, itemIndex - nativeItemCount);
        }

        internal static void ShowHistory() => VogAreaHintManager.ShowHistory();

        internal static int GetNativeItemCount(CaravanTroupeHunter owner)
        {
            return owner != null && CurrentListGroupsField?.GetValue(owner) is
                System.Collections.ICollection groups ? groups.Count : 0;
        }
    }

    [HarmonyPatch(typeof(CaravanTroupeHunter), "GetItems")]
    internal static class VogHintStockPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            CaravanTroupeHunter __instance,
            ref List<ISimpleShopItem> __result
        )
        {
            VogHintManager.AddHintStock(
                __instance,
                __result,
                VogHintManager.ShouldHideNativeStock()
            );
        }
    }

    [HarmonyPatch(typeof(CaravanTroupeHunter), "OnPurchasedItem")]
    internal static class VogHintPurchasePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            CaravanTroupeHunter __instance,
            int itemIndex
        )
        {
            int nativeCount =
                VogHintManager.GetNativeItemCount(__instance);
            return !VogHintManager.TryPurchase(
                __instance,
                itemIndex,
                nativeCount
            );
        }
    }

    [HarmonyPatch(
        typeof(CheckSimpleShopMenuHasStock),
        "get_IsTrue"
    )]
    internal static class VogHintExhaustedStockRoutePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            CheckSimpleShopMenuHasStock __instance,
            ref bool __result
        )
        {
            if (!VogHintManager.ShouldHideNativeStock() ||
                VogHintManager.ShouldOpenHintShop() ||
                __instance?.Owner == null ||
                !string.Equals(
                    __instance.Owner.name,
                    "Caravan Troupe Hunter",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    __instance.Fsm?.Name,
                    "Dialogue",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    __instance.State?.Name,
                    "Already Complete?",
                    StringComparison.Ordinal
                ))
            {
                return true;
            }

            __instance.storeValue.Value = false;
            if (VogHintManager.HasHintHistory())
            {
                VogHintManager.ShowHistory();
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerDataVariableTest), "OnEnter")]
    internal static class VogHintDialogueRoutePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(PlayerDataVariableTest __instance)
        {
            if (!VogHintManager.ShouldOpenHintShop() ||
                __instance?.Owner == null ||
                !string.Equals(
                    __instance.Owner.name,
                    "Caravan Troupe Hunter",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    __instance.Fsm?.Name,
                    "Dialogue",
                    StringComparison.Ordinal
                ))
            {
                return true;
            }

            string stateName = __instance.State?.Name;
            if (string.Equals(
                    stateName,
                    "Already Complete?",
                    StringComparison.Ordinal
                ))
            {
                __instance.Fsm.Event(__instance.IsNotExpectedEvent);
                __instance.Finish();
                return false;
            }

            if (string.Equals(
                    stateName,
                    "Has Map?",
                    StringComparison.Ordinal
                ))
            {
                __instance.Fsm.Event(__instance.IsExpectedEvent);
                __instance.Finish();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(BoolTest), "OnEnter")]
    internal static class VogHintAllFleasRoutePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BoolTest __instance)
        {
            if (!VogHintManager.ShouldOpenHintShop() ||
                __instance?.Owner == null ||
                !string.Equals(
                    __instance.Owner.name,
                    "Caravan Troupe Hunter",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    __instance.Fsm?.Name,
                    "Dialogue",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    __instance.State?.Name,
                    "Has Map?",
                    StringComparison.Ordinal
                ))
            {
                return true;
            }

            __instance.Fsm.Event(__instance.isFalse);
            __instance.Finish();
            return false;
        }
    }

    [HarmonyPatch(typeof(OpenSimpleShopMenu), "OnEnter")]
    internal static class VogHintHistoryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(OpenSimpleShopMenu __instance)
        {
            if (__instance?.Owner != null &&
                string.Equals(
                    __instance.Owner.name,
                    "Caravan Troupe Hunter",
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    __instance.Fsm?.Name,
                    "Dialogue",
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    __instance.State?.Name,
                    "Shop?",
                    StringComparison.Ordinal
                ))
            {
                VogHintManager.ShowHistory();
            }
        }
    }
}
