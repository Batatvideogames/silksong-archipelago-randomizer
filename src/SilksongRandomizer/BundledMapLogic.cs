using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace SilksongRandomizer
{
    internal static class BundledMapLogic
    {
        private sealed class LogicData
        {
            internal readonly string Identity;
            internal readonly JObject Payload;

            internal LogicData()
            {
                using (Stream stream = typeof(BundledMapLogic).Assembly
                    .GetManifestResourceStream("SilksongRandomizer.MapLogic.json"))
                using (MemoryStream buffer = new MemoryStream())
                {
                    if (stream == null)
                        throw new InvalidOperationException("Bundled map logic is missing.");
                    stream.CopyTo(buffer);
                    byte[] bytes = buffer.ToArray();
                    using (SHA256 hash = SHA256.Create())
                        Identity = BitConverter.ToString(hash.ComputeHash(bytes))
                            .Replace("-", "").ToLowerInvariant();
                    Payload = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes));
                }
            }
        }

        private static readonly Lazy<LogicData> Data = new Lazy<LogicData>(() => new LogicData());

        internal static JObject Restore(string identity, JObject changes, bool scuttlebrace, string entranceScope, JObject entrancePairs)
        {
            LogicData data = Data.Value;
            if (!string.Equals(identity, data.Identity, StringComparison.Ordinal))
                throw new FormatException("Map logic does not match this mod. Install the mod supplied with this APWorld.");
            if (changes == null || changes.Properties().Any(p => ((JObject)data.Payload["logic"]).Property(p.Name) == null))
                throw new FormatException("Invalid map logic overrides.");
            JObject result = Prepare(data.Payload, scuttlebrace, entranceScope, entrancePairs);
            Merge(result, changes);
            if (!(result["logic_event_order"] is JArray order) ||
                !(result["logic_events"] is JObject events))
                throw new FormatException("Invalid map logic event data.");
            JArray activeEvents = new JArray();
            foreach (JToken entry in order)
            {
                if (entry.Type != JTokenType.String || !(events[entry.Value<string>()] is JObject value))
                    throw new FormatException("Invalid map logic event reference.");
                activeEvents.Add(value.DeepClone());
            }
            result.Remove("logic_event_order");
            result["logic_events"] = activeEvents;
            return result;
        }

        private static JObject Prepare(JObject data, bool scuttlebrace, string scope, JObject pairs)
        {
            JObject result = (JObject)data["logic"].DeepClone();
            if (scope != null)
            {
                if (!(data["entrance_profiles"]?[scope] is JObject profile) || pairs == null)
                    throw new FormatException("Invalid entrance map logic settings.");
                Merge(result, (JObject)profile["changes"]);
                JObject abstracts = (JObject)result["abstract_requirements"];
                foreach (JObject source in (JArray)profile["sources"])
                {
                    string id = source.Value<string>("id");
                    JToken pair = pairs[id];
                    if (pair != null && pair.Type != JTokenType.String)
                        throw new FormatException("Invalid entrance map logic destination.");
                    string target = pair?.Value<string>() ?? source.Value<string>("vanilla");
                    string destination = profile["destinations"]?[target]?.Value<string>();
                    if (destination == null)
                        throw new FormatException("Unknown entrance map logic destination.");
                    if (!(abstracts[destination] is JObject group))
                    {
                        group = new JObject { ["alternatives"] = new JArray() };
                        abstracts[destination] = group;
                    }
                    JArray alternatives = (JArray)group["alternatives"];
                    foreach (JToken requirement in (JArray)source["requirements"])
                        if (!alternatives.Any(existing => JToken.DeepEquals(existing, requirement)))
                            alternatives.Add(requirement.DeepClone());
                }
            }
            if (!scuttlebrace)
            {
                ((JObject)result["abstract_requirements"]).Remove("Usable Scuttlebrace");
                var groups = ((JObject)result["requirements"]).Properties().Select(p => (JObject)p.Value)
                    .Concat(((JObject)result["abstract_requirements"]).Properties().Select(p => (JObject)p.Value))
                    .Concat(((JObject)result["logic_events"]).Properties().Select(p => (JObject)p.Value["requirement"]));
                foreach (JObject group in groups)
                {
                    JArray alternatives = new JArray();
                    foreach (JObject requirement in (JArray)group["alternatives"])
                    {
                        if (((JArray)requirement["all_of"]).Values<string>().Contains("Usable Scuttlebrace"))
                            continue;
                        JArray any = (JArray)requirement["any_of"];
                        JArray filtered = new JArray(any.Values<string>().Where(name => name != "Usable Scuttlebrace"));
                        if (any.Count > 0 && filtered.Count == 0)
                            continue;
                        requirement["any_of"] = filtered;
                        alternatives.Add(requirement);
                    }
                    group["alternatives"] = alternatives;
                }
            }
            return result;
        }

        private static void Merge(JObject target, JObject changes)
        {
            foreach (JProperty property in changes.Properties())
            {
                if (property.Value.Type == JTokenType.Null)
                    target.Remove(property.Name);
                else if (property.Value is JObject child)
                {
                    JObject current = target[property.Name] as JObject ?? new JObject();
                    Merge(current, child);
                    target[property.Name] = current;
                }
                else
                    target[property.Name] = property.Value.DeepClone();
            }
        }
    }
}
