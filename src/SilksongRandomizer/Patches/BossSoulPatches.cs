using System.Collections;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;

namespace SilksongRandomizer.Patches
{
    internal static class BossSoulPatches
    {
        internal static readonly Dictionary<string, string> Battles = Index("battles");
        private static readonly Dictionary<string, string> Actors = Index("actors");
        private static readonly Dictionary<string, string> States = StateIndex();

        private static Dictionary<string, string> Index(string field) => BossSoulState.Catalogue
            .SelectMany(row => row[field].Select(actor => new { Key = (string)actor["scene"] + "|" + (string)actor["path"], Boss = (string)row["boss"] }))
            .ToDictionary(row => row.Key, row => row.Boss, StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> StateIndex()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (JToken row in BossSoulState.Catalogue)
                foreach (JToken control in row["states"])
                    foreach (string state in control["states"].Values<string>())
                    {
                        string path = control["object_name"] != null ? "@" + (string)control["object_name"] : (string)control["path"];
                        result[(string)control["scene"] + "|" + path + "|" + (string)control["fsm"] + "|" + state] = (string)row["boss"];
                    }
            return result;
        }

        private static readonly Dictionary<GameObject, (SaveState state, string boss)> Hidden =
            new Dictionary<GameObject, (SaveState, string)>();

        private static bool Hold(Transform transform)
        {
            if (SaveState.Instance == null || string.IsNullOrEmpty(SaveState.Instance.bossSoulsJson) || SaveState.Instance.bossSoulsJson == "[]") return false;
            for (Transform current = transform; current != null; current = current.parent)
            {
                GameObject actor = current.gameObject;
                if (!Actors.TryGetValue(actor.scene.name + "|" + Utils.GetHierarchyPath(current), out string boss) ||
                    !BossSoulState.IsMissing(SaveState.Instance, boss)) continue;
                if (string.Equals(actor.scene.name, "Cradle_03", StringComparison.OrdinalIgnoreCase) &&
                    Utils.GetHierarchyPath(current) == "Boss Scene/Silk Boss")
                    current.parent.Find("Silk_Hair")?.gameObject.SetActive(false);
                Hidden[actor] = (SaveState.Instance, boss);
                actor.SetActive(false);
                return true;
            }
            return false;
        }

        internal static void Update()
        {
            if (Hidden.Count == 0) return;
            var release = new List<GameObject>();
            List<GameObject> retry = null;
            foreach (var pair in Hidden)
                if (pair.Key == null || pair.Value.state != SaveState.Instance ||
                    !BossSoulState.IsMissing(pair.Value.state, pair.Value.boss)) release.Add(pair.Key);
                else if (pair.Key.activeSelf)
                {
                    if (retry == null) retry = new List<GameObject>();
                    retry.Add(pair.Key);
                }
            if (retry != null)
                foreach (GameObject actor in retry) actor.SetActive(false);
            foreach (GameObject actor in release)
            {
                var saved = Hidden[actor];
                Hidden.Remove(actor);
                if (actor != null && saved.state == SaveState.Instance) actor.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(GameManager), "StartAct3")]
        private static class PreserveAct3Bosses
        {
            private static void Prefix(out bool?[] __state)
            {
                __state = null;
                SaveState save = SaveState.Instance;
                PlayerData data = PlayerData.instance;
                if (data == null || string.IsNullOrEmpty(save?.bossSoulsJson) || save.bossSoulsJson == "[]") return;
                var bosses = new HashSet<string>(JArray.Parse(save.bossSoulsJson).Values<string>(), StringComparer.Ordinal);
                bool? Capture(string boss, bool defeated) => bosses.Contains(boss) &&
                    save.progressionShuffle?.TryGetBossDefeat(boss, out _) != true ? (bool?)defeated : null;
                __state = new[]
                {
                    Capture("Boss: Widow", data.spinnerDefeated),
                    Capture("Boss: Fourth Chorus", data.defeatedSongGolem),
                    Capture("Boss: Trobbio", data.defeatedTrobbio)
                };
            }

            private static void Postfix(bool?[] __state)
            {
                PlayerData data = PlayerData.instance;
                if (__state == null || data == null) return;
                if (__state[0].HasValue) data.spinnerDefeated = __state[0].Value;
                if (__state[1].HasValue) data.defeatedSongGolem = __state[1].Value;
                if (__state[2].HasValue) data.defeatedTrobbio = __state[2].Value;
            }
        }

        [HarmonyPatch(typeof(HealthManager), "OnEnable")]
        private static class EnableActor
        {
            private static void Postfix(HealthManager __instance) => Hold(__instance.transform);
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "OnEnable")]
        private static class EnableActorFsm
        {
            private static bool Prefix(PlayMakerFSM __instance) => !Hold(__instance.transform);
        }

        internal static bool CanStartBattle(string scene, string path, SaveState state) =>
            !Battles.TryGetValue(scene + "|" + path, out string boss) || !BossSoulState.IsMissing(state, boss);

        internal static bool CanEnterState(string scene, string path, string fsm, string stateName, SaveState state)
        {
            string key = scene + "|" + path + "|" + fsm + "|" + stateName;
            string objectKey = scene + "|@" + path.Substring(path.LastIndexOf('/') + 1) + "|" + fsm + "|" + stateName;
            return ((!States.TryGetValue(key, out string boss) && !States.TryGetValue(objectKey, out boss)) ||
                !BossSoulState.IsMissing(state, boss)) && EnemySoulPatches.CanEnterState(scene, path, fsm, stateName, state) &&
                NpcSoulState.CanEnterState(scene, path, fsm, stateName, state);
        }

        [HarmonyPatch(typeof(BattleScene), nameof(BattleScene.StartBattle))]
        private static class StartBattle
        {
            private static bool Prefix(BattleScene __instance) =>
                CanStartBattle(__instance.gameObject.scene.name,
                    Utils.GetHierarchyPath(__instance.transform), SaveState.Instance);
        }

        [HarmonyPatch(typeof(BattleScene), nameof(BattleScene.DoStartBattle))]
        private static class StartRoutine
        {
            private static bool Prefix(BattleScene __instance, ref IEnumerator __result)
            {
                if (CanStartBattle(__instance.gameObject.scene.name,
                    Utils.GetHierarchyPath(__instance.transform), SaveState.Instance)) return true;
                __result = Empty();
                return false;
            }

            private static IEnumerator Empty() { yield break; }
        }

        [HarmonyPatch(typeof(FsmState), nameof(FsmState.OnEnter))]
        private static class StartSequence
        {
            private static void Prefix(FsmState __instance)
            {
                SaveState save = SaveState.Instance;
                bool Configured(string json) => !string.IsNullOrEmpty(json) && json != "[]";
                if (save == null || (!Configured(save.bossSoulsJson) &&
                    !Configured(save.enemySoulsJson) && !Configured(save.npcSoulsJson))) return;
                Fsm fsm = __instance.Fsm;
                if (fsm?.GameObject == null || CanEnterState(fsm.GameObject.scene.name,
                    Utils.GetHierarchyPath(fsm.GameObject.transform), fsm.Name, __instance.Name, SaveState.Instance)) return;
                __instance.Actions = new FsmStateAction[] { new AwaitSoul(__instance, __instance.Actions) };
            }
        }

        private sealed class AwaitSoul : FsmStateAction
        {
            private readonly FsmState state;
            private readonly FsmStateAction[] actions;

            internal AwaitSoul(FsmState state, FsmStateAction[] actions)
            {
                this.state = state;
                this.actions = actions;
            }

            public override void OnUpdate()
            {
                if (CanEnterState(Fsm.GameObject.scene.name, Utils.GetHierarchyPath(Fsm.GameObject.transform),
                    Fsm.Name, state.Name, SaveState.Instance)) Fsm.SetState(state.Name);
            }

            public override void OnExit() => state.Actions = actions;
        }
    }
}
