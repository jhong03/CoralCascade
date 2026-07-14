using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Debug renderer that outlines every grid cell (occupied cells are drawn filled in
    /// their color, empty cells as faint rings) so the board structure is visible in the
    /// Scene view without final art. Toggleable from the debug HUD.
    /// </summary>
    public class GridGizmoRenderer : MonoBehaviour
    {
        public bool DrawGizmos = true;
        private Board _board;

        public void Init(Board board) => _board = board;

        private void OnDrawGizmos()
        {
            if (!DrawGizmos || _board == null) return;

            float r = _board.Grid.Radius;
            foreach (var cell in _board.AllCells)
            {
                Vector2 p = _board.Grid.CellToWorld(cell.Col, cell.Row);
                if (cell.Occupied)
                {
                    Color c = cell.Color.ToRGBA();
                    c.a = 0.35f;
                    Gizmos.color = c;
                    Gizmos.DrawSphere(p, r * 0.9f);
                }
                else
                {
                    Gizmos.color = new Color(1f, 1f, 1f, 0.08f);
                    Gizmos.DrawWireSphere(p, r * 0.85f);
                }
            }
        }
    }
}
