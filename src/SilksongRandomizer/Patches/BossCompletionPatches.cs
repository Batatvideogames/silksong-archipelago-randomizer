using System;
using HarmonyLib;
using HutongGames.PlayMaker;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class BossCompletionPatches
    {
        [HarmonyPostfix]
        private static void Postfix(PlayMakerFSM __instance)
        {
            string scene = __instance.gameObject.scene.name;
            string owner = __instance.gameObject.name;
            string stateName;
            string locationName;
            string expectedAction;
            if (scene == "Cradle_03" && owner == "Silk Boss" && __instance.FsmName == "Phase Control")
            {
                stateName = "Death Hit";
                locationName = "Boss: Grand Mother Silk";
                expectedAction = "RecordJournalKillV2";
            }
            else if (scene == "Bellway_Centipede_Arena" && owner == "Centipede Control" && __instance.FsmName == "Control")
            {
                stateName = "Spit Head Out";
                locationName = "Boss: Bell Eater";
                expectedAction = "RecordJournalKill";
            }
            else if (scene == "Crawl_10" && owner == "Blue Assistant" && __instance.FsmName == "Control")
            {
                stateName = "Extract Kill";
                locationName = "Boss: Plasmified Zango";
                expectedAction = "CompleteJournalRecord";
            }
            else if (scene == "Peak_07" && owner == "Pinstress Boss" && __instance.FsmName == "Control")
            {
                stateName = "Set Defeated";
                locationName = "Boss: Pinstress";
                expectedAction = "SetIsDead";
            }
            else return;

            foreach (FsmState fsmState in __instance.FsmStates)
            {
                if (fsmState.Name != stateName) continue;
                bool verified = false;
                foreach (FsmStateAction action in fsmState.Actions)
                {
                    if (action is ReportCompletion) return;
                    if (action.GetType().Name == expectedAction) verified = true;
                }
                if (!verified) return;
                ReportCompletion completion = new ReportCompletion(locationName);
                completion.Init(fsmState);
                FsmStateAction[] actions = new FsmStateAction[fsmState.Actions.Length + 1];
                actions[0] = completion;
                Array.Copy(fsmState.Actions, 0, actions, 1, fsmState.Actions.Length);
                fsmState.Actions = actions;
                return;
            }
        }

        private static void Report(string locationName)
        {
            try
            {
                SaveState state = SaveState.Instance;
                if (state != null && state.IsRandomized(ItemType.Boss) &&
                    state.IsLocationEnabled(locationName) && state.IsLocationInSeed(locationName) &&
                    !state.IsLocationChecked(locationName))
                    state.CheckLocation(locationName);
            }
            catch (Exception exception)
            {
                RandomizerPlugin.Log?.LogError("[RANDOMIZER] Could not report " + locationName + ": " + exception);
            }
        }

        private sealed class ReportCompletion : FsmStateAction
        {
            private readonly string locationName;
            internal ReportCompletion(string locationName) { this.locationName = locationName; }
            public override void OnEnter()
            {
                try { Report(locationName); }
                finally { Finish(); }
            }
        }

        [HarmonyPatch(typeof(HealthManager), "Die", new Type[] {
            typeof(float?), typeof(AttackTypes), typeof(NailElements), typeof(UnityEngine.GameObject),
            typeof(bool), typeof(float), typeof(bool), typeof(bool) })]
        private static class ZangoDeathPatch
        {
            [HarmonyPostfix]
            private static void Postfix(HealthManager __instance)
            {
                if (__instance.isDead && __instance.gameObject.scene.name == "Crawl_10" &&
                    __instance.gameObject.name == "Blue Assistant")
                    Report("Boss: Plasmified Zango");
            }
        }
    }
}
