using Archipelago.MultiClient.Net.Enums;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal class ShopPatches
    {
        private static readonly HashSet<string>
            InteractionRevealedLocations = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );
        private static readonly Dictionary<string, SaveState.HintData>
            SilentPreviewHints =
                new Dictionary<string, SaveState.HintData>(
                    StringComparer.OrdinalIgnoreCase
                );
        private static SaveState shopSessionState;
        private static Archipelago shopSessionClient;
        private static string shopSessionSeed;
        private static int shopSessionTeam = -1;
        private static int shopSessionSlot = -1;
        private const int StaleShopRequestSeconds = 15;

        [ThreadStatic]
        private static int presentationRefreshDepth;

        private sealed class PendingShopHintRefresh
        {
            internal ShopMenuStock ShopMenu;
            internal SaveState State;
            internal Archipelago Client;
            internal string RoomSeed;
            internal int Team;
            internal int Slot;
            internal HashSet<string> LocationNames;
            internal bool IsSilentPreview;
            internal DateTime StartedUtc;
            internal Task<Dictionary<string, SaveState.HintData>> Request;
        }

        private sealed class CompletedShopHintRefresh
        {
            internal PendingShopHintRefresh Registration;
            internal Dictionary<string, SaveState.HintData> Hints;
        }

        private static readonly Dictionary<
            ShopMenuStock,
            PendingShopHintRefresh> PendingShopHintRefreshes =
                new Dictionary<ShopMenuStock, PendingShopHintRefresh>();
        private static readonly Dictionary<
            ShopMenuStock,
            PendingShopHintRefresh> PendingShopPreviewPrefetches =
                new Dictionary<ShopMenuStock, PendingShopHintRefresh>();
        private static readonly object CompletedShopHintRefreshesLock =
            new object();
        private static readonly Queue<CompletedShopHintRefresh>
            CompletedShopHintRefreshes =
                new Queue<CompletedShopHintRefresh>();
        private static readonly HashSet<ShopMenuStock>
            DeferredShopDetailRefreshes = new HashSet<ShopMenuStock>();

        private static void SynchronizeShopSession()
        {
            SaveState state = SaveState.Instance;
            Archipelago client = Archipelago.Instance;
            bool sameSession =
                ReferenceEquals(shopSessionState, state) &&
                ReferenceEquals(shopSessionClient, client) &&
                client != null &&
                client.Connected &&
                string.Equals(
                    shopSessionSeed,
                    client.RoomSeed,
                    StringComparison.Ordinal
                ) &&
                shopSessionTeam == client.Team &&
                shopSessionSlot == client.Slot;
            if (!sameSession)
            {
                shopSessionState = state;
                shopSessionClient = client;
                shopSessionSeed =
                    client != null && client.Connected
                        ? client.RoomSeed
                        : null;
                shopSessionTeam =
                    client != null && client.Connected ? client.Team : -1;
                shopSessionSlot =
                    client != null && client.Connected ? client.Slot : -1;
                InteractionRevealedLocations.Clear();
                SilentPreviewHints.Clear();
                PendingShopHintRefreshes.Clear();
                PendingShopPreviewPrefetches.Clear();
                DeferredShopDetailRefreshes.Clear();
            }
        }

        internal static bool CanRequestHint(ShopItem shopItem)
        {
            SynchronizeShopSession();
            return shopSessionState != null &&
                   shopSessionClient != null &&
                   shopSessionClient.Connected &&
                   shopItem != null &&
                   presentationRefreshDepth == 0 &&
                   TryResolveShopPreviewLocation(
                       shopItem,
                       out string locationName
                   ) &&
                   InteractionRevealedLocations.Contains(
                       LocationSet.GetCanonicalLocationName(locationName)
                   );
        }

        private static void RevealAvailableStock(ShopMenuStock shopMenu)
        {
            SynchronizeShopSession();
            if (shopSessionState == null ||
                shopSessionClient == null ||
                !shopSessionClient.Connected ||
                shopMenu == null)
            {
                return;
            }

            ShopMenuStock stockOwner = shopMenu.MasterList ?? shopMenu;
            HashSet<string> locationNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );
            foreach (ShopItem shopItem in stockOwner.EnumerateStock())
            {
                if (shopItem != null && shopItem.IsAvailable)
                {
                    if (TryResolveShopPreviewLocation(
                            shopItem,
                            out string locationName))
                    {
                        string canonicalLocationName =
                            LocationSet.GetCanonicalLocationName(
                                locationName
                            );
                        InteractionRevealedLocations.Add(
                            canonicalLocationName
                        );
                        locationNames.Add(canonicalLocationName);
                    }
                }
            }

            RequestShopHintRefresh(shopMenu, locationNames);
        }

        private static void PrefetchAvailableStock(ShopMenuStock shopMenu)
        {
            SynchronizeShopSession();
            if (presentationRefreshDepth != 0 ||
                shopSessionState == null ||
                shopSessionClient == null ||
                !shopSessionClient.Connected ||
                shopMenu == null)
            {
                return;
            }

            ShopMenuStock stockOwner = shopMenu.MasterList ?? shopMenu;
            HashSet<string> locationNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );
            foreach (ShopItem shopItem in stockOwner.EnumerateStock())
            {
                if (shopItem != null &&
                    shopItem.IsAvailable &&
                    TryResolveShopPreviewLocation(
                        shopItem,
                        out string locationName
                    ))
                {
                    locationNames.Add(
                        LocationSet.GetCanonicalLocationName(locationName)
                    );
                }
            }

            RequestShopPreviewPrefetch(shopMenu, locationNames);
        }

        private static void RequestShopHintRefresh(
            ShopMenuStock shopMenu,
            IEnumerable<string> locationNames)
        {
            BeginShopHintRequest(
                shopMenu,
                locationNames,
                isSilentPreview: false
            );
        }

        private static void RequestShopPreviewPrefetch(
            ShopMenuStock shopMenu,
            IEnumerable<string> locationNames)
        {
            BeginShopHintRequest(
                shopMenu,
                locationNames,
                isSilentPreview: true
            );
        }

        private static void BeginShopHintRequest(
            ShopMenuStock shopMenu,
            IEnumerable<string> locationNames,
            bool isSilentPreview)
        {
            SaveState state = shopSessionState;
            Archipelago client = shopSessionClient;
            Dictionary<ShopMenuStock, PendingShopHintRefresh>
                pendingRequests = isSilentPreview
                    ? PendingShopPreviewPrefetches
                    : PendingShopHintRefreshes;
            if (shopMenu == null ||
                state == null ||
                client == null ||
                !client.Connected)
            {
                if (!ReferenceEquals(shopMenu, null))
                {
                    pendingRequests.Remove(shopMenu);
                }
                return;
            }

            HashSet<string> requestedLocations =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string locationName in locationNames)
            {
                string canonicalLocationName =
                    LocationSet.GetCanonicalLocationName(locationName);
                if (!string.IsNullOrWhiteSpace(canonicalLocationName) &&
                    !state.GetHint(
                        canonicalLocationName,
                        out _,
                        out _,
                        out _,
                        allowNetworkRequest: false
                    ) &&
                    (!isSilentPreview ||
                     !SilentPreviewHints.ContainsKey(
                         canonicalLocationName
                     )))
                {
                    requestedLocations.Add(canonicalLocationName);
                }
            }

            if (requestedLocations.Count == 0)
            {
                pendingRequests.Remove(shopMenu);
                return;
            }

            if (pendingRequests.TryGetValue(
                    shopMenu,
                    out PendingShopHintRefresh existingRefresh) &&
                ReferenceEquals(existingRefresh.State, state) &&
                ReferenceEquals(existingRefresh.Client, client) &&
                string.Equals(
                    existingRefresh.RoomSeed,
                    client.RoomSeed,
                    StringComparison.Ordinal
                ) &&
                existingRefresh.Team == client.Team &&
                existingRefresh.Slot == client.Slot &&
                existingRefresh.LocationNames.SetEquals(
                    requestedLocations
                ) &&
                DateTime.UtcNow - existingRefresh.StartedUtc <
                    TimeSpan.FromSeconds(StaleShopRequestSeconds))
            {
                return;
            }

            Task<Dictionary<string, SaveState.HintData>> request =
                client.RequestHintsAsync(
                    requestedLocations,
                    isSilentPreview
                        ? HintCreationPolicy.None
                        : HintCreationPolicy.CreateAndAnnounceOnce,
                    isSilentPreview
                        ? "shop preview"
                        : "opened shop hints"
                );

            PendingShopHintRefresh registration =
                new PendingShopHintRefresh
                {
                    ShopMenu = shopMenu,
                    State = state,
                    Client = client,
                    RoomSeed = client.RoomSeed,
                    Team = client.Team,
                    Slot = client.Slot,
                    LocationNames = requestedLocations,
                    IsSilentPreview = isSilentPreview,
                    StartedUtc = DateTime.UtcNow,
                    Request = request,
                };
            pendingRequests[shopMenu] = registration;
            _ = AwaitShopHintsAsync(registration);
        }

        private static async Task AwaitShopHintsAsync(
            PendingShopHintRefresh registration)
        {
            Dictionary<string, SaveState.HintData> hints;
            try
            {
                hints = await registration.Request.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Shop hint request failed: " + ex.Message
                );
                hints = new Dictionary<string, SaveState.HintData>(
                    StringComparer.OrdinalIgnoreCase
                );
            }

            lock (CompletedShopHintRefreshesLock)
            {
                CompletedShopHintRefreshes.Enqueue(
                    new CompletedShopHintRefresh
                    {
                        Registration = registration,
                        Hints = hints,
                    }
                );
            }
        }

        internal static void ProcessQueuedHintRefreshes()
        {
            while (true)
            {
                CompletedShopHintRefresh completion;
                lock (CompletedShopHintRefreshesLock)
                {
                    if (CompletedShopHintRefreshes.Count == 0)
                    {
                        break;
                    }

                    completion = CompletedShopHintRefreshes.Dequeue();
                }

                ProcessCompletedHintRefresh(completion);
            }

            ProcessDeferredShopDetailRefreshes();
        }

        private static void ProcessCompletedHintRefresh(
            CompletedShopHintRefresh completion)
        {
            PendingShopHintRefresh registration =
                completion.Registration;
            bool sameState = ReferenceEquals(
                SaveState.Instance,
                registration.State
            );
            bool sameSession =
                ReferenceEquals(Archipelago.Instance, registration.Client) &&
                registration.Client.Connected &&
                string.Equals(
                    registration.Client.RoomSeed,
                    registration.RoomSeed,
                    StringComparison.Ordinal
                ) &&
                registration.Client.Team == registration.Team &&
                registration.Client.Slot == registration.Slot;
            bool hasSuccessfulHint = false;
            if (sameState && sameSession && completion.Hints != null)
            {
                foreach (
                    KeyValuePair<string, SaveState.HintData> entry
                    in completion.Hints)
                {
                    if (entry.Value == null)
                    {
                        continue;
                    }

                    string canonicalLocationName =
                        LocationSet.GetCanonicalLocationName(entry.Key);
                    if (registration.IsSilentPreview)
                    {
                        SilentPreviewHints[canonicalLocationName] =
                            entry.Value;
                    }
                    else
                    {
                        registration.State.CacheHint(entry.Value);
                    }
                    hasSuccessfulHint = true;
                }
            }

            Dictionary<ShopMenuStock, PendingShopHintRefresh>
                pendingRequests = registration.IsSilentPreview
                    ? PendingShopPreviewPrefetches
                    : PendingShopHintRefreshes;
            bool isCurrentRegistration =
                !ReferenceEquals(registration.ShopMenu, null) &&
                pendingRequests.TryGetValue(
                    registration.ShopMenu,
                    out PendingShopHintRefresh currentRefresh
                ) &&
                ReferenceEquals(currentRefresh, registration);
            if (isCurrentRegistration)
            {
                pendingRequests.Remove(registration.ShopMenu);
            }

            if (isCurrentRegistration &&
                sameState &&
                sameSession &&
                hasSuccessfulHint)
            {
                RefreshShopPresentation(registration.ShopMenu);
            }
        }

        private static void RefreshShopPresentation(
            ShopMenuStock shopMenu)
        {
            if (shopMenu == null ||
                shopMenu.gameObject == null ||
                !shopMenu.gameObject.activeInHierarchy)
            {
                return;
            }

            presentationRefreshDepth++;
            try
            {
                ShopMenuStock stockOwner =
                    shopMenu.MasterList ?? shopMenu;
                ShopMenuStock presentationMenu =
                    FindPresentationShopMenu(shopMenu, stockOwner);
                if (presentationMenu == null ||
                    presentationMenu.gameObject == null ||
                    !presentationMenu.gameObject.activeInHierarchy)
                {
                    return;
                }

                if (!ReferenceEquals(stockOwner, presentationMenu))
                {
                    stockOwner.SpawnStock();
                }
                presentationMenu.BuildItemList();

                PlayMakerFSM itemListControl =
                    FindItemListControl(presentationMenu);
                if (itemListControl?.Fsm != null &&
                    string.Equals(
                        itemListControl.Fsm.ActiveStateName,
                        "Idle",
                        StringComparison.Ordinal
                    ))
                {
                    itemListControl.Fsm.SetState("Get Details");
                }
                else if (itemListControl?.Fsm != null)
                {
                    DeferredShopDetailRefreshes.Add(presentationMenu);
                }
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Failed to refresh completed shop hints: " +
                    ex
                );
            }
            finally
            {
                presentationRefreshDepth--;
            }
        }

        private static ShopMenuStock FindPresentationShopMenu(
            ShopMenuStock requestedMenu,
            ShopMenuStock stockOwner)
        {
            if (requestedMenu == null || stockOwner == null)
            {
                return null;
            }

            if (FindItemListControl(requestedMenu) != null)
            {
                return requestedMenu;
            }

            // SetStock and the master inventory live on the Shop Menu root.
            // The visible item list lives on a child, so find that child when
            // late scout results need to redraw the panel.
            ShopMenuStock presentationMenu = null;
            foreach (ShopMenuStock candidate in
                     stockOwner.GetComponentsInChildren<ShopMenuStock>(true))
            {
                if (candidate == null ||
                    !ReferenceEquals(candidate.MasterList, stockOwner) ||
                    FindItemListControl(candidate) == null)
                {
                    continue;
                }

                if (presentationMenu != null &&
                    !ReferenceEquals(presentationMenu, candidate))
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] More than one item-list presentation " +
                        "was bound to the same shop stock owner."
                    );
                    return null;
                }

                presentationMenu = candidate;
            }

            return presentationMenu ?? requestedMenu;
        }

        private static PlayMakerFSM FindItemListControl(
            ShopMenuStock shopMenu)
        {
            return shopMenu == null
                ? null
                : shopMenu.GetComponents<PlayMakerFSM>()
                    .FirstOrDefault(fsm => string.Equals(
                        fsm.FsmName,
                        "Item List Control",
                        StringComparison.Ordinal
                    ));
        }

        private static void ProcessDeferredShopDetailRefreshes()
        {
            if (DeferredShopDetailRefreshes.Count == 0)
            {
                return;
            }

            foreach (
                ShopMenuStock shopMenu
                in DeferredShopDetailRefreshes.ToArray())
            {
                if (shopMenu == null ||
                    shopMenu.gameObject == null ||
                    !shopMenu.gameObject.activeInHierarchy)
                {
                    DeferredShopDetailRefreshes.Remove(shopMenu);
                    continue;
                }

                PlayMakerFSM itemListControl =
                    FindItemListControl(shopMenu);
                if (itemListControl?.Fsm == null)
                {
                    DeferredShopDetailRefreshes.Remove(shopMenu);
                    continue;
                }

                if (!string.Equals(
                        itemListControl.Fsm.ActiveStateName,
                        "Idle",
                        StringComparison.Ordinal
                    ))
                {
                    continue;
                }

                presentationRefreshDepth++;
                try
                {
                    itemListControl.Fsm.SetState("Get Details");
                    DeferredShopDetailRefreshes.Remove(shopMenu);
                }
                catch (Exception ex)
                {
                    DeferredShopDetailRefreshes.Remove(shopMenu);
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Failed to redraw completed shop " +
                        "details: " + ex.Message
                    );
                }
                finally
                {
                    presentationRefreshDepth--;
                }
            }
        }

        internal static bool TryGetPresentationHint(
            string locationName,
            out string user,
            out string item,
            out ItemFlags flags)
        {
            user = null;
            item = null;
            flags = ItemFlags.None;
            SynchronizeShopSession();
            string canonicalLocationName =
                LocationSet.GetCanonicalLocationName(locationName);
            if (shopSessionState == null ||
                string.IsNullOrWhiteSpace(canonicalLocationName))
            {
                return false;
            }

            if (shopSessionState.GetHint(
                    canonicalLocationName,
                    out user,
                    out item,
                    out flags,
                    allowNetworkRequest: false
                ))
            {
                return true;
            }

            if (!SilentPreviewHints.TryGetValue(
                    canonicalLocationName,
                    out SaveState.HintData hint
                ) ||
                hint == null)
            {
                return false;
            }

            user = hint.user;
            item = hint.item;
            flags = hint.flags;
            return true;
        }

        [HarmonyPatch(
            typeof(ShopMenuStock),
            nameof(ShopMenuStock.SetStock),
            new Type[] { typeof(ShopItem[]) })]
        internal static class ShopMenuStock_SetStock_PreviewPrefetch_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(ShopMenuStock __instance)
            {
                // ShopOwnerBase assigns scene stock before the player opens
                // the menu. Scout only this spawned shop and keep the result
                // private so the real interaction can still announce hints.
                PrefetchAvailableStock(__instance);
            }
        }

        [HarmonyPatch(
            typeof(ShopMenuStock),
            nameof(ShopMenuStock.DisplayCurrencyCounters))]
        internal static class ShopMenuStock_DisplayCurrencyCounters_HintReveal_Patch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(ShopMenuStock __instance)
            {
                // The native Shop Menu FSM calls this only after receiving the
                // player-triggered SHOP UP event, before it opens the window and
                // builds the visible item list. SpawnStock and BuildItemList also
                // run during setup, so neither is a safe proof of interaction.
                RevealAvailableStock(__instance);
            }
        }

        private static bool IsRandomizedTarget(ShopItem shopItem)
        {
            SaveState state = SaveState.Instance;
            if (state == null || shopItem == null)
            {
                return false;
            }

            return
                (shopItem.IsToolItem() &&
                 state.IsRandomized(GetToolItemType(shopItem))) ||
                (shopItem.name == "Mapper Quill" &&
                 IsQuillCheckEnabled(state));
        }

        private static bool IsQuillCheckEnabled(SaveState state)
        {
            return state != null &&
                   (state.startFullyMapped ||
                    state.IsRandomized(ItemType.Skill));
        }

        private static bool TryResolveShopPreviewLocation(
            ShopItem shopItem,
            out string locationName)
        {
            bool resolved = TryResolveToolAndQuillPreviewLocation(
                shopItem,
                out locationName
            );
            if (!resolved)
            {
                resolved = CoreLocationPatches.TryResolveShopHintLocation(
                    shopItem,
                    out locationName
                );
            }

            SaveState state = SaveState.Instance;
            if (!resolved ||
                state == null ||
                string.IsNullOrWhiteSpace(locationName))
            {
                locationName = null;
                return false;
            }

            locationName = LocationSet.GetCanonicalLocationName(
                locationName
            );
            if (!state.IsLocationEnabled(locationName) ||
                !state.IsLocationInSeed(locationName))
            {
                locationName = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveToolAndQuillPreviewLocation(
            ShopItem shopItem,
            out string locationName)
        {
            locationName = null;
            if (!IsRandomizedTarget(shopItem))
            {
                return false;
            }

            if (shopItem.IsToolItem())
            {
                SavedItem savedItem = Traverse.Create(shopItem)
                    .Field("savedItem")
                    .GetValue() as SavedItem;
                if (savedItem == null)
                {
                    return false;
                }

                locationName = LocationSet.GetCanonicalLocationName(
                    "Tool Unlock: " + savedItem.name
                );
                return true;
            }

            if (string.Equals(
                    shopItem.name,
                    "Mapper Quill",
                    StringComparison.Ordinal))
            {
                locationName = "Item: Quill";
                return true;
            }

            return false;
        }

        private static ItemType GetToolItemType(ShopItem shopItem)
        {
            SavedItem savedItem = Traverse.Create(shopItem)
                .Field("savedItem")
                .GetValue() as SavedItem;
            string name = savedItem == null ? string.Empty : savedItem.name;
            switch (name)
            {
                case "Silk Spear":
                case "Parry":
                case "Silk Boss Needle":
                case "Silk Charge":
                case "Silk Bomb":
                case "Thread Sphere":
                    return ItemType.Spell;
                default:
                    return ItemType.Tool;
            }
        }

        private static bool IsRuinedToolRepair(ShopItem shopItem)
        {
            if (shopItem == null || !shopItem.IsToolItem())
            {
                return false;
            }

            SavedItem savedItem = Traverse.Create(shopItem)
                .Field("savedItem")
                .GetValue() as SavedItem;
            return savedItem != null &&
                   (
                       string.Equals(
                           savedItem.name,
                           "WebShot Forge",
                           StringComparison.Ordinal
                       ) ||
                       string.Equals(
                           savedItem.name,
                           "WebShot Architect",
                           StringComparison.Ordinal
                       )
                   );
        }

        [HarmonyPatch(
            typeof(ShopItem),
            nameof(ShopItem.SetPurchased),
            new Type[] { typeof(Action), typeof(int) })]
        internal static class ShopItem_SetPurchased_Quill_Patch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                ShopItem __instance,
                ref Action onComplete)
            {
                const string locationName = "Item: Quill";
                SaveState state = SaveState.Instance;
                if (state == null ||
                    __instance == null ||
                    !string.Equals(
                        __instance.name,
                        "Mapper Quill",
                        StringComparison.Ordinal
                    ) ||
                    !IsQuillCheckEnabled(state) ||
                    !state.IsLocationEnabled(locationName) ||
                    !state.IsLocationInSeed(locationName) ||
                    state.IsLocationChecked(locationName))
                {
                    return;
                }

                // The native callback runs only after the purchase succeeds,
                // payment and stock conditionals have completed. Commit the AP
                // check there so the same shop rebuild immediately sees Quill
                // as purchased instead of waiting for the frame-by-frame poll.
                Action originalOnComplete = onComplete;
                onComplete = () =>
                {
                    if (!state.IsLocationChecked(locationName))
                    {
                        state.CheckLocation(locationName);
                    }
                    originalOnComplete?.Invoke();
                };
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_IsAvailable")]
        internal static class ShopItem_IsAvailable_Patch
        {
            private static bool Prefix(ShopItem __instance, ref bool __result)
            {
                if (!IsRandomizedTarget(__instance))
                {
                    return true;
                }

                if (__instance.IsToolItem())
                {
                    __result = !__instance.IsPurchased;
                    return false;
                }
                if (__instance.name == "Mapper Quill")
                {
                    __result = !__instance.IsPurchased;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_IsPurchased")]
        internal static class ShopItem_IsPurchased_Patch
        {
            private static bool Prefix(ShopItem __instance, ref bool __result)
            {
                if (!IsRandomizedTarget(__instance))
                {
                    return true;
                }

                if (__instance.IsToolItem())
                {
                    SavedItem savedItem = Traverse.Create(__instance).Field("savedItem").GetValue() as SavedItem;
                    if (savedItem == null) return true;
                    string locationName = LocationSet.GetCanonicalLocationName(
                        "Tool Unlock: " + savedItem.name
                    );

                    Location location = SaveState.Instance.locations.Locations.FirstOrDefault((f) => { return f.Name == locationName; });
                    if (location != null)
                    {
                        __result = SaveState.Instance.IsLocationChecked(locationName);
                        return false;
                    }
                }
                if (__instance.name == "Mapper Quill")
                {
                    __result = SaveState.Instance.IsLocationChecked("Item: Quill");
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_DisplayName")]
        internal static class ShopItem_DisplayName_Patch
        {
            private static bool Prefix(ShopItem __instance, ref string __result)
            {
                if (!TryResolveShopPreviewLocation(
                        __instance,
                        out string locationName))
                {
                    return true;
                }

                if (TryGetPresentationHint(
                        locationName,
                        out string user,
                        out string item,
                        out ItemFlags flags))
                {
                    __result = user + "'s " + item;
                }
                else
                {
                    __result = "AP Item";
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_Description")]
        internal static class ShopItem_Description_Patch
        {
            private static bool Prefix(ShopItem __instance, ref string __result)
            {
                if (!TryResolveShopPreviewLocation(
                        __instance,
                        out string locationName))
                {
                    return true;
                }

                if (TryGetPresentationHint(
                        locationName,
                        out string user,
                        out string item,
                        out ItemFlags flags))
                {
                    __result = user + "'s " + item + ".\r\n";
                    switch (flags)
                    {
                        case ItemFlags.None:
                            __result += "Seems not important.";
                            break;
                        case ItemFlags.Advancement:
                            __result += "It is very important!";
                            break;
                        case ItemFlags.NeverExclude:
                            __result += "Seems useful.";
                            break;
                        case ItemFlags.Trap:
                            __result += "Seems fun...";
                            break;
                    }
                }
                else
                {
                    __result = "Something for someone else, maybe...";
                }

                if (IsRuinedToolRepair(__instance))
                {
                    __result +=
                        "\r\nRequires Ruined Tool and 1 Craftmetal.";
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(ShopItem), "get_ItemSprite")]
        internal static class ShopItem_ItemSprite_Patch
        {
            private static bool Prefix(ShopItem __instance, ref Sprite __result)
            {
                if (!TryResolveShopPreviewLocation(
                        __instance,
                        out string locationName))
                {
                    return true;
                }

                RandomizerPlugin plugin = RandomizerPlugin.Instance;
                if (plugin == null)
                {
                    return true;
                }

                if (TryGetPresentationHint(
                        locationName,
                        out _,
                        out _,
                        out ItemFlags flags))
                {
                    __result = plugin.GetItemClassificationIcon(flags);
                }
                else
                {
                    __result =
                        plugin.MapCheckIcon ?? plugin.ArchipelagoIcon;
                }
                return false;
            }
        }
    }
}
