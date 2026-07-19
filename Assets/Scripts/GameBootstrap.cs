using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Phase 1 entry point (Prompt 1). Constructs the entire prototype in code — camera,
    /// walls, launcher, managers and UI — so nothing depends on fragile scene wiring.
    /// Drop this component on one GameObject in a scene and press Play.
    ///
    /// Levels declare their own column count (the play area widens as levels go up), so
    /// everything width-dependent lives in <see cref="ConfigureWorld"/>, which BoardManager
    /// invokes on layout load whenever the width changes: grid+board model, walls, camera
    /// framing, launcher position.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Board")]
        public int Columns = 11;       // width of the DEFAULT world only — levels override per layout
        public int GridRows = 16;      // max rows; layouts fill the top portion
        public float BubbleDiameter = 0.72f;
        public int ShotsPerLevel = 30; // fallback only — levels carry their own budgets

        [Header("Camera")]
        // Sunlit-lagoon fallback behind the ReefBackdrop gradient (matches its deep edge).
        public Color BackgroundColor = new Color(0.09f, 0.50f, 0.70f);
        /// <summary>Fallback aspect only — real framing uses Camera.aspect (see ConfigureWorld).</summary>
        public float TargetAspect = 9f / 19.5f;

        /// <summary>Deepest row the camera must keep visible (the danger line cap).</summary>
        private const int MaxDangerRow = 14;
        /// <summary>World units kept clear below the playfield for the launcher + its queue.</summary>
        private const float LauncherRoom = 2.6f;

        private Camera _cam;
        private Transform _boardRoot, _fallRoot, _projectileRoot;
        private BoardView _boardView;
        private GridGizmoRenderer _gizmos;
        private CascadeController _cascade;
        private BoardManager _manager;
        private Launcher _launcher;
        private ReefBackdrop _backdrop;
        private readonly List<GameObject> _walls = new List<GameObject>();

        private void Awake()
        {
            // Deterministic-trajectory hygiene: a cast must never auto-hit a collider it
            // starts inside of (e.g. immediately after a wall-bounce nudge).
            Physics2D.queriesStartInColliders = false;

            // ---- Roots ----
            _boardRoot = new GameObject("BoardRoot").transform;
            _fallRoot = new GameObject("FallRoot").transform;
            _projectileRoot = new GameObject("ProjectileRoot").transform;

            // ---- Camera (portrait; framing is set per-world in ConfigureWorld) ----
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                camGo.AddComponent<AudioListener>();
                _cam = camGo.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = BackgroundColor;

            // ---- Systems & wiring ----
            var systems = new GameObject("Systems");
            _boardView = systems.AddComponent<BoardView>();
            _gizmos = systems.AddComponent<GridGizmoRenderer>();
            _cascade = systems.AddComponent<CascadeController>();
            _manager = systems.AddComponent<BoardManager>();
            var hud = systems.AddComponent<DebugHUD>();
            var flow = systems.AddComponent<GameFlow>();
            var effects = systems.AddComponent<PopEffects>();
            effects.Init(_cam);
            systems.AddComponent<Sfx>(); // synthesises its own clips on first use

            _launcher = new GameObject("Launcher").AddComponent<Launcher>();
            _backdrop = new GameObject("ReefBackdrop").AddComponent<ReefBackdrop>();

            _manager.Init(_boardView, _cascade, _launcher, _projectileRoot, BubbleDiameter, ShotsPerLevel);
            _manager.WorldRebuilder = ConfigureWorld;

            // Build the default-width world so a valid board exists behind the level-select
            // screen; loading a level rebuilds at that level's declared width.
            ConfigureWorld(Columns);

            _launcher.Init(_manager, _cam, BubbleDiameter, _launcher.transform.position);
            // Debug harness keeps just the 5 acceptance boards; the level map builds its
            // own sections (Tutorial Reef + Adventure) from LevelCatalog.
            hud.Init(_manager, _cascade, _gizmos, TestBoards.All);
            flow.Init(_manager, hud);

            Sfx.SetMusic(true); // no-ops when the player has music off

            Debug.Log("[GameBootstrap] Phase 1 prototype ready. Pick a level, drag to aim, " +
                      "release to fire. Debug harness lives behind the top bar's Debug button.");
        }

        /// <summary>
        /// (Re)builds everything that depends on the column count. Safe to call between
        /// levels: old walls are deactivated before their deferred Destroy so same-frame
        /// trajectory casts never hit ghosts, and BoardManager.LoadLayout tears down views,
        /// projectiles and cascades right after.
        /// </summary>
        private void ConfigureWorld(int columns)
        {
            foreach (var wall in _walls)
            {
                if (wall == null) continue;
                wall.SetActive(false);
                Destroy(wall);
            }
            _walls.Clear();

            float radius = BubbleDiameter * 0.5f;

            // ---- Grid origin (cell 0,0 center). Even rows centered; odd rows lean +half. ----
            float originX = -(columns - 1) * BubbleDiameter * 0.5f;
            float originY = 4.0f;
            var grid = new HexGrid(columns, GridRows, BubbleDiameter, new Vector2(originX, originY));
            var board = new Board(grid);

            // ---- Playfield extents ----
            float leftInner = originX - radius - 0.02f;
            float rightInner = originX + (columns - 1) * BubbleDiameter + 0.5f * BubbleDiameter + radius + 0.02f;
            float ceilingSurfaceY = originY + radius + 0.03f;
            float boardCenterX = (leftInner + rightInner) * 0.5f;

            // ---- Camera framing (fit the board's WIDTH and the playable HEIGHT) ----
            // Was: boardHalfWidth / TargetAspect, i.e. framed for a hardcoded 9:19.5 screen.
            // On a 21:9 phone that cropped the outer columns (the board was wider than the
            // view) and on a 4:3 tablet it left the board marooned in the middle. Framing
            // off the REAL Camera.aspect makes the width fit exactly on any device.
            float boardHalfWidth = (rightInner - leftInner) * 0.5f + 0.25f;
            float aspect = _cam.aspect > 0.01f ? _cam.aspect : TargetAspect;
            float orthoWidthFit = boardHalfWidth / aspect;

            // Reserve room for the flow top bar (THREE rows: centered level title + stat
            // chips + star meter — see GameFlow.DrawTopBar, 106px·scale) PLUS the display
            // cutout, so neither can overlap the anchor rows. 114 = 106 content + 8 margin.
            SafeAreaUtil.Refresh();
            float uiScale = Mathf.Max(1f, Screen.height / 800f);
            float topReservePx = 114f * uiScale + SafeAreaUtil.Top;
            float bottomReservePx = SafeAreaUtil.Bottom;

            // Height fit: on a wide screen, fitting the width alone can push the danger row
            // and launcher off the bottom. Everything from the ceiling down to the deepest
            // playable row (plus room for the launcher) has to survive inside the part of
            // the view the UI/system insets do NOT cover. Solved analytically because the
            // reservations are pixel heights, which depend on ortho, which is what we want.
            float topFrac = Mathf.Clamp(topReservePx / Mathf.Max(1f, Screen.height), 0f, 0.6f);
            float bottomFrac = Mathf.Clamp(bottomReservePx / Mathf.Max(1f, Screen.height), 0f, 0.2f);
            float usableFrac = Mathf.Max(0.25f, 1f - topFrac - bottomFrac);
            float deepestPlayY = grid.CellToWorld(0, Mathf.Min(GridRows - 1, MaxDangerRow)).y - radius;
            float contentHeight = (ceilingSurfaceY + 0.3f) - deepestPlayY + LauncherRoom;
            float orthoHeightFit = contentHeight / (2f * usableFrac);

            float orthoSize = Mathf.Max(orthoWidthFit, orthoHeightFit);
            float topBarWorld = topReservePx * (2f * orthoSize / Screen.height);
            float camY = ceilingSurfaceY + 0.6f + topBarWorld - orthoSize;
            float camBottom = camY - orthoSize;
            _cam.orthographicSize = orthoSize;
            _cam.transform.position = new Vector3(boardCenterX, camY, -10f);

            // ---- Physics constants derived from the camera ----
            float floorY = camBottom + 0.4f;
            float killY = camBottom - 3f;

            // ---- Walls ----
            float wallHeight = orthoSize * 2f + 2f;
            _walls.Add(CreateWall("LeftWall", new Vector2(leftInner - 0.1f, camY), new Vector2(0.2f, wallHeight), false));
            _walls.Add(CreateWall("RightWall", new Vector2(rightInner + 0.1f, camY), new Vector2(0.2f, wallHeight), false));
            _walls.Add(CreateWall("Ceiling", new Vector2(boardCenterX, ceilingSurfaceY + 0.25f),
                                  new Vector2(rightInner - leftInner + 1f, 0.5f), true));
            _walls.Add(CreateWall("Floor", new Vector2(boardCenterX, floorY),
                                  new Vector2(rightInner - leftInner + 1f, 0.3f), false));

            // ---- Rebind width-dependent systems ----
            _boardView.Init(board, _boardRoot, BubbleDiameter);
            _gizmos.Init(board);
            _cascade.Init(_manager, _fallRoot, BubbleDiameter, killY);
            _manager.Rebind(board, boardCenterX);
            _launcher.transform.position = new Vector3(boardCenterX, camBottom + 1.6f, 0f);

            // Decor last: it reads the final camera framing. Widened for wide devices.
            float viewHalfWidth = Mathf.Max(boardHalfWidth, orthoSize * _cam.aspect);
            _backdrop.Build(boardCenterX, camY + orthoSize, camBottom, viewHalfWidth, columns);
        }

        private static GameObject CreateWall(string name, Vector2 center, Vector2 size, bool isCeiling)
        {
            var go = new GameObject(name);
            go.transform.position = center;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            go.AddComponent<WallMarker>().IsCeiling = isCeiling;
            return go;
        }
    }
}
