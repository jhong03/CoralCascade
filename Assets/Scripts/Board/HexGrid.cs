using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Grid coordinate math for a hexagonal "odd-r" offset layout (pointy-top bubbles,
    /// odd rows shoved right by half a diameter). This is the classic bubble-shooter
    /// packing: every bubble has up to 6 equidistant neighbors.
    ///
    /// TRADEOFF (flagged per spec): hex odd-r was chosen over a square grid because
    /// square packing gives 4/8 neighbors and reads wrong for a bubble shooter. The
    /// only cost is that neighbor offsets differ between even and odd rows — handled
    /// once here in <see cref="NeighborOffsets"/> so nothing else needs to care.
    ///
    /// Pure math, no scene dependencies, fully deterministic. World layout:
    ///   x = originX + col*Diameter + (row is odd ? Diameter/2 : 0)
    ///   y = originY - row*RowHeight     (RowHeight = Diameter * sqrt(3)/2)
    /// </summary>
    public class HexGrid
    {
        public readonly int Columns;
        public readonly int Rows;
        public readonly float Diameter;
        public readonly float RowHeight;
        public readonly Vector2 Origin; // world position of cell (col=0, row=0) center

        public float Radius => Diameter * 0.5f;

        public HexGrid(int columns, int rows, float diameter, Vector2 origin)
        {
            Columns = columns;
            Rows = rows;
            Diameter = diameter;
            RowHeight = diameter * 0.8660254f; // sqrt(3)/2
            Origin = origin;
        }

        /// <summary>
        /// Descending-pressure support. When the board shifts down one row (content moves
        /// r → r+1), a rigid translation is only possible if the half-diameter lean swaps
        /// sides: row r+1 must lean exactly like row r used to. Toggling this flip makes
        /// "odd rows lean" become "even rows lean" (and back), so every shifted bubble
        /// keeps its exact world x and all six neighbor relationships. ALL parity math in
        /// this class routes through <see cref="ParityIndex"/> — keep it that way.
        /// </summary>
        public int ParityFlip { get; private set; }
        public void ToggleParityFlip() => ParityFlip ^= 1;
        public void ResetParityFlip() => ParityFlip = 0;

        private int ParityIndex(int row) => (row + ParityFlip) & 1;

        // odd-r neighbor deltas as (dCol, dRow). Index 0 = even rows, 1 = odd rows.
        // Validated: with the world layout above, all six land exactly one Diameter away.
        private static readonly Vector2Int[][] NeighborDeltas =
        {
            // even rows
            new[]
            {
                new Vector2Int(+1,  0), new Vector2Int( 0, -1), new Vector2Int(-1, -1),
                new Vector2Int(-1,  0), new Vector2Int(-1, +1), new Vector2Int( 0, +1)
            },
            // odd rows
            new[]
            {
                new Vector2Int(+1,  0), new Vector2Int(+1, -1), new Vector2Int( 0, -1),
                new Vector2Int(-1,  0), new Vector2Int( 0, +1), new Vector2Int(+1, +1)
            }
        };

        public bool InBounds(int col, int row)
        {
            return col >= 0 && col < Columns && row >= 0 && row < Rows;
        }

        public Vector2 CellToWorld(int col, int row)
        {
            float x = Origin.x + col * Diameter + (ParityIndex(row) == 1 ? Diameter * 0.5f : 0f);
            float y = Origin.y - row * RowHeight;
            return new Vector2(x, y);
        }

        /// <summary>Yields the up-to-6 in-bounds neighbor coordinates of a cell.</summary>
        public IEnumerable<Vector2Int> NeighborCoords(int col, int row)
        {
            var deltas = NeighborDeltas[ParityIndex(row)];
            for (int i = 0; i < deltas.Length; i++)
            {
                int nc = col + deltas[i].x;
                int nr = row + deltas[i].y;
                if (InBounds(nc, nr))
                    yield return new Vector2Int(nc, nr);
            }
        }

        /// <summary>
        /// Finds the in-bounds cell whose center is closest to a world point. Deterministic:
        /// pure geometry, no physics. Returns (-1,-1) if the grid is empty.
        /// </summary>
        public Vector2Int WorldToNearestCell(Vector2 world)
        {
            if (Columns <= 0 || Rows <= 0) return new Vector2Int(-1, -1);

            int approxRow = Mathf.RoundToInt((Origin.y - world.y) / RowHeight);
            approxRow = Mathf.Clamp(approxRow, 0, Rows - 1);
            float rowShift = ParityIndex(approxRow) == 1 ? Diameter * 0.5f : 0f;
            int approxCol = Mathf.RoundToInt((world.x - Origin.x - rowShift) / Diameter);

            // Refine over a small neighborhood; offset rows mean the naive guess can be off by one.
            var best = new Vector2Int(-1, -1);
            float bestSqr = float.MaxValue;
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    int c = approxCol + dc;
                    int r = approxRow + dr;
                    if (!InBounds(c, r)) continue;
                    float sqr = ((Vector2)CellToWorld(c, r) - world).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = new Vector2Int(c, r);
                    }
                }
            }
            return best;
        }
    }
}
