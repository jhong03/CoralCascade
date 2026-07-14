using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// The Phase 1 deliverable in a real sense (Prompt 8): a manual test harness that lets
    /// YOU judge whether the core loop feels right. Load any hand-authored test board, replay
    /// the last shot deterministically, A/B the physics cascade against a scripted-clear
    /// baseline, and watch the live cascade counter — all without recompiling.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        /// <summary>
        /// Hidden by default — the panel is now opened via the top bar's "Debug" button so
        /// normal play looks like the real game (level select -> clean level screen).
        /// </summary>
        public bool Visible;

        private BoardManager _manager;
        private CascadeController _cascade;
        private GridGizmoRenderer _gizmos;
        private List<BoardLayoutData> _boards;

        private GUIStyle _label;
        private GUIStyle _header;
        private bool _stylesReady;

        // Panel rect in GUI coordinates (top-left origin), updated every OnGUI.
        private static Rect _hudGuiRect;

        private Vector2 _scroll; // the panel outgrew short windows — content scrolls

        /// <summary>
        /// True if a screen-space pointer position (bottom-left origin, as reported by the
        /// Input System) is over the debug panel — used by the launcher so HUD clicks never
        /// double as aim/fire input.
        /// </summary>
        public static bool IsPointerOverHUD(Vector2 screenPos)
        {
            return _hudGuiRect.Contains(new Vector2(screenPos.x, Screen.height - screenPos.y));
        }

        public void Init(BoardManager manager, CascadeController cascade,
                         GridGizmoRenderer gizmos, List<BoardLayoutData> boards)
        {
            _manager = manager;
            _cascade = cascade;
            _gizmos = gizmos;
            _boards = boards;
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            _header = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _stylesReady = true;
        }

        private void OnGUI()
        {
            // While hidden (or under a full-screen flow modal) the panel must not exist for
            // pointer occlusion either — zero the rect so IsPointerOverHUD returns false.
            if (_manager == null || !Visible || GameFlow.ModalOpen)
            {
                _hudGuiRect = Rect.zero;
                return;
            }
            EnsureStyles();

            const float w = 260f;
            // Fixed content height (not full screen) so the panel doesn't eat aim input
            // across the whole left edge of the play area. Sits below the flow top bar.
            float top = GameFlow.TopBarHeight + 8f;
            float h = Mathf.Min(680f, Screen.height - top - 10f);
            _hudGuiRect = new Rect(10, top, w, h);
            GUILayout.BeginArea(_hudGuiRect, GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("CORAL CASCADE — Phase 1", _header);

            GUILayout.Space(4);
            GUILayout.Label("<b>Test Boards</b>", _label);
            if (_boards != null)
            {
                foreach (var b in _boards)
                {
                    if (GUILayout.Button(b.Name, GUILayout.Height(26)))
                        _manager.LoadLayout(b);
                }
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Reload Current", GUILayout.Height(26)))
                _manager.LoadLayout(_manager.CurrentLayout);
            if (GUILayout.Button("Replay Last Shot (deterministic)", GUILayout.Height(30)))
                _manager.ReplayLastShot();

            GUILayout.Space(6);
            GUILayout.Label("<b>Toggles</b>", _label);
            _cascade.PhysicsCascadeEnabled = GUILayout.Toggle(
                _cascade.PhysicsCascadeEnabled, " Physics cascade (vs scripted clear)");
            if (_gizmos != null)
                _gizmos.DrawGizmos = GUILayout.Toggle(_gizmos.DrawGizmos, " Draw grid gizmos");

            GUILayout.Space(8);
            GUILayout.Label("<b>Live</b>", _label);
            GUILayout.Label($"Cascade size: <b>{_cascade.CascadeSize}</b>", _label);
            GUILayout.Label($"Secondary chains: <b>{_cascade.SecondaryChains}</b>", _label);
            GUILayout.Label($"Cascade falling: {_cascade.IsActive}", _label);
            GUILayout.Label($"Shot in flight: {_manager.IsShotInFlight}", _label);
            GUILayout.Label($"Bubbles on board: {_manager.Board.OccupiedCount}", _label);
            GUILayout.Label($"Shots remaining: {_manager.State.ShotsRemaining}", _label);
            GUILayout.Label($"Score: <b>{_manager.Score.Total}</b>", _label);
            GUILayout.Label($"State: <b>{_manager.State.State}</b>", _label);
            GUILayout.Label($"TimeScale: {Time.timeScale:0.00}", _label);
            GUILayout.Label($"Can fire: {_manager.CanFire}", _label);

            GUILayout.Space(8);
            GUILayout.Label("Drag to aim, release to fire.", _label);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
