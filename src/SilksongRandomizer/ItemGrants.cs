using System;
using SilksongRandomizer.Patches;

namespace SilksongRandomizer
{
    internal static class ItemGrants
    {
        // These are the complete randomized pool counts. The aggregate
        // PlayerData counters themselves are not constrained by shop flags.
        private const int MaxCraftingKitUpgrades = 4;
        private const int MaxToolPouchUpgrades = 4;
        private const int MaxDruidsEyeLevel = 2;
        private const int MaxProgressiveToolLevel = 2;
        private const int MaxSwiftStepLevel = 2;
        private const int MaxSilkHeartLevel = 3;
        private const int MaxNeedleUpgradeLevel = 4;
        private const string PaleOilAssetName = "Pale_Oil";
        private const string RuinedToolAssetName = "Broken SilkShot";
        private const string ClawMirrorAssetName = "Dazzle Bind";
        private const string DarkMirrorAssetName = "Dazzle Bind Upgraded";
        private const string CurveclawAssetName = "Curve Claws";
        private const string CurvesickleAssetName = "Curve Claws Upgraded";

        public static void GrantDash()
        {
            SaveState state = RequireSaveState();
            state.canDash = true;

            // With the split disabled, retain vanilla's combined tap-to-dash
            // and hold-to-sprint acquisition.
            if (!state.splitDashAndSprint)
            {
                state.canSprint = true;
            }
        }

        public static void GrantFaydownCloak()
        {
            SaveState state = RequireSaveState();
            state.canDoubleJump = true;
            WispThicketFaydownPatches.SynchronizeActiveScene();
        }

        public static void GrantClingGrip()
        {
            SaveState state = RequireSaveState();
            state.canWallJump = true;
            FarFieldsWardenflyPatches.SynchronizeActiveScene();
        }

        public static void GrantSilkSoar()
        {
            SaveState state = RequireSaveState();
            state.canSilkSoar = true;
            SilkSoarStoryPatches.SynchronizeActiveScene();
        }

        public static void GrantProgressiveSwiftStep()
        {
            SaveState state = RequireSaveState();
            int nextLevel = Math.Min(
                state.swiftStepLevel + 1,
                MaxSwiftStepLevel
            );
            if (nextLevel == state.swiftStepLevel)
            {
                return;
            }

            state.swiftStepLevel = nextLevel;
            state.canSprint = true;
            if (nextLevel >= MaxSwiftStepLevel)
            {
                state.canDash = true;
            }
        }

        public static void GrantProgressiveSilkheart()
        {
            SaveState state = RequireSaveState();
            state.silkHeartLevel = Math.Min(
                state.silkHeartLevel + 1,
                MaxSilkHeartLevel
            );
        }

        public static void GrantRosaries(int amount)
        {
            if (TryGrantMemoryCurrency(amount, CurrencyType.Money))
            {
                return;
            }

            HeroController hero = RequireHero();
            hero.AddGeo(amount);
        }

        public static void GrantShellShards(int amount)
        {
            if (TryGrantMemoryCurrency(amount, CurrencyType.Shard))
            {
                return;
            }

            HeroController hero = RequireHero();
            hero.AddShards(amount);
        }

        private static bool TryGrantMemoryCurrency(
            int amount,
            CurrencyType currencyType)
        {
            PlayerData playerData = PlayerData.instance;
            if (!MemorySequenceSync.HasRecordedSnapshot(playerData))
            {
                return false;
            }

            int before = currencyType == CurrencyType.Money
                ? playerData.geo
                : playerData.ShellShards;
            if (!MemorySequenceSync.TryCaptureCurrency(
                    playerData,
                    currencyType,
                    out int snapshotBefore))
            {
                return false;
            }

            // PlayerData.AddGeo/AddShards are Harmony-owned by Currency Link
            // when that link is active and synchronized. In that case its
            // postfix mirrors the linked absolute value or rebases this exact
            // delta if the shared write fails. No second rebase occurs here.
            bool currencyLinkOwnsMutation =
                CurrencyLinkManager.OwnsLocalMutation(
                    playerData,
                    currencyType
                );

            // HeroController.AddGeo/AddShards normally queue this work until
            // CurrencyManager.LateUpdate. A memory reward must update the
            // recorded snapshot in the same ordered AP receipt, so reproduce
            // CurrencyManager.ProcessAddCurrency's public-data path exactly.
            CurrencyCounter.RefreshStartCount(currencyType);
            if (currencyType == CurrencyType.Money)
            {
                playerData.AddGeo(amount);
            }
            else
            {
                playerData.AddShards(amount);
            }

            int after = currencyType == CurrencyType.Money
                ? playerData.geo
                : playerData.ShellShards;
            if (!currencyLinkOwnsMutation)
            {
                MemorySequenceSync.RebaseCurrencyDelta(
                    playerData,
                    currencyType,
                    snapshotBefore,
                    before,
                    after
                );
            }
            CurrencyCounter.Add(after - before, currencyType);
            return true;
        }

        public static void GrantCollectable(string collectableAssetName)
        {
            CollectableItem collectable =
                CollectableItemManager.GetItemByName(
                    collectableAssetName
                );
            if (collectable == null)
            {
                throw new InvalidOperationException(
                    "Collectable asset is not ready: " +
                    collectableAssetName
                );
            }

            // The AP receive queue already owns the notification.
            // Collect directly so this cannot re-enter a physical-source hook.
            collectable.Collect(1, false);
        }

        public static void GrantTool(string toolAssetName)
        {
            ToolItem tool = Utils.FindToolScriptableObject<ToolItem>(
                toolAssetName
            );
            if (tool == null)
            {
                throw new InvalidOperationException(
                    "Tool asset is not ready: " + toolAssetName
                );
            }

            tool.Unlock(null, ToolItem.PopupFlags.None);
        }

        public static void GrantSlabKey(Action<PlayerData> grant)
        {
            if (grant == null)
            {
                throw new ArgumentNullException(nameof(grant));
            }

            // All three Slab pickups share one PlayerDataCollectable asset.
            // Their exact ownership is stored in separate PlayerData flags.
            // Setting only the requested flag preserves their destination
            // identity and avoids granting a fungible fourth key.
            PlayerData playerData = RequirePlayerData();
            grant(playerData);
            CollectableItemManager.IncrementVersion();
        }

        public static void GrantWhiteKey()
        {
            GrantCollectable("Ward Key");
            // This historical flag also suppresses Jubilana's fallback copy.
            RequirePlayerData().collectedWardKey = true;
            CollectableItemManager.IncrementVersion();
        }

        public static void GrantSurgeonsKey()
        {
            GrantCollectable("Ward Boss Key");
            // The hatch's historical ownership flag stays synchronized with
            // the native inventory item even when received over AP.
            RequirePlayerData().collectedWardBossKey = true;
            CollectableItemManager.IncrementVersion();
        }

        public static void GrantArchitectsKey()
        {
            GrantCollectable("Architect Key");
        }

        public static void GrantCrawSummons()
        {
            GrantCollectable("Craw Summons");
        }

        public static void GrantRuinedTool()
        {
            CollectableItem ruinedTool =
                CollectableItemManager.GetItemByName(
                    RuinedToolAssetName
                );
            if (ruinedTool == null)
            {
                throw new InvalidOperationException(
                    "Ruined Tool collectable asset is not ready."
                );
            }

            // There is one shared Ruined Tool in vanilla. Repair locations
            // temporarily return it while another randomized repair remains,
            // so an AP delivery cannot create duplicate copies.
            if (ruinedTool.CollectedAmount <= 0)
            {
                ruinedTool.Collect(1, false);
            }
        }

        public static void GrantCraftingKitUpgrade()
        {
            PlayerData playerData = RequirePlayerData();
            int nextUpgrade = Math.Min(
                playerData.ToolKitUpgrades + 1,
                MaxCraftingKitUpgrades
            );

            if (nextUpgrade == playerData.ToolKitUpgrades)
            {
                return;
            }

            playerData.ToolKitUpgrades = nextUpgrade;
            CollectableItemManager.IncrementVersion();
        }

        public static void GrantToolPouchUpgrade()
        {
            PlayerData playerData = RequirePlayerData();
            int nextUpgrade = Math.Min(
                playerData.ToolPouchUpgrades + 1,
                MaxToolPouchUpgrades
            );

            if (nextUpgrade == playerData.ToolPouchUpgrades)
            {
                return;
            }

            playerData.ToolPouchUpgrades = nextUpgrade;
            CollectableItemManager.IncrementVersion();
            ToolItemManager.SendEquippedChangedEvent(force: true);
        }

        public static void GrantProgressiveDruidsEye()
        {
            SaveState state = RequireSaveState();
            int nextLevel = Math.Min(
                state.druidsEyeLevel + 1,
                MaxDruidsEyeLevel
            );
            if (nextLevel == state.druidsEyeLevel)
            {
                return;
            }

            ToolItem baseEye =
                Utils.FindToolScriptableObject<ToolItem>(
                    "Mosscreep Tool 1"
                );
            ToolItem upgradedEyes =
                Utils.FindToolScriptableObject<ToolItem>(
                    "Mosscreep Tool 2"
                );
            if (baseEye == null || upgradedEyes == null)
            {
                throw new InvalidOperationException(
                    "Druid's Eye tool assets are not ready."
                );
            }

            state.druidsEyeLevel = nextLevel;
            SynchronizeProgressiveDruidsEyeEquips(
                baseEye,
                upgradedEyes
            );
        }

        public static void GrantProgressiveClawMirror()
        {
            SaveState state = RequireSaveState();
            int nextLevel = Math.Min(
                state.clawMirrorLevel + 1,
                MaxProgressiveToolLevel
            );
            if (nextLevel == state.clawMirrorLevel)
            {
                return;
            }

            if (!TryGetProgressiveToolPair(
                    ClawMirrorAssetName,
                    DarkMirrorAssetName,
                    out ToolItem baseTool,
                    out ToolItem upgradedTool))
            {
                throw new InvalidOperationException(
                    "Claw Mirror tool assets are not ready."
                );
            }

            state.clawMirrorLevel = nextLevel;
            SynchronizeProgressiveToolPair(
                baseTool,
                upgradedTool,
                nextLevel
            );
        }

        public static void GrantProgressiveCurveclaw()
        {
            SaveState state = RequireSaveState();
            int previousLevel = state.curveclawLevel;
            int nextLevel = Math.Min(
                previousLevel + 1,
                MaxProgressiveToolLevel
            );
            if (nextLevel == previousLevel)
            {
                return;
            }

            if (!TryGetProgressiveToolPair(
                    CurveclawAssetName,
                    CurvesickleAssetName,
                    out ToolItem baseTool,
                    out ToolItem upgradedTool))
            {
                throw new InvalidOperationException(
                    "Curveclaw tool assets are not ready."
                );
            }

            // Unlike ordinary AP-virtualized tools, Curveclaw has finite
            // stock. Reproduce the native first unlock's initial fill, then
            // carry the remaining stock into Curvesickle when tier two is
            // received. This changes only native ammo state, never a source
            // completion flag.
            InitializeProgressiveCurveclawStock(
                baseTool,
                upgradedTool,
                previousLevel,
                nextLevel
            );
            state.curveclawLevel = nextLevel;
            SynchronizeProgressiveToolPair(
                baseTool,
                upgradedTool,
                nextLevel
            );
        }

        public static void GrantProgressiveNeedleUpgrade()
        {
            SaveState state = RequireSaveState();
            PlayerData playerData = RequirePlayerData();
            int nextLevel = Math.Min(
                state.needleUpgradeLevel + 1,
                MaxNeedleUpgradeLevel
            );
            if (nextLevel == state.needleUpgradeLevel)
            {
                return;
            }

            state.needleUpgradeLevel = nextLevel;

            // A received progressive copy may only improve the native damage
            // tier. This also avoids downgrading a migrated save that already
            // had a higher vanilla Needle.
            int currentLevel = Math.Max(
                0,
                Math.Min(MaxNeedleUpgradeLevel, playerData.nailUpgrades)
            );
            playerData.nailUpgrades = Math.Max(currentLevel, nextLevel);
            playerData.InvNailHasNew = true;
            playerData.InvPaneHasNew = true;
            CollectableItemManager.IncrementVersion();
        }

        public static void GrantKrattFlea()
        {
            PlayerData playerData = RequirePlayerData();
            playerData.CaravanLechSaved = true;
            playerData.CaravanLechReturnedToCaravan = true;
        }

        public static void GrantVogFlea()
        {
            RequirePlayerData().MetTroupeHunterWild = true;
        }

        public static void GrantHugeFlea()
        {
            RequirePlayerData().tamedGiantFlea = true;
        }

        public static void GrantPaleOil()
        {
            CollectableItem paleOil =
                CollectableItemManager.GetItemByName(PaleOilAssetName);
            if (paleOil == null)
            {
                throw new InvalidOperationException(
                    "Pale Oil collectable asset is not ready."
                );
            }

            // Bypass SavedItem.TryGet: that method is patched at the three
            // vanilla source scenes to report their AP checks. The randomizer
            // already queues the AP receipt notification, so use the
            // native counter path without a duplicate Pale Oil popup.
            paleOil.Collect(1, false);
        }

        internal static bool TrySynchronizeProgressiveDruidsEyeEquips()
        {
            SaveState state = SaveState.Instance;
            if (state == null || state.druidsEyeLevel <= 0)
            {
                return true;
            }

            ToolItem baseEye =
                Utils.FindToolScriptableObject<ToolItem>(
                    "Mosscreep Tool 1"
                );
            ToolItem upgradedEyes =
                Utils.FindToolScriptableObject<ToolItem>(
                    "Mosscreep Tool 2"
                );
            if (baseEye == null || upgradedEyes == null)
            {
                return false;
            }

            SynchronizeProgressiveDruidsEyeEquips(
                baseEye,
                upgradedEyes
            );
            return true;
        }

        internal static bool TrySynchronizeProgressiveToolEquips()
        {
            SaveState state = SaveState.Instance;
            if (state == null)
            {
                return true;
            }

            if (!TryGetProgressiveToolPair(
                    ClawMirrorAssetName,
                    DarkMirrorAssetName,
                    out ToolItem clawMirror,
                    out ToolItem darkMirror) ||
                !TryGetProgressiveToolPair(
                    CurveclawAssetName,
                    CurvesickleAssetName,
                    out ToolItem curveclaw,
                    out ToolItem curvesickle))
            {
                return false;
            }

            SynchronizeProgressiveToolPair(
                clawMirror,
                darkMirror,
                state.clawMirrorLevel
            );
            SynchronizeProgressiveToolPair(
                curveclaw,
                curvesickle,
                state.curveclawLevel
            );
            return true;
        }

        internal static bool TryGetProgressiveToolState(
            ToolItem tool,
            out int level,
            out int requiredLevel,
            out bool isBaseTier
        )
        {
            level = 0;
            requiredLevel = 0;
            isBaseTier = false;
            SaveState state = SaveState.Instance;
            if (state == null || tool == null)
            {
                return false;
            }

            if (string.Equals(
                    tool.name,
                    ClawMirrorAssetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                level = state.clawMirrorLevel;
                requiredLevel = 1;
                isBaseTier = true;
                return true;
            }
            if (string.Equals(
                    tool.name,
                    DarkMirrorAssetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                level = state.clawMirrorLevel;
                requiredLevel = 2;
                return true;
            }
            if (string.Equals(
                    tool.name,
                    CurveclawAssetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                level = state.curveclawLevel;
                requiredLevel = 1;
                isBaseTier = true;
                return true;
            }
            if (string.Equals(
                    tool.name,
                    CurvesickleAssetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                level = state.curveclawLevel;
                requiredLevel = 2;
                return true;
            }

            return false;
        }

        private static bool TryGetProgressiveToolPair(
            string baseName,
            string upgradedName,
            out ToolItem baseTool,
            out ToolItem upgradedTool
        )
        {
            baseTool =
                Utils.FindToolScriptableObject<ToolItem>(baseName);
            upgradedTool =
                Utils.FindToolScriptableObject<ToolItem>(upgradedName);
            return baseTool != null && upgradedTool != null;
        }

        private static void SynchronizeProgressiveToolPair(
            ToolItem baseTool,
            ToolItem upgradedTool,
            int level
        )
        {
            if (level <= 0)
            {
                ToolItemManager.RemoveToolFromAllCrests(baseTool);
                ToolItemManager.RemoveToolFromAllCrests(upgradedTool);
            }
            else if (level == 1)
            {
                ToolItemManager.ReplaceToolEquips(
                    upgradedTool,
                    baseTool
                );
            }
            else
            {
                ToolItemManager.ReplaceToolEquips(
                    baseTool,
                    upgradedTool
                );
            }

            ToolItemManager.RefreshEquippedState();
            CollectableItemManager.IncrementVersion();
        }

        private static void InitializeProgressiveCurveclawStock(
            ToolItem baseTool,
            ToolItem upgradedTool,
            int previousLevel,
            int nextLevel
        )
        {
            PlayerData playerData = RequirePlayerData();
            if (previousLevel <= 0 && nextLevel >= 1)
            {
                int storageAmount =
                    ToolItemManager.GetToolStorageAmount(baseTool);
                ToolItemsData.Data persistentData =
                    playerData.Tools.GetData(baseTool.name);
                persistentData.AmountLeft = Math.Max(
                    persistentData.AmountLeft,
                    storageAmount
                );
                playerData.Tools.SetData(baseTool.name, persistentData);

                ToolItemsData.Data savedData = baseTool.SavedData;
                savedData.AmountLeft = Math.Max(
                    savedData.AmountLeft,
                    storageAmount
                );
                baseTool.SavedData = savedData;
            }

            if (previousLevel < 2 && nextLevel >= 2)
            {
                int remaining = Math.Max(
                    0,
                    baseTool.SavedData.AmountLeft
                );
                ToolItemsData.Data persistentData =
                    playerData.Tools.GetData(upgradedTool.name);
                persistentData.AmountLeft = Math.Max(
                    persistentData.AmountLeft,
                    remaining
                );
                playerData.Tools.SetData(
                    upgradedTool.name,
                    persistentData
                );

                ToolItemsData.Data savedData = upgradedTool.SavedData;
                savedData.AmountLeft = Math.Max(
                    savedData.AmountLeft,
                    remaining
                );
                upgradedTool.SavedData = savedData;
            }
        }

        private static void SynchronizeProgressiveDruidsEyeEquips(
            ToolItem baseEye,
            ToolItem upgradedEyes
        )
        {
            if (SaveState.Instance.druidsEyeLevel >= 2)
            {
                ToolItemManager.ReplaceToolEquips(
                    baseEye,
                    upgradedEyes
                );
            }
            else
            {
                // A level-one receipt must expose the base form even if the
                // native equipped state still references the upgraded asset.
                ToolItemManager.ReplaceToolEquips(
                    upgradedEyes,
                    baseEye
                );
            }

            ToolItemManager.RefreshEquippedState();
            CollectableItemManager.IncrementVersion();
        }

        public static void GrantMap(Action<PlayerData> grant)
        {
            if (grant == null)
            {
                throw new ArgumentNullException(nameof(grant));
            }

            PlayerData playerData = RequirePlayerData();
            grant(playerData);
            playerData.mapUpdateQueued = true;
            playerData.HasSeenMapUpdated = false;
        }

        public static void GrantStartWithMaps()
        {
            PlayerData playerData = RequirePlayerData();

            playerData.HasMossGrottoMap = true;
            playerData.HasBoneforestMap = true;
            playerData.HasDocksMap = true;
            playerData.HasWildsMap = true;
            playerData.HasCrawlMap = true;
            playerData.HasHuntersNestMap = true;
            playerData.HasGreymoorMap = true;
            playerData.HasBellhartMap = true;
            playerData.HasShellwoodMap = true;
            playerData.HasJudgeStepsMap = true;
            playerData.HasDustpensMap = true;
            playerData.HasPeakMap = true;
            playerData.HasCoralMap = true;
            playerData.HasSwampMap = true;

            playerData.HasWeavehomeMap = true;
            playerData.HasSongGateMap = true;
            playerData.HasCitadelUnderstoreMap = true;
            playerData.HasHallsMap = true;
            playerData.HasLibraryMap = true;
            playerData.HasWardMap = true;
            playerData.HasCogMap = true;
            playerData.HasArboriumMap = true;
            playerData.HasHangMap = true;
            playerData.HasSlabMap = true;
            playerData.HasAqueductMap = true;
            playerData.HasCradleMap = true;
            playerData.HasAbyssMap = true;

            // All guaranteed starting maps are one native map update.
            // Lost Verdania remains at its ordinary source.
            playerData.mapUpdateQueued = true;
            playerData.HasSeenMapUpdated = false;
        }

        public static void GrantStartFullyMapped()
        {
            PlayerData playerData = RequirePlayerData();
            playerData.mapAllRooms = true;
            playerData.mapUpdateQueued = true;
            playerData.HasSeenMapUpdated = false;
        }

        public static void GrantMelody(Action<PlayerData> grant)
        {
            if (grant == null)
            {
                throw new ArgumentNullException(nameof(grant));
            }

            PlayerData playerData = RequirePlayerData();
            grant(playerData);
            CollectableItemManager.IncrementVersion();
        }

        public static void GrantBell()
        {
            PlayerData playerData = RequirePlayerData();
            playerData.HasSeenMapMarkerUpdated = false;
        }

        public static void GrantBeastlingCall()
        {
            SaveState state = RequireSaveState();
            PlayerData playerData = RequirePlayerData();

            state.canUseBeastlingCall = true;
            playerData.UnlockedFastTravelTeleport = true;
            CollectableItemManager.IncrementVersion();
        }

        public static void GrantPin(Action<PlayerData> grant)
        {
            if (grant == null)
            {
                throw new ArgumentNullException(nameof(grant));
            }

            PlayerData playerData = RequirePlayerData();
            grant(playerData);
            playerData.HasSeenMapMarkerUpdated = false;
        }

        public static void GrantRelic(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                throw new ArgumentException("Relic asset name is required.", nameof(assetName));
            }

            if (CollectableRelicManager.Instance == null)
            {
                throw new InvalidOperationException(
                    "Collectable relic manager is not ready."
                );
            }

            CollectableRelic relic = CollectableRelicManager.GetRelic(assetName);
            if (relic == null)
            {
                // A missing grant is not committed. The ordered AP receive queue
                // will retry after the native relic manager is ready.
                throw new InvalidOperationException(
                    "Collectable relic asset is not ready: " + assetName
                );
            }

            CoreLocationPatches.GrantRelicWithoutChecking(relic, false);
        }

        private static HeroController RequireHero()
        {
            HeroController hero = HeroController.instance;
            if (hero == null)
            {
                throw new InvalidOperationException("HeroController is not ready.");
            }

            return hero;
        }

        private static PlayerData RequirePlayerData()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                throw new InvalidOperationException("PlayerData is not ready.");
            }

            return playerData;
        }

        private static SaveState RequireSaveState()
        {
            SaveState state = SaveState.Instance;
            if (state == null)
            {
                throw new InvalidOperationException(
                    "Randomizer save state is not ready."
                );
            }

            return state;
        }
    }
}
