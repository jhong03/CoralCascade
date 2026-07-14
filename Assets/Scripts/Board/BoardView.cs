using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Visual layer for ATTACHED bubbles. Each attached cell gets a GameObject with a
    /// SpriteRenderer (primitive circle) and a STATIC CircleCollider2D (no Rigidbody2D),
    /// used both for trajectory raycasts and for physics bubbles to collide against.
    /// Detached bubbles are owned by the physics layer, not here.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        private Board _board;
        private Transform _root;
        private float _diameter;

        public void Init(Board board, Transform root, float diameter)
        {
            _board = board;
            _root = root;
            _diameter = diameter;
        }

        /// <summary>Destroys all attached-bubble views and rebuilds them from board data.</summary>
        public void RebuildAll()
        {
            for (int i = _root.childCount - 1; i >= 0; i--)
            {
                var child = _root.GetChild(i).gameObject;
                // Deactivate BEFORE the deferred Destroy: colliders must vanish THIS frame,
                // because a replay fires (and casts) in the same frame as the reload.
                child.SetActive(false);
                Destroy(child);
            }

            foreach (var cell in _board.AllCells)
            {
                cell.View = null;
                cell.PhysicsBody = null;
                if (cell.Occupied)
                    SpawnView(cell);
            }
        }

        public GameObject SpawnView(BoardCell cell)
        {
            Vector2 pos = _board.Grid.CellToWorld(cell.Col, cell.Row);
            var go = new GameObject($"Bubble_{cell.Col}_{cell.Row}");
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(_diameter, _diameter, 1f);

            BubbleArt.Apply(go, cell.Color, cell.Frozen, 10);

            // Static collider (no Rigidbody2D): used for trajectory casts and physics impacts.
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f; // local space; world radius = 0.5 * diameter

            var view = go.AddComponent<BubbleView>();
            view.Col = cell.Col;
            view.Row = cell.Row;
            view.Color = cell.Color;

            cell.View = go;
            cell.PhysicsBody = null; // a fresh attach supersedes any old handoff breadcrumb
            return go;
        }

        /// <summary>Re-applies the cell's visual (e.g. after an ice thaw removes the frost).</summary>
        public void RefreshCell(BoardCell cell)
        {
            if (cell?.View == null) return;
            BubbleArt.Apply(cell.View, cell.Color, cell.Frozen, 10);
        }
    }
}
