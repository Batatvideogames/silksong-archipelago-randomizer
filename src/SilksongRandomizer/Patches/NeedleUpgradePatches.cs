using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace SilksongRandomizer.Patches
{
    internal static class NeedleUpgradePatches
    {
        private static readonly MethodInfo UpdateNeedleState =
            AccessTools.Method(typeof(InventoryItemNail), "UpdateState");
        private static readonly MethodInfo UpdateNeedleDisplay =
            AccessTools.Method(typeof(InventoryItemUpdateable), "UpdateDisplay");

        internal static int ResolveSelectedNeedleTier(int unlocked, int selected)
        {
            unlocked = Math.Max(0, Math.Min(4, unlocked));
            return selected < 0 ? unlocked : Math.Min(unlocked, selected);
        }

        private static int GetUnlockedNeedleTier(SaveState state, PlayerData playerData)
        {
            return Math.Max(playerData.nailUpgrades,
                state.IsRandomized(ItemType.NeedleUpgrade) ? state.needleUpgradeLevel : 0);
        }

        internal static int GetSelectedNeedleTier(PlayerData playerData)
        {
            SaveState state = SaveState.Instance;
            return state == null ? playerData.nailUpgrades : ResolveSelectedNeedleTier(
                GetUnlockedNeedleTier(state, playerData), state.selectedNeedleUpgradeLevel);
        }

        internal static bool TryCycleNeedle(SaveState state, PlayerData playerData)
        {
            int unlocked = Math.Max(0, Math.Min(4, GetUnlockedNeedleTier(state, playerData)));
            if (unlocked == 0)
            {
                return false;
            }
            int selected = ResolveSelectedNeedleTier(unlocked, state.selectedNeedleUpgradeLevel);
            state.selectedNeedleUpgradeLevel = (selected + 1) % (unlocked + 1);
            return true;
        }

        [HarmonyPatch(typeof(InventoryItemSelectable), nameof(InventoryItemSelectable.Submit))]
        private static class CycleNeedlePatch
        {
            private static bool Prefix(InventoryItemSelectable __instance, ref bool __result)
            {
                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (!(__instance is InventoryItemNail needle) || state == null ||
                    playerData == null || !TryCycleNeedle(state, playerData))
                {
                    return true;
                }
                UpdateNeedleState.Invoke(needle, null);
                UpdateNeedleDisplay.Invoke(needle, null);
                __result = true;
                return false;
            }
        }

        [HarmonyPatch]
        private static class SelectedNeedleTierPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.PropertyGetter(typeof(PlayerData), nameof(PlayerData.nailDamage));
                yield return AccessTools.Method(typeof(HealthManager), "ApplyDamageScaling");
                yield return UpdateNeedleState;
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                FieldInfo field = AccessTools.Field(typeof(PlayerData), nameof(PlayerData.nailUpgrades));
                MethodInfo selected = AccessTools.Method(typeof(NeedleUpgradePatches), nameof(GetSelectedNeedleTier));
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.LoadsField(field))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = selected;
                    }
                    yield return instruction;
                }
            }
        }

        private const string PlinneyScene = "Belltown_Room_pinsmith";
        private const string PlinneyObject = "Plinney Inside";
        private const string PlinneyFsm = "Dialogue";
        private const string PlinneyUpgradeStateRead = "Upgrade State";
        private const string PlinneyUpgradeState = "Upgrade";
        private const string NeedleUpgradeAsset = "Needle Upgrade";
        private const string PaleOilAsset = "Pale_Oil";

        private const string WhisperingVaultsScene = "Library_03";
        private const string GreatTasteScene = "Song_09b";
        private const string EcstasyOfTheEndScene =
            "Aqueduct_05_festival";

        private static readonly string[] PlinneyLocations =
        {
            "Pinmaster Plinney: Sharpened Needle",
            "Pinmaster Plinney: Shining Needle",
            "Pinmaster Plinney: Hivesteel Needle",
            "Pinmaster Plinney: Pale Steel Needle",
        };

        private static bool NeedleUpgradesEnabled
        {
            get
            {
                return SaveState.Instance != null &&
                       SaveState.Instance.GetRandomizationMode(
                           ItemType.NeedleUpgrade
                       ) != RandomizationMode.Vanilla;
            }
        }

        private static bool PaleOilEnabled
        {
            get
            {
                return SaveState.Instance != null &&
                       SaveState.Instance.GetRandomizationMode(
                           ItemType.PaleOil
                       ) != RandomizationMode.Vanilla;
            }
        }

        private static string CurrentSceneName
        {
            get
            {
                return GameManager.instance == null
                    ? string.Empty
                    : GameManager.instance.sceneName ?? string.Empty;
            }
        }

        private static bool IsExactPlinneyAction(
            FsmStateAction action,
            string stateName)
        {
            return action != null &&
                   action.Owner != null &&
                   action.Fsm != null &&
                   action.State != null &&
                   string.Equals(
                       CurrentSceneName,
                       PlinneyScene,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       action.Owner.name,
                       PlinneyObject,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       action.Fsm.Name,
                       PlinneyFsm,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       action.State.Name,
                       stateName,
                       StringComparison.Ordinal);
        }

        internal static int GetPurchasedPlinneyTier()
        {
            SaveState state = SaveState.Instance;
            if (state == null)
            {
                return 0;
            }

            int tier = 0;
            while (tier < PlinneyLocations.Length &&
                   state.IsLocationChecked(PlinneyLocations[tier]))
            {
                tier++;
            }

            return tier;
        }

        private static string GetCurrentPlinneyLocation()
        {
            int tier = GetPurchasedPlinneyTier();
            return tier >= 0 && tier < PlinneyLocations.Length
                ? PlinneyLocations[tier]
                : null;
        }

        private static string GetPaleOilLocation()
        {
            switch (CurrentSceneName)
            {
                case WhisperingVaultsScene:
                    return "Pale Oil: Whispering Vaults";
                case GreatTasteScene:
                    return "Pale Oil: Great Taste of Pharloom";
                case EcstasyOfTheEndScene:
                    return "Pale Oil: Ecstasy of the End";
                default:
                    return null;
            }
        }

        [HarmonyPatch(
            typeof(GetPlayerDataInt),
            nameof(GetPlayerDataInt.OnEnter))]
        private static class PlinneyServiceProgressPatch
        {
            private static bool Prefix(GetPlayerDataInt __instance)
            {
                if (!NeedleUpgradesEnabled ||
                    !IsExactPlinneyAction(
                        __instance,
                        PlinneyUpgradeStateRead) ||
                    __instance.intName == null ||
                    !string.Equals(
                        __instance.intName.Value,
                        "nailUpgrades",
                        StringComparison.Ordinal) ||
                    __instance.storeValue == null)
                {
                    return true;
                }

                // Plinney's dialogue/cost chain follows bought services, while
                // combat damage follows received Progressive Needle Upgrades.
                // Keeping those counters separate prevents a checked service
                // from being offered and charged for a second time while its
                // Archipelago item is still in transit.
                __instance.storeValue.Value =
                    GetPurchasedPlinneyTier();
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(SavedItemGetV2), nameof(SavedItemGetV2.OnEnter))]
        private static class PlinneyNeedleRewardPatch
        {
            private static bool Prefix(SavedItemGetV2 __instance)
            {
                if (!NeedleUpgradesEnabled ||
                    !IsExactPlinneyAction(
                        __instance,
                        PlinneyUpgradeState))
                {
                    return true;
                }

                SavedItem item = __instance.Item == null
                    ? null
                    : __instance.Item.Value as SavedItem;
                if (item == null ||
                    !string.Equals(
                        item.name,
                        NeedleUpgradeAsset,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                string locationName = GetCurrentPlinneyLocation();
                if (!string.IsNullOrEmpty(locationName))
                {
                    SaveState.Instance.CheckLocation(locationName);
                }

                // The rest of the shipped state remains intact. Inventory-new flags,
                // selection, save request, cutscene and dialogue all continue.
                // Only the native nailUpgrades increment is replaced by AP.
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(
            typeof(SavedItem),
            nameof(SavedItem.TryGet),
            new Type[] { typeof(bool), typeof(bool) })]
        private static class PaleOilSourceRewardPatch
        {
            private static bool Prefix(
                SavedItem __instance,
                ref bool __result)
            {
                SaveState state = SaveState.Instance;
                string locationName = GetPaleOilLocation();
                if (!PaleOilEnabled ||
                    string.IsNullOrEmpty(locationName) ||
                    !state.IsLocationInSeed(locationName) ||
                    __instance == null ||
                    !string.Equals(
                        __instance.name,
                        PaleOilAsset,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                state.CheckLocation(locationName);
                __result = true;
                return false;
            }
        }
    }
}
