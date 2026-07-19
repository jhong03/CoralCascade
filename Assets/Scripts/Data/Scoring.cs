using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Per-level score. Cascade play is worth far more than plain matching — that's the
    /// strategic incentive: cut supports and set up chain impacts instead of spraying pops.
    ///   matched pop                          = 10 / bubble
    ///   dropped (support cut, never matched) = 20 / bubble
    ///   knocked loose by a falling impact
    ///   (secondary chain)                    = 40 / bubble
    ///   level clear                          = +50 per unused shot
    /// </summary>
    public class ScoreKeeper
    {
        public const int MatchedPoints = 10;
        public const int DroppedPoints = 20;
        public const int ChainDroppedPoints = 40;
        public const int ClearBonusPerShot = 50;

        public int Total { get; private set; }

        public void Reset() => Total = 0;
        public void AddMatched(int bubbles)      { if (bubbles > 0) Total += bubbles * MatchedPoints; }
        public void AddDropped(int bubbles)      { if (bubbles > 0) Total += bubbles * DroppedPoints; }
        public void AddChainDropped(int bubbles) { if (bubbles > 0) Total += bubbles * ChainDroppedPoints; }
        public void AddClearBonus(int shotsLeft) { if (shotsLeft > 0) Total += shotsLeft * ClearBonusPerShot; }
    }

    /// <summary>
    /// 1–3 stars per CLEARED level. Thresholds scale with the layout's starting bubble
    /// count, so they stay meaningful across tiny intros and 80-bubble reefs:
    ///   1★ = cleared at all
    ///   2★ = score ≥ 16 × bubbles  (real drop play — pure matching averages ~10–12/bubble)
    ///   3★ = score ≥ 24 × bubbles  (heavy cascade play + banked shots)
    /// Best stars persist in PlayerPrefs. Constants are the balance dials.
    /// Lowered 18/28 → 16/24 (2026-07-18): on obstacle-heavy boards 28× sat at/beyond the
    /// practical score ceiling — stones inflate BubbleCount yet can't be matched/chained and
    /// fragment cascades, so 3★ was effectively unreachable (user-reported on Reefs 8–10).
    /// </summary>
    public static class Stars
    {
        public const int Star2PerBubble = 16;
        public const int Star3PerBubble = 24;

        /// <summary>
        /// The score needed for the 2★ or 3★ rating on this layout (0 for unknown layouts —
        /// callers should hide target UI then). 1★ has no score target: it means CLEARED.
        /// </summary>
        public static int Target(BoardLayoutData layout, int stars)
        {
            if (layout == null || layout.BubbleCount <= 0) return 0;
            return layout.BubbleCount * (stars >= 3 ? Star3PerBubble : Star2PerBubble);
        }

        public static int Compute(BoardLayoutData layout, int score)
        {
            if (layout == null) return 1;
            int bubbles = layout.BubbleCount;
            if (bubbles <= 0) return 3;
            if (score >= bubbles * Star3PerBubble) return 3;
            if (score >= bubbles * Star2PerBubble) return 2;
            return 1;
        }

        private static string Key(string levelName) => "CoralCascade.Stars." + levelName;

        public static int Get(string levelName) => PlayerPrefs.GetInt(Key(levelName), 0);

        /// <summary>Records the stars if they beat the stored best; true if they did.</summary>
        public static bool Submit(string levelName, int stars)
        {
            if (stars <= Get(levelName)) return false;
            PlayerPrefs.SetInt(Key(levelName), stars);
            PlayerPrefs.Save();
            return true;
        }
    }

    /// <summary>
    /// META CURRENCY for the My Reef aquarium (roadmap step 5). Purely cosmetic economy,
    /// never affects gameplay (user decision 2026-07-14). Pearls are a PROGRESS reward, not
    /// a replay faucet (user-reported 2026-07-18: repeating a cleared level farmed pearls
    /// infinitely). The base + per-star payout lands ONCE on first clear; re-clearing pays
    /// only for genuine improvement (new stars beyond the old best, a new best score). The
    /// award policy lives in GameFlow's win block — these are just the dials.
    /// </summary>
    public static class Pearls
    {
        public const int WinBase = 10;         // first clear only
        public const int PerStar = 5;          // first clear: all stars; replay: newly-earned stars only
        public const int FirstClearBonus = 15; // once, on first clear
        public const int NewBestBonus = 5;     // each time a replay beats the stored best score

        private const string Key = "CoralCascade.Pearls";

        public static int Balance => PlayerPrefs.GetInt(Key, 0);

        public static void Add(int amount)
        {
            if (amount <= 0) return;
            PlayerPrefs.SetInt(Key, Balance + amount);
            PlayerPrefs.Save();
        }

        /// <summary>Deducts if affordable; false (and no change) otherwise.</summary>
        public static bool Spend(int amount)
        {
            if (amount <= 0 || Balance < amount) return false;
            PlayerPrefs.SetInt(Key, Balance - amount);
            PlayerPrefs.Save();
            return true;
        }
    }

    /// <summary>
    /// Candy-crush-style sequential unlocking, persisted via PlayerPrefs — PER SECTION:
    /// the Tutorial Reef and the Adventure each carry their own independent chain.
    /// Level numbers are 1-based within a section; clearing level N unlocks N+1.
    /// "Cleared" itself isn't stored separately — a level counts as cleared when it has a
    /// best score (only wins submit one).
    /// </summary>
    public static class Progress
    {
        private static string Key(string section) => "CoralCascade.Unlocked." + section;

        /// <summary>Highest level number the player may enter in this section (>= 1).</summary>
        public static int HighestUnlocked(string section) =>
            Mathf.Max(1, PlayerPrefs.GetInt(Key(section), 1));

        public static bool IsUnlocked(string section, int levelNumber) =>
            levelNumber <= HighestUnlocked(section);

        public static void MarkCleared(string section, int levelNumber)
        {
            if (levelNumber + 1 <= HighestUnlocked(section)) return;
            PlayerPrefs.SetInt(Key(section), levelNumber + 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Per-level best scores, persisted locally via PlayerPrefs. Only CLEARED levels are
    /// submitted (see GameFlow) — a lost attempt can't set a best, so farming cascades
    /// without finishing earns nothing permanent.
    /// </summary>
    public static class HighScores
    {
        private static string Key(string levelName) => "CoralCascade.Best." + levelName;

        public static int Get(string levelName) => PlayerPrefs.GetInt(Key(levelName), 0);

        /// <summary>Records the score if it beats the stored best; true if it did.</summary>
        public static bool Submit(string levelName, int score)
        {
            if (score <= Get(levelName)) return false;
            PlayerPrefs.SetInt(Key(levelName), score);
            PlayerPrefs.Save();
            return true;
        }
    }

    /// <summary>
    /// One-time "new mechanic" tutorial cards (stone / ice / tide): one flag per mechanic
    /// so the card shows only on the player's FIRST encounter, in whatever level that
    /// happens (tutorial board, Adventure reef, or a Daily). Marked seen when the player
    /// dismisses the card — quitting with it open shows it again next time, on purpose.
    /// </summary>
    public static class TutorialFlags
    {
        /// <summary>Every mechanic id that has a tutorial card.</summary>
        public static readonly string[] AllIds = { "Stone", "Ice", "Tide", "Critter" };

        private static string Key(string id) => "CoralCascade.Tutorial." + id;

        public static bool Seen(string id) => PlayerPrefs.GetInt(Key(id), 0) == 1;

        public static void MarkSeen(string id)
        {
            PlayerPrefs.SetInt(Key(id), 1);
            PlayerPrefs.Save();
        }

        /// <summary>Forgets every seen-flag so the guides show again (Settings page).</summary>
        public static void ResetAll()
        {
            foreach (var id in AllIds)
                PlayerPrefs.DeleteKey(Key(id));
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Player-facing options (the Settings page). PlayerPrefs like everything else.
    /// Only REAL, wired options live here — no placeholder toggles for systems that
    /// don't exist yet (audio adds its own entries when it lands).
    /// </summary>
    public static class GameSettings
    {
        private const string ShakeKey = "CoralCascade.Set.Shake";
        private const string SfxKey = "CoralCascade.Set.Sfx";
        private const string MusicKey = "CoralCascade.Set.Music";
        private const string HapticsKey = "CoralCascade.Set.Haptics";

        /// <summary>Camera shake on big pops/cascades. Read by PopEffects.Shake.</summary>
        public static bool ShakeEnabled
        {
            get => PlayerPrefs.GetInt(ShakeKey, 1) == 1;
            set { PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Sound effects. Gated at the single Sfx.Play entry point.</summary>
        public static bool SfxEnabled
        {
            get => PlayerPrefs.GetInt(SfxKey, 1) == 1;
            set { PlayerPrefs.SetInt(SfxKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Ambient music bed. Gated at Sfx.SetMusic.</summary>
        public static bool MusicEnabled
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Vibration on impactful moments. Gated at the single Haptics.Bump entry.</summary>
        public static bool HapticsEnabled
        {
            get => PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            set { PlayerPrefs.SetInt(HapticsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }

    /// <summary>
    /// Device vibration, behind the player's setting — one entry point, like PopEffects.Shake.
    /// Handheld.Vibrate is a blunt ~500ms buzz on Android, so it is reserved for genuinely
    /// big moments (level win/loss, the tide dropping), never per-pop: a bubble shooter that
    /// buzzes on every shot is a bubble shooter people turn the haptics off in.
    /// </summary>
    public static class Haptics
    {
        public static void Bump()
        {
            if (!GameSettings.HapticsEnabled) return;
#if UNITY_ANDROID || UNITY_IOS
            if (Application.isMobilePlatform) Handheld.Vibrate();
#endif
        }
    }
}
