using System.Collections.Generic;

namespace CoralCascade
{
    /// <summary>
    /// Hand-authored levels, defined in code so the prototype is fully playable without
    /// first creating any .asset files. Doubles as the Phase 1 acceptance-board set — each
    /// level still exercises the specific behavior it was built to prove.
    ///
    /// Difficulty ramp (2026-07-13): the play area WIDENS as levels go up — each layout
    /// declares its own column count (8 → 12) and the world rebuilds to fit. Boards are
    /// built around cascade strategy: drops score 2×, chain knock-offs 4× (see ScoreKeeper),
    /// and the layouts hide the drop/chain setups that exploit that.
    ///
    /// Rows are top-first (row 0 = anchor row). R O Y G B P = colors, '.' = empty.
    /// IMPORTANT: after editing any layout, run the odd-r anchoring check (see
    /// PHASE1_README / CLAUDE.md) — no bubble may float at load.
    /// </summary>
    public static class TestBoards
    {
        public static readonly List<BoardLayoutData> All = new List<BoardLayoutData>
        {
            Simple,
            Overhang,
            FloatingTrap,
            BigCascade,
            ChainReactor,
            Stonefall,
            Icebreaker,
            RisingTide
        };

        /// <summary>
        /// Level 1 — 8 columns. Intro that TEACHES the drop economy: the B and Y columns
        /// each carry a different-colored pair at the tip. Popping the column drops the
        /// pair (2× points) instead of having to match it.
        /// </summary>
        public static BoardLayoutData Simple => new BoardLayoutData("Simple", 8, new[]
        {
            "GGBBYYRR",
            "GGBBYYRR",
            "..B..Y..",
            "..B..Y..",
            "..RR.BB.",
        }, shots: 14);

        /// <summary>
        /// Level 2 — 9 columns (Prompt 5 acceptance): the whole P web and the B mass at its
        /// tip hang from the single P at (4,1). Pop into the P cluster and everything
        /// collapses as one emergent physics mass. The G side columns are separate work.
        /// </summary>
        public static BoardLayoutData Overhang => new BoardLayoutData("Overhang", 9, new[]
        {
            "GGGGGGGGG",
            "G...P...G",
            "G.PPPPP.G",
            "G.P...P.G",
            "..P...P..",
            "..PBBBP..",
            "...BBB...",
        }, shots: 15);

        /// <summary>
        /// Level 3 — 10 columns, the BRIDGE puzzle: a Y thread (left) and a B thread (right)
        /// both hold one linked mass — the R blob bridges to the P blob at (7,6)/(6,5).
        /// Cutting ONE thread drops nothing (the mass re-hangs off the other thread);
        /// cutting the second releases everything at once for a huge drop payout.
        /// </summary>
        public static BoardLayoutData FloatingTrap => new BoardLayoutData("FloatingTrap", 10, new[]
        {
            "GGGGYGGGBG",
            "....Y...B.",
            "....Y...B.",
            "..RRRRR.B.",
            "..R.R.R.P.",
            "..RRRRR.P.",
            ".......PPP",
        }, shots: 16);

        /// <summary>
        /// Level 4 — 11 columns (Prompt 6 slow-mo acceptance): a dense fortress whose Green
        /// ring (~20 connected G) pops in one hit from below, dropping the sealed Red core
        /// and everything under the row-2 gap. The roof is segmented 3-color runs, NOT one
        /// color — no single lucky shot clears it.
        /// </summary>
        public static BoardLayoutData BigCascade => new BoardLayoutData("BigCascade", 11, new[]
        {
            "BBBRRRGGGBB",
            "BBRRRGGGBBB",
            "BB.......BB",
            "BB.GGGGG.BB",
            "GG.G...G.GG",
            "GG.G.R.G.BB",
            "BBBG.R.GBBB",
            "...GGRGG...",
            "....GGG....",
        }, shots: 18);

        /// <summary>
        /// Level 5 — 12 columns, DOUBLE chain reactor: a 12-bubble B block at the ceiling
        /// free-falls ~7 rows between TWO shelves (O left, Y right). The detach kick spreads
        /// the mass outward so it slams both shelves — impacts far above the 6.0 dislodge
        /// threshold → secondary chains on both sides (4× points each).
        /// </summary>
        public static BoardLayoutData ChainReactor => new BoardLayoutData("ChainReactor", 12, new[]
        {
            "GGG.BBBB.GGG",
            "GG..BBBB..GG",
            "GG..BBBB..GG",
            "GG........GG",
            "GG........GG",
            "G..........G",
            "G..........G",
            "GOOOO..YYYYG",
        }, shots: 16);

        /// <summary>
        /// Intro 6 — teaches STONE ('S'): stones can never be matched, only DROPPED. The
        /// stone wedge hangs entirely off the Blue band — pop the Blues and the stones fall
        /// (at drop points). Stones are never authored on row 0 (they'd be unremovable).
        /// </summary>
        public static BoardLayoutData Stonefall => new BoardLayoutData("Stonefall", 9, new[]
        {
            "GGGGGGGGG",
            "..BBBBB..",
            "..SSSSS..",
            "...SSS...",
        }, shots: 10);

        /// <summary>
        /// Intro 7 — teaches ICE (lowercase = frozen): frozen bubbles can't be matched
        /// until an ADJACENT cluster pops. Pop the Greens to thaw the nearest frozen Blue,
        /// then build Blue matches to thaw-and-clear your way across the ice shelf.
        /// </summary>
        public static BoardLayoutData Icebreaker => new BoardLayoutData("Icebreaker", 9, new[]
        {
            "RRRGGGbbb",
            "R...GG.bb",
        }, shots: 12);

        /// <summary>
        /// Intro 8 — teaches DESCENDING PRESSURE: every 3 shots the board pushes down one
        /// row and a fresh row grows at the ceiling; bubbles crossing the danger line lose
        /// the level. Shot budget (18) deliberately exceeds what the tide allows (~12
        /// before flooding) so the TIDE, not ammo, is the thing to beat — clear fast.
        /// </summary>
        public static BoardLayoutData RisingTide => new BoardLayoutData("RisingTide", 9, new[]
        {
            "RRRGGGBBB",
            ".RR.GG.BB",
        }, shots: 18, pressureEveryShots: 3, dangerRow: 5);
    }
}
