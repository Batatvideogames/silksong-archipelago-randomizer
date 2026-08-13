using HarmonyLib;
using HutongGames.PlayMaker;
using GlobalEnums;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using PlayerDataBoolTestAction =
    HutongGames.PlayMaker.Actions.PlayerDataBoolTest;
using SetPlayerDataBoolAction =
    HutongGames.PlayMaker.Actions.SetPlayerDataBool;

namespace SilksongRandomizer.Patches
{
    internal static class TravelPatches
    {
        private const string BellBeastObjectName = "Bone Beast NPC";
        private const string BellBeastFsmName = "Interaction";
        private const string BellBeastArrivalEndState = "Travel Arrive End";
        private const string TollMachineObjectName = "Bellway Toll Machine";
        private const string TollMachineFsmName = "Unlock Behaviour";
        private const string TollMachineUnpaidState = "Inert";
        private const string TollMachinePlayerDataVariable =
            "Pickup PlayerData Bool";
        private const string SkipCutsceneVariable = "SkipCutscene";

        private static readonly HashSet<string> BellwayFields =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "UnlockedDocksStation",
                "UnlockedBoneforestEastStation",
                "UnlockedGreymoorStation",
                "UnlockedBelltownStation",
                "UnlockedCoralTowerStation",
                "UnlockedCityStation",
                "UnlockedPeakStation",
                "UnlockedShellwoodStation",
                "UnlockedShadowStation",
                "UnlockedAqueductStation",
            };

        private static readonly Dictionary<string, string>
            BellwayLocationsByField =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "UnlockedDocksStation", "Bellway: Deep Docks" },
                    {
                        "UnlockedBoneforestEastStation",
                        "Bellway: Far Fields"
                    },
                    { "UnlockedGreymoorStation", "Bellway: Greymoor" },
                    { "UnlockedBelltownStation", "Bellway: Bellhart" },
                    {
                        "UnlockedCoralTowerStation",
                        "Bellway: Blasted Steps"
                    },
                    { "UnlockedCityStation", "Bellway: Grand Bellway" },
                    { "UnlockedPeakStation", "Bellway: The Slab" },
                    { "UnlockedShellwoodStation", "Bellway: Shellwood" },
                    { "UnlockedShadowStation", "Bellway: Bilewater" },
                    {
                        "UnlockedAqueductStation",
                        "Bellway: Putrified Ducts"
                    },
                };

        private static readonly MethodInfo HideInteractionMethod =
            AccessTools.Method(typeof(InteractableBase), "HideInteraction");
        private static readonly FieldInfo IsShowingInteractionField =
            AccessTools.Field(
                typeof(InteractableBase),
                "isShowingInteraction"
            );
        private static readonly FieldInfo InteractionPriorityField =
            AccessTools.Field(typeof(InteractableBase), "priority");
        private static readonly Dictionary<int, InteractPriority>
            BoostedTollPriorities =
                new Dictionary<int, InteractPriority>();

        private static readonly HashSet<string> VentricaFields =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "UnlockedSongTube",
                "UnlockedUnderTube",
                "UnlockedCityBellwayTube",
                "UnlockedHangTube",
                "UnlockedEnclaveTube",
                "UnlockedArboriumTube",
            };

        private static bool TryGetRandomizedUnlock(
            string playerDataField,
            out ItemType type,
            out bool isUnlocked
        )
        {
            isUnlocked = false;
            if (BellwayFields.Contains(playerDataField))
            {
                type = ItemType.Bellway;
            }
            else if (VentricaFields.Contains(playerDataField))
            {
                type = ItemType.Ventrica;
            }
            else
            {
                type = ItemType.Unknown;
                return false;
            }

            SaveState state = SaveState.Instance;
            if (state == null || !state.IsRandomized(type))
            {
                return false;
            }

            FieldInfo randomizerField = state.GetType().GetField(
                playerDataField,
                BindingFlags.Instance | BindingFlags.Public
            );
            if (randomizerField == null ||
                randomizerField.FieldType != typeof(bool))
            {
                return false;
            }

            isUnlocked = (bool)randomizerField.GetValue(state);
            return true;
        }

        internal static bool ApplyBellwayAccessOption()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                playerData == null ||
                !state.AllowsBellwaysBeforeBellBeast)
            {
                return false;
            }

            // This is the network permission normally set by the travel NPC,
            // not the Bell Beast boss/story flag. The physical Bone_05 fight,
            // its Silkspear roadblock and its AP check remain untouched.
            playerData.UnlockedFastTravel = true;
            return true;
        }

        private static bool IsBellBeastArrivalEndAction(
            FsmStateAction action
        )
        {
            return action != null &&
                   action.Owner != null &&
                   string.Equals(
                       action.Owner.name,
                       BellBeastObjectName,
                       StringComparison.Ordinal
                   ) &&
                   action.Fsm != null &&
                   string.Equals(
                       action.Fsm.Name,
                       BellBeastFsmName,
                       StringComparison.Ordinal
                   ) &&
                   action.State != null &&
                   string.Equals(
                       action.State.Name,
                       BellBeastArrivalEndState,
                       StringComparison.Ordinal
                   );
        }

        internal static bool RearmUnpaidBellwayTollInteraction()
        {
            SaveState state = SaveState.Instance;
            GameManager gameManager = GameManager.instance;
            if (state == null ||
                gameManager == null ||
                !state.IsRandomized(ItemType.Bellway))
            {
                return false;
            }

            string currentScene = GameManager.GetBaseSceneName(
                gameManager.sceneName ?? string.Empty
            );
            if (string.IsNullOrEmpty(currentScene))
            {
                return false;
            }

            bool repairedAny = false;
            foreach (PlayMakerFSM tollFsm in
                     Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (tollFsm == null ||
                    tollFsm.gameObject == null ||
                    !tollFsm.gameObject.activeInHierarchy ||
                    !tollFsm.gameObject.name.StartsWith(
                        TollMachineObjectName,
                        StringComparison.Ordinal
                    ) ||
                    !string.Equals(
                        tollFsm.FsmName,
                        TollMachineFsmName,
                        StringComparison.Ordinal
                    ) ||
                    tollFsm.Fsm == null ||
                    !string.Equals(
                        tollFsm.Fsm.ActiveStateName,
                        TollMachineUnpaidState,
                        StringComparison.Ordinal
                    ) ||
                    !string.Equals(
                        GameManager.GetBaseSceneName(
                            tollFsm.gameObject.scene.name
                        ),
                        currentScene,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    continue;
                }

                FsmString playerDataVariable =
                    tollFsm.FsmVariables?.GetFsmString(
                        TollMachinePlayerDataVariable
                    );
                string playerDataField =
                    playerDataVariable?.Value ?? string.Empty;
                if (!BellwayLocationsByField.TryGetValue(
                        playerDataField,
                        out string locationName
                    ) ||
                    !state.IsLocationInSeed(locationName) ||
                    state.IsLocationChecked(locationName))
                {
                    continue;
                }

                InteractableBase interactable =
                    tollFsm.GetComponent<InteractableBase>();
                string error = null;
                if (interactable == null ||
                    !TryPrioritizeUnpaidTollInteraction(
                        interactable,
                        out error
                    ))
                {
                    Debug.LogWarning(
                        "[RANDOMIZER] Could not re-arm unpaid " +
                        locationName + " toll interaction: " +
                        (error ?? "InteractableBase was not found.")
                    );
                    continue;
                }

                repairedAny = true;
                Debug.Log(
                    "[RANDOMIZER] Prioritized unpaid " + locationName +
                    " toll interaction after Bell Beast arrival."
                );
            }

            return repairedAny;
        }

        private static bool TryPrioritizeUnpaidTollInteraction(
            InteractableBase interactable,
            out string error
        )
        {
            error = null;
            if (HideInteractionMethod == null ||
                IsShowingInteractionField == null ||
                InteractionPriorityField == null)
            {
                error = "InteractableBase interaction members were not found.";
                return false;
            }

            try
            {
                // Scene activation can clear InteractManager's registry after
                // the toll has already recorded Hornet inside its trigger.
                // Only the stale registration is removed while that overlap
                // remains available to Activate for the purchase prompt.
                HideInteractionMethod.Invoke(interactable, null);
                IsShowingInteractionField.SetValue(interactable, false);

                int instanceId = interactable.GetInstanceID();
                if (!BoostedTollPriorities.ContainsKey(instanceId))
                {
                    BoostedTollPriorities[instanceId] =
                        (InteractPriority)InteractionPriorityField.GetValue(
                            interactable
                        );
                }

                // The unpaid toll and Bell Beast Travel prompts overlap and
                // are both Regular in vanilla. Give the toll one selection at
                // HighPriority, then restore it as soon as that input is used.
                InteractionPriorityField.SetValue(
                    interactable,
                    InteractPriority.HighPriority
                );
                interactable.Activate();
                return true;
            }
            catch (TargetInvocationException exception)
            {
                RestoreTollInteractionPriority(interactable);
                Exception cause = exception.InnerException ?? exception;
                error = cause.GetType().Name + ": " + cause.Message;
                return false;
            }
            catch (Exception exception)
            {
                RestoreTollInteractionPriority(interactable);
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private static void RestoreTollInteractionPriority(
            InteractableBase interactable
        )
        {
            if (interactable == null || InteractionPriorityField == null)
            {
                return;
            }

            int instanceId = interactable.GetInstanceID();
            if (!BoostedTollPriorities.TryGetValue(
                    instanceId,
                    out InteractPriority originalPriority
                ))
            {
                return;
            }

            BoostedTollPriorities.Remove(instanceId);
            InteractionPriorityField.SetValue(
                interactable,
                originalPriority
            );
        }

        private static bool TryGetBellBeastApUnlock(
            FsmStateAction action,
            string playerDataField,
            out bool isUnlocked
        )
        {
            isUnlocked = false;
            if (action == null ||
                action.Owner == null ||
                !string.Equals(
                    action.Owner.name,
                    BellBeastObjectName,
                    StringComparison.Ordinal
                ) ||
                action.Fsm == null ||
                !string.Equals(
                    action.Fsm.Name,
                    BellBeastFsmName,
                    StringComparison.Ordinal
                ) ||
                action.State == null ||
                (
                    !string.Equals(
                        action.State.Name,
                        "Can Appear",
                        StringComparison.Ordinal
                    ) &&
                    !string.Equals(
                        action.State.Name,
                        "Can Appear 2",
                        StringComparison.Ordinal
                    )
                ) ||
                !TryGetRandomizedUnlock(
                    playerDataField,
                    out ItemType type,
                    out isUnlocked
                ) ||
                type != ItemType.Bellway)
            {
                isUnlocked = false;
                return false;
            }

            PlayerData playerData = PlayerData.instance;
            isUnlocked = ShouldAllowBellBeastCall(
                isUnlocked,
                playerData != null && playerData.GetBool(playerDataField)
            );
            return true;
        }

        internal static bool ShouldAllowBellBeastCall(
            bool apStationUnlocked,
            bool physicalStationPurchased
        )
        {
            return apStationUnlocked || physicalStationPurchased;
        }

        internal static bool ShouldUnlockRandomizedDestination(
            ItemType type,
            bool apStationUnlocked,
            bool physicalStationPurchased
        )
        {
            // A paid Bellway remains its native destination as well as an AP
            // check. Ventrica unlocks have no equivalent paid-origin rule and
            // continue to follow only their received AP station item.
            return type == ItemType.Bellway
                ? ShouldAllowBellBeastCall(
                    apStationUnlocked,
                    physicalStationPurchased
                )
                : apStationUnlocked;
        }

        private static IEnumerator SkipStagTravelVideo(
            IEnumerator nativeRoutine
        )
        {
            bool armedSkip = false;
            try
            {
                while (true)
                {
                    GameManager gameManager = GameManager.instance;
                    if (!armedSkip &&
                        gameManager != null &&
                        !gameManager.IsInSceneTransition)
                    {
                        // FastTravelCutscene consumes this flag through its
                        // own startup-skip branch, preserving destination load,
                        // readiness, input cleanup and the arrival sequence.
                        StaticVariableList.SetValue(
                            SkipCutsceneVariable,
                            true
                        );
                        armedSkip = true;
                    }

                    if (!nativeRoutine.MoveNext())
                    {
                        yield break;
                    }

                    yield return nativeRoutine.Current;
                }
            }
            finally
            {
                if (armedSkip &&
                    StaticVariableList.GetValue(
                        SkipCutsceneVariable,
                        false
                    ))
                {
                    // An interrupted travel coroutine cannot skip a later
                    // unrelated Bellway cinematic.
                    StaticVariableList.SetValue(
                        SkipCutsceneVariable,
                        false
                    );
                }

                (nativeRoutine as IDisposable)?.Dispose();
            }
        }

        private static bool IsRandomizerStagTravel(
            FastTravelCutscene cutscene
        )
        {
            SaveState state = SaveState.Instance;
            return cutscene is StagTravel &&
                   state != null &&
                   state.IsRoomBound;
        }

        [HarmonyPatch(typeof(FastTravelCutscene), "Start")]
        private static class StagTravelVideoSkipPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                FastTravelCutscene __instance,
                ref IEnumerator __result
            )
            {
                if (__result != null &&
                    IsRandomizerStagTravel(__instance))
                {
                    __result = SkipStagTravelVideo(__result);
                }
            }
        }

        [HarmonyPatch(
            typeof(InteractableBase),
            nameof(InteractableBase.QueueInteraction)
        )]
        private static class TollInteractionPriorityRestorePatch
        {
            [HarmonyPrefix]
            private static void Prefix(InteractableBase __instance)
            {
                RestoreTollInteractionPriority(__instance);
            }
        }

        [HarmonyPatch]
        private static class FastTravelMapButtonBase_IsUnlocked_Patch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                Type genericBaseType = typeof(FastTravelMapButtonBase<>);

                return AccessTools.AllTypes()
                    .Select(FindFastTravelMapButtonBase)
                    .Where(type =>
                        type != null &&
                        type.IsGenericType &&
                        type.GetGenericTypeDefinition() == genericBaseType &&
                        !type.ContainsGenericParameters)
                    .Distinct()
                    .Select(type => AccessTools.Method(type, "IsUnlocked"))
                    .Where(method => method != null);
            }

            private static Type FindFastTravelMapButtonBase(Type type)
            {
                while (type != null && type != typeof(object))
                {
                    if (type.IsGenericType &&
                        type.GetGenericTypeDefinition() ==
                        typeof(FastTravelMapButtonBase<>))
                    {
                        return type;
                    }

                    type = type.BaseType;
                }

                return null;
            }

            private static bool Prefix(
                string ___playerDataBool,
                ref bool __result
            )
            {
                if (string.IsNullOrEmpty(___playerDataBool) ||
                    !TryGetRandomizedUnlock(
                        ___playerDataBool,
                        out ItemType type,
                        out bool isUnlocked
                    ))
                {
                    return true;
                }

                PlayerData playerData = PlayerData.instance;
                bool physicalStationPurchased =
                    type == ItemType.Bellway &&
                    BellwayFields.Contains(___playerDataBool) &&
                    playerData != null &&
                    playerData.GetBool(___playerDataBool);
                __result = ShouldUnlockRandomizedDestination(
                    type,
                    isUnlocked,
                    physicalStationPurchased
                );
                return false;
            }
        }

        [HarmonyPatch(
            typeof(FastTravelMapButtonBase<FastTravelLocations>),
            "IsUnlocked"
        )]
        private static class MarrowFastTravelUnlockPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                FastTravelLocations ___targetLocation,
                ref bool __result
            )
            {
                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (!__result ||
                    state == null ||
                    playerData == null ||
                    !state.AllowsBellwaysBeforeBellBeast)
                {
                    return;
                }

                // Randomized-station access opens the network before the Bell
                // Beast but The Marrow is the boss's own arrival endpoint. Only
                // that endpoint stays hidden until the physical fight is complete.
                // Bone Bottom remains available.
                if (___targetLocation == FastTravelLocations.Bone &&
                    !playerData.defeatedBellBeast)
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(
            typeof(PlayerDataBoolTestAction),
            nameof(PlayerDataBoolTestAction.OnEnter)
        )]
        private static class BellBeastPlayerDataBoolTestPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                PlayerDataBoolTestAction __instance
            )
            {
                string playerDataField =
                    __instance.boolName?.Value ?? string.Empty;
                if (!TryGetBellBeastApUnlock(
                        __instance,
                        playerDataField,
                        out bool isUnlocked
                    ))
                {
                    return true;
                }

                __instance.Fsm.Event(
                    isUnlocked ? __instance.isTrue : __instance.isFalse
                );
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(
            typeof(SetPlayerDataBoolAction),
            nameof(SetPlayerDataBoolAction.OnEnter)
        )]
        private static class BellBeastArrivalTollInteractionPatch
        {
            [HarmonyPostfix]
            private static void Postfix(SetPlayerDataBoolAction __instance)
            {
                if (IsBellBeastArrivalEndAction(__instance))
                {
                    RearmUnpaidBellwayTollInteraction();
                }
            }
        }

    }
}
