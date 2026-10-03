using System;
using System.Linq;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class NpcPortraitPixels
    {
        internal static Color32[] Create(Color32[] atlas, int atlasWidth, int atlasHeight,
            Vector2[] vertices, Vector2[] uv, int[] triangles, bool premultiplied)
        {
            if (atlasWidth <= 0 || atlasHeight <= 0 || atlas.Length != checked(atlasWidth * atlasHeight) ||
                vertices.Length < 3 || vertices.Length != uv.Length || triangles.Length == 0 || triangles.Length % 3 != 0 ||
                triangles.Any(i => i < 0 || i >= vertices.Length))
                throw new ArgumentException("Invalid portrait geometry.");
            float left = vertices.Min(v => v.x), right = vertices.Max(v => v.x);
            float top = vertices.Max(v => v.y), bottom = top - (top - vertices.Min(v => v.y)) * .8f;
            if (right <= left || top <= bottom) throw new ArgumentException("Empty portrait geometry.");
            float scale = 240f / Math.Max(right - left, top - bottom);
            float offsetX = (256f - (right - left) * scale) * .5f;
            float offsetY = (256f - (top - bottom) * scale) * .5f;
            var positions = vertices.Select(v => new Vector2((v.x - left) * scale + offsetX, (v.y - bottom) * scale + offsetY)).ToArray();
            var result = new Color32[256 * 256];
            bool visible = false;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int ai = triangles[i], bi = triangles[i + 1], ci = triangles[i + 2];
                Vector2 a = positions[ai], b = positions[bi], c = positions[ci];
                double determinant = (b.y - c.y) * (double)(a.x - c.x) + (c.x - b.x) * (double)(a.y - c.y);
                if (Math.Abs(determinant) < 1e-9) continue;
                int minX = Math.Max((int)Math.Ceiling(offsetX - .5), (int)Math.Floor(Math.Min(a.x, Math.Min(b.x, c.x))));
                int maxX = Math.Min((int)Math.Floor(255.5 - offsetX), (int)Math.Ceiling(Math.Max(a.x, Math.Max(b.x, c.x))));
                int minY = Math.Max((int)Math.Ceiling(offsetY - .5), (int)Math.Floor(Math.Min(a.y, Math.Min(b.y, c.y))));
                int maxY = Math.Min((int)Math.Floor(255.5 - offsetY), (int)Math.Ceiling(Math.Max(a.y, Math.Max(b.y, c.y))));
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    double wa = ((b.y - c.y) * (x + .5 - c.x) + (c.x - b.x) * (y + .5 - c.y)) / determinant;
                    double wb = ((c.y - a.y) * (x + .5 - c.x) + (a.x - c.x) * (y + .5 - c.y)) / determinant;
                    double wc = 1 - wa - wb;
                    if (wa < -1e-6 || wb < -1e-6 || wc < -1e-6) continue;
                    double u = wa * uv[ai].x + wb * uv[bi].x + wc * uv[ci].x;
                    double v = wa * uv[ai].y + wb * uv[bi].y + wc * uv[ci].y;
                    int tx = Math.Min(atlasWidth - 1, Math.Max(0, (int)Math.Floor(u * atlasWidth)));
                    int ty = Math.Min(atlasHeight - 1, Math.Max(0, (int)Math.Floor(v * atlasHeight)));
                    Color32 color = atlas[ty * atlasWidth + tx];
                    if (premultiplied && color.a != 0)
                    {
                        color.r = (byte)Math.Min(255, (color.r * 255 + color.a / 2) / color.a);
                        color.g = (byte)Math.Min(255, (color.g * 255 + color.a / 2) / color.a);
                        color.b = (byte)Math.Min(255, (color.b * 255 + color.a / 2) / color.a);
                    }
                    result[y * 256 + x] = color;
                    visible |= color.a != 0;
                }
            }
            if (!visible) throw new InvalidOperationException("Native portrait has no visible pixels.");
            return result;
        }
    }
}
