using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Pure decoration behind the play field: a sunlit water gradient, seaweed and rocks
    /// along the floor, faint distant silhouettes, slow-rising bubbles, and the occasional
    /// fish cruising across.
    ///
    /// FAIRNESS/PHYSICS SAFETY: nothing here has a collider and every renderer sits on a
    /// NEGATIVE sorting order, so trajectory casts, the physics cascade and the gameplay
    /// visuals can never interact with it. All motion runs on SCALED time, so pause
    /// freezes it and the slow-mo beat slows it along with everything else.
    ///
    /// Rebuilt by GameBootstrap.ConfigureWorld on every width change. Decor placement uses
    /// a System.Random seeded from the column count — reproducible, and never touches the
    /// gameplay randomizer.
    /// </summary>
    public class ReefBackdrop : MonoBehaviour
    {
        private const int RisingBubbleCount = 10;

        // Sunlit lagoon: light aqua near the surface, rich (but bright) turquoise below.
        private static readonly Color WaterTop = new Color(0.55f, 0.87f, 0.95f);
        private static readonly Color WaterBottom = new Color(0.09f, 0.50f, 0.70f);

        private static readonly string[] PlantNames =
        {
            "seaweed_green_a", "seaweed_green_b", "seaweed_green_c", "seaweed_green_d",
            "seaweed_pink_a", "seaweed_pink_b", "seaweed_pink_c",
            "seaweed_orange_a", "seaweed_orange_b",
            "seaweed_grass_a", "seaweed_grass_b",
            "rock_a", "rock_b"
        };
        private static readonly string[] SilhouetteNames =
        {
            "background_seaweed_a", "background_seaweed_b", "background_seaweed_c",
            "background_seaweed_e", "background_seaweed_g",
            "background_rock_a", "background_rock_b"
        };
        // NOTE: fish_grey_long_a/b are the two HALVES of one long fish (each hugs a tile
        // edge) — drawn alone they look cut off, so they're excluded here. The composed
        // eel lives in the aquarium (GameFlow.DrawTankFish handles two-tile sprites).
        private static readonly string[] SwimmerNames =
        {
            "fish_blue", "fish_orange", "fish_pink", "fish_green", "fish_red", "fish_brown"
        };

        private class Riser
        {
            public Transform T;
            public float Speed;
            public float Phase;
        }

        private class Swimmer
        {
            public Transform T;
            public SpriteRenderer Sr;
            public float Speed;   // signed: +right / -left
            public float BaseY;
            public float Phase;
        }

        private class Plant
        {
            public Transform T;
            public float Phase;
        }

        private static Sprite _gradientSprite;

        private readonly List<Riser> _risers = new List<Riser>();
        private readonly List<Swimmer> _swimmers = new List<Swimmer>();
        private readonly List<Plant> _plants = new List<Plant>();
        private float _left, _right, _top, _bottom;

        public void Build(float centerX, float top, float bottom, float halfWidth, int seed)
        {
            // Same teardown convention as the rest of the project.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            _risers.Clear();
            _swimmers.Clear();
            _plants.Clear();

            // Cover generously past the framing width so wide devices never see the edge.
            _left = centerX - halfWidth - 1.5f;
            _right = centerX + halfWidth + 1.5f;
            _top = top;
            _bottom = bottom;
            var rng = new System.Random(seed * 31 + 7);

            // ---- Water gradient (the whole mood) ----
            if (_gradientSprite == null)
            {
                var tex = PrimitiveSprites.GradientTexture(WaterTop, WaterBottom);
                _gradientSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                                new Vector2(0.5f, 0.5f), tex.height);
                _gradientSprite.name = "WaterGradient";
            }
            var water = MakeSprite("Water", _gradientSprite, new Vector3(centerX, (top + bottom) * 0.5f, 0f),
                                   Color.white, -100);
            water.transform.localScale = new Vector3(
                (_right - _left) / _gradientSprite.bounds.size.x,
                (top - bottom) / _gradientSprite.bounds.size.y, 1f);

            bool packArt = BubbleArt.Available;

            // ---- Faint distant silhouettes (low-mid field, subtle by design) ----
            if (packArt)
            {
                int silhouettes = 4;
                for (int i = 0; i < silhouettes; i++)
                {
                    var s = BubbleArt.Get(SilhouetteNames[rng.Next(SilhouetteNames.Length)]);
                    if (s == null) continue;
                    float x = Mathf.Lerp(_left + 1f, _right - 1f, (i + 0.5f) / silhouettes + Range(rng, -0.12f, 0.12f));
                    float scale = Range(rng, 2.2f, 3.4f);
                    var sr = MakeSprite("Silhouette", s, new Vector3(x, bottom + scale * 0.4f, 0f),
                                        new Color(1f, 1f, 1f, 0.35f), -60);
                    sr.transform.localScale = new Vector3(scale, scale, 1f);
                }
            }

            // ---- Sandy seabed strip so the reef has ground to grow from ----
            float bedY = bottom + 0.55f; // approximate sand surface line
            if (packArt)
            {
                var sand = BubbleArt.Get("terrain_sand_top_a");
                if (sand != null)
                {
                    const float tile = 1.25f;
                    int tiles = Mathf.CeilToInt((_right - _left) / tile) + 1;
                    for (int i = 0; i < tiles; i++)
                    {
                        var sr = MakeSprite("Sand", sand,
                                            new Vector3(_left + (i + 0.5f) * tile, bottom + tile * 0.15f, 0f),
                                            Color.white, -45);
                        sr.transform.localScale = new Vector3(tile, tile, 1f);
                    }
                }
            }

            // ---- A coral/seaweed bed on the sand (sways around its BASE, like a current) ----
            if (packArt)
            {
                int plants = Mathf.Clamp(Mathf.RoundToInt(halfWidth * 2.8f), 8, 16);
                for (int i = 0; i < plants; i++)
                {
                    var s = BubbleArt.Get(PlantNames[rng.Next(PlantNames.Length)]);
                    if (s == null) continue;
                    float x = Mathf.Lerp(_left + 0.4f, _right - 0.4f, (i + 0.5f) / plants + Range(rng, -0.08f, 0.08f));
                    float scale = Range(rng, 0.7f, 1.5f);

                    // Holder sits at the base so the sway pivots there, not mid-stalk.
                    var holder = new GameObject("Plant").transform;
                    holder.SetParent(transform, false);
                    holder.position = new Vector3(x, bedY - 0.1f, 0f);
                    holder.localScale = new Vector3(scale, scale, 1f);

                    var sprGo = new GameObject("Sprite");
                    sprGo.transform.SetParent(holder, false);
                    sprGo.transform.localPosition = new Vector3(0f, s.bounds.size.y * 0.45f, 0f);
                    var sr = sprGo.AddComponent<SpriteRenderer>();
                    sr.sprite = s;
                    sr.sharedMaterial = PrimitiveSprites.UnlitMaterial();
                    sr.color = Color.white;
                    sr.sortingOrder = rng.Next(2) == 0 ? -40 : -42;
                    sr.flipX = rng.Next(2) == 0;

                    _plants.Add(new Plant { T = holder, Phase = Range(rng, 0f, 6.28f) });
                }
            }

            // ---- Slow-rising bubbles (bubble_c is the pack's biggest, clearest ring) ----
            Sprite bubble = packArt ? BubbleArt.Get("bubble_c") : null;
            if (bubble == null) bubble = PrimitiveSprites.Circle();
            for (int i = 0; i < RisingBubbleCount; i++)
            {
                float scale = Range(rng, 0.08f, 0.20f);
                var sr = MakeSprite("Riser", bubble,
                                    new Vector3(Range(rng, _left, _right), Range(rng, bottom, top), 0f),
                                    new Color(1f, 1f, 1f, Range(rng, 0.30f, 0.55f)), -20);
                sr.transform.localScale = new Vector3(scale, scale, 1f);
                _risers.Add(new Riser
                {
                    T = sr.transform,
                    Speed = Range(rng, 0.3f, 0.8f),
                    Phase = Range(rng, 0f, 6.28f)
                });
            }

            // ---- Ambient swimmers (behind the board bubbles): a whole cast, not a cameo ----
            if (packArt)
            {
                const int swimmerCount = 6;
                int firstKind = rng.Next(SwimmerNames.Length);
                for (int i = 0; i < swimmerCount; i++)
                {
                    // Cycle the pool from a random start so every fish kind gets used.
                    var s = BubbleArt.Get(SwimmerNames[(firstKind + i) % SwimmerNames.Length]);
                    if (s == null) continue;
                    float scale = Range(rng, 0.55f, 1.0f);
                    bool goingRight = rng.Next(2) == 0;
                    // Spread the lanes across the water column so fish appear at all depths.
                    float baseY = Mathf.Lerp(bottom + 1.6f, top - 1.6f,
                                             (i + 0.5f) / swimmerCount + Range(rng, -0.08f, 0.08f));
                    var sr = MakeSprite("Swimmer", s,
                                        new Vector3(Range(rng, _left, _right), baseY, 0f),
                                        new Color(1f, 1f, 1f, Range(rng, 0.85f, 1f)), -30, !goingRight);
                    sr.transform.localScale = new Vector3(scale, scale, 1f);
                    _swimmers.Add(new Swimmer
                    {
                        T = sr.transform,
                        Sr = sr,
                        Speed = Range(rng, 0.4f, 1.2f) * (goingRight ? 1f : -1f),
                        BaseY = baseY,
                        Phase = Range(rng, 0f, 6.28f)
                    });
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime; // scaled: pause freezes the reef too
            if (dt <= 0f) return;
            float t = Time.time;

            foreach (var r in _risers)
            {
                Vector3 p = r.T.position;
                p.y += r.Speed * dt;
                p.x += Mathf.Sin(t * 1.4f + r.Phase) * 0.25f * dt; // gentle sway
                if (p.y > _top + 0.5f)
                {
                    p.y = _bottom - 0.5f;
                    p.x = Mathf.Lerp(_left, _right, Mathf.PingPong(r.Phase + t * 0.13f, 1f));
                }
                r.T.position = p;
            }

            foreach (var p in _plants)
            {
                // Gentle current: a few degrees of sway, out of phase per plant.
                p.T.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f + p.Phase) * 3.5f);
            }

            foreach (var f in _swimmers)
            {
                Vector3 p = f.T.position;
                p.x += f.Speed * dt;
                p.y = f.BaseY + Mathf.Sin(t * 1.8f + f.Phase) * 0.15f;
                if (f.Speed > 0f && p.x > _right + 1.2f)
                {
                    p.x = _left - 1.2f;
                    f.BaseY = Mathf.Lerp(_bottom + 2.0f, _top - 2.0f, Mathf.PingPong(f.Phase + t * 0.07f, 1f));
                }
                else if (f.Speed < 0f && p.x < _left - 1.2f)
                {
                    p.x = _right + 1.2f;
                    f.BaseY = Mathf.Lerp(_bottom + 2.0f, _top - 2.0f, Mathf.PingPong(f.Phase + t * 0.07f, 1f));
                }
                f.T.position = p;
            }
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Vector3 pos,
                                          Color color, int order, bool flipX = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = PrimitiveSprites.UnlitMaterial();
            sr.color = color;
            sr.sortingOrder = order; // negative = always behind gameplay
            sr.flipX = flipX;        // pack fish face right natively
            return sr;
        }

        private static float Range(System.Random rng, float min, float max) =>
            min + (float)rng.NextDouble() * (max - min);
    }
}
