using Archipelago.MultiClient.Net.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SilksongRandomizer
{
    [Serializable]
    public sealed class VogAreaHint
    {
        public string area;
        public List<string> locations = new List<string>();
        public bool purchased;
        public List<string> progressionLocations = new List<string>();

        internal bool TrySetReport(IDictionary<string, SaveState.HintData> scouts)
        {
            if (scouts == null || locations == null || locations.Count == 0 ||
                locations.Any(name => !scouts.TryGetValue(name, out SaveState.HintData hint) || hint == null))
            {
                return false;
            }
            progressionLocations = locations.Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => (scouts[name].flags & ItemFlags.Advancement) != 0).ToList();
            return true;
        }

        internal string GetText(SaveState state)
        {
            int remaining = (progressionLocations ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(name => !state.IsLocationChecked(name));
            return area + " has " + remaining + " progression " +
                   (remaining == 1 ? "item" : "items") + " remaining.";
        }
    }

    public partial class SaveState
    {
        public int vogAreaHintCount;
        public List<VogAreaHint> vogAreaHints = new List<VogAreaHint>();

        internal void BindVogAreaHints(Archipelago client)
        {
            if (!VogAreaHintsMatch(client))
            {
                vogAreaHints = client.VogAreaHints.Select(hint => new VogAreaHint
                {
                    area = hint.area,
                    locations = new List<string>(hint.locations)
                }).ToList();
            }
            vogAreaHintCount = client.VogAreaHintCount;
        }

        internal bool VogAreaHintsMatch(Archipelago client)
        {
            var saved = vogAreaHints ?? new List<VogAreaHint>();
            return vogAreaHintCount == client.VogAreaHintCount &&
                   saved.Count == client.VogAreaHints.Count &&
                   saved.Zip(client.VogAreaHints, (a, b) => a != null &&
                       a.area == b.area && a.locations != null &&
                       a.locations.SequenceEqual(b.locations)).All(matches => matches);
        }

        internal IReadOnlyList<string> GetVogAreaReports()
        {
            return (vogAreaHints ?? new List<VogAreaHint>())
                .Where(hint => hint != null && hint.purchased)
                .Select(hint => hint.GetText(this)).ToArray();
        }
    }
}
