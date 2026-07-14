# Coral Cascade — Phase 1 Prototype

De-risks the core loop and the **fairness boundary** before any art, ads, or meta exist.
Everything is primitive shapes, built in code. No imported assets required.

## How to run

1. Open the project in Unity **6000.4.1f1**.
2. Open scene **`Assets/Scenes/Phase1Prototype.unity`**.
3. Press **Play**. `GameBootstrap` builds the camera, walls, launcher, board and UI, and the
   game opens on the **level-select screen**.
4. Pick a level, then **drag to aim, release to fire.** In-level UI is a clean top bar
   (level name, shots, **Debug**, **Pause**) — the Phase 1 harness panel now lives behind
   the **Debug** button instead of being permanently on screen.
5. The smaller bubble beside the launcher is the **next in queue — tap it to swap** it with
   the loaded bubble.

> Set the Game view to a **portrait** resolution (e.g. 1080×2340 / 9:19.5) for correct framing.

## Flagged design decisions (spec left these to us)

- **Hex `odd-r` offset grid** (not square). Bubbles pack hexagonally with 6 neighbors — the
  genre standard, and it produces far better cascades. Cost: neighbor offsets differ per
  even/odd row, handled once in `HexGrid`. Square would be simpler but reads wrong.
- **Win = board cleared; Lose = shots hit 0.** Both flagged `PLACEHOLDER` in `GameStateController`.
- **Runtime bootstrap.** `GameBootstrap` constructs the whole scene so nothing depends on
  fragile inspector wiring — only one component reference in the scene.
- **New Input System** used directly (project has legacy input disabled: `activeInputHandler: 1`).
- **Unlit sprite material** forced everywhere so primitive bubbles are visible without a Light2D.

## The fairness boundary (game plan §2 / §10 — the whole point of Phase 1)

The aimed shot is **fully deterministic**: `TrajectoryCalculator` resolves the reflected path
and attach cell with kinematic circle-casts + grid geometry only — no Rigidbody, no randomness.
Physics (`FallingBubble` / `CascadeController`) only ever takes over **after** a bubble has
already detached. Nothing in the physics layer can change which bubbles matched or where a
shot attached. This ordering is enforced in `BoardManager.CommitAttach`.

**You can fire while a cascade is still falling.** Firing is only locked during the fired
bubble's ~0.3s flight, not while debris tumbles. This is safe for fairness because falling
debris is *invisible* to the aim: it lives on the built-in Ignore Raycast layer and the
trajectory cast filters to the static world (walls + attached bubbles) — an in-progress
cascade can never bend a shot. Cascade counters and the slow-mo beat track a **burst**
(first detachment after quiet → last debris gone), so overlapping shots read as one avalanche.

Win/lose timing under concurrency: **win** is evaluated on every board mutation (a secondary
chain can clear the board mid-fall); **lose** only once the table is quiet — no shot in
flight, nothing falling — so a cascade that would clear the board is never beaten to the
punch by a premature "Lost".

## Game flow (`UI/GameFlow.cs`)

Genre-standard mobile loop, all IMGUI (no assets):

- **Level map** opens first, with TWO tabs — **Tutorial Reef** (the 5 intro boards) and
  **Adventure** (the main game, 30 generated levels) — each a candy-crush-style winding
  path with its **own independent progression**. **Swipe up/down** (or mouse-wheel) to
  scroll a path. Nodes unlock sequentially per section; cleared nodes are green and show
  their best score, the frontier node glows, locked nodes are grey. The map opens on the
  Tutorial until it's finished, then defaults to the Adventure; clearing the last tutorial
  board offers a "Start the Adventure!" hand-off.
- **Pause** is a real freeze: `Time.timeScale = 0`. Every moving piece (projectile, debris,
  lifetimes) runs on scaled time, so that alone stops the world; the slow-mo beat — the one
  unscaled-time animation — explicitly holds while paused so it can't ramp the freeze away.
  Overlay: Resume / Restart Level / Level Select.
- **Win/lose overlay** appears ~0.8 s after the state flips so the final cascade lands on
  screen first. Won → Next Level / Play Again / Level Select; Lost → Retry / Level Select.
- The launcher is inert outside active gameplay (menus, pause, overlays), and presses on the
  top bar / debug panel never double as aim input (`GameFlow.IsPointerOverUI` +
  `DebugHUD.IsPointerOverHUD`).

## Color queue (fair randomizer + swap)

- The launcher shows the **next** bubble beside the loaded one; **tap it to swap** the two
  (any time during play, even while a shot is in flight — a queued color is not consumed
  until fired).
- Colors are drawn **only from colors still attached to the board**, and a queued color is
  **re-rolled the moment its color leaves the board** (last cluster matched or dropped by a
  cascade). A pure 6-color randomizer could hand out unmatchable colors late in a level and
  make it uncleaable — this is the genre-standard fix.
- Fairness boundary untouched: the fired color is an *input* to the deterministic shot and
  is recorded in `LastShot`, so **Replay Last Shot** still reproduces the identical outcome.
- Mechanism: `Board.Version` bumps on every occupancy mutation (load / attach / detach);
  the launcher re-checks its queue only when the version changes.

## Prompt → file map & acceptance checks

| Prompt | Key files | How to verify |
|---|---|---|
| 1 Project setup | `GameBootstrap.cs`, `Scenes/Phase1Prototype.unity` | Scene runs, no console errors |
| 2 Grid/board data | `HexGrid`, `BoardCell`, `Board`, `Data/BoardLayout` (ScriptableObject), `Data/TestBoards`, `Debug/GridGizmoRenderer` | Load a test board — it renders |
| 3 Deterministic shoot | `Shooter/Launcher`, `Shooter/TrajectoryCalculator`, `Shooter/Projectile`, `Shooter/PointerInput` | **Replay Last Shot** logs the *same* `attach=(col,row)` every press |
| 4 Match detection | `Board.FindColorCluster`, `Board.FindFloatingClusters` | Console logs `[Match] cluster of N` and floating groups |
| 5 Physics cascade | `Physics/CascadeController`, `Physics/FallingBubble` | Load **Overhang**, pop the support → emergent collapse. Toggle physics off to A/B vs scripted clear |
| 6 Slow-mo beat | `CascadeController.SlowMoBeat` | Load **BigCascade** → large pop dips `TimeScale` to 0.3 briefly; a 3-match does not |
| 7 Win/lose stub | `GameStateController` | Clear a board → `Won`; run out of shots → `Lost` (HUD shows state) |
| 8 Test harness | `Debug/DebugHUD` | Top bar ▸ **Debug** — load boards / replay / toggle / live counter without recompiling |

## Scoring & high scores

Cascade play scores far more than plain matching (`ScoreKeeper` in `Data/Scoring.cs`) —
that's the strategic layer: cut supports and set up chain impacts instead of spraying pops.

| Event | Points |
|---|---|
| Matched pop | 10 / bubble |
| Dropped (support cut, never matched) | 20 / bubble |
| Knocked loose by falling impact (secondary chain) | 40 / bubble |
| Level clear bonus | +50 per unused shot |

Live score sits in the top bar. Per-level **best scores persist via PlayerPrefs**
(`HighScores`) and show on the map nodes and the win/lose overlay — but only a
**cleared** level can set a best, so cascade-farming a level you don't finish banks nothing.

**Star ratings** (`Stars`): every win earns 1–3 ★, thresholds scaled by the level's starting
bubble count — 1★ = cleared, 2★ = score ≥ 18×bubbles (real drop play), 3★ = ≥ 28×bubbles
(heavy cascade play + banked shots). Best stars persist and show as gold pips on the win
overlay and under cleared map nodes. The two multipliers are the balance dials.

## Obstacle bubbles

- **Stone** (`'S'` in layouts, grey): can never be matched — remove it by CUTTING ITS
  SUPPORT or knocking it loose with falling debris. Forces cascade play. Stones are never
  authored on row 0 (a ceiling stone could never be removed). Excluded from the color queue.
- **Ice** (lowercase color char, e.g. `'b'` = frozen Blue, rendered washed-out): keeps its
  color but is NOT matchable until it thaws. Popping any cluster ADJACENT to ice thaws it.
  You can always thaw strategically by building your own cluster beside the ice. Thawing is
  deterministic (resolved at match time, before physics) — fairness boundary untouched.
- Taught by **Stonefall** (Intro 6) and **Icebreaker** (Intro 7); the Adventure generator
  sprinkles stones from ~Reef 9 and ice from ~Reef 13, ramping to at most ~10% of cells.

## Levels — two sections (`Data/LevelCatalog.cs`)

- **Tutorial Reef (Intro 1–5)**: the hand-authored boards below (`TestBoards.cs`) — they
  teach drops, overhangs, bridges, fortresses and chain reactors in order, and still
  double as the Phase 1 acceptance boards.
- **Adventure (Level 1–30, "Reef N")**: the main game — **deterministically generated**;
  the level number is the seed, so every visit serves the identical board (fair replays,
  comparable bests). Difficulty ramps: 9→13 columns, 6→11 rows, 4→6 colors, rising
  density, shot budgets derived from bubble count (Reef 1: ~21 bubbles/10 shots → Reef 30:
  80 bubbles/30 shots). Generated boards are anchored **by construction** (a bubble is
  only placed with an occupied up-neighbor), and the full set is additionally BFS-verified.
- **Progression** (`Progress` in `Data/Scoring.cs`): per-section chains — clearing level N
  unlocks N+1 within that section only, persisted in PlayerPrefs alongside high scores.

**The play area widens as levels go up** — each layout declares its own column count
and the whole world (grid, walls, camera, launcher) rebuilds to fit
(`GameBootstrap.ConfigureWorld`, invoked by `BoardManager.LoadLayout` on width change).
Every level also carries its own shot budget — the other difficulty dial.

- **Simple** (8 cols, 14 shots) – teaches the drop economy: the B and Y columns each carry
  a different-colored pair at the tip — pop the column, drop the pair for 2× points
- **Overhang** (9 cols, 15) – Prompt 5 acceptance: the whole P web and the B mass at its
  tip hang from the single P at (4,1); pop into the P cluster and everything collapses
- **FloatingTrap** (10 cols, 16) – the BRIDGE puzzle: Y thread (left) and B thread (right)
  hold one linked mass (the R blob bridges into the P blob). Cutting one thread drops
  nothing — the mass re-hangs off the other thread; cutting the second releases
  everything at once for a huge drop payout
- **BigCascade** (11 cols, 18) – Prompt 6 slow-mo acceptance: dense fortress; the
  ~20-bubble Green ring pops in one hit from below, dropping the sealed Red core. The
  roof is segmented 3-color runs so no single lucky shot clears it
- **ChainReactor** (12 cols, 16) – DOUBLE chain reactor: a 12-bubble B block free-falls
  ~7 rows between TWO shelves (O left, Y right); the detach kick spreads the mass onto
  both → secondary chains on both sides at 4× points

All five are validated by an odd-r connectivity check (at each board's own width): no
bubble floats at load. Re-run it whenever a layout changes.

## Tunables (on the `Systems` object's `CascadeController` at runtime, or in code)

- `ImpactThreshold` (6.0) – approach speed **along the contact normal** needed for falling
  debris to dislodge an attached bubble (~3 rows of free fall; grazes don't count)
- `SlowMoBubbleThreshold` (15) / `SlowMoSecondaryThreshold` (2) / `SlowMoScale` (0.3) / `SlowMoHoldSeconds`
- `GameBootstrap`: `Columns` (11), `GridRows`, `BubbleDiameter`, `ShotsPerLevel` (fallback
  only — each level's `BoardLayoutData.Shots` overrides it)

## Not in Phase 1 (per spec)

No aquarium/meta, no ads, no analytics, no real content. Those are Phase 2/3.

## Note on the ScriptableObject path

`Data/BoardLayout` is a real `CreateAssetMenu` ScriptableObject (Create ▸ Coral Cascade ▸
Board Layout) so levels can be authored as assets. The board consumes the plain
`BoardLayoutData`, so code test boards and future `.asset` levels share one code path — the
"content is data, not logic" principle from the game plan.
