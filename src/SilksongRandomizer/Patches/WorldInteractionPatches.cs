using System;
using System.Collections.Generic;
using System.Reflection;
using GlobalEnums;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class WorldInteractionPatches
    {
        private const float MenuFadeSpeed = 13f;

        private static readonly HashSet<string> BellwayScenes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Bellway_01",
                "Bellway_02",
                "Bellway_03",
                "Bellway_04",
                "Belltown_basement",
                "Bellway_08",
                "Bellway_City",
                "Slab_06",
                "Shellwood_19",
                "Bone_05",
                "Bellway_Shadow",
                "Bellway_Aqueduct",
            };

        private static readonly HashSet<string> VentricaScenes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Tube_Hub",
                "Song_01b",
                "Under_22",
                "Bellway_City",
                "Hang_06b",
                "Song_Enclave_Tube",
                "Arborium_Tube",
            };

        private static readonly HashSet<string> FsmLiftScenes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Bonetown",
                "Belltown_06",
                "Room_Forge",
                "Dock_01",
            };

        private static readonly FieldInfo TrapBridgeOpenDelay =
            AccessTools.Field(typeof(TrapBridge), "openDelay");
        private static readonly FieldInfo TrapBridgeAnticDelay =
            AccessTools.Field(typeof(TrapBridge), "anticDelay");

        internal static bool IsActive
        {
            get
            {
                SaveState state = SaveState.Instance;
                return state != null && state.IsRoomBound;
            }
        }

        private static string GetBaseSceneName(GameObject gameObject)
        {
            return gameObject == null
                ? string.Empty
                : GameManager.GetBaseSceneName(
                    gameObject.scene.name ?? string.Empty
                );
        }

        private static bool IsBellwayFsm(PlayMakerFSM fsm)
        {
            return fsm != null &&
                   string.Equals(
                       fsm.name,
                       "Bone Beast NPC",
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       fsm.FsmName,
                       "Interaction",
                       StringComparison.Ordinal
                   ) &&
                   BellwayScenes.Contains(GetBaseSceneName(fsm.gameObject));
        }

        private static FsmTransition FindTransition(
            FsmState state,
            string eventName
        )
        {
            if (state?.Transitions == null)
            {
                return null;
            }

            foreach (FsmTransition transition in state.Transitions)
            {
                if (transition != null &&
                    string.Equals(
                        transition.EventName,
                        eventName,
                        StringComparison.Ordinal
                    ))
                {
                    return transition;
                }
            }

            return null;
        }

        private static T GetAction<T>(FsmState state, int index)
            where T : FsmStateAction
        {
            return state?.Actions != null &&
                   index >= 0 &&
                   index < state.Actions.Length
                ? state.Actions[index] as T
                : null;
        }

        private static T FindOnlyAction<T>(FsmState state)
            where T : FsmStateAction
        {
            T match = null;
            if (state?.Actions == null)
            {
                return null;
            }

            foreach (FsmStateAction action in state.Actions)
            {
                if (!(action is T typedAction))
                {
                    continue;
                }

                if (match != null)
                {
                    return null;
                }

                match = typedAction;
            }

            return match;
        }

        private static void SpeedBellBeast(PlayMakerFSM fsm)
        {
            if (!IsBellwayFsm(fsm))
            {
                return;
            }

            FsmState startState = fsm.Fsm?.GetState("Start State");
            FsmState wakeUp = fsm.Fsm?.GetState("Wake Up");
            FsmTransition sleep = FindTransition(startState, "SLEEP");
            if (sleep != null && wakeUp != null)
            {
                sleep.ToState = wakeUp.Name;
                sleep.ToFsmState = wakeUp;
            }

            Wait arrivalWait = GetAction<Wait>(
                fsm.Fsm?.GetState("Travel Arrive Start"),
                7
            );
            if (arrivalWait?.time != null)
            {
                arrivalWait.time.Value = 0f;
            }

            ScreenFader departureFade = GetAction<ScreenFader>(
                fsm.Fsm?.GetState("Hero Jump"),
                0
            );
            if (departureFade?.duration != null)
            {
                departureFade.duration.Value = 0.25f;
            }

            FsmState firstEnter = fsm.Fsm?.GetState("First Enter?");
            FsmState idle = fsm.Fsm?.GetState("Idle");
            FsmTransition firstEnterFinished =
                FindTransition(firstEnter, "FINISHED");
            if (firstEnterFinished != null && idle != null)
            {
                firstEnterFinished.ToState = idle.Name;
                firstEnterFinished.ToFsmState = idle;
            }

            Wait appearDelay = GetAction<Wait>(
                fsm.Fsm?.GetState("Appear Delay"),
                5
            );
            if (appearDelay?.time != null)
            {
                appearDelay.time.Value = 0f;
            }
        }

        private static void SpeedFsmLift(PlayMakerFSM fsm)
        {
            if (fsm == null ||
                !string.Equals(
                    fsm.FsmName,
                    "Lift Control",
                    StringComparison.Ordinal
                ) ||
                !FsmLiftScenes.Contains(GetBaseSceneName(fsm.gameObject)))
            {
                return;
            }

            FsmFloat speed = fsm.FsmVariables?.FindFsmFloat("Speed");
            SetFloatValue initialSpeed = FindOnlyAction<SetFloatValue>(
                fsm.Fsm?.GetState("Init")
            );
            if (speed == null ||
                initialSpeed?.floatVariable == null ||
                initialSpeed.floatValue == null ||
                !string.Equals(
                    initialSpeed.floatVariable.Name,
                    "Speed",
                    StringComparison.Ordinal
                ))
            {
                return;
            }

            speed.Value = 19f;
            initialSpeed.floatValue.Value = 31.4f;
        }

        private static void SpeedStationPurchase(PlayMakerFSM fsm)
        {
            if (fsm == null ||
                !string.Equals(
                    fsm.FsmName,
                    "Unlock Behaviour",
                    StringComparison.Ordinal
                ))
            {
                return;
            }

            string sceneName = GetBaseSceneName(fsm.gameObject);
            bool isBellway = BellwayScenes.Contains(sceneName) &&
                fsm.name.StartsWith(
                    "Bellway Toll Machine",
                    StringComparison.Ordinal
                );
            bool isVentrica = VentricaScenes.Contains(sceneName) &&
                string.Equals(
                    fsm.name,
                    "tube_toll_machine",
                    StringComparison.Ordinal
                );
            if (!isBellway && !isVentrica)
            {
                return;
            }

            Animator animator = fsm.GetComponent<Animator>();
            if (animator != null)
            {
                animator.speed = isBellway ? 10f : 20f;
            }

            if (isVentrica)
            {
                Wait pause = FindOnlyAction<Wait>(
                    fsm.Fsm?.GetState("After Retract Pause")
                );
                if (pause?.time != null)
                {
                    pause.time.Value = 0f;
                }
            }
        }

        private static void SpeedVentrica(PlayMakerFSM fsm)
        {
            if (fsm == null ||
                !string.Equals(
                    fsm.name,
                    "City Travel Tube",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    fsm.FsmName,
                    "Tube Travel",
                    StringComparison.Ordinal
                ) ||
                !VentricaScenes.Contains(GetBaseSceneName(fsm.gameObject)))
            {
                return;
            }

            Animator animator = fsm.GetComponent<Animator>();
            if (animator != null)
            {
                animator.speed = 5f;
            }

            FsmState fadeOut = fsm.Fsm?.GetState("Fade Out");
            ScreenFader fader = GetAction<ScreenFader>(fadeOut, 2);
            Wait wait = GetAction<Wait>(fadeOut, 3);
            if (fader?.duration != null && wait?.time != null)
            {
                fader.duration.Value = 0.25f;
                wait.time.Value = 0.25f;
            }
        }

        private static void SkipBeastlingPerformance(PlayMakerFSM fsm)
        {
            if (fsm == null ||
                !string.Equals(
                    fsm.name,
                    "Hero_Hornet(Clone)",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    fsm.FsmName,
                    "Silk Specials",
                    StringComparison.Ordinal
                ))
            {
                return;
            }

            FsmState needolinSub = fsm.Fsm?.GetState("Needolin Sub");
            RunFSM runAction = FindOnlyAction<RunFSM>(needolinSub);
            Fsm nestedFsm = runAction == null
                ? null
                : Traverse.Create(runAction)
                    .Field("runFsm")
                    .GetValue<Fsm>();
            if (nestedFsm == null)
            {
                return;
            }

            BoolTestDelay callWait = GetAction<BoolTestDelay>(
                nestedFsm.GetState("Needolin FT Wait"),
                4
            );
            Wait availabilityWait = GetAction<Wait>(
                nestedFsm.GetState("Can Fast Travel?"),
                1
            );
            Wait performanceWait = GetAction<Wait>(
                nestedFsm.GetState("Needolin FT Antic"),
                5
            );
            if (callWait?.delay == null ||
                availabilityWait?.time == null ||
                performanceWait?.time == null)
            {
                return;
            }

            callWait.delay.Value = 0f;
            availabilityWait.time.Value = 0f;
            performanceWait.time.Value = 0f;

            FsmState childrenFade =
                fsm.Fsm?.GetState("Children Leave Fade");
            ScreenFader fader = GetAction<ScreenFader>(childrenFade, 6);
            Wait fadeWait = GetAction<Wait>(childrenFade, 7);
            if (fader?.duration != null && fadeWait?.time != null)
            {
                fader.duration.Value = 0.25f;
                fadeWait.time.Value = 0.25f;
            }
        }

        [HarmonyPatch(
            typeof(UIManager),
            nameof(UIManager.ShowMenu),
            new[] { typeof(MenuScreen) }
        )]
        private static class FastMenuPatch
        {
            [HarmonyPrefix]
            private static void Prefix(UIManager __instance)
            {
                if (IsActive && __instance != null)
                {
                    __instance.MENU_FADE_SPEED = MenuFadeSpeed;
                }
            }
        }

        [HarmonyPatch(typeof(LiftControl), "Start")]
        private static class LiftControlPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                LiftControl __instance,
                ref float ___moveSpeed,
                ref float ___moveDelay,
                ref float ___endDelay
            )
            {
                if (!IsActive || __instance == null ||
                    string.Equals(
                        GetBaseSceneName(__instance.gameObject),
                        "Ward_01",
                        StringComparison.Ordinal
                    ))
                {
                    return;
                }

                ___moveSpeed = string.Equals(
                    GetBaseSceneName(__instance.gameObject),
                    "Library_11",
                    StringComparison.Ordinal
                )
                    ? 28f
                    : 75f;
                ___moveDelay = 0f;
                ___endDelay = 0f;
            }
        }

        [HarmonyPatch(typeof(ManualLift), "Start")]
        private static class ManualLiftPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                ref float ___moveSpeed,
                ref float ___acceleration,
                ref float ___moveDelay
            )
            {
                if (!IsActive)
                {
                    return;
                }

                ___moveSpeed = 55f;
                ___acceleration = 12f;
                ___moveDelay = 0f;
            }
        }

        [HarmonyPatch(typeof(Lever), "Start")]
        private static class LeverPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ref float ___openGateDelay)
            {
                if (IsActive)
                {
                    ___openGateDelay = 0f;
                }
            }
        }

        [HarmonyPatch(typeof(Lever_tk2d), "Start")]
        private static class Tk2dLeverPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ref float ___openGateDelay)
            {
                if (IsActive)
                {
                    ___openGateDelay = 0f;
                }
            }
        }

        [HarmonyPatch(typeof(Trapdoor), "Awake")]
        private static class TrapdoorPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                ref float ___retractStartDelay,
                ref float ___retractEndDelay,
                ref float ___openWaitTime
            )
            {
                if (!IsActive)
                {
                    return;
                }

                ___retractStartDelay = 0f;
                ___retractEndDelay = 0f;
                ___openWaitTime = 0f;
            }
        }

        [HarmonyPatch(typeof(TrapBridgeExtend), "Awake")]
        private static class TrapBridgeExtendPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                TrapBridgeExtend __instance,
                Animator ___animator
            )
            {
                if (!IsActive || __instance == null)
                {
                    return;
                }

                if (___animator != null)
                {
                    ___animator.speed = 8f;
                }
                TrapBridgeOpenDelay?.SetValue(__instance, 0f);
                TrapBridgeAnticDelay?.SetValue(__instance, 0f);
            }
        }

        [HarmonyPatch(typeof(PressurePlateBase), "Awake")]
        private static class PressurePlatePatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                ref float ___gateOpenDelay,
                ref float ___dropTime,
                ref float ___waitTime
            )
            {
                if (!IsActive)
                {
                    return;
                }

                ___gateOpenDelay = 0f;
                ___dropTime = 0.05f;
                ___waitTime = 0.05f;
            }
        }

        [HarmonyPatch(typeof(DialDoorBridge), "Start")]
        private static class DialDoorBridgePatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                ref float ___doorOpenDelay,
                ref float ___moveDelay,
                ref float ___moveDuration
            )
            {
                if (!IsActive)
                {
                    return;
                }

                ___doorOpenDelay = 0f;
                ___moveDelay = 0f;
                ___moveDuration = 0f;
            }
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class PlayMakerWorldInteractionPatch
        {
            [HarmonyPostfix]
            private static void Postfix(PlayMakerFSM __instance)
            {
                if (!IsActive || __instance == null)
                {
                    return;
                }

                SpeedBellBeast(__instance);
                SpeedFsmLift(__instance);
                SpeedStationPurchase(__instance);
                SpeedVentrica(__instance);
                SkipBeastlingPerformance(__instance);
            }
        }

        [HarmonyPatch(typeof(EnumCompare), nameof(EnumCompare.OnEnter))]
        private static class BellBeastReadyPatch
        {
            [HarmonyPrefix]
            private static void Prefix(EnumCompare __instance)
            {
                if (!IsActive ||
                    __instance == null ||
                    __instance.Owner == null ||
                    !string.Equals(
                        __instance.Owner.name,
                        "Bone Beast NPC",
                        StringComparison.Ordinal
                    ) ||
                    __instance.Fsm == null ||
                    !string.Equals(
                        __instance.Fsm.Name,
                        "Interaction",
                        StringComparison.Ordinal
                    ) ||
                    __instance.State == null ||
                    !string.Equals(
                        __instance.State.Name,
                        "Is Already Present?",
                        StringComparison.Ordinal
                    ) ||
                    !BellwayScenes.Contains(
                        GetBaseSceneName(__instance.Owner)
                    ) ||
                    !(__instance.compareTo?.Value is
                        FastTravelLocations location) ||
                    __instance.equalEvent == null)
                {
                    return;
                }

                PlayerData playerData = PlayerData.instance;
                if (playerData != null)
                {
                    playerData.FastTravelNPCLocation = location;
                }
                __instance.notEqualEvent = __instance.equalEvent;
            }
        }
    }
}
