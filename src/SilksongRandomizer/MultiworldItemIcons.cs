using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class MultiworldItemIcons
    {
        private static readonly Dictionary<string, string> Entries = new Dictionary<string, string>();
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Failed = new HashSet<string>();
        private static ZipArchive archive;
        private static bool initialized;

        internal static Sprite GetSprite(string game, string item)
        {
            if (string.IsNullOrWhiteSpace(game) || string.IsNullOrWhiteSpace(item)) return null;
            Initialize();
            string key = Clean(game) + "\0";
            if (!Entries.TryGetValue(key + Clean(item), out string path) && !Entries.TryGetValue(key, out path))
                return null;
            if (Sprites.TryGetValue(path, out Sprite sprite) && sprite != null) return sprite;
            if (Failed.Contains(path)) return null;

            Texture2D texture = null;
            try
            {
                using (Stream input = archive.GetEntry(path).Open())
                using (var bytes = new MemoryStream())
                {
                    input.CopyTo(bytes);
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        name = path,
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    if (!ImageConversion.LoadImage(texture, bytes.ToArray(), true))
                        throw new InvalidDataException("Invalid icon PNG");
                }
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = path;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                Sprites[path] = sprite;
                return sprite;
            }
            catch (Exception ex)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                Failed.Add(path);
                RandomizerPlugin.Log?.LogWarning("Could not load multiworld icon " + path + ": " + ex.Message);
                return null;
            }
        }

        private static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                Stream stream = typeof(MultiworldItemIcons).Assembly.GetManifestResourceStream("SilksongRandomizer.MultiworldIcons.zip");
                archive = new ZipArchive(stream, ZipArchiveMode.Read);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string path = entry.FullName;
                    int slash = path.IndexOf('/');
                    if (slash < 0 || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                    string game = path.Substring(0, slash);
                    string name = Path.GetFileNameWithoutExtension(path);
                    string item = Clean(name) == Clean(game) ? "" : name.StartsWith(game + "_", StringComparison.Ordinal)
                        ? name.Substring(game.Length + 1) : null;
                    if (item != null) Entries[Clean(game) + "\0" + Clean(item)] = path;
                }
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith("/aliases.json", StringComparison.Ordinal)) continue;
                    string game = Clean(entry.FullName.Substring(0, entry.FullName.IndexOf('/'))) + "\0";
                    using (var reader = new StreamReader(entry.Open()))
                    {
                        foreach (JToken alias in JObject.Parse(reader.ReadToEnd())["Aliases"])
                        {
                            if (!Entries.TryGetValue(game + Clean((string)alias["AliasName"]), out string path)) continue;
                            foreach (JToken item in alias["ItemNames"])
                            {
                                string key = game + Clean((string)item);
                                if (!Entries.ContainsKey(key)) Entries[key] = path;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Entries.Clear();
                archive?.Dispose();
                archive = null;
                RandomizerPlugin.Log?.LogWarning("Could not read bundled multiworld icons: " + ex.Message);
            }
        }

        private static string Clean(string name)
        {
            var result = new StringBuilder();
            foreach (char c in name ?? "")
                if (c != ' ' && c != '_' && c != '\'' && c != ':' && c != '<' && c != '>')
                    result.Append(char.ToLowerInvariant(c));
            return result.ToString();
        }
    }
}
