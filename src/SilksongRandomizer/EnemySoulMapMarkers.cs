using System;
using System.Collections.Generic;
using System.Linq;

namespace SilksongRandomizer
{
    internal static class EnemySoulMapMarkers
    {
        internal sealed class Source
        {
            internal string Location, Species, Room, Scene;
        }

        private static readonly Dictionary<string, string> SpeciesByLocation = EnemySoulState.Names
            .ToDictionary(EnemySoulState.LocationName, name => name, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SoulItems = new HashSet<string>(
            EnemySoulState.Names.Select(EnemySoulState.ItemName), StringComparer.OrdinalIgnoreCase);
        internal static readonly Dictionary<string, Source> Sources = BuildSources();

        private static Dictionary<string, Source> BuildSources()
        {
            var rooms = BundledMapLogic.RoomGeometry["rooms"].ToDictionary(
                room => (string)room["id"], room => (string)room["in_game_id"], StringComparer.Ordinal);
            var result = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
            foreach (var enemy in EnemySoulState.Catalogue)
            {
                string species = (string)enemy["name"];
                string location = EnemySoulState.LocationName(species);
                foreach (string room in enemy["routes"].SelectMany(route => route["nodes"])
                    .Select(node => ((string)node).Split('#')[0]).Distinct())
                {
                    if (!rooms.TryGetValue(room, out string scene) || string.IsNullOrWhiteSpace(scene)) continue;
                    if (scene == "Memory_Ant_Queen") scene = "Ant_Queen";
                    result[location + "@" + scene] = new Source {
                        Location = location, Species = species, Room = room, Scene = scene
                    };
                }
            }
            return result;
        }

        internal static bool IsLocation(string location) => SpeciesByLocation.ContainsKey(location);
        internal static bool IsSoulItem(string item) => item != null && SoulItems.Contains(item);

        internal static bool IsAvailable(SaveState state, string location) =>
            !SpeciesByLocation.TryGetValue(location, out string species) ||
            EnemySoulState.IsFirstKillPending(state, species) && !EnemySoulState.IsMissing(state, species);

        internal static IEnumerable<MapCheckPosition> Positions(SaveState state)
        {
            var available = new HashSet<string>(SpeciesByLocation.Keys.Where(location => IsAvailable(state, location)));
            foreach (Source source in Sources.Values)
                if (available.Contains(source.Location))
                    yield return new MapCheckPosition(source.Location, source.Scene, .5f, .5f, 1f, 1f,
                        MapMarkerPositionConfidence.RoomCenter);
        }

        internal static string LogicName(string location, string scene)
        {
            string key = location + "@" + scene;
            return Sources.ContainsKey(key) ? key : JournalRandomization.MarkerLogicName(location, scene);
        }
    }
}
