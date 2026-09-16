using System;
using SetPlayerDataBoolAction = HutongGames.PlayMaker.Actions.SetPlayerDataBool;
using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

namespace SilksongRandomizer.Patches
{
    internal static class RedMemorySkip
    {
        private const string MemoryScene = "Memory_Red";
        private const string RewardGate = "door_wakeInRedMemory Root";

        private static bool IsLiteral(FsmString value, string expected)
        {
            return value != null && !value.UsesVariable &&
                   string.Equals(value.Value, expected, StringComparison.Ordinal);
        }

        private static bool IsDirectRitualEntry()
        {
            GameManager game = GameManager.UnsafeInstance;
            return SaveState.Instance != null && game != null &&
                   game.lastSceneName == "Tut_04" &&
                   game.entryGateName == RewardGate;
        }

        [HarmonyPatch(typeof(BeginSceneTransitionV2), nameof(BeginSceneTransitionV2.OnEnter))]
        private static class RitualEntry
        {
            [HarmonyPrefix]
            private static void Prefix(BeginSceneTransitionV2 __instance)
            {
                if (SaveState.Instance == null ||
                    __instance.Fsm?.GameObject == null ||
                    __instance.Fsm.GameObject.scene.name != "Tut_04" ||
                    __instance.Fsm.GameObject.name != "Snail Shamans Set" ||
                    __instance.Fsm.Name != "Dialogue" ||
                    __instance.State?.Name != "Load Memory Scene" ||
                    !IsLiteral(__instance.SceneName, MemoryScene) ||
                    !IsLiteral(__instance.EntryGateName, "top1"))
                {
                    return;
                }

                __instance.EntryGateName.Value = RewardGate;
            }
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class RewardSequence
        {
            [HarmonyPrefix]
            private static void Prefix(PlayMakerFSM __instance)
            {
                if (!IsDirectRitualEntry() ||
                    __instance?.gameObject == null ||
                    __instance.gameObject.scene.name != MemoryScene ||
                    __instance.gameObject.name != RewardGate ||
                    __instance.FsmName != "Wake Up")
                {
                    return;
                }

                FsmState start = __instance.FsmStates.FirstOrDefault(s => s.Name == "Start Wait");
                FsmState reward = __instance.FsmStates.FirstOrDefault(s => s.Name == "Set State");
                FsmState exit = __instance.FsmStates.FirstOrDefault(s => s.Name == "End Scene");
                FsmTransition next = start?.Transitions?.Length == 1 ? start.Transitions[0] : null;
                FsmStateAction[] actions = reward?.Actions;
                if (next?.EventName != "FINISHED" || next.ToState != "Low Graphics?" ||
                    actions == null || actions.Length != 4 ||
                    !(actions[0] is SetPlayerDataBoolAction completed) ||
                    !IsLiteral(completed.boolName, "CompletedRedMemory") ||
                    completed.value == null || completed.value.UsesVariable || !completed.value.Value ||
                    !(actions[1] is SavedItemGetV2 grant) ||
                    grant.Item == null || grant.Item.UsesVariable ||
                    !(grant.Item.Value is CollectableItem flower) || flower.name != "White Flower" ||
                    grant.Amount == null || grant.Amount.UsesVariable || grant.Amount.Value != 1 ||
                    actions[2]?.GetType().Name != "CompleteJournalRecord" ||
                    actions[3]?.GetType().Name != "QueueAutoSave" ||
                    exit?.Actions?.Length != 1 ||
                    !(exit.Actions[0] is BeginSceneTransitionV2 transition) ||
                    !IsLiteral(transition.SceneName, "Tut_04") ||
                    !IsLiteral(transition.EntryGateName, "door_ritualEnd"))
                {
                    RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Red Memory reward sequence changed; keeping the final scene.");
                    return;
                }

                next.ToState = reward.Name;
            }
        }

        [HarmonyPatch(typeof(AutoEquipCrestV3), nameof(AutoEquipCrestV3.OnEnter))]
        private static class PreserveRitualCrest
        {
            [HarmonyPrefix]
            private static bool Prefix(AutoEquipCrestV3 __instance)
            {
                if (!IsDirectRitualEntry() ||
                    __instance.Fsm?.GameObject == null ||
                    __instance.Fsm.GameObject.scene.name != MemoryScene ||
                    __instance.Fsm.GameObject.name != RewardGate ||
                    __instance.Fsm.Name != "Wake Up" ||
                    __instance.State?.Name != "Award Achievement" ||
                    __instance.Crest == null || __instance.Crest.UsesVariable ||
                    __instance.Crest.Value != null ||
                    __instance.IsTemp == null || __instance.IsTemp.UsesVariable ||
                    __instance.IsTemp.Value)
                {
                    return true;
                }

                __instance.Finish();
                return false;
            }
        }
    }
}
