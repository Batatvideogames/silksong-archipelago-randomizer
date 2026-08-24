using HarmonyLib;
using SilksongRandomizer.AlphabetMode;
using System;
using System.Collections.Generic;

namespace SilksongRandomizer.Patches
{
    internal static class ScroungeRelicTurnInPatches
    {
        [ThreadStatic]
        private static int relicTurnInPromptScopeDepth;

        private static bool IsEnabled()
        {
            SaveState state = SaveState.Instance;
            return state != null && state.individualRelicTurnIns;
        }

        private static bool IsTurnInOwner(
            string sceneName,
            string objectName
        )
        {
            return (
                string.Equals(
                    sceneName,
                    ScroungeRelicTurnInManifest.SceneName,
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    objectName,
                    ScroungeRelicTurnInManifest.OwnerObjectName,
                    StringComparison.Ordinal
                )
            ) || (
                string.Equals(
                    sceneName,
                    CardiniusCylinderTurnInManifest.SceneName,
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    objectName,
                    CardiniusCylinderTurnInManifest.OwnerObjectName,
                    StringComparison.Ordinal
                )
            );
        }

        private static bool IsIndividualTurnInRelicType(
            CollectableItemRelicType relicType
        )
        {
            if (!IsEnabled() || relicType == null ||
                relicType.Relics == null)
            {
                return false;
            }

            foreach (CollectableRelic relic in relicType.Relics)
            {
                if (relic != null &&
                    (ScroungeRelicTurnInManifest.TryGetLocationName(
                         relic.name,
                         out _
                     ) ||
                     CardiniusCylinderTurnInManifest.TryGetLocationName(
                         relic.name,
                         out _
                     )))
                {
                    return true;
                }
            }

            return false;
        }

        [HarmonyPatch(
            typeof(CollectableItemRelicType),
            nameof(CollectableItemRelicType.RewardAmount),
            MethodType.Getter
        )]
        private static class ScroungeRewardAmountPatch
        {
            private static bool Prefix(
                CollectableItemRelicType __instance,
                ref int __result
            )
            {
                if (!IsIndividualTurnInRelicType(__instance))
                {
                    return true;
                }

                // Scrounge and Cardinius deposits replace, rather than
                // supplement, their native Rosary payouts.
                __result = 0;
                return false;
            }
        }

        [HarmonyPatch(
            typeof(HutongGames.PlayMaker.Actions.RelicBoardOwnerYesNo),
            "DoOpen"
        )]
        private static class ScroungePromptScopePatch
        {
            private static void Prefix(
                HutongGames.PlayMaker.Actions.RelicBoardOwnerYesNo __instance,
                out bool __state
            )
            {
                __state = IsEnabled() &&
                    __instance != null &&
                    __instance.Owner != null &&
                    IsTurnInOwner(
                        __instance.Owner.scene.name,
                        __instance.Owner.name
                    );
                if (__state)
                {
                    relicTurnInPromptScopeDepth++;
                }
            }

            private static Exception Finalizer(
                Exception __exception,
                bool __state
            )
            {
                if (__state && relicTurnInPromptScopeDepth > 0)
                {
                    relicTurnInPromptScopeDepth--;
                }

                return __exception;
            }
        }

        [HarmonyPatch(
            typeof(DialogueYesNoBox),
            nameof(DialogueYesNoBox.Open),
            new Type[]
            {
                typeof(Action),
                typeof(Action),
                typeof(bool),
                typeof(string),
                typeof(IReadOnlyList<SavedItem>),
                typeof(IReadOnlyList<int>),
                typeof(bool),
                typeof(bool),
                typeof(SavedItem),
            }
        )]
        private static class ScroungeConfirmationPromptPatch
        {
            private static void Prefix(
                [HarmonyArgument(3)] ref string text,
                [HarmonyArgument(4)] IReadOnlyList<SavedItem> items,
                [HarmonyArgument(5)] IReadOnlyList<int> amounts
            )
            {
                if (relicTurnInPromptScopeDepth <= 0 ||
                    items == null || items.Count == 0)
                {
                    return;
                }

                for (int index = 0; index < items.Count; index++)
                {
                    if (!(items[index] is CollectableItemRelicType))
                    {
                        return;
                    }
                }

                int totalRelics = 0;
                if (amounts != null)
                {
                    for (int index = 0; index < amounts.Count; index++)
                    {
                        totalRelics += Math.Max(0, amounts[index]);
                    }
                }

                text = AlphabetModeManager.FilterDirectText(
                    totalRelics == 1
                        ? "Turn in this relic as an Archipelago check?"
                        : "Turn in these relics as Archipelago checks?"
                );
            }
        }
    }
}
