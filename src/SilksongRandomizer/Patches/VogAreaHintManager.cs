using Archipelago.MultiClient.Net.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
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
