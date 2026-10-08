using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SilksongRandomizer
{
    internal static class BossSoulState
    {
        internal const string SethSoul = "Progressive Seth Soul";
        internal const string SethBoss = "Boss: Shrine Guardian Seth";

        internal static bool ProgressiveSeth(SaveState state) => state != null &&
            JArray.Parse(string.IsNullOrEmpty(state.bossSoulsJson) ? "[]" : state.bossSoulsJson).Values<string>().Contains(SethBoss) &&
            JArray.Parse(string.IsNullOrEmpty(state.npcSoulsJson) ? "[]" : state.npcSoulsJson).Values<string>().Contains("Seth");

        internal const string Groal = "Boss: Groal the Great";
        internal static readonly JArray Catalogue = Load();
        internal static readonly string[] SupportedBosses = Catalogue.Select(row => (string)row["boss"]).ToArray();

        private static JArray Load()
        {
            using (Stream stream = typeof(BossSoulState).Assembly.GetManifestResourceStream("SilksongRandomizer.BossSouls.json"))
            using (var reader = new StreamReader(stream)) return JArray.Parse(reader.ReadToEnd());
        }

        internal static string ItemName(string boss) => "Soul of " +
            (boss.StartsWith("Boss: Skull Tyrant (", StringComparison.Ordinal) ? "Skull Tyrant" : boss.Substring("Boss: ".Length));

        internal static string NormalizeConfiguration(string json)
        {
            var values = JToken.Parse(string.IsNullOrEmpty(json) ? "[]" : json) as JArray;
            if (values == null || values.Any(value => value.Type != JTokenType.String))
                throw new FormatException("Boss Souls must list supported boss identities.");
            var names = values.Values<string>().ToArray();
            if (names.Distinct(StringComparer.Ordinal).Count() != names.Length ||
                names.Any(name => !SupportedBosses.Contains(name)))
                throw new FormatException("This seed needs a different Boss Souls build.");
            return new JArray(names.OrderBy(name => name, StringComparer.Ordinal)).ToString(Formatting.None);
        }

        internal static bool IsMissing(SaveState state, string boss)
        {
            if (state == null || string.IsNullOrEmpty(state.bossSoulsJson) || state.bossSoulsJson == "[]")
                return false;
            if (boss == SethBoss && ProgressiveSeth(state)) return state.GetReceivedItemCount(SethSoul) < 1;
            return JArray.Parse(state.bossSoulsJson).Values<string>().Contains(boss) &&
                state.receivedItems?.Contains(ItemSet.GetCanonicalItemName(ItemName(boss))) != true;
        }
    }
}
