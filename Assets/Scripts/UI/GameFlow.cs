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

        // End-of-level overlay appears a beat after the state flips so the player sees the
        // final cascade land instead of an instant curtain.
        private const float EndOverlayDelaySeconds = 0.8f;
        private bool _endSeen;
        private float _endSeenAt;
        private int _finalScore;
        private bool _newBest;
        private int _earnedStars;
        private int _endShotsLeft;   // banked shots: their +50s count up on the win screen
        private float _splashUntil;  // start-of-level target splash (tap or first shot skips)
        private int _pearlsEarned;   // aquarium currency granted by this win

        // My Reef aquarium tab (roadmap step 5) — purely cosmetic, see ReefStore.
        private bool _reefTab;
        private bool _shopOpen;
        private Vector2 _shopScroll;
        private bool _goldenUnlocked, _pearlEelUnlocked; // refreshed on tab entry

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
        private GUIStyle _titleStyle, _subtitleStyle, _buttonStyle, _barLabelStyle, _overlayTitleStyle;
        private GUIStyle _nodeStyle, _nodeBestStyle, _tabStyle, _tabActiveStyle, _panelStyle;
        private GUIStyle _meterLabelStyle, _shopSmallStyle;
        private float _scale;
        private bool _stylesReady;

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
                HandleMapDrag();
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
                if (_reefTab)
                    _shopScroll.y += dy;   // swipe scrolls the shop list instead of the map
                else if (_sections != null && Active.MapOffset >= 0f)
                    Active.MapOffset += dy;
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
            _splashUntil = Time.unscaledTime + 1.9f; // show the star targets up front
            _screen = FlowScreen.Playing;
        }

        private void Pause()
        {
            if (_paused) return;
            _paused = true;
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
            _splashUntil = Time.unscaledTime + 1.9f;
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
            Active.MapOffset = -1f; // re-center the map on the frontier
            _reefTab = false;       // land on the played section's map, not the aquarium
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

            var border = new RectOffset(20, 20, 20, 20); // matches RoundedRect's 9-slice corners

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(34 * _scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = DeepTeal;
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(15 * _scale),
                alignment = TextAnchor.MiddleCenter
            };
            _subtitleStyle.normal.textColor = SoftTeal;
            _overlayTitleStyle = new GUIStyle(_titleStyle) { fontSize = (int)(28 * _scale) };

            // Chunky coral buttons: fills are BAKED into the rounded textures (no
            // GUI.backgroundColor games), white bold text in every state.
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = (int)(18 * _scale),
                fontStyle = FontStyle.Bold,
                border = border
            };
            _buttonStyle.normal.background = PrimitiveSprites.RoundedRect(Coral);
            _buttonStyle.hover.background = PrimitiveSprites.RoundedRect(Coral);
            _buttonStyle.active.background = PrimitiveSprites.RoundedRect(CoralDark);
            _buttonStyle.focused.background = _buttonStyle.normal.background;
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.textColor = Color.white;
            _buttonStyle.active.textColor = Color.white;
            _buttonStyle.focused.textColor = Color.white;

            _tabStyle = new GUIStyle(_buttonStyle);
            _tabStyle.normal.background = PrimitiveSprites.RoundedRect(new Color(1f, 1f, 1f, 0.38f));
            _tabStyle.hover.background = _tabStyle.normal.background;
            _tabStyle.active.background = _tabStyle.normal.background;
            _tabStyle.focused.background = _tabStyle.normal.background;
            _tabStyle.normal.textColor = DeepTeal;
            _tabStyle.hover.textColor = DeepTeal;
            _tabStyle.active.textColor = DeepTeal;
            _tabStyle.focused.textColor = DeepTeal;

            _tabActiveStyle = new GUIStyle(_tabStyle);
            _tabActiveStyle.normal.background = PrimitiveSprites.RoundedRect(Sunshine);
            _tabActiveStyle.hover.background = _tabActiveStyle.normal.background;
            _tabActiveStyle.active.background = _tabActiveStyle.normal.background;
            _tabActiveStyle.focused.background = _tabActiveStyle.normal.background;

            _barLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(16 * _scale),
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };
            _barLabelStyle.normal.textColor = DeepTeal;

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

            _meterLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(13 * _scale),
                alignment = TextAnchor.MiddleRight
            };
            _meterLabelStyle.normal.textColor = DeepTeal;

            _shopSmallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(12 * _scale),
                alignment = TextAnchor.MiddleLeft
            };
            _shopSmallStyle.normal.textColor = SoftTeal;

            // Light rounded panel for pause/win/lose (text on it is deep teal).
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                border = border,
                padding = new RectOffset((int)(22 * _scale), (int)(22 * _scale),
                                         (int)(18 * _scale), (int)(18 * _scale))
            };
            _panelStyle.normal.background = PrimitiveSprites.RoundedRect(PanelFill);

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

            if (!_paused && !_endSeen && Time.unscaledTime < _splashUntil)
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

        // ---- Overlay panel plumbing ----------------------------------------------------------
        // Heights are estimates, so the panel budgets for its own padding plus slack, clamps
        // to the screen, and wraps content in a scroll view (inert while everything fits).

        private void BeginPanel(float width, float contentHeight)
        {
            float h = contentHeight + _panelStyle.padding.top + _panelStyle.padding.bottom + 10f * _scale;
            h = Mathf.Min(h, Screen.height - 30f);
            GUILayout.BeginArea(CenteredColumn(width, h), _panelStyle);
            _overlayScroll = GUILayout.BeginScrollView(_overlayScroll);
        }

        private void EndPanel()
        {
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Draws a centered row of 3 star pips (gold = earned, faint = not). Uses the shared
        /// primitive circle texture — no font glyph dependence, works everywhere.
        /// </summary>
        private void DrawStars(Rect area, int stars, float size)
        {
            Texture tex = PrimitiveSprites.GlossyOrb().texture; // gold pearls, not flat dots
            float gap = size * 0.4f;
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

            // Fixed header.
            float headerH = 88f * _scale;
            GUILayout.BeginArea(new Rect(0, 10f * _scale, Screen.width, headerH));
            GUILayout.Label("CORAL CASCADE", _titleStyle);
            GUILayout.Label("Swipe the reef path — tap a level to dive in", _subtitleStyle);
            GUILayout.EndArea();

            // Pearl balance chip (top-right of the header).
            float chipH = 30f * _scale;
            var chipRect = new Rect(Screen.width - 128f * _scale, 12f * _scale, 118f * _scale, chipH);
            GUI.DrawTexture(chipRect, PrimitiveSprites.RoundedRect(new Color(1f, 1f, 1f, 0.40f)));
            float pearlIcon = chipH - 10f * _scale;
            var pipOld = GUI.color;
            GUI.color = new Color(0.98f, 0.93f, 0.82f);
            GUI.DrawTexture(new Rect(chipRect.x + 7f * _scale, chipRect.y + 5f * _scale, pearlIcon, pearlIcon),
                            PrimitiveSprites.GlossyOrb().texture);
            GUI.color = pipOld;
            GUI.Label(new Rect(chipRect.x + pearlIcon + 12f * _scale, chipRect.y,
                               chipRect.width - pearlIcon - 14f * _scale, chipRect.height),
                      Pearls.Balance.ToString(), _barLabelStyle);

            // Tabs: Tutorial Reef / Adventure (separate progressions) / My Reef (aquarium).
            float tabH = 44f * _scale;
            float gap = 6f * _scale;
            int tabCount = _sections.Length + 1;
            float tabW = Mathf.Min(160f * _scale,
                                   (Screen.width - 24f * _scale - gap * (tabCount - 1)) / tabCount);
            float tabX0 = (Screen.width - (tabW * tabCount + gap * (tabCount - 1))) * 0.5f;
            for (int s = 0; s < tabCount; s++)
            {
                var tabRect = new Rect(tabX0 + s * (tabW + gap), headerH + 12f * _scale, tabW, tabH);
                bool isReefTab = s == tabCount - 1;
                bool selected = _reefTab ? isReefTab : (!isReefTab && s == _sectionIndex);
                string label = isReefTab ? "My Reef" : _sections[s].Title;
                if (GUI.Button(tabRect, label, selected ? _tabActiveStyle : _tabStyle))
                {
                    if (isReefTab)
                    {
                        _reefTab = true;
                        _shopOpen = false;
                        _goldenUnlocked = ReefStore.GoldenPufferUnlocked();
                        _pearlEelUnlocked = ReefStore.PearlEelUnlocked();
                    }
                    else
                    {
                        _reefTab = false;
                        _sectionIndex = s;
                    }
                }
            }

            // Scrollable path (or the aquarium) below the tabs.
            float mapTop = headerH + tabH + 20f * _scale;
            var mapRect = new Rect(0, mapTop, Screen.width, Screen.height - mapTop);
            if (_reefTab)
            {
                DrawReefTank(mapRect);
                return;
            }
            float spacing = 112f * _scale;
            float nodeSize = 68f * _scale;
            float basePad = 70f * _scale;
            var section = Active;
            int n = section.Levels.Count;
            float contentH = basePad + (n - 1) * spacing + nodeSize;
            float maxOffset = Mathf.Max(0f, contentH - mapRect.height);

            // Mouse-wheel support for the editor; swiping is handled in HandleMapDrag.
            if (Event.current.type == EventType.ScrollWheel && section.MapOffset >= 0f)
            {
                section.MapOffset += Event.current.delta.y * 24f * _scale;
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
                if (GUI.Button(rect, unlocked ? levelNumber.ToString() : "-", _nodeStyle))
                {
                    GUI.enabled = true;
                    GUI.backgroundColor = oldBg;
                    GUI.EndGroup();
                    StartLevel(section, section.Levels[i]);
                    return;
                }

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

            // Plants and vents live on the sand; fish swim the open water above it.
            foreach (var item in ReefStore.Catalog)
            {
                int n = ReefStore.Count(item.Id);
                owned += n;
                for (int k = 0; k < n; k++)
                {
                    int h = ReefHash(item.Id, k);
                    float fx = area.x + 30f * _scale + Frac(h * 0.618034f) * (area.width - 90f * _scale);
                    switch (item.Kind)
                    {
                        case ReefItemKind.Plant:
                        {
                            var s = BubbleArt.Get(item.SpriteName);
                            if (s == null) break;
                            float w = 58f * _scale * item.SizeMul;
                            float hgt = w * (s.rect.height / s.rect.width);
                            float sway = Mathf.Sin(t * 0.9f + h) * 3f * _scale;
                            DrawSpriteGUI(new Rect(fx + sway, sandTop - hgt + 6f * _scale, w, hgt),
                                          s, (h & 2) == 0, item.Tint);
                            break;
                        }
                        case ReefItemKind.Vent:
                        {
                            var ring = BubbleArt.Get("bubble_c");
                            if (ring == null) break;
                            for (int j = 0; j < 3; j++)
                            {
                                float prog = Frac(t * 0.16f + j / 3f + Frac(h * 0.377f));
                                float size = (9f + 5f * j) * _scale;
                                float y = Mathf.Lerp(sandTop, area.y + 40f * _scale, prog);
                                DrawSpriteGUI(new Rect(fx + Mathf.Sin(t + j + h) * 5f * _scale, y, size, size),
                                              ring, false, new Color(1f, 1f, 1f, 0.7f * (1f - prog)));
                            }
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

            // Header row: shop toggle + rare progress.
            var shopBtn = new Rect(area.x + 12f * _scale, area.y + 8f * _scale,
                                   132f * _scale, 40f * _scale);
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

            if (_shopOpen)
                DrawShopPanel(area);
        }

        private void DrawTankFish(Rect area, float sandTop, ReefItem item, int instance,
                                  float growth, float t, int h)
        {
            var s = BubbleArt.Get(item.SpriteName);
            if (s == null) return;
            float w = 54f * _scale * item.SizeMul * growth;
            float hgt = w * (s.rect.height / s.rect.width);

            float laneTop = area.y + 64f * _scale;
            float laneBottom = sandTop - 40f * _scale - hgt;
            float laneY = Mathf.Lerp(laneTop, Mathf.Max(laneTop, laneBottom), Frac(h * 0.7548f));

            float span = Mathf.Max(40f * _scale, area.width - w - 40f * _scale);
            float speed = (26f + (h & 31)) * _scale; // px/s, per-fish
            float k = t * speed + (h & 1023);
            float px = Mathf.PingPong(k, span);
            bool movingRight = ((int)(k / span) & 1) == 0;
            float bob = Mathf.Sin(t * 1.9f + h) * 6f * _scale;

            DrawSpriteGUI(new Rect(area.x + 20f * _scale + px, laneY + bob, w, hgt),
                          s, !movingRight, item.Tint); // pack fish face right natively
        }

        private void DrawShopPanel(Rect area)
        {
            float w = Mathf.Min(430f * _scale, Screen.width - 36f);
            float h = Mathf.Min(area.height - 16f * _scale, 470f * _scale);
            var rect = new Rect((Screen.width - w) * 0.5f, area.y + 56f * _scale, w,
                                Mathf.Min(h, area.height - 64f * _scale));
            GUILayout.BeginArea(rect, _panelStyle);
            GUILayout.Label($"Reef Shop   —   {Pearls.Balance} pearls", _overlayTitleStyle);
            _shopScroll = GUILayout.BeginScrollView(_shopScroll);
            foreach (var item in ReefStore.Catalog)
            {
                GUILayout.BeginHorizontal(GUILayout.Height(46f * _scale));
                var iconRect = GUILayoutUtility.GetRect(42f * _scale, 42f * _scale,
                                                        GUILayout.Width(42f * _scale));
                var sprite = BubbleArt.Get(item.SpriteName);
                if (sprite != null)
                    DrawSpriteGUI(FitRect(iconRect, sprite), sprite, false, item.Tint);
                GUILayout.Space(8f * _scale);
                GUILayout.BeginVertical();
                GUILayout.Label(item.DisplayName, _barLabelStyle);
                GUILayout.Label($"owned {ReefStore.Count(item.Id)}", _shopSmallStyle);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUI.enabled = Pearls.Balance >= item.Price;
                if (GUILayout.Button($"Buy  {item.Price}", _buttonStyle,
                                     GUILayout.Width(104f * _scale), GUILayout.Height(38f * _scale)))
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
            // Frosted light bar over the bright water (deep-teal text sits on it).
            var oldBarColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.40f);
            GUI.DrawTexture(_topBarRect, _whiteTex);
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(0, h - 2f, Screen.width, 2f), _whiteTex);
            GUI.color = oldBarColor;

            float pad = 10f * _scale;
            GUILayout.BeginArea(new Rect(pad, 0, Screen.width - pad * 2f, row1H));
            GUILayout.BeginHorizontal(GUILayout.Height(row1H));

            int idx = CurrentLevelIndex();
            string name = _manager.CurrentLayout != null ? _manager.CurrentLayout.Name : "?";
            GUILayout.Label(idx >= 0 ? $"<b>{_playingSection.NodePrefix} {idx + 1}</b>  {name}" : $"<b>{name}</b>",
                            _barLabelStyle, GUILayout.ExpandHeight(true));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Score  <b>{_manager.Score.Total}</b>",
                            _barLabelStyle, GUILayout.ExpandHeight(true));
            GUILayout.Space(12f * _scale);
            GUILayout.Label($"Shots  <b>{_manager.State.ShotsRemaining}</b>",
                            _barLabelStyle, GUILayout.ExpandHeight(true));
            if (_manager.PressureActive)
            {
                GUILayout.Space(12f * _scale);
                GUILayout.Label($"Tide in  <b>{_manager.ShotsUntilPressure}</b>",
                                _barLabelStyle, GUILayout.ExpandHeight(true));
            }
            GUILayout.Space(12f * _scale);

            float btnH = row1H - 12f * _scale;
            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (_debugHud != null &&
                GUILayout.Button("Debug", _buttonStyle, GUILayout.Height(btnH), GUILayout.Width(btnH * 1.9f)))
                _debugHud.Visible = !_debugHud.Visible;
            GUILayout.Space(6f * _scale);
            if (GUILayout.Button("Pause", _buttonStyle, GUILayout.Height(btnH), GUILayout.Width(btnH * 1.9f)))
                Pause();
            GUILayout.EndHorizontal();
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
            float size = 15f * _scale;
            if (lit)
            {
                float k = Mathf.Max(0f, 1f - (Time.unscaledTime - litAt) / 0.45f);
                size *= 1f + 0.7f * k; // pop the moment it's earned
            }
            GUI.color = lit ? new Color(1f, 0.8f, 0.15f) : new Color(1f, 1f, 1f, 0.6f);
            var c = new Vector2(bar.x + bar.width * frac, bar.y + bar.height * 0.5f);
            GUI.DrawTexture(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size),
                            PrimitiveSprites.GlossyOrb().texture);
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
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, fade);
            float w = Mathf.Min(360f * _scale, Screen.width - 80f);
            float h = 108f * _scale;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.26f, w, h);
            GUI.Box(rect, GUIContent.none, _panelStyle);
            GUI.Label(new Rect(rect.x, rect.y + 10f * _scale, rect.width, 34f * _scale),
                      layout != null ? layout.Name : "", _overlayTitleStyle);
            GUI.Label(new Rect(rect.x, rect.y + 50f * _scale, rect.width, 24f * _scale),
                      $"2-star {t2}   ·   3-star {t3}", _subtitleStyle);
            GUI.Label(new Rect(rect.x, rect.y + 74f * _scale, rect.width, 22f * _scale),
                      "Clear the reef to earn your first star!", _subtitleStyle);
            GUI.color = old;
        }

        private void DrawPauseOverlay()
        {
            _modalOpen = true;
            FillScreen(new Color(0f, 0.20f, 0.30f, 0.45f)); // teal dim, not a blackout

            float w = Mathf.Min(340f * _scale, Screen.width - 60f);
            float btnH = 50f * _scale;
            BeginPanel(w, 58f * _scale + 3f * (btnH + 10f * _scale));
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
            EndPanel();
        }

        private void DrawEndOverlay()
        {
            _modalOpen = true;
            bool won = _manager.State.State == GameState.Won;
            FillScreen(new Color(0f, 0.20f, 0.30f, 0.45f)); // teal dim, not a blackout

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
            BeginPanel(w, 168f * _scale + starRowH + bonusRowH + buttons * (btnH + 10f * _scale));
            string loseTitle = _manager.PressureLoss ? "THE TIDE ROSE!" : "OUT OF SHOTS";
            string loseSub = _manager.PressureLoss ? "The reef crossed the danger line."
                                                   : "The bubbles won this round.";
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
