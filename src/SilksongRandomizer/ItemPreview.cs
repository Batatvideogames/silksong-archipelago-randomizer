using Archipelago.MultiClient.Net.Enums;
using SilksongRandomizer.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TeamCherry.Localization;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class ItemPreview
    {
        internal static bool DisguisesEnabled => SaveState.Instance?.trapDisguises == true;

        internal sealed class Presentation
        {
            internal string Name;
            internal string Description;
            internal Sprite Icon;
            internal float Scale = 1f;
        }

        private sealed class Disguise
        {
            internal readonly string Item;
            internal readonly string Key;
            internal Disguise(string item, string key) { Item = item; Key = key; }
        }

        private static readonly Disguise[] Disguises =
        {
            new Disguise("Faydown Cloak", "DRESS_DJ"),
            new Disguise("Drifter's Cloak", "DRESS_BROLLY"),
            new Disguise("Cling Grip", "WALLJUMP"),
            new Disguise("Clawline", "SKILL_HARPOON"),
            new Disguise("Needolin", "SKILL_NEEDOLIN"),
            new Disguise("Silk Soar", "SKILL_ASCENT"),
            new Disguise("Swift Step", "SKILL_SPRINT"),
        };

        private static readonly Dictionary<string, SaveState.HintData> Previews =
            new Dictionary<string, SaveState.HintData>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Task<Dictionary<string, SaveState.HintData>>> Pending =
            new Dictionary<string, Task<Dictionary<string, SaveState.HintData>>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> RetryAfter =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<Sprite, Sprite> FlippedIcons = new Dictionary<Sprite, Sprite>();
        private static readonly HashSet<Sprite> FailedIcons = new HashSet<Sprite>();
        private static SaveState owner;
        private static Archipelago client;
        private static string seed;
        private static int slot;
        private static int team;

        internal static Presentation Get(string location, bool typo, bool request = false)
        {
            SaveState state = SaveState.Instance;
            if (state == null || string.IsNullOrWhiteSpace(location)) return null;
            location = LocationSet.GetCanonicalLocationName(location);
            Synchronize(state);
            SaveState.HintData hint;
            if (!ShopPatches.TryGetPresentationHintData(location, out hint) &&
                !Previews.TryGetValue(location, out hint))
            {
                if (Pending.TryGetValue(location, out var pending))
                {
                    if (!pending.IsCompleted) return null;
                    Pending.Remove(location);
                    if (!pending.IsCanceled && !pending.IsFaulted &&
                        pending.Result.TryGetValue(location, out hint) && hint != null)
                        Previews[location] = hint;
                    else
                    {
                        if (pending.IsFaulted) _ = pending.Exception;
                        RetryAfter[location] = Time.realtimeSinceStartup + 5f;
                    }
                }
                if (hint == null && request && client != null && client.Connected &&
                    client.RoomSeed == state.roomSeed && client.Team == state.team && client.Slot == state.slot &&
                    (!RetryAfter.TryGetValue(location, out float retry) || Time.realtimeSinceStartup >= retry))
                    Pending[location] = client.RequestHintsAsync(new[] { location },
                        HintCreationPolicy.None, "wish reward preview");
            }
            return hint == null ? null : Create(location, hint, typo);
        }

        private static void Synchronize(SaveState state)
        {
            if (ReferenceEquals(owner, state) && ReferenceEquals(client, Archipelago.Instance) &&
                seed == state.roomSeed && slot == state.slot && team == state.team) return;
            owner = state;
            client = Archipelago.Instance;
            seed = state.roomSeed;
            slot = state.slot;
            team = state.team;
            Previews.Clear();
            Pending.Clear();
            RetryAfter.Clear();
        }

        private static Presentation Create(string location, SaveState.HintData hint, bool typo)
        {
            bool trap = DisguisesEnabled && (hint.flags & ItemFlags.Trap) != 0;
            bool native = string.Equals(hint.game, "Hollow Knight: Silksong", StringComparison.Ordinal);
            Disguise disguise = trap ? ChooseDisguise(location) :
                native ? Disguises.FirstOrDefault(x => x.Item == ItemSet.GetCanonicalItemName(hint.item)) : null;
            string itemName = disguise == null ? hint.item : Language.Get("INV_NAME_" + disguise.Key, "UI");
            string description = disguise == null ? null : Language.Get("INV_DESC_" + disguise.Key, "UI");
            if (trap && typo) itemName = MakeTypo(itemName, StableSeed(location, "typo"));
            string displayName = string.IsNullOrWhiteSpace(hint.user) ? itemName : hint.user + "'s " + itemName;
            var plugin = RandomizerPlugin.Instance;
            Sprite fallback = plugin?.GetItemClassificationIcon(trap ? ItemFlags.Advancement : hint.flags);
            float scale = 1f;
            Sprite icon = native || trap
                ? ItemIcons.GetSprite(disguise?.Item ?? hint.item, fallback, out scale)
                : fallback;
            if (trap && icon != null && icon != fallback)
            {
                icon = FlipIcon(icon);
                if (icon == null) { icon = fallback; scale = 1f; }
            }
            if (string.IsNullOrEmpty(description))
                description = displayName + ".\r\n" + Classification(hint.flags);
            return new Presentation { Name = displayName, Description = description, Icon = icon, Scale = scale };
        }

        internal static string CrestName(string location, string item, ItemFlags flags)
        {
            if (!DisguisesEnabled || (flags & ItemFlags.Trap) == 0) return item;
            Disguise disguise = ChooseDisguise(location);
            return MakeTypo(Language.Get("INV_NAME_" + disguise.Key, "UI"), StableSeed(location, "typo"));
        }

        private static string Classification(ItemFlags flags)
        {
            if ((flags & ItemFlags.Advancement) != 0) return "It is very important!";
            if ((flags & ItemFlags.NeverExclude) != 0) return "Seems useful.";
            return (flags & ItemFlags.Trap) != 0 ? "Seems fun!" : "Seems not important.";
        }

        private static Disguise ChooseDisguise(string location)
        {
            return Disguises[StableSeed(location, "item") % Disguises.Length];
        }

        private static int StableSeed(string location, string purpose)
        {
            SaveState state = SaveState.Instance;
            string value = (state?.roomSeed ?? "") + "|" + state?.team + "|" + state?.slot + "|" +
                LocationSet.GetCanonicalLocationName(location) + "|" + purpose;
            using (var hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
                return (bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24) & int.MaxValue;
            }
        }

        internal static string MakeTypo(string name, int seed)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var random = new System.Random(seed);
            char[] letters = name.ToCharArray();
            var swaps = new List<Tuple<int, int>>();
            var vowels = new List<int>();
            for (int i = 0; i < letters.Length; i++)
            {
                if (IsVowel(letters[i])) vowels.Add(i);
                if (!IsConsonant(letters[i]) || i == 0 || !IsLetter(letters[i - 1])) continue;
                for (int j = i + 1; j < letters.Length && j <= i + 2 && IsLetter(letters[j]); j++)
                    if (IsConsonant(letters[j]) && char.ToLowerInvariant(letters[i]) != char.ToLowerInvariant(letters[j]))
                        swaps.Add(Tuple.Create(i, j));
            }
            if (swaps.Count > 0 && (vowels.Count == 0 || random.Next(5) != 0))
            {
                var pair = swaps[random.Next(swaps.Count)];
                char first = letters[pair.Item1], second = letters[pair.Item2];
                letters[pair.Item1] = MatchCase(second, first);
                letters[pair.Item2] = MatchCase(first, second);
            }
            else if (vowels.Count > 0)
            {
                int index = vowels[random.Next(vowels.Count)];
                string replacements = "aeiou".Replace(char.ToLowerInvariant(letters[index]).ToString(), "");
                letters[index] = MatchCase(replacements[random.Next(replacements.Length)], letters[index]);
            }
            return new string(letters);
        }

        private static bool IsLetter(char letter) => char.ToLowerInvariant(letter) >= 'a' && char.ToLowerInvariant(letter) <= 'z';
        private static bool IsVowel(char letter) => "aeiou".IndexOf(char.ToLowerInvariant(letter)) >= 0;
        private static bool IsConsonant(char letter) => IsLetter(letter) && !IsVowel(letter);
        private static char MatchCase(char value, char original) => char.IsUpper(original) ? char.ToUpperInvariant(value) : char.ToLowerInvariant(value);

        private static Sprite FlipIcon(Sprite source)
        {
            if (FlippedIcons.TryGetValue(source, out Sprite flipped) && flipped != null) return flipped;
            if (FailedIcons.Contains(source)) return null;
            int width = Mathf.RoundToInt(source.rect.width), height = Mathf.RoundToInt(source.rect.height);
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            Texture2D copy = null;
            Texture2D readable = null;
            try
            {
                Texture2D atlas = source.texture;
                target = RenderTexture.GetTemporary(atlas.width, atlas.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(atlas, target);
                RenderTexture.active = target;
                readable = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false, false);
                readable.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0, false);
                Color32[] pixels = MirroredIconPixels.Create(readable.GetPixels32(), atlas.width, atlas.height,
                    source.vertices, source.uv, source.triangles, width, height, source.pixelsPerUnit, source.pivot);
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                copy.filterMode = source.texture.filterMode;
                copy.wrapMode = TextureWrapMode.Clamp;
                copy.SetPixels32(pixels);
                copy.Apply(false, true);
                flipped = Sprite.Create(copy, new Rect(0, 0, width, height),
                    new Vector2(1f - source.pivot.x / width, source.pivot.y / height), source.pixelsPerUnit);
                flipped.name = source.name + "_mirrored";
                UnityEngine.Object.DontDestroyOnLoad(copy);
                UnityEngine.Object.DontDestroyOnLoad(flipped);
                FlippedIcons[source] = flipped;
                return flipped;
            }
            catch (Exception ex)
            {
                if (copy != null) UnityEngine.Object.Destroy(copy);
                FailedIcons.Add(source);
                RandomizerPlugin.Log?.LogWarning("Could not mirror item icon '" + source.name + "': " + ex.Message);
                return null;
            }
            finally
            {
                GL.sRGBWrite = previousSrgb;
                RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (readable != null) UnityEngine.Object.Destroy(readable);
            }
        }
    }
}
