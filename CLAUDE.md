# Coral Cascade — Project Context

Mobile bubble shooter (Unity 6000.4.1f1, 2D URP, **new Input System only** — legacy `Input` throws).
Ads + one remove-ads IAP; physics-driven cascades + reef meta. Full plan: `CoralCascade_game_plan.md`.

## Status (as of 2026-07-14)

**Where we are:** Roadmap steps 1 (stars), 2 (stone/ice obstacles), and 3 (descending
pressure) DONE — step 3 finished 2026-07-14, awaiting user play-test (RisingTide, Intro 8,
is the acceptance board). **Steps 4 (Daily Reef) and 5 (My Reef aquarium) BOTH BUILT
2026-07-14 — the numbered roadmap is COMPLETE; next work comes from the backlog below,
user-picked.** Session mode: implement ONE step, stop, summarize, wait for go. 2026-07-14 also:
art direction agreed + **gameplay bubble art integrated** (see "Art direction" below —
`BubbleArt.cs`, Kenney Fish Pack under `Assets/Resources/Art/Double`) + **sunlit UI/backdrop
pass** (user: game felt "dark and depressing"; all 4 proposed fixes approved & built —
sunlit color flip, `ReefBackdrop.cs` living decor, bright IMGUI skin, lagoon map screen).
All compile-verified (25 files).
**Next: roadmap step 3 — descending pressure** (board pushes down every N shots, lose when
bubbles cross a danger line; replaces the placeholder lose rule), then step 4 Daily Reef.
**Version control moved to GitHub 2026-07-14:** everything through step 5 committed &
pushed to https://github.com/jhong03/CoralCascade.git (main, root commit 4ea08a0; Unity
.gitignore excludes Library/Temp/.plastic/csproj; commit from THIS git repo going forward —
the old .plastic UVC workspace still exists on disk but is gitignored and stale).


**Phase 1 (core-loop prototype) COMPLETE** — built, multi-agent reviewed (3 rounds), all fixes
applied, compiles clean, checked into Unity Version Control. See `PHASE1_README.md` for the
run guide, prompt→file map, and acceptance checks.

- Scene: `Assets/Scenes/Phase1Prototype.unity` — press Play; `GameBootstrap` builds everything at runtime
- Code: `Assets/Scripts/{Board,Shooter,Physics,Data,Debug,UI}` — no prefabs, no art, primitive sprites in code
- **Game flow added 2026-07-13** (`UI/GameFlow.cs`, IMGUI): opens on a level-select screen;
  in-level UI is a clean top bar (level, shots, Debug, Pause); pause = `timeScale 0` freeze
  (slow-mo beat explicitly holds while paused — it's the only unscaled-time animation);
  win/lose overlay 0.8s after state flip. Debug harness hidden behind the top bar's Debug
  button. Launcher inert outside active gameplay (`GameFlow.GameplayActive` / `IsPointerOverUI`).
- **Difficulty + scoring pass 2026-07-13** (user cleared all 5 levels in a minute):
  - **Variable-width levels:** each layout declares its own column count (8→12 ramp);
    `GameBootstrap.ConfigureWorld(columns)` rebuilds grid/board/walls/camera/launcher and
    `BoardManager.Rebind`s — invoked by `LoadLayout` on width change. Scene `Columns: 11`
    is only the pre-level default world.
  - Per-level shot budgets (`BoardLayoutData.Shots`, 14–18; `ShotsPerLevel=30` fallback).
  - **Scoring** (`Data/Scoring.cs`): matched 10 / dropped 20 / chain-knocked 40 per bubble,
    +50 per unused shot on clear. `BoardManager.Score` (ScoreKeeper), fed by
    CascadeController per detach cause. **High scores** persist in PlayerPrefs
    (`HighScores`, key `CoralCascade.Best.<LevelName>`) — submitted only on WIN (GameFlow).
  - Boards redesigned for cascade strategy (drop tips, two-thread bridge, double shelves);
    all re-verified anchored via the odd-r checker at each board's own width.
- **Level map + catalog 2026-07-13:** TWO sections with independent progressions —
  **Tutorial Reef** (the 5 TestBoards intros) and **Adventure** (the main game: 30 generated
  "Reef N" levels, seed = level number, deterministic; anchored BY CONSTRUCTION via the
  up-neighbor placement rule, plus BFS re-verified). Candy-crush-style swipeable paths with
  tab switcher (manual touch-drag in `GameFlow.HandleMapDrag`; >15px swipe suppresses node
  taps). Per-section unlock via `Progress` (PlayerPrefs `CoralCascade.Unlocked.<Section>`,
  sections "Intro"/"Main"); "cleared" = has a best score. Map opens on Tutorial until done,
  then Adventure; last tutorial win offers "Start the Adventure!". DebugHUD still lists only
  the 5 acceptance boards.
- **Color queue added 2026-07-13:** next bubble shown beside launcher, tap to swap. Randomizer
  draws only from colors still attached; queued colors re-roll when their color leaves the
  board (`Board.Version` bumps on load/attach/detach; launcher re-checks on change). Fired
  color is an input to the deterministic shot — replay unaffected.
  **Not yet checked into UVC** (nor is this CLAUDE.md) — check in at next session start.
- **Awaiting: human play-test of feel** (cascade payoff, aim responsiveness) via the debug HUD —
  that's the Phase 1 exit gate before Phase 2 (vertical slice: meta, 15-20 levels, share card, analytics)

## Roadmap (agreed with user 2026-07-13 — implement in order, STOP after each step)

Build order (mechanics only; game design/art/effects pass comes later, user-led):

1. **Star ratings** — ✅ DONE 2026-07-13. `Stars` in `Data/Scoring.cs`: 1★ clear /
   2★ ≥18×bubbles / 3★ ≥28×bubbles (`BoardLayoutData.BubbleCount`); best persisted at
   `CoralCascade.Stars.<LevelName>`; gold pips (orb texture, no font glyphs — LegacyRuntime
   has no ★) on win overlay + cleared map nodes via `GameFlow.DrawStars`. Multipliers are
   tunables. **In-game star-target meter added 2026-07-14** (`Stars.Target(layout, 2|3)` +
   `GameFlow.DrawStarMeter`): second top-bar row = progress bar toward 3★ with orb pips at
   the 2★/3★ marks (gold + pulse when crossed; pip state resets when score decreases =
   level reload), "next target" label, targets echoed on the win overlay. 1★ has no pip
   (it means CLEARED). Clear bonus lands at win, so a near-miss can still tip over on the
   end screen — intended (banked-shots payoff). The two-row bar (~76px·scale) is RESERVED
   in `GameBootstrap.ConfigureWorld` camera framing (84px·scale → world units above the
   ceiling) so it never overlaps the anchor rows — if the bar grows again, grow that
   constant too. Framing reads Screen.height at world-build time (not on window resize). **Score-legibility trio (same day, all 3
   user-approved):** floating score popups (`PopEffects.ScorePopup`, pooled TextMesh cap
   10, one AGGREGATED popup per cause per resolution at the detach centroid — white +10s /
   aqua +20s / gold +40s, amounts from ScoreKeeper consts; CascadeController keeps a
   detach-position tally for drop centroids); win-screen banked-shot COUNT-UP (+50 ticks
   every 0.15s into the displayed score, star pips upgrade live as targets cross —
   `_endShotsLeft` captured at state flip; the REAL score was submitted in full at flip,
   count-up is display only); start-of-level target splash (1.9s fading card, non-modal,
   no buttons, first touch dismisses — never blocks aiming).
2. **Obstacle bubbles** — ✅ DONE 2026-07-13. **Stone** = `BubbleColor.Stone`, char 'S',
   excluded from Playable/queue (`CollectColors` filters via `IsPlayable()`), unmatchable
   (never equals a fired color), drop/knock-only; NEVER author on row 0 (unremovable →
   unwinnable). **Ice** = `BoardCell.Frozen`, authored as lowercase color char ('b' = frozen
   Blue); match flood-fill skips frozen; thaw = `CascadeController.ThawNeighbors` on every
   matched pop (deterministic layer, pre-physics); frozen render = frost overlay via
   `BubbleArt.Apply` (primitive fallback: color lerped to icy white), refreshed by
   `BoardView.RefreshCell`. Ice is never softlockable (player can build own
   clusters to thaw); tutorial boards **Stonefall** (Intro 6) + **Icebreaker** (Intro 7);
   generator sprinkles: stones from Reef 9 (t≥7), ice from Reef 13 (t≥11), both ≤10%/cell,
   inheritance normalized so obstacles don't propagate through color clumping.
3. **Descending pressure** — ✅ DONE 2026-07-14. Every `BoardLayoutData.PressureEveryShots`
   COMMITTED shots the board shifts down one row + a deterministic ceiling row spawns
   (seed = FNV(layout.Name) ^ dropIndex*7919; colors only from the level's STARTING
   palette; ~85% fill; never stone/ice). **Parity trick (core):** `Board.ShiftDown` moves
   content r→r+1 AND `HexGrid.ToggleParityFlip()` — flip makes the lean swap sides so the
   shift is a RIGID translation (same x, all 6 neighbors preserved; ALL parity routes
   through `HexGrid.ParityIndex`; flip reset in `Board.LoadData`). Views rebuilt via
   `BoardView.RebuildAll`. **Lose**: bubbles at/below `DangerRow` (default `Rows-2`)
   AFTER the shot's full resolution — grace rule: a match that removes the crossing
   saves you; judged in `BoardManager.HandlePressureAfterShot` (also drop overflow off
   the grid = instant loss). Immediate, no quiescence — the crossing IS the failure
   (shots-out lose still exists and stays quiescence-gated). `PressureLoss` drives the
   "THE TIDE ROSE!" overlay; pulsing red `DangerLine` (Pixel sprite, order 5, marks the
   boundary ABOVE the forbidden row); "Tide in N" chip in the top bar; drop = shake +
   "THE TIDE RISES!" banner. Teaching board **RisingTide** (Intro 8, every 3 shots,
   danger row 5, budget 18 > tide allowance ⇒ the tide is the real constraint;
   anchoring machine-checked). Adventure: pressure from Reef 5 (t≥4), every
   max(5, 8−t/8) shots, dangerRow min(14, rows+max(3, 6−t/10)) — fields consume NO rng
   draws, so all existing Reef layouts are byte-identical.
4. **Daily Reef** — ✅ DONE 2026-07-14 (built AFTER step 5 by user's choice).
   `LevelCatalog.Daily(date)`: seed = dateKey^0x5EEF, difficulty t date-picked from 6–23
   (Reef 7–24 band, so days vary incl. obstacles+pressure); `Generate` refactored to
   `(name, seed, t)` — Reef wrapper passes the FROZEN original formulas, layouts
   byte-identical. Level name embeds the date ("Daily 2026-07-14") so per-day best/stars
   fall out of name-keyed persistence for free (pearls' first-clear bonus = daily income,
   intended). GameFlow: banner between tabs and map path (`DrawDailyBanner`/`EnsureDaily`,
   LOCAL date, regenerates at midnight rollover) — sunshine when unplayed, quiet + best +
   star pips when cleared. Daily is NOT in a section: CurrentLevelIndex()=-1 ⇒ no unlock
   chain, no Next Level button — by design.
5. **My Reef aquarium MVP** — ✅ DONE 2026-07-14 (user pulled it AHEAD of step 4).
   Spec section below is the design record; implementation: `Data/ReefStore.cs` (catalog
   13 items + rares + persistence + growth) + `Pearls` in `Data/Scoring.cs` + GameFlow
   "My Reef" third tab (IMGUI tank: `DrawReefTank`/`DrawTankFish`/`DrawShopPanel`;
   `DrawSpriteGUI` handles sprite SUB-RECT UVs + negative-width flip). Pearls awarded in
   the GameFlow win block (first-clear read BEFORE HighScores.Submit — order matters);
   balance chip on map header; swipe scrolls the shop when the reef tab is open. Tank is
   screen-space IMGUI (world untouched); rares derived from star records at tab entry.

Backlog after those (do NOT start without user): level objectives (score target / clear
stones / rescue-critters-by-drop — rescued critters should go INTO the aquarium),
cascade-earned power-up bubbles (bomb/rainbow — must stay skill-earned, not random, to
preserve fairness), endless mode (reuses pressure + generator), share card, analytics.

Standing caution: the Phase 1 exit gate (human play-test of cascade FEEL) is still pending —
feel tunables (ImpactThreshold, slow-mo, shot speed) may need revisiting before deep polish.

**Menu restructure (2026-07-14, user request):** LevelSelect is now PAGED
(`GameFlow.MenuPage`: Home / SectionMap / Reef) — Home = title + daily banner + big
section cards (with cleared counts; the section to chase glows sunshine) + My Reef card;
tapping a card opens that section's route page (Back button, title, winding path — map
drag only works here) or the aquarium page. The 3-tab row is GONE. Back-from-level lands
on the played section's ROUTE page (not Home). Rare-unlock refresh moved to the My Reef
card click.

**Balance pass 2026-07-14 (user: late reefs "impossible"; math agreed — Reef 30 needed a
sustained 5.2 net bubbles/shot):** (A) shot budget = 0.5/bubble × (1 + 12%/color beyond 4)
× (1 + 40% of tide influx/shot), cap 30→40; 5th/6th colors delayed to Reef 13/25; tide
slowed to every 10→8 shots; ceiling-row fill 0.85→0.7 (`BoardManager.PressureRowFill` —
LevelCatalog budgets ASSUME this value, change together). NOTE: colorCount threshold change
recolors some existing Reef layouts (structure/anchoring untouched — accepted). (B) cascade
SHOT REFUND: one shot removing ≥`BoardManager.RefundThreshold` (8) bubbles refunds itself
(+1 SHOT floating text at launcher; `Cascade.LastResolveDetached` = sync match+drops only,
async chain knocks excluded on purpose — they already pay 4×). Tide-relief option (big
clears delay the drop) was offered and declined for now.

## Art direction (agreed 2026-07-14; gameplay bubbles INTEGRATED same day)

Style: **flat cartoon, bold outlines** (reads at bubble size on phones). Source: **asset
packs first**; AI/custom art only to patch gaps.

**Integrated 2026-07-14:** Kenney Fish Pack imported by user; `Double` (2×, 128px) set
lives at `Assets/Resources/Art/Double` (MOVED there — runtime `Resources.LoadAll`; metas
moved with it so GUIDs survived), `Default` (1×) kept unused at `Assets/Art/Default`.
`Assets/Scripts/BubbleArt.cs` is the single builder for EVERY bubble visual (attached /
falling / projectile / launcher queue — all four sites call `BubbleArt.Apply(go, color,
frozen, baseOrder)`): **GLOSSY ORB** (user-chosen design 2026-07-14 from options A-D) =
`PrimitiveSprites.GlossyOrb()` (sphere-shaded WHITE orb, shading baked as luminance →
tints to any color) + "Gloss" child `PrimitiveSprites.OrbGloss()` (UNTINTED white
specular + bottom bounce arc — separate layer because multiply-tint can't make a
highlight whiter than the tint) + translucent circle "Frost" child when frozen +
`rock_a` sprite for Stone (no shell, no gloss — matte reads unmatchable). All
procedural — balls never require imported art. Map nodes + star pips also use the orb
texture (baked shading only; IMGUI can't layer the gloss). Colorblind shape-coding was
deliberately dropped with the critters — revisit if it becomes a complaint (option B
"orb + glyphs" was designed and is easy to add on top). Apply still strips legacy
"Critter" children on refresh.
**Sprite-loading lessons (cost a debugging round):** the pack's `bubble_a/b` are TINY
26px decor bubbles on a 128 canvas (as ball shells they stretched into blobs — never
reuse them for balls); several pack canvases auto-sliced into 2-3 sub-sprites (seaweed =
separate strands), so `BubbleArt.Load` picks the LARGEST slice, and normalizes by the
LONG side (width-normalizing made 30×98 kelp into 3.3-unit pillars — the user's "moving
cuboids"). ALSO: `fish_grey_long_a/b` are the two HALVES of one long fish (each hugs a
tile edge) — never draw either alone; `ReefItem.SpriteName2` + GameFlow's two-tile
rendering compose them (the eel), and they're excluded from ReefBackdrop's swimmer pool. Sprites are runtime-normalized to 1 world unit
(`Sprite.Create` with PPU = rect width) so import PPU never matters; collider contract
(local radius 0.5) unchanged. Primitive-circle fallback auto-engages if the art folder is
missing — the game never requires imported assets. Apply is idempotent and never touches
non-managed children (the launcher's SwapHint label).

**Pop effects (2026-07-14, user request "effects per combo"):** `PopEffects.cs` on Systems
(`PopEffects.Instance`, null-safe callers) — pooled sprite particles (cap 220, order 25,
scaled time, no colliders; fairness untouched: fired only from CascadeController AFTER
deterministic resolution). Tiers: MatchPop per bubble (ring+droplets, scales/golds with
cluster size; ≥7 also Shake), DropPuff (subtle — the fall is the show), KnockBurst +
ImpactWave+Shake on secondary chains, Celebration (22-bubble shower + big Shake) when the
slow-mo beat trips, ThawGlint per thawed ice. Camera shake in LateUpdate re-anchors every
frame (remove last offset → add new) so ConfigureWorld camera moves never corrupt; holds
frozen while paused. **Praise banners** (`PopEffects.Announce`, one slot — highest tier
must fire LAST): NICE POP! ≥6 match / BIG COMBO!! ≥9 / NICE CASCADE! ≥5 dropped /
CHAIN REACTION!→DOUBLE CHAIN!!→UNSTOPPABLE!!! per secondary chain / MEGA CASCADE!!! on
slow-mo trip; world-space TextMesh + shadow (LegacyRuntime font in try/catch), pop-in →
hold → fade-up, scaled time. Intensity pass (user: "more obvious"): shake mult 0.22,
decay 1.1, ImpactWave 0.25, Celebration 0.5; slow-mo deepened to scale 0.22 / hold 0.45 /
ramp 0.18; GameFlow draws a cyan bullet-time tint while timeScale < 0.85 (never while
paused).

**Sunlit UI pass (2026-07-14, user-approved A+B+C+D):** palette = "tropical lagoon at
noon", not "deep sea at night". `ReefBackdrop.cs` (rebuilt by `ConfigureWorld`, seeded
System.Random per width): water-gradient sprite (order -100), sand strip of terrain_sand_top_a tiles (-45), coral/
seaweed bed (8–16 plants, -40/-42, base-pivot holders sway ±3.5°), faint silhouettes
(-60), ~10 rising bubble_c rings (-20), 6 near-opaque ambient fish 0.55–1.0u across all
depths cycling the full pool (-30) — NO colliders, negative orders only, scaled time ⇒
fairness/pause untouched.
`PrimitiveSprites` gained `GradientTexture(top,bottom)` + `RoundedRect(color)` (cached,
9-slice border 20). GameFlow skin: palette consts (DeepTeal text / Coral buttons /
Sunshine active-tab+frontier / PanelFill light panels / MapTop-MapBottom gradient);
map = gradient + `DrawSunRays()` + circle-texture bubble path dots + circle-texture node
buttons (backgroundColor-tinted: green cleared / sunshine frontier w/ breathing pulse /
aqua unlocked / pale locked); top bar = frosted translucent white + deep-teal text;
overlays = teal-dim scrim (NOT black) + light rounded `_panelStyle`. Camera fallback color
0.09/0.50/0.70 (code AND scene — scene serialized value overrides code, keep in sync).
BubbleArt gained public `Get(name)` for decor loading. Star pips off-state = dark faint
(`StarOff`) so they read on both bright map and light panels. Overlay panels go through
`BeginPanel/EndPanel` (padding-aware height + screen clamp + inert-when-fitting scroll
view; EVERY early-return path must call EndPanel); DebugHUD content is scroll-wrapped.

**UI quality pass (2026-07-16, user: "looks OK but make it better quality / feel"):** all
procedural, no assets. `PrimitiveSprites` gained `Star()` (IQ sdStar5 SDF — REPLACES the
orb star-pip workaround everywhere: DrawStars + meter pips; orbs remain for map nodes/
pearls), `RoundedRectShaded(fill)` (candy button: top-lit gradient + bottom lip baked
inside the 9-slice borders — buttons + active tab + urgent chip), `RoundedRectOutlined
(fill, outline)` (~3px ring; overlay panels get a white ring, cached per color-pair —
dictionary key is a (Color,Color) ValueTuple). GameFlow: `DrawLabelShadowed` (draws
twice; page titles, map-node numbers — numbers moved OUT of the node button into an
overlaid label since labels never eat clicks, forced GUI.enabled=true so swipes don't
fade them) + node drop-shadow circles; top-bar Score/Shots/Tide became pill CHIPS
(`_chipStyle`, fixedHeight 30·scale, margin-top centers in the 52·scale row; tide chip
flips to pulsing coral `_chipUrgentStyle` when ShotsUntilPressure ≤ 1) + soft shadow
gradient under the bar; overlay pop-in: `BeginPanel(w,h,openT)` scales around screen
center with `EaseOutBack` and EndPanel restores GUI.matrix (every early-return already
called EndPanel, so the contract held), pause records `_pauseOpenedAt`, end overlay
derives openT from `_endSeenAt`+delay, target splash scales in around its own center
(appear derived from `_splashUntil - SplashSeconds`); scrims fade with openT; home cards
left-aligned two-size rich text + ">" chevron label; home title bobs on a sine; shop rows
sit on `_rowStyle` translucent cards. Compile-verified (27 files).

**UI pass round 2 (2026-07-16, continuation of the quality pass — user approved the
suggestion list):** Kenney UI Pack DOWNLOADED (CC0, direct zip from kenney.nl; license
copy at `Assets/Art/KenneyUIPack_License.txt`) — **fonts**: `Assets/Resources/Fonts/`
KenneyFuture.ttf (display: titles/buttons/node numbers/chevron, set fontStyle Normal —
the face is already heavy, faux-bold mushes it) + KenneyFutureNarrow.ttf (body:
subtitles/bar labels/chips/meter/shop/node-best). **Button plates**:
`Assets/Resources/Art/UI/` button_{red,yellow,grey}_{depth,flat}.png — DEFAULT 1x set
(192x64), NOT Double: 9-slice border vertical sum must stay under the smallest button
(38·scale) and 2x corner radii can't; `plateBorder` scales with _scale, clamped to
(14..60, 14..30 top, 22..31 bottom). normal=depth plate, active=flat plate (reads as
physically pressed); red=primary buttons, yellow=active tab/highlight cards, grey
flat=neutral tabs. ALL loads null-safe in `EnsureStyles` (`_skinProbed` once per domain
load; missing import → procedural skin fallback, game still needs zero assets).
**Menu page transitions**: DrawLevelSelect wraps page dispatch (section route extracted
to `DrawSectionMapPage`) in an EaseOutBack slide-up + fade (0.28s, content only —
backdrop stays put); `_lastDrawnPage` sentinel −1 forces replay on back-from-level.
NOTE: DrawSpriteGUI sets GUI.color absolutely, so tank sprites ignore the fade — accepted
(0.28s). **End-overlay mascot**: `DrawEndFish` — fish_orange bounces on win, fish_blue
grey-tinted droops on loss, rect-offset animation ONLY (GUI.matrix rotation misbehaves
inside BeginArea), panel height budgets `fishRowH` when the sprite exists.
Compile-verified. .metas generated by the editor + committed 2026-07-16 (GUIDs locked).
FONT-WIDTH LESSON (user-reported: "Close Shop" clipped): Kenney Future runs wider than
Arial, so fixed-width text buttons MUST size via `GameFlow.ButtonW(text, style, minW)`
(CalcSize + 12·scale slack, honest because button styles now carry real padding —
16·scale sides, 5·scale bottom on depth plates to lift text above the lip). Shop toggle
sizes to the longer of both labels; shop Buy/Sell columns take the catalog-wide max so
rows align; Back/Debug/Pause sized the same way. Tab-style font stepped down to
16·scale (secondary actions, incl. the daily banner's long label).
TEXT-HEIGHT LESSON (user-reported: target-splash lines clipped at the panel bottom):
same disease vertically — any panel holding wrapping text must size from
`style.CalcHeight(content, width)`, never a hardcoded height. Fixed in DrawTargetSplash
(fully measured layout; `_subtitleStyle.wordWrap = true` explicitly so CalcHeight and
rendering agree) and pre-emptively in the mechanic-tutorial card (`SectionHeight` +
`TutorialCopy` single-source copy helper).
SHOP-ROW LESSON (user-reported: names crushed illegible): an over-constrained IMGUI
horizontal row shrinks the UNSIZED children (the name labels) to slivers — every column
in a fixed-width row must have an explicit width budget. DrawShopPanel now computes
sellW/buyW (catalog-wide max, compact `_sellStyle`/`_buyStyle` 15·scale + 10·scale pads)
and gives the name column the explicit remainder (min 96·scale, `_shopNameStyle`
14·scale); panel cap widened 430→520·scale; title row = "Reef Shop" + right-aligned
balance (the old single title line could clip too).

**Launch splash (2026-07-16, user: "game shouldn't just show the main menu straight
away"):** `MenuPage.Intro` is the boot page (`_menuPage` initializer; NOTHING navigates
back to it — QuitToLevelSelect lands on SectionMap, Back lands on Home, so it's
once-per-session by construction). `GameFlow.DrawIntroPage`: rising bubbles + 3 cruising
pack fish (`DrawIntroFish`, null-safe, depths avoid the title band) + orb trio in game
colors (aqua/coral/sunshine) + EaseOutBack title pop + tagline fade + pulsing white
"Tap to dive in!" (white `_introPromptStyle` — the prompt sits on the gradient's deep
end where teal drowns; title is `_introTitleStyle` 46·scale). Tap-anywhere advance
lives in Update (PressedThisFrame, 0.4s guard so the app-launch tap can't skip it);
leaving Intro rides the existing page slide-in transition. All unscaled time.

**Mechanic tutorial cards (2026-07-16, user request):** first ENCOUNTER of Stone / Ice /
Tide pops a modal card at level start — animated diagram + copy per mechanic, ONE
"Got it — let's play!" button; per user requirement it NEVER auto-dismisses (unlike the
target splash) and aiming is blocked while open (`_modalOpen`). Detection is DATA-driven
in `GameFlow.QueueMechanicTutorials` (layout chars: Stone color / lowercase-playable
frozen; `PressureEveryShots > 0`) so authored intros, generated reefs and Dailies all
trigger it; a level meeting 2+ new mechanics stacks sections in one card. Seen-flags:
`TutorialFlags` in Data/Scoring.cs (`CoralCascade.Tutorial.<Id>`), marked ONLY on
dismissal (quit-with-card-open reshows next time, intended). The star-target splash
defers until the card closes (`_splashUntil = 0` while queued, restarted on Got-it).
Diagrams (`DrawStoneDiagram`/`DrawIceDiagram`/`DrawTideDiagram`) loop on unscaled time,
procedural orbs + rock_a (null-safe fallback grey orb), rect-offset animation only (no
GUI.matrix inside the panel area). RestartLevel re-queues (no-op once seen).

Pack shortlist (researched 2026-07-14):
- **Kenney Fish Pack** (kenney.nl/assets/fish-pack, CC0, 120 vector sea creatures/tiles) —
  DONE for balls; seaweed/terrain/background_* decor + hud_number_* digits still unused.
  Pack limitation handled: only 3 fish silhouettes, no yellow/purple — solved by tinting
  grey fish + spreading silhouettes (see mapping above). Crab/jelly/seahorse variety is
  optional polish later (CC0 hunt or AI-generate).
- **CraftPix Free Underwater World Parallax Backgrounds** (vector, free royalty-free
  license) — in-level + map backdrops; 1920×1080 landscape → crop/re-layer for portrait.
- **Kenney UI Pack** (kenney.nl/assets/ui-pack, CC0, 430+ sprites in 5 colors + 2 TTF
  fonts + UI sounds) — the FREE choice for buttons/panels/win-lose screens; its expansion
  has stars/medals. (Paid alt if ever wanted: CraftPix Bubble Shooter GUI, ~$0.60 on sale.)
- Make ourselves (packs won't have): stone bubble, ONE frost-overlay sprite (drawn over any
  color; thaw = remove overlay — replaces the DisplayColor lerp), danger line (step 3),
  launcher mascot (clam/crab/shrimp — doubles as app icon).
Integration is behind existing seams: sprites replace PrimitiveSprites output in
BoardView/Launcher/Projectile/FallingBubble; IMGUI skins incrementally via GUI.DrawTexture.
Keep primitives as fallback when a sprite is missing. License care: Kenney CC0 = free/no
attribution; CraftPix freebies = royalty-free in games, no asset redistribution; check each
itch pack's terms individually before shipping.

## My Reef aquarium — SPEC (designed 2026-07-14 w/ user; roadmap step 5, do not build early)

Old-Facebook-aquarium (FishVille) feel, minus the punishment-era mechanics: NO feeding
timers, NO fish death/decay, NO social/gifting. **Purely cosmetic forever** (user decision
2026-07-14) — no tank item may ever affect gameplay; fairness boundary extends to the economy.

- **Pearls** (currency, PlayerPrefs int `CoralCascade.Pearls`): earned on WIN only —
  10 base + 5 × stars earned that run, +15 first-ever clear of a level, +5 on new best.
  (≈40 for a 3★ first clear; all tunables.) Show "+N pearls" on the win overlay and a
  pearl-balance chip on the map header. No real-money purchase in prototype (Phase 3 owns
  monetization; remove-ads IAP unaffected).
- **UI**: third tab "My Reef" beside Tutorial Reef/Adventure (NOT a Section — special-case
  the tab row). Tank view fills the map area: reuse ReefBackdrop's rendering/motion
  (gradient, sand, base-pivot swaying plants, swimmers, risers) driven by OWNED items
  instead of ambient seeds — a `ReefTank` component sharing helpers with ReefBackdrop.
  Shop = scrollable list (BeginPanel/scroll pattern): sprite preview, price, Buy button
  (disabled when unaffordable), owned count. PREVIEW GOTCHA: pack sprite rects are
  sub-rects of the texture — GUI previews need DrawTextureWithTexCoords(sprite.rect
  normalized), not the raw texture.
- **Catalog v1** (~12 items, existing pack sprites, code-data like TestBoards; multiples
  buyable — own 3 blue fish → 3 swim): fish_blue/orange/green 30, fish_pink/red 40,
  fish_grey_long_a (eel) 60, fish_brown (puffer) 80; seaweed_grass_a 10, rock_a 10,
  seaweed_green_a/pink_a 15, seaweed_orange_a 20, extra bubble-stream column 25.
- **Achievement rares** (NOT buyable, derived from stars at load — no extra state):
  Golden Puffer = 3★ all Tutorial Reef (fish_brown tinted gold); Pearl Eel = 3★ any 10
  Adventure levels (fish_grey_long_b tinted pearl-white).
- **Persistence**: `CoralCascade.Reef.<ItemId>` = owned count; per-fish buy timestamps
  (CSV under `CoralCascade.Reef.T.<ItemId>`) drive **growth**: scale lerps 0.55 → 1.0
  over 3 real days (UtcNow) — bought as a baby, grows across sessions, never shrinks.
- **Placement (UPGRADED 2026-07-14, user request)**: decor (plants/rocks/vents) is
  DRAG-TO-PLACE along the sand — normalized x per instance in
  `CoralCascade.Reef.X.<Id>` CSV (`ReefStore.GetX/SetX`, invariant culture; defaults
  well-spread for pre-feature purchases); grab via last frame's `_decorHits` rects,
  held piece glows, release persists; shop-open drags scroll the shop instead. Fish
  are never placeable (they swim). **Selling (same day)**: `ReefStore.Sell` = half
  price back, removes the LAST instance + trims its timestamp/position CSVs; Sell
  button in every shop row.

## Architecture invariants (do not break)

- **Fairness boundary (non-negotiable, game plan §10):** aim/attach/match are fully deterministic —
  kinematic circle casts in `TrajectoryCalculator` against the STATIC world only. Physics governs
  bubbles strictly AFTER detachment. Falling debris lives on the built-in Ignore Raycast layer (2)
  so casts never see it (it still collides normally).
- **Firing during cascades is allowed by design:** `BoardManager._busy` covers only projectile
  flight. Cascade counters track a "burst" (first detachment after quiet → last debris gone).
  BUT the avalanche moment (slow-mo/celebration/MEGA banner) is gated on a SINGLE
  resolution's detach delta, NOT the burst-cumulative CascadeSize — cumulative gating let
  rapid-fire play crown a tiny match "MEGA CASCADE" (user-reported 2026-07-14, fixed:
  `EvaluateSlowMo(resolveDelta)`; SecondaryChains≥2 stays burst-level on purpose).
- **Win on any board mutation; lose only when quiescent** (no flight, nothing falling) AND shots = 0.
- Hex **odd-r** offset grid (`HexGrid` owns all parity math). Content is data (`BoardLayoutData` /
  `TestBoards`), never logic. Views are `SetActive(false)`'d before `Destroy` (same-frame replay casts).
- Placeholders flagged in code: win = board cleared, lose = shots out.

## Verification tooling (no editor needed)

- **Compile check** (editor can stay open): `<UnityData>/NetCoreRuntime/dotnet.exe <UnityData>/DotNetSdkRoslyn/csc.dll @compile.rsp`
  with refs = `NetStandard/ref/2.1.0/netstandard.dll` + `Managed/UnityEngine/*.dll` +
  `Library/ScriptAssemblies/Unity.InputSystem.dll`. Use Windows-style `C:/` paths in the rsp.
- **Board anchoring check:** PowerShell `Add-Type` script replicating `HexGrid`'s odd-r deltas —
  every test board must have zero floating bubbles at load. Run it whenever boards change.

## Out of scope until data justifies (game plan §7)

No meta/economy depth, no ads/analytics code yet (Phase 3), no extra themes/modes. Don't build early.
