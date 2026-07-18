using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// The six playable bubble colors plus an explicit "None" for empty cells.
    /// Kept deliberately theme-agnostic (see game plan: "theme is data, not logic").
    /// The reef/coral skin is applied on top of these later; the board never needs
    /// to know what a color "means" visually.
    /// </summary>
    public enum BubbleColor
    {
        None = 0,
        Red,
        Orange,
        Yellow,
        Green,
        Blue,
        Purple,

        /// <summary>
        /// Obstacle, not a color: STONE can never be matched (it's excluded from Playable
        /// and never equals a fired color) — it can only be removed by DROPPING it (cut its
        /// support or knock it loose). Authoring rule: never place stone on row 0, or it
        /// becomes permanently unremovable and the level unwinnable.
        /// </summary>
        Stone,

        /// <summary>
        /// A trapped sea creature (waves feature, 2026-07-17): unmatchable and unfireable
        /// like Stone — a pure in-level obstacle you clear by DETACHING it (drop or chain
        /// knock), since it can never be popped by a match. No meta reward (the rescue-to-
        /// aquarium collection was removed 2026-07-18). Same authoring rule as Stone:
        /// never on row 0. Critters can't be frozen ('C' is always uppercase).
        /// </summary>
        Critter
    }

    public static class BubbleColorExtensions
    {
        /// <summary>Colors that can actually appear on the board / be fired.</summary>
        public static readonly BubbleColor[] Playable =
        {
            BubbleColor.Red,
            BubbleColor.Orange,
            BubbleColor.Yellow,
            BubbleColor.Green,
            BubbleColor.Blue,
            BubbleColor.Purple
        };

        /// <summary>Placeholder primitive colors (Phase 1 has no real art).</summary>
        public static Color ToRGBA(this BubbleColor c)
        {
            switch (c)
            {
                case BubbleColor.Red:    return new Color(0.90f, 0.25f, 0.25f);
                case BubbleColor.Orange: return new Color(0.95f, 0.55f, 0.20f);
                case BubbleColor.Yellow: return new Color(0.95f, 0.85f, 0.25f);
                case BubbleColor.Green:  return new Color(0.30f, 0.75f, 0.35f);
                case BubbleColor.Blue:   return new Color(0.25f, 0.55f, 0.90f);
                case BubbleColor.Purple: return new Color(0.65f, 0.35f, 0.85f);
                case BubbleColor.Stone:  return new Color(0.42f, 0.42f, 0.46f);
                case BubbleColor.Critter: return new Color(1.00f, 0.72f, 0.82f);
                default:                 return new Color(0f, 0f, 0f, 0f);
            }
        }

        /// <summary>Single-char code used by the text-based BoardLayout authoring format.</summary>
        public static char ToChar(this BubbleColor c)
        {
            switch (c)
            {
                case BubbleColor.Red:    return 'R';
                case BubbleColor.Orange: return 'O';
                case BubbleColor.Yellow: return 'Y';
                case BubbleColor.Green:  return 'G';
                case BubbleColor.Blue:   return 'B';
                case BubbleColor.Purple: return 'P';
                case BubbleColor.Stone:  return 'S';
                case BubbleColor.Critter: return 'C';
                default:                 return '.';
            }
        }

        /// <summary>True for the six fireable/matchable colors (excludes None, Stone, Critter).</summary>
        public static bool IsPlayable(this BubbleColor c) =>
            c != BubbleColor.None && c != BubbleColor.Stone && c != BubbleColor.Critter;

        /// <summary>
        /// Parse a layout char into a color. '.', ' ', '_' and '0' all mean empty.
        /// Case-insensitive for the COLOR — a lowercase color char additionally means the
        /// bubble starts FROZEN (see BoardLayoutData.FrozenAt). 'S' = stone (obstacle).
        /// Unknown chars are treated as empty (with a warning).
        /// </summary>
        public static BubbleColor FromChar(char ch)
        {
            switch (char.ToUpperInvariant(ch))
            {
                case 'R': return BubbleColor.Red;
                case 'O': return BubbleColor.Orange;
                case 'Y': return BubbleColor.Yellow;
                case 'G': return BubbleColor.Green;
                case 'B': return BubbleColor.Blue;
                case 'P': return BubbleColor.Purple;
                case 'S': return BubbleColor.Stone;
                case 'C': return BubbleColor.Critter;
                case '.':
                case ' ':
                case '_':
                case '0':
                    return BubbleColor.None;
                default:
                    Debug.LogWarning($"[BoardLayout] Unknown color char '{ch}', treating as empty.");
                    return BubbleColor.None;
            }
        }
    }
}
