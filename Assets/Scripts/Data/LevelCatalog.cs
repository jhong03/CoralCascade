using System;
using System.Collections.Generic;

namespace CoralCascade
{
    /// <summary>
    /// The game's two level sections, each with its OWN progression (see Progress):
    ///
    ///   INTRO ("Tutorial Reef") — the 8 hand-authored boards (TestBoards). They teach
    ///   drops, overhangs, thread bridges, fortresses, chain reactors, stone, ice and
    ///   the tide in order, and double as the Phase 1 acceptance boards.
    ///
    ///   MAIN ("Adventure") — the game proper: 70 deterministically generated levels
    ///   (Reef 1–70) structured as 14 themed WAVES of 5 (restructure 2026-07-17,
    ///   extended 2026-07-19; the old smooth t-formulas are gone). Within a wave the
    ///   first level is a breather and the last is the milestone. The level number seeds
    ///   the generator, so every visit serves the identical board — fair replays,
    ///   comparable best scores.
    ///
    /// THE WAVES (teaching order matches the Tutorial Reef: stone → ice → tide):
    ///    1  Reefs  1–5   First Dive        pure cascade play, 4 colors
    ///    2  Reefs  6–10  Stone Garden      stones introduced (GUARANTEED counts)
    ///    3  Reefs 11–15  Frozen Shallows   ice introduced; 5th color at 14
    ///    4  Reefs 16–20  Rising Tide       tide introduced (every 10 shots)
    ///    5  Reefs 21–25  Critter Cove      critters introduced (obstacle; breather wave)
    ///    6  Reefs 26–30  Deepwater         first stone+ice+tide combinations
    ///    7  Reefs 31–35  Stonefall Trench  stone-heavy under tide; 6th color at 34
    ///    8  Reefs 36–40  Glacier Line      ice-heavy, critters behind the ice
    ///    9  Reefs 41–45  Storm Surge       fast tide (every 7), wide boards
    ///   10  Reefs 46–50  The Abyss         everything combined
    ///   11  Reefs 51–55  Sunken Ruins      MOTIF Pillars — stone columns to topple
    ///   12  Reefs 56–60  Glacier Vault     MOTIF Lattice — ice on a crystalline grid
    ///   13  Reefs 61–65  Sunless Chasm     MOTIF Chasm — a rift splits the board
    ///   14  Reefs 66–70  Leviathan's Rest  MOTIF Spires — deep towers; the finale
    ///
    /// From Reef 51 the board SIZE is maxed out (13 columns is already ~28 logical points
    /// per bubble on a phone), so waves 11–14 get their identity from board SHAPE — see
    /// the Motif enum — rather than from more grid. No new rules for the player to learn.
    ///
    /// Mechanic introductions are GUARANTEED, not sprinkled: each spec carries exact
    /// stone/ice/critter counts placed deterministically after generation (plus optional
    /// sprinkle percentages for background texture). Placement converts already-anchored
    /// cells in place, so anchoring-by-construction is untouched: a bubble is only placed
    /// if it has an occupied up-neighbor (odd-r aware), so connectivity to row 0 holds by
    /// induction — no floating bubbles at load.
    /// </summary>
    public static class LevelCatalog
    {
        public const int MainLevels = 70;

        /// <summary>Tutorial section — the hand-authored intro boards.</summary>
        public static List<BoardLayoutData> Intro => TestBoards.All;

        private static List<BoardLayoutData> _main;
        public static List<BoardLayoutData> Main => _main ?? (_main = BuildMain());

        private static List<BoardLayoutData> BuildMain()
        {
            var list = new List<BoardLayoutData>(MainLevels);
            for (int n = 1; n <= MainLevels; n++)
                list.Add(GenerateBalanced($"Reef {n}", n * 7919 + 12345, Specs[n - 1]));
            return list;
        }

        /// <summary>
        /// The Daily Reef: one fresh level per calendar day, seeded by the date — every
        /// player (and every retry) gets the identical board that day. The spec is
        /// date-picked from the Reef 16–60 band (waves 4–12), so days always have the tide
        /// and range across obstacles, critters and the shape motifs. The band deliberately
        /// stops short of the finale waves. Best score / stars key off the level NAME,
        /// which embeds the date — per-day records fall out of name-keyed persistence.
        /// </summary>
        public static BoardLayoutData Daily(DateTime date)
        {
            int dateKey = date.Year * 10000 + date.Month * 100 + date.Day;
            var pick = new Random(dateKey * 131 + 17);
            var spec = Specs[15 + pick.Next(45)]; // Reefs 16–60
            return GenerateBalanced($"Daily {date:yyyy-MM-dd}", dateKey ^ 0x5EEF, spec);
        }

        /// <summary>
        /// The generator's bubble count swings wildly with anchor-row luck (a gappy row 0
        /// starves everything below — the raw spread was 28..77 bubbles on same-size specs),
        /// which turned authored breathers into accidental spikes. So: generate a few
        /// candidate boards on a deterministic seed ladder and keep the one closest to the
        /// candidates' MEDIAN mass. No calibration constants, still fully deterministic —
        /// the same name/seed/spec always selects the same board.
        /// </summary>
        private static BoardLayoutData GenerateBalanced(string name, int baseSeed, LevelSpec spec)
        {
            const int Candidates = 9;
            var boards = new BoardLayoutData[Candidates];
            var counts = new int[Candidates];
            for (int k = 0; k < Candidates; k++)
            {
                boards[k] = Generate(name, baseSeed + k * 100003, spec);
                counts[k] = boards[k].BubbleCount;
            }
            var sorted = (int[])counts.Clone();
            Array.Sort(sorted);
            int median = sorted[Candidates / 2];
            int best = 0;
            for (int k = 1; k < Candidates; k++)
                if (Math.Abs(counts[k] - median) < Math.Abs(counts[best] - median))
                    best = k;
            return boards[best];
        }

        // ---- The difficulty curve, one row per level ---------------------------------------
        //
        // THIS TABLE *IS* THE ADVENTURE STRUCTURE — tune the game here, not in formulas.
        //   S(cols, rows, colors, density,
        //     stones, stonePct, ice, icePct, critters, tide, margin, budget)
        //   stones/ice/critters = GUARANTEED counts (converted after generation);
        //   stonePct/icePct     = extra per-cell sprinkle chance for texture;
        //   tide                = pressure drop every N shots (0 = no tide);
        //   margin              = danger line this many rows below the layout's bottom;
        //   budget              = shot-budget multiplier (>1 = kinder, <1 = tighter);
        //   motif               = board SHAPE (see Motif) — waves 11–14 only.
        //
        // NOTE on margin: the danger row is min(14, rows + margin), and every 11-row board
        // hits that ceiling, so on the widest boards any margin >= 3 gives the same line.

        /// <summary>
        /// Board-shape motifs — the identity of waves 11–14, added when the level count
        /// went 50 → 70 and the grid had no room left to grow. Two kinds, both of which
        /// preserve anchoring-by-construction:
        ///
        ///   DENSITY MASKS (Chasm, Spires, Pillars) multiply one cell's placement chance
        ///   DURING generation, so the up-neighbour rule still gates every placement — a
        ///   masked board can no more float than an unmasked one. Carving cells out AFTER
        ///   generation would orphan everything below the hole; never do that.
        ///
        ///   PLACEMENT PATTERNS (Pillars) shape where the guaranteed obstacles land.
        ///
        /// Motif.None returns a 1.0 multiplier and the original placement path, and
        /// multiplying a double by 1.0 is exact — so Reefs 1–50 stay bit-identical.
        /// </summary>
        private enum Motif { None = 0, Pillars, Lattice, Chasm, Spires }

        private struct LevelSpec
        {
            public int Cols, Rows, Colors;
            public double Density;
            public int Stones; public double StonePct;
            public int Ice;    public double IcePct;
            public int Critters;
            public int TideEvery, DangerMargin;
            public double BudgetMul;
            public Motif Motif;
        }

        private static LevelSpec S(int cols, int rows, int colors, double density,
                                   int stones = 0, double stonePct = 0,
                                   int ice = 0, double icePct = 0,
                                   int critters = 0, int tide = 0, int margin = 5,
                                   double budget = 1.0, Motif motif = Motif.None)
        {
            return new LevelSpec
            {
                Cols = cols, Rows = rows, Colors = colors, Density = density,
                Stones = stones, StonePct = stonePct, Ice = ice, IcePct = icePct,
                Critters = critters, TideEvery = tide, DangerMargin = margin,
                BudgetMul = budget, Motif = motif
            };
        }

        /// <summary>
        /// Per-cell density multiplier for the shape motifs. 1.0 leaves generation exactly
        /// as it was. Values above 1.0 defeat the downward taper (that's how Spires grow
        /// long towers where a plain board would have petered out).
        /// </summary>
        private static double MotifDensity(Motif motif, int r, int c, int rows, int cols)
        {
            switch (motif)
            {
                // Thicken the columns the stones will later fill, so the pillars have
                // something to be made OF — a sparse column yields scattered stones.
                case Motif.Pillars: return (r >= 3 && (c % 3) == 1) ? 1.6 : 1.0;
                // A hollow rift down the middle, below the always-solid ceiling band.
                case Motif.Chasm:   return (r >= 3 && Math.Abs(c - (cols - 1) / 2.0) <= cols / 6.0) ? 0.10 : 1.0;
                // Paired columns plunge to the floor; the rest stop at the band. Cutting a
                // tower's top drops the whole thing — the finale's answer to the tide.
                case Motif.Spires:  return r >= 3 ? ((c % 4) < 2 ? 1.5 : 0.10) : 1.0;
                default:            return 1.0;
            }
        }

        private static readonly LevelSpec[] Specs =
        {
            // Wave 1 — First Dive (1–5): pure cascade schooling.
            S(9, 6, 4, 0.62, budget: 1.25),
            S(9, 6, 4, 0.66, budget: 1.20),
            S(9, 6, 4, 0.70, budget: 1.10),
            S(9, 6, 4, 0.72, budget: 1.05),
            S(9, 7, 4, 0.74, budget: 1.00),

            // Wave 2 — Stone Garden (6–10): stones guaranteed from day one.
            S(9,  7, 4, 0.68, stones: 3, budget: 1.15),
            S(9,  7, 4, 0.72, stones: 4, budget: 1.05),
            S(10, 7, 4, 0.72, stones: 5, budget: 1.05),
            S(10, 7, 4, 0.74, stones: 6, budget: 1.00),
            S(10, 7, 4, 0.78, stones: 8, budget: 0.95),

            // Wave 3 — Frozen Shallows (11–15): ice guaranteed; 5th color lands at 14.
            S(10, 7, 4, 0.70, ice: 3, stonePct: 0.02, budget: 1.15),
            S(10, 7, 4, 0.72, ice: 4, stonePct: 0.02, budget: 1.05),
            S(10, 8, 4, 0.74, ice: 5, stonePct: 0.02, budget: 1.05),
            S(10, 8, 5, 0.74, ice: 6, budget: 1.05),
            S(10, 8, 5, 0.76, ice: 8, stones: 2, budget: 0.95),

            // Wave 4 — Rising Tide (16–20): the tide enters on a clean board.
            S(11, 8, 5, 0.68, tide: 10, margin: 6, budget: 1.15),
            S(11, 8, 5, 0.70, tide: 10, margin: 6, stonePct: 0.02, budget: 1.10),
            S(11, 8, 5, 0.72, tide: 10, margin: 5, stones: 3, budget: 1.05),
            S(11, 8, 5, 0.74, tide: 10, margin: 5, icePct: 0.02, budget: 1.00),
            S(11, 8, 5, 0.76, tide: 9,  margin: 5, stones: 5, budget: 0.95),

            // Wave 5 — Critter Cove (21–25): critters introduced (drop-to-clear obstacle); a breather wave.
            S(11, 9, 5, 0.68, critters: 2, budget: 1.20),
            S(11, 9, 5, 0.70, critters: 2, stonePct: 0.02, budget: 1.15),
            S(11, 9, 5, 0.72, critters: 3, icePct: 0.02, budget: 1.10),
            S(11, 9, 5, 0.74, critters: 3, stonePct: 0.02, icePct: 0.02, budget: 1.05),
            S(11, 9, 5, 0.76, critters: 4, tide: 10, margin: 5, budget: 1.00),

            // Wave 6 — Deepwater (26–30): the first true combinations.
            S(12, 9, 5, 0.70, stones: 3, ice: 3, budget: 1.10),
            S(12, 9, 5, 0.72, stones: 4, ice: 4, tide: 9, margin: 5, budget: 1.05),
            S(12, 9, 5, 0.74, stones: 5, ice: 4, tide: 9, margin: 5, budget: 1.00),
            S(12, 9, 5, 0.75, stones: 5, ice: 5, tide: 9, margin: 4, critters: 1, budget: 1.00),
            S(12, 9, 5, 0.78, stones: 6, ice: 6, tide: 9, margin: 4, critters: 2, budget: 0.95),

            // Wave 7 — Stonefall Trench (31–35): stone-heavy under tide; 6th color at 34.
            S(12, 10, 5, 0.72, stones: 6,  stonePct: 0.03, tide: 9, margin: 5, budget: 1.10),
            S(12, 10, 5, 0.74, stones: 8,  stonePct: 0.03, tide: 9, margin: 4, budget: 1.05),
            S(12, 10, 5, 0.75, stones: 9,  stonePct: 0.04, tide: 9, margin: 4, budget: 1.00),
            S(12, 10, 6, 0.75, stones: 10, stonePct: 0.04, tide: 9, margin: 4, budget: 1.05),
            S(12, 10, 6, 0.78, stones: 12, stonePct: 0.04, tide: 8, margin: 4, critters: 1, budget: 0.95),

            // Wave 8 — Glacier Line (36–40): ice-heavy, critters trapped behind it.
            S(12, 10, 6, 0.72, ice: 6,  icePct: 0.03, tide: 8, margin: 5, critters: 2, budget: 1.10),
            S(12, 10, 6, 0.74, ice: 8,  icePct: 0.03, tide: 8, margin: 4, critters: 2, budget: 1.05),
            S(13, 10, 6, 0.75, ice: 9,  icePct: 0.04, tide: 8, margin: 4, critters: 2, budget: 1.00),
            S(13, 10, 6, 0.76, ice: 10, icePct: 0.04, stones: 3, tide: 8, margin: 4, critters: 3, budget: 1.00),
            S(13, 10, 6, 0.78, ice: 12, icePct: 0.04, stones: 4, tide: 8, margin: 4, critters: 3, budget: 0.95),

            // Wave 9 — Storm Surge (41–45): pace pressure, not clutter.
            S(13, 11, 6, 0.70, tide: 8, margin: 4, stonePct: 0.02, icePct: 0.02, budget: 1.10),
            S(13, 11, 6, 0.72, tide: 7, margin: 4, stonePct: 0.02, icePct: 0.02, budget: 1.05),
            S(13, 11, 6, 0.73, tide: 7, margin: 4, stones: 3, icePct: 0.02, critters: 1, budget: 1.05),
            S(13, 11, 6, 0.74, tide: 7, margin: 3, ice: 3, stonePct: 0.02, budget: 1.00),
            S(13, 11, 6, 0.76, tide: 7, margin: 3, stones: 4, ice: 4, critters: 2, budget: 0.95),

            // Wave 10 — The Abyss (46–50): everything combined. TRIMMED 2026-07-19 — it is
            // no longer the finale, and at 13 columns under the tide a 75-shot budget only
            // supports ~50 bubbles (see the shot-cap note in Generate).
            S(13, 11, 6, 0.68, stones: 4, stonePct: 0.02, ice: 4, icePct: 0.02, tide: 8, margin: 5, critters: 2, budget: 1.15),
            S(13, 11, 6, 0.70, stones: 5, stonePct: 0.02, ice: 5, icePct: 0.02, tide: 8, margin: 4, critters: 2, budget: 1.10),
            S(13, 11, 6, 0.70, stones: 6, stonePct: 0.03, ice: 6, icePct: 0.03, tide: 7, margin: 4, critters: 2, budget: 1.05),
            S(13, 11, 6, 0.72, stones: 7, stonePct: 0.03, ice: 7, icePct: 0.03, tide: 7, margin: 4, critters: 3, budget: 1.05),
            S(13, 11, 6, 0.72, stones: 8, stonePct: 0.03, ice: 8, icePct: 0.03, tide: 7, margin: 3, critters: 3, budget: 1.00),

            // Wave 11 — Sunken Ruins (51–55): PILLARS. Stones stack into ruined columns
            // instead of scattering; cut the plain rows above one and the whole pillar
            // topples. No stone sprinkle here — it would blur the motif.
            S(13, 11, 6, 0.60, stones: 7,  tide: 9, margin: 5, icePct: 0.02, budget: 1.15, motif: Motif.Pillars),
            S(13, 11, 6, 0.62, stones: 8,  tide: 9, margin: 4, icePct: 0.02, critters: 2, budget: 1.10, motif: Motif.Pillars),
            S(13, 11, 6, 0.64, stones: 9,  tide: 8, margin: 4, ice: 3, critters: 2, budget: 1.05, motif: Motif.Pillars),
            S(13, 11, 6, 0.64, stones: 9,  tide: 8, margin: 4, ice: 4, critters: 2, budget: 1.05, motif: Motif.Pillars),
            S(13, 11, 6, 0.66, stones: 10, tide: 8, margin: 4, ice: 5, critters: 2, budget: 1.00, motif: Motif.Pillars),

            // Wave 12 — Glacier Vault (56–60): LATTICE. Ice lands on a crystalline grid,
            // so thawing becomes a routing puzzle rather than a scatter of chores.
            S(13, 11, 6, 0.66, ice: 9,  icePct: 0.02, stonePct: 0.02, tide: 9, margin: 5, budget: 1.15, motif: Motif.Lattice),
            S(13, 11, 6, 0.68, ice: 11, icePct: 0.03, stonePct: 0.02, tide: 9, margin: 4, critters: 2, budget: 1.10, motif: Motif.Lattice),
            S(13, 11, 6, 0.70, ice: 12, icePct: 0.03, stones: 3, tide: 8, margin: 4, critters: 2, budget: 1.05, motif: Motif.Lattice),
            S(13, 11, 6, 0.70, ice: 13, icePct: 0.04, stones: 4, tide: 8, margin: 4, critters: 2, budget: 1.05, motif: Motif.Lattice),
            S(13, 11, 6, 0.72, ice: 14, icePct: 0.04, stones: 5, stonePct: 0.02, tide: 8, margin: 4, critters: 3, budget: 1.00, motif: Motif.Lattice),

            // Wave 13 — Sunless Chasm (61–65): CHASM. A rift splits the board into two
            // masses under the ceiling band, and the tide runs at its fastest yet.
            S(13, 11, 6, 0.78, stonePct: 0.02, icePct: 0.02, tide: 7, margin: 5, budget: 1.15, motif: Motif.Chasm),
            S(13, 11, 6, 0.80, stones: 4, stonePct: 0.02, ice: 4, icePct: 0.02, tide: 7, margin: 4, critters: 2, budget: 1.10, motif: Motif.Chasm),
            S(13, 11, 6, 0.82, stones: 5, stonePct: 0.03, ice: 5, icePct: 0.03, tide: 6, margin: 4, critters: 2, budget: 1.05, motif: Motif.Chasm),
            S(13, 11, 6, 0.84, stones: 6, stonePct: 0.03, ice: 6, icePct: 0.03, tide: 6, margin: 4, critters: 3, budget: 1.05, motif: Motif.Chasm),
            S(13, 11, 6, 0.84, stones: 6, stonePct: 0.03, ice: 7, icePct: 0.03, tide: 6, margin: 3, critters: 3, budget: 1.00, motif: Motif.Chasm),

            // Wave 14 — Leviathan's Rest (66–70): SPIRES, the finale. Towers reach the
            // floor, so the tide's slack is thin — but a single cut at a tower's top
            // drops it whole. Reef 70 is the hardest board in the game by design.
            S(13, 11, 6, 0.68, stones: 4, stonePct: 0.02, ice: 5,  icePct: 0.02, tide: 8, margin: 5, critters: 2, budget: 1.15, motif: Motif.Spires),
            S(13, 11, 6, 0.70, stones: 4, stonePct: 0.03, ice: 6,  icePct: 0.03, tide: 7, margin: 5, critters: 2, budget: 1.10, motif: Motif.Spires),
            S(13, 11, 6, 0.72, stones: 5, stonePct: 0.03, ice: 7,  icePct: 0.03, tide: 7, margin: 5, critters: 2, budget: 1.05, motif: Motif.Spires),
            S(13, 11, 6, 0.74, stones: 5, stonePct: 0.04, ice: 8,  icePct: 0.04, tide: 7, margin: 5, critters: 3, budget: 1.05, motif: Motif.Spires),
            S(13, 11, 6, 0.76, stones: 6, stonePct: 0.04, ice: 10, icePct: 0.04, tide: 6, margin: 5, critters: 3, budget: 1.00, motif: Motif.Spires),
        };

        // ---- Generation ----------------------------------------------------------------------

        /// <summary>
        /// Shots added to every authored tide interval (see the TIDE RETUNE note in Generate).
        /// The Specs table keeps its original cadence numbers; this is the single global dial
        /// that slows them all down, so re-tuning the tide never means editing 30 rows.
        /// </summary>
        private const int TideIntervalBump = 5;

        /// <summary>Longest a level may run — a playtime ceiling, not a difficulty one.</summary>
        private const int ShotCap = 75;

        /// <summary>Shots guaranteed after the final tide drop, so it can always be answered.</summary>
        private const int MinShotsAfterDrop = 4;

        private static BoardLayoutData Generate(string name, int seed, LevelSpec spec)
        {
            // Deterministic: never UnityEngine.Random here. Same seed + spec = same board.
            var rng = new Random(seed);
            int columns = spec.Cols, rows = spec.Rows;

            char[] palette = { 'R', 'O', 'Y', 'G', 'B', 'P' };
            var grid = new char[rows][];
            for (int r = 0; r < rows; r++)
            {
                grid[r] = new char[columns];
                for (int c = 0; c < columns; c++) grid[r][c] = '.';
            }

            // Row 0: dense anchor row.
            for (int c = 0; c < columns; c++)
                if (rng.NextDouble() < 0.94)
                    grid[0][c] = palette[rng.Next(spec.Colors)];

            // Lower rows: place only where an occupied up-neighbor exists (anchoring by
            // induction), density tapering downward so boards end in pockets and tips.
            for (int r = 1; r < rows; r++)
            {
                double rowBase = spec.Density * (1.0 - r * 0.055);
                for (int c = 0; c < columns; c++)
                {
                    // Up-neighbors in odd-r: even rows also touch (c-1,r-1); odd rows (c+1,r-1).
                    char up1 = grid[r - 1][c];
                    int c2 = (r % 2 == 0) ? c - 1 : c + 1;
                    char up2 = (c2 >= 0 && c2 < columns) ? grid[r - 1][c2] : '.';
                    if (up1 == '.' && up2 == '.') continue;
                    // The motif mask rides on top of the taper — it can only change WHICH
                    // anchored cells get filled, never whether the anchor rule applies.
                    double rowDensity = rowBase * MotifDensity(spec.Motif, r, c, rows, columns);
                    if (rng.NextDouble() >= rowDensity) continue;

                    // Inherit a NORMALIZED neighbor color (obstacles must not propagate
                    // through the clumping bias — 'S'/'C' inherit as random, 'g' as 'G').
                    char color = '.';
                    if (rng.NextDouble() < 0.55)
                        color = NormalizeColor(up1 != '.' ? up1 : up2);
                    else if (c > 0 && grid[r][c - 1] != '.' && rng.NextDouble() < 0.4)
                        color = NormalizeColor(grid[r][c - 1]);
                    grid[r][c] = color != '.' ? color : palette[rng.Next(spec.Colors)];

                    // Sprinkle texture. Stones obey the SAME off-ceiling/off-edge rule as the
                    // guaranteed ones (rows 3+, interior columns) so a sprinkle can't seal a
                    // top-row anchor either. The rng draw is still consumed when suppressed so
                    // the stream stays put; the cell just keeps its color instead.
                    if (rng.NextDouble() < spec.StonePct)
                    {
                        if (r >= 3 && c > 0 && c < columns - 1) grid[r][c] = 'S';
                    }
                    else if (rng.NextDouble() < spec.IcePct)
                        grid[r][c] = char.ToLowerInvariant(grid[r][c]);
                }
            }

            // GUARANTEED mechanics (the wave identity): convert already-placed cells, so
            // anchoring is untouched. Critters first — they have the tightest row rule
            // (rows 2+: buried enough that freeing one is a real act, never hard-locked
            // because only row 0 anchors and critters never sit there).
            ConvertCells(grid, rng, spec.Critters, minRow: 2, ch => 'C');
            // Stones stay OFF the ceiling rows and outer columns: a stone sealing a top-row
            // anchor (removable only by matching, never by dropping) makes the board
            // UNWINNABLE — user-reported dead board on Reef 10 (2026-07-18). Rows 3+, interior.
            // The Pillars motif stacks them into columns but obeys the identical rule.
            if (spec.Motif == Motif.Pillars) ConvertPillars(grid, rng, spec.Stones, minRow: 3);
            else ConvertCells(grid, rng, spec.Stones, minRow: 3, ch => 'S', interiorCols: true);
            ConvertCells(grid, rng, spec.Ice, minRow: 1, char.ToLowerInvariant,
                         where: spec.Motif == Motif.Lattice ? (Func<int, int, bool>)LatticeCell : null);

            var cellRows = new string[rows];
            int bubbles = 0;
            for (int r = 0; r < rows; r++)
            {
                cellRows[r] = new string(grid[r]);
                foreach (char ch in grid[r]) if (ch != '.') bubbles++;
            }

            // TIDE RETUNE 2026-07-19 (user-reported: Reef 18 unwinnable). The tide was
            // outrunning the player — a 70%-fill row every ~10 shots added ~58% more bubbles
            // while the budget only granted +31% more shots, so tide levels demanded ~1.9-2.1
            // bubbles cleared PER SHOT to win, and on some reefs a drop landed on the final
            // shot (zero shots to clear it = guaranteed loss). Three dials moved together:
            // drops are RARER (TideIntervalBump), rows are LIGHTER (PressureRowFill 0.7→0.5),
            // and the budget compensates far more (0.4 → 1.6). Verified: the wave now needs
            // ~1.27-1.42/shot with 6-13 shots of recovery after the last drop.
            int pressureEvery = spec.TideEvery > 0 ? spec.TideEvery + TideIntervalBump : 0;
            int dangerRow = pressureEvery > 0 ? Math.Min(14, rows + spec.DangerMargin) : 0;

            // Shot budget accounts for what makes shots miss: base 0.5/bubble; obstacles
            // and critters need DROPS, not matches (+0.4 each, ice +0.25 for the thaw
            // detour — sprinkles enter as expected counts); +12% per color beyond 4;
            // + the expected tide influx per shot (0.5 = BoardManager.PressureRowFill,
            // CHANGE TOGETHER). The wave's BudgetMul is the fairness dial on top. Cap
            // 45 → 50; the cascade shot-refund (BoardManager) carries the rest.
            int hardCount = spec.Stones + spec.Critters;
            double softCount = bubbles * (spec.StonePct + spec.IcePct);
            double budget = bubbles * 0.5 + hardCount * 0.4 + spec.Ice * 0.25 + softCount * 0.35;
            budget *= 1.0 + 0.12 * (spec.Colors - 4);
            if (pressureEvery > 0)
                budget *= 1.0 + 1.6 * (columns * 0.5 / pressureEvery);
            budget *= spec.BudgetMul;

            // SHOT CAP 50 → 75 (2026-07-19, user-reported: "Level 50 not possible to win").
            // The old cap silently discarded the tide compensation the retune above had
            // just granted: Reef 50 computed a 95-shot budget and was handed 50 — 53% of
            // its own requirement — and every reef from 27 on was clipped to some degree,
            // which also flattened BudgetMul into a no-op across the whole late game.
            // 75 is a PLAYTIME ceiling (~4-5 min), so the heaviest boards were trimmed to
            // fit under it rather than the cap being raised to meet them.
            int shots = Math.Max(10, Math.Min(ShotCap, (int)Math.Round(budget)));

            // RECOVERY WINDOW: a tide drop landing on the final shot is an unavoidable
            // loss — a fresh ceiling row with no shots left to clear it. Reefs 16, 19 and
            // 25 all shipped in that state at some point. Guarantee shots to answer the
            // last drop with. Adding < PressureEveryShots can never create another drop,
            // so this only ever hands back the recovery window (max +3 over the cap).
            if (pressureEvery > 0)
            {
                int afterLastDrop = shots % pressureEvery;
                if (afterLastDrop < MinShotsAfterDrop) shots += MinShotsAfterDrop - afterLastDrop;
            }

            return new BoardLayoutData(name, columns, cellRows, shots,
                                       pressureEvery, dangerRow);
        }

        /// <summary>
        /// Deterministically converts <paramref name="count"/> eligible cells in place —
        /// eligible = occupied, at/below <paramref name="minRow"/>, still a plain playable
        /// color (mechanics never stack on each other). Selection is an rng shuffle of the
        /// candidate list, so it's part of the same deterministic stream as generation.
        /// Places fewer when the board is too small to host them all.
        ///
        /// <paramref name="where"/> (motif pattern) is a PREFERENCE, not a filter: cells
        /// matching it are used first, then the rest, so a sparse board still receives its
        /// full authored count instead of silently under-placing. With no pattern the
        /// spare list is empty and the rng stream is identical to the pre-motif version.
        /// </summary>
        private static void ConvertCells(char[][] grid, Random rng, int count, int minRow,
                                         Func<char, char> convert, bool interiorCols = false,
                                         Func<int, int, bool> where = null)
        {
            if (count <= 0) return;
            var candidates = new List<(int r, int c)>();
            var spares = new List<(int r, int c)>();
            for (int r = minRow; r < grid.Length; r++)
            {
                int cols = grid[r].Length;
                for (int c = 0; c < cols; c++)
                {
                    if (interiorCols && (c == 0 || c == cols - 1)) continue;
                    if (!(grid[r][c] >= 'A' && grid[r][c] <= 'Z' && grid[r][c] != 'S' && grid[r][c] != 'C'))
                        continue;
                    if (where != null && !where(r, c)) spares.Add((r, c));
                    else candidates.Add((r, c));
                }
            }

            Shuffle(candidates, rng);
            Shuffle(spares, rng); // no-op (zero rng draws) when there is no pattern
            candidates.AddRange(spares);
            for (int i = 0; i < count && i < candidates.Count; i++)
            {
                var (r, c) = candidates[i];
                grid[r][c] = convert(grid[r][c]);
            }
        }

        /// <summary>Fisher–Yates, drawing from the shared deterministic stream.</summary>
        private static void Shuffle(List<(int r, int c)> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>The Lattice motif's ice pattern — a diagonal crystalline grid.</summary>
        private static bool LatticeCell(int r, int c) => ((r + c) % 3) == 0;

        /// <summary>
        /// The Pillars motif: fill whole interior COLUMNS top-down so stones read as ruined
        /// columns rather than scatter. Obeys the identical safety rule as ConvertCells
        /// (rows 3+, interior columns), so rows 0–2 stay plain color and no ceiling anchor
        /// can be sealed — which also means the top band always connects the board across,
        /// however tall a pillar grows. Falls back to scattered placement for any stones
        /// the chosen columns were too sparse to hold.
        /// </summary>
        private static void ConvertPillars(char[][] grid, Random rng, int count, int minRow)
        {
            if (count <= 0) return;
            int cols = grid[0].Length;
            var pillarCols = new List<(int r, int c)>();
            for (int c = 1; c < cols - 1; c++)
                if ((c % 3) == 1) pillarCols.Add((0, c)); // r unused — reusing the shuffle
            Shuffle(pillarCols, rng);

            int placed = 0;
            foreach (var pc in pillarCols)
            {
                for (int r = minRow; r < grid.Length && placed < count; r++)
                {
                    char ch = grid[r][pc.c];
                    if (ch >= 'A' && ch <= 'Z' && ch != 'S' && ch != 'C')
                    {
                        grid[r][pc.c] = 'S';
                        placed++;
                    }
                }
                if (placed >= count) break;
            }
            if (placed < count)
                ConvertCells(grid, rng, count - placed, minRow, ch => 'S', interiorCols: true);
        }

        /// <summary>Strips obstacle state for color inheritance: stone/critter → none, frozen → thawed.</summary>
        private static char NormalizeColor(char ch)
        {
            if (ch == 'S' || ch == 'C' || ch == '.') return '.';
            return char.ToUpperInvariant(ch);
        }
    }
}
