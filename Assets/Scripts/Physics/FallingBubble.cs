using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// A detached bubble handed off to Unity physics. It falls under gravity and collides
    /// with walls, the floor, other falling bubbles, and the still-attached structure.
    ///
    /// FAIRNESS BOUNDARY: this object only ever exists AFTER a deterministic shot/match/detach
    /// has already occurred (Prompts 3-4). Nothing here can change which bubbles matched or
    /// where a shot attached — it only governs what already-detached bubbles do next.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class FallingBubble : MonoBehaviour
    {
        private const float ShrinkSeconds = 0.35f; // pop-out at end of life

        // "Already gone" signalling (2026-07-19, user request). Detached debris is scored and
        // off the board the instant it detaches, but it is still a physics body — at some
        // angles it wedges in a pocket and sits there looking exactly like a live bubble, so
        // the player can't tell whether they actually popped it. It now blinks and fades from
        // early in its fall. FadeHold keeps it solid through the FAST part of the drop (that's
        // the part carrying the cascade's drama, and the part that can still chain-knock).
        private const float FadeHold = 0.15f;    // fraction of life at full opacity
        private const float BlinkStart = 0.30f;  // seconds before the pulse begins
        private const float BlinkHz = 5f;        // pulses/sec, quickens toward the end
        private const float BlinkDepth = 0.42f;  // how deep the pulse dips

        private CascadeController _cascade;
        private float _killY;
        private float _impactThreshold;
        private float _lifetime;
        private float _age;
        private float _baseScale;
        private SpriteRenderer[] _renderers;
        private Color[] _baseColors;

        /// <summary>Detach from the controller so a forced teardown doesn't fire settle callbacks.</summary>
        public void Detach() => _cascade = null;

        public void Init(CascadeController cascade, BubbleColor color, float diameter,
                         float killY, float impactThreshold, PhysicsMaterial2D mat)
        {
            _cascade = cascade;
            _killY = killY;
            _impactThreshold = impactThreshold;
            // Short life: debris that lands on the floor shouldn't clutter the launcher area
            // (the player can fire during cascades), and win/lose waits for the last of it.
            _lifetime = 2.5f;
            _baseScale = diameter;

            // Built-in Ignore Raycast layer: debris must be invisible to the deterministic
            // trajectory casts (it still COLLIDES normally — the layer only affects queries).
            gameObject.layer = 2;

            transform.localScale = new Vector3(diameter, diameter, 1f);

            BubbleArt.Apply(gameObject, color, false, 12);

            // Cache AFTER Apply — it builds the child layers (gloss) we also have to fade.
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].color;

            var col = GetComponent<CircleCollider2D>();
            col.radius = 0.5f;
            col.sharedMaterial = mat;

            var rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 1.4f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (transform.position.y < _killY || _age > _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // Shrink away over the last moments instead of blinking out.
            float remain = _lifetime - _age;
            if (remain < ShrinkSeconds)
            {
                float s = _baseScale * Mathf.Max(0.05f, remain / ShrinkSeconds);
                transform.localScale = new Vector3(s, s, 1f);
            }

            ApplyFade();
        }

        /// <summary>
        /// Blink + fade, so wedged debris never masquerades as a live bubble. Purely visual:
        /// scored, detached and collision behaviour are all untouched.
        /// </summary>
        private void ApplyFade()
        {
            if (_renderers == null) return;

            float t = Mathf.Clamp01(_age / _lifetime);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - FadeHold) / (1f - FadeHold)));

            float blink = 1f;
            if (_age > BlinkStart)
            {
                // Frequency ramps with age: a slow throb that turns into a flicker.
                float phase = (_age - BlinkStart) * Mathf.PI * 2f * BlinkHz * (0.6f + t);
                blink = 1f - BlinkDepth * (0.5f - 0.5f * Mathf.Cos(phase));
            }

            float alpha = Mathf.Clamp01(fade * blink);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                Color c = _baseColors[i];
                _renderers[i].color = new Color(c.r, c.g, c.b, c.a * alpha);
            }
        }

        private void OnDestroy()
        {
            if (_cascade != null)
                _cascade.NotifyFallingDestroyed();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_cascade == null) return;

            // Only attached bubbles trigger secondary-chain checks, and only on a hard hit.
            var view = collision.collider.GetComponent<BubbleView>();
            if (view == null) return;

            // Gate on approach speed along the contact normal — a tangential graze has a
            // large relativeVelocity.magnitude but shouldn't dislodge anything.
            if (collision.contactCount == 0) return;
            float approach = Mathf.Abs(Vector2.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
            if (approach < _impactThreshold) return;

            _cascade.OnHardImpact(view);
        }
    }
}
