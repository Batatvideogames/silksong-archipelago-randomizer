using System;
using System.Linq;
using GlobalEnums;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

namespace SilksongRandomizer.Patches
{
    internal static class EncounterAvailabilityPatches
    {
        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class BossEncounterAvailabilityPatch
        {
            [HarmonyPrefix]
            private static void Prefix(PlayMakerFSM __instance)
            {
                string scene = __instance.gameObject.scene.name;
                if (scene == "Coral_11" && __instance.FsmName == "Control" &&
                    Utils.GetHierarchyPath(__instance.transform) ==
                    "Black Thread States Thread Only Variant/Normal World/Boss Scene")
                    KeepGreatConchflies(__instance.Fsm.GetState("State"));
                else if (scene == "Bone_15" && __instance.FsmName == "Behaviour" &&
                    Utils.GetHierarchyPath(__instance.transform) == "Boss Scene/Skull King")
                    KeepMarrowSkullTyrant(__instance.Fsm.GetState("State Check"));
            }
        }

        private static void KeepGreatConchflies(FsmState state)
        {
            if (state == null || SaveState.Instance?.progressionShuffle?.TryGetBossDefeat(
                "Boss: Great Conchflies", out _) != true) return;
            state.Actions = state.Actions.Where(action =>
                !(action is PlayerDataBoolTrueAndFalse solo && solo.trueBool.Value == "coralDrillerSoloReady" &&
                  solo.falseBool.Value == "defeatedCoralDrillerSolo")).ToArray();
        }

        private static void KeepMarrowSkullTyrant(FsmState state)
        {
            if (state == null || SaveState.Instance?.progressionShuffle?.TryGetBossDefeat(
                "Boss: Skull Tyrant (The Marrow)", out _) != true) return;
            state.Actions = state.Actions.Where(action =>
                !(action is PlayerDataBoolTest invasion &&
                  (invasion.boolName.Value == "skullKingInvaded" || invasion.boolName.Value == "skullKingWillInvade")))
                .ToArray();
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.TimePasses))]
        private static class SkullTyrantInvasionPatch
        {
            [HarmonyPostfix]
            private static void Postfix(GameManager __instance)
            {
                SaveState save = SaveState.Instance;
                PlayerData data = PlayerData.instance;
                if (save == null || data == null || data.skullKingKilled || data.blackThreadWorld ||
                    !ProgressionShufflePatches.StoryCredit("Boss: Skull Tyrant (The Marrow)", data.skullKingDefeated) ||
                    !(data.visitedCitadel || data.visitedCoral || data.visitedDustpens)) return;
                var progress = save.progressionShuffle;
                if (progress?.TryGetBossDefeat("Boss: Skull Tyrant (Bone Bottom)", out bool defeated) == true && defeated)
                    return;
                if (QuestManager.GetQuest("Soul Snare")?.IsAccepted == true &&
                    progress?.TryGetBossDefeat("Boss: Skull Tyrant (Bone Bottom)", out _) != true) return;
                MapZone zone = __instance.GetCurrentMapZoneEnum();
                if (zone != MapZone.BONETOWN && zone != MapZone.PATH_OF_BONE && zone != MapZone.MOSS_CAVE)
                    data.skullKingWillInvade = true;
            }
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class NuuAvailabilityPatch
        {
            [HarmonyPrefix]
            private static void Prefix(PlayMakerFSM __instance)
            {
                if (SaveState.Instance == null ||
                    !string.Equals(__instance.gameObject.scene.name, "Halfway_01", StringComparison.OrdinalIgnoreCase)) return;
                string path = Utils.GetHierarchyPath(__instance.transform);
                if (path == "_NPCs/Hunter Fan Control" && __instance.FsmName == "Control")
                {
                    FsmState check = __instance.Fsm.GetState("Check");
                    if (check != null)
                        check.Actions = check.Actions.Where(action =>
                            !(action is PlayerDataBoolTest act3 && act3.boolName.Value == "blackThreadWorld"))
                            .Select(action => {
                                if (!(action is PlayerDataBoolTest home) || home.boolName.Value != "nuuIsHome") return action;
                                var replacement = new NuuHomeCheck();
                                replacement.Init(check);
                                return replacement;
                            }).ToArray();
                    KeepScrolls(__instance.Fsm.GetState("Left"));
                }
                else if (path == "_NPCs/Hunter Fan Control/Nuu" && __instance.FsmName == "Dialogue")
                    KeepScrolls(__instance.Fsm.GetState("Footsteps"));
            }
        }

        private static void KeepScrolls(FsmState state)
        {
            if (state == null) return;
            foreach (var action in state.Actions.OfType<ActivateGameObject>())
                if (action.gameObject.GameObject.Name == "Nuu_Scrolls") action.activate.Value = true;
        }

        private sealed class NuuHomeCheck : FsmStateAction
        {
            public override void OnEnter()
            {
                PlayerData data = PlayerData.instance;
                FullQuestBase quest = QuestManager.GetQuest("Journal");
                bool needsJournal = JournalRandomization.Enabled
                    ? !SaveState.Instance.IsLocationChecked(JournalRandomization.JournalLocation)
                    : !data.hasJournal;
                data.nuuIsHome = !data.MetHalfwayHunterFan || needsJournal || quest == null ||
                    (!quest.IsAccepted && !quest.IsCompleted) || (data.blackThreadWorld && !data.nuuIntroAct3);
                Fsm.Event(data.nuuIsHome ? "HOME" : "AWAY");
                Finish();
            }
        }
    }
}
