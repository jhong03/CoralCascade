using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Level content as DATA, authored separately from board logic (game-plan principle:
    /// "content is data, not logic"). A layout is a grid of single-char color codes, one
    /// string per row. Chars: R O Y G B P = colors; '.', ' ', '_', '0' = empty.
    ///
    /// Rows are top-to-bottom (row 0 = ceiling/anchor row). This plain serializable class
    /// is what the board consumes, so code-authored test boards (see <see cref="TestBoards"/>)
    /// and ScriptableObject assets (<see cref="BoardLayout"/>) share one code path.
    /// </summary>
    [System.Serializable]
    public class BoardLayoutData
    {
        public string Name = "Untitled";
        public int Columns = 9;

        [Tooltip("One string per row, top row first. Each char is a color code.")]
        public string[] CellRows = new string[0];

        [Tooltip("Shot budget for this level. 0 = use the bootstrap default.")]
        public int Shots;

        [Tooltip("Descending pressure: board shifts down one row every N shots. 0 = no pressure.")]
        public int PressureEveryShots;

        [Tooltip("Lose when bubbles reach this grid row (pressure levels only). 0 = grid default.")]
        public int DangerRow;

        public int Rows => CellRows != null ? CellRows.Length : 0;

        /// <summary>Starting bubble count (non-empty cells) — the base for star thresholds.</summary>
        public int BubbleCount
        {
            get
            {
                if (CellRows == null) return 0;
                int n = 0;
                foreach (var row in CellRows)
                {
                    if (row == null) continue;
                    foreach (char ch in row)
                        if (BubbleColorExtensions.FromChar(ch) != BubbleColor.None) n++;
                }
                return n;
            }
        }

        public BoardLayoutData() { }

        public BoardLayoutData(string name, int columns, string[] cellRows, int shots = 0,
                               int pressureEveryShots = 0, int dangerRow = 0)
        {
            Name = name;
            Columns = columns;
            CellRows = cellRows;
            Shots = shots;
            PressureEveryShots = pressureEveryShots;
            DangerRow = dangerRow;
        }

        /// <summary>Color at (col,row), or None if out of the authored range.</summary>
        public BubbleColor ColorAt(int col, int row)
        {
            if (CellRows == null || row < 0 || row >= CellRows.Length) return BubbleColor.None;
            string line = CellRows[row];
            if (line == null || col < 0 || col >= line.Length) return BubbleColor.None;
            return BubbleColorExtensions.FromChar(line[col]);
        }

        /// <summary>
        /// True if the cell starts FROZEN (ice obstacle): authored as a lowercase color
        /// char ('b' = frozen Blue). Stones can't be frozen.
        /// </summary>
        public bool FrozenAt(int col, int row)
        {
            if (CellRows == null || row < 0 || row >= CellRows.Length) return false;
            string line = CellRows[row];
            if (line == null || col < 0 || col >= line.Length) return false;
            char ch = line[col];
            return char.IsLower(ch) && BubbleColorExtensions.FromChar(ch).IsPlayable();
        }
    }

    /// <summary>
    /// ScriptableObject wrapper so levels can be authored as assets in the Project window.
    /// Right-click > Create > Coral Cascade > Board Layout. The board never references this
    /// type directly — it reads <see cref="Data"/> — keeping board logic asset-agnostic.
    /// </summary>
    [CreateAssetMenu(fileName = "BoardLayout", menuName = "Coral Cascade/Board Layout", order = 0)]
    public class BoardLayout : ScriptableObject
    {
        public BoardLayoutData Data = new BoardLayoutData();
    }
}
