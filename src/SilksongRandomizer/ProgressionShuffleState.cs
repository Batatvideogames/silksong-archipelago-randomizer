using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SilksongRandomizer
{
    internal static class ProgressionCatalogue
    {
        private static readonly JObject Data = Load();

        private static JObject Load()
        {
            using (var stream = typeof(ProgressionCatalogue).Assembly.GetManifestResourceStream("SilksongRandomizer.ProgressionCatalogue.json"))
            {
                if (stream == null) throw new InvalidOperationException("Progression catalogue is missing.");
                using (var reader = new System.IO.StreamReader(stream))
                {
                    var data = JObject.Parse(reader.ReadToEnd());
                    if ((int)data["schema"] != 1) throw new InvalidOperationException("Unsupported progression catalogue.");
                    return data;
                }
            }
        }

        internal static HashSet<string> Set(string name) =>
            new HashSet<string>(Data[name].Values<string>(), StringComparer.Ordinal);

        internal static Dictionary<string, string> Map(string name, bool ignoreCase = false) =>
            ((JObject)Data[name]).Properties().ToDictionary(property => property.Name,
                property => (string)property.Value, ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class ProgressionShuffleState
    {
        public const string EmptyAssignments = "{\"schema\":2,\"wishes\":{},\"bosses\":[]}";
        private static readonly HashSet<string> SupportedWishes = ProgressionCatalogue.Set("wishes");

        internal static readonly HashSet<string> SupportedBosses = ProgressionCatalogue.Set("bosses");

        private static readonly Dictionary<string, string> WishStages = ProgressionCatalogue.Map("wish_stage_aliases");
        private static readonly HashSet<string> WishNotices = ProgressionCatalogue.Set("wish_notices");
        private static readonly HashSet<string> NpcWishes = ProgressionCatalogue.Set("npc_wishes");
        private static readonly HashSet<string> FixedWishFinishes = ProgressionCatalogue.Set("fixed_wish_finishes");

        internal static string WishIdentity(string stage) =>
            stage != null && WishStages.TryGetValue(stage, out string identity) ? identity : stage;

        internal static bool IsWishNotice(string wish) => WishNotices.Contains(wish);

        internal static bool IsNpcWish(string wish) => NpcWishes.Contains(wish);

        internal static bool HasNativeWishFinish(string wish) =>
            FixedWishFinishes.Contains(wish) || IsWishNotice(WishIdentity(wish)) || IsNpcWish(wish);

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
            if (source == "Steel Sentinel") return "Zi in Blasted Steps";
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
