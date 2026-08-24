using Archipelago.MultiClient.Net.Enums;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using SilksongRandomizer.AlphabetMode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class VogHintManager
    {
        private const string WothPrefix = "woth:";
        private const string FoolishPrefix = "foolish:";
        private const string GeneralPrefix = "general:";
        private const string WothPriceKey = "vog:woth";
        private const string FoolishPriceKey = "vog:foolish";
        private const string GeneralPriceKey = "vog:general";
        private const int WothDefaultPrice = 120;
        private const int FoolishDefaultPrice = 80;
        private const int GeneralDefaultPrice = 150;

        private sealed class VogHintItem : ISimpleShopItem
        {
            internal string Key;
            internal string Kind;
            internal string Value;
            internal int Price;

            public string GetDisplayName()
            {
                switch (Kind)
                {
                    case "woth":
                        return AlphabetModeManager.FilterDirectText("Hero");
                    case "foolish":
                        return AlphabetModeManager.FilterDirectText("Foolish");
                    default:
                        return AlphabetModeManager.FilterDirectText("General");
                }
            }

            public Sprite GetIcon()
            {
                RandomizerPlugin plugin = RandomizerPlugin.Instance;
                if (plugin == null)
                {
                    return null;
                }

                switch (Kind)
                {
                    case "woth":
                        return plugin.ProgressionIcon ??
                               plugin.ArchipelagoIcon;
                    case "foolish":
                        return plugin.FillerIcon ??
                               plugin.ArchipelagoIcon;
                    default:
                        return plugin.UsefulIcon ??
                               plugin.ArchipelagoIcon;
                }
            }

            public int GetCost()
            {
                return Price;
            }

            public bool DelayPurchase()
            {
                return string.Equals(
                    Kind,
                    "general",
                    StringComparison.Ordinal
                );
            }
        }

        private static readonly FieldInfo CurrentListGroupsField =
            AccessTools.Field(
                typeof(CaravanTroupeHunter),
                "currentListGroups"
            );
        private static readonly object CompletedLock = new object();
        private static readonly Queue<Action> CompletedActions =
            new Queue<Action>();
        private static readonly Dictionary<CaravanTroupeHunter,
            List<VogHintItem>> CurrentHintItems =
                new Dictionary<CaravanTroupeHunter, List<VogHintItem>>();
        private static readonly HashSet<string> PendingGeneralHints =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static long RequestGeneration;
        private static string LastAnnouncementKey = string.Empty;
        private static DateTime NextAnnouncementAttemptUtc =
            DateTime.MinValue;

        private static bool IsHintShopActive()
        {
            SaveState state = SaveState.Instance;
            Archipelago client = Archipelago.Instance;
            return state != null && client != null && client.Connected &&
                   state.vogHintSettingsBound;
        }

        internal static bool HasRemainingHints()
        {
            if (!IsHintShopActive())
            {
                return false;
            }

            SaveState state = SaveState.Instance;
            return TryGetNextHint(state, "woth", out _) ||
                   TryGetNextHint(state, "foolish", out _) ||
                   TryGetNextHint(state, "general", out _);
        }

        internal static bool HasHintHistory()
        {
            SaveState state = SaveState.Instance;
            return IsHintShopActive() &&
                   state.vogHintHistory != null &&
                   state.vogHintHistory.Count > 0;
        }

        internal static bool ShouldOpenHintShop()
        {
            return HasRemainingHints() ||
                   (HasHintHistory() && !ShouldHideNativeStock());
        }

        internal static bool ShouldHideNativeStock()
        {
            return IsHintShopActive() &&
                   SaveState.Instance.checkMapMarkers !=
                       CheckMapMarkerMode.Off;
        }

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

            ReconcilePendingGeneralHint();
        }

        internal static void Reset()
        {
            Interlocked.Increment(ref RequestGeneration);
            CurrentHintItems.Clear();
            lock (CompletedLock)
            {
                PendingGeneralHints.Clear();
                CompletedActions.Clear();
            }
            LastAnnouncementKey = string.Empty;
            NextAnnouncementAttemptUtc = DateTime.MinValue;
        }

        internal static void AddHintStock(
            CaravanTroupeHunter owner,
            List<ISimpleShopItem> stock,
            bool hideNativeStock
        )
        {
            if (owner == null || stock == null)
            {
                return;
            }

            SaveState state = SaveState.Instance;
            Archipelago client = Archipelago.Instance;
            if (state == null || client == null || !client.Connected ||
                !state.vogHintSettingsBound)
            {
                CurrentHintItems.Remove(owner);
                return;
            }

            if (hideNativeStock)
            {
                stock.Clear();
                ClearNativeGroupMap(owner);
            }

            List<VogHintItem> items = new List<VogHintItem>();
            AddNextItem(
                state,
                items,
                "woth",
                WothPriceKey,
                WothDefaultPrice
            );
            AddNextItem(
                state,
                items,
                "foolish",
                FoolishPriceKey,
                FoolishDefaultPrice
            );
            AddNextItem(
                state,
                items,
                "general",
                GeneralPriceKey,
                GeneralDefaultPrice
            );
            stock.AddRange(items);
            CurrentHintItems[owner] = items;
        }

        private static void ClearNativeGroupMap(
            CaravanTroupeHunter owner
        )
        {
            if (CurrentListGroupsField?.GetValue(owner) is
                System.Collections.IList groups)
            {
                groups.Clear();
            }
        }

        private static void AddNextItem(
            SaveState state,
            List<VogHintItem> items,
            string kind,
            string priceKey,
            int defaultPrice
        )
        {
            if (TryGetNextHint(state, kind, out string value))
            {
                int price = state.TryGetPurchasePrice(
                    priceKey,
                    out int resolvedPrice
                )
                    ? resolvedPrice
                    : defaultPrice;
                items.Add(new VogHintItem
                {
                    Key = kind + ":" + value,
                    Kind = kind,
                    Value = value,
                    Price = price
                });
            }
        }

        private static bool TryGetNextHint(
            SaveState state,
            string kind,
            out string value
        )
        {
            value = null;
            if (state == null || !state.vogHintSettingsBound)
            {
                return false;
            }

            IReadOnlyList<string> plan;
            int cap;
            string prefix;
            switch (kind)
            {
                case "woth":
                    plan = state.vogWothAreas;
                    cap = state.vogWothHintCount;
                    prefix = WothPrefix;
                    break;
                case "foolish":
                    plan = state.vogFoolishAreas;
                    cap = state.vogFoolishHintCount;
                    prefix = FoolishPrefix;
                    break;
                default:
                    plan = state.vogGeneralLocations;
                    cap = state.vogGeneralHintCount;
                    prefix = GeneralPrefix;
                    break;
            }

            if (string.Equals(
                    kind,
                    "general",
                    StringComparison.Ordinal
                ) &&
                (HasPendingGeneralHint() ||
                 HasPendingGeneralEscrow(state)))
            {
                return false;
            }

            HashSet<string> purchases = state.purchasedVogHints ??
                new HashSet<string>(StringComparer.Ordinal);
            if (purchases.Count(key =>
                    key.StartsWith(prefix, StringComparison.Ordinal)) >= cap)
            {
                return false;
            }

            value = (plan ?? Array.Empty<string>()).FirstOrDefault(entry =>
                !string.IsNullOrWhiteSpace(entry) &&
                !purchases.Contains(prefix + entry) &&
                (kind != "general" ||
                 (!state.IsLocationChecked(entry) &&
                  !HasCachedHint(state, entry))));
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool HasCachedHint(
            SaveState state,
            string locationName
        )
        {
            return TryGetCachedHint(state, locationName, out _);
        }

        private static bool TryGetCachedHint(
            SaveState state,
            string locationName,
            out SaveState.HintData result
        )
        {
            string canonical =
                LocationSet.GetCanonicalLocationName(locationName);
            result = state?.receivedHints?.FirstOrDefault(hint =>
                hint != null && string.Equals(
                    hint.locationName,
                    canonical,
                    StringComparison.OrdinalIgnoreCase
                ));
            return result != null;
        }

        private static bool HasPendingGeneralEscrow(SaveState state)
        {
            return state != null &&
                   !string.IsNullOrWhiteSpace(
                       state.pendingVogGeneralKey
                   ) &&
                   !string.IsNullOrWhiteSpace(
                       state.pendingVogGeneralLocation
                   );
        }

        private static bool HasPendingGeneralHint()
        {
            lock (CompletedLock)
            {
                return PendingGeneralHints.Count > 0;
            }
        }

        private static bool TryBeginGeneralHint(string locationName)
        {
            lock (CompletedLock)
            {
                if (PendingGeneralHints.Count > 0)
                {
                    return false;
                }

                return PendingGeneralHints.Add(locationName);
            }
        }

        private static void EndGeneralHint(string locationName)
        {
            lock (CompletedLock)
            {
                PendingGeneralHints.Remove(locationName);
            }
        }

        internal static bool TryPurchase(
            CaravanTroupeHunter owner,
            int itemIndex,
            int nativeItemCount
        )
        {
            if (owner == null ||
                !CurrentHintItems.TryGetValue(
                    owner,
                    out List<VogHintItem> items
                ))
            {
                return false;
            }

            int hintIndex = itemIndex - nativeItemCount;
            if (hintIndex < 0 || hintIndex >= items.Count)
            {
                return false;
            }

            VogHintItem item = items[hintIndex];
            if (string.Equals(
                    item.Kind,
                    "general",
                    StringComparison.Ordinal
                ))
            {
                BeginGeneralPurchase(item);
            }
            else
            {
                CompleteLocalPurchase(item);
            }
            return true;
        }

        private static void CompleteLocalPurchase(VogHintItem item)
        {
            SaveState state = SaveState.Instance;
            if (!IsPurchasable(state, item))
            {
                Refund(item.Price);
                return;
            }

            state.purchasedVogHints.Add(item.Key);
            string text = string.Equals(
                    item.Kind,
                    "woth",
                    StringComparison.Ordinal
                )
                ? "Way of the Hero: " + item.Value +
                  " contains a required check."
                : "Foolish: " + item.Value +
                  " contains no progression or useful items.";
            RememberAndShow(state, text, ItemFlags.None);
        }

        private static void BeginGeneralPurchase(VogHintItem item)
        {
            SaveState state = SaveState.Instance;
            Archipelago client = Archipelago.Instance;
            client?.ImportTrackedHints();
            if (!IsPurchasable(state, item) || client == null ||
                !client.Connected || HasPendingGeneralEscrow(state) ||
                HasCachedHint(state, item.Value))
            {
                if (state != null && HasCachedHint(state, item.Value))
                {
                    RandomizerPlugin.Instance?.QueueUnlockPopup(
                        "Vog already knows that hint. No Rosaries were spent.",
                        ItemFlags.None
                    );
                }
                return;
            }

            if (!TryBeginGeneralHint(item.Value))
            {
                return;
            }

            long requestGeneration = Interlocked.Read(
                ref RequestGeneration
            );
            Task<Dictionary<string, SaveState.HintData>> request =
                client.RequestHintsAsync(
                    new[] { item.Value },
                    HintCreationPolicy.None,
                    "Vog general hint preview"
                );
            _ = AwaitGeneralScout(
                item,
                state,
                client,
                requestGeneration,
                request
            );
        }

        private static async Task AwaitGeneralScout(
            VogHintItem item,
            SaveState state,
            Archipelago client,
            long requestGeneration,
            Task<Dictionary<string, SaveState.HintData>> request
        )
        {
            Dictionary<string, SaveState.HintData> hints;
            try
            {
                hints = await request.ConfigureAwait(false);
            }
            catch
            {
                hints = null;
            }

            lock (CompletedLock)
            {
                CompletedActions.Enqueue(() =>
                {
                    if (requestGeneration != Interlocked.Read(
                            ref RequestGeneration
                        ))
                    {
                        return;
                    }

                    string canonical =
                        LocationSet.GetCanonicalLocationName(item.Value);
                    if (!ReferenceEquals(state, SaveState.Instance) ||
                        !ReferenceEquals(client, Archipelago.Instance) ||
                        !client.Connected || hints == null ||
                        !hints.TryGetValue(
                            canonical,
                            out SaveState.HintData hint
                        ) ||
                        hint == null)
                    {
                        EndGeneralHint(item.Value);
                        RandomizerPlugin.Instance?.QueueUnlockPopup(
                            "Vog could not find that hint. No Rosaries were spent.",
                            ItemFlags.None
                        );
                        return;
                    }

                    client.ImportTrackedHints();
                    if (HasCachedHint(state, item.Value))
                    {
                        EndGeneralHint(item.Value);
                        RandomizerPlugin.Instance?.QueueUnlockPopup(
                            "Vog already knows that hint. No Rosaries were spent.",
                            ItemFlags.None
                        );
                        return;
                    }

                    if (state.IsLocationChecked(item.Value))
                    {
                        EndGeneralHint(item.Value);
                        RandomizerPlugin.Instance?.QueueUnlockPopup(
                            "That location was completed. No Rosaries were spent.",
                            ItemFlags.None
                        );
                        return;
                    }

                    if (PlayerData.instance == null ||
                        PlayerData.instance.geo < item.Price)
                    {
                        EndGeneralHint(item.Value);
                        RandomizerPlugin.Instance?.QueueUnlockPopup(
                            "Vog found the hint, but you no longer have " +
                            "enough Rosaries.",
                            ItemFlags.None
                        );
                        return;
                    }

                    state.pendingVogGeneralKey = item.Key;
                    state.pendingVogGeneralLocation = item.Value;
                    state.pendingVogGeneralPrice = item.Price;
                    CurrencyManager.TakeCurrency(
                        item.Price,
                        CurrencyType.Money
                    );
                    QueueEscrowSave();
                    EndGeneralHint(item.Value);
                    TryRequestPendingAnnouncement(state, client);
                });
            }
        }

        private static void ReconcilePendingGeneralHint()
        {
            SaveState state = SaveState.Instance;
            Archipelago client = Archipelago.Instance;
            if (!HasPendingGeneralEscrow(state) || client == null ||
                !client.Connected)
            {
                return;
            }

            if (state.purchasedVogHints.Contains(
                    state.pendingVogGeneralKey
                ))
            {
                ClearPendingGeneralEscrow(state);
                return;
            }

            if (!TryGetCachedHint(
                    state,
                    state.pendingVogGeneralLocation,
                    out SaveState.HintData hint
                ))
            {
                TryRequestPendingAnnouncement(state, client);
                return;
            }

            string key = state.pendingVogGeneralKey;
            string location = state.pendingVogGeneralLocation;
            ClearPendingGeneralEscrow(state);
            if (!state.purchasedVogHints.Add(key))
            {
                return;
            }

            string recipient = string.IsNullOrWhiteSpace(hint.user)
                ? "Unknown player"
                : hint.user;
            RandomizerPlugin.Instance?.QueueUnlockPopup(
                location + " contains " + hint.item +
                " for " + recipient + ".",
                hint.flags
            );
            QueueEscrowSave();
        }

        private static void TryRequestPendingAnnouncement(
            SaveState state,
            Archipelago client
        )
        {
            if (!HasPendingGeneralEscrow(state) || client == null ||
                !client.Connected)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (string.Equals(
                    LastAnnouncementKey,
                    state.pendingVogGeneralKey,
                    StringComparison.Ordinal
                ) && now < NextAnnouncementAttemptUtc)
            {
                return;
            }

            LastAnnouncementKey = state.pendingVogGeneralKey;
            bool sent = client.CreateOfficialHint(
                state.pendingVogGeneralLocation
            );
            NextAnnouncementAttemptUtc = now.AddSeconds(sent ? 10 : 3);
        }

        private static void QueueEscrowSave()
        {
            GameManager gameManager = GameManager.instance;
            if (gameManager != null && gameManager.profileID >= 0)
            {
                gameManager.QueueSaveGame();
            }
        }

        private static void ClearPendingGeneralEscrow(SaveState state)
        {
            state.pendingVogGeneralKey = string.Empty;
            state.pendingVogGeneralLocation = string.Empty;
            state.pendingVogGeneralPrice = 0;
            LastAnnouncementKey = string.Empty;
            NextAnnouncementAttemptUtc = DateTime.MinValue;
        }

        private static bool IsPurchasable(
            SaveState state,
            VogHintItem item
        )
        {
            return state != null && item != null &&
                   !state.purchasedVogHints.Contains(item.Key);
        }

        private static void Refund(int price)
        {
            if (price > 0)
            {
                CurrencyManager.AddCurrency(
                    price,
                    CurrencyType.Money
                );
            }
        }

        private static void RememberAndShow(
            SaveState state,
            string text,
            ItemFlags flags
        )
        {
            state.vogHintHistory.Add(text);
            RandomizerPlugin.Instance?.QueueUnlockPopup(text, flags);
        }

        internal static void ShowHistory()
        {
            SaveState state = SaveState.Instance;
            if (state?.vogHintHistory == null ||
                state.vogHintHistory.Count == 0)
            {
                return;
            }

            int index = Math.Max(0, state.vogHintHistoryReadIndex) %
                state.vogHintHistory.Count;
            RandomizerPlugin.Instance?.QueueUnlockPopup(
                state.vogHintHistory[index],
                ItemFlags.None
            );
            state.vogHintHistoryReadIndex =
                (index + 1) % state.vogHintHistory.Count;
        }

        internal static int GetNativeItemCount(
            CaravanTroupeHunter owner
        )
        {
            if (owner == null ||
                !(CurrentListGroupsField?.GetValue(owner) is
                    System.Collections.ICollection groups))
            {
                return 0;
            }
            return groups.Count;
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
