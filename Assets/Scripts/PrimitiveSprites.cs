using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Generates simple primitive sprites/textures at runtime so the game needs zero
    /// imported art to run: the 1-world-unit circle for bubbles, vertical gradients for
    /// backdrops, and 9-sliceable rounded rectangles for the IMGUI skin.
    /// </summary>
    public static class PrimitiveSprites
    {
        private static Sprite _circle;
        private static Sprite _orb;
        private static Sprite _gloss;
        private static Material _unlit;
        private static readonly Dictionary<Color, Texture2D> _rounded = new Dictionary<Color, Texture2D>();

        /// <summary>
        /// Shared unlit sprite material. In a URP 2D project the default sprite material can be
        /// lit — sprites render black with no Light2D present. Forcing "Sprites/Default" (an
        /// always-included unlit shader) keeps the primitive bubbles visible regardless.
        /// </summary>
        public static Material UnlitMaterial()
        {
            if (_unlit != null) return _unlit;
            var shader = Shader.Find("Sprites/Default");
            _unlit = new Material(shader != null ? shader : Shader.Find("Unlit/Transparent"))
            {
                name = "PrimitiveUnlit"
            };
            return _unlit;
        }

        /// <summary>A soft-edged white circle sprite, 1 world unit in diameter at scale 1.</summary>
        public static Sprite Circle()
        {
            if (_circle != null) return _circle;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // 1.5px anti-aliased edge.
                    float a = Mathf.Clamp01((radius - dist) / 1.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            // pixelsPerUnit = size => sprite spans exactly 1 world unit at scale 1.
            _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _circle.name = "PrimitiveCircle";
            return _circle;
        }

        /// <summary>
        /// The ball: a sphere-shaded WHITE orb (top-left light, darkened rim) whose shading
        /// is baked as luminance, so a SpriteRenderer tint produces a shaded ball in any
        /// color. The white specular shine lives in <see cref="OrbGloss"/> as a separate
        /// UNTINTED layer — multiplying can never make a highlight whiter than the tint.
        /// 1 world unit at scale 1, same contract as Circle().
        /// </summary>
        public static Sprite GlossyOrb()
        {
            if (_orb != null) return _orb;

            const int size = 128;
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;
            // Light from the upper-left (normalized).
            const float lx = -0.42f, ly = 0.52f, lz = 0.744f;

            var tex = NewTex(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * radius / 1.5f); // AA edge
                    if (a <= 0f) { pixels[y * size + x] = new Color(1f, 1f, 1f, 0f); continue; }

                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d * d)); // sphere normal z
                    float lambert = Mathf.Clamp01(dx * lx + dy * ly + nz * lz);
                    float lum = 0.60f + 0.40f * lambert;
                    lum *= 1f - 0.22f * Mathf.SmoothStep(0.78f, 1f, d); // rim shadow
                    pixels[y * size + x] = new Color(lum, lum, lum, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _orb = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _orb.name = "GlossyOrb";
            return _orb;
        }

        /// <summary>
        /// The untinted shine layer for <see cref="GlossyOrb"/>: a soft white highlight
        /// blob upper-left plus a faint bounce-light arc along the bottom rim.
        /// </summary>
        public static Sprite OrbGloss()
        {
            if (_gloss != null) return _gloss;

            const int size = 128;
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;

            var tex = NewTex(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1f) { pixels[y * size + x] = new Color(1f, 1f, 1f, 0f); continue; }

                    // Primary specular: soft ellipse in the upper-left.
                    float ex = (dx + 0.38f) / 0.30f;
                    float ey = (dy - 0.42f) / 0.22f;
                    float fall = Mathf.Clamp01(1f - (ex * ex + ey * ey));
                    float a = 0.9f * fall * fall;

                    // Bounce light: a faint arc hugging the lower rim.
                    float band = Mathf.SmoothStep(0.70f, 0.85f, d) * (1f - Mathf.SmoothStep(0.88f, 0.98f, d));
                    float down = Mathf.Clamp01((-dy - 0.15f) / 0.5f);
                    a = Mathf.Min(1f, a + 0.18f * band * down);

                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _gloss = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _gloss.name = "OrbGloss";
            return _gloss;
        }

        private static Sprite _pixel;

        /// <summary>A plain white 1-unit square — for solid bars/strips (e.g. danger line).</summary>
        public static Sprite Pixel()
        {
            if (_pixel != null) return _pixel;
            var tex = NewTex(4);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            _pixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            _pixel.name = "Pixel";
            return _pixel;
        }

        private static Texture2D NewTex(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

        /// <summary>
        /// A 1px-wide vertical gradient texture (bottom → top). Stretch it over any rect or
        /// sprite for a sky/water backdrop. NOT cached — callers hold their own reference.
        /// </summary>
        public static Texture2D GradientTexture(Color top, Color bottom)
        {
            const int h = 256;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color[h];
            for (int y = 0; y < h; y++)
                px[y] = Color.Lerp(bottom, top, y / (float)(h - 1));
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// A pre-filled rounded-rect texture for GUIStyle backgrounds (cached per color).
        /// Use with GUIStyle.border = RectOffset(20,20,20,20) so corners 9-slice cleanly.
        /// </summary>
        public static Texture2D RoundedRect(Color fill)
        {
            Texture2D cached;
            if (_rounded.TryGetValue(fill, out cached) && cached != null) return cached;

            const int size = 64;
            const float margin = 2f, radius = 16f;
            float half = (size - 1) * 0.5f;
            float inner = half - margin - radius;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Signed-distance to a rounded rect, 1.5px anti-aliased edge.
                    float qx = Mathf.Max(Mathf.Abs(x - half) - inner, 0f);
                    float qy = Mathf.Max(Mathf.Abs(y - half) - inner, 0f);
                    float dist = Mathf.Sqrt(qx * qx + qy * qy);
                    float a = Mathf.Clamp01((radius - dist) / 1.5f);
                    pixels[y * size + x] = new Color(fill.r, fill.g, fill.b, fill.a * a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _rounded[fill] = tex;
            return tex;
        }
    }
}
