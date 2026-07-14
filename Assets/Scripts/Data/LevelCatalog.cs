using System;
using System.Collections.Generic;

namespace CoralCascade
{
    /// <summary>
    /// The game's two level sections, each with its OWN progression (see Progress):
    ///
    ///   INTRO ("Tutorial Reef") — the 5 hand-authored boards (TestBoards). They teach
    ///   drops, overhangs, thread bridges, fortresses and chain reactors in order, and
    ///   they double as the Phase 1 acceptance boards.
    ///
    ///   MAIN ("Adventure") — the game proper: 30 deterministically generated levels
    ///   (Reef 1–30). The level number is the seed, so every visit serves the identical
    ///   board — fair replays, comparable best scores.
    ///
    /// Adventure difficulty ramps: width 9→13 columns, 6→11 rows, 4→6 colors, rising
    /// density, shot budgets derived from bubble count. Generated boards are anchored BY
    /// CONSTRUCTION: a bubble is only placed if it has an occupied up-neighbor (odd-r
    /// aware), so connectivity to row 0 holds by induction — no floating bubbles at load.
    /// Colors bias toward copying a neighbor so real clusters (and drop setups) form.
    /// </summary>
    public static class LevelCatalog
    {
        public const int MainLevels = 30;

        /// <summary>Tutorial section — the hand-authored intro boards.</summary>
        public static List<BoardLayoutData> Intro => TestBoards.All;

        private static List<BoardLayoutData> _main;
        public static List<BoardLayoutData> Main => _main ?? (_main = BuildMain());

        private static List<BoardLayoutData> BuildMain()
        {
            var list = new List<BoardLayoutData>(MainLevels);
            for (int n = 1; n <= MainLevels; n++)
                list.Add(Generate(n));
            return list;
        }

        /// <summary>
        /// The Daily Reef (roadmap step 4): one fresh level per calendar day, seeded by the
        /// date — every player (and every retry) gets the identical board that day. The
        /// difficulty band is also date-picked (Reef 7–24 equivalent), so days vary.
        /// Best score / stars key off the level NAME, which embeds the date — per-day
        /// records fall out of the existing persistence for free.
        /// </summary>
        public static BoardLayoutData Daily(DateTime date)
        {
            int dateKey = date.Year * 10000 + date.Month * 100 + date.Day;
            var pick = new Random(dateKey * 131 + 17);
            int t = 6 + pick.Next(18);
            return Generate($"Daily {date:yyyy-MM-dd}", dateKey ^ 0x5EEF, t);
        }

        private static BoardLayoutData Generate(int levelNumber)
        {
            // Deterministic: the level number IS the seed. Never use UnityEngine.Random
            // here. Seed/t formulas are FROZEN — changing them reshuffles every Reef.
            return Generate($"Reef {levelNumber}", levelNumber * 7919 + 12345, levelNumber - 1);
        }

        private static BoardLayoutData Generate(string name, int seed, int t)
        {
            var rng = new Random(seed);

            int columns = Math.Min(13, 9 + t / 7);
            int rows = Math.Min(11, 6 + t / 5);
            // Balance pass 2026-07-14 (late reefs were mathematically unwinnable): the 5th
            // and 6th colors arrive later — 6-color matching is the single harshest dial.
            int colorCount = Math.Min(6, 4 + (t >= 12 ? 1 : 0) + (t >= 24 ? 1 : 0));
            double density = Math.Min(0.85, 0.68 + t * 0.006);
            // Obstacles enter mid-Adventure and ramp gently. Stones only below row 0
            // (a row-0 stone could never be removed — see BubbleColor.Stone).
            double stoneChance = t >= 7 ? Math.Min(0.10, 0.02 + (t - 7) * 0.004) : 0.0;
            double iceChance = t >= 11 ? Math.Min(0.10, 0.02 + (t - 11) * 0.004) : 0.0;

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
                    grid[0][c] = palette[rng.Next(colorCount)];

            // Lower rows: place only where an occupied up-neighbor exists (anchoring by
            // induction), density tapering downward so boards end in pockets and tips.
            for (int r = 1; r < rows; r++)
            {
                double rowDensity = density * (1.0 - r * 0.055);
                for (int c = 0; c < columns; c++)
                {
                    // Up-neighbors in odd-r: even rows also touch (c-1,r-1); odd rows (c+1,r-1).
                    char up1 = grid[r - 1][c];
                    int c2 = (r % 2 == 0) ? c - 1 : c + 1;
                    char up2 = (c2 >= 0 && c2 < columns) ? grid[r - 1][c2] : '.';
                    if (up1 == '.' && up2 == '.') continue;
                    if (rng.NextDouble() >= rowDensity) continue;

                    // Inherit a NORMALIZED neighbor color (stones/ice must not propagate
                    // through the clumping bias — 'S' inherits as random, 'g' as 'G').
                    char color = '.';
                    if (rng.NextDouble() < 0.55)
                        color = NormalizeColor(up1 != '.' ? up1 : up2);
                    else if (c > 0 && grid[r][c - 1] != '.' && rng.NextDouble() < 0.4)
                        color = NormalizeColor(grid[r][c - 1]);
                    grid[r][c] = color != '.' ? color : palette[rng.Next(colorCount)];

                    // Obstacle overrides (r >= 1 only, so stones never touch the anchor row).
                    if (rng.NextDouble() < stoneChance)
                        grid[r][c] = 'S';
                    else if (rng.NextDouble() < iceChance)
                        grid[r][c] = char.ToLowerInvariant(grid[r][c]);
                }
            }

            var cellRows = new string[rows];
            int bubbles = 0;
            for (int r = 0; r < rows; r++)
            {
                cellRows[r] = new string(grid[r]);
                foreach (char ch in grid[r]) if (ch != '.') bubbles++;
            }

            // Descending pressure enters at Reef 5 and tightens gently. Balance pass
            // 2026-07-14: was every 8→5 shots — with 13 columns that added ~2.2 bubbles
            // per shot, provably unclearable. Now 10→8, paired with thinner ceiling rows
            // (see BoardManager.PressureRowFill) ⇒ ~1.1/shot influx at worst.
            int pressureEvery = t >= 4 ? Math.Max(8, 10 - t / 12) : 0;
            int dangerRow = t >= 4 ? Math.Min(14, rows + Math.Max(3, 6 - t / 10)) : 0;

            // Shot budget accounts for what makes shots miss (balance pass 2026-07-14):
            // base 0.5/bubble, + 12% per color beyond 4, + ~40% of the expected tide
            // influx per shot (fresh anchor rows clear cheaply). Cap raised 30 → 40; the
            // cascade shot-refund (BoardManager) carries the rest on the biggest reefs.
            double budget = bubbles * 0.5;
            budget *= 1.0 + 0.12 * (colorCount - 4);
            if (pressureEvery > 0)
                budget *= 1.0 + 0.4 * (columns * 0.7 / pressureEvery);
            int shots = Math.Max(10, Math.Min(40, (int)Math.Round(budget)));

            return new BoardLayoutData(name, columns, cellRows, shots,
                                       pressureEvery, dangerRow);
        }

        /// <summary>Strips obstacle state for color inheritance: stone → none, frozen → thawed.</summary>
        private static char NormalizeColor(char ch)
        {
            if (ch == 'S' || ch == '.') return '.';
            return char.ToUpperInvariant(ch);
        }
    }
}
