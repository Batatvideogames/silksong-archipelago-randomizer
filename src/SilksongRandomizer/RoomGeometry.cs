using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SilksongRandomizer
{
    internal sealed class RoomGeometry
    {
        internal sealed class Section
        {
            internal string Id, Name;
            internal bool HasBounds;
            internal double X, Y, Width, Height;

            internal bool Contains(double x, double y) => HasBounds &&
                x >= X && x <= X + Width && y >= Y && y <= Y + Height;
        }

        internal sealed class Room
        {
            internal string Area, Name, Id;
            internal readonly List<Section> Sections = new List<Section>();
        }

        internal sealed class Reading
        {
            internal string Heading, Detail, Tracker;
        }

        internal readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>(StringComparer.Ordinal);

        internal static RoomGeometry Load(JObject data)
        {
            if (data == null || data.Value<int>("schema_version") != 2 || !(data["rooms"] is JArray rooms))
                throw new InvalidDataException("Unsupported room geometry.");
            var result = new RoomGeometry();
            foreach (JObject source in rooms)
            {
                string scene = source.Value<string>("in_game_id")?.Trim();
                if (string.IsNullOrEmpty(scene))
                    continue;
                var room = new Room
                {
                    Id = source.Value<string>("id"),
                    Area = source.Value<string>("room_group_name"),
                    Name = source.Value<string>("name")
                };
                foreach (JObject node in (JArray)source["nodes"])
                {
                    var section = new Section
                    {
                        Id = node.Value<string>("id"),
                        Name = node.Value<string>("name")
                    };
                    double? x = Number(node["annotation_x"]), y = Number(node["annotation_y"]),
                        width = Number(node["annotation_width"]), height = Number(node["annotation_height"]);
                    section.HasBounds = node.Value<bool?>("annotation_enabled") == true &&
                        x.HasValue && y.HasValue && width > 0 && height > 0;
                    if (section.HasBounds)
                    {
                        section.X = x.Value;
                        section.Y = y.Value;
                        section.Width = width.Value;
                        section.Height = height.Value;
                    }
                    room.Sections.Add(section);
                }
                result.Rooms.Add(scene, room);
            }
            return result;
        }

        private static double? Number(JToken value)
        {
            if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float))
                return null;
            double number = value.Value<double>();
            return double.IsNaN(number) || double.IsInfinity(number) ? (double?)null : number;
        }

        internal Reading Locate(string scene, double x, double y)
        {
            if (!Rooms.TryGetValue(scene ?? "", out var room))
                return new Reading { Heading = "Unmapped room", Detail = "No room mapping for this scene", Tracker = "" };
            var matches = room.Sections.Where(s => s.Contains(x, y)).ToArray();
            if (matches.Length == 0 && room.Sections.Count == 1 && !room.Sections[0].HasBounds &&
                room.Sections[0].Id == room.Id + "#room")
                matches = new[] { room.Sections[0] };
            string detail, tracker;
            if (matches.Length == 1)
            {
                detail = "Subroom: " + matches[0].Name;
                tracker = "Room Node: " + matches[0].Id;
            }
            else if (matches.Length > 1)
            {
                detail = "Overlapping subrooms: " + string.Join(" / ", matches.Select(s => s.Name));
                tracker = string.Join("\n", matches.Select(s => "Room Node: " + s.Id));
            }
            else
            {
                var unbounded = room.Sections.Where(s => !s.HasBounds).ToArray();
                detail = room.Sections.Any(s => s.HasBounds)
                    ? "Subroom: outside mapped boundaries"
                    : "Subroom unknown (boundaries not mapped)";
                if (unbounded.Length > 0)
                    detail += "\nUnmapped sections: " + string.Join(" / ", unbounded.Select(s => s.Name));
                tracker = "Room: " + room.Id;
            }
            return new Reading { Heading = room.Area + "  /  " + room.Name, Detail = detail, Tracker = tracker };
        }
    }
}
