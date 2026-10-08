using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HutongGames.PlayMaker;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class EnemySoulPatches
    {
        private static readonly AccessTools.FieldRef<EnemyDeathEffects, EnemyJournalRecord> Journal =
            AccessTools.FieldRefAccess<EnemyDeathEffects, EnemyJournalRecord>("journalRecord");
        private static readonly AccessTools.FieldRef<EnemyDeathEffects, bool> DidFire =
            AccessTools.FieldRefAccess<EnemyDeathEffects, bool>("didFire");
        private static readonly Dictionary<GameObject, (SaveState state, string[] species)> Hidden =
            new Dictionary<GameObject, (SaveState, string[])>();
        private static readonly Dictionary<GameObject, (SaveState state, BattleWave wave)> SkippedBossEnemies = new Dictionary<GameObject, (SaveState, BattleWave)>();
        private static readonly Dictionary<string, string[]> Battles = BuildRequirements("battles");
        private static readonly Dictionary<string, string[]> Entrances = BuildRequirements("entrances");
        private static readonly Dictionary<string, string[]> Actors = BuildRequirements("actors");
        private static readonly Dictionary<string, string[]> States = BuildRequirements("states");

        private static Dictionary<string, string[]> BuildRequirements(string field)
        {
            var rows = new List<KeyValuePair<string, string>>();
            foreach (JToken row in EnemySoulState.Catalogue)
                foreach (JToken gate in row[field] ?? new JArray())
                {
                    string key = (string)gate["scene"] + "|" + (string)gate["path"];
                    if (field == "states")
                        foreach (string state in gate["states"].Values<string>())
                            rows.Add(new KeyValuePair<string, string>(key + "|" + (string)gate["fsm"] + "|" + state, (string)row["name"]));
                    else rows.Add(new KeyValuePair<string, string>(key, (string)row["name"]));
                }
            return rows.GroupBy(row => row.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(row => row.Value).Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);
        }

        private static bool MissingAny(SaveState state, IEnumerable<string> species) => species.Any(name => EnemySoulState.IsMissing(state, name));

        internal static bool CanEnterState(string scene, string path, string fsm, string name, SaveState state) =>
            !States.TryGetValue(scene + "|" + path + "|" + fsm + "|" + name, out string[] species) || !MissingAny(state, species);
        private static readonly ConditionalWeakTable<object, Reward> Rewards = new ConditionalWeakTable<object, Reward>();
        private static readonly MethodInfo NativeCurrency = AccessTools.Method(typeof(HealthManager), "SpawnCurrency");

        private sealed class Reward { internal bool Replaced; }

        internal static bool TryGetSpecies(Component component, out string species)
        {
            species = null;
            EnemyDeathEffects death = component.GetComponent<EnemyDeathEffects>();
            if (death == null) return false;
            EnemyJournalRecord record = Journal(death);
            if (record != null) return EnemySoulState.TryGetSpecies(record, out species);
            string key = component.gameObject.scene.name + "|" + Utils.GetHierarchyPath(component.transform);
            if (Actors.TryGetValue(key, out string[] actors))
            {
                species = actors[0];
                return true;
            }
            if (component.GetComponent<HealthManager>() == null) return false;
            foreach (PlayMakerFSM fsm in component.GetComponents<PlayMakerFSM>())
                foreach (FsmState state in fsm.FsmStates ?? Array.Empty<FsmState>())
                    foreach (FsmStateAction action in state.Actions ?? Array.Empty<FsmStateAction>())
                        if (EnemySoulState.TryGetSpecies(JournalRecord(action), out string candidate) &&
                            EnemySoulState.IsJournalDeath(candidate, fsm.FsmName, state.Name))
                        {
                            species = candidate;
                            return true;
                        }
            return false;
        }

        private static EnemyJournalRecord JournalRecord(FsmStateAction action) =>
            (action is RecordJournalKill kill ? kill.Record?.Value :
                action is RecordJournalKillV2 killV2 ? killV2.Record?.Value : null) as EnemyJournalRecord;

        [HarmonyPatch]
        private static class JournalActionDeath
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(RecordJournalKill), nameof(RecordJournalKill.OnEnter));
                yield return AccessTools.Method(typeof(RecordJournalKillV2), nameof(RecordJournalKillV2.OnEnter));
            }

            private static void Prefix(FsmStateAction __instance)
            {
                if (!EnemySoulState.TryGetSpecies(JournalRecord(__instance), out string species) ||
                    !EnemySoulState.IsJournalDeath(species, __instance.Fsm.Name, __instance.State.Name)) return;
                HealthManager health = __instance.Fsm.GameObject.GetComponent<HealthManager>();
                if (health != null && health.WillAwardJournalKill && ClaimKill(SaveState.Instance, species))
                {
                    Rewards.Remove(health);
                    Rewards.Add(health, new Reward { Replaced = true });
                }
            }
        }

        internal static bool Hold(Transform transform)
        {
            if (SaveState.Instance == null || string.IsNullOrEmpty(SaveState.Instance.enemySoulsJson) || SaveState.Instance.enemySoulsJson == "[]") return false;
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (WasSkipped(current.gameObject))
                {
                    current.gameObject.SetActive(false);
                    return true;
                }
                string key = current.gameObject.scene.name + "|" + Utils.GetHierarchyPath(current);
                string[] required = Entrances.TryGetValue(key, out string[] entrance) ? entrance :
                    Actors.TryGetValue(key, out string[] actors) ? actors :
                    TryGetSpecies(current, out string species) ? new[] { species } : Array.Empty<string>();
                if (!MissingAny(SaveState.Instance, required)) continue;
                Hidden[current.gameObject] = (SaveState.Instance, required);
                current.gameObject.SetActive(false);
                return true;
            }
            return false;
        }

        internal static bool IsSceneActor(string key) => Actors.ContainsKey(key) || Entrances.ContainsKey(key);

        internal static bool CancelGateClose(FsmState state)
        {
            Fsm fsm = state.Fsm;
            if (fsm?.GameObject == null || fsm.GameObject.scene.name != "Under_10" ||
                fsm.Name != "BG Control" || state.Name != "Lock Levers") return false;
            string path = Utils.GetHierarchyPath(fsm.GameObject.transform);
            if (CanEnterState(fsm.GameObject.scene.name, path, fsm.Name, state.Name, SaveState.Instance)) return false;
            fsm.SetState("Idle");
            return true;
        }

        [HarmonyPatch(typeof(BattleScene), nameof(BattleScene.LockInBattle))]
        private static class LockBattle
        {
            private static bool Prefix(BattleScene __instance) => CanStartBattle(__instance);
        }

        internal static void Update()
        {
            foreach (GameObject actor in SkippedBossEnemies.Where(pair => pair.Key == null || !WasSkipped(pair.Key)).Select(pair => pair.Key).ToArray())
                SkippedBossEnemies.Remove(actor);
            if (Hidden.Count == 0) return;
            var release = new List<GameObject>();
            List<GameObject> retry = null;
            foreach (var pair in Hidden)
                if (pair.Key == null || pair.Value.state != SaveState.Instance ||
                    (!MissingAny(pair.Value.state, pair.Value.species) && !WasSkipped(pair.Key))) release.Add(pair.Key);
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
                if (actor != null && saved.state == SaveState.Instance)
                {
                    if (string.Equals(actor.scene.name, "Bone_12", StringComparison.OrdinalIgnoreCase) &&
                        Utils.GetHierarchyPath(actor.transform) == "Black Thread States/Normal World/Jail Scene 1/Pilgrim Dummy" &&
                        actor.transform.parent.GetComponents<PlayMakerFSM>().Any(fsm =>
                            fsm.FsmName == "Control Pilgrim" && fsm.ActiveStateName == "State 3")) continue;
                    actor.SetActive(true);
                }
            }
        }

        [HarmonyPatch(typeof(HealthManager), "OnEnable")]
        private static class EnableEnemy
        {
            private static void Postfix(HealthManager __instance) => Hold(__instance.transform);
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "OnEnable")]
        private static class EnableEnemyFsm
        {
            private static bool Prefix(PlayMakerFSM __instance) => !Hold(__instance.transform);
        }

        internal static bool CanStartBattle(BattleScene battle)
        {
            SaveState state = SaveState.Instance;
            if (state == null || string.IsNullOrEmpty(state.enemySoulsJson) || state.enemySoulsJson == "[]" || IsBossBattle(battle)) return true;
            string key = battle.gameObject.scene.name + "|" + Utils.GetHierarchyPath(battle.transform);
            if (Battles.TryGetValue(key, out string[] expected) && MissingAny(state, expected)) return false;
            foreach (EnemyDeathEffects death in battle.GetComponentsInChildren<EnemyDeathEffects>(true))
                if (TryGetSpecies(death, out string species) &&
                    EnemySoulState.IsMissing(state, species)) return false;
            if (battle.waves != null)
                foreach (BattleWave wave in battle.waves)
                    if (wave != null)
                        foreach (EnemyDeathEffects death in wave.GetComponentsInChildren<EnemyDeathEffects>(true))
                            if (TryGetSpecies(death, out string species) &&
                                EnemySoulState.IsMissing(state, species)) return false;
            return true;
        }

        private static bool IsBossBattle(BattleScene battle) => battle != null &&
            BossSoulPatches.Battles.ContainsKey(battle.gameObject.scene.name + "|" + Utils.GetHierarchyPath(battle.transform));

        private static bool WasSkipped(GameObject actor) =>
            SkippedBossEnemies.TryGetValue(actor, out var saved) && saved.state == SaveState.Instance &&
            saved.wave != null && actor.transform.parent == saved.wave.transform;

        private static bool SkipBossEnemy(GameObject actor, BattleWave wave)
        {
            if (WasSkipped(actor)) return true;
            if (!TryGetSpecies(actor.transform, out string species) || !EnemySoulState.IsMissing(SaveState.Instance, species)) return false;
            Hold(actor.transform);
            SkippedBossEnemies[actor] = (SaveState.Instance, wave);
            return true;
        }

        private static readonly AccessTools.FieldRef<BattleWave, BattleScene> WaveBattle =
            AccessTools.FieldRefAccess<BattleWave, BattleScene>("battleScene");
        private static readonly FieldInfo WaveChildren = AccessTools.Field(typeof(BattleWave), "children");
        private static readonly FieldInfo ChildObject = AccessTools.Field(WaveChildren.FieldType.GetGenericArguments()[0], "gameObject");

        internal static int CountWaveChildren(Transform transform)
        {
            SaveState state = SaveState.Instance;
            if (string.IsNullOrEmpty(state?.enemySoulsJson) || state.enemySoulsJson == "[]") return transform.childCount;
            BattleWave wave = transform.GetComponent<BattleWave>();
            if (wave == null || !IsBossBattle(WaveBattle(wave))) return transform.childCount;
            int count = 0;
            foreach (Transform child in transform)
                if (!SkipBossEnemy(child.gameObject, wave)) count++;
            return count;
        }

        [HarmonyPatch(typeof(BattleWave), nameof(BattleWave.WaveStarted))]
        private static class StartBossWave
        {
            private static void Prefix(BattleWave __instance, out IList __state)
            {
                __state = null;
                SaveState state = SaveState.Instance;
                if (string.IsNullOrEmpty(state?.enemySoulsJson) || state.enemySoulsJson == "[]") return;
                __instance.Init(null);
                if (!IsBossBattle(WaveBattle(__instance))) return;
                IList original = (IList)WaveChildren.GetValue(__instance);
                IList present = (IList)Activator.CreateInstance(WaveChildren.FieldType);
                foreach (object child in original)
                    if (!SkipBossEnemy((GameObject)ChildObject.GetValue(child), __instance)) present.Add(child);
                __state = original;
                WaveChildren.SetValue(__instance, present);
            }

            private static void Finalizer(BattleWave __instance, IList __state)
            {
                if (__state != null) WaveChildren.SetValue(__instance, __state);
            }
        }

        [HarmonyPatch]
        private static class CountBossWaves
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(BattleScene), "DoCheckEnemiesNew");
                Type iterator = AccessTools.Method(typeof(BattleScene), nameof(BattleScene.CheckEnemyCount))
                    .GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
                yield return AccessTools.Method(iterator, "MoveNext");
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo count = AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.childCount));
                MethodInfo replacement = AccessTools.Method(typeof(EnemySoulPatches), nameof(CountWaveChildren));
                foreach (CodeInstruction instruction in instructions)
                    yield return instruction.Calls(count)
                        ? new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = replacement }
                        : instruction;
            }
        }

        [HarmonyPatch(typeof(BattleScene), nameof(BattleScene.StartBattle))]
        private static class StartBattle
        {
            private static bool Prefix(BattleScene __instance) => CanStartBattle(__instance);
        }

        [HarmonyPatch(typeof(BattleScene), nameof(BattleScene.DoStartBattle))]
        private static class StartRoutine
        {
            private static bool Prefix(BattleScene __instance, ref IEnumerator __result)
            {
                if (CanStartBattle(__instance)) return true;
                __result = Empty();
                return false;
            }
            private static IEnumerator Empty() { yield break; }
        }

        internal static bool ClaimKill(SaveState state, string species)
        {
            string location = EnemySoulState.LocationName(species);
            if (!EnemySoulState.IsEnabled(state, species) || !state.IsLocationInSeed(location) ||
                state.checkedLocations.Contains(location)) return false;
            state.CheckLocation(location);
            return state.checkedLocations.Contains(location);
        }

        [HarmonyPatch]
        private static class EnemyDeath
        {
            private static MethodBase TargetMethod() => AccessTools.Method(typeof(EnemyDeathEffects), "ReceiveDeathEvent",
                new[] { typeof(float?), typeof(AttackTypes), typeof(NailElements), typeof(GameObject), typeof(float),
                        typeof(bool), typeof(Action<Transform>), typeof(bool).MakeByRefType(), typeof(GameObject).MakeByRefType() });

            private static void Prefix(EnemyDeathEffects __instance, bool resetDeathEvent, Action<Transform> onCorpseBegin)
            {
                if (SaveState.Instance == null || string.IsNullOrEmpty(SaveState.Instance.enemySoulsJson) || SaveState.Instance.enemySoulsJson == "[]") return;
                if (DidFire(__instance) && !resetDeathEvent) return;
                HealthManager health = __instance.GetComponent<HealthManager>();
                if (health == null || health.hasSpecialDeath || !health.isDead || !health.WillAwardJournalKill ||
                    !TryGetSpecies(__instance, out string species)) return;
                bool replaced = ClaimKill(SaveState.Instance, species);
                if (onCorpseBegin?.Target != null)
                {
                    Rewards.Remove(onCorpseBegin.Target);
                    Rewards.Add(onCorpseBegin.Target, new Reward { Replaced = replaced });
                }
            }
        }

        [HarmonyPatch(typeof(EnemyDeathEffects), "RecordKillForJournal")]
        private static class ScriptedEnemyDeath
        {
            private static void Prefix(EnemyDeathEffects __instance)
            {
                if (string.IsNullOrEmpty(SaveState.Instance?.enemySoulsJson) || SaveState.Instance.enemySoulsJson == "[]") return;
                HealthManager health = __instance.GetComponent<HealthManager>();
                if (health == null || !health.hasSpecialDeath || !health.WillAwardJournalKill ||
                    !TryGetSpecies(__instance, out string species) || !EnemySoulState.HasScriptedDeath(species)) return;
                if (ClaimKill(SaveState.Instance, species))
                {
                    Rewards.Remove(health);
                    Rewards.Add(health, new Reward { Replaced = true });
                }
            }
        }

        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die), new[] {
            typeof(float?), typeof(AttackTypes), typeof(NailElements), typeof(GameObject),
            typeof(bool), typeof(float), typeof(bool), typeof(bool) })]
        private static class ScriptedDeathCurrency
        {
            private static void Prefix(HealthManager __instance) => Rewards.Remove(__instance);
            private static void Finalizer(HealthManager __instance) => Rewards.Remove(__instance);
        }

        internal static bool IsReplaced(object closure)
        {
            if (closure == null) return false;
            if (Rewards.TryGetValue(closure, out Reward reward)) return reward.Replaced;
            foreach (FieldInfo field in closure.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.FieldType.IsDefined(typeof(CompilerGeneratedAttribute), false) &&
                    field.GetValue(closure) is object parent && Rewards.TryGetValue(parent, out reward)) return reward.Replaced;
            return false;
        }

        [HarmonyPatch]
        private static class CurrencyCallbacks
        {
            private static IEnumerable<MethodBase> TargetMethods() => typeof(HealthManager)
                .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(method => method.Name.StartsWith("<Die>", StringComparison.Ordinal));

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo replacement = AccessTools.Method(typeof(EnemySoulPatches), nameof(SpawnCurrency));
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.Calls(NativeCurrency))
                    {
                        var closure = new CodeInstruction(instruction) { opcode = OpCodes.Ldarg_0, operand = null };
                        yield return closure;
                        yield return new CodeInstruction(OpCodes.Call, replacement);
                    }
                    else yield return instruction;
                }
            }
        }

        private static void SpawnCurrency(HealthManager health, Transform spawnPoint,
            float speedMin, float speedMax, float angleMin, float angleMax,
            int small, int medium, int large, int smooth, bool geoFlash, int shards, bool shardFlash, object closure)
        {
            if (!IsReplaced(closure) && !(Rewards.TryGetValue(health, out Reward reward) && reward.Replaced)) NativeCurrency.Invoke(health,
                new object[] { spawnPoint, speedMin, speedMax, angleMin, angleMax, small, medium, large, smooth, geoFlash, shards, shardFlash });
        }
    }
}
