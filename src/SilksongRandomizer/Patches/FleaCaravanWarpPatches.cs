using HarmonyLib;
using HutongGames.PlayMaker;
using System;
using System.Linq;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Records the completed Bone Bottom-to-Greymoor caravan ride for the
    /// progressive F4 hub. The native caravan-location enum is not suitable:
    /// Bone Bottom advances it even when the player declines the ride.
    /// </summary>
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class FleaCaravanWarpPatches
    {
        private const string ArrivalSceneName = "Greymoor_08_caravan";
        private const string ArrivalObjectName = "door_caravanTravelEnd";
        private const string ArrivalFsmName = "Travel End";
        private const string ArrivalStateName = "Send Event";

        private static readonly string[] ExpectedActionTypes =
        {
            "HutongGames.PlayMaker.Actions.CallMethodProper",
            "HutongGames.PlayMaker.Actions.CallMethodProper",
            "HutongGames.PlayMaker.Actions.SendMessage",
            "QueueSaveGame",
            "HutongGames.PlayMaker.Actions.RemoveHeroInputBlocker",
            "HutongGames.PlayMaker.Actions.SetPlayerDataBool",
            "HutongGames.PlayMaker.Actions.SendEventToRegister",
        };

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(PlayMakerFSM __instance)
        {
            if (__instance == null ||
                __instance.gameObject == null ||
                !string.Equals(
                    __instance.gameObject.scene.name,
                    ArrivalSceneName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    __instance.gameObject.name,
                    ArrivalObjectName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    __instance.FsmName,
                    ArrivalFsmName,
                    StringComparison.Ordinal))
            {
                return;
            }

            FsmState arrivalState = __instance.Fsm?.GetState(
                ArrivalStateName
            );
            FsmStateAction[] actions = arrivalState?.Actions;
            if (actions == null)
            {
                LogFailure("the shipped Send Event state was missing");
                return;
            }

            if (actions.Any(action =>
                    action is RecordGreymoorCaravanRideAction))
            {
                return;
            }

            string[] actionTypes = actions
                .Select(action => action?.GetType().FullName ?? string.Empty)
                .ToArray();
            if (!actionTypes.SequenceEqual(ExpectedActionTypes))
            {
                LogFailure(
                    "the shipped Send Event action layout no longer matched"
                );
                return;
            }

            const int queueSaveIndex = 3;
            RecordGreymoorCaravanRideAction marker =
                new RecordGreymoorCaravanRideAction();
            marker.Init(arrivalState);

            arrivalState.Actions = actions
                .Take(queueSaveIndex)
                .Concat(new FsmStateAction[] { marker })
                .Concat(actions.Skip(queueSaveIndex))
                .ToArray();
        }

        private static void LogFailure(string reason)
        {
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Flea Caravan F4 unlock failed closed: " +
                reason + "."
            );
        }

        private sealed class RecordGreymoorCaravanRideAction :
            FsmStateAction
        {
            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (state != null && state.IsRoomBound)
                {
                    state.rodeFleaCaravanToGreymoor = true;
                }

                Finish();
            }
        }
    }
}
