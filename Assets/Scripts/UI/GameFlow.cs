using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Screen flow for the prototype, mirroring the genre-standard mobile loop:
    /// level select -> level (clean HUD: top bar only) -> pause / win / lose overlays ->
    /// back to level select. The debug harness (Prompt 8) is tucked behind a "Debug" button
    /// instead of living permanently on screen. IMGUI like the rest of Phase 1 — no assets.
    ///
    /// Pause freezes the level with Time.timeScale = 0. Every moving piece (projectile,
    /// falling debris, lifetimes) runs on scaled time, so that alone is a full freeze; the
    /// only unscaled-time animation is the slow-mo beat, which checks <see cref="IsPaused"/>.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        private enum FlowScreen { LevelSelect, Playing }

        /// <summary>
        /// Pages within the LevelSelect screen (user request 2026-07-14: menu and level
        /// routes must be SEPARATE pages): Intro = the launch splash (shown ONCE at boot —
        /// nothing ever navigates back to it); Home = big section cards + daily banner;
        /// SectionMap = one section's winding route with a Back button; Reef = aquarium.
        /// </summary>
        private enum MenuPage { Intro, Home, SectionMap, Reef }

        /// <summary>
        /// One tab of the level map with its own level list and its own progression chain.
        /// The Tutorial Reef (the 5 hand-authored intro boards) and the Adventure (the main
        /// game: generated levels) are entirely separate progressions.
        /// </summary>
        private class Section
        {
            public string Key;        // Progress persistence key
            public string Title;      // tab label
            public string NodePrefix; // top-bar naming ("Intro 3" vs "Level 12")
            public List<BoardLayoutData> Levels;
            public float MapOffset = -1f; // -1 = auto-center the frontier on next draw
        }

        private static GameFlow _instance;

        private BoardManager _manager;
        private DebugHUD _debugHud;
        private Section[] _sections;
        private int _sectionIndex;
        private Section Active => _sections[_sectionIndex];
        private Section _playingSection; // the section the currently loaded level belongs to

        private FlowScreen _screen = FlowScreen.LevelSelect;
        private bool _paused;
        private float _pausedTimeScale = 1f;
        private float _pauseOpenedAt; // drives the pause panel's pop-in (unscaled: timeScale is 0)

        // End-of-level overlay appears a beat after the state flips so the player sees the
        // final cascade land instead of an instant curtain.
        private const float EndOverlayDelaySeconds = 0.8f;
        private const float SplashSeconds = 1.9f; // start-of-level target splash lifetime
        private const float PanelPopSeconds = 0.22f; // overlay panels scale in with a bubble pop
        private bool _endSeen;
        private float _endSeenAt;
        private int _finalScore;
        private bool _newBest;
        private int _earnedStars;
        private int _endShotsLeft;   // banked shots: their +50s count up on the win screen
        private float _splashUntil;  // start-of-level target splash (tap or first shot skips)
        private int _pearlsEarned;   // aquarium currency granted by this win

        private MenuPage _menuPage = MenuPage.Intro; // boot lands on the launch splash
        private float _introShownAt; // staggers the splash animation + guards instant skips

        // First-encounter mechanic tutorials (Stone / Ice / Tide): queued when a loaded
        // level contains a mechanic the player has never met, drawn as a MODAL card that
        // only the player's "Got it" dismisses — it never times out or fades on its own.
        private readonly List<string> _pendingTutorials = new List<string>();
        private float _tutorialShownAt;

        // My Reef aquarium page (roadmap step 5) — purely cosmetic, see ReefStore.
        private bool _shopOpen;
        private Vector2 _shopScroll;
        private bool _goldenUnlocked, _pearlEelUnlocked; // refreshed on tab entry

        // Daily Reef (roadmap step 4) — regenerated when the local calendar day changes.
        private BoardLayoutData _dailyLayout;
        private System.DateTime _dailyDate;

        // Decor placement: drag corals/rocks/vents along the sand (persisted per instance).
        private struct DecorHit { public Rect R; public string Id; public int Inst; }
        private readonly List<DecorHit> _decorHits = new List<DecorHit>();
        private string _placingId;   // null = not dragging decor
        private int _placingInst;
        private float _placingX01;

        // Pointer occlusion for the launcher (GUI coords, top-left origin).
        private Rect _topBarRect;
        private bool _modalOpen;

        // Touch-drag scrolling state for the map (each section keeps its own offset).
        private float _lastDragY = -1f;
        private float _dragDistance; // kept after release so a swipe can't also click a node

        // Overlay panels scroll if (and only if) they can't fit the screen.
        private Vector2 _overlayScroll;

        // ---- Sunlit-lagoon UI palette (bright + vivid; text is deep teal on light fills) ----
        private static readonly Color DeepTeal = new Color(0.02f, 0.26f, 0.36f);
        private static readonly Color SoftTeal = new Color(0.04f, 0.35f, 0.47f, 0.92f);
        private static readonly Color Coral = new Color(0.98f, 0.45f, 0.30f);
        private static readonly Color CoralDark = new Color(0.85f, 0.33f, 0.20f);
        private static readonly Color Sunshine = new Color(1f, 0.80f, 0.25f);
        private static readonly Color PanelFill = new Color(0.93f, 0.99f, 1f, 0.97f);
        private static readonly Color MapTop = new Color(0.62f, 0.90f, 0.97f);
        private static readonly Color MapBottom = new Color(0.13f, 0.56f, 0.75f);
        private static readonly Color StarOff = new Color(0f, 0.30f, 0.40f, 0.22f);

        private Texture2D _whiteTex;
        private Texture2D _mapGradientTex;
        private Texture2D _barShadowTex; // soft drop shadow under the in-level top bar
        private GUIStyle _titleStyle, _subtitleStyle, _buttonStyle, _barLabelStyle, _overlayTitleStyle;
        private GUIStyle _nodeStyle, _nodeBestStyle, _tabStyle, _tabActiveStyle, _panelStyle;
        private GUIStyle _meterLabelStyle, _shopSmallStyle, _cardStyle, _cardActiveStyle;
        private GUIStyle _chipStyle, _chipUrgentStyle, _nodeTextStyle, _chevronStyle, _rowStyle;
        private GUIStyle _buyStyle, _sellStyle, _shopNameStyle;
        private GUIStyle _introTitleStyle, _introPromptStyle;
        private GUIStyle _tutTitleStyle, _tutBodyStyle;
        private float _scale;
        private bool _stylesReady;
        private Matrix4x4 _panelMatrix; // saved by BeginPanel (pop-in scale), restored by EndPanel

        // Imported skin (Kenney UI Pack, CC0): display + narrow fonts and 9-slice button
        // plates. ALL null-safe — the procedural skin is the fallback when the assets
        // haven't been imported (the game must never require imported art to run).
        private bool _skinProbed;
        private Font _fontDisplay, _fontBody;
        private Texture2D _btnRedUp, _btnRedDown, _btnYellowUp, _btnYellowDown, _btnGreyFlat;

        // Menu page transition: the incoming page slides up + fades for a beat.
        private MenuPage _lastDrawnPage = (MenuPage)(-1);
        private float _pageShownAt;

        // Star-target meter state: pips pulse the moment their score target is crossed.
        private int _meterLastScore;
        private bool _pip2Lit, _pip3Lit;
        private float _pip2LitAt, _pip3LitAt;

        /// <summary>True while the player is inside a level and not paused (aim/fire allowed).</summary>
        public static bool GameplayActive =>
            _instance == null || (_instance._screen == FlowScreen.Playing && !_instance._paused);

        /// <summary>True while the pause overlay freezes the level (Time.timeScale = 0).</summary>
        public static bool IsPaused => _instance != null && _instance._paused;

        /// <summary>True while a full-screen UI (level select / pause / end) owns the pointer.</summary>
        public static bool ModalOpen => _instance != null && _instance._modalOpen;

        /// <summary>GUI-space height of the in-level top bar (so other panels can sit below it).</summary>
        public static float TopBarHeight => _instance == null ? 0f : _instance._topBarRect.height;

        /// <summary>
        /// True if a screen-space pointer position (bottom-left origin, as reported by the
        /// Input System) is over flow UI — the top bar, or anywhere while a modal is open.
        /// </summary>
        public static bool IsPointerOverUI(Vector2 screenPos)
        {
            if (_instance == null) return false;
            if (_instance._modalOpen) return true;
            return _instance._topBarRect.Contains(new Vector2(screenPos.x, Screen.height - screenPos.y));
        }

        public void Init(BoardManager manager, DebugHUD debugHud)
        {
            _instance = this;
            _manager = manager;
            _debugHud = debugHud;
            _sections = new[]
            {
                new Section { Key = "Intro", Title = "Tutorial Reef", NodePrefix = "Intro", Levels = LevelCatalog.Intro },
                new Section { Key = "Main",  Title = "Adventure",     NodePrefix = "Level", Levels = LevelCatalog.Main },
            };
            // Open on the tutorial until it's finished, then default to the main game.
            bool introDone = Progress.HighestUnlocked("Intro") > _sections[0].Levels.Count;
            _sectionIndex = introDone ? 1 : 0;
            _playingSection = _sections[_sectionIndex];
            _introShownAt = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_manager == null) return;
            if (_screen != FlowScreen.Playing)
            {
                _endSeen = false;
                // Any tap leaves the launch splash (after a short guard so the tap that
                // launched the app can't skip it before it's even been seen).
                if (_menuPage == MenuPage.Intro && PointerInput.PressedThisFrame &&
                    Time.unscaledTime - _introShownAt > 0.4f)
                    _menuPage = MenuPage.Home;
                HandleMapDrag();
                HandleDecorPlacement();
                return;
            }
            if (_splashUntil > 0f && PointerInput.PressedThisFrame)
                _splashUntil = 0f; // first touch dismisses the target splash

            bool over = _manager.State.State != GameState.Playing;
            if (over && !_endSeen)
            {
                _endSeen = true;
                _endSeenAt = Time.unscaledTime;
                _overlayScroll = Vector2.zero;
                _finalScore = _manager.Score.Total;
                bool won = _manager.State.State == GameState.Won;
                _endShotsLeft = won ? _manager.State.ShotsRemaining : 0;
                if (won)
                {
                    int idx = CurrentLevelIndex();
                    if (idx >= 0) Progress.MarkCleared(_playingSection.Key, idx + 1); // unlock next node
                    _earnedStars = Stars.Compute(_manager.CurrentLayout, _finalScore);
                    if (_manager.CurrentLayout != null)
                        Stars.Submit(_manager.CurrentLayout.Name, _earnedStars);
                }
                // First-clear must be read BEFORE the submit below records a best score.
                bool firstClear = won && _manager.CurrentLayout != null &&
                                  HighScores.Get(_manager.CurrentLayout.Name) == 0;
                // Only a CLEARED level can set a best score — losing banks nothing.
                _newBest = won && _manager.CurrentLayout != null &&
                           HighScores.Submit(_manager.CurrentLayout.Name, _finalScore);

                // Aquarium currency — wins only (purely cosmetic economy, see ReefStore).
                _pearlsEarned = 0;
                if (won)
                {
                    _pearlsEarned = Pearls.WinBase + Pearls.PerStar * _earnedStars
                                  + (firstClear ? Pearls.FirstClearBonus : 0)
                                  + (_newBest ? Pearls.NewBestBonus : 0);
                    Pearls.Add(_pearlsEarned);
                }
            }
            else if (!over)
            {
                _endSeen = false; // board was reloaded (debug panel) while an overlay was pending
            }
        }

        /// <summary>
        /// Drag-to-place for aquarium decor (plants/rocks/vents — fish swim freely). Press
        /// on a piece grabs it, dragging slides it along the sand, release persists. Uses
        /// the hit rects recorded by the previous frame's draw.
        /// </summary>
        private void HandleDecorPlacement()
        {
            if (_menuPage != MenuPage.Reef || _shopOpen)
            {
                _placingId = null;
                return;
            }

            Vector2 p = PointerInput.ScreenPosition;
            var gui = new Vector2(p.x, Screen.height - p.y); // GUI space is top-left origin

            if (PointerInput.PressedThisFrame)
            {
                for (int i = _decorHits.Count - 1; i >= 0; i--) // topmost = drawn last
                {
                    if (!_decorHits[i].R.Contains(gui)) continue;
                    _placingId = _decorHits[i].Id;
                    _placingInst = _decorHits[i].Inst;
                    _placingX01 = ReefStore.GetX(_placingId, _placingInst);
                    break;
                }
            }
            else if (_placingId != null && PointerInput.IsPressed)
            {
                // Same margins as the tank's draw-time Lerp — keep the two in sync.
                float left = 30f * _scale, right = Screen.width - 30f * _scale;
                _placingX01 = Mathf.Clamp01((gui.x - left) / Mathf.Max(1f, right - left));
            }
            else if (_placingId != null)
            {
                ReefStore.SetX(_placingId, _placingInst, _placingX01); // release → persist
                _placingId = null;
            }
        }

        /// <summary>Touch-drag scrolling for the map (IMGUI has no native swipe scroll).</summary>
        private void HandleMapDrag()
        {
            if (PointerInput.PressedThisFrame)
            {
                _dragDistance = 0f;
                _lastDragY = PointerInput.ScreenPosition.y;
            }
            else if (PointerInput.IsPressed && _lastDragY >= 0f)
            {
                float y = PointerInput.ScreenPosition.y;
                float dy = y - _lastDragY; // finger up → reveal higher levels
                if (_menuPage == MenuPage.Reef)
                {
                    if (_shopOpen) _shopScroll.y += dy; // shop closed → drags place decor instead
                }
                else if (_menuPage == MenuPage.SectionMap && _sections != null && Active.MapOffset >= 0f)
                {
                    // Floor at 0: MapOffset < 0 is the "auto-center the frontier" SENTINEL —
                    // dragging past the bottom must never trip it (it read as an endless
                    // scroll loop: hit level 1 → snap back to the frontier → repeat).
                    Active.MapOffset = Mathf.Max(0f, Active.MapOffset + dy);
                }
                _dragDistance += Mathf.Abs(dy);
                _lastDragY = y;
            }
            else if (!PointerInput.IsPressed)
            {
                _lastDragY = -1f; // _dragDistance intentionally survives the release frame
            }
        }

        // ---- Flow actions ------------------------------------------------------------------

        private void StartLevel(Section section, BoardLayoutData level)
        {
            _playingSection = section;
            _paused = false;
            _endSeen = false;
            _manager.LoadLayout(level); // resets timescale, cascade, projectiles
            QueueMechanicTutorials(level);
            // The star-target splash waits its turn: it starts when the tutorial closes.
            _splashUntil = _pendingTutorials.Count > 0 ? 0f : Time.unscaledTime + SplashSeconds;
            _screen = FlowScreen.Playing;
        }

        /// <summary>
        /// Detects mechanics in the LOADED LEVEL DATA that the player has never met and
        /// queues their tutorial cards. Data-driven (chars + pressure field), so it works
        /// identically for authored intros, generated reefs and Dailies.
        /// </summary>
        private void QueueMechanicTutorials(BoardLayoutData layout)
        {
            _pendingTutorials.Clear();
            if (layout == null) return;

            bool stone = false, ice = false;
            if (layout.CellRows != null)
            {
                foreach (var row in layout.CellRows)
                {
                    if (row == null) continue;
                    foreach (char ch in row)
                    {
                        if (BubbleColorExtensions.FromChar(ch) == BubbleColor.Stone) stone = true;
                        else if (char.IsLower(ch) && BubbleColorExtensions.FromChar(ch).IsPlayable()) ice = true;
                    }
                }
            }
            if (stone && !TutorialFlags.Seen("Stone")) _pendingTutorials.Add("Stone");
            if (ice && !TutorialFlags.Seen("Ice")) _pendingTutorials.Add("Ice");
            if (layout.PressureEveryShots > 0 && !TutorialFlags.Seen("Tide")) _pendingTutorials.Add("Tide");
            if (_pendingTutorials.Count > 0) _tutorialShownAt = Time.unscaledTime;
        }

        private void Pause()
        {
            if (_paused) return;
            _paused = true;
            _pauseOpenedAt = Time.unscaledTime;
            _overlayScroll = Vector2.zero;
            _pausedTimeScale = Time.timeScale; // may be mid slow-mo; restored verbatim
            Time.timeScale = 0f;
        }

        private void Resume()
        {
            if (!_paused) return;
            _paused = false;
            Time.timeScale = _pausedTimeScale;
        }

        private void RestartLevel()
        {
            _paused = false;
            _endSeen = false;
            _manager.LoadLayout(_manager.CurrentLayout); // sets timescale back to 1 itself
            QueueMechanicTutorials(_manager.CurrentLayout); // no-op once flags are seen
            _splashUntil = _pendingTutorials.Count > 0 ? 0f : Time.unscaledTime + SplashSeconds;
        }

        /// <summary>Back to the map, landing on the given section's tab (default: the one just played).</summary>
        private void QuitToLevelSelect(int sectionIndex = -1)
        {
            _paused = false;
            _endSeen = false;
            // Reload (rather than leave) the board so projectiles/cascades/slow-mo are all
            // torn down while the menu covers the table.
            if (_manager.CurrentLayout != null)
                _manager.LoadLayout(_manager.CurrentLayout);
            if (_debugHud != null) _debugHud.Visible = false;
            if (sectionIndex >= 0)
                _sectionIndex = sectionIndex;
            else
            {
                int played = System.Array.IndexOf(_sections, _playingSection);
                if (played >= 0) _sectionIndex = played;
            }
            Active.MapOffset = -1f;             // re-center the map on the frontier
            _menuPage = MenuPage.SectionMap;    // back from a level lands on its route page
            _lastDrawnPage = (MenuPage)(-1);    // replay the page slide-in on return
            _screen = FlowScreen.LevelSelect;
        }

        private int CurrentLevelIndex()
        {
            var cur = _manager.CurrentLayout;
            if (cur == null || _playingSection == null) return -1;
            var levels = _playingSection.Levels;
            for (int i = 0; i < levels.Count; i++)
                if (ReferenceEquals(levels[i], cur) || levels[i].Name == cur.Name)
                    return i;
            return -1; // e.g. an acceptance board loaded via the debug panel mid-Adventure
        }

        // ---- GUI -----------------------------------------------------------------------------

        private void EnsureStyles()
        {
            float scale = Mathf.Max(1f, Screen.height / 800f);
            if (_stylesReady && Mathf.Abs(scale - _scale) < 0.01f) return;
            _scale = scale;

            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
            if (_mapGradientTex == null)
                _mapGradientTex = PrimitiveSprites.GradientTexture(MapTop, MapBottom);
            if (_barShadowTex == null)
                _barShadowTex = PrimitiveSprites.GradientTexture(new Color(0f, 0.10f, 0.18f, 0.16f),
                                                                 new Color(0f, 0.10f, 0.18f, 0f));

            if (!_skinProbed)
            {
                // One probe per domain load; a Unity import triggers a domain reload, so a
                // late import still lands on the next Play. Missing assets = null = fallback.
                _skinProbed = true;
                _fontDisplay = Resources.Load<Font>("Fonts/KenneyFuture");
                _fontBody = Resources.Load<Font>("Fonts/KenneyFutureNarrow");
                _btnRedUp = Resources.Load<Texture2D>("Art/UI/button_red_depth");
                _btnRedDown = Resources.Load<Texture2D>("Art/UI/button_red_flat");
                _btnYellowUp = Resources.Load<Texture2D>("Art/UI/button_yellow_depth");
                _btnYellowDown = Resources.Load<Texture2D>("Art/UI/button_yellow_flat");
                _btnGreyFlat = Resources.Load<Texture2D>("Art/UI/button_grey_flat");
            }
            if (_fontBody == null) _fontBody = _fontDisplay; // narrow variant optional

            var border = new RectOffset(20, 20, 20, 20); // matches RoundedRect's 9-slice corners
            // Pack plates are 192x64 (1x): corner radius ~13px, depth lip ~8px. The border
            // grows with UI scale for chunkier corners but stays inside the texture halves,
            // and its vertical sum stays under the smallest button height (38*scale).
            var plateBorder = new RectOffset(
                (int)Mathf.Clamp(14f * _scale, 14f, 60f), (int)Mathf.Clamp(14f * _scale, 14f, 60f),
                (int)Mathf.Clamp(14f * _scale, 14f, 30f), (int)Mathf.Clamp(22f * _scale, 22f, 31f));

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(34 * _scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = DeepTeal;
            if (_fontDisplay != null) { _titleStyle.font = _fontDisplay; _titleStyle.fontStyle = FontStyle.Normal; }
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(15 * _scale),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true // long lines wrap; panels must CalcHeight, never hardcode
            };
            _subtitleStyle.normal.textColor = SoftTeal;
            if (_fontBody != null) _subtitleStyle.font = _fontBody;
            _overlayTitleStyle = new GUIStyle(_titleStyle) { fontSize = (int)(28 * _scale) };

            // Chunky coral buttons: fills are BAKED into the textures (no GUI.backgroundColor
            // games), white bold text in every state. With the Kenney UI Pack imported the
            // plates are the pack's depth buttons (pressed = flat plate, visually pushed
            // down); otherwise the procedural shaded rounded rect stands in.
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = (int)(18 * _scale),
                fontStyle = FontStyle.Bold,
                border = border
            };
            if (_btnRedUp != null && _btnRedDown != null)
            {
                _buttonStyle.border = plateBorder;
                _buttonStyle.normal.background = _btnRedUp;
                _buttonStyle.hover.background = _btnRedUp;
                _buttonStyle.active.background = _btnRedDown;
            }
            else
            {
                _buttonStyle.normal.background = PrimitiveSprites.RoundedRectShaded(Coral);
                _buttonStyle.hover.background = PrimitiveSprites.RoundedRectShaded(Coral);
                _buttonStyle.active.background = PrimitiveSprites.RoundedRectShaded(CoralDark);
            }
            _buttonStyle.focused.background = _buttonStyle.normal.background;
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.textColor = Color.white;
            _buttonStyle.active.textColor = Color.white;
            _buttonStyle.focused.textColor = Color.white;
            if (_fontDisplay != null) { _buttonStyle.font = _fontDisplay; _buttonStyle.fontStyle = FontStyle.Normal; }
            // Real padding keeps text off the rounded corners AND makes CalcSize honest —
            // fixed-width buttons are sized via ButtonW, never by eyeballed constants
            // (Kenney Future runs wider than the default font; "Close Shop" clipped).
            // The bottom pad lifts text onto the depth plate's face, above its lip.
            _buttonStyle.padding = new RectOffset((int)(16 * _scale), (int)(16 * _scale), 0,
                _btnRedUp != null ? (int)(5 * _scale) : 0);

            _tabStyle = new GUIStyle(_buttonStyle);
            _tabStyle.border = border;
            _tabStyle.fontSize = (int)(16 * _scale); // secondary actions sit a step down
            _tabStyle.padding = new RectOffset((int)(16 * _scale), (int)(16 * _scale), 0, 0);
            _tabStyle.normal.background = _btnGreyFlat != null
                ? _btnGreyFlat
                : PrimitiveSprites.RoundedRect(new Color(1f, 1f, 1f, 0.38f));
            if (_btnGreyFlat != null) _tabStyle.border = plateBorder;
            _tabStyle.hover.background = _tabStyle.normal.background;
            _tabStyle.active.background = _tabStyle.normal.background;
            _tabStyle.focused.background = _tabStyle.normal.background;
            _tabStyle.normal.textColor = DeepTeal;
            _tabStyle.hover.textColor = DeepTeal;
            _tabStyle.active.textColor = DeepTeal;
            _tabStyle.focused.textColor = DeepTeal;

            _tabActiveStyle = new GUIStyle(_tabStyle);
            if (_btnYellowUp != null && _btnYellowDown != null)
            {
                _tabActiveStyle.border = plateBorder;
                _tabActiveStyle.padding = new RectOffset((int)(16 * _scale), (int)(16 * _scale),
                                                         0, (int)(5 * _scale)); // depth plate lip
                _tabActiveStyle.normal.background = _btnYellowUp;
                _tabActiveStyle.active.background = _btnYellowDown;
                _tabActiveStyle.hover.background = _btnYellowUp;
                _tabActiveStyle.focused.background = _btnYellowUp;
            }
            else
            {
                _tabActiveStyle.normal.background = PrimitiveSprites.RoundedRectShaded(Sunshine);
                _tabActiveStyle.hover.background = _tabActiveStyle.normal.background;
                _tabActiveStyle.active.background = _tabActiveStyle.normal.background;
                _tabActiveStyle.focused.background = _tabActiveStyle.normal.background;
            }

            _barLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(16 * _scale),
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                wordWrap = false // width-budgeted labels CLIP, never wrap char-by-char
            };
            _barLabelStyle.normal.textColor = DeepTeal;
            if (_fontBody != null) _barLabelStyle.font = _fontBody;

            // Map nodes are bubbles: the button background is the shaded orb texture
            // (tint comes via GUI.backgroundColor at the draw site — baked shading makes
            // the nodes read as balls, matching the in-game glossy orbs).
            var circleTex = PrimitiveSprites.GlossyOrb().texture;
            _nodeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = (int)(22 * _scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(0, 0, 0, 0)
            };
            _nodeStyle.normal.background = circleTex;
            _nodeStyle.hover.background = circleTex;
            _nodeStyle.active.background = circleTex;
            _nodeStyle.focused.background = circleTex;
            _nodeStyle.normal.textColor = Color.white;
            _nodeStyle.hover.textColor = Color.white;
            _nodeStyle.active.textColor = Color.white;
            _nodeStyle.focused.textColor = Color.white;

            _nodeBestStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(12 * _scale),
                alignment = TextAnchor.UpperCenter
            };
            _nodeBestStyle.normal.textColor = DeepTeal;
            if (_fontBody != null) _nodeBestStyle.font = _fontBody;

            // Node numbers are drawn as a separate shadowed label ON TOP of the orb button
            // (labels never eat clicks) so they stay readable on every node tint.
            _nodeTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(22 * _scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _nodeTextStyle.normal.textColor = Color.white;
            if (_fontDisplay != null) { _nodeTextStyle.font = _fontDisplay; _nodeTextStyle.fontStyle = FontStyle.Normal; }

            // Top-bar stat chips: translucent white pills, deep-teal text; the urgent
            // variant (tide about to drop) flips to solid coral with white text.
            // Sized TIGHT: a 1080-wide portrait phone is only ~370 logical points across,
            // and the whole HUD row must fit inside that.
            float chipH = 28f * _scale;
            _chipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(13 * _scale),
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                border = border,
                padding = new RectOffset((int)(9 * _scale), (int)(9 * _scale), 0, 0),
                margin = new RectOffset(0, 0, (int)(12 * _scale), 0), // centers in the 52px row
                fixedHeight = chipH
            };
            _chipStyle.normal.background = PrimitiveSprites.RoundedRect(new Color(1f, 1f, 1f, 0.55f));
            _chipStyle.normal.textColor = DeepTeal;
            if (_fontBody != null) _chipStyle.font = _fontBody;
            _chipUrgentStyle = new GUIStyle(_chipStyle);
            _chipUrgentStyle.normal.background = PrimitiveSprites.RoundedRectShaded(Coral);
            _chipUrgentStyle.normal.textColor = Color.white;

            // Home-card chevron ("more inside" affordance) — plain ASCII, no glyph risk.
            _chevronStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(26 * _scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _chevronStyle.normal.textColor = SoftTeal;
            if (_fontDisplay != null) _chevronStyle.font = _fontDisplay;

            // Shop rows sit on their own translucent card so the list reads as items,
            // not a wall of text.
            _rowStyle = new GUIStyle
            {
                border = border,
                padding = new RectOffset((int)(8 * _scale), (int)(8 * _scale),
                                         (int)(5 * _scale), (int)(5 * _scale)),
                margin = new RectOffset(0, (int)(4 * _scale), 0, 0)
            };
            _rowStyle.normal.background = PrimitiveSprites.RoundedRect(new Color(1f, 1f, 1f, 0.50f));

            // Compact shop-row buttons: smaller type + tighter padding than the main
            // buttons, so the item-name column keeps real width on narrow screens.
            _buyStyle = new GUIStyle(_buttonStyle)
            {
                fontSize = (int)(15 * _scale),
                padding = new RectOffset((int)(10 * _scale), (int)(10 * _scale), 0,
                                         _btnRedUp != null ? (int)(4 * _scale) : 0)
            };
            _sellStyle = new GUIStyle(_tabStyle)
            {
                fontSize = (int)(15 * _scale),
                padding = new RectOffset((int)(10 * _scale), (int)(10 * _scale), 0, 0)
            };
            // A step under _barLabelStyle so the longest names fit the tightest column.
            _shopNameStyle = new GUIStyle(_barLabelStyle) { fontSize = (int)(14 * _scale) };

            // Launch splash: an oversized title and a white tap prompt (the prompt sits on
            // the DEEP end of the gradient, where deep-teal text would drown).
            _introTitleStyle = new GUIStyle(_titleStyle) { fontSize = (int)(46 * _scale) };
            _introPromptStyle = new GUIStyle(_subtitleStyle)
            {
                fontSize = (int)(20 * _scale),
                fontStyle = FontStyle.Bold
            };
            _introPromptStyle.normal.textColor = Color.white;
            if (_fontDisplay != null) { _introPromptStyle.font = _fontDisplay; _introPromptStyle.fontStyle = FontStyle.Normal; }

            // Mechanic-tutorial card: bold section titles, wrapped body copy, all left-set.
            _tutTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(17 * _scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _tutTitleStyle.normal.textColor = DeepTeal;
            if (_fontDisplay != null) { _tutTitleStyle.font = _fontDisplay; _tutTitleStyle.fontStyle = FontStyle.Normal; }
            _tutBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(13 * _scale),
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _tutBodyStyle.normal.textColor = SoftTeal;
            if (_fontBody != null) _tutBodyStyle.font = _fontBody;

            _meterLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(13 * _scale),
                alignment = TextAnchor.MiddleRight
            };
            _meterLabelStyle.normal.textColor = DeepTeal;
            if (_fontBody != null) _meterLabelStyle.font = _fontBody;

            _shopSmallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(12 * _scale),
                alignment = TextAnchor.MiddleLeft
            };
            _shopSmallStyle.normal.textColor = SoftTeal;
            if (_fontBody != null) _shopSmallStyle.font = _fontBody;

            // Home-page section cards: two-line rich text ("<b>Title</b>\nprogress"),
            // left-aligned like a real list card, right padding reserves the chevron slot.
            _cardStyle = new GUIStyle(_tabStyle)
            {
                fontSize = (int)(17 * _scale),
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset((int)(22 * _scale), (int)(48 * _scale), 0, 0)
            };
            _cardActiveStyle = new GUIStyle(_tabActiveStyle)
            {
                fontSize = (int)(17 * _scale),
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset((int)(22 * _scale), (int)(48 * _scale), 0, 0)
            };

            // Light rounded panel for pause/win/lose (text on it is deep teal); the white
            // outline ring lifts it off the teal scrim like a bubble card.
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                border = border,
                padding = new RectOffset((int)(22 * _scale), (int)(22 * _scale),
                                         (int)(18 * _scale), (int)(18 * _scale))
            };
            _panelStyle.normal.background = PrimitiveSprites.RoundedRectOutlined(PanelFill, Color.white);

            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (_manager == null) return;
            EnsureStyles();

            _modalOpen = false;
            _topBarRect = default;

            if (_screen == FlowScreen.LevelSelect)
            {
                DrawLevelSelect();
                return;
            }

            // Bullet-time tint: makes the slow-mo beat unmistakable (pause is timeScale 0,
            // but it's excluded explicitly so the pause menu never tints).
            if (!_paused && Time.timeScale < 0.85f)
                FillScreen(new Color(0.45f, 0.85f, 1f, 0.13f));

            DrawTopBar();

            bool tutorialOpen = _pendingTutorials.Count > 0 && !_paused && !_endSeen;
            if (tutorialOpen)
                DrawMechanicTutorial();

            if (!_paused && !_endSeen && !tutorialOpen && Time.unscaledTime < _splashUntil)
                DrawTargetSplash();

            if (_paused)
                DrawPauseOverlay();
            else if (_endSeen && Time.unscaledTime - _endSeenAt >= EndOverlayDelaySeconds)
                DrawEndOverlay();
        }

        private void FillScreen(Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whiteTex);
            GUI.color = old;
        }

        /// <summary>Soft god-rays fanning down from the surface — pure GUI candy.</summary>
        private void DrawSunRays()
        {
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var pivot = new Vector2(Screen.width * 0.5f, -40f);
            float w = Screen.width * 0.16f;
            float h = Screen.height * 0.85f;
            float[] angles = { -16f, 2f, 14f };
            GUI.color = new Color(1f, 1f, 0.85f, 0.07f);
            foreach (float angle in angles)
            {
                GUI.matrix = Matrix4x4.identity;
                GUIUtility.RotateAroundPivot(angle, pivot);
                GUI.DrawTexture(new Rect(pivot.x - w * 0.5f, 0f, w, h), _whiteTex);
            }
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private Rect CenteredColumn(float width, float height)
        {
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        /// <summary>
        /// Width that fits <paramref name="text"/> in a button style, never below
        /// <paramref name="minW"/>. Fixed-width buttons MUST size through this — hardcoded
        /// widths clip when the font changes (the "Close Shop" lesson).
        /// </summary>
        private float ButtonW(string text, GUIStyle style, float minW = 0f)
        {
            return Mathf.Max(minW, style.CalcSize(new GUIContent(text)).x + 12f * _scale);
        }

        /// <summary>Ease-out with a soft overshoot — the standard bubbly pop-in curve.</summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        /// <summary>
        /// Label with a soft drop shadow (drawn twice) — the only way IMGUI text gets any
        /// depth on the bright water backdrop. Mutates-and-restores the style's text color.
        /// </summary>
        private void DrawLabelShadowed(Rect r, string text, GUIStyle style)
        {
            float off = 1.5f * _scale;
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0.12f, 0.20f, 0.35f);
            GUI.Label(new Rect(r.x, r.y + off, r.width, r.height), text, style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
        }

        // ---- Overlay panel plumbing ----------------------------------------------------------
        // Heights are estimates, so the panel budgets for its own padding plus slack, clamps
        // to the screen, and wraps content in a scroll view (inert while everything fits).
        // openT < 1 scales the panel in around the screen center (pop-in); EndPanel restores
        // the matrix, so every early-return path keeps its existing "call EndPanel" contract.

        private void BeginPanel(float width, float contentHeight, float openT = 1f)
        {
            _panelMatrix = GUI.matrix;
            if (openT < 1f)
            {
                float s = Mathf.Max(0.01f, EaseOutBack(Mathf.Clamp01(openT)));
                GUIUtility.ScaleAroundPivot(new Vector2(s, s),
                    new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            }
            float h = contentHeight + _panelStyle.padding.top + _panelStyle.padding.bottom + 10f * _scale;
            h = Mathf.Min(h, Screen.height - 30f);
            GUILayout.BeginArea(CenteredColumn(width, h), _panelStyle);
            _overlayScroll = GUILayout.BeginScrollView(_overlayScroll);
        }

        private void EndPanel()
        {
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = _panelMatrix;
        }

        /// <summary>
        /// Draws a centered row of 3 star pips (gold = earned, faint = not). Uses the
        /// procedural star sprite — real star shapes, still no font glyph dependence.
        /// </summary>
        private void DrawStars(Rect area, int stars, float size)
        {
            Texture tex = PrimitiveSprites.Star().texture;
            float gap = size * 0.3f;
            float x = area.x + (area.width - (3f * size + 2f * gap)) * 0.5f;
            float y = area.y + (area.height - size) * 0.5f;
            var old = GUI.color;
            for (int i = 0; i < 3; i++)
            {
                GUI.color = i < stars ? new Color(1f, 0.8f, 0.15f) : StarOff;
                GUI.DrawTexture(new Rect(x + i * (size + gap), y, size, size), tex);
            }
            GUI.color = old;
        }

        /// <summary>
        /// Candy-crush-style level map: a winding path of numbered nodes, level 1 at the
        /// bottom, swipe up to see later levels. Locked nodes are grey and disabled; the
        /// frontier (furthest unlocked, uncleared) glows; cleared nodes are green and show
        /// their best score. Tap an unlocked node to play it.
        /// </summary>
        private void DrawLevelSelect()
        {
            _modalOpen = true;
            // Sunlit lagoon backdrop (opaque: fully covers the table) + soft god-rays.
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _mapGradientTex,
                            ScaleMode.StretchToFill);
            DrawSunRays();

            // Page transition: the incoming page slides up out of the water and fades in.
            // Only the CONTENT animates — the backdrop above stays put, so it reads as
            // pages moving over the lagoon, not the world lurching.
            if (_menuPage != _lastDrawnPage)
            {
                _lastDrawnPage = _menuPage;
                _pageShownAt = Time.unscaledTime;
            }
            float pageT = Mathf.Clamp01((Time.unscaledTime - _pageShownAt) / 0.28f);
            var pageMatrix = GUI.matrix;
            var pageColor = GUI.color;
            if (pageT < 1f)
            {
                GUI.matrix = Matrix4x4.Translate(
                    new Vector3(0f, (1f - EaseOutBack(pageT)) * 30f * _scale, 0f)) * GUI.matrix;
                GUI.color = new Color(1f, 1f, 1f, 0.25f + 0.75f * pageT);
            }
            if (_menuPage == MenuPage.Intro) DrawIntroPage();
            else if (_menuPage == MenuPage.Home) DrawHomePage();
            else if (_menuPage == MenuPage.Reef) DrawReefPage();
            else DrawSectionMapPage();
            GUI.matrix = pageMatrix;
            GUI.color = pageColor;
        }

        /// <summary>The section route page: Back + section title, then the winding path.</summary>
        private void DrawSectionMapPage()
        {
            if (DrawBackButton())
                return;
            DrawLabelShadowed(new Rect(0, 12f * _scale, Screen.width, 40f * _scale), Active.Title, _titleStyle);
            DrawPearlChip();

            float mapTop = 68f * _scale;
            var mapRect = new Rect(0, mapTop, Screen.width, Screen.height - mapTop);
            float spacing = 112f * _scale;
            float nodeSize = 68f * _scale;
            float basePad = 70f * _scale;
            var section = Active;
            int n = section.Levels.Count;
            float contentH = basePad + (n - 1) * spacing + nodeSize;
            float maxOffset = Mathf.Max(0f, contentH - mapRect.height);

            // Mouse-wheel support for the editor; swiping is handled in HandleMapDrag.
            // Same 0-floor as the drag path: never wheel into the auto-center sentinel.
            if (Event.current.type == EventType.ScrollWheel && section.MapOffset >= 0f)
            {
                section.MapOffset = Mathf.Max(0f, section.MapOffset + Event.current.delta.y * 24f * _scale);
                Event.current.Use();
            }

            if (section.MapOffset < 0f)
            {
                // First open (or back from a level): center the frontier node.
                int target = Mathf.Clamp(Progress.HighestUnlocked(section.Key) - 1, 0, n - 1);
                section.MapOffset = Mathf.Clamp(target * spacing - mapRect.height * 0.45f, 0f, maxOffset);
            }
            section.MapOffset = Mathf.Clamp(section.MapOffset, 0f, maxOffset);

            bool swiping = _dragDistance > 15f; // a swipe must not double as a node tap

            GUI.BeginGroup(mapRect);

            Vector2 NodeCenter(int i) => new Vector2(
                mapRect.width * 0.5f + Mathf.Sin(i * 0.85f) * mapRect.width * 0.28f,
                mapRect.height - basePad - i * spacing + section.MapOffset);

            // Path dots first, so nodes draw on top — little bubbles along the reef path.
            var circleTex = PrimitiveSprites.Circle().texture;
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            float dot = 9f * _scale;
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 a = NodeCenter(i), b = NodeCenter(i + 1);
                if ((a.y < -spacing && b.y < -spacing) || (a.y > mapRect.height + spacing && b.y > mapRect.height + spacing))
                    continue;
                for (int d = 1; d <= 3; d++)
                {
                    Vector2 p = Vector2.Lerp(a, b, d / 4f);
                    GUI.DrawTexture(new Rect(p.x - dot * 0.5f, p.y - dot * 0.5f, dot, dot), circleTex);
                }
            }
            GUI.color = oldColor;

            var oldBg = GUI.backgroundColor;
            for (int i = 0; i < n; i++)
            {
                Vector2 c = NodeCenter(i);
                if (c.y < -nodeSize || c.y > mapRect.height + nodeSize) continue;

                int levelNumber = i + 1;
                bool unlocked = Progress.IsUnlocked(section.Key, levelNumber);
                int best = HighScores.Get(section.Levels[i].Name);
                bool cleared = best > 0;
                bool frontier = unlocked && !cleared && levelNumber == Progress.HighestUnlocked(section.Key);

                GUI.backgroundColor = cleared ? new Color(0.30f, 0.90f, 0.50f)
                                    : frontier ? Sunshine
                                    : unlocked ? new Color(0.35f, 0.78f, 1f)
                                    : new Color(0.72f, 0.84f, 0.90f);
                GUI.enabled = unlocked && !swiping;

                // The frontier node breathes so the eye lands on "play this next".
                float size = frontier
                    ? nodeSize * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f))
                    : nodeSize;
                var rect = new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size);

                // Soft drop shadow under the node — cheap depth on the flat map.
                var oldTint = GUI.color;
                GUI.color = new Color(0f, 0.12f, 0.22f, 0.20f);
                GUI.DrawTexture(new Rect(rect.x + 2f * _scale, rect.y + 5f * _scale,
                                         rect.width, rect.height), circleTex);
                GUI.color = oldTint;

                // Number drawn as a shadowed label ON TOP (labels never eat button clicks).
                if (GUI.Button(rect, GUIContent.none, _nodeStyle))
                {
                    GUI.enabled = true;
                    GUI.backgroundColor = oldBg;
                    GUI.EndGroup();
                    StartLevel(section, section.Levels[i]);
                    return;
                }
                GUI.enabled = true; // label must not fade while a swipe disables the buttons
                DrawLabelShadowed(rect, unlocked ? levelNumber.ToString() : "-", _nodeTextStyle);

                if (cleared)
                {
                    GUI.enabled = true;
                    float pip = 10f * _scale;
                    DrawStars(new Rect(c.x - 40f * _scale, rect.yMax + 3f * _scale, 80f * _scale, pip),
                              Stars.Get(section.Levels[i].Name), pip);
                    GUI.Label(new Rect(c.x - 70f * _scale, rect.yMax + 15f * _scale, 140f * _scale, 20f * _scale),
                              $"Best {best}", _nodeBestStyle);
                }
            }
            GUI.enabled = true;
            GUI.backgroundColor = oldBg;
            GUI.EndGroup();
        }

        // ---- Menu pages (intro / home / section route / reef) --------------------------------

        /// <summary>
        /// The launch splash: the lagoon breathes (rising bubbles, cruising fish), the
        /// title pops in with a trio of game orbs above it, then a pulsing "tap to dive"
        /// prompt. Tap-anywhere to continue is handled in Update; everything here is
        /// unscaled-time animation and null-safe sprite loads.
        /// </summary>
        private void DrawIntroPage()
        {
            float t = Time.unscaledTime - _introShownAt;
            var oldColor = GUI.color;

            // Rising ambient bubbles, looping and deterministic per index.
            var circle = PrimitiveSprites.Circle().texture;
            for (int i = 0; i < 14; i++)
            {
                float ph = Frac(t * (0.05f + 0.012f * ((i * 7) % 5)) + i * 0.137f);
                float x = Screen.width * Frac(i * 0.618f) + Mathf.Sin(t * 0.8f + i) * 12f * _scale;
                float y = Mathf.Lerp(Screen.height + 30f, -30f, ph);
                float s = (6f + (i * 13) % 18) * _scale;
                GUI.color = new Color(1f, 1f, 1f, 0.10f + 0.25f * (1f - ph));
                GUI.DrawTexture(new Rect(x, y, s, s), circle);
            }
            GUI.color = oldColor;

            // Fish cruising by at depths that keep the title band (~0.32-0.44) clear.
            DrawIntroFish("fish_blue", 0.14f, 44f, 130, t);
            DrawIntroFish("fish_orange", 0.58f, 36f, 470, t);
            DrawIntroFish("fish_green", 0.72f, 40f, 910, t);

            // A trio of game orbs bobbing above the title — "this is a bubble game".
            var orbTex = PrimitiveSprites.GlossyOrb().texture;
            Color[] orbTints = { new Color(0.35f, 0.78f, 1f), Coral, Sunshine };
            float orbAppear = Mathf.Clamp01((t - 0.25f) / 0.5f);
            for (int i = 0; i < 3; i++)
            {
                float size = (30f + 6f * (i == 1 ? 1f : 0f)) * _scale * Mathf.Max(0.01f, EaseOutBack(orbAppear));
                float ox = Screen.width * 0.5f + (i - 1) * 64f * _scale;
                float oy = Screen.height * 0.24f + Mathf.Sin(t * 2.2f + i * 1.9f) * 7f * _scale;
                GUI.color = orbTints[i];
                GUI.DrawTexture(new Rect(ox - size * 0.5f, oy - size * 0.5f, size, size), orbTex);
            }
            GUI.color = oldColor;

            // Title pops in with the bubble curve, then floats.
            float appear = Mathf.Clamp01(t / 0.7f);
            float bob = Mathf.Sin(t * 1.3f) * 4f * _scale;
            var titleRect = new Rect(0, Screen.height * 0.32f + bob, Screen.width, 64f * _scale);
            var m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * Mathf.Max(0.01f, EaseOutBack(appear)),
                                        titleRect.center);
            DrawLabelShadowed(titleRect, "CORAL CASCADE", _introTitleStyle);
            GUI.matrix = m;

            float subA = Mathf.Clamp01((t - 0.55f) / 0.5f);
            GUI.color = new Color(1f, 1f, 1f, subA);
            GUI.Label(new Rect(0, titleRect.yMax + 4f * _scale, Screen.width, 26f * _scale),
                      "pop bubbles · ride the cascade · grow your reef", _subtitleStyle);
            GUI.color = oldColor;

            // The tap prompt breathes once the title has settled.
            if (t > 1.1f)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Sin(t * 3.2f));
                DrawLabelShadowed(new Rect(0, Screen.height * 0.66f, Screen.width, 30f * _scale),
                                  "Tap to dive in!", _introPromptStyle);
                GUI.color = oldColor;
            }
        }

        /// <summary>A splash fish swimming laps across the whole screen. Null-safe.</summary>
        private void DrawIntroFish(string sprite, float y01, float sizePx, int phase, float t)
        {
            var s = BubbleArt.Get(sprite);
            if (s == null) return;
            float w = sizePx * _scale;
            float h = w * (s.rect.height / s.rect.width);
            float span = Screen.width + w * 2f;
            float k = t * (34f + (phase % 7) * 5f) * _scale + phase;
            float px = Mathf.PingPong(k, span);
            bool movingRight = ((int)(k / span) & 1) == 0;
            float y = Screen.height * y01 + Mathf.Sin(t * 1.7f + phase) * 8f * _scale;
            DrawSpriteGUI(new Rect(px - w, y, w, h), s, !movingRight, new Color(1f, 1f, 1f, 0.95f));
        }

        /// <summary>The main menu: title, daily banner, then one big card per destination.</summary>
        private void DrawHomePage()
        {
            // The title bobs gently like it's floating — subtle, but the page feels alive.
            float bob = Mathf.Sin(Time.unscaledTime * 1.1f) * 2.5f * _scale;
            DrawLabelShadowed(new Rect(0, 24f * _scale + bob, Screen.width, 48f * _scale),
                              "CORAL CASCADE", _titleStyle);
            GUI.Label(new Rect(0, 72f * _scale + bob, Screen.width, 24f * _scale),
                      "Choose your waters", _subtitleStyle);
            DrawPearlChip();

            float y = 128f * _scale;
            y += DrawDailyBanner(y) + 6f * _scale;

            // The section whose frontier the player should chase glows sunshine.
            bool introDone = Progress.HighestUnlocked(_sections[0].Key) > _sections[0].Levels.Count;
            for (int s = 0; s < _sections.Length; s++)
            {
                bool highlight = (s == 0) != introDone; // tutorial until done, then adventure
                if (DrawMenuCard(ref y, _sections[s].Title,
                                 $"{ClearedCount(_sections[s])}/{_sections[s].Levels.Count} cleared",
                                 highlight))
                {
                    _sectionIndex = s;
                    _sections[s].MapOffset = -1f; // open centered on the frontier
                    _menuPage = MenuPage.SectionMap;
                }
            }
            if (DrawMenuCard(ref y, "My Reef", "your aquarium — spend pearls, watch it grow", false))
            {
                _menuPage = MenuPage.Reef;
                _shopOpen = false;
                _goldenUnlocked = ReefStore.GoldenPufferUnlocked();
                _pearlEelUnlocked = ReefStore.PearlEelUnlocked();
            }
        }

        private void DrawReefPage()
        {
            if (DrawBackButton())
                return;
            DrawLabelShadowed(new Rect(0, 12f * _scale, Screen.width, 40f * _scale), "My Reef", _titleStyle);
            DrawPearlChip();
            float top = 68f * _scale;
            DrawReefTank(new Rect(0, top, Screen.width, Screen.height - top));
        }

        /// <summary>Top-left Back button shared by the sub-pages. True if it navigated.</summary>
        private bool DrawBackButton()
        {
            if (GUI.Button(new Rect(10f * _scale, 12f * _scale,
                                    ButtonW("< Back", _tabStyle, 92f * _scale), 40f * _scale),
                           "< Back", _tabStyle))
            {
                _menuPage = MenuPage.Home;
                return true;
            }
            return false;
        }

        private void DrawPearlChip()
        {
            float chipH = 30f * _scale;
            var chipRect = new Rect(Screen.width - 128f * _scale, 12f * _scale, 118f * _scale, chipH);
            GUI.DrawTexture(chipRect, PrimitiveSprites.RoundedRect(new Color(1f, 1f, 1f, 0.40f)));
            float pearlIcon = chipH - 10f * _scale;
            var old = GUI.color;
            GUI.color = new Color(0.98f, 0.93f, 0.82f);
            GUI.DrawTexture(new Rect(chipRect.x + 7f * _scale, chipRect.y + 5f * _scale, pearlIcon, pearlIcon),
                            PrimitiveSprites.GlossyOrb().texture);
            GUI.color = old;
            GUI.Label(new Rect(chipRect.x + pearlIcon + 12f * _scale, chipRect.y,
                               chipRect.width - pearlIcon - 14f * _scale, chipRect.height),
                      Pearls.Balance.ToString(), _barLabelStyle);
        }

        private bool DrawMenuCard(ref float y, string title, string sub, bool highlight)
        {
            float w = Mathf.Min(430f * _scale, Screen.width - 24f * _scale);
            float h = 72f * _scale;
            var rect = new Rect((Screen.width - w) * 0.5f, y, w, h);
            y += h + 12f * _scale;
            bool swiping = _dragDistance > 15f; // same guard as everywhere: swipes never tap
            GUI.enabled = !swiping;
            // Two type sizes give the card a real hierarchy: big title, quiet subtitle.
            string body = $"<size={(int)(19f * _scale)}><b>{title}</b></size>\n" +
                          $"<size={(int)(13f * _scale)}><color=#0A5978>{sub}</color></size>";
            bool hit = GUI.Button(rect, body, highlight ? _cardActiveStyle : _cardStyle);
            GUI.enabled = true;
            GUI.Label(new Rect(rect.xMax - 44f * _scale, rect.y, 32f * _scale, rect.height),
                      ">", _chevronStyle); // labels never eat the button's click
            return hit;
        }

        private int ClearedCount(Section section)
        {
            int n = 0;
            foreach (var level in section.Levels)
                if (HighScores.Get(level.Name) > 0) n++;
            return n;
        }

        // ---- Daily Reef (roadmap step 4) ------------------------------------------------------

        private void EnsureDaily()
        {
            var today = System.DateTime.Now.Date; // LOCAL date: resets at the player's midnight
            if (_dailyLayout == null || _dailyDate != today)
            {
                _dailyDate = today;
                _dailyLayout = LevelCatalog.Daily(today);
            }
        }

        /// <summary>
        /// The Daily Reef banner between the tabs and the map path. Returns the height it
        /// consumed. Uncleared today = sunshine (calls attention); cleared = quiet tab
        /// style with today's best + star pips.
        /// </summary>
        private float DrawDailyBanner(float y)
        {
            EnsureDaily();
            float h = 50f * _scale;
            float w = Mathf.Min(430f * _scale, Screen.width - 24f * _scale);
            var rect = new Rect((Screen.width - w) * 0.5f, y, w, h);

            int best = HighScores.Get(_dailyLayout.Name);
            bool cleared = best > 0;
            string label = cleared
                ? $"DAILY REEF  ·  cleared!  Best {best}"
                : $"DAILY REEF  ·  {_dailyDate:MMM d}  ·  a fresh challenge!";

            bool swiping = _dragDistance > 15f; // same guard as map nodes: swipes never tap
            GUI.enabled = !swiping;
            bool tapped = GUI.Button(rect, label, cleared ? _tabStyle : _tabActiveStyle);
            GUI.enabled = true;
            if (cleared)
                DrawStars(new Rect(rect.xMax - 76f * _scale, rect.yMax - 16f * _scale,
                                   64f * _scale, 12f * _scale),
                          Stars.Get(_dailyLayout.Name), 9f * _scale);
            if (tapped)
                StartLevel(Active, _dailyLayout); // not in a section: no unlock chain, no Next
            return h + 10f * _scale;
        }

        // ---- My Reef aquarium (roadmap step 5, purely cosmetic — see ReefStore) -------------

        /// <summary>
        /// The personal aquarium: owned fish swim (ping-pong lanes + bob), plants sway on
        /// the sand, vents bubble, achievement rares glide by. Screen-space IMGUI like the
        /// rest of the menus — the world behind stays untouched.
        /// </summary>
        private void DrawReefTank(Rect area)
        {
            float t = Time.unscaledTime;

            // Sand bed along the bottom.
            var sand = BubbleArt.Get("terrain_sand_top_a");
            float tile = 64f * _scale;
            float sandTop = area.yMax - tile * 0.62f;
            if (sand != null)
                for (float x = 0; x < area.width; x += tile)
                    DrawSpriteGUI(new Rect(area.x + x, area.yMax - tile, tile, tile), sand, false, Color.white);

            int owned = 0;
            _decorHits.Clear(); // rebuilt every draw; placement input reads last frame's

            // Plants and vents live on the sand (drag to place); fish swim freely above.
            foreach (var item in ReefStore.Catalog)
            {
                int n = ReefStore.Count(item.Id);
                owned += n;
                for (int k = 0; k < n; k++)
                {
                    int h = ReefHash(item.Id, k);
                    bool held = item.Id == _placingId && k == _placingInst;
                    float x01 = held ? _placingX01 : ReefStore.GetX(item.Id, k);
                    float fx = Mathf.Lerp(area.x + 30f * _scale, area.xMax - 30f * _scale, x01);
                    switch (item.Kind)
                    {
                        case ReefItemKind.Plant:
                        {
                            var s = BubbleArt.Get(item.SpriteName);
                            if (s == null) break;
                            float w = 58f * _scale * item.SizeMul;
                            float hgt = w * (s.rect.height / s.rect.width);
                            float sway = Mathf.Sin(t * 0.9f + h) * 3f * _scale;
                            var rect = new Rect(fx + sway - w * 0.5f, sandTop - hgt + 6f * _scale, w, hgt);
                            if (held) DrawHeldGlow(rect);
                            DrawSpriteGUI(rect, s, (h & 2) == 0, item.Tint);
                            _decorHits.Add(new DecorHit { R = rect, Id = item.Id, Inst = k });
                            break;
                        }
                        case ReefItemKind.Vent:
                        {
                            var ring = BubbleArt.Get("bubble_c");
                            if (ring == null) break;
                            var baseRect = new Rect(fx - 22f * _scale, sandTop - 34f * _scale,
                                                    44f * _scale, 44f * _scale);
                            if (held) DrawHeldGlow(baseRect);
                            for (int j = 0; j < 3; j++)
                            {
                                float prog = Frac(t * 0.16f + j / 3f + Frac(h * 0.377f));
                                float size = (9f + 5f * j) * _scale;
                                float y = Mathf.Lerp(sandTop, area.y + 40f * _scale, prog);
                                DrawSpriteGUI(new Rect(fx + Mathf.Sin(t + j + h) * 5f * _scale, y, size, size),
                                              ring, false, new Color(1f, 1f, 1f, 0.7f * (1f - prog)));
                            }
                            _decorHits.Add(new DecorHit { R = baseRect, Id = item.Id, Inst = k });
                            break;
                        }
                        case ReefItemKind.Fish:
                            DrawTankFish(area, sandTop, item, k,
                                         ReefStore.GrowthScale(item.Id, k), t, h);
                            break;
                    }
                }
            }

            // Achievement rares swim too (fully grown, one of each).
            if (_goldenUnlocked)
            {
                owned++;
                DrawTankFish(area, sandTop, ReefStore.GoldenPuffer, 0, 1f, t, ReefHash("rare_gold", 0));
            }
            if (_pearlEelUnlocked)
            {
                owned++;
                DrawTankFish(area, sandTop, ReefStore.PearlEel, 0, 1f, t, ReefHash("rare_eel", 0));
            }

            if (owned == 0)
                GUI.Label(new Rect(area.x, area.y + area.height * 0.32f, area.width, 60f * _scale),
                          "Your reef is waiting.\nWin levels, earn pearls, fill it with life!",
                          _subtitleStyle);

            // Header row: shop toggle + rare progress. Width fits the LONGER of the two
            // labels so the button doesn't resize (or clip) when toggled.
            float shopW = Mathf.Max(ButtonW("Reef Shop", _buttonStyle),
                                    ButtonW("Close Shop", _buttonStyle));
            var shopBtn = new Rect(area.x + 12f * _scale, area.y + 8f * _scale,
                                   shopW, 40f * _scale);
            if (GUI.Button(shopBtn, _shopOpen ? "Close Shop" : "Reef Shop", _buttonStyle))
                _shopOpen = !_shopOpen;
            GUI.Label(new Rect(shopBtn.xMax + 14f * _scale, area.y + 4f * _scale,
                               area.width - shopBtn.width - 40f * _scale, 24f * _scale),
                      _goldenUnlocked ? "Golden Puffer — UNLOCKED!"
                                      : "Golden Puffer — 3-star all of Tutorial Reef",
                      _shopSmallStyle);
            GUI.Label(new Rect(shopBtn.xMax + 14f * _scale, area.y + 26f * _scale,
                               area.width - shopBtn.width - 40f * _scale, 24f * _scale),
                      _pearlEelUnlocked ? "Pearl Eel — UNLOCKED!"
                                        : "Pearl Eel — 3-star any 10 Adventure reefs",
                      _shopSmallStyle);
            if (owned > 0)
                GUI.Label(new Rect(area.x + 12f * _scale, area.yMax - 24f * _scale,
                                   area.width - 24f * _scale, 22f * _scale),
                          "Tip: drag corals, rocks and vents to arrange your reef",
                          _shopSmallStyle);

            if (_shopOpen)
                DrawShopPanel(area);
        }

        /// <summary>Soft ring under the decor piece currently being dragged.</summary>
        private void DrawHeldGlow(Rect r)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 0.95f, 0.6f, 0.35f);
            float pad = 10f * _scale;
            GUI.DrawTexture(new Rect(r.x - pad, r.y - pad, r.width + pad * 2f, r.height + pad * 2f),
                            PrimitiveSprites.Circle().texture);
            GUI.color = old;
        }

        private void DrawTankFish(Rect area, float sandTop, ReefItem item, int instance,
                                  float growth, float t, int h)
        {
            var s1 = BubbleArt.Get(item.SpriteName);
            if (s1 == null) return;
            var s2 = string.IsNullOrEmpty(item.SpriteName2) ? null : BubbleArt.Get(item.SpriteName2);

            // Two-tile fish (the eel) compose side by side at one shared pixel scale.
            float unitW = s1.rect.width + (s2 != null ? s2.rect.width : 0f);
            float unitH = Mathf.Max(s1.rect.height, s2 != null ? s2.rect.height : 0f);
            float w = 54f * _scale * item.SizeMul * growth * (s2 != null ? 1.7f : 1f);
            float px2unit = w / unitW;
            float hgt = unitH * px2unit;

            float laneTop = area.y + 64f * _scale;
            float laneBottom = sandTop - 40f * _scale - hgt;
            float laneY = Mathf.Lerp(laneTop, Mathf.Max(laneTop, laneBottom), Frac(h * 0.7548f));

            float span = Mathf.Max(40f * _scale, area.width - w - 40f * _scale);
            float speed = (26f + (h & 31)) * _scale; // px/s, per-fish
            float k = t * speed + (h & 1023);
            float px = Mathf.PingPong(k, span);
            bool movingRight = ((int)(k / span) & 1) == 0;
            float bob = Mathf.Sin(t * 1.9f + h) * 6f * _scale;

            float x = area.x + 20f * _scale + px;
            float y = laneY + bob;
            if (s2 == null)
            {
                DrawSpriteGUI(new Rect(x, y, w, hgt), s1, !movingRight, item.Tint);
                return; // pack fish face right natively
            }

            float w1 = s1.rect.width * px2unit, h1 = s1.rect.height * px2unit;
            float w2 = s2.rect.width * px2unit, h2 = s2.rect.height * px2unit;
            // Vertical alignment must follow TEXTURE space, not centering: the halves'
            // artwork heights differ but they share a top edge in their source tiles —
            // centering stepped the seam by ~2px (visible split, user-reported).
            float texTopMax = Mathf.Max(s1.rect.yMax, s2.rect.yMax);
            float y1 = y + (texTopMax - s1.rect.yMax) * px2unit;
            float y2 = y + (texTopMax - s2.rect.yMax) * px2unit;
            // Overlap the joint by ~1.5 source px of solid body so filtering/sub-pixel
            // placement can never open a gap between the quads.
            float overlap = 1.5f * px2unit;
            if (movingRight)
            {
                DrawSpriteGUI(new Rect(x, y1, w1, h1), s1, false, item.Tint);
                DrawSpriteGUI(new Rect(x + w1 - overlap, y2, w2, h2), s2, false, item.Tint);
            }
            else
            {
                // Mirrored: halves swap order AND each half flips.
                DrawSpriteGUI(new Rect(x, y2, w2, h2), s2, true, item.Tint);
                DrawSpriteGUI(new Rect(x + w2 - overlap, y1, w1, h1), s1, true, item.Tint);
            }
        }

        /// <summary>Shop/list icon for a reef item — handles two-tile sprites too.</summary>
        private void DrawReefItemIcon(Rect outer, ReefItem item)
        {
            var s1 = BubbleArt.Get(item.SpriteName);
            if (s1 == null) return;
            var s2 = string.IsNullOrEmpty(item.SpriteName2) ? null : BubbleArt.Get(item.SpriteName2);
            if (s2 == null)
            {
                DrawSpriteGUI(FitRect(outer, s1), s1, false, item.Tint);
                return;
            }
            float unitW = s1.rect.width + s2.rect.width;
            float unitH = Mathf.Max(s1.rect.height, s2.rect.height);
            float k = Mathf.Min(outer.width / unitW, outer.height / unitH);
            float w1 = s1.rect.width * k, w2 = s2.rect.width * k;
            float x = outer.x + (outer.width - (w1 + w2)) * 0.5f;
            // Same seam rules as DrawTankFish: texture-top alignment + slight overlap.
            float texTopMax = Mathf.Max(s1.rect.yMax, s2.rect.yMax);
            float yTop = outer.y + (outer.height - unitH * k) * 0.5f;
            DrawSpriteGUI(new Rect(x, yTop + (texTopMax - s1.rect.yMax) * k,
                                   w1, s1.rect.height * k), s1, false, item.Tint);
            DrawSpriteGUI(new Rect(x + w1 - 1.5f * k, yTop + (texTopMax - s2.rect.yMax) * k,
                                   w2, s2.rect.height * k), s2, false, item.Tint);
        }

        private void DrawShopPanel(Rect area)
        {
            float w = Mathf.Min(520f * _scale, Screen.width - 36f);
            float h = Mathf.Min(area.height - 16f * _scale, 470f * _scale);
            var rect = new Rect((Screen.width - w) * 0.5f, area.y + 56f * _scale, w,
                                Mathf.Min(h, area.height - 64f * _scale));
            GUILayout.BeginArea(rect, _panelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Reef Shop", _overlayTitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{Pearls.Balance} pearls", _barLabelStyle, GUILayout.ExpandHeight(true));
            GUILayout.EndHorizontal();
            _shopScroll = GUILayout.BeginScrollView(_shopScroll);

            // Column budget: Sell/Buy take their widest label (rows stay aligned), the
            // name column gets EVERYTHING left over — an over-constrained row crushes the
            // flexible name labels into illegible slivers (user-reported), so the name
            // width is always explicit, never leftovers-after-overflow.
            float sellW = 0f, buyW = 0f;
            foreach (var item in ReefStore.Catalog)
            {
                sellW = Mathf.Max(sellW, ButtonW($"Sell {ReefStore.SellValue(item)}", _sellStyle));
                buyW = Mathf.Max(buyW, ButtonW($"Buy {item.Price}", _buyStyle));
            }
            float iconW = 42f * _scale;
            float nameW = rect.width - _panelStyle.padding.horizontal - 24f /*scrollbar*/
                          - _rowStyle.padding.horizontal - _rowStyle.margin.right
                          - iconW - sellW - buyW - 24f * _scale /*spacers*/;
            nameW = Mathf.Max(96f * _scale, nameW);

            foreach (var item in ReefStore.Catalog)
            {
                // Each item on its own translucent card — the list reads as rows, not text.
                GUILayout.BeginHorizontal(_rowStyle, GUILayout.Height(52f * _scale));
                var iconRect = GUILayoutUtility.GetRect(iconW, iconW, GUILayout.Width(iconW));
                DrawReefItemIcon(iconRect, item);
                GUILayout.Space(8f * _scale);
                GUILayout.BeginVertical(GUILayout.Width(nameW));
                GUILayout.Label(item.DisplayName, _shopNameStyle, GUILayout.Width(nameW));
                GUILayout.Label($"owned {ReefStore.Count(item.Id)}", _shopSmallStyle,
                                GUILayout.Width(nameW));
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUI.enabled = ReefStore.Count(item.Id) > 0;
                if (GUILayout.Button($"Sell {ReefStore.SellValue(item)}", _sellStyle,
                                     GUILayout.Width(sellW), GUILayout.Height(38f * _scale)))
                    ReefStore.Sell(item);
                GUILayout.Space(6f * _scale);
                GUI.enabled = Pearls.Balance >= item.Price;
                if (GUILayout.Button($"Buy {item.Price}", _buyStyle,
                                     GUILayout.Width(buyW), GUILayout.Height(38f * _scale)))
                    ReefStore.Buy(item);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.Space(4f * _scale);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Draws a pack sprite in GUI space. Sprites are SUB-RECTS of their texture, so this
        /// must go through DrawTextureWithTexCoords; a negative-width UV rect flips X.
        /// </summary>
        private static void DrawSpriteGUI(Rect r, Sprite s, bool flipX, Color tint)
        {
            if (s == null) return;
            var tex = s.texture;
            var uv = new Rect(s.rect.x / tex.width, s.rect.y / tex.height,
                              s.rect.width / tex.width, s.rect.height / tex.height);
            if (flipX) { uv.x += uv.width; uv.width = -uv.width; }
            var old = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(r, tex, uv);
            GUI.color = old;
        }

        /// <summary>Largest sprite-aspect rect centered inside <paramref name="outer"/>.</summary>
        private static Rect FitRect(Rect outer, Sprite s)
        {
            float aspect = s.rect.width / s.rect.height;
            float w = outer.width, h = outer.height;
            if (w / h > aspect) w = h * aspect; else h = w / aspect;
            return new Rect(outer.x + (outer.width - w) * 0.5f,
                            outer.y + (outer.height - h) * 0.5f, w, h);
        }

        private static float Frac(float v) => v - Mathf.Floor(v);

        private static int ReefHash(string id, int instance)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in id) h = h * 31 + c;
                return h * 31 + instance * 2654435;
            }
        }

        private void DrawTopBar()
        {
            float row1H = 52f * _scale;
            float meterH = 24f * _scale;
            float h = row1H + meterH;
            _topBarRect = new Rect(0, 0, Screen.width, h);
            // Frosted light bar over the bright water (deep-teal text sits on it), with a
            // soft shadow fading out below it so the bar reads as a layer, not a stripe.
            var oldBarColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.40f);
            GUI.DrawTexture(_topBarRect, _whiteTex);
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(0, h - 2f, Screen.width, 2f), _whiteTex);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, h, Screen.width, 7f * _scale), _barShadowTex,
                            ScaleMode.StretchToFill);
            GUI.color = oldBarColor;

            float pad = 10f * _scale;
            float avail = Screen.width - pad * 2f;
            GUILayout.BeginArea(new Rect(pad, 0, avail, row1H));
            GUILayout.BeginHorizontal(GUILayout.Height(row1H));

            // Column budget (the shop-row lesson, applied to the HUD): chips and the pause
            // button are measured first and the level label gets EXACTLY the remainder —
            // on a narrow portrait screen the unbudgeted row crushed the label into a
            // vertical sliver and shoved the buttons off the right edge (user-reported).
            // Debug moved into the pause menu: a phone HUD has no room for a dev button.
            string scoreText = $"Score  <b>{_manager.Score.Total}</b>";
            string shotsText = $"Shots  <b>{_manager.State.ShotsRemaining}</b>";
            string tideText = _manager.PressureActive
                ? $"Tide  <b>{_manager.ShotsUntilPressure}</b>" : null;
            float ChipW(string s) => _chipStyle.CalcSize(new GUIContent(s)).x + 2f;
            float btnH = row1H - 12f * _scale;
            float pauseW = btnH; // icon-sized square: "II"
            float fixedW = ChipW(scoreText) + 6f * _scale + ChipW(shotsText)
                         + (tideText != null ? ChipW(tideText) + 6f * _scale : 0f)
                         + pauseW + 20f * _scale;
            float labelW = avail - fixedW;

            int idx = CurrentLevelIndex();
            string name = _manager.CurrentLayout != null ? _manager.CurrentLayout.Name : "?";
            string levelText = idx >= 0
                ? $"<b>{_playingSection.NodePrefix} {idx + 1}</b>  {name}" : $"<b>{name}</b>";
            // Too tight for the full title? Drop to the short form, then to nothing.
            if (idx >= 0 && _barLabelStyle.CalcSize(new GUIContent(levelText)).x > labelW)
                levelText = $"<b>{_playingSection.NodePrefix} {idx + 1}</b>";
            if (labelW > 34f * _scale)
                GUILayout.Label(levelText, _barLabelStyle,
                                GUILayout.Width(labelW), GUILayout.ExpandHeight(true));
            GUILayout.FlexibleSpace();

            // Stats live in pill chips so the HUD reads as designed UI, not floating text.
            GUILayout.Label(scoreText, _chipStyle);
            GUILayout.Space(6f * _scale);
            GUILayout.Label(shotsText, _chipStyle);
            if (tideText != null)
            {
                GUILayout.Space(6f * _scale);
                // The tide chip goes coral and pulses when the NEXT shot drops the board.
                bool urgent = _manager.ShotsUntilPressure <= 1;
                var oldChipColor = GUI.color;
                if (urgent)
                    GUI.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 7f));
                GUILayout.Label(tideText, urgent ? _chipUrgentStyle : _chipStyle);
                GUI.color = oldChipColor;
            }
            GUILayout.Space(10f * _scale);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            GUILayout.FlexibleSpace();
            // Icon-sized pause square ("II" — plain ASCII, no glyph risk).
            if (GUILayout.Button("II", _buyStyle, GUILayout.Height(btnH), GUILayout.Width(pauseW)))
                Pause();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            DrawStarMeter(new Rect(pad, row1H - 3f * _scale, Screen.width - pad * 2f, meterH));
        }

        /// <summary>
        /// The star-target row: a progress bar toward the 3★ score with orb pips at the
        /// 2★ and 3★ marks (they turn gold and pulse the moment they're crossed), plus a
        /// "next target" label. 1★ has no pip — it simply means clearing the level. Note
        /// the clear bonus (+50/unused shot) lands at the win, so a near-miss here can
        /// still tip over on the end screen — that's the banked-shots strategy paying out.
        /// </summary>
        private void DrawStarMeter(Rect r)
        {
            var layout = _manager.CurrentLayout;
            int t2 = Stars.Target(layout, 2);
            int t3 = Stars.Target(layout, 3);
            if (t3 <= 0) return;

            int score = _manager.Score.Total;
            if (score < _meterLastScore) { _pip2Lit = false; _pip3Lit = false; } // level (re)loaded
            _meterLastScore = score;
            if (!_pip2Lit && score >= t2) { _pip2Lit = true; _pip2LitAt = Time.unscaledTime; }
            if (!_pip3Lit && score >= t3) { _pip3Lit = true; _pip3LitAt = Time.unscaledTime; }

            float labelW = Mathf.Min(170f * _scale, r.width * 0.38f);
            var bar = new Rect(r.x + 4f * _scale, r.y + r.height * 0.5f - 4f * _scale,
                               r.width - labelW - 20f * _scale, 8f * _scale);

            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(bar, _whiteTex);
            GUI.color = Sunshine;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(score / (float)t3), bar.height),
                            _whiteTex);
            DrawMeterPip(bar, t2 / (float)t3, _pip2Lit, _pip2LitAt);
            DrawMeterPip(bar, 1f, _pip3Lit, _pip3LitAt);
            GUI.color = old;

            string label = score >= t3 ? "All score targets hit!"
                         : score >= t2 ? $"3-star at {t3}"
                         : $"2-star at {t2}";
            GUI.Label(new Rect(r.xMax - labelW, r.y, labelW, r.height), label, _meterLabelStyle);
        }

        private void DrawMeterPip(Rect bar, float frac, bool lit, float litAt)
        {
            float size = 17f * _scale; // stars have a thinner silhouette than orbs
            if (lit)
            {
                float k = Mathf.Max(0f, 1f - (Time.unscaledTime - litAt) / 0.45f);
                size *= 1f + 0.7f * k; // pop the moment it's earned
            }
            GUI.color = lit ? new Color(1f, 0.8f, 0.15f) : new Color(1f, 1f, 1f, 0.7f);
            var c = new Vector2(bar.x + bar.width * frac, bar.y + bar.height * 0.5f);
            GUI.DrawTexture(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size),
                            PrimitiveSprites.Star().texture);
        }

        /// <summary>
        /// Start-of-level card naming the star targets before the first shot. NOT modal —
        /// it has no buttons, never blocks aiming, and the first touch dismisses it
        /// (see Update); otherwise it fades out on its own.
        /// </summary>
        private void DrawTargetSplash()
        {
            var layout = _manager.CurrentLayout;
            int t2 = Stars.Target(layout, 2);
            int t3 = Stars.Target(layout, 3);
            if (t3 <= 0) return;

            float fade = Mathf.Clamp01((_splashUntil - Time.unscaledTime) / 0.4f);
            // Pops in with the same bubble curve as the overlays (appear time is derived
            // from _splashUntil, which is always set to now + SplashSeconds).
            float appear = Mathf.Clamp01(
                (Time.unscaledTime - (_splashUntil - SplashSeconds)) / PanelPopSeconds);

            // The panel is sized from MEASURED text heights — hardcoded heights clip the
            // moment a font change makes a line wrap (user-reported).
            string title = layout != null ? layout.Name : "";
            string targets = $"2-star {t2}   ·   3-star {t3}";
            string hint = "Clear the reef to earn your first star!";
            float w = Mathf.Min(380f * _scale, Screen.width - 80f);
            float innerW = w - 32f * _scale;
            float hTitle = _overlayTitleStyle.CalcHeight(new GUIContent(title), innerW);
            float hTargets = _subtitleStyle.CalcHeight(new GUIContent(targets), innerW);
            float hHint = _subtitleStyle.CalcHeight(new GUIContent(hint), innerW);
            float pad = 14f * _scale, gap = 5f * _scale;
            float h = pad * 2f + hTitle + gap + hTargets + gap + hHint;

            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, fade);
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.26f, w, h);
            var oldMatrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(
                Vector2.one * Mathf.Max(0.01f, EaseOutBack(appear)), rect.center);
            GUI.Box(rect, GUIContent.none, _panelStyle);
            float x = rect.x + 16f * _scale;
            float y = rect.y + pad;
            GUI.Label(new Rect(x, y, innerW, hTitle), title, _overlayTitleStyle);
            y += hTitle + gap;
            GUI.Label(new Rect(x, y, innerW, hTargets), targets, _subtitleStyle);
            y += hTargets + gap;
            GUI.Label(new Rect(x, y, innerW, hHint), hint, _subtitleStyle);
            GUI.matrix = oldMatrix;
            GUI.color = old;
        }

        // ---- First-encounter mechanic tutorial (modal, player-dismissed only) -----------------

        /// <summary>
        /// The "new mechanic" card: one animated diagram + explanation per newly-met
        /// mechanic, and a single Got-it button. Deliberately NOT self-dismissing (user
        /// requirement): it stays until the player closes it, and only closing marks the
        /// mechanics as seen. Modal — aiming is blocked while it's up.
        /// </summary>
        private void DrawMechanicTutorial()
        {
            _modalOpen = true;
            float openT = Mathf.Clamp01((Time.unscaledTime - _tutorialShownAt) / PanelPopSeconds);
            FillScreen(new Color(0f, 0.20f, 0.30f, 0.45f * openT));

            float w = Mathf.Min(410f * _scale, Screen.width - 50f);
            float btnH = 50f * _scale;
            // Section heights are MEASURED per mechanic (body copy wraps differently per
            // font/width — hardcoded heights clip, same lesson as the target splash).
            float textW = w - _panelStyle.padding.horizontal - 148f * _scale - 24f;
            float total = 0f;
            foreach (var key in _pendingTutorials)
                total += SectionHeight(key, textW) + 8f * _scale;
            BeginPanel(w, 52f * _scale + total + btnH + 20f * _scale, openT);
            GUILayout.Label(_pendingTutorials.Count > 1 ? "NEW DISCOVERIES!" : "NEW DISCOVERY!",
                            _overlayTitleStyle);
            GUILayout.Space(8f * _scale);
            foreach (var key in _pendingTutorials)
            {
                DrawTutorialSection(key, SectionHeight(key, textW));
                GUILayout.Space(8f * _scale);
            }
            GUILayout.Space(6f * _scale);
            if (GUILayout.Button("Got it — let's play!", _buttonStyle, GUILayout.Height(btnH)))
            {
                foreach (var key in _pendingTutorials) TutorialFlags.MarkSeen(key);
                _pendingTutorials.Clear();
                _splashUntil = Time.unscaledTime + SplashSeconds; // star targets take the stage next
            }
            EndPanel();
        }

        /// <summary>The card copy for one mechanic — single source for drawing AND measuring.</summary>
        private static void TutorialCopy(string key, out string title, out string body)
        {
            switch (key)
            {
                case "Stone":
                    title = "Stone bubbles";
                    body = "Stones are too heavy to match — shots never pop them.\n" +
                           "Pop the bubbles HOLDING a stone and it sinks away!";
                    break;
                case "Ice":
                    title = "Frozen bubbles";
                    body = "Iced-over bubbles can't join a match.\n" +
                           "Pop a match right beside the ice to thaw it free.";
                    break;
                default:
                    title = "The rising tide";
                    body = "Every few shots the tide pushes the reef DOWN.\n" +
                           "Bubbles crossing the red line flood the reef — watch the Tide chip!";
                    break;
            }
        }

        /// <summary>Measured section height: title row + wrapped body, floor of the diagram stage.</summary>
        private float SectionHeight(string key, float textW)
        {
            TutorialCopy(key, out _, out string body);
            float bodyH = _tutBodyStyle.CalcHeight(new GUIContent(body), textW);
            return Mathf.Max(112f * _scale, 30f * _scale + bodyH + 12f * _scale);
        }

        /// <summary>One tutorial row: animated diagram on a light stage, title + copy right.</summary>
        private void DrawTutorialSection(string key, float h)
        {
            var row = GUILayoutUtility.GetRect(1f, h, GUILayout.ExpandWidth(true));
            float pad = 4f * _scale;
            var stage = new Rect(row.x + pad, row.y + pad, 108f * _scale, h - pad * 2f);
            var text = new Rect(stage.xMax + 12f * _scale, row.y + pad,
                                row.xMax - stage.xMax - 16f * _scale, h - pad * 2f);

            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.DrawTexture(stage, PrimitiveSprites.RoundedRect(new Color(0.72f, 0.92f, 0.98f)));
            GUI.color = old;

            TutorialCopy(key, out string title, out string body);
            switch (key)
            {
                case "Stone": DrawStoneDiagram(stage); break;
                case "Ice": DrawIceDiagram(stage); break;
                default: DrawTideDiagram(stage); break;
            }
            GUI.Label(new Rect(text.x, text.y, text.width, 26f * _scale), title, _tutTitleStyle);
            GUI.Label(new Rect(text.x, text.y + 28f * _scale, text.width, text.height - 28f * _scale),
                      body, _tutBodyStyle);
        }

        /// <summary>Two green bubbles pop; the stone they held sinks. Loops forever.</summary>
        private void DrawStoneDiagram(Rect r)
        {
            float t = Frac(Time.unscaledTime / 2.8f);
            var orb = PrimitiveSprites.GlossyOrb().texture;
            float d = r.width * 0.27f;
            float y = r.y + r.height * 0.28f;
            float x0 = r.x + (r.width - 3f * d) * 0.5f;

            float popK = Mathf.Clamp01((t - 0.45f) / 0.12f); // greens shrink out
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.62f) / 0.28f))
                         * (r.yMax - y - d - 6f * _scale);
            float fade = 1f - Mathf.Clamp01((t - 0.9f) / 0.1f);

            var old = GUI.color;
            float g = d * (1f - popK);
            if (g > 1f)
            {
                GUI.color = new Color(0.30f, 0.85f, 0.45f);
                GUI.DrawTexture(new Rect(x0 + (d - g) * 0.5f, y + (d - g) * 0.5f, g, g), orb);
                GUI.DrawTexture(new Rect(x0 + 2f * d + (d - g) * 0.5f, y + (d - g) * 0.5f, g, g), orb);
            }
            var stoneRect = new Rect(x0 + d, y + fall, d, d);
            var rock = BubbleArt.Get("rock_a");
            GUI.color = new Color(1f, 1f, 1f, fade);
            if (rock != null) DrawSpriteGUI(stoneRect, rock, false, new Color(1f, 1f, 1f, fade));
            else { GUI.color = new Color(0.55f, 0.57f, 0.62f, fade); GUI.DrawTexture(stoneRect, orb); }
            GUI.color = old;
        }

        /// <summary>A neighbor match pops and the frost melts off the frozen bubble. Loops.</summary>
        private void DrawIceDiagram(Rect r)
        {
            float t = Frac(Time.unscaledTime / 2.8f);
            var orb = PrimitiveSprites.GlossyOrb().texture;
            var circle = PrimitiveSprites.Circle().texture;
            float d = r.width * 0.30f;
            float y = r.y + (r.height - d) * 0.5f;
            float xGreen = r.x + r.width * 0.5f - d - 3f * _scale;
            float xIce = r.x + r.width * 0.5f + 3f * _scale;

            float popK = Mathf.Clamp01((t - 0.45f) / 0.12f);  // the neighbor match pops...
            float thaw = Mathf.Clamp01((t - 0.60f) / 0.25f);  // ...and the frost melts off

            var old = GUI.color;
            float g = d * (1f - popK);
            if (g > 1f)
            {
                GUI.color = new Color(0.30f, 0.85f, 0.45f);
                GUI.DrawTexture(new Rect(xGreen + (d - g) * 0.5f, y + (d - g) * 0.5f, g, g), orb);
            }
            // Frozen bubble: washed-out blue that turns vivid as the frost layer fades.
            GUI.color = Color.Lerp(new Color(0.72f, 0.86f, 0.95f), new Color(0.25f, 0.55f, 1f), thaw);
            GUI.DrawTexture(new Rect(xIce, y, d, d), orb);
            GUI.color = new Color(0.85f, 0.95f, 1f, 0.62f * (1f - thaw));
            GUI.DrawTexture(new Rect(xIce, y, d, d), circle);
            GUI.color = old;
        }

        /// <summary>A bubble row rides the tide down toward the pulsing red line. Loops.</summary>
        private void DrawTideDiagram(Rect r)
        {
            float t = Frac(Time.unscaledTime / 3.2f);
            var orb = PrimitiveSprites.GlossyOrb().texture;
            float d = r.width * 0.22f;
            float lineY = r.yMax - 12f * _scale;

            // Three discrete tide steps (it shoves, it doesn't glide).
            float steps = Mathf.Floor(t * 3f) + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Frac(t * 3f) * 5f));
            float y = Mathf.Lerp(r.y + 8f * _scale, lineY - d - 2f * _scale, Mathf.Clamp01(steps / 3f));

            Color[] tints = { new Color(0.35f, 0.78f, 1f), new Color(0.98f, 0.55f, 0.30f),
                              new Color(0.30f, 0.85f, 0.45f) };
            var old = GUI.color;
            float x0 = r.x + (r.width - 3.3f * d) * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                GUI.color = tints[i];
                GUI.DrawTexture(new Rect(x0 + i * 1.15f * d, y, d, d), orb);
            }
            // The danger line: red, pulsing harder as the bubbles close in.
            float closeness = Mathf.Clamp01(steps / 3f);
            GUI.color = new Color(1f, 0.25f, 0.25f,
                                  0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f) * closeness);
            GUI.DrawTexture(new Rect(r.x + 6f * _scale, lineY, r.width - 12f * _scale, 3f * _scale),
                            _whiteTex);
            GUI.color = old;
        }

        /// <summary>
        /// A pack fish tops the end panel: bright and bouncing on a win, grey-tinted and
        /// sunk low on a loss. Null-safe — no fish if the art pack is missing. Animated
        /// with rect offsets only (GUI.matrix tricks misbehave inside a BeginArea).
        /// </summary>
        private void DrawEndFish(bool won)
        {
            var s = BubbleArt.Get(won ? "fish_orange" : "fish_blue");
            if (s == null) return;
            Rect slot = GUILayoutUtility.GetRect(1f, 54f * _scale, GUILayout.ExpandWidth(true));
            float w = 66f * _scale;
            float h = w * (s.rect.height / s.rect.width);
            float t = Time.unscaledTime;
            float bob = won ? Mathf.Sin(t * 2.4f) * 4f * _scale : 3f * _scale; // winners bounce
            float pulse = won ? 1f + 0.05f * Mathf.Sin(t * 4.8f) : 1f;
            var r = new Rect(slot.x + (slot.width - w * pulse) * 0.5f,
                             slot.y + (slot.height - h * pulse) * 0.5f + bob,
                             w * pulse, h * pulse);
            // Winners face the sun; losers droop grey-blue.
            DrawSpriteGUI(r, s, !won, won ? Color.white : new Color(0.62f, 0.72f, 0.80f));
        }

        private void DrawPauseOverlay()
        {
            _modalOpen = true;
            float openT = Mathf.Clamp01((Time.unscaledTime - _pauseOpenedAt) / PanelPopSeconds);
            // Scrim fades in with the panel pop — teal dim, not a blackout.
            FillScreen(new Color(0f, 0.20f, 0.30f, 0.45f * openT));

            float w = Mathf.Min(340f * _scale, Screen.width - 60f);
            float btnH = 50f * _scale;
            int buttons = _debugHud != null ? 4 : 3;
            BeginPanel(w, 58f * _scale + buttons * (btnH + 10f * _scale), openT);
            GUILayout.Label("PAUSED", _overlayTitleStyle);
            GUILayout.Space(12f * _scale);
            if (GUILayout.Button("Resume", _buttonStyle, GUILayout.Height(btnH)))
                Resume();
            GUILayout.Space(10f * _scale);
            if (GUILayout.Button("Restart Level", _buttonStyle, GUILayout.Height(btnH)))
                RestartLevel();
            GUILayout.Space(10f * _scale);
            if (GUILayout.Button("Level Select", _buttonStyle, GUILayout.Height(btnH)))
                QuitToLevelSelect();
            // Dev tools live here now — the in-level HUD has no room on phone widths.
            if (_debugHud != null)
            {
                GUILayout.Space(10f * _scale);
                if (GUILayout.Button("Debug Tools", _tabStyle, GUILayout.Height(btnH)))
                {
                    _debugHud.Visible = !_debugHud.Visible;
                    Resume(); // the harness is unusable behind a frozen pause scrim
                }
            }
            EndPanel();
        }

        private void DrawEndOverlay()
        {
            _modalOpen = true;
            bool won = _manager.State.State == GameState.Won;
            float openT = Mathf.Clamp01(
                (Time.unscaledTime - _endSeenAt - EndOverlayDelaySeconds) / PanelPopSeconds);
            // Scrim fades in with the panel pop — teal dim, not a blackout.
            FillScreen(new Color(0f, 0.20f, 0.30f, 0.45f * openT));

            int idx = CurrentLevelIndex();
            bool hasNext = won && idx >= 0 && idx + 1 < _playingSection.Levels.Count;
            // Finishing the tutorial hands the player over to the main game.
            bool introFinished = won && !hasNext && idx >= 0 &&
                                 _playingSection == _sections[0] && _sections.Length > 1;

            float w = Mathf.Min(340f * _scale, Screen.width - 60f);
            float btnH = 50f * _scale;
            int buttons = won ? ((hasNext || introFinished) ? 3 : 2) : 2;
            float starRowH = won ? 88f * _scale : 0f; // stars + target line + pearls line
            float bonusRowH = won && _endShotsLeft > 0 ? 22f * _scale : 0f;
            float fishRowH = BubbleArt.Get(won ? "fish_orange" : "fish_blue") != null ? 54f * _scale : 0f;
            BeginPanel(w, 168f * _scale + starRowH + bonusRowH + fishRowH
                          + buttons * (btnH + 10f * _scale), openT);
            string loseTitle = _manager.PressureLoss ? "THE TIDE ROSE!" : "OUT OF SHOTS";
            string loseSub = _manager.PressureLoss ? "The reef crossed the danger line."
                                                   : "The bubbles won this round.";
            DrawEndFish(won);
            GUILayout.Label(won ? "LEVEL CLEAR!" : loseTitle, _overlayTitleStyle);
            GUILayout.Label(won ? "The reef breathes again." : loseSub, _subtitleStyle);
            GUILayout.Space(6f * _scale);

            // Banked-shot bonus counts up live: +50 per unused shot ticks into the score,
            // and the star pips upgrade the moment the running total crosses a target.
            int displayScore = _finalScore;
            int bonusRemaining = 0;
            if (won && _endShotsLeft > 0)
            {
                float elapsed = Time.unscaledTime - (_endSeenAt + EndOverlayDelaySeconds);
                int steps = Mathf.Clamp((int)(elapsed / 0.15f), 0, _endShotsLeft);
                bonusRemaining = _endShotsLeft - steps;
                displayScore = _finalScore - bonusRemaining * ScoreKeeper.ClearBonusPerShot;
            }

            if (won)
            {
                Rect starRect = GUILayoutUtility.GetRect(1f, 42f * _scale, GUILayout.ExpandWidth(true));
                int liveStars = Mathf.Max(1, Stars.Compute(_manager.CurrentLayout, displayScore));
                DrawStars(starRect, liveStars, 28f * _scale);
                int t2 = Stars.Target(_manager.CurrentLayout, 2);
                int t3 = Stars.Target(_manager.CurrentLayout, 3);
                if (t3 > 0)
                    GUILayout.Label($"Targets   2-star {t2}  ·  3-star {t3}", _subtitleStyle);
            }

            GUILayout.Label($"Score   {displayScore}", _overlayTitleStyle);
            if (won && _endShotsLeft > 0)
                GUILayout.Label(bonusRemaining > 0
                                    ? $"Banked shots   +{ScoreKeeper.ClearBonusPerShot} × {bonusRemaining}"
                                    : $"Banked shots paid   +{_endShotsLeft * ScoreKeeper.ClearBonusPerShot}!",
                                _subtitleStyle);
            string levelName = _manager.CurrentLayout != null ? _manager.CurrentLayout.Name : "";
            int best = HighScores.Get(levelName);
            if (won && _newBest)
                GUILayout.Label("NEW BEST!", _subtitleStyle);
            else if (best > 0)
                GUILayout.Label(won ? $"Best  {best}" : $"Best (cleared)  {best}", _subtitleStyle);
            if (won && _pearlsEarned > 0)
                GUILayout.Label($"+{_pearlsEarned} pearls for My Reef", _subtitleStyle);
            GUILayout.Space(12f * _scale);

            if (hasNext && GUILayout.Button("Next Level", _buttonStyle, GUILayout.Height(btnH)))
            {
                StartLevel(_playingSection, _playingSection.Levels[idx + 1]);
                EndPanel();
                return;
            }
            if (introFinished && GUILayout.Button("Start the Adventure!", _buttonStyle, GUILayout.Height(btnH)))
            {
                QuitToLevelSelect(1); // land on the Adventure tab, centered on its frontier
                EndPanel();
                return;
            }
            if (hasNext || introFinished) GUILayout.Space(10f * _scale);

            if (GUILayout.Button(won ? "Play Again" : "Retry", _buttonStyle, GUILayout.Height(btnH)))
            {
                RestartLevel();
                EndPanel();
                return;
            }
            GUILayout.Space(10f * _scale);
            if (GUILayout.Button("Level Select", _buttonStyle, GUILayout.Height(btnH)))
                QuitToLevelSelect();
            EndPanel();
        }
    }
}
