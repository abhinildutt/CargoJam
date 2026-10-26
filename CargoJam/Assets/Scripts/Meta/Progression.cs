using System;

namespace CargoJam.Meta
{
    /// <summary>What a win paid out, for the win panel to animate.</summary>
    public sealed class WinResult
    {
        public int BaseCoins;
        public float Multiplier;
        public int CoinsEarned;
        public int StreakBefore;
        public int StreakAfter;
        /// <summary>Chest bar before and after, in wins (0..ChestEvery).</summary>
        public int ChestBefore;
        public int ChestAfter;
        public bool ChestReady;
        /// <summary>A booster unlocked by reaching the next level (its gift is already granted).</summary>
        public BoosterType? Unlocked;
        public int NewLevelIndex;
    }

    public sealed class ChestReward
    {
        public bool Big;
        public int Coins;
        /// <summary>Boosters granted, indexed by <see cref="BoosterType"/>.</summary>
        public int[] Boosters = new int[EconomyConfig.BoosterKinds];
    }

    public sealed class DailyRewardInfo
    {
        /// <summary>True when today's reward hasn't been claimed yet.</summary>
        public bool Available;
        /// <summary>Calendar day 0..6 this reward belongs to.</summary>
        public int Day;
        public DailyReward Reward;
        public int LoginStreak;
    }

    /// <summary>
    /// The rules of the meta game: coins, streaks, chests, boosters, unlocks and the daily calendar.
    /// Engine-free and deterministic given the clock and random seed. Raises <see cref="Changed"/>
    /// after every mutation so the UI and the save layer can react.
    ///
    /// CONTRACT for stream B (docs/design-v2-polish-and-retention.md). Placeholders throw until implemented.
    /// </summary>
    public sealed class Progression
    {
        public ProfileData Data { get; }
        public event Action Changed;

        public Progression(ProfileData data, IClock clock, int seed = 0)
        {
            Data = data ?? new ProfileData();
            throw new NotImplementedException("stream B");
        }

        public int Coins => Data.coins;
        public int LevelIndex => Data.levelIndex;
        public int WinStreak => Data.winStreak;
        public float StreakMultiplier => EconomyConfig.StreakMultiplier(Data.winStreak);
        public int ChestProgress => Data.chestProgress;
        public bool ChestReady => Data.chestProgress >= EconomyConfig.ChestEvery;
        public bool NextChestIsBig => (Data.chestsOpened + 1) % EconomyConfig.BigChestEvery == 0;

        public int BoosterCount(BoosterType booster) => Data.boosters[(int)booster];
        public bool IsUnlocked(BoosterType booster) => Data.unlocked[(int)booster];

        /// <summary>Call once per app launch: updates the login streak for today.</summary>
        public void RegisterOpen() => throw new NotImplementedException("stream B");

        /// <summary>Pays out a win, advances the level, streak and chest bar, and applies unlocks.</summary>
        public WinResult RecordWin(bool hard) => throw new NotImplementedException("stream B");

        /// <summary>The player abandoned a failed or started level: the streak resets.</summary>
        public void RecordGiveUp() => throw new NotImplementedException("stream B");

        public bool CanAfford(int coins) => Data.coins >= coins;
        public bool TrySpend(int coins) => throw new NotImplementedException("stream B");
        public void AddCoins(int coins) => throw new NotImplementedException("stream B");

        /// <summary>Uses one owned booster. False when none are owned.</summary>
        public bool TryConsumeBooster(BoosterType booster) => throw new NotImplementedException("stream B");

        /// <summary>Buys one booster for coins (it goes to the inventory). False when unaffordable.</summary>
        public bool TryBuyBooster(BoosterType booster) => throw new NotImplementedException("stream B");

        /// <summary>Opens the ready chest and grants its contents. Null when no chest is ready.</summary>
        public ChestReward OpenChest() => throw new NotImplementedException("stream B");

        public DailyRewardInfo DailyStatus() => throw new NotImplementedException("stream B");

        /// <summary>Grants today's reward and advances the calendar. Null when already claimed today.</summary>
        public DailyRewardInfo ClaimDaily() => throw new NotImplementedException("stream B");

        void RaiseChanged() => Changed?.Invoke();
    }
}
