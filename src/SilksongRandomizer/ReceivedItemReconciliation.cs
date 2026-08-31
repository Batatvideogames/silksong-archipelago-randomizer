using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SilksongRandomizer
{
    internal static class ReceivedItemReconciliation
    {
        private const int MaxCraftingKitUpgrades = 4;
        private const int MaxToolPouchUpgrades = 4;
        private const int MaxNeedleUpgrades = 4;
        private const string WhiteKeySceneName = "Ward_01";
        private const string WhiteKeyGateId = "under_lift_large";
        private const string SurgeonsKeySceneName = "Ward_02";
        private const string SurgeonsKeyGateId = "lock_machine";
        private const string ArchitectsKeySceneName = "Under_17";
        private const string ArchitectsKeyGateId = "Architect Shrine Door";

        private static readonly Dictionary<string, string>
            PlayerDataFlagsByItem =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase
                )
                {
                    { "Map: Mosslands", "HasMossGrottoMap" },
                    { "Map: The Marrow", "HasBoneforestMap" },
                    { "Map: Deep Docks", "HasDocksMap" },
                    { "Map: Far Fields", "HasWildsMap" },
                    { "Map: Wormways", "HasCrawlMap" },
                    { "Map: Hunter's March", "HasHuntersNestMap" },
                    { "Map: Greymoor", "HasGreymoorMap" },
                    { "Map: Bellhart", "HasBellhartMap" },
                    { "Map: Shellwood", "HasShellwoodMap" },
                    { "Map: Sands of Karak", "HasCoralMap" },
                    { "Map: Sinner's Road", "HasDustpensMap" },
                    { "Map: Mount Fay", "HasPeakMap" },
                    { "Map: Blasted Steps", "HasJudgeStepsMap" },
                    { "Map: Bilewater", "HasSwampMap" },
                    { "Map: Weavenest Atla", "HasWeavehomeMap" },
                    { "Map: Grand Gate", "HasSongGateMap" },
                    { "Map: Underworks", "HasCitadelUnderstoreMap" },
                    { "Map: Choral Chambers", "HasHallsMap" },
                    { "Map: Whispering Vaults", "HasLibraryMap" },
                    { "Map: Whiteward", "HasWardMap" },
                    { "Map: Cogwork Core", "HasCogMap" },
                    { "Map: Memorium", "HasArboriumMap" },
                    { "Map: High Halls", "HasHangMap" },
                    { "Map: The Slab", "HasSlabMap" },
                    { "Map: Putrified Ducts", "HasAqueductMap" },
                    { "Map: The Cradle", "HasCradleMap" },
                    { "Map: Verdania", "HasCloverMap" },
                    { "Map: The Abyss", "HasAbyssMap" },
                    { "Bench Pins", "hasPinBench" },
                    { "Ventrica Pins", "hasPinTube" },
                    { "Bellway Pins", "hasPinStag" },
                    { "Vendor Pins", "hasPinShop" },
                };

        internal static bool ReconcilePermanentState()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null || playerData == null)
            {
                return false;
            }

            bool mapChanged = false;
            bool pinChanged = false;
            foreach (KeyValuePair<string, string> entry in
                     PlayerDataFlagsByItem)
            {
                FieldInfo field = typeof(PlayerData).GetField(
                    entry.Value,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );
                if (!HasReceived(state, entry.Key) ||
                    field == null ||
                    (bool)field.GetValue(playerData))
                {
                    continue;
                }

                field.SetValue(playerData, true);
                if (entry.Key.StartsWith(
                        "Map: ",
                        StringComparison.OrdinalIgnoreCase))
                {
                    mapChanged = true;
                }
                else
                {
                    pinChanged = true;
                }
            }

            if (mapChanged)
            {
                playerData.mapUpdateQueued = true;
                playerData.HasSeenMapUpdated = false;
            }
            if (pinChanged)
            {
                playerData.HasSeenMapMarkerUpdated = false;
            }

            Patches.QuillPatches.ReconcileReceivedQuill(
                state,
                playerData,
                HasReceived(
                    state,
                    Patches.QuillPatches.QuillItemName
                ) ||
                HasReceived(
                    state,
                    Patches.QuillPatches.ProgressiveCompassItemName
                )
            );

            bool inventoryChanged = false;
            bool toolPouchChanged = false;
            int craftingKits = CountReceivedCopies(
                state,
                "Progressive Crafting Kit",
                MaxCraftingKitUpgrades
            );
            if (state.IsRandomized(ItemType.Upgrade) &&
                playerData.ToolKitUpgrades < craftingKits)
            {
                playerData.ToolKitUpgrades = craftingKits;
                inventoryChanged = true;
            }

            int toolPouches = CountReceivedCopies(
                state,
                "Progressive Tool Pouch",
                MaxToolPouchUpgrades
            );
            if (state.IsRandomized(ItemType.ToolPouch) &&
                playerData.ToolPouchUpgrades < toolPouches)
            {
                playerData.ToolPouchUpgrades = toolPouches;
                inventoryChanged = true;
                toolPouchChanged = true;
            }

            int needleLevel = Math.Min(
                MaxNeedleUpgrades,
                Math.Max(0, state.needleUpgradeLevel)
            );
            if (state.IsRandomized(ItemType.NeedleUpgrade) &&
                playerData.nailUpgrades < needleLevel)
            {
                playerData.nailUpgrades = needleLevel;
                playerData.InvNailHasNew = true;
                playerData.InvPaneHasNew = true;
                inventoryChanged = true;
            }

            if (inventoryChanged)
            {
                CollectableItemManager.IncrementVersion();
            }
            if (HasReceived(state, Patches.FleaPatches.KrattItemName))
            {
                ItemGrants.GrantKrattFlea();
            }
            if (HasReceived(state, Patches.FleaPatches.VogItemName))
            {
                ItemGrants.GrantVogFlea();
            }
            if (HasReceived(state, Patches.FleaPatches.HugeFleaItemName))
            {
                ItemGrants.GrantHugeFlea();
            }

            return toolPouchChanged;
        }

        internal static IEnumerator ReconcileReceivedMajorKeysAfterLoad(
            SaveState expectedState)
        {
            while (ReferenceEquals(SaveState.Instance, expectedState))
            {
                if (TryReconcileReceivedMajorKeys())
                {
                    yield break;
                }
                yield return null;
            }
        }

        internal static bool TryReconcileReceivedMajorKeys()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null || playerData == null)
            {
                return false;
            }

            bool ownsApostate = HasReceived(state, "Key of Apostate");
            bool ownsWhiteKey = HasReceived(state, "White Key");
            bool ownsSurgeonsKey = HasReceived(state, "Surgeon's Key");
            bool ownsArchitectsKey = HasReceived(state, "Architect's Key");
            bool ownsCrawSummons = HasReceived(state, "Craw Summons");

            bool whiteKeySpent = false;
            bool surgeonsKeySpent = false;
            bool architectsKeySpent = false;
            if (ownsWhiteKey || ownsSurgeonsKey || ownsArchitectsKey)
            {
                SceneData sceneData = SceneData.instance;
                if (sceneData == null)
                {
                    return false;
                }

                whiteKeySpent = ownsWhiteKey &&
                    sceneData.PersistentBools.GetValueOrDefault(
                        WhiteKeySceneName,
                        WhiteKeyGateId
                    );
                surgeonsKeySpent = ownsSurgeonsKey &&
                    sceneData.PersistentBools.GetValueOrDefault(
                        SurgeonsKeySceneName,
                        SurgeonsKeyGateId
                    );
                architectsKeySpent = ownsArchitectsKey &&
                    sceneData.PersistentBools.GetValueOrDefault(
                        ArchitectsKeySceneName,
                        ArchitectsKeyGateId
                    );
            }

            CollectableItem whiteKey = GetReadyCollectable(
                ownsWhiteKey && !whiteKeySpent,
                "Ward Key"
            );
            CollectableItem surgeonsKey = GetReadyCollectable(
                ownsSurgeonsKey && !surgeonsKeySpent,
                "Ward Boss Key"
            );
            CollectableItem architectsKey = GetReadyCollectable(
                ownsArchitectsKey && !architectsKeySpent,
                "Architect Key"
            );
            CollectableItem crawSummons = GetReadyCollectable(
                ownsCrawSummons && !playerData.OpenedCrowSummonsDoor,
                "Craw Summons"
            );
            if ((ownsWhiteKey && !whiteKeySpent && whiteKey == null) ||
                (ownsSurgeonsKey &&
                 !surgeonsKeySpent &&
                 surgeonsKey == null) ||
                (ownsArchitectsKey &&
                 !architectsKeySpent &&
                 architectsKey == null) ||
                (ownsCrawSummons &&
                 !playerData.OpenedCrowSummonsDoor &&
                 crawSummons == null))
            {
                return false;
            }

            bool playerDataChanged = false;
            if (ShouldRestorePermanentKey(
                    ownsApostate,
                    playerData.HasSlabKeyC))
            {
                playerData.HasSlabKeyC = true;
                playerDataChanged = true;
            }
            if (ownsWhiteKey && !playerData.collectedWardKey)
            {
                playerData.collectedWardKey = true;
                playerDataChanged = true;
            }
            if (ownsSurgeonsKey && !playerData.collectedWardBossKey)
            {
                playerData.collectedWardBossKey = true;
                playerDataChanged = true;
            }

            RestoreConsumableKey(ownsWhiteKey, whiteKey, whiteKeySpent);
            RestoreConsumableKey(
                ownsSurgeonsKey,
                surgeonsKey,
                surgeonsKeySpent
            );
            RestoreConsumableKey(
                ownsArchitectsKey,
                architectsKey,
                architectsKeySpent
            );
            RestoreConsumableKey(
                ownsCrawSummons,
                crawSummons,
                playerData.OpenedCrowSummonsDoor
            );

            if (playerDataChanged)
            {
                CollectableItemManager.IncrementVersion();
            }
            return true;
        }

        internal static bool ShouldRestorePermanentKey(
            bool hasReceived,
            bool nativeOwned)
        {
            return hasReceived && !nativeOwned;
        }

        internal static bool ShouldRestoreConsumableKey(
            bool hasReceived,
            int nativeAmount,
            bool spent)
        {
            return hasReceived && nativeAmount <= 0 && !spent;
        }

        private static CollectableItem GetReadyCollectable(
            bool required,
            string nativeName)
        {
            return required
                ? CollectableItemManager.GetItemByName(nativeName)
                : null;
        }

        private static void RestoreConsumableKey(
            bool hasReceived,
            CollectableItem nativeItem,
            bool spent)
        {
            int nativeAmount = nativeItem == null
                ? 0
                : nativeItem.CollectedAmount;
            if (ShouldRestoreConsumableKey(
                    hasReceived,
                    nativeAmount,
                    spent))
            {
                nativeItem.Collect(1, false);
            }
        }

        private static bool HasReceived(
            SaveState state,
            string itemName)
        {
            return state.receivedItems != null &&
                   state.receivedItems.Contains(
                       ItemSet.GetCanonicalItemName(itemName)
                   );
        }

        private static int CountReceivedCopies(
            SaveState state,
            string itemName,
            int maximum)
        {
            string canonicalName = ItemSet.GetCanonicalItemName(itemName);
            int count = 0;
            if (state.receivedItemHistory != null)
            {
                foreach (string receivedName in state.receivedItemHistory)
                {
                    if (string.Equals(
                            ItemSet.GetCanonicalItemName(receivedName),
                            canonicalName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                    }
                }
            }

            if (count == 0 && HasReceived(state, canonicalName))
            {
                count = 1;
            }
            return Math.Min(maximum, count);
        }
    }
}
