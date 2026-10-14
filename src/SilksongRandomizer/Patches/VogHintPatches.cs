using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;
using System.Reflection;
using Archipelago.MultiClient.Net.Enums;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

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

    internal static class VogAreaHintManager
    {
        private sealed class AreaShopItem : ISimpleShopItem
        {
            internal VogAreaHint Hint;
            internal int Price;
            public string GetDisplayName() => AlphabetMode.AlphabetModeManager.FilterDirectText("Area");
            public Sprite GetIcon() => RandomizerPlugin.Instance?.ProgressionIcon;
            public int GetCost() => Price;
            public bool DelayPurchase() => true;
        }

        private static readonly Dictionary<CaravanTroupeHunter, AreaShopItem> Stock =
            new Dictionary<CaravanTroupeHunter, AreaShopItem>();
        private static bool Pending;
        private static long Generation;

        private static VogAreaHint NextHint(SaveState state)
        {
            if (Pending || state?.vogAreaHints == null ||
                state.vogAreaHints.Count(hint => hint != null && hint.purchased) >= state.vogAreaHintCount)
            {
                return null;
            }
            return state.vogAreaHints.FirstOrDefault(hint => hint != null && !hint.purchased &&
                hint.locations.Any(name => !state.IsLocationChecked(name)));
        }

        internal static bool HasRemainingHints() => NextHint(SaveState.Instance) != null;
        internal static bool HasHistory() => SaveState.Instance?.GetVogAreaReports().Count > 0;

        internal static void Reset()
        {
            Interlocked.Increment(ref Generation);
            Pending = false;
            Stock.Clear();
        }

        internal static void AddStock(CaravanTroupeHunter owner, List<ISimpleShopItem> stock)
        {
            Stock.Remove(owner);
            SaveState state = SaveState.Instance;
            VogAreaHint hint = NextHint(state);
            if (hint == null)
            {
                return;
            }
            var item = new AreaShopItem
            {
                Hint = hint,
                Price = state.TryGetPurchasePrice("vog:area", out int price) ? price : 120
            };
            Stock[owner] = item;
            stock.Add(item);
        }

        internal static bool TryPurchase(CaravanTroupeHunter owner, int index)
        {
            if (index != 0 || !Stock.TryGetValue(owner, out AreaShopItem item))
            {
                return false;
            }
            SaveState state = SaveState.Instance;
            Archipelago client = Archipelago.Instance;
            if (Pending || state == null || client == null || !client.Connected ||
                item.Hint.purchased || NextHint(state) != item.Hint)
            {
                return true;
            }
            Pending = true;
            _ = ScoutReport(item, state, client, Interlocked.Read(ref Generation));
            return true;
        }

        private static async Task ScoutReport(AreaShopItem item, SaveState state,
            Archipelago client, long generation)
        {
            Dictionary<string, SaveState.HintData> scouts;
            try
            {
                scouts = await client.RequestHintsAsync(item.Hint.locations,
                    HintCreationPolicy.None, "Vog area report").ConfigureAwait(false);
            }
            catch
            {
                scouts = null;
            }
            VogHintManager.EnqueueCompleted(() =>
            {
                if (generation != Interlocked.Read(ref Generation))
                {
                    return;
                }
                Pending = false;
                if (!ReferenceEquals(state, SaveState.Instance) ||
                    !ReferenceEquals(client, Archipelago.Instance) || !client.Connected)
                {
                    return;
                }
                if (!item.Hint.TrySetReport(scouts))
                {
                    Show("Vog could not check that area. No Rosaries were spent.");
                    return;
                }
                if (item.Hint.purchased || PlayerData.instance == null ||
                    PlayerData.instance.geo < item.Price)
                {
                    Show("No Rosaries were spent. Try speaking to Vog again.");
                    return;
                }
                CurrencyManager.TakeCurrency(item.Price, CurrencyType.Money);
                item.Hint.purchased = true;
                Show(item.Hint.GetText(state));
                if (GameManager.instance != null && GameManager.instance.profileID >= 0)
                {
                    GameManager.instance.QueueSaveGame();
                }
            });
        }

        internal static void ShowHistory()
        {
            SaveState state = SaveState.Instance;
            var reports = state?.GetVogAreaReports();
            if (reports == null || reports.Count == 0)
            {
                return;
            }
            int index = Math.Max(0, state.vogHintHistoryReadIndex) % reports.Count;
            Show(reports[index]);
            state.vogHintHistoryReadIndex = (index + 1) % reports.Count;
        }

        private static void Show(string text)
        {
            RandomizerPlugin.Instance?.QueueUnlockPopup(text, ItemFlags.None);
        }
    }
}
