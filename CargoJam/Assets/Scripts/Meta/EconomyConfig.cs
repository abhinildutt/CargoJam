namespace CargoJam.Meta
{
    public enum BoosterType
    {
        Undo = 0,
        Crane = 1,
        ExtraSlot = 2,
    }

    /// <summary>
    /// Every economy number in one place (docs/design-v2-polish-and-retention.md, section 3; the
    /// streak tiers follow docs/design-v3-premium-journey.md, section 6).
    /// Level numbers here are 1-based, as players see them; level indexes elsewhere are 0-based.
    /// </summary>
    public static class EconomyConfig
    {
        public const int BoosterKinds = 3;

        public const int StartingCoins = 300;
        public const int WinCoins = 40;
        public const int HardWinCoins = 80;

        public const int ChestEvery = 5;
        /// <summary>Every Nth chest is a big one.</summary>
        public const int BigChestEvery = 4;
        public const int ChestCoins = 100;
        public const int BigChestCoins = 300;

        public const int UnlockGift = 2;

        // ---- Streak tiers (docs/design-v3-premium-journey.md, section 6) ----

        /// <summary>The highest streak tier; it carries the free-Undo perk.</summary>
        public const int MaxStreakTier = 3;
        /// <summary>Win streak at which tier 1 starts (×1.5).</summary>
        public const int StreakTier1At = 3;
        /// <summary>Win streak at which tier 2 starts (×2).</summary>
        public const int StreakTier2At = 6;
        /// <summary>Win streak at which tier 3 starts (×2.5 and one free Undo per level).</summary>
        public const int StreakTier3At = 10;

        public const float StreakTier0Multiplier = 1f;
        public const float StreakTier1Multiplier = 1.5f;
        public const float StreakTier2Multiplier = 2f;
        public const float StreakTier3Multiplier = 2.5f;

        /// <summary>The tier whose perk is one free Undo charge per level (not banked).</summary>
        public const int FreeUndoTier = MaxStreakTier;
        /// <summary>Free Undo charges a level gets at <see cref="FreeUndoTier"/>.</summary>
        public const int FreeUndoPerLevel = 1;

        /// <summary>Streak tier 0..<see cref="MaxStreakTier"/> for a win streak (0-2, 3-5, 6-9, 10+).</summary>
        public static int StreakTierFor(int streak) =>
            streak >= StreakTier3At ? 3 : streak >= StreakTier2At ? 2 : streak >= StreakTier1At ? 1 : 0;

        /// <summary>The lowest streak in a tier (tier 0 starts at 0). Tiers are clamped to 0..<see cref="MaxStreakTier"/>.</summary>
        public static int StreakTierStart(int tier)
        {
            switch (tier)
            {
                case 1: return StreakTier1At;
                case 2: return StreakTier2At;
                case 3: return StreakTier3At;
                default: return tier > MaxStreakTier ? StreakTier3At : 0;
            }
        }

        /// <summary>The coin multiplier of a tier (×1, ×1.5, ×2, ×2.5). Tiers are clamped to 0..<see cref="MaxStreakTier"/>.</summary>
        public static float StreakTierMultiplier(int tier)
        {
            switch (tier)
            {
                case 1: return StreakTier1Multiplier;
                case 2: return StreakTier2Multiplier;
                case 3: return StreakTier3Multiplier;
                default: return tier > MaxStreakTier ? StreakTier3Multiplier : StreakTier0Multiplier;
            }
        }

        /// <summary>The coin multiplier for a win streak: the multiplier of its tier.</summary>
        public static float StreakMultiplier(int streak) => StreakTierMultiplier(StreakTierFor(streak));

        public static int BoosterPrice(BoosterType booster)
        {
            switch (booster)
            {
                case BoosterType.Undo: return 120;
                case BoosterType.Crane: return 200;
                default: return 240;
            }
        }

        /// <summary>Cost of the fail "Continue (+1 slot)", given how many continues this level already used.</summary>
        public static int ContinueCost(int continuesUsed) => continuesUsed <= 0 ? 300 : 600;

        /// <summary>The 1-based level number at which a booster unlocks (with <see cref="UnlockGift"/> free).</summary>
        public static int UnlockLevel(BoosterType booster)
        {
            switch (booster)
            {
                case BoosterType.Undo: return 6;
                case BoosterType.Crane: return 11;
                default: return 16;
            }
        }

        /// <summary>The 7-day reward calendar; <paramref name="day"/> is 0..6.</summary>
        public static DailyReward Daily(int day)
        {
            switch (day)
            {
                case 0: return DailyReward.CoinsOnly(50);
                case 1: return DailyReward.OneBooster(BoosterType.Undo);
                case 2: return DailyReward.CoinsOnly(100);
                case 3: return DailyReward.OneBooster(BoosterType.Crane);
                case 4: return DailyReward.CoinsOnly(150);
                case 5: return DailyReward.OneBooster(BoosterType.ExtraSlot);
                default: return DailyReward.Chest(200);
            }
        }
    }

    public readonly struct DailyReward
    {
        public readonly int Coins;
        /// <summary>The single booster granted, or null.</summary>
        public readonly BoosterType? Booster;
        /// <summary>Day-7 chest: coins plus one of each booster.</summary>
        public readonly bool IsChest;

        DailyReward(int coins, BoosterType? booster, bool chest)
        {
            Coins = coins;
            Booster = booster;
            IsChest = chest;
        }

        public static DailyReward CoinsOnly(int coins) => new DailyReward(coins, null, false);
        public static DailyReward OneBooster(BoosterType booster) => new DailyReward(0, booster, false);
        public static DailyReward Chest(int coins) => new DailyReward(coins, null, true);
    }
}
