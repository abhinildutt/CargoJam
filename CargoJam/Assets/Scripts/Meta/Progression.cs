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
        /// <summary>The streak multiplier applied: the multiplier of <see cref="TierAfter"/>.</summary>
        public float Multiplier;
        /// <summary>Coins actually granted (BaseCoins × Multiplier, rounded).</summary>
        public int CoinsEarned;
        public int StreakBefore;
        public int StreakAfter;
        /// <summary>Streak tier (0..<see cref="EconomyConfig.MaxStreakTier"/>) before and after this win.</summary>
        public int TierBefore;
        public int TierAfter;
        /// <summary>This win moved the streak into a higher tier (play the tier-up moment).</summary>
        public bool TierUp;
        /// <summary>The streak at which the next tier starts, or -1 at the top tier.</summary>
        public int NextTierAt = -1;
        /// <summary>Chest bar before and after, in wins (0..ChestEvery).</summary>
        public int ChestBefore;
        public int ChestAfter;
        public bool ChestReady;
        /// <summary>A booster unlocked by reaching the next level (its gift is already granted).</summary>
        public BoosterType? Unlocked;
        public int NewLevelIndex;
        /// <summary>
        /// The destination (<see cref="Journey"/> stop) this win arrived at, or -1 when the next level
        /// is not the first of a destination. Set on every arrival, including later loops.
        /// </summary>
        public int ArrivedAt = -1;
        /// <summary>The stop whose postcard this win earned for the first time, or -1 for none.</summary>
        public int PostcardEarned = -1;
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
    /// The rules of the meta game: coins, streak tiers, chests, boosters, unlocks, the daily calendar
    /// and journey postcards. Engine-free and deterministic given the clock and random seed. Raises
    /// <see cref="Changed"/> after every mutation so the UI and the save layer can react.
    /// See docs/design-v2-polish-and-retention.md, section 3, and docs/design-v3-premium-journey.md,
    /// sections 2 and 6.
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
            ProfileMigration.Upgrade(Data);
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

        // ---- Streak tiers (docs/design-v3-premium-journey.md, section 6) ----

        /// <summary>
        /// Raised when a win or give-up moves the streak to another tier, as (from, to), after
        /// <see cref="Changed"/>.
        /// </summary>
        public event Action<int, int> StreakTierChanged;

        /// <summary>Streak tier 0..3 of the current streak (0-2, 3-5, 6-9, 10+).</summary>
        public int StreakTier => StreakTierFor(Data.winStreak);

        /// <summary>Tier for a streak length (0-2 → 0, 3-5 → 1, 6-9 → 2, 10+ → 3).</summary>
        public static int StreakTierFor(int streak) => EconomyConfig.StreakTierFor(streak);

        /// <summary>The coin multiplier of a tier (×1, ×1.5, ×2, ×2.5), for tier chips.</summary>
        public static float TierMultiplier(int tier) => EconomyConfig.StreakTierMultiplier(tier);

        /// <summary>The streak at which the tier after <paramref name="streak"/>'s starts (3, 6, 10), or -1 at the top tier.</summary>
        public static int NextTierThreshold(int streak)
        {
            int tier = StreakTierFor(streak);
            return tier >= EconomyConfig.MaxStreakTier ? -1 : EconomyConfig.StreakTierStart(tier + 1);
        }

        /// <summary>The streak a give-up leaves: the floor of the tier below (7 → 3, 12 → 6, 4 → 0, 2 → 0).</summary>
        public static int StreakFloorBelow(int streak)
        {
            int tier = StreakTierFor(streak);
            return tier <= 0 ? 0 : EconomyConfig.StreakTierStart(tier - 1);
        }

        /// <summary>The streak at which the next tier starts, or -1 at the top tier.</summary>
        public int NextTierAt => NextTierThreshold(Data.winStreak);

        /// <summary>What <see cref="RecordGiveUp"/> would leave the streak at right now.</summary>
        public int StreakAfterGiveUp => StreakFloorBelow(Data.winStreak);

        /// <summary>The tier a give-up would leave.</summary>
        public int TierAfterGiveUp => StreakTierFor(StreakAfterGiveUp);

        /// <summary>The tier multiplier a give-up would leave, for "Streak will drop to ×1.5".</summary>
        public float MultiplierAfterGiveUp => TierMultiplier(TierAfterGiveUp);

        /// <summary>True when giving up now would drop the streak tier (show the neutral warning line).</summary>
        public bool GiveUpDropsTier => TierAfterGiveUp < StreakTier;

        // ---- Tier-3 perk: one free Undo each level ----

        bool freeUndoUsed;

        /// <summary>The top tier's perk: one free Undo charge each level (not banked).</summary>
        public bool TierPerkUndo => StreakTier >= EconomyConfig.FreeUndoTier;

        /// <summary>
        /// Free Undo charges left in the current level: <see cref="EconomyConfig.FreeUndoPerLevel"/>
        /// while <see cref="TierPerkUndo"/> holds, Undo is unlocked and the charge is unused, else 0.
        /// The charge is never saved or banked; it renews in <see cref="BeginLevel"/>.
        /// </summary>
        public int FreeUndoCharges => TierPerkUndo && IsUnlocked(BoosterType.Undo) && !freeUndoUsed
            ? EconomyConfig.FreeUndoPerLevel
            : 0;

        /// <summary>
        /// Call when a level starts (every <c>LevelStarted</c>, restarts included): renews the free
        /// Undo charge. Wins and give-ups also renew it, so a missed call can't strand the perk.
        /// </summary>
        public void BeginLevel()
        {
            if (!freeUndoUsed) return;
            freeUndoUsed = false;
            RaiseChanged();
        }

        /// <summary>
        /// Boosters usable right now: the owned count plus, for Undo, <see cref="FreeUndoCharges"/>.
        /// Use this (not <see cref="BoosterCount"/>) to decide between using a booster and offering to buy one.
        /// </summary>
        public int UsableBoosterCount(BoosterType booster) =>
            BoosterCount(booster) + (booster == BoosterType.Undo ? FreeUndoCharges : 0);

        // ---- Journey: postcards and arrival reveals ----

        /// <summary>Raised when a win arrives at a destination, as (stop, leg), after <see cref="Changed"/>.</summary>
        public event Action<int, int> DestinationReached;

        public bool HasPostcard(int stop) =>
            Data.postcards != null && stop >= 0 && stop < Data.postcards.Length && Data.postcards[stop];

        /// <summary>How many destination postcards have been earned.</summary>
        public int PostcardCount
        {
            get
            {
                int n = 0;
                foreach (bool earned in Data.postcards)
                    if (earned) n++;
                return n;
            }
        }

        /// <summary>Marks a destination's postcard earned. Returns true when it is new (and raises <see cref="Changed"/>).</summary>
        public bool MarkPostcard(int stop)
        {
            if (!SetPostcard(stop)) return false;
            RaiseChanged();
            return true;
        }

        /// <summary>True when the level at <paramref name="levelIndex"/> starts a destination whose arrival reveal hasn't played.</summary>
        public bool ShouldPlayArrival(int levelIndex) =>
            Journey.IsArrival(levelIndex) && Journey.LegFor(levelIndex) > Data.lastArrivalShown;

        /// <summary>
        /// Records that the arrival reveal for <paramref name="levelIndex"/>'s leg has played, so it
        /// won't play again until the next destination (or the same one on the next loop). Never moves
        /// backwards. Raises <see cref="Changed"/> when it changes anything.
        /// </summary>
        public void MarkArrivalShown(int levelIndex)
        {
            int leg = Journey.LegFor(levelIndex);
            if (leg <= Data.lastArrivalShown) return;
            Data.lastArrivalShown = leg;
            RaiseChanged();
        }

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
        /// Pays out a win, advances the level, streak and chest bar, applies unlocks, and earns the
        /// postcard when the next level starts a new destination.
        /// The streak multiplier uses the streak <b>including</b> this win, so the 3rd win in a row
        /// already pays ×1.5, the 6th ×2 and the 10th ×2.5. A full chest bar stays full until the chest
        /// is opened. Raises <see cref="Changed"/> once, then <see cref="StreakTierChanged"/> and
        /// <see cref="DestinationReached"/> when they apply.
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
            result.TierBefore = StreakTierFor(result.StreakBefore);
            result.TierAfter = StreakTierFor(result.StreakAfter);
            result.TierUp = result.TierAfter > result.TierBefore;
            result.NextTierAt = NextTierThreshold(result.StreakAfter);
            result.Multiplier = TierMultiplier(result.TierAfter);
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
            if (Journey.IsArrival(Data.levelIndex))
            {
                result.ArrivedAt = Journey.StopFor(Data.levelIndex);
                if (SetPostcard(result.ArrivedAt)) result.PostcardEarned = result.ArrivedAt;
            }
            freeUndoUsed = false;

            RaiseChanged();
            if (result.TierUp) StreakTierChanged?.Invoke(result.TierBefore, result.TierAfter);
            if (result.ArrivedAt >= 0) DestinationReached?.Invoke(result.ArrivedAt, Journey.LegFor(Data.levelIndex));
            return result;
        }

        /// <summary>
        /// The player abandoned a failed or started level (fail → give up, or restart / home after a
        /// move). Gentle loss: the streak drops to the floor of the tier below
        /// (<see cref="StreakFloorBelow"/>: 7 → 3, 12 → 6, 4 → 0) instead of to zero. Continue doesn't
        /// call this, so it keeps the streak. Raises <see cref="Changed"/>, then
        /// <see cref="StreakTierChanged"/> when the tier dropped.
        /// </summary>
        public void RecordGiveUp()
        {
            int tierBefore = StreakTier;
            Data.winStreak = StreakFloorBelow(Data.winStreak);
            Data.totalGiveUps++;
            freeUndoUsed = false;
            RaiseChanged();
            int tierAfter = StreakTier;
            if (tierAfter != tierBefore) StreakTierChanged?.Invoke(tierBefore, tierAfter);
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

        /// <summary>
        /// Uses one booster. For Undo, the level's free tier-3 charge (<see cref="FreeUndoCharges"/>) is
        /// spent first, since it can't be banked; otherwise an owned one. False when none is usable.
        /// </summary>
        public bool TryConsumeBooster(BoosterType booster)
        {
            if (booster == BoosterType.Undo && FreeUndoCharges > 0)
            {
                freeUndoUsed = true;
                RaiseChanged();
                return true;
            }
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

        /// <summary>Sets a postcard without raising <see cref="Changed"/>. True when it is new.</summary>
        bool SetPostcard(int stop)
        {
            if (stop < 0 || stop >= Data.postcards.Length || Data.postcards[stop]) return false;
            Data.postcards[stop] = true;
            return true;
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

            // Journey (v2): every destination reached has its postcard (Harbor Town from the first
            // launch), and no reveal can have played for a leg not reached yet.
            ProfileMigration.MarkReachedPostcards(d);
            d.lastArrivalShown = Math.Clamp(d.lastArrivalShown, -1, Journey.LegFor(d.levelIndex));
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
