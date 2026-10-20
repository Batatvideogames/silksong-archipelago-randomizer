using System;
using System.IO;
using System.IO.Compression;
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
                    string json = System.Text.Encoding.UTF8.GetString(buffer.ToArray())
                        .Replace("\r\n", "\n").Replace("\r", "\n");
                    using (SHA256 hash = SHA256.Create())
                        Identity = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json)))
                            .Replace("-", "").ToLowerInvariant();
                    Payload = JObject.Parse(json);
                }
            }
        }

        private static readonly Lazy<LogicData> Data = new Lazy<LogicData>(() => new LogicData());

        internal static JObject Restore(string identity, JObject changes, bool scuttlebrace, string entranceScope, JObject entrancePairs)
        {
            LogicData data = Data.Value;
            if (!string.Equals(identity, data.Identity, StringComparison.Ordinal))
                throw new FormatException("Map logic does not match this mod. Install the mod supplied with this APWorld.");
            changes = Expand(changes);
            if (changes == null || changes.Properties().Any(p => ((JObject)data.Payload["logic"]).Property(p.Name) == null))
                throw new FormatException("Invalid map logic overrides.");
            JObject result = Prepare(data.Payload, scuttlebrace, entranceScope, entrancePairs);
            Merge(result, changes);
            RestoreDefaults(result);
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

        private static JObject Expand(JObject changes)
        {
            if (changes?["encoding"] == null)
                return changes;
            if (changes.Count != 2 || changes.Value<string>("encoding") != "gzip+base64" ||
                changes["data"]?.Type != JTokenType.String)
                throw new FormatException("Invalid compressed map logic.");
            using (var input = new MemoryStream(Convert.FromBase64String(changes.Value<string>("data"))))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int count;
                while ((count = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + count > 64 * 1024 * 1024)
                        throw new FormatException("Map logic exceeds the size limit.");
                    output.Write(buffer, 0, count);
                }
                return JObject.Parse(System.Text.Encoding.UTF8.GetString(output.ToArray()));
            }
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
                foreach (JObject group in RequirementGroups(result))
                {
                    JArray alternatives = (JArray)group["alternatives"];
                    for (int index = alternatives.Count - 1; index >= 0; index--)
                    {
                        JObject requirement = (JObject)alternatives[index];
                        if (requirement["all_of"] is JArray all && all.Values<string>().Contains("Usable Scuttlebrace"))
                        {
                            alternatives.RemoveAt(index);
                            continue;
                        }
                        JArray any = (JArray)requirement["any_of"];
                        if (any == null)
                            continue;
                        bool hadAlternatives = any.Count > 0;
                        for (int item = any.Count - 1; item >= 0; item--)
                        {
                            if (any[item].Value<string>() == "Usable Scuttlebrace")
                                any.RemoveAt(item);
                        }
                        if (hadAlternatives && any.Count == 0)
                            alternatives.RemoveAt(index);
                    }
                }
            }
            return result;
        }

        private static System.Collections.Generic.IEnumerable<JObject> RequirementGroups(JObject payload, bool includeSilk = true)
        {
            return ((JObject)payload["requirements"]).Properties().Select(p => (JObject)p.Value)
                .Concat(((JObject)payload["abstract_requirements"]).Properties()
                    .Where(p => includeSilk || !p.Name.StartsWith("Silk ", StringComparison.Ordinal))
                    .Select(p => (JObject)p.Value))
                .Concat(((JObject)payload["logic_events"]).Properties().Select(p => (JObject)p.Value["requirement"]));
        }

        private static void RestoreDefaults(JObject payload)
        {
            var defaults = new JObject
            {
                ["all_of"] = new JArray(), ["any_of"] = new JArray(),
                ["required_locations"] = new JArray(), ["item_counts"] = new JArray(),
                ["require_any_crest"] = false, ["require_silk_spear"] = false,
                ["minimum_skip_tier"] = 0, ["act"] = "", ["path"] = "",
            };
            foreach (JObject group in RequirementGroups(payload, includeSilk: false))
                foreach (JObject rule in (JArray)group["alternatives"])
                    foreach (JProperty field in defaults.Properties())
                        if (rule.Property(field.Name) == null)
                            rule[field.Name] = field.Value.DeepClone();
        }

        private static void Merge(JObject target, JObject changes)
        {
            foreach (JProperty property in changes.Properties())
            {
                if (property.Value.Type == JTokenType.Null)
                    target.Remove(property.Name);
                else if (property.Value is JObject child)
                {
                    if (target[property.Name] is JObject current)
                    {
                        Merge(current, child);
                    }
                    else
                    {
                        JObject added = new JObject();
                        Merge(added, child);
                        target[property.Name] = added;
                    }
                }
                else
                    target[property.Name] = property.Value.DeepClone();
            }
        }
    }
}
