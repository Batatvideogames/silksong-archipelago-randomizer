using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class PriceRandomizerPatches
    {
        private const string ShopPrefix = "shop:";
        private const string BellwayPrefix = "bellway:";
        private const string DonationPrefix = "donation:";
        private const string UpgradePrefix = "upgrade:plinney:";

        private const string BellwayTollObject = "Bellway Toll Machine";
        private const string BellwayTollFsm = "Unlock Behaviour";
        private const string BellwayFlagVariable =
            "Pickup PlayerData Bool";
        private const string BellwayCostVariable = "Cost Reference";

        private const string PlinneyScene = "Belltown_Room_pinsmith";
        private const string PlinneyObject = "Plinney Inside";
        private const string PlinneyFsm = "Dialogue";
        private const string PlinneyCostState = "Upgrade? Cost";

        private static readonly FieldInfo IntReferenceValueField =
            AccessTools.Field(typeof(IntReference), "value");

        private static readonly FieldInfo QuestTargetsField =
            AccessTools.Field(typeof(FullQuestBase), "targets");

        private static readonly Dictionary<string, int>
            VanillaDonationPrices =
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    { "Belltown House Start", 250 },
                    { "Belltown House Mid", 400 },
                    { "Songclave Donation 1", 300 },
                    { "Songclave Donation 2", 500 },
                    { "Building Materials", 200 },
                    { "Building Materials (Bridge)", 300 },
                    { "Building Materials (Statue)", 440 },
                };

        private static bool TryGetPrice(string key, out int price)
        {
            price = 0;
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.TryGetPurchasePrice(key, out price);
        }

        [HarmonyPatch(typeof(ShopItem), "get_Cost")]
        private static class ShopItemCostPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                ShopItem __instance,
                ref int __result)
            {
                if (__instance == null ||
                    !__instance.IsAvailableNotInfinite ||
                    !TryGetPrice(
                        ShopPrefix + (__instance.name ?? string.Empty),
                        out int price
                    ))
                {
                    return true;
                }

                // The native IsAvailableNotInfinite property is the shipped
                // distinction between finite stock and repeatable currency
                // exchanges. ShopItem.Cost is used by the shop display,
                // affordability check and SetPurchased payment path. One
                // override keeps all three views of a finite transaction
                // identical without touching repeatable stock.
                __result = price;
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class BellwayTollCostPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(PlayMakerFSM __instance)
            {
                if (__instance == null ||
                    __instance.gameObject == null ||
                    !string.Equals(
                        __instance.FsmName,
                        BellwayTollFsm,
                        StringComparison.Ordinal
                    ) ||
                    !(
                        string.Equals(
                            __instance.gameObject.name,
                            BellwayTollObject,
                            StringComparison.Ordinal
                        ) ||
                        string.Equals(
                            __instance.gameObject.name,
                            BellwayTollObject + " (1)",
                            StringComparison.Ordinal
                        )
                    ))
                {
                    return;
                }

                FsmString flagVariable =
                    __instance.FsmVariables?.GetFsmString(
                        BellwayFlagVariable
                    );
                string flag = flagVariable?.Value ?? string.Empty;
                string scene = GameManager.GetBaseSceneName(
                    __instance.gameObject.scene.name ?? string.Empty
                );
                string key = BellwayPrefix + scene + ":" +
                    __instance.gameObject.name + ":" + flag;
                if (!TryGetPrice(key, out int price))
                {
                    return;
                }

                FsmObject costVariable =
                    __instance.FsmVariables?.GetFsmObject(
                        BellwayCostVariable
                    );
                if (costVariable == null ||
                    IntReferenceValueField == null)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Could not apply Bellway price for " +
                        key + ": the Cost Reference field was not found."
                    );
                    return;
                }

                CostReference reference =
                    ScriptableObject.CreateInstance<CostReference>();
                reference.name = "AP " + key;
                IntReferenceValueField.SetValue(reference, price);
                costVariable.Value = reference;
            }
        }

        private static void ApplyDonationPrice(FullQuestBase quest)
        {
            if (quest == null || QuestTargetsField == null ||
                !VanillaDonationPrices.TryGetValue(
                    quest.name ?? string.Empty,
                    out int vanillaPrice
                ))
            {
                return;
            }

            int price = vanillaPrice;
            if (TryGetPrice(
                    DonationPrefix + quest.name,
                    out int randomizedPrice
                ))
            {
                price = randomizedPrice;
            }

            FullQuestBase.QuestTarget[] targets =
                QuestTargetsField.GetValue(quest) as
                    FullQuestBase.QuestTarget[];
            if (targets == null)
            {
                return;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                FullQuestBase.QuestTarget target = targets[index];
                if (!(target.Counter is QuestTargetCurrency))
                {
                    continue;
                }

                target.Count = price;
                targets[index] = target;
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_Targets")]
        private static class DonationTargetsPatch
        {
            [HarmonyPrefix]
            private static void Prefix(FullQuestBase __instance)
            {
                ApplyDonationPrice(__instance);
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_Counters")]
        private static class DonationCountersPatch
        {
            [HarmonyPrefix]
            private static void Prefix(FullQuestBase __instance)
            {
                ApplyDonationPrice(__instance);
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_CanComplete")]
        private static class DonationCanCompletePatch
        {
            [HarmonyPrefix]
            private static void Prefix(FullQuestBase __instance)
            {
                ApplyDonationPrice(__instance);
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "TryEndQuest")]
        private static class DonationTryEndQuestPatch
        {
            [HarmonyPrefix]
            private static void Prefix(FullQuestBase __instance)
            {
                // TryEndQuest consumes the private target array directly, so
                // refresh it immediately before the quest consumes it.
                ApplyDonationPrice(__instance);
            }
        }

        [HarmonyPatch(
            typeof(GetCostFromReference),
            nameof(GetCostFromReference.OnEnter))]
        private static class PlinneyUpgradeCostPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(GetCostFromReference __instance)
            {
                if (!IsExactPlinneyCostAction(__instance))
                {
                    return true;
                }

                SaveState state = SaveState.Instance;
                int purchasedTier;
                if (state != null && state.randomizeNeedleUpgrades)
                {
                    purchasedTier =
                        NeedleUpgradePatches.GetPurchasedPlinneyTier();
                }
                else
                {
                    purchasedTier = PlayerData.instance == null
                        ? -1
                        : PlayerData.instance.nailUpgrades;
                }

                // Only services 3 and 4 have Rosary costs in the shipped FSM.
                // The first two remain free/item-gated in every mode.
                int serviceNumber = purchasedTier + 1;
                if ((serviceNumber != 3 && serviceNumber != 4) ||
                    !TryGetPrice(
                        UpgradePrefix + serviceNumber,
                        out int price
                    ) ||
                    __instance.StoreValue == null)
                {
                    return true;
                }

                __instance.StoreValue.Value = price;
                __instance.Finish();
                return false;
            }
        }

        private static bool IsExactPlinneyCostAction(
            FsmStateAction action)
        {
            return action != null &&
                   action.Owner != null &&
                   action.Fsm != null &&
                   action.State != null &&
                   string.Equals(
                       GameManager.instance?.sceneName ?? string.Empty,
                       PlinneyScene,
                       StringComparison.OrdinalIgnoreCase
                   ) &&
                   string.Equals(
                       action.Owner.name,
                       PlinneyObject,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       action.Fsm.Name,
                       PlinneyFsm,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       action.State.Name,
                       PlinneyCostState,
                       StringComparison.Ordinal
                   );
        }
    }
}
