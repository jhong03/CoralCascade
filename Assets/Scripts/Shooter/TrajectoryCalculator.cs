using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// The result of a fully-resolved shot: the polyline the bubble travels (for the preview
    /// line and the projectile animation) and the grid cell it will attach to.
    /// </summary>
    public struct TrajectoryResult
    {
        public List<Vector2> Points;   // origin -> bounce points -> contact point
        public BoardCell AttachCell;   // null if no valid attach found
        public Vector2 ContactPoint;   // where the bubble came to rest
        public bool Valid => AttachCell != null;
    }

    /// <summary>
    /// DETERMINISTIC shot resolver — the heart of the fairness boundary (game-plan §10).
    /// Given an origin, aim direction and the current board state, it computes the exact
    /// reflected path and attach cell using KINEMATIC circle casts and grid geometry only.
    /// No Rigidbody simulation, no randomness: identical inputs always yield an identical
    /// attach cell.
    ///
    /// The shot only "sees" the STATIC world — walls and attached bubbles. Anything carrying
    /// a Rigidbody2D (falling cascade debris) is skipped by the cast, so an in-progress
    /// cascade can never bend a shot. That is what makes firing DURING a cascade safe:
    /// physics stays strictly downstream of the player's deterministic input.
    /// </summary>
    public class TrajectoryCalculator
    {
        // Generous: near-horizontal (10°) shots rise only ~1.2 units per wall bounce, so a
        // small budget could run out below the board and attach somewhere never previewed.
        private const int MaxBounces = 24;
        private const float MaxSegment = 60f;

        private static readonly RaycastHit2D[] HitBuffer = new RaycastHit2D[64];
        // Debris lives on the built-in Ignore Raycast layer, so it can't even crowd the hit
        // buffer, let alone be hit (DefaultRaycastLayers = everything except that layer).
        private static readonly ContactFilter2D StaticWorldFilter = MakeFilter();

        private static ContactFilter2D MakeFilter()
        {
            var f = new ContactFilter2D();
            f.SetLayerMask(Physics2D.DefaultRaycastLayers);
            return f;
        }

        private readonly Board _board;
        private readonly float _castRadius;

        public TrajectoryCalculator(Board board)
        {
            _board = board;
            // Slightly under the true radius so we don't pre-trigger on grazing contact.
            _castRadius = board.Grid.Radius * 0.92f;
        }

        public TrajectoryResult Compute(Vector2 origin, Vector2 aimDir)
        {
            // Ensure the physics world matches current transforms before querying (static
            // colliders moved this frame otherwise wouldn't be seen). Keeps casts deterministic.
            Physics2D.SyncTransforms();

            var result = new TrajectoryResult
            {
                Points = new List<Vector2> { origin },
                AttachCell = null,
                ContactPoint = origin
            };

            Vector2 pos = origin;
            Vector2 dir = aimDir.sqrMagnitude > 1e-6f ? aimDir.normalized : Vector2.up;

            for (int bounce = 0; bounce <= MaxBounces; bounce++)
            {
                RaycastHit2D hit = CastStaticOnly(pos, dir);
                if (hit.collider == null)
                {
                    // No enclosure hit — degenerate; end the line without attaching.
                    result.Points.Add(pos + dir * MaxSegment);
                    return result;
                }

                Vector2 contact = hit.centroid; // circle center at the moment of contact
                var wall = hit.collider.GetComponent<WallMarker>();
                bool isSideWall = wall != null && !wall.IsCeiling && Mathf.Abs(hit.normal.x) > 0.5f;

                if (isSideWall)
                {
                    result.Points.Add(contact);
                    dir = Vector2.Reflect(dir, hit.normal).normalized;
                    pos = contact + hit.normal * 0.01f; // nudge off the wall to avoid re-hit
                    continue;
                }

                // Ceiling or an existing attached bubble => this is where we attach.
                result.Points.Add(contact);
                result.ContactPoint = contact;
                result.AttachCell = _board.FindAttachCell(contact);
                return result;
            }

            // Bounce budget exhausted (pathological near-horizontal aim). Refuse the shot —
            // never attach somewhere the preview line didn't show.
            result.ContactPoint = pos;
            result.AttachCell = null;
            return result;
        }

        /// <summary>
        /// Nearest cast hit against STATIC colliders only. Falling debris (anything with an
        /// attachedRigidbody) is invisible to the deterministic shot — the fairness boundary
        /// expressed as a filter. Hits are compared by fraction; ghost-collider ties can't
        /// occur because views are deactivated the moment they leave the grid.
        /// </summary>
        private RaycastHit2D CastStaticOnly(Vector2 pos, Vector2 dir)
        {
            int count = Physics2D.CircleCast(pos, _castRadius, dir, StaticWorldFilter, HitBuffer, MaxSegment);
            if (count == HitBuffer.Length)
                Debug.LogWarning("[Trajectory] Hit buffer saturated — nearest-hit selection may be unreliable.");

            RaycastHit2D best = default;
            float bestFraction = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var h = HitBuffer[i];
                if (h.collider == null) continue;
                if (h.collider.attachedRigidbody != null) continue; // belt & braces vs the layer filter
                if (h.fraction < bestFraction)
                {
                    bestFraction = h.fraction;
                    best = h;
                }
            }
            return best;
        }
    }
}
