using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// One logical board cell. This is grid DATA — it is the source of truth for the
    /// deterministic layer (aim/match/connectivity). Physics never mutates it.
    ///
    /// Per the Phase 1 spec each cell holds: occupied, color, and a reference to its
    /// physics representation once detached (null while attached).
    /// </summary>
    public class BoardCell
    {
        public readonly int Col;
        public readonly int Row;

        public bool Occupied;
        public BubbleColor Color;

        /// <summary>
        /// ICE obstacle state: a frozen bubble keeps its color but is NOT matchable (the
        /// match flood-fill skips it) until it thaws — which happens when any adjacent
        /// cluster is popped. Connectivity/dropping is unaffected by freezing.
        /// </summary>
        public bool Frozen;

        /// <summary>The static, attached-bubble visual (has no Rigidbody2D). Null when empty.</summary>
        public GameObject View;

        /// <summary>
        /// The physics body this cell's bubble became when it detached (assigned by the
        /// cascade controller at handoff; null while attached). Kept as an inspectable
        /// breadcrumb per the Prompt 2 spec; cleared when the cell is reloaded/re-attached.
        /// Note: the referenced body self-destructs when its fall ends.
        /// </summary>
        public GameObject PhysicsBody;

        public BoardCell(int col, int row)
        {
            Col = col;
            Row = row;
            Occupied = false;
            Color = BubbleColor.None;
            View = null;
            PhysicsBody = null;
        }

        public Vector2Int Coord => new Vector2Int(Col, Row);

        public void Clear()
        {
            Occupied = false;
            Color = BubbleColor.None;
            Frozen = false;
            View = null;
            PhysicsBody = null;
        }
    }
}
