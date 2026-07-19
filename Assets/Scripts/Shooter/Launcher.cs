using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// The bottom-center launcher: reads aim input, draws the predicted (bounce-aware)
    /// trajectory, and fires a bubble on release. Aim is clamped to the upper hemisphere so
    /// the deterministic trajectory never needs to consider downward shots. Records the last
    /// shot (origin/dir/color) so the debug harness can replay it deterministically.
    /// </summary>
    public class Launcher : MonoBehaviour
    {
        public struct Shot
        {
            public Vector2 Origin;
            public Vector2 Direction;
            public BubbleColor Color;
            public bool HasValue;
        }

        private BoardManager _board;
        private Camera _camera;
        private float _diameter;
        private LineRenderer _line;

        private BubbleColor _currentColor;
        private BubbleColor _nextColor;
        private GameObject _loadedBubble;
        private GameObject _nextBubble;   // the visible queue: tap it to swap with the loaded bubble

        // Color-queue fairness: draw only from colors still on the board, and re-roll queued
        // colors the moment their color leaves the board (otherwise the pure randomizer can
        // hand out colors that no longer exist, making a level uncleaable).
        private readonly List<BubbleColor> _colorsOnBoard = new List<BubbleColor>();
        private readonly List<int> _countsOnBoard = new List<int>();
        private int _boardVersionSeen = -1;

        // Softened proportional draw: the queue favors colors that have MORE bubbles left,
        // so a nearly-gone color stops being handed out as often as a full one (which just
        // wasted shots on unplaceable balls). Blend of count-weighting and uniform —
        // 1 = strict proportional (P = share), 0 = old uniform-over-distinct. The floor from
        // the uniform term keeps the last few of a color appearing often enough to clear.
        // TUNABLE: raise toward 1 to punish rare colors harder, lower to soften. See DrawColor.
        private const float ColorWeightBias = 0.65f;
        private Board _boardSeen; // boards are REBUILT when the level width changes

        private Shot _lastShot;
        public Shot LastShot => _lastShot;

        // True while the current press is a live aim (began in the play area while firing
        // was allowed). A press that began over the debug HUD belongs to the UI, not the aim.
        private bool _aimActive;

        // Aim clamp: keep at least ~10° off horizontal on both sides.
        private const float MinAngleDeg = 10f;

        public void Init(BoardManager board, Camera cam, float diameter, Vector3 position)
        {
            _board = board;
            _camera = cam;
            _diameter = diameter;
            transform.position = position;

            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = diameter * 0.12f;
            _line.numCornerVertices = 2;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.startColor = new Color(1f, 1f, 1f, 0.75f);
            _line.endColor = new Color(1f, 1f, 1f, 0.2f);
            _line.sortingOrder = 15;
            _line.positionCount = 0;

            _currentColor = DrawColor();
            _nextColor = DrawColor();
            RefreshLoadedBubble();
        }

        private void Update()
        {
            if (_board == null) return;

            // In a menu / pause / level-select screen, the launcher is inert: no preview,
            // and any held press is cancelled so it can't turn into a shot on release.
            if (!GameFlow.GameplayActive)
            {
                _aimActive = false;
                _line.positionCount = 0;
                return;
            }

            // Re-roll queued colors whose color just left the board (match, cascade, reload,
            // or a whole-board rebuild when the level width changed).
            if (_board.Board != null &&
                (_board.Board != _boardSeen || _board.Board.Version != _boardVersionSeen))
            {
                _boardSeen = _board.Board;
                _boardVersionSeen = _board.Board.Version;
                EnsureQueueMatchesBoard();
            }

            // Gate the press ONLY on UI ownership — never on CanFire. A press that lands
            // while a shot is in flight must still aim/fire once the flight ends (the preview
            // and release paths re-check CanFire), otherwise rapid-fire taps get swallowed.
            if (PointerInput.PressedThisFrame)
            {
                Vector2 press = PointerInput.ScreenPosition;
                if (IsOverNextBubble(press))
                {
                    // The tap belongs to the queue swap, never to the aim.
                    SwapColors();
                    _aimActive = false;
                }
                else
                {
                    _aimActive = !DebugHUD.IsPointerOverHUD(press)
                              && !GameFlow.IsPointerOverUI(press);
                }
            }

            Vector2 origin = transform.position;
            Vector2 aimDir = AimDirection(origin);

            if (_aimActive && _board.CanFire && PointerInput.IsPressed)
                DrawPreview(_board.PredictTrajectory(origin, aimDir));
            else
                _line.positionCount = 0;

            if (PointerInput.ReleasedThisFrame)
            {
                if (_aimActive && _board.CanFire)
                    Fire(origin, aimDir);
                _aimActive = false;
            }
        }

        private Vector2 AimDirection(Vector2 origin)
        {
            Vector3 screen = PointerInput.ScreenPosition;
            screen.z = Mathf.Abs(_camera.transform.position.z - transform.position.z);
            Vector3 world = _camera.ScreenToWorldPoint(screen);
            Vector2 dir = (Vector2)world - origin;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;

            // Clamp to the upper hemisphere with a small margin off horizontal. Atan2 spans
            // (-180,180], so a below-horizontal aim on the LEFT (~-170°) must snap to the
            // left limit — a naive clamp would flip it all the way to the right one.
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (ang < -90f)
                ang = 180f - MinAngleDeg;
            else
                ang = Mathf.Clamp(ang, MinAngleDeg, 180f - MinAngleDeg);
            float rad = ang * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private void Fire(Vector2 origin, Vector2 dir)
        {
            _lastShot = new Shot { Origin = origin, Direction = dir, Color = _currentColor, HasValue = true };
            bool fired = _board.TryFire(origin, dir, _currentColor);
            _line.positionCount = 0;
            if (fired)
            {
                Sfx.Play(Sfx.Clip.Fire, 0.45f, UnityEngine.Random.Range(0.95f, 1.08f));
                _currentColor = _nextColor;
                _nextColor = DrawColor();
                RefreshLoadedBubble();
            }
        }

        // ---- Color queue (fair randomizer + swap) -----------------------------------------

        /// <summary>
        /// Draws a random color from those still ATTACHED to the board — never a color the
        /// player can no longer match. Falls back to the full playable set only when the
        /// board is empty (level select / just cleared), where the draw doesn't matter.
        /// </summary>
        private BubbleColor DrawColor()
        {
            _board.Board.CollectColors(_colorsOnBoard, _countsOnBoard);
            if (_colorsOnBoard.Count == 0)
            {
                var p = BubbleColorExtensions.Playable;
                return p[Random.Range(0, p.Length)];
            }
            return WeightedPick();
        }

        /// <summary>
        /// Picks a color from <see cref="_colorsOnBoard"/> weighted by remaining count,
        /// softened toward uniform by <see cref="ColorWeightBias"/>. Caller must have just
        /// refreshed <see cref="_colorsOnBoard"/>/<see cref="_countsOnBoard"/> (non-empty).
        /// weight(c) = bias·count(c)·n + (1−bias)·total  ⇒  P(c) = bias·share(c) + (1−bias)/n.
        /// </summary>
        private BubbleColor WeightedPick()
        {
            int n = _colorsOnBoard.Count;
            int total = 0;
            for (int i = 0; i < n; i++) total += _countsOnBoard[i];

            float sum = 0f;
            for (int i = 0; i < n; i++)
                sum += ColorWeightBias * _countsOnBoard[i] * n + (1f - ColorWeightBias) * total;

            float r = Random.value * sum;
            for (int i = 0; i < n; i++)
            {
                r -= ColorWeightBias * _countsOnBoard[i] * n + (1f - ColorWeightBias) * total;
                if (r <= 0f) return _colorsOnBoard[i];
            }
            return _colorsOnBoard[n - 1]; // float rounding guard
        }

        /// <summary>
        /// If a queued color no longer exists on the board (its last bubbles were matched or
        /// fell in a cascade), re-roll it so the queue always holds matchable colors.
        /// </summary>
        private void EnsureQueueMatchesBoard()
        {
            _board.Board.CollectColors(_colorsOnBoard, _countsOnBoard);
            if (_colorsOnBoard.Count == 0) return;

            bool changed = false;
            if (!_colorsOnBoard.Contains(_currentColor))
            {
                _currentColor = WeightedPick();
                changed = true;
            }
            if (!_colorsOnBoard.Contains(_nextColor))
            {
                _nextColor = WeightedPick();
                changed = true;
            }
            if (changed) RefreshLoadedBubble();
        }

        private void SwapColors()
        {
            (_currentColor, _nextColor) = (_nextColor, _currentColor);
            RefreshLoadedBubble();
        }

        /// <summary>Generous hit test around the next-bubble display (min ~44 px for touch).</summary>
        private bool IsOverNextBubble(Vector2 screenPos)
        {
            if (_nextBubble == null) return false;
            Vector3 center = _camera.WorldToScreenPoint(_nextBubble.transform.position);
            Vector3 edge = _camera.WorldToScreenPoint(_nextBubble.transform.position + Vector3.right * _diameter);
            float radius = Mathf.Max((edge - center).magnitude * 1.4f, 44f);
            return ((Vector2)center - screenPos).sqrMagnitude <= radius * radius;
        }

        private void DrawPreview(TrajectoryResult result)
        {
            if (result.Points == null || result.Points.Count < 2)
            {
                _line.positionCount = 0;
                return;
            }
            _line.positionCount = result.Points.Count;
            for (int i = 0; i < result.Points.Count; i++)
                _line.SetPosition(i, new Vector3(result.Points[i].x, result.Points[i].y, 0f));
        }

        private void RefreshLoadedBubble()
        {
            if (_loadedBubble == null)
            {
                _loadedBubble = new GameObject("LoadedBubble");
                _loadedBubble.transform.SetParent(transform, false);
                _loadedBubble.transform.localScale = new Vector3(_diameter, _diameter, 1f);
            }
            if (_nextBubble == null)
                _nextBubble = CreateNextBubbleDisplay();

            // Full re-apply, not just a tint: the critter sprite must track the color too.
            BubbleArt.Apply(_loadedBubble, _currentColor, false, 18);
            BubbleArt.Apply(_nextBubble, _nextColor, false, 16);
        }

        private GameObject CreateNextBubbleDisplay()
        {
            var go = new GameObject("NextBubble");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(-_diameter * 2.0f, -_diameter * 0.35f, 0f);
            go.transform.localScale = new Vector3(_diameter * 0.65f, _diameter * 0.65f, 1f);
            // Sprite/tint/critter are applied by BubbleArt.Apply in RefreshLoadedBubble.

            // Tiny hint label; if the built-in font is unavailable, the label is just skipped.
            try
            {
                var label = new GameObject("SwapHint");
                label.transform.SetParent(go.transform, false);
                label.transform.localPosition = new Vector3(0f, -1.05f, 0f);
                // Counter the parent's 0.65*diameter scale so text size is font-driven.
                float inv = 1f / (_diameter * 0.65f);
                label.transform.localScale = new Vector3(inv, inv, 1f);
                var tm = label.AddComponent<TextMesh>();
                tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tm.text = "NEXT — tap to swap";
                tm.fontSize = 48;
                tm.characterSize = _diameter * 0.045f;
                tm.anchor = TextAnchor.UpperCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(0.02f, 0.26f, 0.36f, 0.8f); // deep teal reads on bright water
                var mr = label.GetComponent<MeshRenderer>();
                mr.sharedMaterial = tm.font.material;
                mr.sortingOrder = 17;
            }
            catch { /* purely cosmetic */ }

            return go;
        }
    }
}
