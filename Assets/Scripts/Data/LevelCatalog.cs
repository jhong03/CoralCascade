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
    ///   MAIN ("Adventure") — the game proper: 50 deterministically generated levels
    ///   (Reef 1–50) structured as 10 themed WAVES of 5 (restructure 2026-07-17; the
    ///   old smooth t-formulas are gone). Within a wave the first level is a breather
    ///   and the last is the milestone. The level number seeds the generator, so every
    ///   visit serves the identical board — fair replays, comparable best scores.
    ///
    /// THE WAVES (teaching order matches the Tutorial Reef: stone → ice → tide):
    ///    1  Reefs  1–5   First Dive        pure cascade play, 4 colors
    ///    2  Reefs  6–10  Stone Garden      stones introduced (GUARANTEED counts)
    ///    3  Reefs 11–15  Frozen Shallows   ice introduced; 5th color at 14
    ///    4  Reefs 16–20  Rising Tide       tide introduced (every 10 shots)
    ///    5  Reefs 21–25  Critter Cove      rescue critters introduced (breather wave)
    ///    6  Reefs 26–30  Deepwater         first stone+ice+tide combinations
    ///    7  Reefs 31–35  Stonefall Trench  stone-heavy under tide; 6th color at 34
    ///    8  Reefs 36–40  Glacier Line      ice-heavy, critters behind the ice
    ///    9  Reefs 41–45  Storm Surge       fast tide (every 7), wide boards
    ///   10  Reefs 46–50  The Abyss         everything combined; challenging but fair
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
        public const int MainLevels = 50;

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
        /// date-picked from the Reef 16–45 band (waves 4–9), so days always have the tide
        /// and range across obstacles/critters. Best score / stars key off the level NAME,
        /// which embeds the date — per-day records fall out of name-keyed persistence.
        /// </summary>
        public static BoardLayoutData Daily(DateTime date)
        {
            int dateKey = date.Year * 10000 + date.Month * 100 + date.Day;
            var pick = new Random(dateKey * 131 + 17);
            var spec = Specs[15 + pick.Next(30)]; // Reefs 16–45
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
        //   budget              = shot-budget multiplier (>1 = kinder, <1 = tighter).

        private struct LevelSpec
        {
            public int Cols, Rows, Colors;
            public double Density;
            public int Stones; public double StonePct;
            public int Ice;    public double IcePct;
            public int Critters;
            public int TideEvery, DangerMargin;
            public double BudgetMul;
        }

        private static LevelSpec S(int cols, int rows, int colors, double density,
                                   int stones = 0, double stonePct = 0,
                                   int ice = 0, double icePct = 0,
                                   int critters = 0, int tide = 0, int margin = 5,
                                   double budget = 1.0)
        {
            return new LevelSpec
            {
                Cols = cols, Rows = rows, Colors = colors, Density = density,
                Stones = stones, StonePct = stonePct, Ice = ice, IcePct = icePct,
                Critters = critters, TideEvery = tide, DangerMargin = margin,
                BudgetMul = budget
            };
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

            // Wave 5 — Critter Cove (21–25): rescues introduced; a breather wave.
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

            // Wave 10 — The Abyss (46–50): everything, tuned fair; Reef 50 is the finale.
            S(13, 11, 6, 0.74, stones: 5, stonePct: 0.03, ice: 5, icePct: 0.03, tide: 8, margin: 4, critters: 2, budget: 1.10),
            S(13, 11, 6, 0.76, stones: 6, stonePct: 0.03, ice: 6, icePct: 0.03, tide: 7, margin: 4, critters: 2, budget: 1.05),
            S(13, 11, 6, 0.78, stones: 7, stonePct: 0.03, ice: 7, icePct: 0.03, tide: 7, margin: 3, critters: 3, budget: 1.00),
            S(13, 11, 6, 0.80, stones: 8, stonePct: 0.04, ice: 8, icePct: 0.04, tide: 7, margin: 3, critters: 3, budget: 1.00),
            S(13, 11, 6, 0.82, stones: 9, stonePct: 0.04, ice: 9, icePct: 0.04, tide: 7, margin: 3, critters: 5, budget: 1.00),
        };

        // ---- Generation ----------------------------------------------------------------------

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
                double rowDensity = spec.Density * (1.0 - r * 0.055);
                for (int c = 0; c < columns; c++)
                {
                    // Up-neighbors in odd-r: even rows also touch (c-1,r-1); odd rows (c+1,r-1).
                    char up1 = grid[r - 1][c];
                    int c2 = (r % 2 == 0) ? c - 1 : c + 1;
                    char up2 = (c2 >= 0 && c2 < columns) ? grid[r - 1][c2] : '.';
                    if (up1 == '.' && up2 == '.') continue;
                    if (rng.NextDouble() >= rowDensity) continue;

                    // Inherit a NORMALIZED neighbor color (obstacles must not propagate
                    // through the clumping bias — 'S'/'C' inherit as random, 'g' as 'G').
                    char color = '.';
                    if (rng.NextDouble() < 0.55)
                        color = NormalizeColor(up1 != '.' ? up1 : up2);
                    else if (c > 0 && grid[r][c - 1] != '.' && rng.NextDouble() < 0.4)
                        color = NormalizeColor(grid[r][c - 1]);
                    grid[r][c] = color != '.' ? color : palette[rng.Next(spec.Colors)];

                    // Sprinkle texture (r >= 1 only, so stones never touch the anchor row).
                    if (rng.NextDouble() < spec.StonePct)
                        grid[r][c] = 'S';
                    else if (rng.NextDouble() < spec.IcePct)
                        grid[r][c] = char.ToLowerInvariant(grid[r][c]);
                }
            }

            // GUARANTEED mechanics (the wave identity): convert already-placed cells, so
            // anchoring is untouched. Critters first — they have the tightest row rule
            // (rows 2+: buried enough that freeing one is a real act, never hard-locked
            // because only row 0 anchors and critters never sit there).
            ConvertCells(grid, rng, spec.Critters, minRow: 2, ch => 'C');
            ConvertCells(grid, rng, spec.Stones,   minRow: 1, ch => 'S');
            ConvertCells(grid, rng, spec.Ice,      minRow: 1, char.ToLowerInvariant);

            var cellRows = new string[rows];
            int bubbles = 0;
            for (int r = 0; r < rows; r++)
            {
                cellRows[r] = new string(grid[r]);
                foreach (char ch in grid[r]) if (ch != '.') bubbles++;
            }

            int pressureEvery = spec.TideEvery;
            int dangerRow = pressureEvery > 0 ? Math.Min(14, rows + spec.DangerMargin) : 0;

            // Shot budget accounts for what makes shots miss: base 0.5/bubble; obstacles
            // and critters need DROPS, not matches (+0.4 each, ice +0.25 for the thaw
            // detour — sprinkles enter as expected counts); +12% per color beyond 4;
            // + ~40% of the expected tide influx per shot (fresh anchor rows clear
            // cheaply; 0.7 = BoardManager.PressureRowFill, change together). The wave's
            // BudgetMul is the fairness dial on top. Cap raised 40 → 45 for the Abyss
            // boards; the cascade shot-refund (BoardManager) carries the rest.
            int hardCount = spec.Stones + spec.Critters;
            double softCount = bubbles * (spec.StonePct + spec.IcePct);
            double budget = bubbles * 0.5 + hardCount * 0.4 + spec.Ice * 0.25 + softCount * 0.35;
            budget *= 1.0 + 0.12 * (spec.Colors - 4);
            if (pressureEvery > 0)
                budget *= 1.0 + 0.4 * (columns * 0.7 / pressureEvery);
            budget *= spec.BudgetMul;
            int shots = Math.Max(10, Math.Min(45, (int)Math.Round(budget)));

            return new BoardLayoutData(name, columns, cellRows, shots,
                                       pressureEvery, dangerRow);
        }

        /// <summary>
        /// Deterministically converts <paramref name="count"/> eligible cells in place —
        /// eligible = occupied, at/below <paramref name="minRow"/>, still a plain playable
        /// color (mechanics never stack on each other). Selection is an rng shuffle of the
        /// candidate list, so it's part of the same deterministic stream as generation.
        /// Places fewer when the board is too small to host them all.
        /// </summary>
        private static void ConvertCells(char[][] grid, Random rng, int count, int minRow,
                                         Func<char, char> convert)
        {
            if (count <= 0) return;
            var candidates = new List<(int r, int c)>();
            for (int r = minRow; r < grid.Length; r++)
                for (int c = 0; c < grid[r].Length; c++)
                    if (grid[r][c] >= 'A' && grid[r][c] <= 'Z' && grid[r][c] != 'S' && grid[r][c] != 'C')
                        candidates.Add((r, c));

            // Fisher–Yates on the candidate list, then take the first `count`.
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            for (int i = 0; i < count && i < candidates.Count; i++)
            {
                var (r, c) = candidates[i];
                grid[r][c] = convert(grid[r][c]);
            }
        }

        /// <summary>Strips obstacle state for color inheritance: stone/critter → none, frozen → thawed.</summary>
        private static char NormalizeColor(char ch)
        {
            if (ch == 'S' || ch == 'C' || ch == '.') return '.';
            return char.ToUpperInvariant(ch);
        }
    }
}
