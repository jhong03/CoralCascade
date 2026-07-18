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

        // Frost matches the old "washed toward icy white" lerp (62%), as a translucent layer.
        private static readonly Color FrostTint = new Color(0.85f, 0.95f, 1f, 0.62f);

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

            if (color == BubbleColor.Stone && Available)
            {
                // Stones ARE rocks — no bubble shell (and no gloss: matte = unmatchable).
                sr.sprite = Load("rock_a");
                sr.color = Color.white;
                SetLayerChild(go, "Critter", null, Color.white, 1f, 0);
                SetLayerChild(go, "Gloss", null, Color.white, 1f, 0);
                SetLayerChild(go, "Frost", null, Color.white, 1f, 0);
                return;
            }

            if (color == BubbleColor.Critter)
            {
                // A trapped critter: fish inside a pale silvery bubble. The shell stays a
                // NON-playable tint (no color to match — the read is "free me by dropping").
                // Primitive fallback: the pale pink orb alone still reads as "not a color".
                sr.sprite = PrimitiveSprites.GlossyOrb();
                sr.color = new Color(0.94f, 0.97f, 1f);
                SetLayerChild(go, "Critter", Available ? Load("fish_pink") : null,
                              Color.white, 0.62f, baseOrder + 1);
                SetLayerChild(go, "Gloss", PrimitiveSprites.OrbGloss(), Color.white, 1f, baseOrder + 2);
                SetLayerChild(go, "Frost", null, Color.white, 1f, 0);
                return;
            }

            // Glossy orb (user-chosen design 2026-07-14): sphere-shaded white orb tinted per
            // color, with the white specular shine as an UNTINTED child layer so it stays
            // white on every color. Both are procedural — balls never need imported art.
            // "Critter" removal strips legacy children on refresh.
            sr.sprite = PrimitiveSprites.GlossyOrb();
            sr.color = color.ToRGBA();
            SetLayerChild(go, "Critter", null, Color.white, 1f, 0);
            SetLayerChild(go, "Gloss", PrimitiveSprites.OrbGloss(), Color.white, 1f, baseOrder + 1);
            SetLayerChild(go, "Frost", frozen ? PrimitiveSprites.Circle() : null,
                          FrostTint, 1f, baseOrder + 2);
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
