using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Central orchestrator for one board:
    ///   shot -> deterministic attach -> match (Prompt 4) -> cascade (Prompt 5/6) -> win/lose (Prompt 7).
    ///
    /// Firing is only locked while a projectile is IN FLIGHT (a few hundred ms), not while a
    /// cascade falls — shooting stays responsive during the avalanche, which is where the fun
    /// is. The fairness boundary survives because the deterministic trajectory only ever sees
    /// the static grid (falling debris is invisible to it — see TrajectoryCalculator), and the
    /// grid itself is only mutated by deterministic rules (attach, flood-fill, connectivity).
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        public Board Board { get; private set; }
        public BoardView BoardView { get; private set; }
        public GameStateController State { get; private set; } = new GameStateController();
        public ScoreKeeper Score { get; private set; } = new ScoreKeeper();
        public CascadeController Cascade { get; private set; }

        public float BoardCenterX { get; private set; }
        public BoardLayoutData CurrentLayout { get; private set; }
        public Vector2Int? LastAttachCoord { get; private set; }

        /// <summary>
        /// Set by GameBootstrap: rebuilds the column-count-dependent world (grid/board model,
        /// walls, camera framing, launcher position) and calls <see cref="Rebind"/>. Invoked
        /// by LoadLayout whenever a layout declares a different width than the current board.
        /// </summary>
        public System.Action<int> WorldRebuilder;

        private Launcher _launcher;
        private TrajectoryCalculator _trajectory;
        private Transform _projectileRoot;
        private float _diameter;
        private int _defaultShots;
        private bool _busy; // true ONLY while a fired bubble is travelling to its attach cell

        // ---- Descending pressure (roadmap step 3) ----
        // Every PressureEveryShots shots the board shifts down a row (new deterministic row
        // at the ceiling); crossing DangerRow loses. All deterministic: drop timing counts
        // COMMITTED shots, new rows are seeded by layout name + drop index — replays match.
        private int _shotsSincePressure;
        private int _pressureDropIndex;
        private readonly System.Collections.Generic.List<BubbleColor> _pressurePalette =
            new System.Collections.Generic.List<BubbleColor>();
        private GameObject _dangerLine;

        /// <summary>True when the loss was the danger line, not the shot budget (overlay text).</summary>
        public bool PressureLoss { get; private set; }
        public bool PressureActive => CurrentLayout != null && CurrentLayout.PressureEveryShots > 0;
        /// <summary>Shots until the next pressure drop, or -1 when pressure is off (HUD).</summary>
        public int ShotsUntilPressure =>
            PressureActive ? CurrentLayout.PressureEveryShots - _shotsSincePressure : -1;
        public int DangerRowResolved =>
            CurrentLayout != null && CurrentLayout.DangerRow > 0
                ? Mathf.Min(CurrentLayout.DangerRow, Board.Rows - 1)
                : Board.Rows - 2;

        // ShotsRemaining must gate firing here too: lose is only DECLARED once the table is
        // quiet, so without this a player at 0 shots could keep firing while debris falls.
        public bool CanFire => State.State == GameState.Playing && !_busy && State.ShotsRemaining > 0;
        /// <summary>True only during a projectile's flight — cascades do NOT block firing.</summary>
        public bool IsShotInFlight => _busy;

        public void Init(BoardView boardView, CascadeController cascade,
                         Launcher launcher, Transform projectileRoot,
                         float diameter, int defaultShots)
        {
            BoardView = boardView;
            Cascade = cascade;
            _launcher = launcher;
            _projectileRoot = projectileRoot;
            _diameter = diameter;
            _defaultShots = defaultShots;
        }

        /// <summary>Adopts a (re)built board model. Called by the world builder.</summary>
        public void Rebind(Board board, float boardCenterX)
        {
            Board = board;
            BoardCenterX = boardCenterX;
            _trajectory = new TrajectoryCalculator(board);
        }

        // ---- Layout loading --------------------------------------------------------------

        public void LoadLayout(BoardLayoutData data)
        {
            CurrentLayout = data;

            // Levels declare their own width — rebuild the world when it changes.
            if (Board == null || data.Columns != Board.Columns)
                WorldRebuilder?.Invoke(data.Columns);

            _busy = false;
            LastAttachCoord = null;
            Time.timeScale = 1f;

            // Kill any in-flight projectile — its pending attach belongs to the OLD board.
            // (Deactivating stops its travel coroutine immediately; Destroy is end-of-frame.)
            for (int i = _projectileRoot.childCount - 1; i >= 0; i--)
            {
                var child = _projectileRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            Cascade.ResetState();
            Board.LoadData(data);
            BoardView.RebuildAll();
            Score.Reset();
            // Per-level difficulty dial: a layout can carry its own shot budget.
            State.Reset(data.Shots > 0 ? data.Shots : _defaultShots);

            // Pressure state resets with the level; new ceiling rows draw only from the
            // colors this level actually starts with.
            _shotsSincePressure = 0;
            _pressureDropIndex = 0;
            PressureLoss = false;
            Board.CollectColors(_pressurePalette);
            if (_pressurePalette.Count == 0)
                _pressurePalette.AddRange(BubbleColorExtensions.Playable);
            UpdateDangerLine();

            Debug.Log($"[Board] Loaded '{data.Name}' — {Board.OccupiedCount} bubbles, {State.ShotsRemaining} shots.");
        }

        // ---- Aim preview (no side effects) -----------------------------------------------

        public TrajectoryResult PredictTrajectory(Vector2 origin, Vector2 dir)
        {
            return _trajectory.Compute(origin, dir);
        }

        // ---- Firing ----------------------------------------------------------------------

        /// <summary>
        /// Fires a shot. The attach cell is resolved deterministically HERE and logged
        /// immediately, then a purely-cosmetic projectile animates to it before the attach
        /// is committed. Returns false (without consuming a shot) if there is no valid attach.
        /// </summary>
        public bool TryFire(Vector2 origin, Vector2 dir, BubbleColor color)
        {
            if (!CanFire) return false;

            TrajectoryResult result = _trajectory.Compute(origin, dir);
            if (!result.Valid)
            {
                Debug.LogWarning("[Shot] No valid attach cell — shot ignored.");
                return false;
            }

            Vector2Int coord = result.AttachCell.Coord;
            LastAttachCoord = coord;
            _busy = true;
            State.ConsumeShot();

            // Deterministic result — identical inputs always log the same cell.
            Debug.Log($"[Shot] color={color} angle={Vector2.SignedAngle(Vector2.right, dir):0.0}° -> attach=({coord.x},{coord.y})");

            var go = new GameObject("Projectile");
            go.transform.SetParent(_projectileRoot, false);
            var proj = go.AddComponent<Projectile>();
            proj.Launch(result.Points, color, _diameter, () => CommitAttach(coord, color));
            return true;
        }

        private void CommitAttach(Vector2Int coord, BubbleColor color)
        {
            // The deterministic resolution for this shot completes synchronously below —
            // the launcher may accept the next shot as soon as we return.
            _busy = false;

            // The board may have been won/lost while this bubble was airborne (a secondary
            // chain can clear the board mid-fall). A dead board takes no new bubbles.
            if (State.State != GameState.Playing)
                return;

            var cell = Board.Cell(coord);
            if (cell == null || cell.Occupied)
            {
                // Defensive: cells are only ever filled by shots (one in flight at a time),
                // so this should be unreachable — but never strand the game if it happens.
                Debug.LogWarning($"[Shot] Attach cell ({coord.x},{coord.y}) unavailable at commit — shot fizzles.");
                EvaluateState();
                return;
            }

            cell.Occupied = true;
            cell.Color = color;
            Board.BumpVersion();
            BoardView.SpawnView(cell);

            // Match detection (Prompt 4): flood-fill same-color cluster from the new bubble.
            var cluster = Board.FindColorCluster(cell);
            Debug.Log($"[Match] cluster of {cluster.Count} {color} at ({coord.x},{coord.y})");

            if (cluster.Count >= 3)
            {
                Cascade.ResolveMatch(cluster);
            }
            else
            {
                // No-op on a stable board. Matters when a concurrent cascade's secondary
                // chain knocked out this bubble's support while it was in flight.
                Cascade.ResolveFloating();
            }

            HandlePressureAfterShot();
            EvaluateState();
        }

        // ---- Descending pressure -----------------------------------------------------------

        /// <summary>
        /// Runs after a shot's FULL deterministic resolution (attach + match + floating):
        /// ticks the pressure counter, performs the drop when due, then judges the danger
        /// line. Judging after resolution is the grace rule — a match that removes the
        /// crossing bubbles saves you; a drop or attach that leaves any bubble at/below
        /// the line loses immediately (the crossing IS the failure, no quiescence needed).
        /// </summary>
        private void HandlePressureAfterShot()
        {
            if (!PressureActive) return;
            if (State.State != GameState.Playing || Board.IsCleared()) return;

            _shotsSincePressure++;
            bool overflowed = false;
            if (_shotsSincePressure >= CurrentLayout.PressureEveryShots)
            {
                _shotsSincePressure = 0;
                overflowed = !PerformPressureDrop(); // content pushed off the grid = instant loss
            }
            if (overflowed || Board.DeepestOccupiedRow() >= DangerRowResolved)
                LoseToPressure();
        }

        /// <summary>Shifts the board down one row and spawns a deterministic ceiling row.</summary>
        private bool PerformPressureDrop()
        {
            _pressureDropIndex++;
            // Seeded per level + drop index (never UnityEngine.Random): replays and repeat
            // plays of a level always see the identical descending rows.
            var rng = new System.Random(StableHash(CurrentLayout.Name) ^ (_pressureDropIndex * 7919));
            var newRow = new BubbleColor[Board.Columns];
            for (int c = 0; c < newRow.Length; c++)
                if (rng.NextDouble() < 0.85)
                    newRow[c] = _pressurePalette[rng.Next(_pressurePalette.Count)];

            bool ok = Board.ShiftDown(newRow);
            BoardView.RebuildAll(); // every view moved; same-frame-safe teardown inside

            var fx = PopEffects.Instance;
            if (fx != null)
            {
                fx.Shake(0.2f);
                fx.Announce("THE TIDE RISES!", new Color(0.55f, 0.90f, 1f), 1.05f);
            }
            Debug.Log($"[Pressure] Drop #{_pressureDropIndex} — deepest row now {Board.DeepestOccupiedRow()}" +
                      $" (danger at {DangerRowResolved}).");
            return ok;
        }

        private void LoseToPressure()
        {
            if (State.State != GameState.Playing) return;
            PressureLoss = true;
            State.LoseByPressure();
            Debug.Log("[Pressure] Bubbles crossed the danger line — LOST.");
            if (PopEffects.Instance != null)
                PopEffects.Instance.Announce("THE REEF FLOODED!", new Color(1f, 0.5f, 0.4f), 1.2f);
        }

        /// <summary>Shows/positions the danger line for pressure levels (hidden otherwise).</summary>
        private void UpdateDangerLine()
        {
            if (!PressureActive)
            {
                if (_dangerLine != null) _dangerLine.SetActive(false);
                return;
            }
            if (_dangerLine == null)
            {
                _dangerLine = new GameObject("DangerLine");
                var sr = _dangerLine.AddComponent<SpriteRenderer>();
                sr.sprite = PrimitiveSprites.Pixel();
                sr.sharedMaterial = PrimitiveSprites.UnlitMaterial();
                sr.sortingOrder = 5; // above backdrop, below bubbles
                _dangerLine.AddComponent<DangerLinePulse>();
            }
            _dangerLine.SetActive(true);
            // The line marks the BOUNDARY above the forbidden row.
            float y = Board.Grid.CellToWorld(0, DangerRowResolved).y + Board.Grid.RowHeight * 0.5f;
            float width = Board.Columns * Board.Grid.Diameter + Board.Grid.Diameter;
            _dangerLine.transform.position = new Vector3(BoardCenterX, y, 0f);
            _dangerLine.transform.localScale = new Vector3(width, 0.07f, 1f);
        }

        /// <summary>FNV-1a — a STABLE string hash (string.GetHashCode may vary per runtime).</summary>
        private static int StableHash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                if (s != null)
                    foreach (char ch in s)
                        h = (h ^ ch) * 16777619;
                return h;
            }
        }

        // ---- Win/lose --------------------------------------------------------------------

        /// <summary>
        /// Re-evaluates win/lose. Called after every board mutation: shot commit, burst
        /// settle, AND each secondary-chain detachment (a chain can clear the board mid-fall,
        /// and win must land before a concurrent shot could re-occupy the empty board).
        /// Win is checked whenever the board may have changed; lose only once the table is
        /// quiet — no shot in flight, nothing falling — so a still-falling cascade that would
        /// clear the board is never beaten to the punch by a premature "Lost".
        /// </summary>
        public void EvaluateState()
        {
            bool cleared = Board.IsCleared();
            bool quiescent = !_busy && !Cascade.IsActive;
            GameState before = State.State;
            State.Evaluate(cleared, quiescent);
            if (State.State != before)
            {
                if (State.State == GameState.Won)
                    Score.AddClearBonus(State.ShotsRemaining); // bank unused shots
                Debug.Log($"[State] {State.State} (shots left {State.ShotsRemaining}, score {Score.Total})");
            }
        }

        // ---- Debug: deterministic replay -------------------------------------------------

        /// <summary>
        /// Reloads the current layout and re-fires the exact last shot. Because both the
        /// board reset and the trajectory are deterministic, repeated presses log the SAME
        /// attach cell every time — the Prompt 3 acceptance check.
        /// </summary>
        public void ReplayLastShot()
        {
            var shot = _launcher.LastShot;
            if (!shot.HasValue)
            {
                Debug.LogWarning("[Replay] No shot recorded yet — fire once first.");
                return;
            }
            Debug.Log("[Replay] Reloading board and re-firing last shot deterministically...");
            LoadLayout(CurrentLayout);
            TryFire(shot.Origin, shot.Direction, shot.Color);
        }
    }

    /// <summary>Slow red pulse on the danger line so it reads as a threat, not decor.</summary>
    public class DangerLinePulse : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private void Awake() => _sr = GetComponent<SpriteRenderer>();
        private void Update()
        {
            if (_sr == null) return;
            float a = 0.35f + 0.20f * Mathf.Sin(Time.time * 3f); // scaled time: pauses freeze it
            _sr.color = new Color(1f, 0.40f, 0.35f, a);
        }
    }
}
