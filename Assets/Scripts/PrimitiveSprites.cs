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
        private static Sprite _star;
        private static Sprite _stoneOrb;
        private static Sprite _critterOrb;
        private static Material _unlit;
        private static readonly Dictionary<Color, Texture2D> _rounded = new Dictionary<Color, Texture2D>();
        private static readonly Dictionary<Color, Texture2D> _shadedRects = new Dictionary<Color, Texture2D>();
        private static readonly Dictionary<(Color, Color), Texture2D> _outlinedRects =
            new Dictionary<(Color, Color), Texture2D>();

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

        /// <summary>
        /// STONE, fully baked (2026-07-19): a matte, faceted, mottled grey rock. Everything
        /// is in this ONE root sprite — colours included, so callers tint it white — because
        /// the previous stone was the imported `rock_a` and imported pack sprites do not
        /// render reliably on in-level world SpriteRenderers (the open 2026-07-16 bug; it
        /// also hid the ice, and left critters as featureless white balls until a user asked
        /// "what is this white ball?"). Deliberately has NO specular: matte reads unmatchable
        /// next to the glossy playable orbs, which is the whole job of the visual.
        /// </summary>
        public static Sprite StoneOrb()
        {
            if (_stoneOrb != null) return _stoneOrb;

            const int size = 128;
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;
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
                    float a = Mathf.Clamp01((1f - d) * radius / 1.5f);
                    if (a <= 0f) { pixels[y * size + x] = new Color(1f, 1f, 1f, 0f); continue; }

                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d * d));
                    float lambert = Mathf.Clamp01(dx * lx + dy * ly + nz * lz);
                    // Quantised into three bands: flat facets, not a smooth ball.
                    float faceted = Mathf.Floor(lambert * 3f) / 3f;
                    float lum = 0.52f + 0.34f * faceted;
                    // Deterministic speckle so the surface reads as grainy rock.
                    lum += (Hash01(x * 7 + y * 131) - 0.5f) * 0.11f;
                    lum *= 1f - 0.30f * Mathf.SmoothStep(0.70f, 1f, d); // heavy rim shadow
                    lum = Mathf.Clamp01(lum);
                    // Cool blue-grey; slightly desaturated toward the shadowed side.
                    pixels[y * size + x] = new Color(lum * 0.86f, lum * 0.88f, lum * 0.96f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _stoneOrb = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _stoneOrb.name = "StoneOrb";
            return _stoneOrb;
        }

        /// <summary>
        /// CRITTER, fully baked (2026-07-19): a coral fish silhouette sealed inside a pale
        /// silvery bubble, shell shading + specular + fish all in ONE root sprite. Same
        /// reason as <see cref="StoneOrb"/> — the fish used to be an imported `fish_pink`
        /// CHILD renderer, which is exactly the combination that does not draw in-level, so
        /// the bubble read as a blank white ball with no hint of what it was.
        /// </summary>
        public static Sprite CritterOrb()
        {
            if (_critterOrb != null) return _critterOrb;

            const int size = 128;
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;
            const float lx = -0.42f, ly = 0.52f, lz = 0.744f;
            var shell = new Color(0.93f, 0.96f, 1f);
            var fish = new Color(1f, 0.47f, 0.62f);
            var fishDark = new Color(0.80f, 0.28f, 0.44f);

            var tex = NewTex(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * radius / 1.5f);
                    if (a <= 0f) { pixels[y * size + x] = new Color(1f, 1f, 1f, 0f); continue; }

                    Color c = shell;

                    // ---- The fish, drawn in the orb's normalised space ----
                    // Body ellipse, nosing left; tail wedge trailing right.
                    float bx = (dx + 0.08f) / 0.40f, by = dy / 0.25f;
                    bool body = bx * bx + by * by <= 1f;
                    bool tail = dx > 0.26f && dx < 0.60f &&
                                Mathf.Abs(dy) <= 0.04f + (dx - 0.26f) * 0.78f;
                    // Outline band just outside the body keeps it legible on the pale shell.
                    float bo = (dx + 0.08f) / 0.46f, byo = dy / 0.30f;
                    bool bodyOutline = bo * bo + byo * byo <= 1f;

                    if (body || tail) c = fish;
                    else if (bodyOutline) c = fishDark;
                    float eyeX = dx + 0.23f, eyeY = dy - 0.08f;
                    if (body && eyeX * eyeX + eyeY * eyeY <= 0.055f * 0.055f) c = fishDark;

                    // ---- Shell shading over everything, so the fish sits INSIDE ----
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d * d));
                    float lambert = Mathf.Clamp01(dx * lx + dy * ly + nz * lz);
                    float lum = 0.66f + 0.34f * lambert;
                    lum *= 1f - 0.22f * Mathf.SmoothStep(0.78f, 1f, d);

                    // Baked specular (no separate gloss child — same rendering risk).
                    float ex = (dx + 0.38f) / 0.30f, ey = (dy - 0.42f) / 0.22f;
                    float spec = 0.85f * Mathf.Pow(Mathf.Clamp01(1f - (ex * ex + ey * ey)), 2f);

                    pixels[y * size + x] = new Color(
                        Mathf.Clamp01(c.r * lum + spec),
                        Mathf.Clamp01(c.g * lum + spec),
                        Mathf.Clamp01(c.b * lum + spec), a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _critterOrb = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _critterOrb.name = "CritterOrb";
            return _critterOrb;
        }

        /// <summary>Cheap deterministic 0..1 hash — texture speckle only, never gameplay.</summary>
        private static float Hash01(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }

        /// <summary>
        /// A classic 5-point star (points up), anti-aliased with a soft darkened edge so it
        /// reads as a chunky sticker. White — tint it gold via GUI.color / SpriteRenderer.
        /// Replaces the orb-pip workaround (LegacyRuntime has no ★ glyph); still zero
        /// imported art. Signed distance per Inigo Quilez's sdStar5.
        /// </summary>
        public static Sprite Star()
        {
            if (_star != null) return _star;

            const int size = 128;
            float half = (size - 1) * 0.5f;
            const float r = 0.90f;   // outer radius in normalized coords
            const float rf = 0.55f;  // inner/outer ratio — plumper than the classic 0.5
            var k1 = new Vector2(0.809016994f, -0.587785252f);
            var k2 = new Vector2(-k1.x, k1.y);
            var ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0f, 1f);
            float baLen2 = Vector2.Dot(ba, ba);

            var tex = NewTex(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(Mathf.Abs((x - half) / half), (y - half) / half);
                    p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
                    p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
                    p.x = Mathf.Abs(p.x);
                    p.y -= r;
                    float t = Mathf.Clamp(Vector2.Dot(p, ba) / baLen2, 0f, r);
                    var q = p - ba * t;
                    float d = q.magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
                    float a = Mathf.Clamp01(-d * half / 1.5f); // 1.5px AA edge
                    // Slightly darker toward the silhouette so the tinted star has depth.
                    float lum = 1f - 0.18f * (1f - Mathf.Clamp01(-d / 0.30f));
                    pixels[y * size + x] = new Color(lum, lum, lum, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _star = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _star.name = "Star";
            return _star;
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

        /// <summary>
        /// A candy-style shaded rounded rect for chunky buttons: lighter toward the top,
        /// a soft highlight along the top edge and a darker "lip" along the bottom (both
        /// live inside the 9-slice borders so they survive stretching). Same 9-slice
        /// contract as <see cref="RoundedRect"/> (border 20). Cached per color.
        /// </summary>
        public static Texture2D RoundedRectShaded(Color fill)
        {
            Texture2D cached;
            if (_shadedRects.TryGetValue(fill, out cached) && cached != null) return cached;

            const int size = 64;
            const float margin = 2f, radius = 16f;
            float half = (size - 1) * 0.5f;
            float inner = half - margin - radius;

            var tex = NewTex(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Max(Mathf.Abs(x - half) - inner, 0f);
                    float qy = Mathf.Max(Mathf.Abs(y - half) - inner, 0f);
                    float dist = Mathf.Sqrt(qx * qx + qy * qy);
                    float a = Mathf.Clamp01((radius - dist) / 1.5f);

                    float shade = Mathf.Lerp(0.90f, 1.10f, y / (float)(size - 1)); // top-lit
                    shade *= 1f - 0.22f * Mathf.Clamp01((13f - y) / 9f);            // bottom lip
                    shade *= 1f + 0.10f * Mathf.Clamp01((y - (size - 11f)) / 7f);   // top shine
                    pixels[y * size + x] = new Color(Mathf.Clamp01(fill.r * shade),
                                                     Mathf.Clamp01(fill.g * shade),
                                                     Mathf.Clamp01(fill.b * shade),
                                                     fill.a * a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _shadedRects[fill] = tex;
            return tex;
        }

        /// <summary>
        /// A rounded rect with a crisp ~3px outline ring — for panels and chips that need
        /// to pop off busy backdrops. Same 9-slice contract as <see cref="RoundedRect"/>.
        /// Cached per (fill, outline) pair.
        /// </summary>
        public static Texture2D RoundedRectOutlined(Color fill, Color outline)
        {
            Texture2D cached;
            if (_outlinedRects.TryGetValue((fill, outline), out cached) && cached != null) return cached;

            const int size = 64;
            const float margin = 2f, radius = 16f, outlineW = 3f;
            float half = (size - 1) * 0.5f;
            float inner = half - margin - radius;

            var tex = NewTex(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Max(Mathf.Abs(x - half) - inner, 0f);
                    float qy = Mathf.Max(Mathf.Abs(y - half) - inner, 0f);
                    float dist = Mathf.Sqrt(qx * qx + qy * qy);
                    float edgeA = Mathf.Clamp01((radius - dist) / 1.5f);
                    float innerT = Mathf.Clamp01((radius - dist - outlineW) / 1.5f);
                    Color c = Color.Lerp(outline, fill, innerT);
                    pixels[y * size + x] = new Color(c.r, c.g, c.b, c.a * edgeA);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _outlinedRects[(fill, outline)] = tex;
            return tex;
        }
    }
}
