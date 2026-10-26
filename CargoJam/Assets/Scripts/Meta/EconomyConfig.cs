namespace CargoJam.Meta
{
    public enum BoosterType
    {
        Undo = 0,
        Crane = 1,
        ExtraSlot = 2,
    }

    /// <summary>
    /// Every economy number in one place (docs/design-v2-polish-and-retention.md, section 3).
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

        public static float StreakMultiplier(int streak) => streak >= 6 ? 2f : streak >= 3 ? 1.5f : 1f;

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
