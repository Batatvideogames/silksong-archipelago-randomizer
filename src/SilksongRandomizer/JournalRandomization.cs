using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

namespace SilksongRandomizer
{
    internal static class JournalRandomization
    {
        internal const string Journal = "Hunter's Journal";
        internal const string JournalLocation = "Nuu - Hunter's Journal";
        internal static readonly JArray Catalogue = Load();
        private static readonly Dictionary<string, string> LocationsByRecord = Catalogue.ToDictionary(
            row => (string)row["record"], row => "Journal: " + (string)row["name"], StringComparer.Ordinal);
        private static readonly Dictionary<string, string> RecordsByItem = Catalogue.SelectMany(row =>
            ((int)row["kills_required"] > 1 ? new[] { "Journal Entry: ", "Journal Completion: " } : new[] { "Journal Entry: " })
                .Select(prefix => new KeyValuePair<string, string>(prefix + (string)row["name"], (string)row["record"])))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        internal sealed class MarkerSource
        {
            internal string Location;
            internal string Scene;
            internal string Boss;
            internal string[] Events;
            internal FieldInfo DefeatedFlag;
            internal string Persistent;
        }

        internal static readonly Dictionary<string, MarkerSource> MarkerSources = BuildMarkerSources();

        private static Dictionary<string, MarkerSource> BuildMarkerSources()
        {
            var result = new Dictionary<string, MarkerSource>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in Catalogue)
                foreach (var source in row["sources"].Where(source => source["scene"] != null))
                    foreach (string prefix in new[] { "Journal: ", "Journal Completion: " })
                    {
                        if (prefix == "Journal Completion: " && ((int)row["kills_required"] <= 1 ||
                            row["completion_boss"] != null && (string)row["completion_boss"] != (string)source["boss"])) continue;
                        string location = prefix + (string)row["name"];
                        string scene = (string)source["scene"];
                        result.Add(location + "@" + scene, new MarkerSource {
                            Location = location, Scene = scene, Boss = (string)source["boss"],
                            Events = source["events"].Values<string>().ToArray(),
                            DefeatedFlag = typeof(PlayerData).GetField((string)source["defeated_flag"] ?? string.Empty),
                            Persistent = (string)source["persistent"]
                        });
                    }
            return result;
        }

        internal static string MarkerLogicName(string location, string scene)
        {
            string key = location + "@" + scene;
            return MarkerSources.ContainsKey(key) ? key : location;
        }

        internal static bool IsMarkerAvailable(SaveState state, string location, string scene)
        {
            if (!MarkerSources.TryGetValue(MarkerLogicName(location, scene), out var source)) return true;
            if (source.Persistent != null)
                return SceneData.instance == null || !SceneData.instance.PersistentBools.GetValueOrDefault(source.Scene, source.Persistent);
            if (state?.progressionShuffle?.TryGetBossDefeat(source.Boss, out bool defeated) == true) return !defeated;
            return source.DefeatedFlag == null || !(bool)source.DefeatedFlag.GetValue(PlayerData.instance);
        }

        private static JArray Load()
        {
            using (var reader = new StreamReader(typeof(JournalRandomization).Assembly.GetManifestResourceStream(
                "SilksongRandomizer.BossJournal.json")))
                return JArray.Parse(reader.ReadToEnd());
        }

        internal static bool Enabled => SaveState.Instance?.IsRandomized(ItemType.Journal) == true;

        internal static bool IsRandomizedRecord(string record) => Enabled && record != null &&
            LocationsByRecord.TryGetValue(record, out string location) && SaveState.Instance.IsLocationInSeed(location);

        internal static bool HasEntry(string record) => LocationsByRecord.TryGetValue(record, out string location) &&
            SaveState.Instance?.receivedItems?.Contains("Journal Entry: " + location.Substring("Journal: ".Length)) == true;

        internal static int VisibleKills(string record, int required, int nativeKills)
        {
            if (!HasEntry(record)) return 0;
            string completion = "Journal Completion: " + LocationsByRecord[record].Substring("Journal: ".Length);
            if (required <= 1) return 1;
            if (!SaveState.Instance.IsLocationInSeed(completion)) return Math.Max(1, nativeKills);
            return SaveState.Instance.receivedItems.Contains(completion) ? required : 1;
        }

        internal static void ReportKill(string record, int kills, int required)
        {
            if (!IsRandomizedRecord(record) || kills <= 0) return;
            string location = LocationsByRecord[record];
            Report(location);
            if (required > 1 && kills >= required)
                Report("Journal Completion: " + location.Substring("Journal: ".Length));
        }

        internal static void Report(string location)
        {
            var state = SaveState.Instance;
            if (!Enabled || !state.IsLocationInSeed(location) || state.IsLocationChecked(location)) return;
            try
            {
                state.CheckLocation(location);
                GameManager.instance?.QueueSaveGame();
            }
            catch (Exception exception)
            {
                RandomizerPlugin.Log?.LogError("Could not report " + location + ": " + exception);
            }
        }

        internal static IEnumerable<Item> Items()
        {
            yield return new Item(Journal, ItemType.Journal, () => PlayerData.instance.hasJournal = true);
            foreach (var pair in RecordsByItem)
                yield return new Item(pair.Key, ItemType.Journal, () => { });
        }

        internal static IEnumerable<Location> Locations()
        {
            yield return new Location(JournalLocation, ItemType.Journal, null);
            foreach (string location in LocationsByRecord.Values)
                yield return new Location(location, ItemType.Journal, null);
            foreach (string item in RecordsByItem.Keys.Where(name => name.StartsWith("Journal Completion: ", StringComparison.Ordinal)))
                yield return new Location(item, ItemType.Journal, null);
        }

        internal static IEnumerable<MapCheckPosition> MapPositions(IEnumerable<MapCheckPosition> anchors)
        {
            yield return new MapCheckPosition("Journal: Moss Mother", "Weave_03", 13f, 31.12f, 284f, 35f, MapMarkerPositionConfidence.ExactUpstream);
            yield return new MapCheckPosition("Journal Completion: Moss Mother", "Weave_03", 13f, 31.12f, 284f, 35f, MapMarkerPositionConfidence.ExactUpstream);
            foreach (MapCheckPosition anchor in anchors)
            {
                foreach (var row in Catalogue)
                {
                    if (!row["sources"].Any(source => (string)source["boss"] == anchor.LocationName &&
                        (source["scene"] == null || (string)source["scene"] == anchor.SceneName))) continue;
                    yield return CopyPosition("Journal: " + (string)row["name"], anchor);
                    if ((int)row["kills_required"] > 1 &&
                        (row["completion_boss"] == null || (string)row["completion_boss"] == anchor.LocationName))
                        yield return CopyPosition("Journal Completion: " + (string)row["name"], anchor);
                }
                if (anchor.LocationName == "Wish: Bugs of Pharloom")
                    yield return CopyPosition(JournalLocation, anchor);
            }
        }

        private static MapCheckPosition CopyPosition(string location, MapCheckPosition anchor) =>
            new MapCheckPosition(location, anchor.SceneName, anchor.PositionInScene.x, anchor.PositionInScene.y,
                anchor.SceneSize.x, anchor.SceneSize.y, anchor.Confidence);

        internal static Sprite GetIcon(string item)
        {
            if (item == null || !RecordsByItem.ContainsKey(item)) return null;
            return ItemIcons.GetSprite("Journal Entry", null);
        }
    }
}

namespace SilksongRandomizer.Patches
{
    internal static class JournalPatches
    {
        [ThreadStatic] private static int nativeAwardDepth;

        [HarmonyPatch(typeof(EnemyJournalManager), nameof(EnemyJournalManager.GetKillData))]
        private static class EntryDataPatch
        {
            [HarmonyPostfix]
            private static void Postfix(EnemyJournalRecord journalRecord, ref EnemyJournalKillData.KillData __result)
            {
                if (nativeAwardDepth != 0 || journalRecord == null ||
                    !JournalRandomization.IsRandomizedRecord(journalRecord.name)) return;
                __result.Kills = JournalRandomization.VisibleKills(journalRecord.name, journalRecord.KillsRequired, __result.Kills);
            }
        }

        [HarmonyPatch(typeof(EnemyJournalManager), nameof(EnemyJournalManager.RecordKill),
            new Type[] { typeof(EnemyJournalRecord), typeof(bool), typeof(bool) })]
        private static class RecordKillPatch
        {
            [HarmonyPrefix]
            private static void Prefix(EnemyJournalRecord journalRecord, ref bool showPopup, out int __state)
            {
                __state = nativeAwardDepth;
                nativeAwardDepth++;
                if (journalRecord == null || !JournalRandomization.IsRandomizedRecord(journalRecord.name)) return;
                showPopup = false;
            }

            [HarmonyPostfix]
            private static void Postfix(EnemyJournalRecord journalRecord)
            {
                var data = GameManager.instance?.playerData?.EnemyJournalKillData;
                if (journalRecord != null && data != null)
                    JournalRandomization.ReportKill(journalRecord.name, data.GetKillData(journalRecord.name).Kills, journalRecord.KillsRequired);
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(Exception __exception, int __state)
            {
                nativeAwardDepth = __state;
                return __exception;
            }
        }

        // Full-entry awards loop over KillCount. They still need the real kills.
        [HarmonyPatch]
        private static class FullEntryAwardPatch
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(CompleteJournalRecord), "OnEnter");
                yield return AccessTools.Method(typeof(CompleteJournalRecordV2), "OnEnter");
                yield return AccessTools.Method(typeof(EnemyDeathEffects), "RecordKillForJournal");
            }

            [HarmonyPrefix]
            private static void Prefix(out int __state)
            {
                __state = nativeAwardDepth;
                nativeAwardDepth++;
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(Exception __exception, int __state)
            {
                nativeAwardDepth = __state;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class NuuJournalPatch
        {
            [HarmonyPostfix]
            private static void Postfix(PlayMakerFSM __instance)
            {
                if (!string.Equals(__instance.gameObject.scene.name, "Halfway_01", StringComparison.OrdinalIgnoreCase) || __instance.FsmName != "Dialogue" ||
                    Utils.GetHierarchyPath(__instance.transform) != "_NPCs/Hunter Fan Control/Nuu") return;
                foreach (FsmState state in __instance.FsmStates)
                {
                    if (state.Name != "Journal" && state.Name != "Has Journal?") continue;
                    for (int i = 0; i < state.Actions.Length; i++)
                    {
                        var action = state.Actions[i];
                        if (state.Name == "Journal" && action is HutongGames.PlayMaker.Actions.SetPlayerDataBool set && set.boolName.Value == "hasJournal")
                        {
                            var replacement = new GiveJournalCheck(action);
                            replacement.Init(state);
                            state.Actions[i] = replacement;
                        }
                        else if (state.Name == "Has Journal?" && action is PlayerDataVariableTest test && test.VariableName.Value == "hasJournal")
                        {
                            var replacement = new TestJournalSource(action);
                            replacement.Init(state);
                            state.Actions[i] = replacement;
                        }
                    }
                }
            }
        }

        private sealed class GiveJournalCheck : FsmStateAction
        {
            private readonly FsmStateAction original;
            internal GiveJournalCheck(FsmStateAction original) { this.original = original; }
            public override void OnEnter()
            {
                if (!JournalRandomization.Enabled) { original.OnEnter(); Finish(); return; }
                JournalRandomization.Report(JournalRandomization.JournalLocation);
                Fsm.SetState("Talk Journal Given");
                Finish();
            }
        }

        private sealed class TestJournalSource : FsmStateAction
        {
            private readonly FsmStateAction original;
            internal TestJournalSource(FsmStateAction original) { this.original = original; }
            public override void OnEnter()
            {
                if (!JournalRandomization.Enabled) original.OnEnter();
                else Fsm.Event(SaveState.Instance.IsLocationChecked(JournalRandomization.JournalLocation) ? "TRUE" : "FALSE");
                Finish();
            }
        }
    }
}
