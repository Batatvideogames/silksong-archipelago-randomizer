using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class EnemySoulState
    {
        internal static readonly JArray Catalogue = Load();
        internal static readonly string[] Names = Catalogue.Select(row => (string)row["name"]).ToArray();
        private static readonly Dictionary<string, string> Records = Catalogue.ToDictionary(row => (string)row["record"], row => (string)row["name"], StringComparer.Ordinal);
        private static readonly HashSet<string> ScriptedDeaths = new HashSet<string>(
            Catalogue.Where(row => (bool?)row["scripted_death"] == true).Select(row => (string)row["name"]), StringComparer.Ordinal);
        internal static bool HasScriptedDeath(string species) => ScriptedDeaths.Contains(species);
        private static string cachedJson;
        private static HashSet<string> cachedNames = new HashSet<string>(StringComparer.Ordinal);

        private static JArray Load()
        {
            using (Stream stream = typeof(EnemySoulState).Assembly.GetManifestResourceStream("SilksongRandomizer.EnemySouls.json"))
            using (var reader = new StreamReader(stream)) return JArray.Parse(reader.ReadToEnd());
        }

        internal static string ItemName(string npc) => "Enemy Soul: " + npc;

        internal static string NormalizeConfiguration(string json)
        {
            var values = JToken.Parse(string.IsNullOrEmpty(json) ? "[]" : json) as JArray;
            if (values == null || values.Any(value => value.Type != JTokenType.String))
                throw new FormatException("Enemy Souls must list supported enemy species.");
            string[] names = values.Values<string>().ToArray();
            if (names.Distinct(StringComparer.Ordinal).Count() != names.Length || names.Any(name => !Names.Contains(name)))
                throw new FormatException("This seed needs a different Enemy Souls build.");
            return new JArray(names.OrderBy(name => name, StringComparer.Ordinal)).ToString(Formatting.None);
        }

        internal static bool IsMissing(SaveState state, string npc)
        {
            if (state == null || string.IsNullOrEmpty(state.enemySoulsJson) || state.enemySoulsJson == "[]") return false;
            if (cachedJson != state.enemySoulsJson)
            {
                cachedNames = new HashSet<string>(JArray.Parse(state.enemySoulsJson).Values<string>(), StringComparer.Ordinal);
                cachedJson = state.enemySoulsJson;
            }
            return cachedNames.Contains(npc) && state.receivedItems?.Contains(ItemSet.GetCanonicalItemName(ItemName(npc))) != true;
        }

        internal static Sprite GetIcon(string itemName)
        {
            const string prefix = "Enemy Soul: ";
            if (!itemName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            string species = itemName.Substring(prefix.Length);
            string recordName = Records.FirstOrDefault(pair => string.Equals(pair.Value, species, StringComparison.OrdinalIgnoreCase)).Key;
            EnemyJournalRecord record = recordName == null ? null : EnemyJournalManager.GetRecord(recordName);
            return record != null ? record.IconSprite : null;
        }

        internal static string LocationName(string name) => "First Kill: " + name;

        internal static bool TryGetSpecies(EnemyJournalRecord record, out string species)
        {
            species = null;
            return record != null && Records.TryGetValue(record.name, out species);
        }

        internal static bool IsEnabled(SaveState state, string name)
        {
            if (state == null || string.IsNullOrEmpty(state.enemySoulsJson) || state.enemySoulsJson == "[]") return false;
            IsMissing(state, name);
            return cachedNames.Contains(name);
        }
    }
}
