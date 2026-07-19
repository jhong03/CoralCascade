using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Central builder for every bubble visual (attached, falling, projectile, launcher
    /// queue). Uses the Kenney Fish Pack under Resources/Art/Double when present:
    /// a white bubble shell tinted per color, a critter sprite inside, a translucent frost
    /// overlay when frozen, and a rock for Stone. Falls back to the Phase 1 primitive
    /// circle when the art folder is missing, so the game never REQUIRES imported assets.
    ///
    /// Loaded sprites are re-created with pixelsPerUnit = their rect width so every visual
    /// spans exactly 1 world unit at scale 1 — callers scale transforms to BubbleDiameter
    /// and colliders assume local radius 0.5, same contract as PrimitiveSprites.Circle().
    /// </summary>
    public static class BubbleArt
    {
        private const string ArtPath = "Art/Double/";

        // Frozen bubbles wash their base tint toward this icy blue-white; FrostWash is how
        // far (0..1). Applied on the ROOT orb tint (not a translucent overlay child — those
        // world SpriteRenderers didn't render reliably; user-reported 2026-07-18 "the ice
        // isn't visible"). Keep it under ~0.6 so the underlying color stays readable.
        private static readonly Color FrostColor = new Color(0.80f, 0.92f, 1f);
        private const float FrostWash = 0.55f;

        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private static bool _probed;
        private static bool _available;

        /// <summary>
        /// Public access to any pack sprite (normalized to 1 world unit) for decor users
        /// like ReefBackdrop. Returns null when the pack (or that sprite) is missing.
        /// </summary>
        public static Sprite Get(string name) => Load(name);

        /// <summary>True when the imported art pack is present (probed once, cached).</summary>
        public static bool Available
        {
            get
            {
                if (!_probed)
                {
                    _probed = true;
                    _available = Load("bubble_b") != null;
                }
                return _available;
            }
        }

        /// <summary>
        /// Makes (or refreshes) a complete bubble visual on <paramref name="go"/>: shell
        /// sprite + tint on the root SpriteRenderer, plus managed "Critter" and "Frost"
        /// children. Idempotent — safe to call again when color/frozen state changes
        /// (launcher queue swaps, ice thaw). Never touches other children (e.g. hint labels).
        /// </summary>
        public static void Apply(GameObject go, BubbleColor color, bool frozen, int baseOrder)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = PrimitiveSprites.UnlitMaterial();
            sr.sortingOrder = baseOrder;

            // Stone and Critter are FULLY PROCEDURAL and fully baked into the ROOT sprite
            // (2026-07-19). They used to depend on imported pack art — `rock_a` on the root,
            // `fish_pink` on a CHILD renderer — and imported sprites don't draw reliably on
            // in-level world SpriteRenderers (open 2026-07-16 bug). The critter therefore
            // rendered as a featureless white ball, which a player hit on Reef 58 and had to
            // ask about. Same lesson as the ice fix: put the READ where it cannot fail — one
            // root sprite, no children, no imported assets.
            if (color == BubbleColor.Stone)
            {
                // Matte, faceted, unmatchable — no gloss, on purpose.
                sr.sprite = PrimitiveSprites.StoneOrb();
                sr.color = Color.white;
                SetLayerChild(go, "Critter", null, Color.white, 1f, 0);
                SetLayerChild(go, "Gloss", null, Color.white, 1f, 0);
                SetLayerChild(go, "Frost", null, Color.white, 1f, 0);
                return;
            }

            if (color == BubbleColor.Critter)
            {
                // A trapped fish in a pale silvery bubble: no colour to match, so the read
                // has to be "free me by dropping". Shell, shine and fish are all one sprite.
                sr.sprite = PrimitiveSprites.CritterOrb();
                sr.color = Color.white;
                SetLayerChild(go, "Critter", null, Color.white, 1f, 0);
                SetLayerChild(go, "Gloss", null, Color.white, 1f, 0);
                SetLayerChild(go, "Frost", null, Color.white, 1f, 0);
                return;
            }

            // Glossy orb (user-chosen design 2026-07-14): sphere-shaded white orb tinted per
            // color, with the white specular shine as an UNTINTED child layer so it stays
            // white on every color. Both are procedural — balls never need imported art.
            // "Critter" removal strips legacy children on refresh.
            sr.sprite = PrimitiveSprites.GlossyOrb();
            // Frozen bubbles read as pale, shiny ice-coated orbs: wash the tint toward icy
            // white on the root renderer (guaranteed to render), keeping the gloss child so
            // it still looks glassy. The color stays hinted so players know what to match
            // once it thaws. Thaw calls Apply again with frozen=false → full color returns.
            sr.color = frozen ? Color.Lerp(color.ToRGBA(), FrostColor, FrostWash) : color.ToRGBA();
            SetLayerChild(go, "Critter", null, Color.white, 1f, 0);
            SetLayerChild(go, "Gloss", PrimitiveSprites.OrbGloss(), Color.white, 1f, baseOrder + 1);
            SetLayerChild(go, "Frost", null, Color.white, 1f, 0); // legacy overlay child retired
        }

        /// <summary>Creates/updates/removes a named child sprite layer under the bubble root.</summary>
        private static void SetLayerChild(GameObject go, string name, Sprite sprite,
                                          Color color, float scale, int order)
        {
            Transform child = go.transform.Find(name);
            if (sprite == null)
            {
                if (child != null)
                {
                    // Same-frame safety convention as everywhere else in the project.
                    child.gameObject.SetActive(false);
                    Object.Destroy(child.gameObject);
                }
                return;
            }

            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(go.transform, false);
                child.gameObject.AddComponent<SpriteRenderer>();
            }
            child.localScale = new Vector3(scale, scale, 1f); // parent is diameter-scaled

            var sr = child.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = PrimitiveSprites.UnlitMaterial();
            sr.color = color;
            sr.sortingOrder = order;
        }

        /// <summary>
        /// Loads a pack sprite normalized to FIT a 1×1 world-unit box at scale 1 (the
        /// import PPU is irrelevant). LoadAll handles both Single and Multiple sprite
        /// modes; some pack files auto-sliced into several sub-sprites (e.g. seaweed
        /// canvases hold 2-3 separate strands), so we take the LARGEST slice — grabbing
        /// slice [0] blindly rendered arbitrary fragments.
        /// </summary>
        private static Sprite Load(string name)
        {
            Sprite cached;
            if (_cache.TryGetValue(name, out cached)) return cached;

            Sprite normalized = null;
            var loaded = Resources.LoadAll<Sprite>(ArtPath + name);
            if (loaded != null && loaded.Length > 0)
            {
                Sprite src = loaded[0];
                for (int i = 1; i < loaded.Length; i++)
                    if (loaded[i].rect.width * loaded[i].rect.height > src.rect.width * src.rect.height)
                        src = loaded[i];
                // Normalize by the LONG side: a 30×98 kelp strand becomes 1 unit tall,
                // not 1 unit wide and 3.3 tall (that bug rendered as giant swaying pillars).
                float ppu = Mathf.Max(src.rect.width, src.rect.height);
                normalized = Sprite.Create(src.texture, src.rect, new Vector2(0.5f, 0.5f), ppu);
                normalized.name = name;
            }
            _cache[name] = normalized; // negative results cached too — no per-frame re-probing
            return normalized;
        }
    }
}
