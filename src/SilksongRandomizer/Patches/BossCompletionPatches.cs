using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(PlayerData), "get_VampireGnatBossInAltLoc")]
    internal static class MoorwingLocationPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            if (SaveState.Instance == null) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class BossCompletionPatches
    {
        private static readonly Dictionary<string, string> DeathActors = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["Coral_33|Black Thread States/Black Thread World/Garmond Scenes/Garmond Black Threaded Scene/Garmond Black Threaded Fighter"] = "Boss: Lost Garmond",
            ["Coral_27|Battle Scene/Wave 1/Coral Conch Driller Giant Solo"] = "Boss: Raging Conchfly",
            ["Dust_Chef|Battle Parent/Battle Scene/Wave 2/Roachkeeper Chef (1)"] = "Boss: Disgraced Chef Lugoli",
            ["Greymoor_05_boss|Vampire Gnat Boss Scene/Vampire Gnat"] = "Boss: Moorwing",
            ["Greymoor_08_boss|Vampire Gnat Scene/Vampire Gnat"] = "Boss: Moorwing",
            ["Shellwood_18|Boss Scene Parent/Boss Scene/Splinter Queen"] = "Boss: Sister Splinter",
            ["Slab_16b|Broodmother Scene Control/Broodmother Scene/Battle Scene Broodmother/Wave 4/Slab Fly Broodmother"] = "Boss: Broodmother",
            ["Coral_39|Coral Warrior Grey"] = "Boss: Watcher at the Edge",
            ["Shellwood_22|Boss Scene/Seth"] = "Boss: Shrine Guardian Seth",
            ["Song_Tower_01|Boss Scene/Lace Boss2 New"] = "Boss: Lace (Cradle)",
            ["Hang_17b|Boss Scene - To Additive Load/Song Knight"] = "Boss: Second Sentinel",
            ["Bone_East_18b|Boss Scene/Bone Hunter Trapper"] = "Boss: Gurr the Outcast",
            ["Bone_15|Boss Scene/Skull King"] = "Boss: Skull Tyrant (The Marrow)",
            ["Bonetown_boss|Boss Scene/Skull King"] = "Boss: Skull Tyrant (Bone Bottom)",
            ["Memory_Ant_Queen|Boss Scene/Hunter Queen Boss"] = "Boss: Skarrsinger Karmelita",
            ["Shellwood_11b_Memory|Boss Scene/Flower Queen Boss"] = "Boss: Nyleth",
            ["Room_CrowCourt_02|Battle Scene/Wave 6/Crawfather"] = "Boss: Crawfather",
            ["Library_13|Grand Stage Scene/Boss Scene TormentedTrobbio/Tormented Trobbio"] = "Boss: Tormented Trobbio",
            ["Coral_Judge_Arena|Boss Scene/Last Judge"] = "Boss: Last Judge",
            ["Clover_19|Boss Scene/Cloverstag White Boss"] = "Boss: Palestag",
            ["Crawl_10|Blue Assistant"] = "Boss: Plasmified Zango",
        };

        private static readonly Dictionary<string, (string State, string Action, string Location)> DeathStates =
            new Dictionary<string, (string, string, string)>(StringComparer.Ordinal) {
            ["Bone_05_boss|Boss Scene|Battle End"] = ("End", "SetPlayerDataBool", "Boss: Bell Beast"),
            ["Bone_East_08_boss_golem|Boss Scene/song_golem|Control"] = ("Death Start", "RecordJournalKill", "Boss: Fourth Chorus"),
            ["Belltown_Shrine|Black Thread States Thread Only Variant/Normal World/Boss Scene/Spinner Boss|Control"] = ("Final Bind Burst", "RecordJournalKillV2", "Boss: Widow"),
            ["Dock_09|Boss Scene|Control"] = ("End Pause", "RecordJournalKill", "Boss: Forebrothers Signis & Gron"),
            ["Belltown_08|Boss Scene/Wisp Pyre Effigy|Summon Control"] = ("Award", "RecordJournalKill", "Boss: Father of the Flame"),
            ["Shadow_18|Battle Scene/Wave 6 - Boss/Swamp Shaman|Control"] = ("Death Hit", "RecordJournalKill", "Boss: Groal the Great"),
            ["Organ_01|Boss Scene/Phantom|Control"] = ("Death Explode", "RecordJournalKill", "Boss: Phantom"),
            ["Library_13|Grand Stage Scene/Boss Scene Trobbio/Trobbio|Control"] = ("Death Hit", "RecordJournalKill", "Boss: Trobbio"),
            ["Memory_Coral_Tower|Boss Scene/Coral King|Control"] = ("Heart Death Start", "SetPlayerDataBool", "Boss: Crust King Khann"),
            ["Slab_10b|Boss Scene/First Weaver|Control"] = ("Death Stagger", "RecordJournalKill", "Boss: First Sinner"),
            ["Ward_02_boss|Boss Scene/Conductor Boss|Control"] = ("Die", "RecordJournalKill", "Boss: The Unravelled"),
            ["Tut_03|Black Thread States/Normal World/Battle Scene/Wave 1/Mossbone Mother|Control"] = ("End", "SetPlayerDataBool", "Boss: Moss Mother"),
            ["Coral_11|Black Thread States Thread Only Variant/Normal World/Boss Scene|Control"] = ("Kill Final Driller", "SetPlayerDataBool", "Boss: Great Conchflies"),
            ["Clover_10|Boss Scene/Dancer Control|Control"] = ("Final Kill Pause", "Wait", "Boss: Clover Dancers"),
        };

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
            else if (scene == "Cog_Dancers" && owner == "Boss Scene" && __instance.FsmName == "Sequence")
            {
                stateName = "Wait";
                locationName = "Boss: Cogwork Dancers";
                expectedAction = "RecordJournalKill";
            }
            else if (scene == "Coral_29" && owner == "Zap Core Enemy" && __instance.FsmName == "Control")
            {
                stateName = "Death Hit";
                locationName = "Boss: Voltvyrm";
                expectedAction = "RecordJournalKill";
            }
            else if (DeathStates.TryGetValue(scene + "|" + Utils.GetHierarchyPath(__instance.transform) + "|" +
                __instance.FsmName, out var death))
            {
                stateName = death.State;
                locationName = death.Location;
                expectedAction = death.Action;
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
                if (state?.progressionShuffle?.RecordBossDefeated(locationName) == true)
                {
                    GameManager.instance?.QueueSaveGame();
                }
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
        private static class BossDeathPatch
        {
            [HarmonyPrefix]
            private static void Prefix(HealthManager __instance, out string __state)
            {
                __state = null;
                if (SaveState.Instance == null || __instance.isDead) return;
                DeathActors.TryGetValue(__instance.gameObject.scene.name + "|" +
                    Utils.GetHierarchyPath(__instance.transform), out __state);
            }

            [HarmonyPostfix]
            private static void Postfix(HealthManager __instance, string __state)
            {
                if (__state != null && __instance.isDead) Report(__state);
            }
        }
    }
}
