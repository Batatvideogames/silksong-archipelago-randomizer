using HarmonyLib;
using HutongGames.PlayMaker;
using System;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Prevents the F4 recovery warp from impersonating Moss Mother's real
    /// defeat. Bonetown's opening-story setup can assert defeatedMossMother
    /// while crossing the tutorial boundary, so the AP check waits for the
    /// boss Control/End state instead.
    /// </summary>
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class MossMotherWarpSafety
    {
        internal const string LocationName = "Boss: Moss Mother";

        [HarmonyPostfix]
        private static void Postfix(PlayMakerFSM __instance)
        {
            if (__instance == null ||
                !string.Equals(
                    __instance.gameObject.scene.name,
                    "Tut_03",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    __instance.name,
                    "Mossbone Mother",
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    __instance.FsmName,
                    "Control",
                    StringComparison.Ordinal
                ))
            {
                return;
            }

            FsmState endState = FindState(__instance, "End");
            if (endState == null || endState.Actions == null)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Moss Mother F4 safety could not find " +
                    "the shipped Control/End state."
                );
                return;
            }

            for (int index = 0; index < endState.Actions.Length; index++)
            {
                if (endState.Actions[index] is ConfirmRealDefeat)
                {
                    return;
                }
            }

            int setPlayerDataIndex = -1;
            for (int index = 0; index < endState.Actions.Length; index++)
            {
                if (endState.Actions[index] is
                    HutongGames.PlayMaker.Actions.SetPlayerDataBool)
                {
                    setPlayerDataIndex = index;
                    break;
                }
            }
            if (setPlayerDataIndex < 0)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Moss Mother F4 safety found Control/End " +
                    "but not its defeatedMossMother action."
                );
                return;
            }

            ConfirmRealDefeat confirmation = new ConfirmRealDefeat();
            confirmation.Init(endState);
            FsmStateAction[] oldActions = endState.Actions;
            FsmStateAction[] patchedActions =
                new FsmStateAction[oldActions.Length + 1];
            Array.Copy(
                oldActions,
                0,
                patchedActions,
                0,
                setPlayerDataIndex
            );
            patchedActions[setPlayerDataIndex] = confirmation;
            Array.Copy(
                oldActions,
                setPlayerDataIndex,
                patchedActions,
                setPlayerDataIndex + 1,
                oldActions.Length - setPlayerDataIndex
            );
            endState.Actions = patchedActions;

            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Moss Mother F4 safety attached to the " +
                "shipped boss completion state."
            );
        }

        internal static bool PrepareForBoneBottomWarp()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                playerData == null ||
                playerData.defeatedMossMother ||
                state.IsLocationChecked(LocationName))
            {
                return false;
            }

            state.mossMotherBypassedByBoneBottomWarp = true;
            return true;
        }

        internal static void CancelPreparedWarp()
        {
            SaveState state = SaveState.Instance;
            if (state != null)
            {
                state.mossMotherBypassedByBoneBottomWarp = false;
            }
        }

        internal static void RecoverInterruptedBoneBottomWarp()
        {
            SaveState state = SaveState.Instance;
            if (state == null || state.IsLocationChecked(LocationName))
            {
                return;
            }

            // If the game closes during the Bone Bottom warp, the intro can
            // finish before the randomizer saves that the warp was used.
            // Restore the bypass flag so it can finish cleaning up.
            state.mossMotherBypassedByBoneBottomWarp = true;
        }

        internal static void Update()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                playerData == null ||
                !state.mossMotherBypassedByBoneBottomWarp)
            {
                return;
            }

            if (state.IsLocationChecked(LocationName))
            {
                state.mossMotherBypassedByBoneBottomWarp = false;
                return;
            }

            // Bone Bottom temporarily marks Moss Mother as defeated during
            // its intro.
            if (!playerData.churchKeeperIntro)
            {
                return;
            }

            // A real kill clears the bypass flag before reaching this point.
            playerData.defeatedMossMother = false;
        }

        internal static bool IsLegitimateDefeat()
        {
            SaveState state = SaveState.Instance;
            return PlayerData.instance != null &&
                   PlayerData.instance.defeatedMossMother &&
                   (
                       state == null ||
                       !state.mossMotherBypassedByBoneBottomWarp
                   );
        }

        private static FsmState FindState(
            PlayMakerFSM fsm,
            string stateName
        )
        {
            FsmState[] states = fsm.FsmStates;
            if (states == null)
            {
                return null;
            }

            foreach (FsmState state in states)
            {
                if (state != null &&
                    string.Equals(
                        state.Name,
                        stateName,
                        StringComparison.Ordinal
                    ))
                {
                    return state;
                }
            }

            return null;
        }

        private sealed class ConfirmRealDefeat : FsmStateAction
        {
            public override void OnEnter()
            {
                SaveState state = SaveState.Instance;
                if (state != null)
                {
                    state.mossMotherBypassedByBoneBottomWarp = false;
                }
                Finish();
            }
        }
    }
}
