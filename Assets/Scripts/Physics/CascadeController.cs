using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Owns the post-detachment mechanic: converting matched/floating grid bubbles into
    /// physics bodies, secondary chain reactions, and the slow-mo cascade beat. Also hosts
    /// the scripted-clear baseline so the harness can A/B "feel" (physics on vs off).
    ///
    /// Cascades are tracked as a BURST: from the first detachment after quiescence until the
    /// last falling bubble is gone. Because the player can keep shooting while bubbles fall,
    /// overlapping shots feed the same burst — the counters (and the slow-mo beat) describe
    /// the whole on-screen avalanche, which is what the player actually perceives.
    ///
    /// Everything here runs strictly AFTER the deterministic shot/match/detach.
    /// </summary>
    public class CascadeController : MonoBehaviour
    {
        [Header("Physics feel")]
        // Approach speed ALONG THE CONTACT NORMAL needed to dislodge an attached bubble.
        // ~6 u/s is roughly a 3-row free fall at gravityScale 1.4 — grazes don't count.
        public float ImpactThreshold = 6.0f;
        public float BurstSpread = 1.2f;        // slight outward kick when a cluster detaches

        [Header("Slow-mo beat (Prompt 6)")]
        public int SlowMoBubbleThreshold = 15;   // cascade size that trips the beat
        public int SlowMoSecondaryThreshold = 2; // ...or this many secondary chains
        // Deepened 2026-07-14 (user: "make slow-mo more obvious"): lower scale + longer
        // hold; GameFlow adds a cyan bullet-time tint whenever timeScale dips below ~0.85.
        public float SlowMoScale = 0.22f;
        public float SlowMoHoldSeconds = 0.45f;
        public float SlowMoRampSeconds = 0.18f;

        /// <summary>Debug A/B toggle: physics cascade vs instant scripted clear.</summary>
        public bool PhysicsCascadeEnabled = true;

        public int CascadeSize { get; private set; }
        public int SecondaryChains { get; private set; }

        /// <summary>True while any detached bubble is still falling.</summary>
        public bool IsActive => _activeFalling > 0;

        private BoardManager _manager;
        private Transform _fallRoot;
        private float _diameter;
        private float _killY;
        private PhysicsMaterial2D _material;

        private int _activeFalling;
        private bool _slowMoFiredThisBurst;
        private bool _burstOpen;
        private Coroutine _slowMoRoutine;

        public void Init(BoardManager manager, Transform fallRoot, float diameter, float killY)
        {
            _manager = manager;
            _fallRoot = fallRoot;
            _diameter = diameter;
            _killY = killY;
            _material = new PhysicsMaterial2D("BubbleBounce") { bounciness = 0.25f, friction = 0.35f };
        }

        // ---- Entry points ------------------------------------------------------------------

        /// <summary>
        /// Resolves a match: detach the matched cluster, then any newly floating clusters.
        /// In scripted mode this completes synchronously; in physics mode bodies are spawned
        /// and secondary chains resolve asynchronously as they collide.
        /// </summary>
        public void ResolveMatch(List<BoardCell> matched)
        {
            EnsureBurst();
            int sizeBefore = CascadeSize; // this SHOT's contribution gates the avalanche moment
            ThawNeighbors(matched); // a pop thaws adjacent ice BEFORE the cluster detaches
            int clusterSize = matched != null ? matched.Count : 0;
            Vector2 matchCenter = Centroid(matched);
            int matchedCount = DetachCells(matched, DetachFx.Match, clusterSize);
            _manager.Score.AddMatched(matchedCount);
            ResetDetachTally();
            int dropped = DetachAllFloating();
            _manager.Score.AddDropped(dropped);
            if (PopEffects.Instance != null)
            {
                // Score popups color-coded by cause: white pops, aqua drops (2×).
                if (matchedCount > 0)
                    PopEffects.Instance.ScorePopup(matchCenter, matchedCount * ScoreKeeper.MatchedPoints,
                                                   new Color(0.95f, 0.99f, 1f));
                if (dropped > 0)
                    PopEffects.Instance.ScorePopup(TallyCenter(), dropped * ScoreKeeper.DroppedPoints,
                                                   new Color(0.55f, 0.95f, 1f));
            }

            var fx = PopEffects.Instance;
            if (fx != null)
            {
                if (clusterSize >= 7) fx.Shake(0.15f); // a monster match earns a bump
                // Praise ladder for THIS shot; the slow-mo cheer (below) outranks these —
                // the Announce slot holds one banner, so the highest tier must fire last.
                if (dropped >= 5)
                    fx.Announce("NICE CASCADE!", new Color(0.55f, 0.95f, 1f), 1.1f);
                else if (clusterSize >= 9)
                    fx.Announce("BIG COMBO!!", new Color(1f, 0.85f, 0.30f), 1.2f);
                else if (clusterSize >= 6)
                    fx.Announce("NICE POP!", Color.white, 1f);
            }
            EvaluateSlowMo(CascadeSize - sizeBefore);
            if (_activeFalling == 0)
                FinishBurst(); // scripted mode, or nothing actually detached
        }

        /// <summary>
        /// Detaches floating clusters even without a match. On a stable board this is a
        /// no-op; it matters when a shot lands somewhere whose support was knocked out by a
        /// still-falling cascade during the bubble's flight.
        /// </summary>
        public void ResolveFloating()
        {
            var floating = _manager.Board.FindFloatingClusters();
            if (floating.Count == 0) return;

            EnsureBurst();
            ResetDetachTally();
            int dropped = 0;
            foreach (var cluster in floating)
                dropped += DetachCells(cluster, DetachFx.Drop);
            _manager.Score.AddDropped(dropped);
            if (dropped > 0 && PopEffects.Instance != null)
                PopEffects.Instance.ScorePopup(TallyCenter(), dropped * ScoreKeeper.DroppedPoints,
                                               new Color(0.55f, 0.95f, 1f));
            EvaluateSlowMo(dropped);
            if (_activeFalling == 0)
                FinishBurst();
        }

        // ---- Burst lifecycle ---------------------------------------------------------------

        private void EnsureBurst()
        {
            if (_burstOpen) return;
            _burstOpen = true;
            CascadeSize = 0;
            SecondaryChains = 0;
            _slowMoFiredThisBurst = false;
        }

        private void FinishBurst()
        {
            if (!_burstOpen) return;
            _burstOpen = false;
            _manager.EvaluateState();
        }

        /// <summary>Tears down any in-flight cascade (used when a layout is (re)loaded).</summary>
        public void ResetState()
        {
            StopAllCoroutines();
            _slowMoRoutine = null;
            Time.timeScale = 1f;
            if (_fallRoot != null)
            {
                for (int i = _fallRoot.childCount - 1; i >= 0; i--)
                {
                    var child = _fallRoot.GetChild(i);
                    var fb = child.GetComponent<FallingBubble>();
                    if (fb != null) fb.Detach(); // suppress settle callback during teardown
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
            _activeFalling = 0;
            _burstOpen = false;
            _slowMoFiredThisBurst = false;
            CascadeSize = 0;
            SecondaryChains = 0;
        }

        /// <summary>
        /// Ice rule: popping a cluster thaws every frozen bubble adjacent to it. Thawing is
        /// part of the DETERMINISTIC layer (it happens at match resolution, before any
        /// physics), so the fairness boundary is untouched.
        /// </summary>
        private void ThawNeighbors(List<BoardCell> popped)
        {
            if (popped == null) return;
            foreach (var cell in popped)
            {
                foreach (var n in _manager.Board.Neighbors(cell))
                {
                    if (!n.Occupied || !n.Frozen) continue;
                    n.Frozen = false;
                    _manager.BoardView.RefreshCell(n);
                    if (PopEffects.Instance != null)
                        PopEffects.Instance.ThawGlint(_manager.Board.Grid.CellToWorld(n.Col, n.Row));
                }
            }
        }

        // ---- Detachment ----------------------------------------------------------------------

        /// <summary>Visual tier for a detachment — picks which PopEffects burst plays.</summary>
        private enum DetachFx { None, Match, Drop, Knock }

        // Running centroid of cells detached since the last ResetDetachTally — gives the
        // score popup for a multi-cluster drop a single sensible anchor point.
        private Vector2 _tallySum;
        private int _tallyCount;

        private void ResetDetachTally() { _tallySum = Vector2.zero; _tallyCount = 0; }
        private Vector2 TallyCenter() => _tallyCount > 0 ? _tallySum / _tallyCount : Vector2.zero;

        private Vector2 Centroid(List<BoardCell> cells)
        {
            if (cells == null || cells.Count == 0) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            foreach (var cell in cells)
                sum += _manager.Board.Grid.CellToWorld(cell.Col, cell.Row);
            return sum / cells.Count;
        }

        private int DetachAllFloating()
        {
            int detached = 0;
            foreach (var cluster in _manager.Board.FindFloatingClusters())
                detached += DetachCells(cluster, DetachFx.Drop);
            return detached;
        }

        /// <summary>Returns how many bubbles actually detached (callers score by cause).</summary>
        private int DetachCells(List<BoardCell> cells, DetachFx fx = DetachFx.None, int clusterSize = 0)
        {
            if (cells == null) return 0;
            int detached = 0;
            foreach (var cell in cells)
            {
                if (!cell.Occupied) continue;
                detached++;

                Vector2 pos = _manager.Board.Grid.CellToWorld(cell.Col, cell.Row);
                BubbleColor color = cell.Color;
                GameObject oldView = cell.View;
                _tallySum += pos;
                _tallyCount++;

                // Juice by cause — strictly after the deterministic resolution above.
                var effects = PopEffects.Instance;
                if (effects != null)
                {
                    switch (fx)
                    {
                        case DetachFx.Match: effects.MatchPop(pos, color.ToRGBA(), clusterSize); break;
                        case DetachFx.Drop:  effects.DropPuff(pos, color.ToRGBA()); break;
                        case DetachFx.Knock: effects.KnockBurst(pos, color.ToRGBA()); break;
                    }
                }

                // Clear the grid data FIRST — the deterministic model no longer owns this bubble.
                cell.Clear();
                CascadeSize++;

                if (oldView != null)
                {
                    // Deactivate BEFORE the deferred Destroy so its collider vanishes this
                    // frame — same-frame trajectory casts (e.g. replay) must not hit ghosts.
                    oldView.SetActive(false);
                    Destroy(oldView);
                }

                if (PhysicsCascadeEnabled)
                {
                    // Handoff breadcrumb (Prompt 2 spec): the cell keeps an inspectable link
                    // to the physics body its bubble became. Cleared on reload / re-attach.
                    cell.PhysicsBody = SpawnFalling(pos, color);
                }
            }
            if (detached > 0)
                _manager.Board.BumpVersion();
            return detached;
        }

        private GameObject SpawnFalling(Vector2 pos, BubbleColor color)
        {
            var go = new GameObject("FallingBubble");
            go.transform.SetParent(_fallRoot, false);
            go.transform.position = pos;

            var fb = go.AddComponent<FallingBubble>();
            fb.Init(this, color, _diameter, _killY, ImpactThreshold, _material);

            // A small outward kick so a detached mass visibly bursts rather than sliding straight down.
            var rb = go.GetComponent<Rigidbody2D>();
            float kx = (pos.x - _manager.BoardCenterX) * 0.15f;
            rb.linearVelocity = new Vector2(kx, -0.5f) * BurstSpread;

            _activeFalling++;
            return go;
        }

        // ---- Secondary chains ----------------------------------------------------------------

        /// <summary>
        /// Called by a falling bubble when it slams into the attached structure hard enough.
        /// The struck bubble is knocked loose, then anything that was only held up through it
        /// loses its anchor and falls too — genuine emergent chain reactions (still purely a
        /// post-detachment consequence; the deterministic shot/match is long since resolved).
        /// </summary>
        public void OnHardImpact(BubbleView hitView)
        {
            if (!PhysicsCascadeEnabled || hitView == null) return;

            var cell = _manager.Board.Cell(hitView.Coord);
            if (cell == null || !cell.Occupied) return; // already knocked loose

            EnsureBurst(); // safety; a live falling bubble implies the burst is open
            int before = CascadeSize;
            Vector2 impactPos = _manager.Board.Grid.CellToWorld(cell.Col, cell.Row);

            int knocked = DetachCells(new List<BoardCell> { cell }, DetachFx.Knock);
            knocked += DetachAllFloating();

            if (CascadeSize > before)
            {
                SecondaryChains++;
                if (PopEffects.Instance != null)
                {
                    PopEffects.Instance.ImpactWave(impactPos); // shockwave + kick at the hit
                    string cheer = SecondaryChains >= 3 ? "UNSTOPPABLE!!!"
                                 : SecondaryChains == 2 ? "DOUBLE CHAIN!!"
                                 : "CHAIN REACTION!";
                    PopEffects.Instance.Announce(cheer, new Color(1f, 0.62f, 0.35f),
                                                 1f + 0.15f * Mathf.Min(SecondaryChains, 3));
                }
                // Chain knock-offs are the jackpot tier — the whole reason to set up drops.
                _manager.Score.AddChainDropped(knocked);
                if (PopEffects.Instance != null)
                    PopEffects.Instance.ScorePopup(impactPos, knocked * ScoreKeeper.ChainDroppedPoints,
                                                   new Color(1f, 0.78f, 0.35f)); // gold = 4× tier
                EvaluateSlowMo(CascadeSize - before);
                // A chain can clear the board mid-fall; win must land immediately, or a
                // concurrent shot could re-occupy the empty board and "steal" the clear.
                _manager.EvaluateState();
            }
        }

        public void NotifyFallingDestroyed()
        {
            _activeFalling = Mathf.Max(0, _activeFalling - 1);
            if (_activeFalling == 0)
                FinishBurst();
        }

        // ---- Slow-mo beat ----------------------------------------------------------------------

        /// <summary>
        /// The avalanche moment (slow-mo + celebration + MEGA banner) must be EARNED by a
        /// single resolution — <paramref name="resolveDelta"/> is how many bubbles THIS
        /// match/drop/knock detached. The burst-cumulative CascadeSize is deliberately NOT
        /// used here: firing is allowed while debris falls, so rapid-fire play chains many
        /// small matches into one long burst, and the running total used to crown a puny
        /// 3-match "MEGA CASCADE" once it crept past the threshold (user-reported).
        /// SecondaryChains stays burst-level: two chain impacts in one avalanche ARE one
        /// spectacle, however the count got there.
        /// </summary>
        private void EvaluateSlowMo(int resolveDelta)
        {
            if (_slowMoFiredThisBurst) return;
            if (resolveDelta >= SlowMoBubbleThreshold || SecondaryChains >= SlowMoSecondaryThreshold)
            {
                _slowMoFiredThisBurst = true;
                if (PopEffects.Instance != null)
                {
                    PopEffects.Instance.Celebration(); // avalanche-tier: bubble shower + shake
                    PopEffects.Instance.Announce("MEGA CASCADE!!!", new Color(1f, 0.82f, 0.25f), 1.4f);
                }
                if (_slowMoRoutine != null) StopCoroutine(_slowMoRoutine); // don't stack ramps
                _slowMoRoutine = StartCoroutine(SlowMoBeat());
            }
        }

        private IEnumerator SlowMoBeat()
        {
            // Transition uses UNSCALED time so the dip/ramp itself never stutters. Ramp from
            // the CURRENT timescale in case a previous beat was still recovering. Because this
            // is the one animation that ignores Time.timeScale, it must explicitly hold still
            // while the pause menu has frozen the game (timeScale 0 is the pause's, not ours).
            yield return RampTimeScale(Time.timeScale, SlowMoScale, SlowMoRampSeconds);
            float t = 0f;
            while (t < SlowMoHoldSeconds)
            {
                if (!GameFlow.IsPaused) t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return RampTimeScale(SlowMoScale, 1f, SlowMoRampSeconds);
            if (!GameFlow.IsPaused) Time.timeScale = 1f;
            _slowMoRoutine = null;
        }

        private IEnumerator RampTimeScale(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                if (GameFlow.IsPaused) { yield return null; continue; }
                t += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            while (GameFlow.IsPaused) yield return null;
            Time.timeScale = to;
        }
    }
}
