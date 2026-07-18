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

        // ---- Rescued critters (waves feature 2026-07-17) --------------------------------------
        // NOT buyable and never sellable: each one was freed in a level (banked on WIN by
        // GameFlow). They swim in the tank like any fish; no growth clock — a rescue
        // arrives fully grown (GrowthScale returns 1 for ids without timestamps).

        public static readonly ReefItem RescuedCritter =
            new ReefItem("rescued_critter", "Rescued Critter", "fish_pink", 0, ReefItemKind.Fish, 0.9f)
            { Tint = new Color(1f, 0.88f, 0.94f) }; // the pale-bubble pink they were freed from

        private const string RescuedKey = "CoralCascade.Reef.Rescued";

        public static int RescuedCount => PlayerPrefs.GetInt(RescuedKey, 0);

        public static void AddRescued(int n)
        {
            if (n <= 0) return;
            PlayerPrefs.SetInt(RescuedKey, RescuedCount + n);
            PlayerPrefs.Save();
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

        /// <summary>Selling returns half the purchase price (rounded down, min 1).</summary>
        public static int SellValue(ReefItem item) => Mathf.Max(1, item.Price / 2);

        /// <summary>Sells the LAST owned instance of an item back for pearls.</summary>
        public static bool Sell(ReefItem item)
        {
            if (item == null) return false;
            int n = Count(item.Id);
            if (n <= 0) return false;
            PlayerPrefs.SetInt(CountKey(item.Id), n - 1);
            TrimLastCsvEntry(TimesKey(item.Id));
            TrimLastCsvEntry(XKey(item.Id));
            Pearls.Add(SellValue(item));
            PlayerPrefs.Save();
            return true;
        }

        private static void TrimLastCsvEntry(string key)
        {
            string csv = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(csv)) return;
            int i = csv.LastIndexOf(',');
            PlayerPrefs.SetString(key, i < 0 ? "" : csv.Substring(0, i));
        }

        // ---- Decor placement (normalized 0..1 x along the sand, per instance) -----------------

        private static string XKey(string id) => "CoralCascade.Reef.X." + id;

        /// <summary>
        /// Stored placement for the k-th instance, or a deterministic well-spread default
        /// (also covers items bought before placement existed).
        /// </summary>
        public static float GetX(string id, int instance)
        {
            string csv = PlayerPrefs.GetString(XKey(id), "");
            if (!string.IsNullOrEmpty(csv))
            {
                string[] parts = csv.Split(',');
                float v;
                if (instance < parts.Length &&
                    float.TryParse(parts[instance], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out v))
                    return Mathf.Clamp01(v);
            }
            return DefaultX(id, instance);
        }

        public static void SetX(string id, int instance, float x01)
        {
            string csv = PlayerPrefs.GetString(XKey(id), "");
            var parts = new List<string>(string.IsNullOrEmpty(csv) ? new string[0] : csv.Split(','));
            while (parts.Count <= instance)
                parts.Add(DefaultX(id, parts.Count).ToString("0.###",
                          System.Globalization.CultureInfo.InvariantCulture));
            parts[instance] = Mathf.Clamp01(x01).ToString("0.###",
                              System.Globalization.CultureInfo.InvariantCulture);
            PlayerPrefs.SetString(XKey(id), string.Join(",", parts.ToArray()));
            PlayerPrefs.Save();
        }

        private static float DefaultX(string id, int instance)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in id) h = h * 31 + c;
                float v = Mathf.Abs(h % 997) / 997f + instance * 0.37f;
                return v - Mathf.Floor(v);
            }
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
