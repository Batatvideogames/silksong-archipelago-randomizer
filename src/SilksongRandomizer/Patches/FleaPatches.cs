using System;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using PlayerDataBoolTestAction =
    HutongGames.PlayMaker.Actions.PlayerDataBoolTest;
using PlayerDataVariableTestAction =
    HutongGames.PlayMaker.Actions.PlayerDataVariableTest;
using SetPlayerDataBoolAction =
    HutongGames.PlayMaker.Actions.SetPlayerDataBool;
using SetPlayerDataVariableAction =
    HutongGames.PlayMaker.Actions.SetPlayerDataVariable;

namespace SilksongRandomizer.Patches
{
    public class FleaPatches
    {
        private const string SavedFleaFieldTemplate = "SavedFlea_";
        private const int OrdinaryFleaCount = 27;
        private const int TotalFleaCount = 30;
        private const float GoalCheckIntervalSeconds = 0.25f;
        private static float nextGoalCheckTime;

        internal const string KrattItemName = "Flea: Greymoor - Kratt";
        internal const string VogItemName =
            "Flea: Putrified Ducts - Vog";
        internal const string HugeFleaItemName =
            "Flea: Memorium - Huge Flea";

        internal const string KrattLocationName = KrattItemName;
        internal const string VogLocationName = VogItemName;
        internal const string HugeFleaLocationName = HugeFleaItemName;

        private static readonly FieldInfo SavedFleaActivatorTemplateField =
            AccessTools.Field(typeof(SavedFleaActivator), "pdBoolTemplate");

        private static readonly FieldInfo SavedFleaActivatorParentsField =
            AccessTools.Field(typeof(SavedFleaActivator), "fleaParents");

        private static readonly MethodInfo ActivateFleasMethod =
            AccessTools.Method(
                typeof(SavedFleaActivator),
                "ActivateFleas",
                new[]
                {
                    typeof(Transform),
                    typeof(int),
                    typeof(int).MakeByRefType(),
                }
            );

        internal static int GetReceivedFleaCount()
        {
            SaveState state = SaveState.Instance;

            return state == null ? 0 : state.GetReceivedFleaCount();
        }

        internal static bool IsNamedNpcFleaLocation(string locationName)
        {
            return string.Equals(
                       locationName,
                       KrattLocationName,
                       StringComparison.OrdinalIgnoreCase
                   ) ||
                   string.Equals(
                       locationName,
                       VogLocationName,
                       StringComparison.OrdinalIgnoreCase
                   ) ||
                   string.Equals(
                       locationName,
                       HugeFleaLocationName,
                       StringComparison.OrdinalIgnoreCase
                   );
        }

        internal static void Update()
        {
            if (Time.unscaledTime < nextGoalCheckTime)
            {
                return;
            }

            nextGoalCheckTime = Time.unscaledTime + GoalCheckIntervalSeconds;
            SaveState.Instance?.TryCompleteFleaHuntGoal();
        }

        internal static int GetReceivedOrdinaryFleaCount()
        {
            SaveState state = SaveState.Instance;
            if (state == null)
            {
                return 0;
            }

            int namedNpcFleas = 0;
            if (HasReceivedItem(state, KrattItemName))
            {
                namedNpcFleas++;
            }
            if (HasReceivedItem(state, VogItemName))
            {
                namedNpcFleas++;
            }
            if (HasReceivedItem(state, HugeFleaItemName))
            {
                namedNpcFleas++;
            }

            return Math.Max(
                0,
                Math.Min(
                    OrdinaryFleaCount,
                    GetReceivedFleaCount() - namedNpcFleas
                )
            );
        }

        private static bool HasReceivedItem(
            SaveState state,
            string itemName
        )
        {
            return state?.receivedItems != null &&
                   state.receivedItems.Contains(
                       ItemSet.GetCanonicalItemName(itemName)
                   );
        }

        private static bool IsExactAction(
            FsmStateAction action,
            string sceneName,
            string objectName,
            string fsmName,
            string stateName
        )
        {
            return action != null &&
                   action.Owner != null &&
                   string.Equals(
                       action.Owner.scene.name,
                       sceneName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       action.Owner.name,
                       objectName,
                       StringComparison.Ordinal
                   ) &&
                   action.Fsm != null &&
                   string.Equals(
                       action.Fsm.Name,
                       fsmName,
                       StringComparison.Ordinal
                   ) &&
                   action.State != null &&
                   string.Equals(
                       action.State.Name,
                       stateName,
                       StringComparison.Ordinal
                   );
        }

        private static bool CanUseNpcFleaLocation(string locationName)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsLocationEnabled(locationName) &&
                   state.IsLocationInSeed(locationName);
        }

        private static bool ShouldExposeReceivedNpcFleaSource(
            string itemName,
            string locationName
        )
        {
            SaveState state = SaveState.Instance;
            return CanUseNpcFleaLocation(locationName) &&
                   !state.IsLocationChecked(locationName) &&
                   HasReceivedItem(state, itemName);
        }

        private static void ReportNpcFleaSource(string locationName)
        {
            SaveState state = SaveState.Instance;
            if (!CanUseNpcFleaLocation(locationName) ||
                state.IsLocationChecked(locationName))
            {
                return;
            }

            state.CheckLocation(locationName);
            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Reported named NPC Flea source: " +
                locationName
            );
        }

        [HarmonyPatch(typeof(PlayerData), "get_SavedFleasCount")]
        private static class PlayerData_SavedFleasCount_Patch
        {
            private static bool Prefix(ref int __result)
            {
                if (SaveState.Instance == null ||
                    !SaveState.Instance.IsRandomized(ItemType.Flea))
                {
                    return true;
                }

                __result = GetReceivedFleaCount();
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerData), "get_IsFleaPinMapKeyVisible")]
        private static class PlayerData_IsFleaPinMapKeyVisible_Patch
        {
            private static bool Prefix(PlayerData __instance, ref bool __result)
            {
                if (SaveState.Instance == null ||
                    !SaveState.Instance.IsRandomized(ItemType.Flea))
                {
                    return true;
                }

                if (!__instance.HasAnyFleaPin)
                {
                    __result = false;
                    return false;
                }

                __result = GetReceivedFleaCount() < TotalFleaCount;
                return false;
            }
        }

        [HarmonyPatch(typeof(QuestTargetPlayerDataBools), "GetCounts")]
        private static class QuestTargetPlayerDataBools_GetCounts_Patch
        {
            private static bool Prefix(
                string ___pdFieldTemplate,
                ref int completed,
                ref int total
            )
            {
                if (SaveState.Instance == null ||
                    !SaveState.Instance.IsRandomized(ItemType.Flea) ||
                    !string.Equals(
                        ___pdFieldTemplate,
                        SavedFleaFieldTemplate,
                        StringComparison.Ordinal
                    ))
                {
                    return true;
                }

                // FleasCollected Target drives the caravan milestones and the
                // all-Fleas quest. All 30 are AP Flea items, including the
                // three named NPC recruits, so native source flags must not
                // count independently and double-credit the same Flea.
                completed = GetReceivedFleaCount();
                total = TotalFleaCount;
                return false;
            }
        }

        [HarmonyPatch(
            typeof(PlayerDataBoolTestAction),
            nameof(PlayerDataBoolTestAction.OnEnter)
        )]
        private static class NamedNpcFleaBoolSourceVisibilityPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerDataBoolTestAction __instance)
            {
                string itemName = null;
                string locationName = null;
                string flagName = __instance?.boolName?.Value;

                if (string.Equals(
                        flagName,
                        "CaravanLechSaved",
                        StringComparison.Ordinal
                    ) &&
                    (IsExactAction(
                         __instance,
                         "Greymoor_24",
                         "Caravan Lech Strung Up",
                         "Control",
                         "Init"
                     ) ||
                     IsExactAction(
                         __instance,
                         "Greymoor_24",
                         "Caravan Lech",
                         "Custom Dialogue - Act3 Rescue",
                         "Convo Check"
                     )))
                {
                    itemName = KrattItemName;
                    locationName = KrattLocationName;
                }
                else if (string.Equals(
                             flagName,
                             "tamedGiantFlea",
                             StringComparison.Ordinal
                         ) &&
                         IsExactAction(
                             __instance,
                             "Arborium_08",
                             "Giant Flea Cage",
                             "Flea Control",
                             "Idle"
                         ))
                {
                    itemName = HugeFleaItemName;
                    locationName = HugeFleaLocationName;
                }

                if (itemName == null ||
                    !ShouldExposeReceivedNpcFleaSource(
                        itemName,
                        locationName
                    ))
                {
                    return true;
                }

                __instance.Fsm.Event(__instance.isFalse);
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(
            typeof(PlayerDataVariableTestAction),
            nameof(PlayerDataVariableTestAction.OnEnter)
        )]
        private static class VogSourceVisibilityPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                PlayerDataVariableTestAction __instance
            )
            {
                object expectedValue =
                    __instance?.ExpectedValue?.GetValue();
                if (!(expectedValue is bool expectsMet) ||
                    !expectsMet ||
                    !string.Equals(
                        __instance.VariableName?.Value,
                        "MetTroupeHunterWild",
                        StringComparison.Ordinal
                    ) ||
                    !(IsExactAction(
                          __instance,
                          "Bellway_Aqueduct",
                          "Caravan Troupe Hunter Wild",
                          "Dialogue",
                          "Met?"
                      ) ||
                      IsExactAction(
                          __instance,
                          "Bellway_Aqueduct",
                          "Caravan Troupe Hunter Wild",
                          "Dialogue",
                          "Met? N"
                      )) ||
                    !ShouldExposeReceivedNpcFleaSource(
                        VogItemName,
                        VogLocationName
                    ))
                {
                    return true;
                }

                __instance.Fsm.Event(__instance.IsNotExpectedEvent);
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(
            typeof(SetPlayerDataBoolAction),
            nameof(SetPlayerDataBoolAction.OnEnter)
        )]
        private static class NamedNpcFleaBoolSourceCompletionPatch
        {
            [HarmonyPostfix]
            private static void Postfix(SetPlayerDataBoolAction __instance)
            {
                if (__instance?.value == null ||
                    !__instance.value.Value ||
                    __instance.boolName == null ||
                    PlayerData.instance == null)
                {
                    return;
                }

                string flagName = __instance.boolName.Value;
                if (string.Equals(
                        flagName,
                        "CaravanLechSaved",
                        StringComparison.Ordinal
                    ) &&
                    PlayerData.instance.CaravanLechSaved &&
                    (IsExactAction(
                         __instance,
                         "Greymoor_24",
                         "Caravan Lech Strung Up",
                         "Control",
                         "Break"
                     ) ||
                     IsExactAction(
                         __instance,
                         "Greymoor_24",
                         "Caravan Lech",
                         "Custom Dialogue - Act3 Rescue",
                         "Meet"
                     )))
                {
                    ReportNpcFleaSource(KrattLocationName);
                }
                else if (string.Equals(
                             flagName,
                             "tamedGiantFlea",
                             StringComparison.Ordinal
                         ) &&
                         PlayerData.instance.tamedGiantFlea &&
                         IsExactAction(
                             __instance,
                             "Arborium_08",
                             "Giant Flea",
                             "Control",
                             "Stun"
                         ))
                {
                    ReportNpcFleaSource(HugeFleaLocationName);
                }
            }
        }

        [HarmonyPatch(
            typeof(SetPlayerDataVariableAction),
            nameof(SetPlayerDataVariableAction.OnEnter)
        )]
        private static class VogSourceCompletionPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                SetPlayerDataVariableAction __instance
            )
            {
                if (__instance == null ||
                    !string.Equals(
                        __instance.VariableName?.Value,
                        "MetTroupeHunterWild",
                        StringComparison.Ordinal
                    ) ||
                    !(IsExactAction(
                          __instance,
                          "Bellway_Aqueduct",
                          "Caravan Troupe Hunter Wild",
                          "Dialogue",
                          "Meet"
                      ) ||
                      IsExactAction(
                          __instance,
                          "Bellway_Aqueduct",
                          "Caravan Troupe Hunter Wild",
                          "Dialogue",
                          "Meet N"
                      )) ||
                    PlayerData.instance == null ||
                    !PlayerData.instance.MetTroupeHunterWild)
                {
                    return;
                }

                ReportNpcFleaSource(VogLocationName);
            }
        }

        [HarmonyPatch(typeof(SavedFleaActivator), "Start")]
        private static class SavedFleaActivator_Start_Patch
        {
            private static bool Prefix(SavedFleaActivator __instance)
            {
                if (SaveState.Instance == null ||
                    !SaveState.Instance.IsRandomized(ItemType.Flea) ||
                    SavedFleaActivatorTemplateField == null ||
                    SavedFleaActivatorParentsField == null ||
                    ActivateFleasMethod == null)
                {
                    return true;
                }

                string template =
                    SavedFleaActivatorTemplateField.GetValue(__instance) as string;

                if (!string.Equals(
                    template,
                    SavedFleaFieldTemplate,
                    StringComparison.Ordinal
                ))
                {
                    return true;
                }

                Transform[] fleaParents =
                    SavedFleaActivatorParentsField.GetValue(__instance)
                        as Transform[];

                if (fleaParents == null)
                {
                    return true;
                }

                // The named recruits have their own caravan actors. Only the
                // ordinary AP Fleas should populate SavedFleaActivator's
                // anonymous Flea display roots.
                int remaining = GetReceivedOrdinaryFleaCount();

                foreach (Transform fleaParent in fleaParents)
                {
                    object[] arguments =
                    {
                        fleaParent,
                        remaining,
                        remaining,
                    };

                    ActivateFleasMethod.Invoke(null, arguments);
                    remaining = (int)arguments[2];
                }

                return false;
            }
        }
    }
}
