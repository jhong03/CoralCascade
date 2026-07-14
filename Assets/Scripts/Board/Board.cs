using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// The deterministic board model: a fixed grid of <see cref="BoardCell"/> plus the
    /// pure graph operations the game relies on (match flood-fill, top-anchor connectivity).
    /// No physics, no randomness — same board + same input always gives the same result.
    /// </summary>
    public class Board
    {
        public readonly HexGrid Grid;
        private readonly BoardCell[,] _cells; // [col, row]

        public int Columns => Grid.Columns;
        public int Rows => Grid.Rows;

        public Board(HexGrid grid)
        {
            Grid = grid;
            _cells = new BoardCell[grid.Columns, grid.Rows];
            for (int c = 0; c < grid.Columns; c++)
                for (int r = 0; r < grid.Rows; r++)
                    _cells[c, r] = new BoardCell(c, r);
        }

        public BoardCell Cell(int col, int row) => Grid.InBounds(col, row) ? _cells[col, row] : null;
        public BoardCell Cell(Vector2Int c) => Cell(c.x, c.y);

        /// <summary>
        /// Bumped on every occupancy mutation (load / attach / detach) so listeners — e.g.
        /// the launcher's color queue — can notice board changes without scanning per frame.
        /// </summary>
        public int Version { get; private set; }
        public void BumpVersion() => Version++;

        /// <summary>
        /// Distinct PLAYABLE colors currently attached, written into <paramref name="into"/>
        /// (cleared first). Stones are excluded — they can't be fired or matched. Frozen
        /// bubbles' colors ARE included: they become matchable after a thaw.
        /// </summary>
        public void CollectColors(List<BubbleColor> into)
        {
            into.Clear();
            foreach (var cell in AllCells)
                if (cell.Occupied && cell.Color.IsPlayable() && !into.Contains(cell.Color))
                    into.Add(cell.Color);
        }

        /// <summary>Clears every cell then fills the top rows from a layout (top row = anchor row).</summary>
        public void LoadData(BoardLayoutData data)
        {
            foreach (var cell in AllCells) cell.Clear();
            Grid.ResetParityFlip(); // pressure drops from a previous run must not leak in
            BumpVersion();
            if (data == null) return;

            int rows = Mathf.Min(data.Rows, Rows);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    var color = data.ColorAt(c, r);
                    if (color == BubbleColor.None) continue;
                    var cell = _cells[c, r];
                    cell.Occupied = true;
                    cell.Color = color;
                    cell.Frozen = data.FrozenAt(c, r);
                }
            }
        }

        public IEnumerable<BoardCell> AllCells
        {
            get
            {
                for (int c = 0; c < Columns; c++)
                    for (int r = 0; r < Rows; r++)
                        yield return _cells[c, r];
            }
        }

        public int OccupiedCount
        {
            get
            {
                int n = 0;
                foreach (var cell in AllCells) if (cell.Occupied) n++;
                return n;
            }
        }

        public IEnumerable<BoardCell> Neighbors(BoardCell cell)
        {
            foreach (var coord in Grid.NeighborCoords(cell.Col, cell.Row))
                yield return _cells[coord.x, coord.y];
        }

        // ---- Flood-fill same-color match -------------------------------------------------

        /// <summary>
        /// Connected same-color cluster containing <paramref name="start"/> (inclusive).
        /// Returns an empty list if the start cell is empty.
        /// </summary>
        public List<BoardCell> FindColorCluster(BoardCell start)
        {
            var result = new List<BoardCell>();
            if (start == null || !start.Occupied) return result;

            BubbleColor color = start.Color;
            var seen = new HashSet<BoardCell> { start };
            var stack = new Stack<BoardCell>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                var cell = stack.Pop();
                result.Add(cell);
                foreach (var n in Neighbors(cell))
                {
                    // Frozen bubbles are NOT matchable until thawed (ice obstacle rule) —
                    // they still count for connectivity/dropping elsewhere.
                    if (n.Occupied && !n.Frozen && n.Color == color && seen.Add(n))
                        stack.Push(n);
                }
            }
            return result;
        }

        // ---- Top-anchor connectivity / floating clusters ---------------------------------

        /// <summary>
        /// Every occupied cell NOT transitively connected to the top anchor row (row 0),
        /// grouped into connected clusters (any color). These are "floating" and should fall.
        /// </summary>
        public List<List<BoardCell>> FindFloatingClusters()
        {
            var anchored = new HashSet<BoardCell>();

            // BFS from all occupied cells in the top row.
            var queue = new Queue<BoardCell>();
            for (int c = 0; c < Columns; c++)
            {
                var top = _cells[c, 0];
                if (top.Occupied && anchored.Add(top))
                    queue.Enqueue(top);
            }
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var n in Neighbors(cell))
                {
                    if (n.Occupied && anchored.Add(n))
                        queue.Enqueue(n);
                }
            }

            // Anything occupied but not anchored is floating. Group into connected clusters.
            var floatingClusters = new List<List<BoardCell>>();
            var grouped = new HashSet<BoardCell>();
            foreach (var cell in AllCells)
            {
                if (!cell.Occupied || anchored.Contains(cell) || grouped.Contains(cell))
                    continue;

                var cluster = new List<BoardCell>();
                var stack = new Stack<BoardCell>();
                stack.Push(cell);
                grouped.Add(cell);
                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    cluster.Add(cur);
                    foreach (var n in Neighbors(cur))
                    {
                        if (n.Occupied && !anchored.Contains(n) && grouped.Add(n))
                            stack.Push(n);
                    }
                }
                floatingClusters.Add(cluster);
            }
            return floatingClusters;
        }

        public bool IsCleared()
        {
            foreach (var cell in AllCells) if (cell.Occupied) return false;
            return true;
        }

        /// <summary>Deepest occupied row index, or -1 on an empty board (danger-line checks).</summary>
        public int DeepestOccupiedRow()
        {
            for (int r = Rows - 1; r >= 0; r--)
                for (int c = 0; c < Columns; c++)
                    if (_cells[c, r].Occupied)
                        return r;
            return -1;
        }

        /// <summary>
        /// Descending pressure: shifts every row's content down by one, spawns
        /// <paramref name="newAnchorRow"/> in the emptied row 0, and toggles the grid's
        /// parity flip — which makes the move a RIGID translation (every bubble keeps its
        /// exact world x and all six neighbors; see HexGrid.ParityFlip). Views are NOT
        /// moved — the caller must BoardView.RebuildAll() afterwards. Returns false if
        /// occupied content was pushed off the bottom of the grid (an automatic loss).
        /// </summary>
        public bool ShiftDown(BubbleColor[] newAnchorRow)
        {
            bool overflow = false;
            for (int c = 0; c < Columns; c++)
                if (_cells[c, Rows - 1].Occupied) overflow = true;

            for (int r = Rows - 1; r >= 1; r--)
            {
                for (int c = 0; c < Columns; c++)
                {
                    var src = _cells[c, r - 1];
                    var dst = _cells[c, r];
                    dst.Occupied = src.Occupied;
                    dst.Color = src.Color;
                    dst.Frozen = src.Frozen;
                    dst.View = null;        // stale handles; RebuildAll re-links everything
                    dst.PhysicsBody = null;
                }
            }
            for (int c = 0; c < Columns; c++)
            {
                var cell = _cells[c, 0];
                cell.Clear();
                if (newAnchorRow != null && c < newAnchorRow.Length &&
                    newAnchorRow[c] != BubbleColor.None)
                {
                    cell.Occupied = true;
                    cell.Color = newAnchorRow[c];
                }
            }

            Grid.ToggleParityFlip();
            BumpVersion(); // occupancy changed everywhere — the color queue must re-check
            return !overflow;
        }

        // ---- Attach-cell resolution (deterministic) --------------------------------------

        /// <summary>
        /// An empty cell is a valid attach target if it is on the anchor row or touches an
        /// occupied cell — bubbles snap onto the ceiling or the existing structure, never
        /// into empty space.
        /// </summary>
        public bool IsValidAttach(BoardCell cell)
        {
            if (cell == null || cell.Occupied) return false;
            if (cell.Row == 0) return true;
            foreach (var n in Neighbors(cell))
                if (n.Occupied) return true;
            return false;
        }

        /// <summary>
        /// Resolves the grid cell an incoming bubble should snap to, given the world point
        /// where it came to rest (the trajectory's contact centroid). Pure geometry over the
        /// current grid state => deterministic. Prefers the nearest valid-attach empty cell;
        /// falls back to a full-board scan so a shot never silently fails to place.
        /// </summary>
        public BoardCell FindAttachCell(Vector2 worldPoint)
        {
            var nearest = Grid.WorldToNearestCell(worldPoint);
            BoardCell best = null;
            float bestSqr = float.MaxValue;

            // Local search first (radius 2 covers the offset-row worst case).
            for (int dr = -2; dr <= 2; dr++)
            {
                for (int dc = -2; dc <= 2; dc++)
                {
                    var cell = Cell(nearest.x + dc, nearest.y + dr);
                    if (!IsValidAttach(cell)) continue;
                    float sqr = ((Vector2)Grid.CellToWorld(cell.Col, cell.Row) - worldPoint).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; best = cell; }
                }
            }
            if (best != null) return best;

            // Fallback: nearest valid-attach empty cell anywhere on the board.
            foreach (var cell in AllCells)
            {
                if (!IsValidAttach(cell)) continue;
                float sqr = ((Vector2)Grid.CellToWorld(cell.Col, cell.Row) - worldPoint).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = cell; }
            }
            return best;
        }
    }
}
