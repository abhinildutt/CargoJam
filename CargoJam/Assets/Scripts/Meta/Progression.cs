using System;
using System.Collections.Generic;
using System.Globalization;

namespace CargoJam.Meta
{
    /// <summary>What a win paid out, for the win panel to animate.</summary>
    public sealed class WinResult
    {
        /// <summary>Coins before the streak multiplier: <see cref="EconomyConfig.WinCoins"/> or <see cref="EconomyConfig.HardWinCoins"/>.</summary>
        public int BaseCoins;
        /// <summary>The streak multiplier applied, from <see cref="StreakAfter"/>.</summary>
        public float Multiplier;
        /// <summary>Coins actually granted (BaseCoins × Multiplier, rounded).</summary>
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

    /// <summary>The contents of an opened chest (already granted).</summary>
    public sealed class ChestReward
    {
        public bool Big;
        public int Coins;
        /// <summary>Boosters granted, indexed by <see cref="BoosterType"/>.</summary>
        public int[] Boosters = new int[EconomyConfig.BoosterKinds];
    }

    /// <summary>The daily calendar's state for today, or what was just claimed.</summary>
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
    /// See docs/design-v2-polish-and-retention.md, section 3.
    /// </summary>
    public sealed class Progression
    {
        /// <summary>Coins a normal chest gives instead of a booster when no booster is unlocked yet.</summary>
        public const int NoBoosterChestCoins = 50;

        const string DateFormat = "yyyy-MM-dd";
        const int DailyDays = 7;

        readonly IClock clock;
        readonly Random random;

        /// <summary>The persisted state this instance mutates (normalized on construction).</summary>
        public ProfileData Data { get; }

        /// <summary>Raised after every mutation, once per call.</summary>
        public event Action Changed;

        /// <summary>
        /// Wraps (and repairs) a profile. Null or short arrays are resized, negative or out-of-range
        /// numbers are clamped, and any booster whose unlock level the player has already reached is
        /// unlocked with its gift, so saves from before the booster system get what they earned.
        /// </summary>
        /// <param name="data">The loaded profile, or null for a fresh one.</param>
        /// <param name="clock">The local calendar; null uses <see cref="SystemClock"/>.</param>
        /// <param name="seed">Seed for chest booster picks.</param>
        public Progression(ProfileData data, IClock clock, int seed = 0)
        {
            Data = data ?? new ProfileData();
            this.clock = clock ?? new SystemClock();
            random = new Random(seed);
            Normalize(Data);
            ApplyUnlocks(Data.levelIndex);
        }

        public int Coins => Data.coins;
        public int LevelIndex => Data.levelIndex;
        public int WinStreak => Data.winStreak;
        public int BestStreak => Data.bestStreak;
        public int LoginStreak => Data.loginStreak;

        /// <summary>The multiplier for the current streak, i.e. what the most recent win paid.</summary>
        public float StreakMultiplier => EconomyConfig.StreakMultiplier(Data.winStreak);

        /// <summary>The multiplier the next win will pay (the streak counts that win, see <see cref="RecordWin"/>).</summary>
        public float NextWinMultiplier => EconomyConfig.StreakMultiplier(Data.winStreak + 1);

        public int ChestProgress => Data.chestProgress;
        public bool ChestReady => Data.chestProgress >= EconomyConfig.ChestEvery;
        public bool NextChestIsBig => (Data.chestsOpened + 1) % EconomyConfig.BigChestEvery == 0;

        public int BoosterCount(BoosterType booster) => Data.boosters[(int)booster];
        public bool IsUnlocked(BoosterType booster) => Data.unlocked[(int)booster];

        /// <summary>Cost of the fail "Continue (+1 slot)", given how many continues this level already used.</summary>
        public int ContinueCost(int continuesUsed) => EconomyConfig.ContinueCost(continuesUsed);

        /// <summary>The booster that unlocks on reaching the level at <paramref name="levelIndex"/> (0-based), or null.</summary>
        public static BoosterType? UnlockFor(int levelIndex)
        {
            for (int i = 0; i < EconomyConfig.BoosterKinds; i++)
            {
                var booster = (BoosterType)i;
                if (EconomyConfig.UnlockLevel(booster) == levelIndex + 1) return booster;
            }
            return null;
        }

        /// <summary>The next booster still locked (in unlock order), or null when all are unlocked.</summary>
        public BoosterType? NextLocked()
        {
            BoosterType? best = null;
            for (int i = 0; i < EconomyConfig.BoosterKinds; i++)
            {
                var booster = (BoosterType)i;
                if (Data.unlocked[i]) continue;
                if (best == null || EconomyConfig.UnlockLevel(booster) < EconomyConfig.UnlockLevel(best.Value)) best = booster;
            }
            return best;
        }

        /// <summary>A date as stored in <see cref="ProfileData"/>: "yyyy-MM-dd", invariant culture.</summary>
        public static string DateKey(DateTime date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

        string Today => DateKey(clock.Today.Date);

        /// <summary>
        /// Call once per app launch: the login streak grows when the last open was yesterday, stays
        /// the same when it was today, and restarts at 1 otherwise.
        /// </summary>
        public void RegisterOpen()
        {
            string today = Today;
            if (Data.lastOpenDay == today) return;
            string yesterday = DateKey(clock.Today.Date.AddDays(-1));
            Data.loginStreak = Data.lastOpenDay == yesterday ? Data.loginStreak + 1 : 1;
            Data.lastOpenDay = today;
            RaiseChanged();
        }

        /// <summary>
        /// Pays out a win, advances the level, streak and chest bar, and applies unlocks.
        /// The streak multiplier uses the streak <b>including</b> this win, so the 3rd win in a row
        /// already pays ×1.5 and the 6th ×2. A full chest bar stays full until the chest is opened.
        /// </summary>
        /// <param name="hard">The level was a HARD level (double base coins).</param>
        public WinResult RecordWin(bool hard)
        {
            var result = new WinResult
            {
                BaseCoins = hard ? EconomyConfig.HardWinCoins : EconomyConfig.WinCoins,
                StreakBefore = Data.winStreak,
                StreakAfter = Data.winStreak + 1,
                ChestBefore = Data.chestProgress,
            };
            result.Multiplier = EconomyConfig.StreakMultiplier(result.StreakAfter);
            result.CoinsEarned = (int)Math.Round(result.BaseCoins * result.Multiplier, MidpointRounding.AwayFromZero);

            Grant(result.CoinsEarned);
            Data.totalWins++;
            Data.winStreak = result.StreakAfter;
            Data.bestStreak = Math.Max(Data.bestStreak, Data.winStreak);
            Data.chestProgress = Math.Min(Data.chestProgress + 1, EconomyConfig.ChestEvery);
            Data.levelIndex++;

            result.ChestAfter = Data.chestProgress;
            result.ChestReady = ChestReady;
            result.NewLevelIndex = Data.levelIndex;
            result.Unlocked = ApplyUnlocks(Data.levelIndex);

            RaiseChanged();
            return result;
        }

        /// <summary>The player abandoned a failed or started level: the streak resets.</summary>
        public void RecordGiveUp()
        {
            Data.winStreak = 0;
            Data.totalGiveUps++;
            RaiseChanged();
        }

        public bool CanAfford(int coins) => Data.coins >= coins;

        /// <summary>Spends coins. False (and nothing changes) when unaffordable or negative.</summary>
        public bool TrySpend(int coins)
        {
            if (coins < 0 || !CanAfford(coins)) return false;
            if (coins == 0) return true;
            Data.coins -= coins;
            RaiseChanged();
            return true;
        }

        /// <summary>Grants coins (counted in lifetime <see cref="ProfileData.coinsEarned"/>). Non-positive amounts are ignored.</summary>
        public void AddCoins(int coins)
        {
            if (coins <= 0) return;
            Grant(coins);
            RaiseChanged();
        }

        /// <summary>Uses one owned booster. False when none are owned.</summary>
        public bool TryConsumeBooster(BoosterType booster)
        {
            int i = (int)booster;
            if (Data.boosters[i] <= 0) return false;
            Data.boosters[i]--;
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Buys one booster for <see cref="EconomyConfig.BoosterPrice"/> coins (it goes to the inventory).
        /// False when the booster is still locked or unaffordable.
        /// </summary>
        public bool TryBuyBooster(BoosterType booster)
        {
            int price = EconomyConfig.BoosterPrice(booster);
            if (!IsUnlocked(booster) || !CanAfford(price)) return false;
            Data.coins -= price;
            Data.boosters[(int)booster]++;
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Opens the ready chest and grants its contents. Null when no chest is ready.
        /// A normal chest holds <see cref="EconomyConfig.ChestCoins"/> plus one random unlocked booster
        /// (or <see cref="NoBoosterChestCoins"/> extra coins when none is unlocked); every
        /// <see cref="EconomyConfig.BigChestEvery"/>th chest is big: <see cref="EconomyConfig.BigChestCoins"/>
        /// plus one of each booster.
        /// </summary>
        public ChestReward OpenChest()
        {
            if (!ChestReady) return null;

            var reward = new ChestReward { Big = NextChestIsBig };
            if (reward.Big)
            {
                reward.Coins = EconomyConfig.BigChestCoins;
                for (int i = 0; i < EconomyConfig.BoosterKinds; i++) reward.Boosters[i] = 1;
            }
            else
            {
                reward.Coins = EconomyConfig.ChestCoins;
                var unlocked = new List<int>();
                for (int i = 0; i < EconomyConfig.BoosterKinds; i++)
                    if (Data.unlocked[i]) unlocked.Add(i);
                if (unlocked.Count > 0) reward.Boosters[unlocked[random.Next(unlocked.Count)]] = 1;
                else reward.Coins += NoBoosterChestCoins;
            }

            Grant(reward.Coins);
            for (int i = 0; i < EconomyConfig.BoosterKinds; i++) Data.boosters[i] += reward.Boosters[i];
            Data.chestProgress = 0;
            Data.chestsOpened++;
            RaiseChanged();
            return reward;
        }

        /// <summary>Today's calendar state: whether it can be claimed, which day it is and what it pays.</summary>
        public DailyRewardInfo DailyStatus() => new DailyRewardInfo
        {
            Available = Data.lastDailyClaim != Today,
            Day = Data.dailyIndex,
            Reward = EconomyConfig.Daily(Data.dailyIndex),
            LoginStreak = Data.loginStreak,
        };

        /// <summary>
        /// Grants today's reward and advances the calendar (wrapping after day 7). Missed days don't
        /// reset it; it simply waits. Returns what was claimed (with <c>Available</c> false), or null
        /// when today's reward was already claimed. Booster rewards go to the inventory even if the
        /// booster is still locked; they become usable once it unlocks.
        /// </summary>
        public DailyRewardInfo ClaimDaily()
        {
            var info = DailyStatus();
            if (!info.Available) return null;

            var reward = info.Reward;
            Grant(reward.Coins);
            if (reward.Booster.HasValue) Data.boosters[(int)reward.Booster.Value]++;
            if (reward.IsChest)
                for (int i = 0; i < EconomyConfig.BoosterKinds; i++) Data.boosters[i]++;

            Data.lastDailyClaim = Today;
            Data.dailyIndex = (Data.dailyIndex + 1) % DailyDays;
            info.Available = false;
            RaiseChanged();
            return info;
        }

        void Grant(int coins)
        {
            if (coins <= 0) return;
            Data.coins += coins;
            Data.coinsEarned += coins;
        }

        /// <summary>Unlocks (with the gift) every locked booster available at this level; returns the first, or null.</summary>
        BoosterType? ApplyUnlocks(int levelIndex)
        {
            BoosterType? first = null;
            for (int i = 0; i < EconomyConfig.BoosterKinds; i++)
            {
                var booster = (BoosterType)i;
                if (Data.unlocked[i] || levelIndex + 1 < EconomyConfig.UnlockLevel(booster)) continue;
                Data.unlocked[i] = true;
                Data.boosters[i] += EconomyConfig.UnlockGift;
                first ??= booster;
            }
            return first;
        }

        static void Normalize(ProfileData d)
        {
            d.boosters = Resize(d.boosters);
            d.unlocked = Resize(d.unlocked);
            for (int i = 0; i < d.boosters.Length; i++) d.boosters[i] = Math.Max(0, d.boosters[i]);

            d.coins = Math.Max(0, d.coins);
            d.levelIndex = Math.Max(0, d.levelIndex);
            d.winStreak = Math.Max(0, d.winStreak);
            d.bestStreak = Math.Max(d.winStreak, d.bestStreak);
            d.chestProgress = Math.Clamp(d.chestProgress, 0, EconomyConfig.ChestEvery);
            d.chestsOpened = Math.Max(0, d.chestsOpened);
            d.dailyIndex = d.dailyIndex < 0 ? 0 : d.dailyIndex % DailyDays;
            d.loginStreak = Math.Max(0, d.loginStreak);
            d.lastDailyClaim ??= "";
            d.lastOpenDay ??= "";
            d.totalWins = Math.Max(0, d.totalWins);
            d.totalGiveUps = Math.Max(0, d.totalGiveUps);
            d.coinsEarned = Math.Max(0, d.coinsEarned);
        }

        static T[] Resize<T>(T[] array)
        {
            if (array != null && array.Length == EconomyConfig.BoosterKinds) return array;
            var sized = new T[EconomyConfig.BoosterKinds];
            if (array != null) Array.Copy(array, sized, Math.Min(array.Length, sized.Length));
            return sized;
        }

        void RaiseChanged() => Changed?.Invoke();
    }
}
