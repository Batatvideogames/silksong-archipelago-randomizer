using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class EntranceRandomization
    {
        internal sealed class Exit
        {
            internal string Id, Scene, Gate, VanillaScene, VanillaGate, Target, TargetScene, TargetGate, Group;
            internal bool IsDoor;
        }

        private static SaveState cachedState;
        private static string cachedJson;
        private static Dictionary<string, Exit> exits = new Dictionary<string, Exit>();
        private static SaveState pendingState;
        private static Exit pendingExit;
        private static float nextSync;
        private static string pendingScene, pendingGate;
        private static readonly EntranceReturnPoint returnPoint = new EntranceReturnPoint();

        internal static bool TryGetReturnPoint(out string scene, out string gate)
        {
            scene = gate = null;
            Prepare(SaveState.Instance);
            var manager = GameManager.SilentInstance;
            var hero = HeroController.instance;
            if (exits.Count == 0 || manager == null || hero == null) return false;
            scene = manager.GetSceneNameString();
            return returnPoint.TryGet(SaveState.Instance, scene, hero.gameObject.scene.handle, out gate);
        }

        private static void ClearPending()
        {
            pendingExit = null;
            pendingState = null;
            pendingScene = pendingGate = null;
        }

        internal static string NormalizeLayout(JObject layout)
        {
            if (layout == null || layout.Count == 0 || layout.Count > 2048)
                throw new FormatException("Entrance beta layout is missing or invalid.");
            var parsed = new Dictionary<string, Exit>(StringComparer.Ordinal);
            var gates = new HashSet<string>(StringComparer.Ordinal);
            var nativeExits = new List<Exit>();
            var normalized = new JObject();
            foreach (var property in layout.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (!(property.Value is JObject row)) throw new FormatException("Invalid entrance entry.");
                var entry = new Exit {
                    Id = property.Name, Scene = Read(row, "scene"), Gate = Read(row, "gate"),
                    VanillaScene = Read(row, "vanilla_scene"), VanillaGate = Read(row, "vanilla_gate"),
                    Group = Read(row, "group"), IsDoor = ReadDoor(row),
                    Target = Read(row, "target"), TargetScene = Read(row, "target_scene"), TargetGate = Read(row, "target_gate")
                };
                var aliases = new JArray();
                foreach (var gate in ReadGates(row, entry))
                {
                    if (!MatchesGroup(gate) || !gates.Add(gate.Scene + "/" + gate.Gate))
                        throw new FormatException("Entrance beta requires unique, compatible exits.");
                    nativeExits.Add(gate);
                    if (!ReferenceEquals(gate, entry)) aliases.Add(new JObject {
                        ["gate"] = gate.Gate, ["vanilla_scene"] = gate.VanillaScene,
                        ["vanilla_gate"] = gate.VanillaGate
                    });
                }
                parsed.Add(entry.Id, entry);
                normalized.Add(entry.Id, new JObject {
                    ["scene"] = entry.Scene, ["gate"] = entry.Gate,
                    ["group"] = entry.Group, ["is_door"] = entry.IsDoor,
                    ["vanilla_scene"] = entry.VanillaScene, ["vanilla_gate"] = entry.VanillaGate,
                    ["target"] = entry.Target, ["target_scene"] = entry.TargetScene, ["target_gate"] = entry.TargetGate,
                    ["aliases"] = aliases
                });
            }
            var byGate = nativeExits.ToDictionary(e => e.Scene + "/" + e.Gate, e => e, StringComparer.Ordinal);
            foreach (var entry in nativeExits)
            {
                if (!byGate.TryGetValue(entry.VanillaScene + "/" + entry.VanillaGate, out var vanilla) ||
                    !byGate.TryGetValue(vanilla.VanillaScene + "/" + vanilla.VanillaGate, out var returning) ||
                    returning.Id != entry.Id ||
                    OppositeGroup(entry.Group) != vanilla.Group)
                    throw new FormatException("Entrance beta layout has an invalid vanilla connection.");
                if (!parsed.TryGetValue(entry.Target, out var target) || target.Target != entry.Id ||
                    target.Scene != entry.TargetScene || target.Gate != entry.TargetGate ||
                    OppositeGroup(entry.Group) != target.Group)
                    throw new FormatException("Entrance beta layout is not coupled or has incompatible directions.");
            }
            return normalized.ToString(Formatting.None);
        }

        private static IEnumerable<Exit> ReadGates(JObject row, Exit entry)
        {
            yield return entry;
            if (row["aliases"] == null) yield break;
            if (!(row["aliases"] is JArray aliases))
                throw new FormatException("Entrance beta has invalid gate aliases.");
            var rows = aliases.Select(token => token as JObject).ToArray();
            if (rows.Any(alias => alias == null))
                throw new FormatException("Entrance beta has invalid gate aliases.");
            foreach (var alias in rows.OrderBy(alias => Read(alias, "gate"), StringComparer.Ordinal))
                yield return new Exit {
                    Id = entry.Id, Scene = entry.Scene, Gate = Read(alias, "gate"),
                    VanillaScene = Read(alias, "vanilla_scene"), VanillaGate = Read(alias, "vanilla_gate"),
                    Group = entry.Group, IsDoor = entry.IsDoor, Target = entry.Target,
                    TargetScene = entry.TargetScene, TargetGate = entry.TargetGate
                };
        }

        private static string Read(JObject row, string key)
        {
            if (row[key]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)row[key]))
                throw new FormatException("Entrance beta is missing " + key + ".");
            return (string)row[key];
        }

        private static bool ReadDoor(JObject row)
        {
            if (row["is_door"]?.Type != JTokenType.Boolean)
                throw new FormatException("Entrance beta is missing its door type.");
            return (bool)row["is_door"];
        }

        private static string GateDirection(string gate)
        {
            if (gate.EndsWith(" extra", StringComparison.Ordinal))
                gate = gate.Substring(0, gate.Length - " extra".Length);
            foreach (var direction in new[] { "left", "right", "top", "bot" })
                if (gate.StartsWith(direction, StringComparison.Ordinal) && gate.Length > direction.Length &&
                    gate.Skip(direction.Length).All(c => c >= '0' && c <= '9')) return direction;
            return gate.StartsWith("door", StringComparison.Ordinal) ? "door" : null;
        }

        private static bool MatchesGroup(Exit exit)
        {
            var direction = GateDirection(exit.Gate);
            if (exit.Group == "door_in") return exit.IsDoor && direction == "door";
            if (exit.Group == "door_out") return !exit.IsDoor && (direction == "left" || direction == "right");
            return !exit.IsDoor && direction != null && direction != "door" && direction == exit.Group;
        }

        private static string OppositeGroup(string group)
        {
            switch (group)
            {
                case "left": return "right";
                case "right": return "left";
                case "top": return "bot";
                case "bot": return "top";
                case "door_in": return "door_out";
                case "door_out": return "door_in";
                default: return null;
            }
        }

        private static void Prepare(SaveState state)
        {
            if (ReferenceEquals(state, cachedState) && ReferenceEquals(state?.entranceLayoutJson, cachedJson)) return;
            var updated = new Dictionary<string, Exit>(StringComparer.Ordinal);
            if (state != null && state.IsRoomBound && state.entranceLayoutJson != "{}")
            {
                var data = JObject.Parse(NormalizeLayout(JObject.Parse(state.entranceLayoutJson)));
                foreach (var property in data.Properties())
                {
                    var row = (JObject)property.Value;
                    var entry = new Exit {
                        Id = property.Name, Scene = (string)row["scene"], Gate = (string)row["gate"],
                        VanillaScene = (string)row["vanilla_scene"], VanillaGate = (string)row["vanilla_gate"],
                        Group = (string)row["group"], IsDoor = (bool)row["is_door"],
                        Target = (string)row["target"], TargetScene = (string)row["target_scene"], TargetGate = (string)row["target_gate"]
                    };
                    foreach (var gate in ReadGates(row, entry))
                        updated.Add(gate.Scene + "/" + gate.Gate, gate);
                }
            }
            exits = updated;
            cachedState = state;
            cachedJson = state?.entranceLayoutJson;
            ClearPending();
            returnPoint.Clear();
        }

        internal static void Update()
        {
            if (Time.unscaledTime < nextSync) return;
            nextSync = Time.unscaledTime + 5f;
            Archipelago.Instance?.SynchronizeExploredEntrances();
        }

        private static Exit ApplyExit(TransitionPoint point, bool starting)
        {
            Prepare(SaveState.Instance);
            if (!exits.TryGetValue(point.gameObject.scene.name + "/" + point.gameObject.name, out var exit)) return null;
            bool vanilla = point.targetScene == exit.VanillaScene && point.entryPoint == exit.VanillaGate;
            bool mapped = point.targetScene == exit.TargetScene && point.entryPoint == exit.TargetGate;
            if ((!vanilla && !mapped) || point.isADoor != exit.IsDoor || (point.isADoor && point.isInactive))
                throw new InvalidOperationException("Entrance beta found a changed transition: " + exit.Id);
            if (starting) point.targetScene = exit.TargetScene;
            else if (point.targetScene != exit.TargetScene) point.SetTargetScene(exit.TargetScene);
            point.entryPoint = exit.TargetGate;
            return exit;
        }

        [HarmonyPatch(typeof(TransitionPoint), "Start")]
        private static class PrepareDestination
        {
            private static void Prefix(TransitionPoint __instance)
            {
                ApplyExit(__instance, true);
            }
        }

        [HarmonyPatch(typeof(TransitionPoint), "DoSceneTransition")]
        private static class Redirect
        {
            private static void Prefix(TransitionPoint __instance)
            {
                ClearPending();
                var exit = ApplyExit(__instance, false);
                if (exits.Count == 0) return;
                pendingState = SaveState.Instance;
                pendingExit = exit;
                pendingScene = __instance.targetScene;
                pendingGate = __instance.entryPoint;
            }

            private static Exception Finalizer(Exception __exception)
            {
                if (__exception != null) ClearPending();
                return __exception;
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.BeginSceneTransition))]
        private static class ConfirmDeparture
        {
            [HarmonyPriority(Priority.Last)]
            private static void Prefix(GameManager.SceneLoadInfo info)
            {
                if (pendingState == null && info != null &&
                    TryGetReturnPoint(out var scene, out var gate) && info.SceneName == scene && info.EntryGateName == gate)
                {
                    pendingState = SaveState.Instance;
                    pendingScene = scene;
                    pendingGate = gate;
                }
                if (pendingState != null && (info == null || info.SceneName != pendingScene || info.EntryGateName != pendingGate))
                    ClearPending();
                returnPoint.Clear();
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.FinishedEnteringScene))]
        private static class ConfirmArrival
        {
            private static void Postfix(GameManager __instance)
            {
                var exit = pendingExit;
                var state = pendingState;
                var scene = pendingScene;
                var gate = pendingGate;
                ClearPending();
                var hero = HeroController.instance;
                if (state == null || !ReferenceEquals(state, SaveState.Instance) ||
                    __instance.GetSceneNameString() != scene || hero == null || hero.GetEntryGateName() != gate) return;
                returnPoint.Record(state, scene, gate, hero.gameObject.scene.handle);
                if (exit == null) return;
                if (state.exploredEntrances == null) state.exploredEntrances = new HashSet<string>();
                state.exploredEntrances.Add(exit.Id);
                Archipelago.Instance?.SynchronizeExploredEntrances();
            }
        }
    }
}
