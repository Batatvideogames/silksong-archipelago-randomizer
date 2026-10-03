using System;
using System.Linq;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class NpcPortrait
    {
        internal static Sprite Create(string name, Texture atlas, Vector2[] vertices, Vector2[] uv, int[] triangles, bool premultiplied)
        {
            RenderTexture previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            RenderTexture target = null;
            Texture2D readable = null, texture = null;
            try
            {
                target = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(atlas, target);
                RenderTexture.active = target;
                readable = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0, false);
                readable.Apply(false, false);
                var pixels = NpcPortraitPixels.Create(readable.GetPixels32(), atlas.width, atlas.height, vertices, uv, triangles, premultiplied);
                texture = new Texture2D(256, 256, TextureFormat.RGBA32, false)
                {
                    name = "NPC Soul: " + name,
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 256, 256), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = texture.name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                texture = null;
                return sprite;
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgb;
                if (readable != null) UnityEngine.Object.Destroy(readable);
                if (texture != null) UnityEngine.Object.Destroy(texture);
                if (target != null) RenderTexture.ReleaseTemporary(target);
            }
        }
    }

}
