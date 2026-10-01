using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SilksongRandomizer.Patches;
using SilksongRandomizer.AlphabetMode;
using UnityEngine;

namespace SilksongRandomizer
{
    internal sealed class ArchipelagoInventoryRow
    {
        internal string Title;
        internal string Detail;
        internal string Icon;
        internal bool Dim;
        internal bool Required;
        internal bool? LocationCompleted;
        internal string CompletionText;
        internal float Points;

        internal ArchipelagoInventoryRow(string title, string detail = "", string icon = null, bool dim = false)
        {
            Title = title;
            Detail = detail;
            Icon = icon;
            Dim = dim;
        }
    }

    internal sealed class ArchipelagoInventorySheet
    {
        internal string Key;
        internal string LeftHeading;
        internal string RightHeading;
        internal int FleaCount;
        internal int TrapsReceived;
        internal string DeathSummary;
        internal bool IconGrid;
        internal bool SoulChecklist;
        internal bool BellProgress;
        internal readonly List<ArchipelagoInventoryRow> Caravan = new List<ArchipelagoInventoryRow>();
        internal readonly List<ArchipelagoInventoryRow> Fleas = new List<ArchipelagoInventoryRow>();
        internal readonly List<ArchipelagoInventoryRow> Bells = new List<ArchipelagoInventoryRow>();
        internal readonly List<ArchipelagoInventoryRow> CursedRequirements = new List<ArchipelagoInventoryRow>();
        internal readonly List<ArchipelagoInventoryRow> Bosses = new List<ArchipelagoInventoryRow>();
        internal readonly List<ArchipelagoInventoryRow> Melodies = new List<ArchipelagoInventoryRow>();
        internal readonly List<ArchipelagoInventoryRow> Story = new List<ArchipelagoInventoryRow>();
        internal readonly List<ArchipelagoInventoryRow> Left = new List<ArchipelagoInventoryRow>();
        internal readonly List<ArchipelagoInventoryRow> Right = new List<ArchipelagoInventoryRow>();
    }

    internal static class ArchipelagoInventoryModel
    {
        internal const int RowsPerColumn = 6;
        internal const int RecentItemCount = 3;
        internal const int BossesPerPage = 20;
        internal const int SoulRowsPerColumn = 15;

        internal static string BossIconBadge(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            string name = itemName.Replace('_', ' ').Trim();
            foreach (string prefix in new[] { "Boss: ", "Boss Credit: ", "Story credit: " })
            {
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                switch (name.Substring(prefix.Length))
                {
                    case "Skull Tyrant (The Marrow)":
                    case "Great Conchflies": return "1";
                    case "Skull Tyrant (Bone Bottom)":
                    case "Raging Conchfly": return "2";
                    default: return null;
                }
            }
            return null;
        }

        internal static string Plain(string value) => (value ?? string.Empty)
            .Replace("<", "").Replace(">", "").Replace('\r', ' ').Replace('\n', ' ');

        internal static List<ArchipelagoInventorySheet> Build(SaveState state, QuestCompleteTotalGroup soul)
        {
            var pages = new List<ArchipelagoInventorySheet>();
            if (state?.IsRoomBound != true) return pages;
            var history = History(state, RecentItemCount);
            var overview = new ArchipelagoInventorySheet {
                Key = "overview", LeftHeading = "Your Journey", RightHeading = "Recently Received",
                FleaCount = state.GetFleaHuntProgressCount(),
                TrapsReceived = CountReceivedTraps(state),
                DeathSummary = "Deaths: " + Math.Max(0, state.deathCount) +
                    (state.deathLink ? " | Total Deaths (including DeathLink): " +
                        ((long)Math.Max(0, state.deathCount) + Math.Max(0, state.deathLinkDeathCount)) : "")
            };
            overview.Fleas.AddRange(FleaPortraits(state, overview.FleaCount));
            overview.Caravan.AddRange(CaravanDestinations(overview.FleaCount));
            overview.Left.Add(new ArchipelagoInventoryRow(GoalName(state.goal),
                state.goalCompleted ? "Goal completed" : GoalDescription(state)));
            overview.Left.Add(new ArchipelagoInventoryRow("Fleas: " + state.GetFleaHuntProgressCount() +
                (state.goal == Archipelago.FleaHuntGoal ? " / " + state.fleaHuntGoalCount : ""),
                state.IsRandomized(ItemType.Flea) ? "Received fleas" : "Rescued fleas", "Flea"));
            if (state.goal == Archipelago.ActThreeGoal)
            {
                overview.Left.Add(new ArchipelagoInventoryRow("Silk and Soul", SoulProgress(state, soul)));
            }
            if (state.goal == Archipelago.SpellingBeeGoal)
            {
                var letters = (state.spellingBeePhrase ?? "").ToUpperInvariant()
                    .Where(c => c >= 'A' && c <= 'Z').Distinct().OrderBy(c => c).ToArray();
                string missing = new string(letters.Where(c => state.receivedItems?.Contains("Letter: " + c) != true).ToArray());
                overview.Left.Add(new ArchipelagoInventoryRow("Required letters",
                    missing.Length == 0 ? "All required letters received" : "Missing: " + missing));
            }
            if (ShowMelodies(state))
            {
                int owned = Melodies(state).Take(3).Count(row => !row.Dim);
                overview.Left.Add(new ArchipelagoInventoryRow("Threefold Melody: " + owned + " / 3",
                    "Three melodies open the way", MelodyLocationManifest.ArchitectsMelody));
            }
            if (state.progressionShuffle?.HasBossAssignments == true)
            {
                var bosses = state.progressionShuffle.BossCreditIds.ToArray();
                overview.Left.Add(new ArchipelagoInventoryRow("Boss Credits: " +
                    bosses.Count(id => OwnsBossCredit(state, id)) + " / " + bosses.Length,
                    "Received credits are separate from physical defeats"));
            }
            overview.Left.Add(new ArchipelagoInventoryRow("Return to " + FastTravelUtil.GetPreferredHubName(),
                "F4: Return to safety   |   F3: Change destination"));
            overview.Right.AddRange(history);
            if (overview.Right.Count == 0)
                overview.Right.Add(new ArchipelagoInventoryRow("No items received yet"));
            pages.Add(overview);
            if (state.goal == Archipelago.ActThreeGoal)
            {
                var rows = new List<ArchipelagoInventoryRow>();
                if (soul == null)
                {
                    rows.Add(new ArchipelagoInventoryRow("Wish progress unavailable", "Open this page again after the game has loaded."));
                }
                else
                {
                    overview.Story.AddRange(QuestRequirementPatches.SoulSnareStoryRequirements(soul)
                        .Select(pair => new ArchipelagoInventoryRow(StoryRequirementName(pair.Key),
                            pair.Value ? "Fulfilled  |  Required" : "Incomplete  |  Required",
                            pair.Key == "hasDoubleJump" ? "Faydown Cloak" : null, !pair.Value) { Required = true }));
                    rows.AddRange(overview.Story);
                    rows.AddRange(soul.Quests.Where(q => state.gameMode != SteelSoulSettings.SteelSoul ||
                            q.Quest.name != "Courier Delivery Dustpens Slave")
                        .Select(q => new ArchipelagoInventoryRow(ReadWishName(q.Quest),
                            icon: null, dim: !q.Quest.IsCompleted) { Required = q.IsRequired, Points = q.Value })
                        .OrderByDescending(q => q.Required).ThenBy(q => q.Title));
                }
                if (soul == null) AddSheets(pages, "soul", "Silk and Soul", SoulProgress(state, soul), rows);
                else AddSoulSheets(pages, SoulProgress(state, soul), rows);
            }

            if (state.progressionShuffle?.HasBossAssignments == true)
            {
                var shuffle = state.progressionShuffle;
                var bosses = shuffle.BossCreditIds.OrderBy(id => id
                    .Replace("Skull Tyrant (The Marrow)", "Skull Tyrant (1)")
                    .Replace("Skull Tyrant (Bone Bottom)", "Skull Tyrant (2)"), StringComparer.Ordinal).Select(id => {
                    bool owned = OwnsBossCredit(state, id);
                    bool defeated = shuffle.defeatedBosses.Contains(id);
                    bool checkedLocation = state.checkedLocations?.Contains(id) == true;
                    return new ArchipelagoInventoryRow(id.StartsWith("Boss: ") ? id.Substring(6) : id,
                        owned ? "Credit received" : "Credit missing", id, !owned) {
                            LocationCompleted = defeated || checkedLocation,
                            CompletionText = defeated ? "Defeated" : checkedLocation ? "Did the location" : "Not defeated"
                        };
                }).ToList();
                overview.Bosses.AddRange(bosses);
                AddSheets(pages, "bosses", "Boss Credits", "Received / Physically Defeated", bosses);
            }
            if (ShowMelodies(state))
            {
                overview.Melodies.AddRange(Melodies(state));
            }
            var music = new ArchipelagoInventorySheet {
                Key = overview.Melodies.Count > 0 ? "melodies:0" : "bells:0",
                LeftHeading = overview.Melodies.Count > 0 ? "Bells & Melodies" : "Bells",
                BellProgress = true
            };
            music.Bells.AddRange(Bells(state));
            music.Left.AddRange(overview.Melodies);
            if (state.goal == Archipelago.CursedEndingGoal)
            {
                var pd = PlayerData.instance;
                bool heartsDelivered = false;
                if (pd?.QuestCompletionData != null)
                {
                    var completion = pd.QuestCompletionData.GetData("Shell Flowers");
                    heartsDelivered = completion.IsCompleted || completion.WasEverCompleted;
                }
                int bud = QuestItemProgress(state, "Twisted Bud", "Wood Witch Item", 1,
                    pd?.WoodWitchGaveMandrake == true);
                int hearts = QuestItemProgress(state, "Pollip Heart", "Shell Flower", 6, heartsDelivered);
                music.CursedRequirements.Add(new ArchipelagoInventoryRow("Twisted Bud   " + bud + " / 1",
                    icon: "Twisted Bud", dim: bud < 1));
                music.CursedRequirements.Add(new ArchipelagoInventoryRow("Pollip Hearts   " + hearts + " / 6",
                    icon: "Pollip Heart", dim: hearts < 6));
            }
            pages.Add(music);
            return pages;
        }

        private static int QuestItemProgress(SaveState state, string itemName, string assetName,
            int target, bool delivered)
        {
            if (delivered) return target;
            int received = (state.receivedItemHistory ?? new List<string>())
                .Take(Math.Max(0, state.receivedItemIndex))
                .Count(name => string.Equals(ItemSet.GetCanonicalItemName(name), itemName, StringComparison.OrdinalIgnoreCase));
            if (state.receivedItems?.Contains(itemName) == true) received = Math.Max(1, received);
            long native = 0;
            if (PlayerData.instance?.Collectables != null)
            {
                var data = PlayerData.instance.Collectables.GetData(assetName);
                native = (long)Math.Max(0, data.Amount) + Math.Max(0, data.AmountWhileHidden);
            }
            return (int)Math.Min(target, Math.Max(received, native));
        }

        private static bool OwnsBossCredit(SaveState state, string id) =>
            state.progressionShuffle.HasBossCredit(id) ||
            (id.StartsWith("Boss: ", StringComparison.Ordinal) &&
             state.receivedItems?.Contains("Boss Credit: " + id.Substring(6)) == true);

        private static List<ArchipelagoInventoryRow> FleaPortraits(SaveState state, int total)
        {
            string[] names = { FleaPatches.KrattItemName, FleaPatches.VogItemName, FleaPatches.HugeFleaItemName };
            string[] flags = { "CaravanLechSaved", "MetTroupeHunterWild", "tamedGiantFlea" };
            var named = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                bool owned = state.receivedItems?.Contains(names[i]) == true ||
                    (!state.IsRandomized(ItemType.Flea) && PlayerData.instance?.GetBool(flags[i]) == true);
                if (owned) named.Add(names[i]);
            }
            int ordinary = Math.Max(0, Math.Min(27, total - named.Count));
            var fleas = new List<ArchipelagoInventoryRow>();
            if (state.IsRandomized(ItemType.Flea))
            {
                var fleaNames = new HashSet<string>(state.items.items.Where(item => item.Type == ItemType.Flea)
                    .Select(item => item.Name), StringComparer.OrdinalIgnoreCase);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string receipt in (state.receivedItemHistory ?? new List<string>())
                    .Take(Math.Max(0, state.receivedItemIndex)))
                {
                    string name = ItemSet.GetCanonicalItemName(receipt);
                    if (string.IsNullOrEmpty(name) || !fleaNames.Contains(name) ||
                        state.receivedItems?.Contains(name) != true || !seen.Add(name)) continue;
                    if (named.Remove(name)) fleas.Add(new ArchipelagoInventoryRow("", icon: name));
                    else if (!names.Contains(name) && ordinary > 0)
                    {
                        fleas.Add(new ArchipelagoInventoryRow("", icon: "Flea"));
                        ordinary--;
                    }
                }
            }
            fleas.AddRange(Enumerable.Range(0, ordinary).Select(i => new ArchipelagoInventoryRow("", icon: "Flea")));
            fleas.AddRange(named.Select(name => new ArchipelagoInventoryRow("", icon: name)));
            return fleas;
        }

        private static IEnumerable<ArchipelagoInventoryRow> CaravanDestinations(int fleas)
        {
            var pd = PlayerData.instance;
            int stage = pd == null ? 0 : (int)pd.CaravanTroupeLocation;
            bool Ready(string flag) => pd?.GetBool(flag) == true;
            yield return new ArchipelagoInventoryRow("Greymoor", dim: !(stage >= 1 || fleas >= 5));
            yield return new ArchipelagoInventoryRow("Blasted Steps", dim: !(stage >= 2 ||
                (stage == 1 && fleas >= 12 && Ready("MetCaravanTroupeLeaderGreymoor") &&
                 Ready("defeatedLastJudge") && Ready("SeenLastJudgeGateOpen") &&
                 Ready("CaravanTroupeLeaderCanLeaveGreymoor"))));
            yield return new ArchipelagoInventoryRow("Pale Lake", dim: !(stage >= 3 ||
                (stage == 2 && fleas >= 22 && Ready("MetCaravanTroupeLeaderJudge") &&
                 Ready("SeenFleatopiaEmpty") && Ready("CaravanTroupeLeaderCanLeaveJudge"))));
        }

        internal static int CountReceivedTraps(SaveState state)
        {
            if (state.receivedItemHistory == null) return 0;
            var traps = new HashSet<string>(state.items.items.Where(item => item.Type == ItemType.Trap)
                .Select(item => item.Name), StringComparer.OrdinalIgnoreCase);
            return state.receivedItemHistory.Take(Math.Max(0, state.receivedItemIndex))
                .Count(name => !string.IsNullOrWhiteSpace(name) && traps.Contains(ItemSet.GetCanonicalItemName(name)));
        }

        internal static string ReadWishName(FullQuestBase quest)
        {
            AlphabetModeManager.BeginTextBypass();
            try
            {
                string title = quest.DisplayName;
                return string.IsNullOrWhiteSpace(title) ? quest.name : title;
            }
            finally
            {
                AlphabetModeManager.EndTextBypass();
            }
        }

        private static void AddSoulSheets(List<ArchipelagoInventorySheet> pages, string progress,
            IReadOnlyList<ArchipelagoInventoryRow> rows)
        {
            var required = rows.Where(row => row.Required).ToArray();
            var wishes = rows.Where(row => !row.Required).ToArray();
            int count = Math.Max(1, Math.Max((required.Length + SoulRowsPerColumn - 1) / SoulRowsPerColumn,
                (wishes.Length + SoulRowsPerColumn * 2 - 1) / (SoulRowsPerColumn * 2)));
            for (int i = 0; i < count; i++)
            {
                var sheet = new ArchipelagoInventorySheet {
                    Key = "soul:" + i, LeftHeading = "Silk and Soul", RightHeading = progress, SoulChecklist = true
                };
                sheet.Left.AddRange(required.Skip(i * SoulRowsPerColumn).Take(SoulRowsPerColumn));
                sheet.Right.AddRange(wishes.Skip(i * SoulRowsPerColumn * 2).Take(SoulRowsPerColumn * 2));
                pages.Add(sheet);
            }
        }

        internal static void AddSheets(List<ArchipelagoInventorySheet> pages, string key, string left,
            string right, IReadOnlyList<ArchipelagoInventoryRow> rows)
        {
            int columnSize = key == "bosses" ? BossesPerPage / 2 : RowsPerColumn;
            for (int offset = 0; offset < rows.Count; offset += columnSize * 2)
            {
                var page = new ArchipelagoInventorySheet { Key = key + ":" + offset, LeftHeading = left, RightHeading = right, IconGrid = key == "bosses" };
                page.Left.AddRange(rows.Skip(offset).Take(columnSize));
                page.Right.AddRange(rows.Skip(offset + columnSize).Take(columnSize));
                pages.Add(page);
            }
        }

        internal static List<ArchipelagoInventoryRow> History(SaveState state, int limit = int.MaxValue)
        {
            var rows = new List<ArchipelagoInventoryRow>();
            var history = state.receivedItemHistory;
            if (history == null) return rows;
            for (int i = Math.Min(history.Count, Math.Max(0, state.receivedItemIndex)) - 1; i >= 0 && rows.Count < limit; i--)
            {
                string name = history[i];
                if (string.IsNullOrWhiteSpace(name)) continue;
                string sender = "";
                if (state.receivedItemReceiptsInitialized && state.receivedItemReceipts != null &&
                    i < state.receivedItemReceipts.Count)
                {
                    var receipt = state.receivedItemReceipts[i];
                    if (receipt != null && string.Equals(receipt.name, name, StringComparison.Ordinal))
                        sender = "From " + (Archipelago.Instance?.GetReceivedItemSender(state, receipt.player) ??
                            (receipt.player == 0 ? "Server" : "Player " + receipt.player));
                }
                rows.Add(new ArchipelagoInventoryRow(name, sender, name));
            }
            return rows;
        }

        private static string ContentScope(SaveState state)
        {
            if (state.contentScope == Archipelago.ActOneGoal || state.contentScope == Archipelago.ActTwoGoal ||
                state.contentScope == Archipelago.ActThreeGoal) return state.contentScope;
            return state.goal == Archipelago.CursedEndingGoal ? Archipelago.ActTwoGoal : state.goal;
        }

        private static bool ShowMelodies(SaveState state)
        {
            string scope = ContentScope(state);
            return scope != Archipelago.ActOneGoal &&
                (scope == Archipelago.ActTwoGoal || scope == Archipelago.ActThreeGoal || state.IsRandomized(ItemType.Melody));
        }

        private static List<ArchipelagoInventoryRow> Bells(SaveState state)
        {
            string[] areas = { "The Marrow", "Deep Docks", "Greymoor", "Shellwood", "Bellhart" };
            string[] flags = { "bellShrineBoneForest", "bellShrineWilds", "bellShrineGreymoor",
                "bellShrineShellwood", "bellShrineBellhart" };
            bool randomized = state.IsRandomized(ItemType.BellShrine);
            var result = new List<ArchipelagoInventoryRow>();
            for (int i = 0; i < areas.Length; i++)
            {
                bool owned = randomized ? state.receivedItems?.Contains("Bell: " + areas[i]) == true :
                    PlayerData.instance?.GetBool(flags[i]) == true;
                result.Add(new ArchipelagoInventoryRow(areas[i],
                    randomized ? (owned ? "Received" : "Missing") : (owned ? "Rung" : "Not rung"),
                    "Bell", !owned) { LocationCompleted = randomized ?
                        (PlayerData.instance?.GetBool(flags[i]) == true ||
                         state.checkedLocations?.Contains(areas[i] + " - Bellshrine") == true) : (bool?)null });
            }
            return result;
        }

        private static List<ArchipelagoInventoryRow> Melodies(SaveState state)
        {
            var pd = PlayerData.instance;
            string[] names = { MelodyLocationManifest.ArchitectsMelody, MelodyLocationManifest.ConductorsMelody,
                MelodyLocationManifest.VaultkeepersMelody, MelodyLocationManifest.ElegyOfTheDeep, MelodyLocationManifest.BeastlingCall };
            bool[] native = { pd?.HasMelodyArchitect == true, pd?.HasMelodyConductor == true,
                pd?.HasMelodyLibrarian == true, pd?.hasNeedolinMemoryPowerup == true, pd?.UnlockedFastTravelTeleport == true };
            var result = new List<ArchipelagoInventoryRow>();
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 2 && (ContentScope(state) == Archipelago.ActTwoGoal ||
                    (ContentScope(state) != Archipelago.ActThreeGoal && !state.IsRandomized(ItemType.Melody)))) continue;
                bool owned = state.IsRandomized(ItemType.Melody) ? state.receivedItems?.Contains(names[i]) == true : native[i];
                result.Add(new ArchipelagoInventoryRow(names[i], owned ? "Acquired" : "Missing", names[i], !owned));
            }
            return result;
        }

        private static string SoulProgress(SaveState state, QuestCompleteTotalGroup soul) =>
            (soul == null ? "?" : soul.CurrentValueCount.ToString("0.#", CultureInfo.InvariantCulture)) +
            " / " + QuestRequirementPatches.SoulSnarePointTarget(state) + " points" +
            (soul == null ? "" : QuestRequirementPatches.SoulSnarePrerequisitesMet(soul) ? "  |  Prerequisites met" : "  |  Prerequisites incomplete");

        private static string StoryRequirementName(string field)
        {
            switch (field)
            {
                case "CaravanTroupeLocation": return "Caravan reaches Fleatopia";
                case "hasDoubleJump": return "Faydown Cloak";
                case "defeatedLaceTower": return "Lace (Cradle) story requirement";
                case "BelltownGreeterHouseFullDlg": return "Speak to Bellhart's greeter at home";
                default: return "Additional story requirements";
            }
        }

        private static string GoalName(string goal)
        {
            switch (goal)
            {
                case Archipelago.ActOneGoal: return "Act 1";
                case Archipelago.ActTwoGoal: return "Act 2";
                case Archipelago.ActThreeGoal: return "Act 3";
                case Archipelago.CursedEndingGoal: return "Cursed Ending";
                case Archipelago.FleaHuntGoal: return "Flea Hunt";
                case Archipelago.SpellingBeeGoal: return "Spelling Bee";
                default: return "Archipelago";
            }
        }

        private static string GoalDescription(SaveState state)
        {
            switch (state.goal)
            {
                case Archipelago.ActOneGoal: return "Reach Act 2";
                case Archipelago.ActTwoGoal: return "Defeat Grand Mother Silk";
                case Archipelago.ActThreeGoal: return "Defeat Lost Lace";
                case Archipelago.CursedEndingGoal: return "Complete the Cursed Ending";
                case Archipelago.FleaHuntGoal: return "Collect " + state.fleaHuntGoalCount + " fleas";
                case Archipelago.SpellingBeeGoal: return state.spellingBeePhrase;
                default: return "";
            }
        }
    }
}
