using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Lightweight pooled sprite particles for gameplay juice — match pops, drop puffs,
    /// chain-impact shockwaves, avalanche celebrations, ice glints — plus a tiny camera
    /// shake. Different combo tiers get visibly different shows (see CascadeController).
    ///
    /// FAIRNESS/PHYSICS SAFETY: purely visual and only ever invoked AFTER the deterministic
    /// layer has resolved. No particle has a collider; everything runs on SCALED time, so
    /// pause freezes the show and the slow-mo beat makes big pops linger (intended).
    /// The pool is hard-capped so a mega cascade can't spawn unbounded objects.
    /// </summary>
    public class PopEffects : MonoBehaviour
    {
        public static PopEffects Instance { get; private set; }

        private const int MaxParticles = 220;
        private const int EffectOrder = 25; // above all gameplay sprites; IMGUI is screen-space anyway

        private class Particle
        {
            public GameObject Go;
            public Transform T;
            public SpriteRenderer Sr;
            public Vector2 Vel;
            public float Gravity;
            public float Age, Life;
            public float ScaleFrom, ScaleTo;
            public Color ColorFrom, ColorTo;
        }

        private readonly List<Particle> _live = new List<Particle>();
        private readonly Stack<Particle> _pool = new Stack<Particle>();
        private readonly System.Random _rng = new System.Random(9241); // visuals only — NEVER gameplay
        private Transform _root;
        private Camera _cam;
        private float _shake;
        private Vector3 _shakeApplied;

        // One-slot praise banner ("BIG COMBO!!") — a new cheer replaces the current one.
        private Transform _announce;
        private TextMesh _announceMain, _announceShadow;
        private float _announceAge, _announceLife;
        private Color _announceColor;
        private bool _announceActive, _announceTried;

        // Floating score popups ("+120"), pooled like the particles.
        private class TextPop
        {
            public Transform T;
            public TextMesh Main, Shadow;
            public float Age, Life;
            public Color Color;
            public Vector2 Vel;
        }

        private const int MaxTextPops = 10;
        private readonly List<TextPop> _livePops = new List<TextPop>();
        private readonly Stack<TextPop> _popPool = new Stack<TextPop>();
        private Font _font;
        private bool _fontTried;

        private Font BuiltinFont()
        {
            if (_fontTried) return _font;
            _fontTried = true;
            try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { _font = null; }
            return _font;
        }

        public void Init(Camera cam)
        {
            Instance = this;
            _cam = cam;
            _root = new GameObject("EffectsRoot").transform;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- Tiered bursts -------------------------------------------------------------------

        /// <summary>Matched-bubble pop; bigger clusters pop bigger and golder.</summary>
        public void MatchPop(Vector2 pos, Color color, int clusterSize)
        {
            float tier = Mathf.Clamp01((clusterSize - 3) / 9f); // 3-match → 0, 12+ → full
            Ring(pos, Color.Lerp(new Color(1f, 1f, 1f, 0.85f), new Color(1f, 0.93f, 0.55f, 0.95f), tier),
                 0.25f + 0.30f * tier, 1.0f + 1.2f * tier);
            Droplets(pos, color, 4 + (int)(6 * tier), 2.5f + 2f * tier);
        }

        /// <summary>Support-cut release: subtle — the falling bubble itself is the show.</summary>
        public void DropPuff(Vector2 pos, Color color) => Droplets(pos, color, 3, 1.6f);

        /// <summary>Chain knock-off (the 4× jackpot tier): white-hot sparks.</summary>
        public void KnockBurst(Vector2 pos, Color color)
        {
            Droplets(pos, Color.white, 4, 4f);
            Droplets(pos, color, 4, 3f);
        }

        /// <summary>Shockwave at the point where falling debris smashed the structure.</summary>
        public void ImpactWave(Vector2 pos)
        {
            Ring(pos, new Color(1f, 1f, 1f, 0.9f), 0.35f, 2.4f);
            Shake(0.25f);
        }

        /// <summary>The slow-mo-worthy avalanche: a bubble shower across the view.</summary>
        public void Celebration()
        {
            if (_cam == null) return;
            float halfW = _cam.orthographicSize * _cam.aspect;
            Vector3 c = _cam.transform.position;
            Sprite ring = RingSprite();
            for (int i = 0; i < 22; i++)
            {
                var pos = new Vector2(c.x + Range(-halfW, halfW) * 0.9f,
                                      c.y + Range(-_cam.orthographicSize, 0f));
                Emit(ring, pos, new Vector2(Range(-0.3f, 0.3f), Range(1.5f, 3.2f)), 0f,
                     Range(0.7f, 1.3f), Range(0.08f, 0.22f), Range(0.12f, 0.30f),
                     new Color(1f, 1f, 1f, 0.75f), new Color(1f, 1f, 1f, 0f));
            }
            Shake(0.5f);
        }

        /// <summary>Icy sparkle when a frozen bubble thaws.</summary>
        public void ThawGlint(Vector2 pos)
        {
            var icy = new Color(0.85f, 0.97f, 1f);
            Droplets(pos, icy, 5, 2.2f);
            Ring(pos, new Color(icy.r, icy.g, icy.b, 0.8f), 0.22f, 0.9f);
        }

        /// <summary>Camera kick (max of pending shakes; decays on scaled time).
        /// Honors the player's screen-shake setting — this is the single entry point.</summary>
        public void Shake(float amount)
        {
            if (!GameSettings.ShakeEnabled) return;
            _shake = Mathf.Max(_shake, amount);
        }

        /// <summary>
        /// Big praise text ("BIG COMBO!!") centered above the board: pops in with an
        /// overshoot, holds, then fades upward. One slot — a fresh cheer replaces the
        /// last, so the HIGHEST-tier caller should fire last. Skipped silently if the
        /// built-in font is unavailable (purely cosmetic, like the launcher hint).
        /// </summary>
        public void Announce(string text, Color color, float intensity = 1f)
        {
            if (_cam == null) return;
            if (_announce == null && !CreateAnnouncement()) return;

            float ortho = _cam.orthographicSize;
            _announce.gameObject.SetActive(true);
            _announce.position = new Vector3(_cam.transform.position.x - _shakeApplied.x,
                                             _cam.transform.position.y - _shakeApplied.y + ortho * 0.30f,
                                             0f);
            float ch = ortho * 0.017f * intensity;
            _announceMain.characterSize = ch;
            _announceShadow.characterSize = ch;
            _announceShadow.transform.localPosition = new Vector3(ch * 3f, -ch * 3f, 0f);
            _announceMain.text = text;
            _announceShadow.text = text;
            _announceColor = color;
            _announceAge = 0f;
            _announceLife = 1.25f;
            _announceActive = true;
        }

        /// <summary>
        /// Floating "+120" at the scoring position — color-coded by cause so players learn
        /// the 10/20/40 economy that feeds the star meter. Capped; extras are skipped.
        /// </summary>
        public void ScorePopup(Vector2 pos, int amount, Color color)
        {
            if (amount > 0) FloatingText(pos, "+" + amount, color);
        }

        /// <summary>Any short floating text (e.g. "+1 SHOT") — rises and fades like a score pop.</summary>
        public void FloatingText(Vector2 pos, string text, Color color)
        {
            if (BuiltinFont() == null || _livePops.Count >= MaxTextPops) return;

            TextPop p = _popPool.Count > 0 ? _popPool.Pop() : CreateTextPop();
            if (p == null) return;
            p.T.gameObject.SetActive(true);
            p.T.position = new Vector3(pos.x + Range(-0.15f, 0.15f), pos.y + 0.25f, 0f);
            p.Main.text = text;
            p.Shadow.text = text;
            p.Color = color;
            p.Age = 0f;
            p.Life = 0.85f;
            p.Vel = new Vector2(Range(-0.15f, 0.15f), 1.3f);
            _livePops.Add(p);
        }

        private TextPop CreateTextPop()
        {
            var font = BuiltinFont();
            if (font == null) return null;
            var root = new GameObject("ScorePop").transform;
            root.SetParent(_root, false);

            TextMesh Make(string name, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                var tm = go.AddComponent<TextMesh>();
                tm.font = font;
                tm.fontSize = 48;
                tm.fontStyle = FontStyle.Bold;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.characterSize = 0.075f; // ≈0.36 world units tall — reads beside a bubble
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = font.material;
                mr.sortingOrder = order;
                return tm;
            }

            var shadow = Make("Shadow", EffectOrder + 2);
            shadow.transform.localPosition = new Vector3(0.025f, -0.025f, 0f);
            return new TextPop { T = root, Main = Make("Main", EffectOrder + 3), Shadow = shadow };
        }

        private bool CreateAnnouncement()
        {
            if (_announceTried) return false; // font missing — don't retry every burst
            _announceTried = true;
            var font = BuiltinFont();
            if (font == null) { _announce = null; return false; }
            try
            {
                _announce = new GameObject("Announcement").transform;

                TextMesh Make(string name, int order)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(_announce, false);
                    var tm = go.AddComponent<TextMesh>();
                    tm.font = font;
                    tm.fontSize = 72;
                    tm.fontStyle = FontStyle.Bold;
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.alignment = TextAlignment.Center;
                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = font.material;
                    mr.sortingOrder = order;
                    return tm;
                }

                _announceShadow = Make("Shadow", EffectOrder + 4);
                _announceMain = Make("Main", EffectOrder + 5);
                _announce.gameObject.SetActive(false);
                return true;
            }
            catch
            {
                _announce = null;
                return false;
            }
        }

        // ---- Emission primitives ---------------------------------------------------------------

        private void Ring(Vector2 pos, Color color, float life, float endScale)
        {
            Emit(RingSprite(), pos, Vector2.zero, 0f, life, 0.25f, endScale,
                 color, new Color(color.r, color.g, color.b, 0f));
        }

        private void Droplets(Vector2 pos, Color color, int count, float speed)
        {
            Sprite s = PrimitiveSprites.Circle();
            for (int i = 0; i < count; i++)
            {
                float ang = Range(0f, Mathf.PI * 2f);
                var vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (speed * Range(0.5f, 1.15f))
                          + Vector2.up * 0.8f;
                Emit(s, pos, vel, 7f, Range(0.35f, 0.6f), Range(0.10f, 0.16f), 0.02f,
                     color, new Color(color.r, color.g, color.b, 0f));
            }
        }

        private Sprite RingSprite()
        {
            var ring = BubbleArt.Get("bubble_c"); // the pack's clean bubble ring
            return ring != null ? ring : PrimitiveSprites.Circle();
        }

        private void Emit(Sprite sprite, Vector2 pos, Vector2 vel, float gravity, float life,
                          float scaleFrom, float scaleTo, Color from, Color to)
        {
            if (_live.Count >= MaxParticles) return; // cap: drop extra juice, never gameplay
            Particle p = _pool.Count > 0 ? _pool.Pop() : Create();
            p.Go.SetActive(true);
            p.T.position = new Vector3(pos.x, pos.y, 0f);
            p.T.localScale = new Vector3(scaleFrom, scaleFrom, 1f);
            p.Sr.sprite = sprite;
            p.Sr.color = from;
            p.Vel = vel;
            p.Gravity = gravity;
            p.Age = 0f;
            p.Life = life;
            p.ScaleFrom = scaleFrom;
            p.ScaleTo = scaleTo;
            p.ColorFrom = from;
            p.ColorTo = to;
            _live.Add(p);
        }

        private Particle Create()
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = PrimitiveSprites.UnlitMaterial();
            sr.sortingOrder = EffectOrder;
            return new Particle { Go = go, T = go.transform, Sr = sr };
        }

        // ---- Simulation ------------------------------------------------------------------------

        private void Update()
        {
            float dt = Time.deltaTime; // scaled: pause freezes, slow-mo lingers
            if (dt <= 0f) return;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    Recycle(i);
                    continue;
                }
                float t = p.Age / p.Life;
                p.Vel += Vector2.down * (p.Gravity * dt);
                p.T.position += (Vector3)(p.Vel * dt);
                float s = Mathf.Lerp(p.ScaleFrom, p.ScaleTo, t);
                p.T.localScale = new Vector3(s, s, 1f);
                p.Sr.color = Color.Lerp(p.ColorFrom, p.ColorTo, t);
            }
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 1.1f);

            for (int i = _livePops.Count - 1; i >= 0; i--)
            {
                var p = _livePops[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.T.gameObject.SetActive(false);
                    _livePops.RemoveAt(i);
                    _popPool.Push(p);
                    continue;
                }
                p.T.position += (Vector3)(p.Vel * dt);
                float fade = 1f - Mathf.Clamp01((p.Age - p.Life * 0.55f) / (p.Life * 0.45f));
                p.Main.color = new Color(p.Color.r, p.Color.g, p.Color.b, fade);
                p.Shadow.color = new Color(0.02f, 0.20f, 0.30f, 0.8f * fade);
            }

            if (_announceActive)
            {
                _announceAge += dt;
                if (_announceAge >= _announceLife)
                {
                    _announceActive = false;
                    _announce.gameObject.SetActive(false);
                }
                else
                {
                    // Pop in with overshoot → settle → fade upward.
                    float t = _announceAge;
                    float scale = t < 0.14f ? Mathf.Lerp(0.25f, 1.25f, t / 0.14f)
                                : t < 0.30f ? Mathf.Lerp(1.25f, 1f, (t - 0.14f) / 0.16f)
                                : 1f;
                    _announce.localScale = new Vector3(scale, scale, 1f);

                    float fadeT = Mathf.Clamp01((t - (_announceLife - 0.35f)) / 0.35f);
                    if (fadeT > 0f)
                        _announce.position += Vector3.up * (dt * 0.9f);
                    float a = 1f - fadeT;
                    _announceMain.color = new Color(_announceColor.r, _announceColor.g, _announceColor.b, a);
                    _announceShadow.color = new Color(0.02f, 0.20f, 0.30f, 0.85f * a);
                }
            }
        }

        private void LateUpdate()
        {
            if (_cam == null || GameFlow.IsPaused) return;
            // Re-anchor every frame (remove last offset, add the new one) so world rebuilds
            // that reposition the camera are never corrupted by a leftover shake offset.
            Vector3 pos = _cam.transform.position - _shakeApplied;
            _shakeApplied = _shake > 0.001f
                ? new Vector3(Range(-1f, 1f), Range(-1f, 1f), 0f) * (_shake * 0.22f)
                : Vector3.zero;
            _cam.transform.position = pos + _shakeApplied;
        }

        private void Recycle(int index)
        {
            var p = _live[index];
            _live.RemoveAt(index);
            p.Go.SetActive(false);
            _pool.Push(p);
        }

        private float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
    }
}
