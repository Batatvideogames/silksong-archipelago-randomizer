using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SilksongRandomizer
{
    [Serializable]
    public sealed class ProgressionShuffleState
    {
        public const string EmptyAssignments = "{\"schema\":2,\"wishes\":{},\"bosses\":[]}";
        private static readonly HashSet<string> SupportedWishes = new HashSet<string>(StringComparer.Ordinal) {
            "Building Materials", "Building Materials (Bridge)", "Belltown House Start",
            "Belltown House Mid", "Songclave Donation 1", "Songclave Donation 2",
            "Building Materials (Statue)", "Fine Pins", "Song Pilgrim Cloaks", "Shiny Bell Goomba", "Rock Rollers", "Skull King",
            "Beastfly Hunt", "Ant Trapper", "Broodmother Hunt",
            "Save City Merchant", "Save City Merchant Bridge", "Save Sherma",
            "Save Courier Short", "Save Courier Tall", "Garmond Black Threaded", "Tormented Trobbio", "Song Knight", "Shakra Final Quest",
            "Save the Fleas Pre", "Mossberry Collection Pre", "Pinstress Battle Pre", "Flea Games Pre", "Crow Feathers Pre",
            "Courier Delivery Bonebottom", "Courier Delivery Pilgrims Rest", "Courier Delivery Songclave",
            "Courier Delivery Fleatopia", "Courier Delivery Fixer", "Courier Delivery Dustpens Slave",
            "Courier Delivery Mask Maker", "Great Gourmand", "A Pinsmiths Tools", "Brolly Get", "Mr Mushroom", "Shell Flowers", "Extractor Blue", "Extractor Blue Worms", "Huntress Quest", "Wood Witch Curse", "Doctor Curse Cure"
        };

        internal static readonly HashSet<string> SupportedBosses = new HashSet<string>(StringComparer.Ordinal) {
            "Boss: Bell Beast",
            "Boss: Bell Eater",
            "Boss: Broodmother",
            "Boss: Cogwork Dancers",
            "Boss: Crawfather",
            "Boss: Crust King Khann",
            "Boss: Disgraced Chef Lugoli",
            "Boss: Father of the Flame",
            "Boss: First Sinner",
            "Boss: Forebrothers Signis & Gron",
            "Boss: Fourth Chorus",
            "Boss: Grand Mother Silk",
            "Boss: Great Conchflies",
            "Boss: Groal the Great",
            "Boss: Gurr the Outcast",
            "Boss: Lace (Cradle)",
            "Boss: Last Judge",
            "Boss: Lost Garmond",
            "Boss: Moorwing",
            "Boss: Moss Mother",
            "Boss: Nyleth",
            "Boss: Phantom",
            "Boss: Pinstress",
            "Boss: Plasmified Zango",
            "Boss: Raging Conchfly",
            "Boss: Second Sentinel",
            "Boss: Shrine Guardian Seth",
            "Boss: Sister Splinter",
            "Boss: Skarrsinger Karmelita",
            "Boss: Skull Tyrant (Bone Bottom)",
            "Boss: Skull Tyrant (The Marrow)",
            "Boss: The Unravelled",
            "Boss: Tormented Trobbio",
            "Boss: Trobbio",
            "Boss: Voltvyrm",
            "Boss: Watcher at the Edge",
            "Boss: Widow",
        };

        internal static string WishIdentity(string stage)
        {
            switch (stage)
            {
                case "Huntress Quest Runt": return "Huntress Quest";
                case "Save the Fleas": return "Save the Fleas Pre";
                case "Mossberry Collection 1": return "Mossberry Collection Pre";
                case "Pinstress Battle": return "Pinstress Battle Pre";
                case "Flea Games": return "Flea Games Pre";
                case "Crow Feathers": return "Crow Feathers Pre";
                default: return stage;
            }
        }

        internal static bool IsWishNotice(string wish) =>
            wish == "Save the Fleas Pre" || wish == "Mossberry Collection Pre" ||
            wish == "Pinstress Battle Pre" || wish == "Flea Games Pre" || wish == "Crow Feathers Pre";

        internal static bool IsNpcWish(string wish) => wish == "Great Gourmand" ||
            wish == "Wood Witch Curse" || wish == "Doctor Curse Cure" ||
            wish == "Huntress Quest" || wish == "Huntress Quest Runt" ||
            wish == "Extractor Blue" || wish == "Extractor Blue Worms" ||
            wish == "A Pinsmiths Tools" || wish == "Brolly Get" || wish == "Mr Mushroom" || wish == "Shell Flowers" ||
            wish == "Courier Delivery Bonebottom" || wish == "Courier Delivery Pilgrims Rest" ||
            wish == "Courier Delivery Songclave" || wish == "Courier Delivery Fleatopia" ||
            wish == "Courier Delivery Fixer" || wish == "Courier Delivery Dustpens Slave" ||
            wish == "Courier Delivery Mask Maker";

        internal static bool HasNativeWishFinish(string wish) =>
            wish == "Save City Merchant" || wish == "Save City Merchant Bridge" ||
            wish == "Save Sherma" || wish == "Save Courier Short" || wish == "Save Courier Tall" ||
            wish == "Garmond Black Threaded" || wish == "Tormented Trobbio" || wish == "Song Knight" || wish == "Shakra Final Quest" ||
            IsWishNotice(WishIdentity(wish)) || IsNpcWish(wish);

        public static string NormalizeConfiguration(string json)
        {
            Parse(json, out var wishMap, out var bossMap);
            if (wishMap.Keys.Any(id => !SupportedWishes.Contains(id)) ||
                bossMap.Any(id => !SupportedBosses.Contains(id)))
                throw new InvalidOperationException("Unsupported progression shuffle identities.");
            return new JObject {
                ["schema"] = 2,
                ["wishes"] = new JObject(wishMap.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new JProperty(pair.Key, pair.Value))),
                ["bosses"] = new JArray(bossMap.OrderBy(name => name, StringComparer.Ordinal))
            }.ToString(Formatting.None);
        }

        public void BindConfiguration(string json, string seed)
        {
            string normalized = NormalizeConfiguration(json);
            Parse(normalized, out var wishMap, out var bossMap);
            Bind(normalized, seed, wishMap.Keys, bossMap);
        }

        public bool MatchesConfiguration(string json) =>
            NormalizeConfiguration(string.IsNullOrEmpty(assignmentJson) ? EmptyAssignments : assignmentJson) ==
            NormalizeConfiguration(json);

        public string assignmentJson = string.Empty;
        public string seedIdentity = string.Empty;
        public HashSet<string> receivedBossCredits = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> defeatedBosses = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> unlockedWishOffers = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> acceptedWishes = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> completedWishes = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> appliedEffects = new HashSet<string>(StringComparer.Ordinal);

        [NonSerialized] [XmlIgnore] private string cachedJson;
        [NonSerialized] [XmlIgnore] private Dictionary<string, string> wishes;
        [NonSerialized] [XmlIgnore] private HashSet<string> bosses;

        [XmlIgnore] public bool HasWishAssignments { get { EnsureParsed(); return wishes.Count != 0; } }
        [XmlIgnore] public bool HasBossAssignments { get { EnsureParsed(); return bosses.Count != 0; } }

        public void Bind(string json, string seed, IEnumerable<string> wishIds, IEnumerable<string> bossIds)
        {
            if (string.IsNullOrWhiteSpace(seed))
                throw new ArgumentException("Progression assignments need a seed identity.");
            Parse(json, out var nextWishes, out var nextBosses);
            ValidateIds(nextWishes, wishIds, "wish");
            if (!nextBosses.SetEquals(bossIds))
                throw new InvalidOperationException("Unexpected boss credit identities.");
            if (!string.IsNullOrEmpty(assignmentJson))
            {
                EnsureParsed();
                if (seedIdentity != seed || !Same(wishes, nextWishes) || !bosses.SetEquals(nextBosses))
                    throw new InvalidOperationException("Progression assignments do not match this save.");
            }
            else if (HasRecordedProgress())
                throw new InvalidOperationException("Cannot assign progression shuffles after recording progress.");
            ValidateRecordedProgress(nextWishes, nextBosses);
            assignmentJson = json;
            seedIdentity = seed;
            wishes = nextWishes;
            bosses = nextBosses;
            cachedJson = json;
        }

        internal IEnumerable<string> WishOfferIds { get { EnsureParsed(); return wishes.Keys; } }

        internal bool IsWishOfferUnlocked(string offer) => unlockedWishOffers.Contains(offer);

        internal bool RecordWishOfferUnlocked(string offer)
        {
            EnsureParsed();
            return wishes.ContainsKey(offer) && unlockedWishOffers.Add(offer);
        }

        public string AssignedWish(string offer)
        {
            EnsureParsed();
            return wishes.TryGetValue(offer, out string target) ? target : null;
        }

        public string OfferForWish(string wish)
        {
            EnsureParsed();
            foreach (var pair in wishes)
                if (pair.Value == wish) return pair.Key;
            return null;
        }

        public string WishTurnInBoard(string wish)
        {
            if (HasNativeWishFinish(wish)) return null;
            switch (OfferForWish(wish))
            {
                case "Building Materials":
                case "Building Materials (Bridge)":
                case "Building Materials (Statue)":
                case "Rock Rollers":
                case "Skull King":
                case "Save the Fleas Pre":
                case "Mossberry Collection Pre": return "Bone Bottom";
                case "Belltown House Start":
                case "Belltown House Mid":
                case "Shiny Bell Goomba":
                case "Beastfly Hunt":
                case "Ant Trapper":
                case "Save Courier Short":
                case "Save Courier Tall":
                case "Garmond Black Threaded":
                case "Shakra Final Quest":
                case "Pinstress Battle Pre":
                case "Flea Games Pre":
                case "Crow Feathers Pre": return "Bellhart";
                case "Songclave Donation 1":
                case "Songclave Donation 2":
                case "Fine Pins":
                case "Song Pilgrim Cloaks":
                case "Broodmother Hunt":
                case "Save City Merchant":
                case "Save City Merchant Bridge":
                case "Save Sherma":
                case "Tormented Trobbio":
                case "Song Knight": return "Songclave";
                default: return null;
            }
        }

        public string WishTurnInNpc(string wish)
        {
            if (HasNativeWishFinish(wish)) return null;
            string source = OfferForWish(wish);
            if (source == "Great Gourmand") return "the Gourmand's servant in Choral Chambers";
            if (source == "A Pinsmiths Tools") return "Pinmaster Plinney in Bellhart";
            if (source == "Brolly Get") return "the Seamstress in Far Fields";
            if (source == "Mr Mushroom") return "the Herald tablet in Fleatopia";
            if (source == "Extractor Blue" || source == "Extractor Blue Worms") return "the Alchemist in Wormways";
            if (source == "Huntress Quest") return "the Huntress in Putrified Ducts";
            if (source == "Doctor Curse Cure") return "Yarnaby in Greymoor";
            if (source == "Shell Flowers" || source == "Wood Witch Curse") return "Greyroot in Shellwood";
            return IsNpcWish(source) ? "the couriers in Bellhart" : null;
        }

        public bool RecordWishAccepted(string wish)
        {
            if (OfferForWish(wish) == null) return false;
            return acceptedWishes.Add(wish);
        }

        public bool RecordWishCompleted(string wish)
        {
            if (OfferForWish(wish) == null || !acceptedWishes.Contains(wish)) return false;
            return completedWishes.Add(wish);
        }

        public bool TryGetBossDefeat(string identity, out bool completed)
        {
            EnsureParsed();
            completed = defeatedBosses.Contains(identity);
            return bosses.Contains(identity);
        }

        public bool TryGetBossCredit(string identity, out bool completed)
        {
            EnsureParsed();
            completed = HasBossCredit(identity);
            return bosses.Contains(identity);
        }

        public bool RecordBossDefeated(string encounter)
        {
            EnsureParsed();
            if (!bosses.Contains(encounter)) return false;
            return defeatedBosses.Add(encounter);
        }

        public bool HasBossCredit(string identity) => receivedBossCredits.Contains(identity);

        public void ReceiveBossCredit(string identity)
        {
            if (!SupportedBosses.Contains(identity))
                throw new InvalidOperationException("Unknown boss credit: " + identity);
            receivedBossCredits.Add(identity);
        }

        private bool HasRecordedProgress() =>
            receivedBossCredits.Count != 0 || defeatedBosses.Count != 0 || unlockedWishOffers.Count != 0 || acceptedWishes.Count != 0 ||
            completedWishes.Count != 0 || appliedEffects.Count != 0;

        private void ValidateRecordedProgress(Dictionary<string, string> wishMap, HashSet<string> bossMap)
        {
            if (!receivedBossCredits.IsSubsetOf(SupportedBosses) ||
                !unlockedWishOffers.IsSubsetOf(wishMap.Keys) ||
                !defeatedBosses.IsSubsetOf(bossMap) ||
                !acceptedWishes.IsSubsetOf(wishMap.Values) ||
                !completedWishes.IsSubsetOf(acceptedWishes))
                throw new InvalidOperationException("Saved progression contains unknown or incomplete events.");
        }

        private void EnsureParsed()
        {
            if (wishes != null && bosses != null && cachedJson == assignmentJson) return;
            if (string.IsNullOrEmpty(assignmentJson))
            {
                wishes = new Dictionary<string, string>(StringComparer.Ordinal);
                bosses = new HashSet<string>(StringComparer.Ordinal);
            }
            else Parse(assignmentJson, out wishes, out bosses);
            cachedJson = assignmentJson;
        }

        private static void Parse(string json, out Dictionary<string, string> wishes,
            out HashSet<string> bosses)
        {
            JObject data = JObject.Parse(json, new JsonLoadSettings {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            if (data.Properties().Count() != 3 || data["schema"]?.Type != JTokenType.Integer ||
                (long)data["schema"] != 2 || data["wishes"] == null || data["bosses"] == null)
                throw new InvalidOperationException("This seed uses an older boss credit format. Generate a new seed with the matching APWorld.");
            wishes = ReadPermutation(data["wishes"]);
            if (!(data["bosses"] is JArray ids) || ids.Any(id => id.Type != JTokenType.String))
                throw new InvalidOperationException("Boss credit identities must be a list.");
            bosses = new HashSet<string>(ids.Select(id => (string)id), StringComparer.Ordinal);
            if (bosses.Count != ids.Count || !bosses.IsSubsetOf(SupportedBosses))
                throw new InvalidOperationException("Unknown or duplicate boss credit identity.");
        }

        private static Dictionary<string, string> ReadPermutation(JToken token)
        {
            if (!(token is JObject obj))
                throw new InvalidOperationException("Progression assignments must be an object.");
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in obj.Properties())
            {
                if (property.Value.Type != JTokenType.String || string.IsNullOrEmpty(property.Name) ||
                    string.IsNullOrEmpty((string)property.Value))
                    throw new InvalidOperationException("Invalid progression assignment identity.");
                result.Add(property.Name, (string)property.Value);
            }
            if (!new HashSet<string>(result.Keys, StringComparer.Ordinal).SetEquals(result.Values))
                throw new InvalidOperationException("Progression assignments must be a permutation.");
            return result;
        }

        private static void ValidateIds(Dictionary<string, string> mapping, IEnumerable<string> ids, string kind)
        {
            if (!new HashSet<string>(ids, StringComparer.Ordinal).SetEquals(mapping.Keys))
                throw new InvalidOperationException("Unexpected " + kind + " identities in progression assignments.");
        }

        private static bool Same(Dictionary<string, string> first, Dictionary<string, string> second) =>
            first.Count == second.Count && first.All(pair =>
                second.TryGetValue(pair.Key, out string value) && value == pair.Value);
    }
}
