using System.Linq;
using NUnit.Framework;

namespace CargoJam.Meta.Tests
{
    public class ProgressionTests
    {
        FakeClock clock;
        Progression p;
        int changes;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock();
            p = Make(new ProfileData());
        }

        Progression Make(ProfileData data, int seed = 0)
        {
            var progression = new Progression(data, clock, seed);
            changes = 0;
            progression.Changed += () => changes++;
            return progression;
        }

        static int TotalBoosters(int[] boosters) => boosters.Sum();

        // ---- construction and normalization ----

        [Test]
        public void FreshProfileStartsWithStartingCoinsAndNothingUnlocked()
        {
            Assert.That(p.Coins, Is.EqualTo(EconomyConfig.StartingCoins));
            Assert.That(p.LevelIndex, Is.EqualTo(0));
            Assert.That(p.WinStreak, Is.EqualTo(0));
            for (int i = 0; i < EconomyConfig.BoosterKinds; i++)
            {
                Assert.That(p.IsUnlocked((BoosterType)i), Is.False);
                Assert.That(p.BoosterCount((BoosterType)i), Is.EqualTo(0));
            }
        }

        [Test]
        public void NullDataMakesAFreshProfile()
        {
            var fresh = new Progression(null, clock);
            Assert.That(fresh.Data, Is.Not.Null);
            Assert.That(fresh.Coins, Is.EqualTo(EconomyConfig.StartingCoins));
        }

        [Test]
        public void NullClockFallsBackToSystemClock()
        {
            var system = new Progression(new ProfileData(), null);
            Assert.That(system.DailyStatus().Available, Is.True);
        }

        [Test]
        public void NullAndShortArraysAreResized()
        {
            var data = new ProfileData { boosters = null, unlocked = new[] { true } };
            p = Make(data);
            Assert.That(data.boosters.Length, Is.EqualTo(EconomyConfig.BoosterKinds));
            Assert.That(data.unlocked.Length, Is.EqualTo(EconomyConfig.BoosterKinds));
            Assert.That(p.IsUnlocked(BoosterType.Undo), Is.True, "existing entries are kept");
            Assert.That(p.IsUnlocked(BoosterType.Crane), Is.False);
            Assert.That(p.BoosterCount(BoosterType.ExtraSlot), Is.EqualTo(0));
        }

        [Test]
        public void LongArraysAreTruncatedKeepingKnownEntries()
        {
            var data = new ProfileData { boosters = new[] { 1, 2, 3, 4, 5 } };
            p = Make(data);
            Assert.That(data.boosters, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void NegativeValuesAreClamped()
        {
            var data = new ProfileData
            {
                coins = -10, levelIndex = -3, winStreak = -1, bestStreak = -1, chestProgress = -2,
                chestsOpened = -1, dailyIndex = -4, loginStreak = -1, totalWins = -1, totalGiveUps = -1,
                coinsEarned = -5, boosters = new[] { -1, 2, -3 },
            };
            p = Make(data);
            Assert.That(data.coins, Is.EqualTo(0));
            Assert.That(data.levelIndex, Is.EqualTo(0));
            Assert.That(data.winStreak, Is.EqualTo(0));
            Assert.That(data.bestStreak, Is.EqualTo(0));
            Assert.That(data.chestProgress, Is.EqualTo(0));
            Assert.That(data.chestsOpened, Is.EqualTo(0));
            Assert.That(data.dailyIndex, Is.EqualTo(0));
            Assert.That(data.loginStreak, Is.EqualTo(0));
            Assert.That(data.totalWins, Is.EqualTo(0));
            Assert.That(data.totalGiveUps, Is.EqualTo(0));
            Assert.That(data.coinsEarned, Is.EqualTo(0));
            Assert.That(data.boosters, Is.EqualTo(new[] { 0, 2, 0 }));
        }

        [Test]
        public void OutOfRangeValuesAreBroughtIntoRange()
        {
            var data = new ProfileData { chestProgress = 99, dailyIndex = 9, winStreak = 7, bestStreak = 2 };
            p = Make(data);
            Assert.That(data.chestProgress, Is.EqualTo(EconomyConfig.ChestEvery));
            Assert.That(p.ChestReady, Is.True);
            Assert.That(data.dailyIndex, Is.EqualTo(2));
            Assert.That(data.bestStreak, Is.EqualTo(7), "best streak is never below the current one");
        }

        [Test]
        public void NullDateStringsBecomeEmpty()
        {
            var data = new ProfileData { lastDailyClaim = null, lastOpenDay = null };
            p = Make(data);
            Assert.That(data.lastDailyClaim, Is.EqualTo(""));
            Assert.That(data.lastOpenDay, Is.EqualTo(""));
            Assert.That(p.DailyStatus().Available, Is.True);
        }

        [Test]
        public void MigratedSaveUnlocksBoostersAlreadyReachedWithGifts()
        {
            // levelIndex 12 = level 13: past Undo (6) and Crane (11), not Extra Slot (16).
            var data = new ProfileData { levelIndex = 12 };
            p = Make(data);
            Assert.That(p.IsUnlocked(BoosterType.Undo), Is.True);
            Assert.That(p.IsUnlocked(BoosterType.Crane), Is.True);
            Assert.That(p.IsUnlocked(BoosterType.ExtraSlot), Is.False);
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(EconomyConfig.UnlockGift));
            Assert.That(p.BoosterCount(BoosterType.Crane), Is.EqualTo(EconomyConfig.UnlockGift));
            Assert.That(p.BoosterCount(BoosterType.ExtraSlot), Is.EqualTo(0));
        }

        [Test]
        public void MigrationUnlocksAtExactlyTheUnlockLevel()
        {
            p = Make(new ProfileData { levelIndex = 5 }); // level 6
            Assert.That(p.IsUnlocked(BoosterType.Undo), Is.True);
            p = Make(new ProfileData { levelIndex = 4 }); // level 5
            Assert.That(p.IsUnlocked(BoosterType.Undo), Is.False);
        }

        [Test]
        public void MigrationDoesNotRegiftAlreadyUnlockedBoosters()
        {
            var data = new ProfileData { levelIndex = 7, unlocked = new[] { true, false, false }, boosters = new[] { 1, 0, 0 } };
            p = Make(data);
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(1));
            var again = new Progression(data, clock);
            Assert.That(again.BoosterCount(BoosterType.Undo), Is.EqualTo(1), "reloading is idempotent");
        }

        // ---- wins and streaks ----

        [Test]
        public void NormalWinPaysWinCoins()
        {
            var r = p.RecordWin(false);
            Assert.That(r.BaseCoins, Is.EqualTo(EconomyConfig.WinCoins));
            Assert.That(r.CoinsEarned, Is.EqualTo(40));
            Assert.That(p.Coins, Is.EqualTo(EconomyConfig.StartingCoins + 40));
        }

        [Test]
        public void HardWinPaysDouble()
        {
            var r = p.RecordWin(true);
            Assert.That(r.BaseCoins, Is.EqualTo(EconomyConfig.HardWinCoins));
            Assert.That(r.CoinsEarned, Is.EqualTo(80));
        }

        [Test]
        public void StreakMultiplierCountsTheWinBeingRecorded()
        {
            // Wins 1-2 ×1, wins 3-5 ×1.5, wins 6-9 ×2, wins 10+ ×2.5: the 3rd win in a row already pays the bonus.
            var expected = new[] { 40, 40, 60, 60, 60, 80, 80, 80, 80, 100, 100 };
            var multipliers = new[] { 1f, 1f, 1.5f, 1.5f, 1.5f, 2f, 2f, 2f, 2f, 2.5f, 2.5f };
            for (int i = 0; i < expected.Length; i++)
            {
                var r = p.RecordWin(false);
                Assert.That(r.StreakBefore, Is.EqualTo(i), $"win {i + 1}");
                Assert.That(r.StreakAfter, Is.EqualTo(i + 1), $"win {i + 1}");
                Assert.That(r.Multiplier, Is.EqualTo(multipliers[i]), $"win {i + 1}");
                Assert.That(r.CoinsEarned, Is.EqualTo(expected[i]), $"win {i + 1}");
            }
        }

        [Test]
        public void HardWinOnALongStreakStacks()
        {
            for (int i = 0; i < 5; i++) p.RecordWin(false);
            var r = p.RecordWin(true);
            Assert.That(r.Multiplier, Is.EqualTo(2f));
            Assert.That(r.CoinsEarned, Is.EqualTo(160));
        }

        [Test]
        public void StreakMultiplierPropertiesTrackCurrentAndNextWin()
        {
            Assert.That(p.StreakMultiplier, Is.EqualTo(1f));
            Assert.That(p.NextWinMultiplier, Is.EqualTo(1f));
            p.RecordWin(false);
            p.RecordWin(false);
            Assert.That(p.StreakMultiplier, Is.EqualTo(1f));
            Assert.That(p.NextWinMultiplier, Is.EqualTo(1.5f));
            var r = p.RecordWin(false);
            Assert.That(r.Multiplier, Is.EqualTo(1.5f));
            Assert.That(p.StreakMultiplier, Is.EqualTo(1.5f));
        }

        [Test]
        public void WinFillsEveryResultField()
        {
            p.Data.chestProgress = 2;
            var r = p.RecordWin(false);
            Assert.That(r.BaseCoins, Is.EqualTo(40));
            Assert.That(r.Multiplier, Is.EqualTo(1f));
            Assert.That(r.CoinsEarned, Is.EqualTo(40));
            Assert.That(r.StreakBefore, Is.EqualTo(0));
            Assert.That(r.StreakAfter, Is.EqualTo(1));
            Assert.That(r.ChestBefore, Is.EqualTo(2));
            Assert.That(r.ChestAfter, Is.EqualTo(3));
            Assert.That(r.ChestReady, Is.False);
            Assert.That(r.Unlocked, Is.Null);
            Assert.That(r.NewLevelIndex, Is.EqualTo(1));
            Assert.That(r.TierBefore, Is.EqualTo(0));
            Assert.That(r.TierAfter, Is.EqualTo(0));
            Assert.That(r.TierUp, Is.False);
            Assert.That(r.NextTierAt, Is.EqualTo(3));
            Assert.That(r.ArrivedAt, Is.EqualTo(-1));
            Assert.That(r.PostcardEarned, Is.EqualTo(-1));
        }

        [Test]
        public void WinUpdatesTotalsAndRaisesChangedOnce()
        {
            p.RecordWin(false);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(p.Data.totalWins, Is.EqualTo(1));
            Assert.That(p.Data.coinsEarned, Is.EqualTo(40));
            Assert.That(p.LevelIndex, Is.EqualTo(1));
            Assert.That(p.BestStreak, Is.EqualTo(1));
        }

        [Test]
        public void GiveUpInTierOneResetsStreakButKeepsBest()
        {
            for (int i = 0; i < 4; i++) p.RecordWin(false);
            p.RecordGiveUp();
            Assert.That(changes, Is.EqualTo(5));
            Assert.That(p.WinStreak, Is.EqualTo(0), "tier 1 drops to tier 0's floor");
            Assert.That(p.BestStreak, Is.EqualTo(4));
            Assert.That(p.Data.totalGiveUps, Is.EqualTo(1));
            Assert.That(p.LevelIndex, Is.EqualTo(4), "giving up doesn't move the level");
            Assert.That(p.RecordWin(false).Multiplier, Is.EqualTo(1f));
        }

        [Test]
        public void GiveUpHigherUpDropsOnlyOneTier()
        {
            for (int i = 0; i < 7; i++) p.RecordWin(false);
            p.RecordGiveUp();
            Assert.That(p.WinStreak, Is.EqualTo(3), "7 drops to the floor of tier 1");
            Assert.That(p.BestStreak, Is.EqualTo(7));
            Assert.That(p.RecordWin(false).Multiplier, Is.EqualTo(1.5f), "the next win still pays the lower tier");
        }

        [Test]
        public void GiveUpWithoutStreakStillCounts()
        {
            p.RecordGiveUp();
            p.RecordGiveUp();
            Assert.That(p.Data.totalGiveUps, Is.EqualTo(2));
            Assert.That(changes, Is.EqualTo(2));
        }

        // ---- chest bar ----

        [Test]
        public void ChestIsReadyAfterFiveWins()
        {
            for (int i = 0; i < 4; i++) Assert.That(p.RecordWin(false).ChestReady, Is.False);
            var r = p.RecordWin(false);
            Assert.That(r.ChestBefore, Is.EqualTo(4));
            Assert.That(r.ChestAfter, Is.EqualTo(5));
            Assert.That(r.ChestReady, Is.True);
            Assert.That(p.ChestReady, Is.True);
        }

        [Test]
        public void ChestBarCapsWhileAChestIsUnopened()
        {
            for (int i = 0; i < 7; i++) p.RecordWin(false);
            Assert.That(p.ChestProgress, Is.EqualTo(EconomyConfig.ChestEvery));
            var r = p.RecordWin(false);
            Assert.That(r.ChestBefore, Is.EqualTo(5));
            Assert.That(r.ChestAfter, Is.EqualTo(5));
        }

        // ---- unlocks ----

        [Test]
        public void BoostersUnlockOnReachingLevels6_11_16WithGifts()
        {
            var unlockedOnWin = new System.Collections.Generic.Dictionary<BoosterType, int>();
            for (int win = 1; win <= 20; win++)
            {
                var r = p.RecordWin(false);
                if (r.Unlocked.HasValue) unlockedOnWin.Add(r.Unlocked.Value, win);
                if (p.ChestReady) p.OpenChest();
            }
            // Winning level N (1-based) moves the player to level N+1.
            Assert.That(unlockedOnWin[BoosterType.Undo], Is.EqualTo(5));
            Assert.That(unlockedOnWin[BoosterType.Crane], Is.EqualTo(10));
            Assert.That(unlockedOnWin[BoosterType.ExtraSlot], Is.EqualTo(15));
            Assert.That(unlockedOnWin.Count, Is.EqualTo(3));
        }

        [Test]
        public void UnlockGrantsGiftImmediately()
        {
            for (int i = 0; i < 4; i++) p.RecordWin(false);
            Assert.That(p.IsUnlocked(BoosterType.Undo), Is.False);
            var r = p.RecordWin(false);
            Assert.That(r.Unlocked, Is.EqualTo(BoosterType.Undo));
            Assert.That(r.NewLevelIndex, Is.EqualTo(5));
            Assert.That(p.IsUnlocked(BoosterType.Undo), Is.True);
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(EconomyConfig.UnlockGift));
            Assert.That(p.RecordWin(false).Unlocked, Is.Null, "an unlock is reported once");
        }

        [Test]
        public void SimultaneousUnlocksReportFirstAndGrantAll()
        {
            p.Data.levelIndex = 14; // jumped ahead behind the API's back, e.g. a debug level skip
            var r = p.RecordWin(false);
            Assert.That(r.Unlocked, Is.EqualTo(BoosterType.Undo));
            for (int i = 0; i < EconomyConfig.BoosterKinds; i++)
            {
                Assert.That(p.IsUnlocked((BoosterType)i), Is.True);
                Assert.That(p.BoosterCount((BoosterType)i), Is.EqualTo(EconomyConfig.UnlockGift));
            }
        }

        [Test]
        public void UnlockForAndNextLocked()
        {
            Assert.That(Progression.UnlockFor(5), Is.EqualTo(BoosterType.Undo));
            Assert.That(Progression.UnlockFor(10), Is.EqualTo(BoosterType.Crane));
            Assert.That(Progression.UnlockFor(15), Is.EqualTo(BoosterType.ExtraSlot));
            Assert.That(Progression.UnlockFor(0), Is.Null);
            Assert.That(Progression.UnlockFor(6), Is.Null);
            Assert.That(p.NextLocked(), Is.EqualTo(BoosterType.Undo));
            p = Make(new ProfileData { levelIndex = 10 });
            Assert.That(p.NextLocked(), Is.EqualTo(BoosterType.ExtraSlot));
            p = Make(new ProfileData { levelIndex = 30 });
            Assert.That(p.NextLocked(), Is.Null);
        }

        // ---- coins ----

        [Test]
        public void TrySpendDeductsWhenAffordable()
        {
            Assert.That(p.TrySpend(100), Is.True);
            Assert.That(p.Coins, Is.EqualTo(200));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(p.TrySpend(200), Is.True);
            Assert.That(p.Coins, Is.EqualTo(0));
        }

        [Test]
        public void SpendingMoreThanOwnedFailsAndChangesNothing()
        {
            Assert.That(p.CanAfford(301), Is.False);
            Assert.That(p.TrySpend(301), Is.False);
            Assert.That(p.Coins, Is.EqualTo(300));
            Assert.That(changes, Is.EqualTo(0));
        }

        [Test]
        public void NegativeSpendIsRejected()
        {
            Assert.That(p.TrySpend(-50), Is.False);
            Assert.That(p.Coins, Is.EqualTo(300));
            Assert.That(changes, Is.EqualTo(0));
        }

        [Test]
        public void AddCoinsCountsAsEarnedAndIgnoresNonPositive()
        {
            p.AddCoins(25);
            Assert.That(p.Coins, Is.EqualTo(325));
            Assert.That(p.Data.coinsEarned, Is.EqualTo(25));
            Assert.That(changes, Is.EqualTo(1));
            p.AddCoins(0);
            p.AddCoins(-10);
            Assert.That(p.Coins, Is.EqualTo(325));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ContinueCostEscalates()
        {
            Assert.That(p.ContinueCost(0), Is.EqualTo(300));
            Assert.That(p.ContinueCost(1), Is.EqualTo(600));
            Assert.That(p.ContinueCost(2), Is.EqualTo(600));
        }

        // ---- boosters ----

        [Test]
        public void ConsumingWithNoneOwnedFails()
        {
            Assert.That(p.TryConsumeBooster(BoosterType.Undo), Is.False);
            Assert.That(changes, Is.EqualTo(0));
        }

        [Test]
        public void ConsumingDecrementsInventory()
        {
            p = Make(new ProfileData { levelIndex = 5 });
            Assert.That(p.TryConsumeBooster(BoosterType.Undo), Is.True);
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(1));
            Assert.That(p.TryConsumeBooster(BoosterType.Undo), Is.True);
            Assert.That(p.TryConsumeBooster(BoosterType.Undo), Is.False);
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(0));
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void BuyingSpendsThePriceAndAddsOne()
        {
            p = Make(new ProfileData { levelIndex = 20, coins = 1000 });
            Assert.That(p.TryBuyBooster(BoosterType.Undo), Is.True);
            Assert.That(p.TryBuyBooster(BoosterType.Crane), Is.True);
            Assert.That(p.TryBuyBooster(BoosterType.ExtraSlot), Is.True);
            Assert.That(p.Coins, Is.EqualTo(1000 - 120 - 200 - 240));
            for (int i = 0; i < EconomyConfig.BoosterKinds; i++)
                Assert.That(p.BoosterCount((BoosterType)i), Is.EqualTo(EconomyConfig.UnlockGift + 1));
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void BuyingUnaffordableFailsAndChangesNothing()
        {
            p = Make(new ProfileData { levelIndex = 20, coins = 239 });
            Assert.That(p.TryBuyBooster(BoosterType.ExtraSlot), Is.False);
            Assert.That(p.Coins, Is.EqualTo(239));
            Assert.That(p.BoosterCount(BoosterType.ExtraSlot), Is.EqualTo(EconomyConfig.UnlockGift));
            Assert.That(changes, Is.EqualTo(0));
        }

        [Test]
        public void BuyingALockedBoosterFails()
        {
            Assert.That(p.TryBuyBooster(BoosterType.Undo), Is.False);
            Assert.That(p.Coins, Is.EqualTo(300));
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(0));
        }

        [Test]
        public void BuyThenConsume()
        {
            p = Make(new ProfileData { levelIndex = 5, boosters = new[] { 0, 0, 0 }, unlocked = new[] { true, false, false } });
            Assert.That(p.TryBuyBooster(BoosterType.Undo), Is.True);
            Assert.That(p.TryConsumeBooster(BoosterType.Undo), Is.True);
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(0));
            Assert.That(p.Coins, Is.EqualTo(180));
        }

        // ---- chests ----

        [Test]
        public void OpenChestIsNullWhenNotReady()
        {
            p.RecordWin(false);
            changes = 0;
            Assert.That(p.OpenChest(), Is.Null);
            Assert.That(changes, Is.EqualTo(0));
            Assert.That(p.ChestProgress, Is.EqualTo(1));
        }

        [Test]
        public void NormalChestGivesCoinsAndOneUnlockedBooster()
        {
            p = Make(new ProfileData { levelIndex = 5, chestProgress = 5 });
            int coins = p.Coins, undo = p.BoosterCount(BoosterType.Undo);
            var c = p.OpenChest();
            Assert.That(c.Big, Is.False);
            Assert.That(c.Coins, Is.EqualTo(EconomyConfig.ChestCoins));
            Assert.That(c.Boosters, Is.EqualTo(new[] { 1, 0, 0 }), "Undo is the only unlocked booster");
            Assert.That(p.Coins, Is.EqualTo(coins + 100));
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(undo + 1));
            Assert.That(p.ChestProgress, Is.EqualTo(0));
            Assert.That(p.ChestReady, Is.False);
            Assert.That(p.Data.chestsOpened, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void NormalChestOnlyPicksUnlockedBoosters()
        {
            p = Make(new ProfileData { levelIndex = 10 }); // Undo and Crane unlocked
            bool sawUndo = false, sawCrane = false;
            for (int i = 0; i < 30; i++)
            {
                p.Data.chestsOpened = 0; // keep every chest normal
                p.Data.chestProgress = EconomyConfig.ChestEvery;
                var c = p.OpenChest();
                Assert.That(TotalBoosters(c.Boosters), Is.EqualTo(1));
                Assert.That(c.Boosters[(int)BoosterType.ExtraSlot], Is.EqualTo(0));
                sawUndo |= c.Boosters[0] == 1;
                sawCrane |= c.Boosters[1] == 1;
            }
            Assert.That(sawUndo && sawCrane, Is.True, "both unlocked boosters come up");
        }

        [Test]
        public void NormalChestWithNothingUnlockedGivesExtraCoins()
        {
            p = Make(new ProfileData { chestProgress = 5 });
            var c = p.OpenChest();
            Assert.That(c.Coins, Is.EqualTo(EconomyConfig.ChestCoins + Progression.NoBoosterChestCoins));
            Assert.That(TotalBoosters(c.Boosters), Is.EqualTo(0));
            Assert.That(p.Coins, Is.EqualTo(300 + 150));
        }

        [Test]
        public void EveryFourthChestIsBig()
        {
            p = Make(new ProfileData { levelIndex = 20 });
            for (int n = 1; n <= 8; n++)
            {
                bool shouldBeBig = n % 4 == 0;
                p.Data.chestProgress = EconomyConfig.ChestEvery;
                Assert.That(p.NextChestIsBig, Is.EqualTo(shouldBeBig), $"chest {n}");
                int coins = p.Coins;
                var c = p.OpenChest();
                Assert.That(c.Big, Is.EqualTo(shouldBeBig), $"chest {n}");
                if (shouldBeBig)
                {
                    Assert.That(c.Coins, Is.EqualTo(EconomyConfig.BigChestCoins));
                    Assert.That(c.Boosters, Is.EqualTo(new[] { 1, 1, 1 }));
                }
                else
                {
                    Assert.That(c.Coins, Is.EqualTo(EconomyConfig.ChestCoins));
                    Assert.That(TotalBoosters(c.Boosters), Is.EqualTo(1));
                }
                Assert.That(p.Coins, Is.EqualTo(coins + c.Coins));
            }
        }

        [Test]
        public void BigChestGivesEveryBoosterEvenIfLocked()
        {
            p = Make(new ProfileData { chestProgress = 5, chestsOpened = 3 });
            var c = p.OpenChest();
            Assert.That(c.Big, Is.True);
            for (int i = 0; i < EconomyConfig.BoosterKinds; i++)
                Assert.That(p.BoosterCount((BoosterType)i), Is.EqualTo(1));
        }

        [Test]
        public void TwentyWinsGiveThreeNormalChestsThenABigOne()
        {
            var chests = new System.Collections.Generic.List<ChestReward>();
            for (int i = 0; i < 20; i++)
            {
                p.RecordWin(false);
                if (p.ChestReady) chests.Add(p.OpenChest());
            }
            Assert.That(chests.Select(c => c.Big), Is.EqualTo(new[] { false, false, false, true }));
            Assert.That(p.Data.chestsOpened, Is.EqualTo(4));
        }

        [Test]
        public void ChestPicksAreDeterministicForASeed()
        {
            int[] Picks(int seed)
            {
                var prog = new Progression(new ProfileData { levelIndex = 20 }, clock, seed);
                var picks = new int[20];
                for (int i = 0; i < picks.Length; i++)
                {
                    prog.Data.chestsOpened = 0;
                    prog.Data.chestProgress = EconomyConfig.ChestEvery;
                    picks[i] = System.Array.IndexOf(prog.OpenChest().Boosters, 1);
                }
                return picks;
            }
            Assert.That(Picks(7), Is.EqualTo(Picks(7)));
        }
    }
}
