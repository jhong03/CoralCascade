# Coral Cascade — Project Context

Mobile bubble shooter (Unity 6000.5.3f1, 2D URP, **new Input System only** — legacy `Input` throws).
Ads + one remove-ads IAP; physics-driven cascades + reef meta. Full plan: `CoralCascade_game_plan.md`.

## NEXT SESSION — start here

### ⇢ TEST THIS FIRST (2026-07-19 night session — Play prep + UX + audio)

Everything below is compile-verified and, where possible, verified by rendering the actual
output (icon PNGs, audio waveforms, camera-framing maths across 7 device shapes). **None of
it has been seen running.** Publishing checklist: `PLAY_STORE_CHECKLIST.md`.

**Do this one FIRST — it's a single click and it finishes the icon setup:**
> Unity ▸ **Coral Cascade ▸ Play Store ▸ Apply Release Settings**, then **Verify Release
> Settings**. Icons cannot be assigned by hand-editing YAML, so the menu item does it.

Then, in rough order of "how badly would this hurt":
1. **DOES THE GAME EVEN APPEAR?** The build list pointed at the empty `SampleScene` — a
   build would have launched blank. Now `Phase1Prototype`. Build once and confirm.
2. **AUDIO** — brand new, entirely synthesised at runtime (no asset files). Listen for:
   pops that pitch DOWN as clusters get bigger, a low tide swell, win/lose flourishes, and
   the ambient pad. Check the pad's 8-second loop doesn't click at the seam, and that rapid
   cascades don't sound machine-gunny (8-voice pool). Toggles: Settings ▸ Music / Sound.
3. **CAMERA FRAMING CHANGED FOR EVERY LEVEL.** It now fits the REAL screen aspect instead
   of a hardcoded 9:19.5. Verify on a wide Game-view preset (tablet 4:3) AND a tall one
   (21:9): the board should never clip columns, and the danger line + launcher should
   always be visible. This is the riskiest change of the session.
4. **Safe area** — test with a notch preset. The frosted top bar should run to the very top
   of the screen while its TEXT sits below the cutout. Back buttons/pearl chip too.
5. **Touch targets** grew to 44·scale (~52dp) for Back/Settings/toggles, which pushed the
   Home page header down. Check nothing collides on a small screen.
6. **Settings page** now has Music / Sound / Vibration / Screen shake + Privacy. Check it
   fits without scrolling on a short screen, and that Privacy scrolls.
7. **Vibration** fires only on tide drops and win/lose — deliberately not per-pop.

**Known-not-done (deliberate):** keystore (needs your password), hosted privacy-policy URL,
store listing art, and every Play Console form. All listed in the checklist.

Everything is **committed & pushed** (late-game retune + 50→70 levels + procedural
stone/critter + debris fade + banner-width fix + Pearl Eel shimmer). Working tree is clean
apart from the pre-existing Unity churn listed under "Uncommitted" below, which was left
alone again — it is still NOT mine to decide on.

**THE GATE: none of it is play-tested.** Everything is compile-verified, and the reef math
is verified by RUNNING the shipping `LevelCatalog` standalone (see "Verification tooling" —
that technique is new and much stronger than the old replication). Worth checking first:
1. **Reefs 47–70 winnable now?** They were being handed ~55-80% of their own computed shot
   budget. If they now feel too GENEROUS, `LevelCatalog.ShotCap` (75) is the one dial.
2. **Do the four SHAPE MOTIFS read on screen?** Pillars (51–55), Lattice (56–60), Chasm
   (61–65), Spires (66–70). They were tuned by dumping ASCII boards, never seen rendered.
   Spires especially — do the towers look deliberate or just like a gappy board?
3. **70 nodes on the Adventure map** — the path is data-driven off `Levels.Count` and needs
   no change, but that's unverified visually (scroll length, frontier centring).
4. **3-row top bar / ice visibility / Reef 10** — still unverified from the last session.
5. **Tutorial-card friction** — guides re-show on EVERY level start; on a 3-mechanic reef
   that's a tall modal before every play, retries included. Walk-backs if it grates: skip
   on restart-only, or a compact splash line for repeats + full cards for firsts.
6. **Banner text** — "THE REEF FLOODED!" now shrinks to fit; check it still reads big.
7. **Stone + critter now procedural** — stone should be a matte grainy grey rock, critter a
   pink fish in a pale bubble. If stones now read as "just another grey ball", add a glyph
   or notch rather than going back to `rock_a`.
8. **Debris blink/fade** — if the flicker is distracting during big cascades, `BlinkDepth`
   (0.42) and `FadeHold` (0.15) in `FallingBubble` are the two dials.

**Deferred / residual (my flags, user-aware):**
- **Records for Reefs 27–70 are re-based** — every clipped level's shot budget changed, and
  Reefs 46–50 got new boards entirely (trimmed specs). Old bests/stars for those keys are
  orphaned or easier to beat. Accepted, same as the earlier restructures.
- **`BudgetMul` is still partly a no-op at the very top** — 7 late levels want more than the
  75-shot ceiling and clip to it. Much better than the old 24, but the last waves are
  differentiated by board content, not by their fairness dial.
- **Critters can still sit at row 2** — same seal risk stones just had fixed; lower risk
  (droppable, single) but the same class of bug. Applying the rows-3+/interior rule to
  critters was offered and NOT done.
- **Full winnability/reachability solver** in candidate selection — OFFERED and DEFERRED in
  favour of the cheaper stone placement rule. Nothing yet *proves* a generated board is
  clearable; the stone rule only removes the known mechanism.
- **Tutorial RisingTide (Intro 8)** keeps its every-3-shots cadence (`TideIntervalBump`
  applies to generated reefs only); it only got the lighter rows.
- Old best/star records for stone + tide reefs are orphaned/re-based (accepted).

**Still open from earlier sessions (untouched):** the in-level ReefBackdrop world-
SpriteRenderer bug (pale untextured quads); camera framing off a hardcoded 9:19.5 aspect
(21:9 phones clip outer columns, tablets show a small board); no `Screen.safeArea` insets.

**Uncommitted (NOT mine, left alone all session — decide whether to keep):**
`ProjectVersion.txt` (now 6000.5.3f1), `Packages/manifest.json` + `packages-lock.json`,
`UniversalRenderPipelineGlobalSettings.asset`, `EditorBuildSettings.asset`,
`PackageManagerSettings.asset`, `ShaderGraphSettings.asset`, and untracked
`ProjectSettings/PhysicsCoreProjectSettings2D.asset`.

## Status (as of 2026-07-19)

**2026-07-19 — LATE-GAME RETUNE + ADVENTURE 50 → 70 LEVELS (uncommitted; compile-verified
and generator-verified by running the REAL LevelCatalog, NOT play-tested).** User-reported:
"Level 50 not possible to win." It was not a seal bug — it was the shot cap.

- **Diagnosis: the `Math.Min(50, …)` shot cap was silently discarding the budget formula's
  output.** Reef 50 computed a **95-shot** budget and was handed **50** (53%). Every reef
  from **27** on was clipped, widening with depth (Reef 37 → 63 wanted / 50 given, which is
  exactly the level the user lost to a REEF FLOOD). Two knock-ons: (a) the previous session's
  tide compensation (factor `0.4 → 1.6`) was thrown away for all 24 of those levels — it only
  ever landed on the 16–25 band that got verified; (b) **`BudgetMul` became a dead dial** —
  Reef 46 (1.10, breather) and Reef 50 (1.00, finale) both rounded to 50, so the wave rhythm
  stopped existing. Reef 50 needed 89 bubbles cleared in 50 shots (1.78/shot) at 6 colours
  with 22% unmatchable obstacles. NOTE: no HARD lock was found — row 0 is always plain
  colour and every obstacle is droppable, so the 2026-07-18 stone rule is holding.
- **Fix (user chose "raise cap + trim mass", 75-shot playtime ceiling):**
  1. `LevelCatalog.ShotCap` **50 → 75** — a PLAYTIME ceiling (~4–5 min), not a difficulty
     one. Heavy boards were trimmed to fit under it rather than raising it to meet them.
  2. **Wave 10 (46–50) trimmed** (density 0.74–0.82 → 0.68–0.72, stones/ice 5–9 → 4–8,
     critters ≤3). It is no longer the finale and had to make room for 51–70.
  3. `MinShotsAfterDrop = 4` — **new general rule**: after capping, shots are nudged up so
     the last tide drop always leaves shots to answer it. A drop landing on the final shot
     is an unavoidable loss; Reefs 16/19 (fixed last session) and **Reef 25 (0 shots, still
     live until now)** all shipped in that state. Adding < `PressureEveryShots` can never
     create another drop, so the true max is 78.
- **20 NEW LEVELS — waves 11–14, identity from board SHAPE not size.** At 13 columns a
  bubble is already ~28 logical points on a phone, so the grid CANNOT grow; `Motif` gives
  the new waves distinct geometry with **no new rules, art, or tutorial cards**:
    11 Reefs 51–55 **Sunken Ruins** — `Pillars`: stones fill whole interior COLUMNS
       top-down (ruined columns you topple by cutting the plain rows above).
    12 Reefs 56–60 **Glacier Vault** — `Lattice`: ice on an `(r+c)%3` crystalline grid.
    13 Reefs 61–65 **Sunless Chasm** — `Chasm`: a hollow rift splits the board; fastest tide.
    14 Reefs 66–70 **Leviathan's Rest** — `Spires`: paired columns plunge to the floor.
       Thin tide slack, but one cut at a tower's top drops it whole. Reef 70 = hardest board.
- **Motif safety (the part that matters):** motifs are **density masks applied DURING
  generation**, so the up-neighbour rule still gates every placement and anchoring-by-
  construction is untouched. **Carving cells out AFTER generation would orphan everything
  below the hole — never do that.** `Motif.None` returns a 1.0 multiplier (exact in IEEE754)
  and keeps the original placement path, so **Reefs 1–50 are bit-identical**, verified.
- **`ConvertCells` gained a `where` PREFERENCE (not a filter)**: pattern cells are used
  first, then any eligible cell, so a sparse board still gets its full authored count
  instead of silently under-placing (Pillars was placing 4 of 12 stones before this).
- **Verified** (running the shipping catalog, all 70 reefs + 21 dailies): zero floating
  bubbles; every guaranteed count placed; every tide level ≥4 shots after its last drop;
  need-rate across 46–70 now **1.06–1.32** (was 1.78 at Reef 50), mass 41–60, shots 57–76.
  Two independently-written implementations (C# + PowerShell) agree on every number.
- Daily band widened to the Reef **16–60** spec range (was 16–45) so dailies see the motifs;
  deliberately stops short of the finale waves.
- **Tuning lesson:** two of the four motifs failed their first draft and were caught only by
  dumping ASCII boards — "Teeth" (per-column depth limits) was INVISIBLE because the
  generator's own `1 - r*0.055` taper already bites harder than the mask, and Pillars
  scattered because the columns it filled were too sparse to stack in. Fixes: replaced
  Teeth with Spires (mask >1.0 to DEFEAT the taper) and gave Pillars a 1.6 mask to thicken
  its own columns first. **Always dump a generated board before trusting a generator change.**

**2026-07-19 (night) — PLAY-STORE PREP, DEVICE FIT, UI PASS, AUDIO (uncommitted at time of
writing; compile-verified, output-verified where renderable, NOT play-tested).** User asked
what the Settings page still needed and then for a full UI/UX cleanup + Play prep, run
autonomously to completion. Full requirement detail: `PLAY_STORE_CHECKLIST.md`.

- **THE BUILD SHIPPED AN EMPTY SCENE.** `EditorBuildSettings` listed only
  `SampleScene.unity`; the game lives in `Phase1Prototype.unity`, which was not in the build
  list at all. Any APK built before this fix would have launched to a blank screen. Fixed;
  SampleScene retained but disabled. **This was committed, not local churn — it had been
  true for a long time.**
- **Other build blockers fixed:** `applicationIdentifier` had no Android entry at all (would
  have fallen back to a `DefaultCompany` id, which is PERMANENT once published) → now
  `com.jhong03.coralcascade`; company/product names; target SDK 0 (auto) → **36**. NOTE:
  API 35 is today's floor but **36 is mandatory from 2026-08-31**, so 36 now avoids a redo.
  IL2CPP + ARM64 were already correct (an early grep of mine truncated the value and I
  briefly believed otherwise — the lesson is to grep with context, not just the key line).
- **App ICON generated** procedurally in the game's palette (glossy orb trio on a lagoon
  gradient) at `Assets/Art/Icon/`, checked at 48px and under a circular launcher mask.
- **`Assets/Editor/PlayReadiness.cs`** — menu items to APPLY and VERIFY release settings.
  Icon assignment goes through `PlayerSettings.SetPlatformIcons` because that structure is
  nested/platform-keyed and hand-written YAML is how you silently end up with no launcher
  icon. It enumerates `GetSupportedIconKinds` rather than naming `AndroidPlatformIconKind` —
  that type lives in the Android platform EXTENSION and won't compile without the module.
  Verify also fails the build if the game scene ever falls out of the build list again.
- **CAMERA FRAMING now uses the real `Camera.aspect`** (was a hardcoded 9:19.5). It fits the
  board WIDTH exactly on any device and separately guarantees the danger row + launcher stay
  on screen, solved analytically because the UI reservation is a pixel height that itself
  depends on ortho. Verified across 7 device shapes × 3 column counts (`framing.ps1`): no
  horizontal clipping anywhere, and the tablet case is now height-bound instead of shrinking
  the board to a postage stamp. **This changes framing on every level — the riskiest change.**
- **SAFE AREA** (`SafeAreaUtil`): cutout/gesture-bar insets applied to the top bar, page
  headers, back buttons, pearl chip, map/tank content and overlay centring. Backgrounds stay
  full-bleed on purpose — a frosted bar that stops below the notch reads as a bug.
  COORDINATE TRAP recorded in the file: `Screen.safeArea` is bottom-left origin, IMGUI is
  top-left, so the GUI top inset comes from `safeArea.yMax`. Reversing those two puts the
  HUD under the notch on exactly the devices you were protecting.
- **Touch targets**: `MinTouch` = 44·scale (~52dp vs Android's 48dp guidance; the old
  30·scale chrome buttons were ~35dp). Raising a button REQUIRES moving what sits below it —
  the Home header is now derived from MinTouch rather than hardcoded offsets.
- **Per-frame GC**: the three HUD chips were interpolating strings on every OnGUI pass
  (which runs several times per frame, all through gameplay). Now cached on the value.
- **AUDIO — `Sfx.cs`, fully SYNTHESISED at runtime**, same principle as PrimitiveSprites:
  the game still requires zero imported assets, and there is no licence question for the
  store build. 12 one-shots + an 8-second ambient pad, 8-voice pool, wired into the existing
  effect seams (pop tier, drop, chain, thaw, tide, fire, attach, win/lose). Music/SFX/
  Vibration toggles are now REAL settings — the page's no-dead-switches rule finally allows
  them. `Haptics.Bump()` is deliberately reserved for tide drops and win/lose: Android's
  `Handheld.Vibrate` is a blunt ~500ms buzz and per-pop haptics get switched off by players.
- **VERIFY-THE-WAVEFORM technique** (new): the synthesis was replicated in System.Drawing and
  plotted (`RenderAudio.cs`). It caught three things code review would not: the pad peaked
  at 1.035 (**would clip**), the stingers ended on a non-zero sample (**click**), and
  win/lose notes were confined to their own slots so they decayed to silence between notes —
  four detached blips, not a flourish. Fixed by normalising to a 0.9 target, a 3ms fade-out
  (**suppressed for the looping pad — a fade there would notch the seam**), and letting each
  arpeggio note ring 3× its slot into the ones after it.
- **PRIVACY PAGE added in-app** (Settings ▸ Privacy). Play requires a policy in the Console
  AND "a link **or text** within the app" — text satisfies the in-app half without needing a
  hosted URL. **The text asserts no data collection, no network, no ads, no analytics, which
  is true TODAY. It must be rewritten together with the Data safety form the moment ads or
  analytics land.**

**2026-07-19 — PEARL EEL MADE VISUALLY DISTINCT (uncommitted; compile-verified, previewed
as a rendered PNG, NOT play-tested).** User: "Pearl Eel doesn't look any different from the
normal moray eel." It genuinely wasn't: it shares the Moray's sprite pair and its only
distinction was `Tint = (0.95,0.97,1)` — and **sprite/GUI tinting MULTIPLIES, so tinting an
already-grey fish toward white is a ~5% no-op. Multiply can never lighten.** (Same law the
orb gloss already obeys by being its own untinted layer; the Golden Puffer works only
because brown × gold is a genuine darkening.)
- Fix: two new `ReefItem` fields, both general. **`Shimmer`** (0..1) redraws the body in a
  second ALPHA-BLENDED pass — that's what lightens — in a slowly cycling pale colour;
  **`Sparkle`** adds twinkling procedural `Star()` glints. Pearl Eel also grew 1.3 → 1.55,
  so it is visibly LONGER than the Moray (1.2) even before the colour registers.
- Tuning notes: the hue swing must stay SMALL (±0.08 around 0.92) — the first attempt at
  ±0.18 rendered a candy-pink eel, not a pearl one. Glints are confined to the middle
  0.42–0.60 of the sprite CANVAS because the artwork only fills part of it; a wider spread
  put glints in open water. Both caught by rendering a comparison PNG (`RenderEel.cs`,
  scratchpad — composes the real pack halves and applies the actual blend maths).
- Golden Puffer deliberately left alone (gold already reads); one line adds `Sparkle` to it
  if the rares should feel like a matched set.

**2026-07-19 — STONE + CRITTER ART MADE FULLY PROCEDURAL, and DEBRIS NOW BLINKS/FADES
(uncommitted; compile-verified, textures eyeballed as rendered PNGs, NOT play-tested).**
User on Reef 58: "what is this white colored ball, I don't have it in my shooter."

- **It was a CRITTER whose fish had vanished.** `BubbleArt` drew critters as a pale shell
  (`0.94,0.97,1`) with an imported `fish_pink` sprite on a CHILD renderer, and stone as
  imported `rock_a` on the root. Imported pack sprites don't draw reliably on in-level world
  SpriteRenderers (the still-open 2026-07-16 bug), so the critter read as a featureless white
  ball and stones were most likely pale silhouettes. **Third time this bug has cost
  something** (backdrop decor → ice → this).
- **Fix (user picked "procedural read for both"): put the read where it CANNOT fail.** New
  `PrimitiveSprites.StoneOrb()` (matte, 3-band faceted, hash-speckled grey — deliberately NO
  specular, because matte is what reads "unmatchable" next to the glossy playables) and
  `CritterOrb()` (coral fish + eye + shell shading + baked specular, all in ONE sprite).
  Both go on the ROOT renderer with `Color.white`, no children, no imported assets — the
  same lesson as the ice fix. `BubbleArt` no longer branches on `Available` for these two,
  so the with-pack and without-pack looks are finally identical.
- **VERIFY-THE-PIXELS TECHNIQUE (new, worth reusing):** the textures were replicated in
  System.Drawing (`RenderOrbs.cs`, scratchpad) and saved as PNGs — including a contact sheet
  on the in-game water blue at real gameplay size (~28px) — so they could be LOOKED at
  before shipping. Caught a blocky rectangular fish eye. Same principle as dumping ASCII
  boards: never ship generated content you have not viewed. NOTE when replicating: Unity's
  `Mathf.SmoothStep(from, to, t)` maps into [from,to] — it is NOT the classic 0..1 edge
  function, and the existing orb code relies on that (its "rim shadow" dims the whole ball).
- **DETACHED DEBRIS BLINKS AND FADES** (user request): debris is scored and off the board the
  moment it detaches, but it is still a physics body and at some angles it wedges in a pocket
  looking exactly like a live bubble. `FallingBubble` now fades (hold 15% of life, then
  smoothstep to 0) with a blink that quickens as it dies. Full opacity is held through the
  FAST part of the fall on purpose — that's the cascade's drama, and the window in which it
  can still chain-knock. Lifetime stays 2.5s; scoring/collision/quiescence untouched.

**2026-07-19 — TIDE RETUNE (committed; compile- + generator-verified, NOT play-tested).**
User-reported: "impossible to win level 18, too few moves before tide rises." Verified with
the scratchpad generator replication — it was a WAVE-WIDE bug, not just Reef 18:

- **Diagnosis.** Reefs 16–20 needed **1.77–2.10 bubbles cleared PER SHOT** to win (fair is
  ~1.3): Reef 18 = 40 starting bubbles **+23 dumped by the tide** in only 33 shots. Worse,
  "shots left after the LAST tide drop" was **0 on Reefs 16 & 19** — the tide dumped a fresh
  ~8-bubble row on the final shot with no shots to clear it = **guaranteed loss even with
  perfect play**. Root cause: the budget granted only **+31%** shots for the tide while the
  tide added **+58%** bubbles, and the extra shots just triggered MORE drops (self-defeating).
- **Fix — three dials moved together** (user chose the "fullest fix"):
  1. `LevelCatalog.TideIntervalBump = 5` — a single GLOBAL dial added to every authored
     `spec.TideEvery` in `Generate` (so re-tuning the tide never means editing 30 spec rows;
     the Specs table keeps its original cadence numbers). Wave 4 now drops every 15, not 10.
  2. `BoardManager.PressureRowFill 0.7 → 0.5` — lighter ceiling rows (~5.5 bubbles/drop, not
     ~7.7). **Coupled to the budget formula — CHANGE TOGETHER.**
  3. Budget tide factor `0.4 → 1.6` (and it now uses 0.5, the new fill); shot cap 45 → 50.
- **Verified** (Reefs 16–20): NeedRate now **1.27–1.42**, shots-after-last-drop **6–13** (no
  more auto-loss). Reef 18: 40 bubbles / 40 shots / drops every 15 / NeedRate 1.27.
- NOTE: the tutorial **RisingTide** board (Intro 8) is hand-authored, so `TideIntervalBump`
  does NOT apply to it (still every 3 shots) — it only gets the lighter rows, i.e. slightly
  easier. Dailies DO get the full retune (they route through `Generate`).
- All tide reefs' shot budgets changed ⇒ their old best/star records are effectively
  re-based (harmless; scores just get easier to beat).

**2026-07-19 — MECHANIC GUIDES NOW REPEAT ON EVERY LEVEL START** (user request; they used
to be first-encounter-only). `QueueMechanicTutorials` no longer gates on
`TutorialFlags.Seen` — every mechanic the board actually contains queues its card on every
start, restarts included. Seen-flags are STILL written, but now only to choose the heading:
a genuine first meeting keeps the "NEW DISCOVERY!"/"NEW DISCOVERIES!" moment, a repeat reads
"THIS REEF HAS" (`_tutorialHasNew`, set by the new `QueueTutorial` helper). Everything else
is unchanged: the card is modal and blocks aiming until "Got it", and the star-target splash
still defers until it closes. Settings → "Replay mechanic guides" still works (it now only
resets the heading back to "new"). NOTE: on a 3-mechanic reef this is a tall card before
EVERY play — user accepted that trade when picking this over a non-blocking splash line.

## Status (as of 2026-07-18)

**2026-07-18 session — PLAY-TEST FIXES (committed & pushed; compile-verified, generator-
verified for the reef changes, but NOT deeply play-tested beyond the user's reports).**
Eight fixes, all user-approved via question dialogs. Verify tooling: Roslyn compile check
(no editor needed) + a scratchpad PowerShell replication of the deterministic generator
(bubble counts / shots / star targets / stone audit — matched the game exactly: computed
Reef 10 2★ = 666, same as the in-game HUD).

- **Pearl economy is now PROGRESS-gated (was an infinite replay faucet).** `WinBase +
  PerStar×stars` used to pay on EVERY win — replaying any cleared level farmed pearls
  forever. Now (GameFlow win block): first clear pays `WinBase + PerStar×stars +
  FirstClearBonus`; a REPLAY pays only for genuine improvement — `PerStar×(stars beyond the
  old best) + (NewBestBonus if a new best score)`; no improvement = 0. Captures `prevStars`
  BEFORE `Stars.Submit` overwrites it. Total pearls per level is now bounded.
- **CRITTERS: rescue-to-aquarium collection REMOVED — they are now a pure in-level
  difficulty obstacle** (user decision; SUPERSEDES the 2026-07-17 critter-collection notes
  below). Deleted `ReefStore.RescuedCritter/RescuedCount/AddRescued`,
  `BoardManager.CrittersRescued/NotifyCritterRescued` + reset, the `CascadeController`
  notify call, and GameFlow's win-block banking + tank-draw loop + end-overlay "joins your
  reef" line + `_crittersSaved`. Critters still parse ('C'), render (trapped-fish bubble),
  teach (tutorial card, reworded to drop the reef promise), and must be DROPPED to clear —
  that's the difficulty. Present identically on every play/replay (deterministic layout);
  nothing removes them. Old `CoralCascade.Reef.Rescued` PlayerPref is orphaned (harmless).
- **Ice now VISIBLE.** The frozen look was a translucent "Frost" CHILD SpriteRenderer that
  didn't render (same class as the open backdrop world-SpriteRenderer bug). Moved onto the
  ROOT orb tint: `sr.color = Lerp(color, FrostColor(0.80,0.92,1), FrostWash 0.55)` when
  frozen (BubbleArt) — guaranteed to render (the orb does), color still hinted, gloss kept.
  Frost child retired.
- **Queue weighting — SOFTENED PROPORTIONAL** (rare colors stopped flooding the queue).
  `Launcher.WeightedPick` weights by remaining count, `P(c) = bias·share + (1−bias)/n`,
  `ColorWeightBias = 0.65` (1 = strict proportional, 0 = old uniform). New
  `Board.CollectColors(into, counts)` overload. Replay-safe (fired color still recorded in
  the Shot struct). Both the visible draw and the auto re-roll use it.
- **STAR CURVE lowered 18×/28× → 16×/24×** (`Stars.Star2PerBubble`/`Star3PerBubble`). On
  obstacle-heavy reefs 28× sat at/beyond the practical score ceiling (stones inflate
  BubbleCount yet can't be matched/chained and fragment cascades), so 3★ was unreachable
  (user-reported Reefs 8–10). e.g. Reef 10 3★ 1036 → 888.
- **STONES stay OFF the ceiling rows and outer columns** (the big one — the generator could
  ship UNWINNABLE boards). A stone sealing a row-0 anchor (removable only by matching, never
  by dropping) = a dead level; Reef 10 was PROVEN unwinnable (2 yellows walled in a corner,
  one a ceiling anchor). Fix: guaranteed stones now `minRow: 3, interiorCols: true` via a new
  `ConvertCells` param; sprinkle stones gated inline to `r>=3 && interior` (rng draw still
  consumed so the stream stays put). Verified: all 50 reefs place their full stone counts,
  ZERO stones in rows 0–2 / edge columns, bubble counts (hence star targets) unchanged,
  anchoring preserved by construction. NOTE: stone positions moved ⇒ old best/star records
  for stone reefs are orphaned (accepted, same as the restructure). RESIDUAL (not done):
  critters can still sit at row 2 — far lower seal risk (droppable, single), revisit if it
  bites. A full winnability/reachability solver in candidate selection was OFFERED and
  DEFERRED in favor of this cheaper placement rule.
- **Top-bar LEVEL TITLE moved to its OWN centered row** (user request, after the upper-left
  label clipped to a misleading "1"/"Level 1" whenever the chip row got crowded — worst with
  the 3-chip tide layout). The bar is now THREE rows: centered title (`NodePrefix N`, e.g.
  "Level 18"; Daily shows its name) via new `_barTitleStyle` (display font, centered) + chip
  row (chips left, pause right — level label removed, so no more width fight) + star meter.
  Bar height 76→106px·scale; the `GameBootstrap.ConfigureWorld` camera reservation bumped
  84f→114f (=106 content+8 margin, uiScale == GameFlow `_scale`) so it still can't overlap
  the anchor rows. `TopBarHeight`/pointer-occlusion track `_topBarRect.height` automatically.
- **Mechanic-tutorial card TITLE no longer clips.** The wide display font wrapped "Frozen
  bubbles" to two lines but the layout reserved one line. Now measures title height with
  `CalcHeight` (same lesson as the body copy / target splash) in both `SectionHeight` and
  `DrawTutorialSection`.

## Status (as of 2026-07-17)

**2026-07-17 session — ADVENTURE RESTRUCTURE (30 → 50 levels, NOT yet committed/tested):**
user-approved via question dialog: full restructure of Reefs 1–50 (old records orphaned,
accepted), waves + rescue critters, challenging-but-fair endgame; critters = optional
bonus, banked on WIN only.
- **10 themed WAVES of 5** replace the smooth t-formulas: `LevelCatalog.Specs` is a
  per-level table (cols/rows/colors/density/GUARANTEED stone-ice-critter counts +
  sprinkle %/tide/danger margin/budget multiplier) — TUNE THE GAME IN THE TABLE, not in
  formulas. Waves: First Dive 1–5, Stone Garden 6–10, Frozen Shallows 11–15 (5th color
  14), Rising Tide 16–20, Critter Cove 21–25, Deepwater 26–30, Stonefall Trench 31–35
  (6th color 34), Glacier Line 36–40, Storm Surge 41–45, The Abyss 46–50. Teaching order
  now matches the Tutorial Reef (stone → ice → tide → critters). Wave rhythm: first level
  breather, last milestone. Guaranteed mechanics via `ConvertCells` (deterministic
  post-gen conversion of anchored cells; critters rows 2+, stones/ice rows 1+; anchoring
  by construction untouched). `GenerateBalanced`: 9 candidate seeds per level, keep the
  board closest to the candidates' MEDIAN bubble count (raw generator swung 28..77 on
  identical specs — anchor-row luck; median pick restores authored pacing). Daily now
  draws specs from the Reef 16–45 band. Budget formula gained obstacle taxes
  (+0.4/stone+critter, +0.25/ice, sprinkles as expected counts); cap 40 → 45.
  NOTE: the 45 cap binds from ~Reef 31 on (BudgetMul stops differentiating there —
  late-game tuning lives in board mass/mechanics; revisit after play-test).
- **CRITTERS (rescue mechanic):** `BubbleColor.Critter`, char 'C' — unmatchable/unfireable
  like Stone (IsPlayable false ⇒ excluded from queue/palette/ceiling rows), never row 0,
  never frozen. Rescue = DETACH (drop or chain knock): single choke point in
  `CascadeController.DetachCells` → `BoardManager.NotifyCritterRescued` (run tally reset
  in LoadLayout + "RESCUED!" floating text). Critters score as normal dropped/chain
  bubbles (they're in BubbleCount, so star math stays consistent). Board-clear frees all
  remaining critters EMERGENTLY (only row 0 anchors; critters never sit there ⇒ clearing
  colors always drops them). Win block in GameFlow banks `_crittersSaved` via
  `ReefStore.AddRescued` (WIN only), end overlay line (height budgeted), visual =
  GlossyOrb pale shell + fish_pink child in `BubbleArt.Apply` (primitive fallback safe),
  tutorial card added ("Critter" in TutorialFlags.AllIds, data-driven 'C' detection,
  DrawCritterDiagram). Rescued critters swim in My Reef as `ReefStore.RescuedCritter`
  (count at `CoralCascade.Reef.Rescued`, not buyable/sellable, fully grown, pale-pink
  tint; drawn after the rares in DrawReefTank).
- **Tank fish fix (user-reported "all at the top"):** DrawTankFish derived lane fractions
  via `Frac(hugeHash * k)` — beyond float fractional precision, returned ~0 for every
  fish. LESSON: modulo a hash to a SMALL int before float math. Fish now also GLIDE
  vertically (hash-derived home depth ± slow sine over the column) plus the quick bob.
- **Verification:** compile clean (all scripts); PS checker `check50.ps1` (scratchpad;
  replicates generator + median pick + odd-r BFS) — all 50 reefs + 14 dailies zero
  floating bubbles, all guaranteed counts placed. Curve eyeballed: mass 23→~58, breathers
  dip, milestones peak. NOT play-tested; old bests/stars for Reef N keys are now orphaned
  PlayerPrefs (harmless; Settings reset wipes).

## Status (as of 2026-07-16)

**2026-07-16 session (all pushed):** UI QUALITY OVERHAUL — two passes (procedural: star
pips/candy buttons/HUD chips/pop-in overlays; then Kenney UI Pack: real fonts + 9-slice
button plates + page transitions + end-screen fish mascot), LAUNCH SPLASH (tap-to-dismiss
title intro), FIRST-ENCOUNTER MECHANIC TUTORIAL CARDS (Stone/Ice/Tide, player-dismissed
modal), SETTINGS PAGE (shake toggle / replay guides / two-tap reset / credits),
PORTRAIT-ONLY lock + a portrait layout sweep fixing many user-reported cutoffs (see the
LESSONS entries in "Art direction" — the recurring theme: a phone is ~370 logical points
wide; budget every row, measure every text). Test with a FIXED-RESOLUTION portrait Game
view preset (1080x2340), not Aspect Ratio.
**KNOWN OPEN BUG (backdrop):** in-level ReefBackdrop decor (plants/sand/swimmers) renders
as PALE UNTEXTURED QUADS — world SpriteRenderers only; the SAME sprites via IMGUI
(aquarium/splash) and a sorting-order-40 test row render fine; materials/import/scene/
renderer-asset all verified healthy. Diagnosis incomplete (a mid-hunt swimmer-motion
commit was rolled back via reset to 2b3d318). NEXT STEP: during Play, pause and click one
pale shape in the Scene view — the Inspector tells us what it actually is.
**Open mobile gaps (user aware, deferred):** camera frames from hardcoded 9:19.5 (21:9
phones clip outer columns; tablets show a small board) + no Screen.safeArea insets.
**Next session:** user picks — backdrop bug, camera framing/safe-area, or backlog below.

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
stones — rescue-critters DONE 2026-07-17, see Status), cascade-earned power-up bubbles
(bomb/rainbow — must stay skill-earned, not random, to preserve fairness), endless mode
(reuses pressure + generator), share card, analytics.

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
BANNER-WIDTH LESSON (2026-07-19, user screenshot: "THE REEF FLOODED!" rendered as
"REEF FLOOD", both ends off-screen): `Announce` sized characterSize from `orthographicSize`
alone — a VERTICAL measure — so long cheers overran a narrow portrait view. Now it measures
the string (`PopEffects.MeasureWidth`: sum of `CharacterInfo.advance` after
`RequestCharactersInTexture`, × 0.1 = TextMesh's world scale at characterSize 1) and shrinks
ch to fit 90% of `ortho * cam.aspect * 2`. Same disease as the HUD/splash text lessons: any
world-space TextMesh must be measured against the camera's WIDTH, not just its height.
Metrics must be queried at the render font size/style — hence the `AnnounceFontSize` const.

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
PORTRAIT MENU SWEEP (user-reported, 5 screenshots): all menu pages now use a TWO-ROW
header (Back/pearl-chip strip on top, title on its own line via `DrawLabelShadowedFit` —
shrinks font until one line fits; splash title fit-drawn too); route-map/tank content
starts at 102·scale. Daily banner label is measured with a short fallback ("DAILY ·
Best N"). My Reef: rare-goal lines are FULL-WIDTH under the shop button (beside it they
wrapped over each other), fish lanes start 108·scale down, tip shortened; shopSmall
wordWrap=false. Shop Sell/Buy slimmed to 13·scale/7·scale pads so the Buy column fits
the row budget. My Reef card sub shortened.
HUD-WIDTH LESSON (user-reported on the first true-portrait test: level label crushed to
a vertical sliver, Debug/Pause pushed off-screen): a 1080-wide portrait phone is only
~370 LOGICAL points across (scale ≈ 2.9) — budget the whole row in logical points before
designing it. DrawTopBar now: chips shrunk (13·scale font, 9·scale pads, "Tide N"),
Pause is an icon-square ("II", plain ASCII), Debug button MOVED to the pause menu
("Debug Tools", toggles the HUD + resumes), and the level label gets the measured
remainder (full title → short "Level N" → hidden). `_barLabelStyle.wordWrap = false`
(width-constrained labels must clip, never wrap char-by-char).
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

**Settings page (2026-07-16, user request):** `MenuPage.Settings`, entered via a
"Settings" button top-LEFT on Home (mirrors the pearl chip), two-row header like every
sub-page. Contains ONLY wired options (deliberate: no audio toggles until audio exists —
dead switches are worse than none): screen-shake ON/OFF (`GameSettings.ShakeEnabled` in
Data/Scoring.cs, gated at the single `PopEffects.Shake` entry point), "Replay mechanic
guides" (`TutorialFlags.ResetAll`, ids single-sourced in `TutorialFlags.AllIds`),
"Reset ALL progress" (two-tap confirm, arms for 3s, `PlayerPrefs.DeleteAll` — wipes
settings too, defaults return; section MapOffsets re-sentineled), transient feedback
notes, Kenney credits at the bottom.

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
  Adventure levels (the Moray's own sprite pair + `Shimmer`/`Sparkle`, see below).
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

**Portrait-only (2026-07-16, user decision — "never intended for landscape"):**
ProjectSettings = AutoRotation with ONLY Portrait + PortraitUpsideDown allowed (landscape
flags 0; upside-down kept for tablets). NOTE: this locks DEVICES only — the editor Game
view needs a portrait preset (e.g. 1080x2340) to test the intended framing; the
landscape-window screenshots that looked "board lost in water" were exactly this.
Still OPEN mobile gaps (assessed 2026-07-16, user deferred): camera frames from the
HARDCODED 9:19.5 TargetAspect not Camera.aspect (outer columns clip on 21:9 phones;
board floats small on 4:3 tablets) + no Screen.safeArea insets (top bar under notches).

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
- **RUN THE REAL GENERATOR (best method, added 2026-07-19 — prefer this over replicating).**
  `LevelCatalog` only needs `BoardLayout.cs` + `BubbleColor.cs` + `TestBoards.cs`, and none
  of them touch a Unity API at runtime, so they compile into a plain console exe and RUN:
  csc with `-target:exe` + refs `netstandard.dll`, `UnityEngine.CoreModule.dll`,
  `UnityEngine.dll`; then copy those two Unity DLLs next to the exe, write a
  `verify.runtimeconfig.json` (`tfm net8.0`, framework `Microsoft.NETCore.App` 8.0.0) and
  run it with `NetCoreRuntime/dotnet.exe`. Dumps real boards/shots/star inputs with no
  editor and no transcription risk. Scratchpad: `Verify.cs`. Keeping a hand-written
  replication ALONGSIDE it is still worth it as a cross-check (both agreed cell-for-cell
  on all 70 boards after the port).
- **RNG-STREAM LESSON (cost a false alarm 2026-07-19):** changing ONE spec number moves
  cells that number has nothing to do with. Dropping Reef 55's stones 11 → 10 stopped the
  Pillars fallback from firing, which consumed different rng draws, which relocated the
  board's ICE. So a board dump is only comparable against a dump from the SAME spec
  revision — re-dump both sides after any table edit before concluding "they diverged".
- NOTE: `csc.dll` now lives at `<UnityData>/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll`
  (the old `DotNetSdkRoslyn/` path in the compile-check line above is stale for 6000.5.3f1).

## Out of scope until data justifies (game plan §7)

No meta/economy depth, no ads/analytics code yet (Phase 3), no extra themes/modes. Don't build early.
