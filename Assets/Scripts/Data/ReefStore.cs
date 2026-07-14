using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>What a reef item is, visually — decides how the tank renders/animates it.</summary>
    public enum ReefItemKind { Fish, Plant, Vent }

    /// <summary>One purchasable aquarium item. Content is data, like the boards.</summary>
    public class ReefItem
    {
        public string Id;
        public string DisplayName;
        public string SpriteName;   // BubbleArt.Get name (Kenney pack)
        /// <summary>
        /// Second half for two-tile sprites. The pack's long fish is SPLIT across two
        /// textures (long_a = left half hugging its right edge, long_b = right half
        /// hugging its left edge) — drawn alone it looks cut off; renderers must place
        /// the halves side by side.
        /// </summary>
        public string SpriteName2;
        public int Price;           // pearls
        public ReefItemKind Kind;
        public Color Tint = Color.white;
        public float SizeMul = 1f;

        public ReefItem(string id, string name, string sprite, int price,
                        ReefItemKind kind, float sizeMul = 1f)
        {
            Id = id;
            DisplayName = name;
            SpriteName = sprite;
            Price = price;
            Kind = kind;
            SizeMul = sizeMul;
        }
    }

    /// <summary>
    /// The My Reef aquarium: catalog, pearl purchases, ownership persistence, fish growth,
    /// and achievement rares. PURELY COSMETIC — nothing here may ever affect gameplay
    /// (user decision 2026-07-14; the fairness boundary extends to the economy).
    ///
    /// Persistence (PlayerPrefs): "CoralCascade.Reef.&lt;Id&gt;" = owned count;
    /// "CoralCascade.Reef.T.&lt;Id&gt;" = CSV of purchase timestamps (UTC ticks) driving
    /// growth — a fish is bought as a baby (0.55×) and reaches full size in 3 real days.
    /// </summary>
    public static class ReefStore
    {
        public const float GrowthDays = 3f;
        public const float BabyScale = 0.55f;

        public static readonly ReefItem[] Catalog =
        {
            new ReefItem("fish_blue",   "Blue Tang",     "fish_blue",        30, ReefItemKind.Fish),
            new ReefItem("fish_orange", "Clownfish",     "fish_orange",      30, ReefItemKind.Fish),
            new ReefItem("fish_green",  "Green Chromis", "fish_green",       30, ReefItemKind.Fish),
            new ReefItem("fish_pink",   "Pink Damsel",   "fish_pink",        40, ReefItemKind.Fish, 0.85f),
            new ReefItem("fish_red",    "Red Snapper",   "fish_red",         40, ReefItemKind.Fish, 1.1f),
            new ReefItem("eel",         "Moray Eel",     "fish_grey_long_a", 60, ReefItemKind.Fish, 1.2f)
                { SpriteName2 = "fish_grey_long_b" },
            new ReefItem("puffer",      "Pufferfish",    "fish_brown",       80, ReefItemKind.Fish, 1.15f),
            new ReefItem("grass",       "Sea Grass",     "seaweed_grass_a",  10, ReefItemKind.Plant),
            new ReefItem("rock",        "Reef Rock",     "rock_a",           10, ReefItemKind.Plant, 0.8f),
            new ReefItem("kelp",        "Green Kelp",    "seaweed_green_a",  15, ReefItemKind.Plant, 1.2f),
            new ReefItem("coral_pink",  "Pink Coral",    "seaweed_pink_a",   15, ReefItemKind.Plant, 1.1f),
            new ReefItem("coral_fire",  "Fire Coral",    "seaweed_orange_a", 20, ReefItemKind.Plant),
            new ReefItem("vent",        "Bubble Vent",   "bubble_c",         25, ReefItemKind.Vent),
        };

        // Achievement rares — NOT buyable; unlocked state derived from star records at
        // ask-time (no extra saved state, so they even award retroactively).
        public static readonly ReefItem GoldenPuffer =
            new ReefItem("rare_gold_puffer", "Golden Puffer", "fish_brown", 0, ReefItemKind.Fish, 1.25f)
            { Tint = new Color(1f, 0.84f, 0.35f) };
        public static readonly ReefItem PearlEel =
            new ReefItem("rare_pearl_eel", "Pearl Eel", "fish_grey_long_a", 0, ReefItemKind.Fish, 1.3f)
            { SpriteName2 = "fish_grey_long_b", Tint = new Color(0.95f, 0.97f, 1f) };

        /// <summary>Golden Puffer: 3★ on EVERY Tutorial Reef board.</summary>
        public static bool GoldenPufferUnlocked()
        {
            foreach (var level in LevelCatalog.Intro)
                if (Stars.Get(level.Name) < 3) return false;
            return true;
        }

        /// <summary>Pearl Eel: 3★ on any 10 Adventure levels.</summary>
        public static bool PearlEelUnlocked()
        {
            int threeStarred = 0;
            foreach (var level in LevelCatalog.Main)
                if (Stars.Get(level.Name) >= 3 && ++threeStarred >= 10) return true;
            return false;
        }

        // ---- Ownership -----------------------------------------------------------------------

        private static string CountKey(string id) => "CoralCascade.Reef." + id;
        private static string TimesKey(string id) => "CoralCascade.Reef.T." + id;

        public static int Count(string id) => PlayerPrefs.GetInt(CountKey(id), 0);

        /// <summary>Buys one of an item (pearls permitting). Stamps the growth clock.</summary>
        public static bool Buy(ReefItem item)
        {
            if (item == null || !Pearls.Spend(item.Price)) return false;
            PlayerPrefs.SetInt(CountKey(item.Id), Count(item.Id) + 1);
            string csv = PlayerPrefs.GetString(TimesKey(item.Id), "");
            csv = string.IsNullOrEmpty(csv)
                ? DateTime.UtcNow.Ticks.ToString()
                : csv + "," + DateTime.UtcNow.Ticks;
            PlayerPrefs.SetString(TimesKey(item.Id), csv);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// Growth scale (BabyScale → 1) for the k-th owned instance of an item. Instances
        /// without a recorded timestamp (defensive) count as fully grown.
        /// </summary>
        public static float GrowthScale(string id, int instance)
        {
            string csv = PlayerPrefs.GetString(TimesKey(id), "");
            if (string.IsNullOrEmpty(csv)) return 1f;
            string[] parts = csv.Split(',');
            if (instance < 0 || instance >= parts.Length) return 1f;
            long ticks;
            if (!long.TryParse(parts[instance], out ticks)) return 1f;
            double days = (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalDays;
            float t = Mathf.Clamp01((float)(days / GrowthDays));
            return Mathf.Lerp(BabyScale, 1f, t);
        }
    }
}
