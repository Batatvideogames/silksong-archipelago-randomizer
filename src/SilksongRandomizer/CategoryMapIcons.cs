using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class CategoryMapIcons
    {
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<Sprite, Sprite> Outlines = new Dictionary<Sprite, Sprite>();
        private static readonly Dictionary<string, string> Resources = ResourceCategories();
        private static readonly HashSet<string> Shops = new HashSet<string>(
            CoreLocationManifest.ShopLocations.Select(entry => Canonical(entry.LocationName)), StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> WishRewards = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Savage Beastfly - Mask Shard", "The Hidden Hunter - Mask Shard", "Volatile Flintbeetles - Memory Locket"
        };
        private static LocationSet source;
        private static Dictionary<string, Location> locations;

        private static string Canonical(string name) => LocationSet.GetCanonicalLocationName(name);

        private static Dictionary<string, string> ResourceCategories()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in MinorCacheManifest.Entries)
                result[Canonical(entry.LocationName)] = entry.Kind == MinorCacheManifest.SourceKind.BreakableHolder ||
                    entry.LocationName == "Deep Docks Church - Shell Shard Chest" ? "Shell Shards" : "Rosaries";
            foreach (var entry in MinorPickupManifest.Entries)
            {
                if (entry.AssetName.StartsWith("Rosary", StringComparison.Ordinal))
                    result[Canonical(entry.LocationName)] = "Rosaries";
                else if (entry.AssetName == "Shard Pouch" || entry.AssetName == "Great Shard" || entry.AssetName == "Pristine Core")
                    result[Canonical(entry.LocationName)] = "Shell Shards";
            }
            foreach (string name in BeastShardSourceManifest.LocationNames) result[Canonical(name)] = "Shell Shards";
            result[Canonical(MinorPickupManifest.LoddieNecklace)] = "Rosaries";
            return result;
        }

        internal static string Category(SaveState state, IReadOnlyList<string> names)
        {
            if (state?.locations == null || names.Count == 0) return null;
            if (!ReferenceEquals(source, state.locations))
            {
                source = state.locations;
                locations = source.Locations.GroupBy(location => location.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            }
            var entries = names.Select(name => locations.TryGetValue(Canonical(name), out var location) ? location : null).ToArray();
            if (entries.Any(location => location == null)) return null;
            string category = SingleCategory(entries[0]);
            if (category != null && entries.All(location => SingleCategory(location) == category)) return category;
            if (entries.Length == 1) return null;
            if (entries.All(location => Shops.Contains(location.Name) ||
                location.Name.IndexOf(" Shop - ", StringComparison.OrdinalIgnoreCase) >= 0)) return "Merchants";
            if (entries.All(location => location.Type == ItemType.Quest || WishRewards.Contains(location.Name))) return "Wish";
            if (entries.Any(location => location.Type == ItemType.Boss)) return "First Time Kill";
            return null;
        }

        private static string SingleCategory(Location location)
        {
            string name = location.SourceName;
            switch (location.Type)
            {
                case ItemType.Crest:
                case ItemType.CrestSlot:
                case ItemType.Eva: return "Crests";
                case ItemType.Flea: return "Fleas";
                case ItemType.MaskShard: return "Mask Shards";
                case ItemType.SpoolFragment: return "Spool Fragments";
                case ItemType.SilkHeart: return "Silk Hearts";
                case ItemType.MemoryLocket: return "Memory Lockets";
                case ItemType.OldHeart: return "Hunter Heart";
                case ItemType.PollipHeart: return "Pollip Hearts";
                case ItemType.Everbloom: return "Everbloom";
                case ItemType.ToolPouch: return "Tool Pouch";
                case ItemType.Bellway: return "Bellways";
                case ItemType.Ventrica: return "Ventrica";
                case ItemType.BellShrine: return "Bells";
                case ItemType.Map:
                case ItemType.Quill:
                case ItemType.Compass: return "Maps";
                case ItemType.Quest: return "Wish";
                case ItemType.Boss: return "First Time Kill";
                case ItemType.Journal: return name == JournalRandomization.JournalLocation ? null : "First Time Kill";
                case ItemType.Bellhome: return Shops.Contains(location.Name) ? "Merchants" : null;
                case ItemType.Melody:
                    if (name == "Architect's Melody") return "Architect Melody";
                    if (name == "Conductor's Melody") return "Conductor Melody";
                    if (name == "Vaultkeeper's Melody") return "Vaultkeeper Melody";
                    return null;
                case ItemType.Pin:
                    if (name.StartsWith("Pin Purchase: Bench", StringComparison.Ordinal)) return "Benches";
                    if (name.StartsWith("Pin Purchase: Ventrica", StringComparison.Ordinal)) return "Ventrica";
                    if (name.StartsWith("Pin Purchase: Bellway", StringComparison.Ordinal)) return "Bellways";
                    if (name.StartsWith("Pin Purchase: Vendor", StringComparison.Ordinal)) return "Merchants";
                    return null;
                case ItemType.Tool:
                    if (!name.StartsWith("Tool Unlock: ", StringComparison.Ordinal)) return null;
                    string asset = name.Substring("Tool Unlock: ".Length);
                    ToolItem tool = ToolItemManager.GetAllTools().FirstOrDefault(item => item != null && item.name == asset);
                    if (tool == null) return null;
                    switch (tool.Type)
                    {
                        case ToolItemType.Red: return "Red Tools";
                        case ToolItemType.Blue: return "Blue Tools";
                        case ToolItemType.Yellow: return "Yellow Tools";
                        default: return null;
                    }
                case ItemType.Resource:
                case ItemType.Currency:
                    return Resources.TryGetValue(location.Name, out string resource) ? resource : null;
                default: return null;
            }
        }

        internal static Sprite GetSprite(SaveState state, IReadOnlyList<string> names)
        {
            string category = Category(state, names);
            if (category == null) return null;
            if (Sprites.TryGetValue(category, out var cached)) return cached;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                using (Stream stream = typeof(CategoryMapIcons).Assembly.GetManifestResourceStream(
                    "SilksongRandomizer.MapPins." + category + ".png"))
                {
                    if (stream == null) return null;
                    using (var bytes = new MemoryStream())
                    {
                        stream.CopyTo(bytes);
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) {
                            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                            hideFlags = HideFlags.HideAndDontSave
                        };
                        if (!texture.LoadImage(bytes.ToArray())) throw new InvalidDataException("Invalid pin PNG");
                    }
                }
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                sprite.name = "Map Pin: " + category;
                sprite.hideFlags = HideFlags.HideAndDontSave;
            }
            catch (Exception ex)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                RandomizerPlugin.Log?.LogWarning("Could not load map pin " + category + ": " + ex.Message);
            }
            finally { Sprites[category] = sprite; }
            return sprite;
        }

        internal static Sprite GetOutline(Sprite sprite)
        {
            if (!Outlines.TryGetValue(sprite, out var outline))
            {
                outline = RandomizerPlugin.CreateWhiteOutlineIcon(sprite);
                Outlines[sprite] = outline;
            }
            return outline;
        }
    }
}
