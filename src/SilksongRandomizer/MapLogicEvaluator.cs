using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilksongRandomizer
{
    internal enum MapCheckReachability
    {
        Unknown = 0,
        Reachable = 1,
        Unreachable = 2,
    }

    internal sealed class LogicTooltipLine
    {
        internal readonly string Text;
        internal readonly Color Color;

        internal LogicTooltipLine(string text, Color color)
        {
            Text = text ?? string.Empty;
            Color = color;
        }
    }

    /// <summary>
    /// Evaluates the exact APWorld graph exported with the room. This is a
    /// local, spoiler-free calculation: it reads received items and never
    /// scouts the item placed at a location.
    /// </summary>
    internal static class MapLogicEvaluator
    {
        private const int MaxMossberryCount = 7;
        private const int MaxPollipHeartCount = 6;
        private const int MaxPaleOilCount = 3;
        private const string UsableScuttlebraceRequirement =
            "Usable Scuttlebrace";

        private sealed class LogicPayload
        {
            [JsonProperty("skips")]
            internal int SkipsTier { get; set; }

            [JsonProperty(
                "scuttlebrace_logic",
                Required = Required.Always
            )]
            internal bool ScuttlebraceLogic { get; set; }

            [JsonProperty("requirements")]
            internal Dictionary<string, LogicRequirement> Requirements
            {
                get;
                set;
            }

            [JsonProperty("abstract_requirements")]
            internal Dictionary<string, LogicRequirement>
                AbstractRequirements
            {
                get;
                set;
            }

            [JsonProperty("logic_item_dependencies")]
            internal Dictionary<string, List<string>>
                LogicItemDependencies
            {
                get;
                set;
            }

            [JsonProperty("logic_events")]
            internal List<LogicEvent> LogicEvents { get; set; }
        }

        private sealed class LogicRequirement
        {
            [JsonProperty("all_of")]
            internal List<string> AllOf { get; set; }

            [JsonProperty("any_of")]
            internal List<string> AnyOf { get; set; }

            [JsonProperty("required_locations")]
            internal List<string> RequiredLocations { get; set; }

            [JsonProperty("require_any_crest")]
            internal bool RequireAnyCrest { get; set; }

            [JsonProperty("require_silk_spear")]
            internal bool RequireSilkSpear { get; set; }

            [JsonProperty("minimum_skip_tier")]
            internal int MinimumSkipTier { get; set; }

            [JsonProperty("path")]
            internal string Path { get; set; }

            [JsonProperty("item_counts")]
            internal List<LogicItemCount> ItemCounts { get; set; }

            [JsonProperty("alternatives")]
            internal List<LogicRequirement> Alternatives { get; set; }

            [JsonProperty("logic_unknown")]
            internal bool LogicUnknown { get; set; }
        }

        private sealed class LogicItemCount
        {
            [JsonProperty("items")]
            internal List<string> Items { get; set; }

            [JsonProperty("minimum")]
            internal int Minimum { get; set; }
        }

        private sealed class LogicEvent
        {
            [JsonProperty("location")]
            internal string Location { get; set; }

            [JsonProperty("item")]
            internal string Item { get; set; }

            [JsonProperty("requirement")]
            internal LogicRequirement Requirement { get; set; }
        }

        private sealed class ParsedPayload
        {
            internal readonly Dictionary<string, LogicRequirement>
                Requirements;
            internal readonly Dictionary<string, LogicRequirement>
                AbstractRequirements;
            internal readonly Dictionary<string, List<string>>
                Dependencies;
            internal readonly List<LogicEvent> LogicEvents;
            internal readonly int SkipsTier;

            internal ParsedPayload(LogicPayload payload)
            {
                Requirements = CanonicalizeLocationKeys(
                    payload?.Requirements
                );
                AbstractRequirements =
                    payload?.AbstractRequirements ??
                    new Dictionary<string, LogicRequirement>(
                        StringComparer.Ordinal
                    );
                Dependencies = CanonicalizeDependencyKeys(
                    payload?.LogicItemDependencies
                );
                LogicEvents = (payload?.LogicEvents ?? new List<LogicEvent>())
                    .Where(logicEvent =>
                        logicEvent != null &&
                        !string.IsNullOrWhiteSpace(logicEvent.Item) &&
                        logicEvent.Requirement != null)
                    .ToList();
                SkipsTier = payload?.SkipsTier ?? 0;
                if (payload != null && !payload.ScuttlebraceLogic)
                {
                    RemoveScuttlebraceAlternatives(Requirements);
                    RemoveScuttlebraceAlternatives(
                        AbstractRequirements
                    );
                }
            }
        }

        private static void RemoveScuttlebraceAlternatives(
            IDictionary<string, LogicRequirement> groups
        )
        {
            if (groups == null)
            {
                return;
            }

            foreach (LogicRequirement group in groups.Values)
            {
                if (group?.Alternatives == null)
                {
                    continue;
                }

                group.Alternatives = group.Alternatives
                    .Where(AlternativeDoesNotRequireScuttlebrace)
                    .ToList();
            }
        }

        private static bool AlternativeDoesNotRequireScuttlebrace(
            LogicRequirement requirement
        )
        {
            if (requirement == null ||
                (requirement.AllOf ?? new List<string>()).Any(
                    name => string.Equals(
                        name,
                        UsableScuttlebraceRequirement,
                        StringComparison.Ordinal
                    )
                ))
            {
                return false;
            }

            if (requirement.AnyOf == null)
            {
                return true;
            }

            int originalCount = requirement.AnyOf.Count;
            requirement.AnyOf = requirement.AnyOf
                .Where(name => !string.Equals(
                    name,
                    UsableScuttlebraceRequirement,
                    StringComparison.Ordinal
                ))
                .ToList();
            return originalCount == 0 || requirement.AnyOf.Count > 0;
        }

        private static readonly string[] CrestItemNames =
        {
            "Crest: Hunter",
            "Crest: Wanderer",
            "Crest: Reaper",
            "Crest: Beast",
            "Crest: Architect",
            "Crest: Witch",
            "Crest: Shaman",
        };

        private static readonly string[] NonArchitectCrestItemNames =
            CrestItemNames.Where(
                name => !string.Equals(
                    name,
                    "Crest: Architect",
                    StringComparison.OrdinalIgnoreCase
                )
            ).ToArray();

        private const string SwiftStepItemName = "Swift Step";

        private static string cachedJson = string.Empty;
        private static ParsedPayload cachedPayload;
        private static ParsedPayload cachedAbstractPayload;
        private static Dictionary<string, int> cachedAbstractInventory;
        private static Dictionary<string, int> cachedResolvedInventory;
        private static Dictionary<string, bool> cachedAbstractValues;
        private static bool cachedAbstractGoalCompleted;
        private static bool loggedPayloadFailure;

        internal static MapCheckReachability Evaluate(
            SaveState state,
            string locationName
        )
        {
            IReadOnlyDictionary<string, MapCheckReachability> result =
                EvaluateAll(state, new[] { locationName });
            string canonicalLocationName =
                LocationSet.GetCanonicalLocationName(locationName);
            return !string.IsNullOrWhiteSpace(canonicalLocationName) &&
                    result.TryGetValue(
                        canonicalLocationName,
                        out MapCheckReachability reachability
                    )
                ? reachability
                : MapCheckReachability.Unknown;
        }

        internal static IReadOnlyDictionary<
            string,
            MapCheckReachability
        > EvaluateAll(
            SaveState state,
            IEnumerable<string> locationNames
        )
        {
            string[] canonicalLocationNames =
                (locationNames ?? Enumerable.Empty<string>())
                    .Select(LocationSet.GetCanonicalLocationName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            Dictionary<string, MapCheckReachability> results =
                canonicalLocationNames.ToDictionary(
                    name => name,
                    _ => MapCheckReachability.Unknown,
                    StringComparer.OrdinalIgnoreCase
                );
            ParsedPayload payload = GetPayload(state);
            if (payload == null)
            {
                return results;
            }

            Dictionary<string, int> inventory =
                BuildInventoryCounts(state);
            bool goalCompleted = state?.goalCompleted == true;
            Dictionary<string, bool> abstractValues =
                GetAbstractRequirements(
                    payload,
                    inventory,
                    goalCompleted
                );
            bool hasCrest = CrestItemNames.Any(
                itemName => CountItem(inventory, itemName) > 0
            );
            bool hasSilkSpear =
                CountItem(inventory, "Silkspear") > 0 &&
                NonArchitectCrestItemNames.Any(
                    itemName => CountItem(inventory, itemName) > 0
                );

            foreach (string canonicalLocationName in
                canonicalLocationNames)
            {
                if (!payload.Requirements.TryGetValue(
                        canonicalLocationName,
                        out LogicRequirement requirement
                    ))
                {
                    continue;
                }
                results[canonicalLocationName] = SatisfiesGroup(
                        requirement,
                        goalCompleted,
                        abstractValues,
                        inventory,
                        hasCrest,
                        hasSilkSpear,
                        payload.Requirements,
                        payload.Dependencies,
                        payload.SkipsTier,
                        new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase
                        )
                    )
                    ? MapCheckReachability.Reachable
                    : MapCheckReachability.Unreachable;
            }
            return results;
        }

        internal static bool TryGetLocationPath(
            SaveState state,
            string locationName,
            out string path
        )
        {
            path = string.Empty;
            ParsedPayload payload = GetPayload(state);
            string canonicalLocationName =
                LocationSet.GetCanonicalLocationName(locationName);
            if (payload == null ||
                string.IsNullOrWhiteSpace(canonicalLocationName) ||
                !payload.Requirements.TryGetValue(
                    canonicalLocationName,
                    out LogicRequirement group
                ))
            {
                return false;
            }

            foreach (LogicRequirement alternative in
                GetAlternatives(group))
            {
                if (!string.IsNullOrWhiteSpace(alternative?.Path))
                {
                    path = alternative.Path.Trim();
                    return true;
                }
            }
            return false;
        }

        internal static bool RequiresLogicVerification(
            SaveState state,
            string locationName
        )
        {
            ParsedPayload payload = GetPayload(state);
            string canonicalLocationName =
                LocationSet.GetCanonicalLocationName(locationName);
            return payload != null &&
                   !string.IsNullOrWhiteSpace(canonicalLocationName) &&
                   payload.Requirements.TryGetValue(
                       canonicalLocationName,
                       out LogicRequirement requirement
                   ) &&
                   requirement.LogicUnknown;
        }

        // Setting this to 2 to lower adjacencies in massive rooms like Bone Bottom town.
        private const int MaxAdjacentRooms = 2;
        private const string RoomNodePrefix = "Room Node: ";
        private const string RoomEventPrefix = "Room Event: ";
        // Grey already used in repository. ^-^
        private static readonly Color OutOfLogicColor =
            new Color(0.58f, 0.58f, 0.58f, 1f);
        // Mint Green
        private static readonly Color MetLogicColor =
            new Color(0.45f, 0.92f, 0.52f, 1f);
        // Grey already used in repository. ^-^
        private static readonly Color NeedLogicColor =
            new Color(0.58f, 0.58f, 0.58f, 1f);

        private static ParsedPayload cachedExplainPayload;
        private static Dictionary<string, int> cachedHoverInventory;
        private static HoverLogicContext cachedHoverContext;
        private static bool cachedHoverGoalCompleted;
        private static bool loggedExplainFailure;

        private sealed class HoverLogicContext
        {
            internal ParsedPayload Payload;
            internal Dictionary<string, int> Inventory;
            internal Dictionary<string, bool> AbstractValues;
            internal bool HasSilkSpear;
        }

        // This calls CollectAccessDelta and returns the lines for the logic tooltip.
        internal static IReadOnlyList<LogicTooltipLine> Explain(
            SaveState state,
            string locationName,
            MapCheckReachability reachability
        )
        {
            List<LogicTooltipLine> lines = new List<LogicTooltipLine>();
            try
            {
                ParsedPayload payload = GetPayload(state);
                string canonicalLocationName =
                    LocationSet.GetCanonicalLocationName(locationName);
                if (payload == null ||
                    string.IsNullOrWhiteSpace(canonicalLocationName) ||
                    !payload.Requirements.TryGetValue(
                        canonicalLocationName,
                        out LogicRequirement group
                    ))
                {
                    return lines;
                }
                if (group.LogicUnknown)
                {
                    lines.Add(
                        new LogicTooltipLine("Logic Unknown / Unmapped", Color.white)
                    );
                    return lines;
                }
                HoverLogicContext context = GetHoverLogicContext(
                    state,
                    payload
                );
                if (reachability == MapCheckReachability.Reachable)
                {
                    lines.Add(
                        new LogicTooltipLine("In Logic", Color.white)
                    );
                }
                else if (reachability == MapCheckReachability.Unreachable)
                {
                    lines.Add(
                        new LogicTooltipLine(
                            "Out of Logic",
                            OutOfLogicColor
                        )
                    );
                }
                lines.AddRange(CollectAccessDelta(context, group));
            }
            catch (Exception ex)
            {
                if (!loggedExplainFailure)
                {
                    loggedExplainFailure = true;
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Map-marker logic text failed: " +
                        ex.Message
                    );
                }
            }
            return lines;
        }

        // This caches the context of inventory and the additional requirements for the tooltips.
        private static HoverLogicContext GetHoverLogicContext(
            SaveState state,
            ParsedPayload payload
        )
        {
            Dictionary<string, int> inventory =
                BuildInventoryCounts(state);
            bool goalCompleted = state?.goalCompleted == true;
            if (ReferenceEquals(payload, cachedExplainPayload) &&
                cachedHoverContext != null &&
                cachedHoverGoalCompleted == goalCompleted &&
                HaveEqualInventory(inventory, cachedHoverInventory))
            {
                return cachedHoverContext;
            }

            cachedExplainPayload = payload;
            cachedHoverGoalCompleted = goalCompleted;
            cachedHoverInventory = new Dictionary<string, int>(
                inventory,
                StringComparer.OrdinalIgnoreCase
            );
            cachedHoverContext = new HoverLogicContext
            {
                Payload = payload,
                Inventory = inventory,
                AbstractValues = GetAbstractRequirements(
                    payload,
                    inventory,
                    goalCompleted
                ),
                HasSilkSpear =
                    CountItem(inventory, "Silkspear") > 0 &&
                    NonArchitectCrestItemNames.Any(
                        itemName => CountItem(inventory, itemName) > 0
                    ),
            };
            return cachedHoverContext;
        }

        // This walks the logic graph using logical AND and OR operators to find the items needed to access the location.
        // Tested the most against Deep Docks - Mask Shard which led me to use 4 adjacent rooms as the maximum so the tooltip doesn't go on forever.
        private static List<LogicTooltipLine> CollectAccessDelta(
            HoverLogicContext context,
            LogicRequirement startGroup
        )
        {
            List<List<string>> found = new List<List<string>>();
            Queue<Tuple<LogicRequirement, int>> pending =
                new Queue<Tuple<LogicRequirement, int>>();
            HashSet<string> visited = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );
            pending.Enqueue(Tuple.Create(startGroup, 0));
            int foundHop = -1;
            while (pending.Count > 0)
            {
                Tuple<LogicRequirement, int> next = pending.Dequeue();
                LogicRequirement group = next.Item1;
                int hop = next.Item2;
                if (group == null || hop > MaxAdjacentRooms)
                {
                    continue;
                }
                if (foundHop >= 0 && hop > foundHop)
                {
                    break;
                }

                List<LogicRequirement> alternatives =
                    GetAlternatives(group).ToList();
                if (alternatives.Count == 0)
                {
                    alternatives.Add(group);
                }
                List<string> walk = new List<string>();
                int simpleNoneCount = 0;
                bool addedItems = false;
                foreach (LogicRequirement alternative in alternatives)
                {
                    List<string> roomRefs = new List<string>();
                    List<string> items = new List<string>();
                    SplitAccess(alternative, roomRefs, items);
                    if (items.Count > 0)
                    {
                        found.Add(items);
                        addedItems = true;
                        continue;
                    }
                    if (roomRefs.Count == 1 &&
                        roomRefs[0].StartsWith(
                            RoomNodePrefix,
                            StringComparison.OrdinalIgnoreCase
                        ))
                    {
                        simpleNoneCount++;
                    }
                    foreach (string roomRef in roomRefs)
                    {
                        if (!walk.Contains(roomRef))
                        {
                            walk.Add(roomRef);
                        }
                    }
                }
                if (addedItems)
                {
                    if (foundHop < 0)
                    {
                        foundHop = hop;
                    }
                    continue;
                }
                if (simpleNoneCount >= 2 || hop >= MaxAdjacentRooms)
                {
                    continue;
                }
                foreach (string roomRef in walk)
                {
                    if (!visited.Add(roomRef) ||
                        !TryGetNamedGroup(
                            context.Payload,
                            roomRef,
                            out LogicRequirement nextGroup
                        ))
                    {
                        continue;
                    }
                    pending.Enqueue(Tuple.Create(nextGroup, hop + 1));
                }
            }

            List<LogicTooltipLine> lines = new List<LogicTooltipLine>();
            foreach (List<string> items in found)
            {
                if (items == null || items.Count == 0)
                {
                    continue;
                }
                string text = string.Join(" + ", items);
                if (lines.Any(line =>
                        string.Equals(
                            line.Text,
                            text,
                            StringComparison.OrdinalIgnoreCase
                        )))
                {
                    continue;
                }
                bool met = items.All(item => IsAccessItemMet(context, item));
                lines.Add(
                    new LogicTooltipLine(
                        text,
                        met ? MetLogicColor : NeedLogicColor
                    )
                );
            }
            return lines;
        }

        // Checks over the requirements and splits them into references for rooms and items.
        private static void SplitAccess(
            LogicRequirement requirement,
            List<string> roomRefs,
            List<string> items
        )
        {
            if (requirement == null)
            {
                return;
            }
            if (requirement.RequireSilkSpear)
            {
                items.Add("Usable Silkspear");
            }
            foreach (LogicItemCount itemCount in
                requirement.ItemCounts ?? new List<LogicItemCount>())
            {
                string formatted = FormatItemCount(itemCount);
                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    items.Add(formatted);
                }
            }

            List<string> anyOfItems = new List<string>();
            foreach (string name in
                (requirement.AllOf ?? new List<string>()).Concat(
                    requirement.RequiredLocations ?? new List<string>()
                ))
            {
                ClassifyAccessName(name, roomRefs, items);
            }
            foreach (string name in requirement.AnyOf ?? new List<string>())
            {
                ClassifyAccessName(name, roomRefs, anyOfItems);
            }
            if (anyOfItems.Count == 1)
            {
                items.Add(anyOfItems[0]);
            }
            else if (anyOfItems.Count > 1)
            {
                items.Add("(" + string.Join(" or ", anyOfItems) + ")");
            }
        }

        // Classifying if the name of the requirement is a room or an item for adjacent room walks.
        private static void ClassifyAccessName(
            string name,
            List<string> roomRefs,
            List<string> items
        )
        {
            string trimmed = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return;
            }
            if (IsRoomGraphReference(trimmed))
            {
                roomRefs.Add(trimmed);
                return;
            }
            if (!trimmed.StartsWith("Path: ", StringComparison.Ordinal))
            {
                items.Add(trimmed);
            }
        }

        // Checks the inventory and requirements to see if the item is available.
        private static bool IsAccessItemMet(
            HoverLogicContext context,
            string item
        )
        {
            if (string.Equals(
                    item,
                    "Usable Silkspear",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return context.HasSilkSpear;
            }
            if (item != null &&
                item.StartsWith("(", StringComparison.Ordinal) &&
                item.EndsWith(")", StringComparison.Ordinal) &&
                item.IndexOf(" or ", StringComparison.Ordinal) >= 0)
            {
                return item.Substring(1, item.Length - 2)
                    .Split(new[] { " or " }, StringSplitOptions.None)
                    .Any(part => IsAccessItemMet(context, part.Trim()));
            }
            return HasNamedRequirement(
                item,
                context.AbstractValues,
                context.Inventory,
                context.Payload.Dependencies
            );
        }

        // Resolves the conditionals in the logic graph to a logical group to assign to the logic walk / tooltip.
        private static bool TryGetNamedGroup(
            ParsedPayload payload,
            string name,
            out LogicRequirement group
        )
        {
            if (payload.AbstractRequirements.TryGetValue(name, out group))
            {
                return true;
            }
            string canonicalLocationName =
                LocationSet.GetCanonicalLocationName(name);
            return !string.IsNullOrWhiteSpace(canonicalLocationName) &&
                   payload.Requirements.TryGetValue(
                       canonicalLocationName,
                       out group
                   );
        }

        // This checks if the name is a room graph reference for classifying adjacent room walks.
        private static bool IsRoomGraphReference(string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                   (name.StartsWith(
                        RoomNodePrefix,
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    name.StartsWith(
                        RoomEventPrefix,
                        StringComparison.OrdinalIgnoreCase
                    ));
        }

        // Formats the amount of items required for the tooltip. If there's only one, it's just the item.
        private static string FormatItemCount(LogicItemCount itemCount)
        {
            if (itemCount?.Items == null)
            {
                return string.Empty;
            }
            List<string> items = itemCount.Items
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .ToList();
            if (items.Count == 0)
            {
                return string.Empty;
            }
            string names = items.Count == 1
                ? items[0]
                : "(" + string.Join(" or ", items) + ")";
            int minimum = Math.Max(0, itemCount.Minimum);
            return minimum <= 1 ? names : names + " x" + minimum;
        }

        private static ParsedPayload GetPayload(SaveState state)
        {
            string json = state?.mapLogicPayloadJson ?? string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            if (string.Equals(json, cachedJson, StringComparison.Ordinal))
            {
                return cachedPayload;
            }

            cachedJson = json;
            cachedPayload = null;
            try
            {
                LogicPayload payload =
                    JsonConvert.DeserializeObject<LogicPayload>(json);
                if (payload?.Requirements == null ||
                    payload.Requirements.Count == 0 ||
                    payload.AbstractRequirements == null ||
                    payload.AbstractRequirements.Count == 0 ||
                    payload.LogicItemDependencies == null ||
                    payload.Requirements.Values.Any(group =>
                        group?.Alternatives == null) ||
                    payload.AbstractRequirements.Values.Any(group =>
                        group?.Alternatives == null))
                {
                    return null;
                }
                cachedPayload = new ParsedPayload(payload);
                loggedPayloadFailure = false;
            }
            catch (Exception ex)
            {
                if (!loggedPayloadFailure)
                {
                    loggedPayloadFailure = true;
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] The room's check-map logic payload " +
                        "could not be read; marker reachability will remain " +
                        "neutral: " + ex.Message
                    );
                }
            }
            return cachedPayload;
        }

        private static Dictionary<string, LogicRequirement>
            CanonicalizeLocationKeys(
                Dictionary<string, LogicRequirement> source
            )
        {
            Dictionary<string, LogicRequirement> result =
                new Dictionary<string, LogicRequirement>(
                    StringComparer.OrdinalIgnoreCase
                );
            if (source == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, LogicRequirement> entry in source)
            {
                string canonicalName =
                    LocationSet.GetCanonicalLocationName(entry.Key);
                if (!string.IsNullOrWhiteSpace(canonicalName))
                {
                    result[canonicalName] = entry.Value;
                }
            }
            return result;
        }

        private static Dictionary<string, List<string>>
            CanonicalizeDependencyKeys(
                Dictionary<string, List<string>> source
            )
        {
            Dictionary<string, List<string>> result =
                new Dictionary<string, List<string>>(
                    StringComparer.OrdinalIgnoreCase
                );
            if (source == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, List<string>> entry in source)
            {
                string canonicalName =
                    ItemSet.GetCanonicalItemName(entry.Key);
                result[canonicalName] = (entry.Value ?? new List<string>())
                    .Select(ItemSet.GetCanonicalItemName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();
            }
            return result;
        }

        private static Dictionary<string, int> BuildInventoryCounts(
            SaveState state
        )
        {
            Dictionary<string, int> counts =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase
                );

            Archipelago archipelago = Archipelago.Instance;
            IReadOnlyDictionary<string, int> receivedCounts =
                archipelago?.GetReceivedItemCounts();
            if (receivedCounts != null)
            {
                foreach (KeyValuePair<string, int> entry in receivedCounts)
                {
                    AddCount(counts, entry.Key, entry.Value);
                }
            }

            if (state?.receivedItems != null)
            {
                foreach (string itemName in state.receivedItems)
                {
                    SetMinimumCount(counts, itemName, 1);
                }
            }

            if (state != null)
            {
                SetMinimumCount(
                    counts,
                    "Progressive Swift Step",
                    state.swiftStepLevel
                );
                SetMinimumCount(
                    counts,
                    "Progressive Druid's Eyes",
                    state.druidsEyeLevel
                );
                SetMinimumCount(
                    counts,
                    "Progressive Needle Upgrade",
                    state.needleUpgradeLevel
                );
                SetMinimumCount(
                    counts,
                    "Progressive Silkheart",
                    state.silkHeartLevel
                );
                GameManager gameManager = GameManager.UnsafeInstance;
                PlayerData playerData = gameManager == null
                    ? null
                    : gameManager.playerData;
                if (playerData != null)
                {
                    if (
                        state.GetRandomizationMode(ItemType.NeedleUpgrade) ==
                        RandomizationMode.Vanilla
                    )
                    {
                        SetMinimumCount(
                            counts,
                            "Progressive Needle Upgrade",
                            Math.Max(0, playerData.nailUpgrades)
                        );
                    }

                    SetMinimumCount(
                        counts,
                        "Progressive Crafting Kit",
                        playerData.ToolKitUpgrades
                    );
                    SetMinimumCount(
                        counts,
                        "Progressive Tool Pouch",
                        playerData.ToolPouchUpgrades
                    );
                    SetMinimumCount(
                        counts,
                        "Mossberry",
                        GetPersistedMossberryCount(playerData, state)
                    );
                    SetMinimumCount(
                        counts,
                        "Pollip Heart",
                        GetPersistedPollipHeartCount(playerData, state)
                    );
                    SetMinimumCount(
                        counts,
                        "Pale Oil",
                        GetPersistedPaleOilCount(playerData, state)
                    );
                }
                AddNativeSkillState(counts, state);
                AddNativeEquipmentState(counts, state);
                AddNativeWorldState(counts, state);
            }
            return counts;
        }

        private static int GetPersistedCollectableCount(
            PlayerData playerData,
            string assetName,
            int maximum
        )
        {
            if (playerData?.Collectables == null || maximum <= 0)
            {
                return 0;
            }

            CollectableItemsData.Data data =
                playerData.Collectables.GetData(assetName);
            long count =
                (long)Math.Max(0, data.Amount) +
                Math.Max(0, data.AmountWhileHidden);
            return (int)Math.Min(maximum, count);
        }

        private static int GetPersistedMossberryCount(
            PlayerData playerData,
            SaveState state
        )
        {
            int count = GetPersistedCollectableCount(
                playerData,
                "Mossberry",
                MaxMossberryCount
            );

            // The Druid records only the three middle one-berry trades.
            // The initial three-berry and final one-berry costs come from
            // the completed reward checks, which are persisted by SaveState.
            count += Math.Max(
                0,
                Math.Min(3, playerData.druidMossBerriesSold)
            );
            if (state.IsLocationChecked("Tool Unlock: Mosscreep Tool 1"))
            {
                count += 3;
            }
            if (state.IsLocationChecked("Tool Unlock: Mosscreep Tool 2"))
            {
                count += 1;
            }

            return Math.Min(MaxMossberryCount, count);
        }

        private static int GetPersistedPaleOilCount(
            PlayerData playerData,
            SaveState state
        )
        {
            int count = GetPersistedCollectableCount(
                playerData,
                "Pale_Oil",
                MaxPaleOilCount
            );

            bool vanillaNeedleUpgrades =
                state.GetRandomizationMode(ItemType.NeedleUpgrade) ==
                RandomizationMode.Vanilla;
            int spentPaleOil = vanillaNeedleUpgrades
                ? Math.Max(
                    0,
                    Math.Min(MaxPaleOilCount, playerData.nailUpgrades - 1)
                )
                : 0;
            if (!vanillaNeedleUpgrades &&
                state.IsLocationChecked(
                    "Pinmaster Plinney: Sharpened Needle") &&
                state.IsLocationChecked(
                    "Pinmaster Plinney: Shining Needle"))
            {
                spentPaleOil = 1;
                if (state.IsLocationChecked(
                        "Pinmaster Plinney: Hivesteel Needle"))
                {
                    spentPaleOil = 2;
                    if (state.IsLocationChecked(
                            "Pinmaster Plinney: Pale Steel Needle"))
                    {
                        spentPaleOil = 3;
                    }
                }
            }

            return Math.Min(MaxPaleOilCount, count + spentPaleOil);
        }

        private static int GetPersistedPollipHeartCount(
            PlayerData playerData,
            SaveState state
        )
        {
            int count = GetPersistedCollectableCount(
                playerData,
                "Shell Flower",
                MaxPollipHeartCount
            );

            // Shell Flowers consumes all six hearts before granting Pollip
            // Pouch. Recover those consumed copies so offline/reloaded map
            // logic does not collapse this repeatable AP item to the single
            // entry retained by SaveState.receivedItems.
            bool questCompleted = false;
            if (playerData?.QuestCompletionData != null)
            {
                QuestCompletionData.Completion completion =
                    playerData.QuestCompletionData.GetData(
                        "Shell Flowers"
                    );
                questCompleted = completion.IsCompleted ||
                                 completion.WasEverCompleted;
            }
            if (questCompleted ||
                (state != null && state.IsLocationChecked(
                    "Tool Unlock: Poison Pouch")))
            {
                count += MaxPollipHeartCount;
            }

            return Math.Min(MaxPollipHeartCount, count);
        }

        private static void AddNativeSkillState(
            Dictionary<string, int> counts,
            SaveState state
        )
        {
            if (state.IsRandomized(ItemType.Skill))
            {
                return;
            }

            PlayerData playerData = PlayerData.instance;
            if (state.canDoubleJump ||
                (playerData != null && playerData.hasDoubleJump))
            {
                SetMinimumCount(counts, "Faydown Cloak", 1);
            }
            if (state.canChargeSlash ||
                (playerData != null && playerData.hasChargeSlash))
            {
                SetMinimumCount(counts, "Needle Strike", 1);
            }
            if (state.canSilkSoar ||
                (playerData != null && playerData.hasSuperJump))
            {
                SetMinimumCount(counts, "Silk Soar", 1);
            }
            if (state.canWallJump ||
                (playerData != null && playerData.hasWalljump))
            {
                SetMinimumCount(counts, "Cling Grip", 1);
            }
            if (state.canBrolly ||
                (playerData != null && playerData.hasBrolly))
            {
                SetMinimumCount(counts, "Drifter's Cloak", 1);
            }
            if (state.splitDashAndSprint)
            {
                SetMinimumCount(
                    counts,
                    "Progressive Swift Step",
                    state.swiftStepLevel
                );
            }
            else if (state.canDash ||
                (playerData != null && playerData.hasDash))
            {
                SetMinimumCount(counts, "Swift Step", 1);
            }
            if (state.canUseHarpoon ||
                (playerData != null && playerData.hasHarpoonDash))
            {
                SetMinimumCount(counts, "Clawline", 1);
            }
            if (state.canUseNeedolin ||
                (playerData != null && playerData.hasNeedolin))
            {
                SetMinimumCount(counts, "Needolin", 1);
            }
            if (state.canUseQuill ||
                (playerData != null && playerData.hasQuill))
            {
                SetMinimumCount(counts, "Quill", 1);
            }
        }

        private static void AddNativeEquipmentState(
            Dictionary<string, int> counts,
            SaveState state
        )
        {
            bool needsTools = !state.IsRandomized(ItemType.Tool);
            bool needsSpells = !state.IsRandomized(ItemType.Spell);
            bool needsCrests = !state.IsRandomized(ItemType.Crest);
            if (!needsTools && !needsSpells && !needsCrests)
            {
                return;
            }

            try
            {
                if (needsTools || needsSpells)
                {
                    foreach (ToolItem tool in
                        Resources.FindObjectsOfTypeAll<ToolItem>())
                    {
                        if (tool == null || !tool.IsUnlocked)
                        {
                            continue;
                        }

                        ItemType itemType = tool.Type == ToolItemType.Skill
                            ? ItemType.Spell
                            : ItemType.Tool;
                        if (state.IsRandomized(itemType))
                        {
                            continue;
                        }

                        SetMinimumCount(
                            counts,
                            (
                                itemType == ItemType.Spell
                                    ? "Spell: "
                                    : "Tool: "
                            ) + tool.name,
                            1
                        );
                    }
                }

                if (needsCrests)
                {
                    foreach (ToolCrest crest in
                        ToolItemManager.GetAllCrests())
                    {
                        if (crest != null &&
                            crest.IsBaseVersion &&
                            crest.IsUnlocked)
                        {
                            SetMinimumCount(
                                counts,
                                CrestNames.GetItemNameFromInternal(
                                    crest.name
                                ),
                                1
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogDebug(
                    "[RANDOMIZER] Native equipment was not ready for " +
                    "check-map logic: " + ex.Message
                );
            }
        }

        private static Dictionary<string, bool> GetAbstractRequirements(
            ParsedPayload payload,
            Dictionary<string, int> inventory,
            bool goalCompleted
        )
        {
            if (ReferenceEquals(payload, cachedAbstractPayload) &&
                goalCompleted == cachedAbstractGoalCompleted &&
                HaveEqualInventory(inventory, cachedAbstractInventory))
            {
                foreach (KeyValuePair<string, int> entry in
                    cachedResolvedInventory ??
                    new Dictionary<string, int>())
                {
                    SetMinimumCount(inventory, entry.Key, entry.Value);
                }
                return cachedAbstractValues;
            }

            Dictionary<string, int> inventorySnapshot =
                new Dictionary<string, int>(
                    inventory,
                    StringComparer.OrdinalIgnoreCase
                );
            Dictionary<string, bool> abstractValues =
                ResolveAbstractRequirements(
                    payload,
                    inventory,
                    goalCompleted
                );

            cachedAbstractPayload = payload;
            cachedAbstractInventory = inventorySnapshot;
            cachedResolvedInventory = new Dictionary<string, int>(
                inventory,
                StringComparer.OrdinalIgnoreCase
            );
            cachedAbstractValues = abstractValues;
            cachedAbstractGoalCompleted = goalCompleted;
            return cachedAbstractValues;
        }

        private static bool HaveEqualInventory(
            IReadOnlyDictionary<string, int> left,
            IReadOnlyDictionary<string, int> right
        )
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, int> entry in left)
            {
                if (!right.TryGetValue(entry.Key, out int count) ||
                    count != entry.Value)
                {
                    return false;
                }
            }
            return true;
        }

        private static void AddNativeWorldState(
            Dictionary<string, int> counts,
            SaveState state
        )
        {
            bool needsNativeWorldState =
                !state.IsRandomized(ItemType.SilkHeart) ||
                !state.IsRandomized(ItemType.Map) ||
                !state.IsRandomized(ItemType.Flea) ||
                !state.IsRandomized(ItemType.Bellway) ||
                !state.IsRandomized(ItemType.Ventrica) ||
                !state.IsRandomized(ItemType.BellShrine);
            if (!needsNativeWorldState)
            {
                return;
            }

            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return;
            }

            if (!state.IsRandomized(ItemType.SilkHeart))
            {
                int silkHeartCount = Math.Max(
                    0,
                    Math.Min(3, playerData.silkRegenMax)
                );
                SetMinimumCount(
                    counts,
                    "Progressive Silkheart",
                    silkHeartCount
                );
            }

            if (!state.IsRandomized(ItemType.Map))
            {
                AddNativeMaps(counts, playerData);
            }
            if (!state.IsRandomized(ItemType.Flea))
            {
                AddNativeFleas(counts, playerData);
            }
            if (!state.IsRandomized(ItemType.Bellway))
            {
                AddNativeBellways(counts, playerData);
            }
            if (!state.IsRandomized(ItemType.Ventrica))
            {
                AddNativeVentrica(counts, playerData);
            }
            if (!state.IsRandomized(ItemType.BellShrine))
            {
                AddNativeBellShrines(counts, playerData);
            }
        }

        private static void AddNativeBellShrines(
            Dictionary<string, int> counts,
            PlayerData playerData
        )
        {
            SetIfTrue(
                counts,
                "Bell: The Marrow",
                playerData.bellShrineBoneForest
            );
            SetIfTrue(
                counts,
                "Bell: Deep Docks",
                playerData.bellShrineWilds
            );
            SetIfTrue(
                counts,
                "Bell: Greymoor",
                playerData.bellShrineGreymoor
            );
            SetIfTrue(
                counts,
                "Bell: Shellwood",
                playerData.bellShrineShellwood
            );
            SetIfTrue(
                counts,
                "Bell: Bellhart",
                playerData.bellShrineBellhart
            );
        }

        private static void AddNativeMaps(
            Dictionary<string, int> counts,
            PlayerData playerData
        )
        {
            SetIfTrue(counts, "Map: Mosslands", playerData.HasMossGrottoMap);
            SetIfTrue(counts, "Map: The Marrow", playerData.HasBoneforestMap);
            SetIfTrue(counts, "Map: Deep Docks", playerData.HasDocksMap);
            SetIfTrue(counts, "Map: Far Fields", playerData.HasWildsMap);
            SetIfTrue(counts, "Map: Wormways", playerData.HasCrawlMap);
            SetIfTrue(
                counts,
                "Map: Hunter's March",
                playerData.HasHuntersNestMap
            );
            SetIfTrue(counts, "Map: Greymoor", playerData.HasGreymoorMap);
            SetIfTrue(counts, "Map: Bellhart", playerData.HasBellhartMap);
            SetIfTrue(counts, "Map: Shellwood", playerData.HasShellwoodMap);
            SetIfTrue(
                counts,
                "Map: Blasted Steps",
                playerData.HasJudgeStepsMap
            );
            SetIfTrue(
                counts,
                "Map: Sinner's Road",
                playerData.HasDustpensMap
            );
            SetIfTrue(counts, "Map: Mount Fay", playerData.HasPeakMap);
            SetIfTrue(
                counts,
                "Map: Sands of Karak",
                playerData.HasCoralMap
            );
            SetIfTrue(counts, "Map: Bilewater", playerData.HasSwampMap);
        }

        private static void AddNativeFleas(
            Dictionary<string, int> counts,
            PlayerData playerData
        )
        {
            SetIfTrue(
                counts,
                "Flea: The Marrow",
                playerData.SavedFlea_Bone_06
            );
            SetIfTrue(
                counts,
                "Flea: Deep Docks - Bellway",
                playerData.SavedFlea_Dock_16
            );
            SetIfTrue(
                counts,
                "Flea: Deep Docks - Weaver Burial Spire",
                playerData.SavedFlea_Bone_East_05
            );
            SetIfTrue(
                counts,
                "Flea: Far Fields - Captured",
                playerData.SavedFlea_Bone_East_17b
            );
            SetIfTrue(
                counts,
                "Flea: Hunter's March",
                playerData.SavedFlea_Ant_03
            );
            SetIfTrue(
                counts,
                "Flea: Greymoor - Craw Lake",
                playerData.SavedFlea_Greymoor_15b
            );
            SetIfTrue(
                counts,
                "Flea: Greymoor - Tower",
                playerData.SavedFlea_Greymoor_06
            );
            SetIfTrue(
                counts,
                "Flea: Shellwood",
                playerData.SavedFlea_Shellwood_03
            );
            SetIfTrue(
                counts,
                "Flea: Pilgrim's Rest",
                playerData.SavedFlea_Bone_East_10_Church
            );
            SetIfTrue(
                counts,
                "Flea: Blasted Steps",
                playerData.SavedFlea_Coral_35
            );
            SetIfTrue(
                counts,
                "Flea: Sinner's Road",
                playerData.SavedFlea_Dust_12
            );
            SetIfTrue(
                counts,
                "Flea: Exhaust Organ",
                playerData.SavedFlea_Dust_09
            );
            SetIfTrue(
                counts,
                "Flea: Bellhart",
                playerData.SavedFlea_Belltown_04
            );
            SetIfTrue(
                counts,
                "Flea: Wormways",
                playerData.SavedFlea_Crawl_06
            );
            SetIfTrue(
                counts,
                "Flea: The Slab - Cell",
                playerData.SavedFlea_Slab_Cell
            );
            SetIfTrue(
                counts,
                "Flea: Bilewater - Thieves",
                playerData.SavedFlea_Shadow_28
            );
            SetIfTrue(
                counts,
                "Flea: Deep Docks - Mines",
                playerData.SavedFlea_Dock_03d
            );
            SetIfTrue(
                counts,
                "Flea: Underworks - Wisp Thicket Passage",
                playerData.SavedFlea_Under_23
            );
            SetIfTrue(
                counts,
                "Flea: Bilehaven",
                playerData.SavedFlea_Shadow_10
            );
            SetIfTrue(
                counts,
                "Flea: Choral Chambers - Spa",
                playerData.SavedFlea_Song_14
            );
            SetIfTrue(
                counts,
                "Flea: Sands of Karak",
                playerData.SavedFlea_Coral_24
            );
            SetIfTrue(
                counts,
                "Flea: Mount Fay",
                playerData.SavedFlea_Peak_05c
            );
            SetIfTrue(
                counts,
                "Flea: Songclave",
                playerData.SavedFlea_Library_09
            );
            SetIfTrue(
                counts,
                "Flea: Choral Chambers - Walled Room",
                playerData.SavedFlea_Song_11
            );
            SetIfTrue(
                counts,
                "Flea: Whispering Vaults",
                playerData.SavedFlea_Library_01
            );
            SetIfTrue(
                counts,
                "Flea: Underworks",
                playerData.SavedFlea_Under_21
            );
            SetIfTrue(
                counts,
                "Flea: The Slab - Bellway",
                playerData.SavedFlea_Slab_06
            );
            SetIfTrue(
                counts,
                "Flea: Greymoor - Kratt",
                playerData.CaravanLechSaved
            );
            SetIfTrue(
                counts,
                "Flea: Putrified Ducts - Vog",
                playerData.MetTroupeHunterWild
            );
            SetIfTrue(
                counts,
                "Flea: Memorium - Huge Flea",
                playerData.tamedGiantFlea
            );
        }

        private static void AddNativeBellways(
            Dictionary<string, int> counts,
            PlayerData playerData
        )
        {
            SetIfTrue(
                counts,
                "Bellway: Deep Docks",
                playerData.UnlockedDocksStation
            );
            SetIfTrue(
                counts,
                "Bellway: Far Fields",
                playerData.UnlockedBoneforestEastStation
            );
            SetIfTrue(
                counts,
                "Bellway: Greymoor",
                playerData.UnlockedGreymoorStation
            );
            SetIfTrue(
                counts,
                "Bellway: Bellhart",
                playerData.UnlockedBelltownStation
            );
            SetIfTrue(
                counts,
                "Bellway: Blasted Steps",
                playerData.UnlockedCoralTowerStation
            );
            SetIfTrue(
                counts,
                "Bellway: Grand Bellway",
                playerData.UnlockedCityStation
            );
            SetIfTrue(
                counts,
                "Bellway: The Slab",
                playerData.UnlockedPeakStation
            );
            SetIfTrue(
                counts,
                "Bellway: Shellwood",
                playerData.UnlockedShellwoodStation
            );
            SetIfTrue(
                counts,
                "Bellway: Bilewater",
                playerData.UnlockedShadowStation
            );
            SetIfTrue(
                counts,
                "Bellway: Putrified Ducts",
                playerData.UnlockedAqueductStation
            );
        }

        private static void AddNativeVentrica(
            Dictionary<string, int> counts,
            PlayerData playerData
        )
        {
            SetIfTrue(
                counts,
                "Ventrica: Choral Chambers",
                playerData.UnlockedSongTube
            );
            SetIfTrue(
                counts,
                "Ventrica: Underworks",
                playerData.UnlockedUnderTube
            );
            SetIfTrue(
                counts,
                "Ventrica: Grand Bellway",
                playerData.UnlockedCityBellwayTube
            );
            SetIfTrue(
                counts,
                "Ventrica: High Halls",
                playerData.UnlockedHangTube
            );
            SetIfTrue(
                counts,
                "Ventrica: Songclave",
                playerData.UnlockedEnclaveTube
            );
            SetIfTrue(
                counts,
                "Ventrica: Memorium",
                playerData.UnlockedArboriumTube
            );
        }

        private static Dictionary<string, bool>
            ResolveAbstractRequirements(
                ParsedPayload payload,
                Dictionary<string, int> inventory,
                bool goalCompleted
            )
        {
            Dictionary<string, bool> values =
                payload.AbstractRequirements.Keys.ToDictionary(
                    name => name,
                    _ => false,
                    StringComparer.Ordinal
                );
            bool hasCrest = CrestItemNames.Any(
                itemName => CountItem(inventory, itemName) > 0
            );
            bool hasSilkSpear =
                CountItem(inventory, "Silkspear") > 0 &&
                NonArchitectCrestItemNames.Any(
                    itemName => CountItem(inventory, itemName) > 0
                );

            bool changed = true;
            HashSet<LogicEvent> collectedEvents =
                new HashSet<LogicEvent>();
            while (changed)
            {
                changed = false;
                foreach (KeyValuePair<string, LogicRequirement> entry in
                    payload.AbstractRequirements)
                {
                    if (values[entry.Key] ||
                        !SatisfiesGroup(
                            entry.Value,
                            goalCompleted,
                            values,
                            inventory,
                            hasCrest,
                            hasSilkSpear,
                            payload.Requirements,
                            payload.Dependencies,
                            payload.SkipsTier,
                            new HashSet<string>(
                                StringComparer.OrdinalIgnoreCase
                            )
                        ))
                    {
                        continue;
                    }
                    values[entry.Key] = true;
                    changed = true;
                }

                foreach (LogicEvent logicEvent in payload.LogicEvents)
                {
                    if (collectedEvents.Contains(logicEvent) ||
                        !SatisfiesGroup(
                            logicEvent.Requirement,
                            goalCompleted,
                            values,
                            inventory,
                            hasCrest,
                            hasSilkSpear,
                            payload.Requirements,
                            payload.Dependencies,
                            payload.SkipsTier,
                            new HashSet<string>(
                                StringComparer.OrdinalIgnoreCase
                            )
                        ))
                    {
                        continue;
                    }

                    collectedEvents.Add(logicEvent);
                    AddCount(inventory, logicEvent.Item, 1);
                    changed = true;
                }
            }
            return values;
        }

        private static bool SatisfiesGroup(
            LogicRequirement group,
            bool goalCompleted,
            IReadOnlyDictionary<string, bool> abstractValues,
            Dictionary<string, int> inventory,
            bool hasCrest,
            bool hasSilkSpear,
            IReadOnlyDictionary<string, LogicRequirement>
                locationRequirements,
            IReadOnlyDictionary<string, List<string>> dependencies,
            int skipsTier,
            HashSet<string> activeLocations
        )
        {
            if (group == null)
            {
                return false;
            }
            if (group.LogicUnknown)
            {
                return goalCompleted;
            }
            return GetAlternatives(group).Any(
                alternative => SatisfiesRequirement(
                    alternative,
                    goalCompleted,
                    abstractValues,
                    inventory,
                    hasCrest,
                    hasSilkSpear,
                    locationRequirements,
                    dependencies,
                    skipsTier,
                    activeLocations
                )
            );
        }

        private static IEnumerable<LogicRequirement> GetAlternatives(
            LogicRequirement group
        )
        {
            return (group?.Alternatives ??
                    Enumerable.Empty<LogicRequirement>())
                .Where(alternative => alternative != null);
        }

        private static bool SatisfiesRequirement(
            LogicRequirement requirement,
            bool goalCompleted,
            IReadOnlyDictionary<string, bool> abstractValues,
            Dictionary<string, int> inventory,
            bool hasCrest,
            bool hasSilkSpear,
            IReadOnlyDictionary<string, LogicRequirement>
                locationRequirements,
            IReadOnlyDictionary<string, List<string>> dependencies,
            int skipsTier,
            HashSet<string> activeLocations
        )
        {
            if (requirement == null ||
                requirement.MinimumSkipTier > skipsTier ||
                (requirement.RequireAnyCrest && !hasCrest) ||
                (requirement.RequireSilkSpear && !hasSilkSpear))
            {
                return false;
            }
            if ((requirement.AllOf ?? new List<string>()).Any(
                    name => !HasNamedRequirement(
                        name,
                        abstractValues,
                        inventory,
                        dependencies
                    )
                ))
            {
                return false;
            }
            if (requirement.AnyOf != null &&
                requirement.AnyOf.Count > 0 &&
                !requirement.AnyOf.Any(
                    name => HasNamedRequirement(
                        name,
                        abstractValues,
                        inventory,
                        dependencies
                    )
                ))
            {
                return false;
            }
            if ((requirement.RequiredLocations ?? new List<string>()).Any(
                    name => !SatisfiesRequiredLocation(
                        name,
                        goalCompleted,
                        abstractValues,
                        inventory,
                        hasCrest,
                        hasSilkSpear,
                        locationRequirements,
                        dependencies,
                        skipsTier,
                        activeLocations
                    )
                ))
            {
                return false;
            }
            if ((requirement.ItemCounts ?? new List<LogicItemCount>())
                .Any(itemCount =>
                    (itemCount?.Items ?? new List<string>())
                        .Sum(name => CountItem(inventory, name)) <
                    Math.Max(0, itemCount?.Minimum ?? 0)
                ))
            {
                return false;
            }
            return true;
        }

        private static bool SatisfiesRequiredLocation(
            string locationName,
            bool goalCompleted,
            IReadOnlyDictionary<string, bool> abstractValues,
            Dictionary<string, int> inventory,
            bool hasCrest,
            bool hasSilkSpear,
            IReadOnlyDictionary<string, LogicRequirement>
                locationRequirements,
            IReadOnlyDictionary<string, List<string>> dependencies,
            int skipsTier,
            HashSet<string> activeLocations
        )
        {
            string canonicalName =
                LocationSet.GetCanonicalLocationName(locationName);
            if (string.IsNullOrWhiteSpace(canonicalName) ||
                locationRequirements == null ||
                !locationRequirements.TryGetValue(
                    canonicalName,
                    out LogicRequirement requirement
                ) ||
                activeLocations == null ||
                !activeLocations.Add(canonicalName))
            {
                return false;
            }

            try
            {
                return SatisfiesGroup(
                    requirement,
                    goalCompleted,
                    abstractValues,
                    inventory,
                    hasCrest,
                    hasSilkSpear,
                    locationRequirements,
                    dependencies,
                    skipsTier,
                    activeLocations
                );
            }
            finally
            {
                activeLocations.Remove(canonicalName);
            }
        }

        private static bool HasNamedRequirement(
            string name,
            IReadOnlyDictionary<string, bool> abstractValues,
            Dictionary<string, int> inventory,
            IReadOnlyDictionary<string, List<string>> dependencies
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }
            if (abstractValues.TryGetValue(name, out bool abstractValue))
            {
                return abstractValue;
            }

            string canonicalName = ItemSet.GetCanonicalItemName(name);
            bool dependencyBackedAlias =
                string.Equals(
                    canonicalName,
                    SwiftStepItemName,
                    StringComparison.OrdinalIgnoreCase
                ) &&
                dependencies.TryGetValue(
                    canonicalName,
                    out List<string> aliasDependencies
                ) &&
                aliasDependencies.Count > 0;
            if (CountItem(inventory, canonicalName) <= 0 &&
                !dependencyBackedAlias)
            {
                return false;
            }
            if (!dependencies.TryGetValue(
                    canonicalName,
                    out List<string> itemDependencies
                ))
            {
                return true;
            }

            return itemDependencies
                .Where(dependency =>
                    !string.IsNullOrWhiteSpace(dependency)
                )
                .GroupBy(
                    ItemSet.GetCanonicalItemName,
                    StringComparer.OrdinalIgnoreCase
                )
                .All(group =>
                    CountItem(inventory, group.Key) >= group.Count()
                );
        }

        private static int CountItem(
            IReadOnlyDictionary<string, int> counts,
            string itemName
        )
        {
            if (counts == null || string.IsNullOrWhiteSpace(itemName))
            {
                return 0;
            }
            string canonicalName =
                ItemSet.GetCanonicalItemName(itemName);
            return counts.TryGetValue(canonicalName, out int count)
                ? count
                : 0;
        }

        private static void AddCount(
            Dictionary<string, int> counts,
            string itemName,
            int count
        )
        {
            if (counts == null || string.IsNullOrWhiteSpace(itemName) ||
                count <= 0)
            {
                return;
            }
            string canonicalName =
                ItemSet.GetCanonicalItemName(itemName);
            counts.TryGetValue(canonicalName, out int previousCount);
            counts[canonicalName] = previousCount + count;
        }

        private static void SetMinimumCount(
            Dictionary<string, int> counts,
            string itemName,
            int count
        )
        {
            if (counts == null || string.IsNullOrWhiteSpace(itemName) ||
                count <= 0)
            {
                return;
            }
            string canonicalName =
                ItemSet.GetCanonicalItemName(itemName);
            counts.TryGetValue(canonicalName, out int previousCount);
            counts[canonicalName] = Math.Max(previousCount, count);
        }

        private static void SetIfTrue(
            Dictionary<string, int> counts,
            string itemName,
            bool isAvailable
        )
        {
            if (isAvailable)
            {
                SetMinimumCount(counts, itemName, 1);
            }
        }
    }
}
