using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Marker on an attached-bubble GameObject linking it back to its grid cell.
    /// Used by the trajectory raycaster (to know a hit is a bubble, not a wall) and by
    /// falling bubbles (to identify what they've struck for secondary-chain checks).
    /// </summary>
    public class BubbleView : MonoBehaviour
    {
        public int Col;
        public int Row;
        public BubbleColor Color;

        public Vector2Int Coord => new Vector2Int(Col, Row);
    }

    /// <summary>Marks a wall collider so the trajectory knows whether to reflect or attach.</summary>
    public class WallMarker : MonoBehaviour
    {
        /// <summary>True for the top wall: the trajectory attaches instead of reflecting.</summary>
        public bool IsCeiling;
    }
}
