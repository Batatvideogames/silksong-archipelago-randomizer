using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class NpcSoulState
    {
        internal static readonly JArray Catalogue = Load();
        internal static readonly string[] Names = Catalogue.Where(row => (bool?)row["legacy"] != true).Select(row => (string)row["name"]).ToArray();
        internal static readonly Dictionary<string, string[]> Actors = BuildIndex("actors");
        private static readonly Dictionary<string, string[]> Interactions = BuildIndex("interactions");
        private static readonly Dictionary<string, string[]> States = BuildIndex("states");
        private static string cachedJson;
        private static HashSet<string> cachedNames = new HashSet<string>(StringComparer.Ordinal);

        private static JArray Load()
        {
            using (Stream stream = typeof(NpcSoulState).Assembly.GetManifestResourceStream("SilksongRandomizer.NpcSouls.json"))
            using (var reader = new StreamReader(stream)) return JArray.Parse(reader.ReadToEnd());
        }

        private static Dictionary<string, string[]> BuildIndex(string field)
        {
            var entries = new List<KeyValuePair<string, string>>();
            foreach (JToken row in Catalogue)
                foreach (JToken actor in row[field] ?? new JArray())
                {
                    string key = (string)actor["scene"] + "|" + (string)actor["path"];
                    if (field == "states")
                        foreach (string state in actor["states"].Values<string>())
                            entries.Add(new KeyValuePair<string, string>(key + "|" + (string)actor["fsm"] + "|" + state, (string)row["name"]));
                    else entries.Add(new KeyValuePair<string, string>(key, (string)row["name"]));
                }
            return entries.GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(entry => entry.Value).Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);
        }

        internal static bool MissingAny(SaveState state, IEnumerable<string> npcs) => npcs.Any(npc => IsMissing(state, npc));

        internal static bool CanEnterState(string scene, string path, string fsm, string name, SaveState state) =>
            !States.TryGetValue(scene + "|" + path + "|" + fsm + "|" + name, out string[] npcs) || !MissingAny(state, npcs);

        internal static bool CanInteract(Transform transform)
        {
            SaveState state = SaveState.Instance;
            if (string.IsNullOrEmpty(state?.npcSoulsJson) || state.npcSoulsJson == "[]") return true;
            for (Transform current = transform; current != null; current = current.parent)
            {
                string key = current.gameObject.scene.name + "|" + Utils.GetHierarchyPath(current);
                if (Interactions.TryGetValue(key, out string[] npcs) && MissingAny(SaveState.Instance, npcs)) return false;
                if (Actors.TryGetValue(key, out npcs) && MissingAny(SaveState.Instance, npcs)) return false;
            }
            return true;
        }

        internal static string ItemName(string npc) => "NPC Soul: " + npc;

        internal static string NormalizeConfiguration(string json)
        {
            var values = JToken.Parse(string.IsNullOrEmpty(json) ? "[]" : json) as JArray;
            if (values == null || values.Any(value => value.Type != JTokenType.String))
                throw new FormatException("NPC Souls must list supported NPC identities.");
            string[] names = values.Values<string>().ToArray();
            if (names.Distinct(StringComparer.Ordinal).Count() != names.Length || names.Any(name => !Catalogue.Any(row => (string)row["name"] == name)))
                throw new FormatException("This seed needs a different NPC Souls build.");
            return new JArray(names.Where(Names.Contains).OrderBy(name => name, StringComparer.Ordinal)).ToString(Formatting.None);
        }

        internal static bool IsMissing(SaveState state, string npc)
        {
            if (state == null || string.IsNullOrEmpty(state.npcSoulsJson) || state.npcSoulsJson == "[]") return false;
            if (cachedJson != state.npcSoulsJson)
            {
                cachedNames = new HashSet<string>(JArray.Parse(state.npcSoulsJson).Values<string>(), StringComparer.Ordinal);
                cachedJson = state.npcSoulsJson;
            }
            return cachedNames.Contains(npc) && state.receivedItems?.Contains(ItemSet.GetCanonicalItemName(ItemName(npc))) != true;
        }

        internal static bool FindActor(Transform transform, out GameObject actor, out string[] npcs)
        {
            actor = null;
            npcs = null;
            if (string.IsNullOrEmpty(SaveState.Instance?.npcSoulsJson) || SaveState.Instance.npcSoulsJson == "[]") return false;
            for (Transform current = transform; current != null; current = current.parent)
                if (Actors.TryGetValue(current.gameObject.scene.name + "|" + Utils.GetHierarchyPath(current), out npcs))
                {
                    actor = current.gameObject;
                    return true;
                }
            return false;
        }
    }
}
