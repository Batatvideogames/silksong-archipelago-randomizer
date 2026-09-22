using System;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class MirroredIconPixels
    {
        internal static Color32[] Create(Color32[] atlas, int atlasWidth, int atlasHeight,
            Vector2[] vertices, Vector2[] uv, ushort[] triangles,
            int width, int height, float pixelsPerUnit, Vector2 pivot)
        {
            if (atlasWidth <= 0 || atlasHeight <= 0 || atlas.Length != checked(atlasWidth * atlasHeight) ||
                width <= 0 || height <= 0 || pixelsPerUnit <= 0 || vertices.Length != uv.Length ||
                triangles.Length == 0 || triangles.Length % 3 != 0)
                throw new ArgumentException("Invalid icon geometry.");
            var result = new Color32[checked(width * height)];
            var positions = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                positions[i] = new Vector2(width - (vertices[i].x * pixelsPerUnit + pivot.x),
                    vertices[i].y * pixelsPerUnit + pivot.y);

            bool hasVisiblePixel = false;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int ai = triangles[i], bi = triangles[i + 1], ci = triangles[i + 2];
                Vector2 a = positions[ai], b = positions[bi], c = positions[ci];
                double determinant = (b.y - c.y) * (double)(a.x - c.x) + (c.x - b.x) * (double)(a.y - c.y);
                if (Math.Abs(determinant) < 1e-9) continue;
                int minX = Math.Max(0, (int)Math.Floor(Math.Min(a.x, Math.Min(b.x, c.x))));
                int maxX = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(a.x, Math.Max(b.x, c.x))));
                int minY = Math.Max(0, (int)Math.Floor(Math.Min(a.y, Math.Min(b.y, c.y))));
                int maxY = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(a.y, Math.Max(b.y, c.y))));
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    double wa = ((b.y - c.y) * (x + 0.5 - c.x) + (c.x - b.x) * (y + 0.5 - c.y)) / determinant;
                    double wb = ((c.y - a.y) * (x + 0.5 - c.x) + (a.x - c.x) * (y + 0.5 - c.y)) / determinant;
                    double wc = 1 - wa - wb;
                    if (wa < -1e-6 || wb < -1e-6 || wc < -1e-6) continue;
                    double u = wa * uv[ai].x + wb * uv[bi].x + wc * uv[ci].x;
                    double v = wa * uv[ai].y + wb * uv[bi].y + wc * uv[ci].y;
                    int tx = Math.Min(atlasWidth - 1, Math.Max(0, (int)Math.Floor(u * atlasWidth)));
                    int ty = Math.Min(atlasHeight - 1, Math.Max(0, (int)Math.Floor(v * atlasHeight)));
                    Color32 color = atlas[ty * atlasWidth + tx];
                    result[y * width + x] = color;
                    hasVisiblePixel |= color.a != 0;
                }
            }
            if (!hasVisiblePixel) throw new InvalidOperationException("Mirrored icon has no visible pixels.");
            return result;
        }
    }
}
