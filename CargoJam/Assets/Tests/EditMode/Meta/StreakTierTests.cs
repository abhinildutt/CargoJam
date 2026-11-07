using System.Collections.Generic;
using NUnit.Framework;

namespace CargoJam.Meta.Tests
{
    public class StreakTierTests
    {
        FakeClock clock;
        Progression p;
        int changes;
        List<(int from, int to)> tierChanges;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock();
            p = Make(new ProfileData());
        }

        Progression Make(ProfileData data)
        {
            var progression = new Progression(data, clock);
            changes = 0;
            tierChanges = new List<(int, int)>();
            progression.Changed += () => changes++;
            progression.StreakTierChanged += (from, to) => tierChanges.Add((from, to));
            return progression;
        }

        /// <summary>A profile on a long streak, far enough in that every booster is unlocked.</summary>
        Progression OnStreak(int streak, int levelIndex = 30) =>
            Make(new ProfileData { winStreak = streak, bestStreak = streak, levelIndex = levelIndex });

        // ---- tiers and multipliers ----

        [Test]
        public void TierBoundaries()
        {
            int[] expected = { 0, 0, 0, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3 };
            for (int streak = 0; streak < expected.Length; streak++)
                Assert.That(Progression.StreakTierFor(streak), Is.EqualTo(expected[streak]), $"streak {streak}");
            Assert.That(Progression.StreakTierFor(250), Is.EqualTo(3));
        }

        [Test]
        public void TierThresholdsLiveInEconomyConfig()
        {
            Assert.That(EconomyConfig.StreakTier1At, Is.EqualTo(3));
            Assert.That(EconomyConfig.StreakTier2At, Is.EqualTo(6));
            Assert.That(EconomyConfig.StreakTier3At, Is.EqualTo(10));
            Assert.That(EconomyConfig.MaxStreakTier, Is.EqualTo(3));
            for (int tier = 0; tier <= EconomyConfig.MaxStreakTier; tier++)
            {
                int start = EconomyConfig.StreakTierStart(tier);
                Assert.That(Progression.StreakTierFor(start), Is.EqualTo(tier), $"tier {tier} starts at {start}");
                if (start > 0)
                    Assert.That(Progression.StreakTierFor(start - 1), Is.EqualTo(tier - 1), $"just below tier {tier}");
            }
        }

        [Test]
        public void TierMultipliers()
        {
            Assert.That(Progression.TierMultiplier(0), Is.EqualTo(1f));
            Assert.That(Progression.TierMultiplier(1), Is.EqualTo(1.5f));
            Assert.That(Progression.TierMultiplier(2), Is.EqualTo(2f));
            Assert.That(Progression.TierMultiplier(3), Is.EqualTo(2.5f));
            Assert.That(Progression.TierMultiplier(-1), Is.EqualTo(1f), "out-of-range tiers clamp");
            Assert.That(Progression.TierMultiplier(9), Is.EqualTo(2.5f), "out-of-range tiers clamp");
        }

        [Test]
        public void StreakMultiplierIsTheTierMultiplier()
        {
            float[] expected = { 1f, 1f, 1f, 1.5f, 1.5f, 1.5f, 2f, 2f, 2f, 2f, 2.5f, 2.5f };
            for (int streak = 0; streak < expected.Length; streak++)
                Assert.That(EconomyConfig.StreakMultiplier(streak), Is.EqualTo(expected[streak]), $"streak {streak}");
        }

        [Test]
        public void NextTierThreshold()
        {
            int[] expected = { 3, 3, 3, 6, 6, 6, 10, 10, 10, 10, -1, -1 };
            for (int streak = 0; streak < expected.Length; streak++)
                Assert.That(Progression.NextTierThreshold(streak), Is.EqualTo(expected[streak]), $"streak {streak}");
            p = OnStreak(4);
            Assert.That(p.NextTierAt, Is.EqualTo(6));
        }

        // ---- gentle loss ----

        [Test]
        public void FloorBelow()
        {
            var table = new (int streak, int floor)[]
            {
                (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0),
                (6, 3), (7, 3), (9, 3), (10, 6), (12, 6), (40, 6),
            };
            foreach (var row in table)
                Assert.That(Progression.StreakFloorBelow(row.streak), Is.EqualTo(row.floor), $"streak {row.streak}");
        }

        [Test]
        public void GiveUpDropsToTheFloorOfTheTierBelow()
        {
            var table = new (int streak, int after)[] { (7, 3), (12, 6), (4, 0), (2, 0), (10, 6), (6, 3), (0, 0) };
            foreach (var row in table)
            {
                p = OnStreak(row.streak);
                p.RecordGiveUp();
                Assert.That(p.WinStreak, Is.EqualTo(row.after), $"give up at {row.streak}");
                Assert.That(p.BestStreak, Is.EqualTo(row.streak), "best streak is kept");
                Assert.That(changes, Is.EqualTo(1));
            }
        }

        [Test]
        public void GiveUpPreviewMatchesWhatGiveUpDoes()
        {
            p = OnStreak(7);
            Assert.That(p.StreakTier, Is.EqualTo(2));
            Assert.That(p.StreakAfterGiveUp, Is.EqualTo(3));
            Assert.That(p.TierAfterGiveUp, Is.EqualTo(1));
            Assert.That(p.MultiplierAfterGiveUp, Is.EqualTo(1.5f), "\"Streak will drop to ×1.5\"");
            Assert.That(p.GiveUpDropsTier, Is.True);
            p.RecordGiveUp();
            Assert.That(p.WinStreak, Is.EqualTo(3));
            Assert.That(p.StreakMultiplier, Is.EqualTo(1.5f));
        }

        [Test]
        public void GiveUpInTierZeroDropsNoTier()
        {
            p = OnStreak(2);
            Assert.That(p.GiveUpDropsTier, Is.False, "no warning line when the tier can't drop");
            Assert.That(p.MultiplierAfterGiveUp, Is.EqualTo(1f));
            p.RecordGiveUp();
            Assert.That(tierChanges, Is.Empty);
        }

        [Test]
        public void ContinuingKeepsTheStreak()
        {
            // Continue spends coins and nothing else: the streak only moves on win or give-up.
            p = OnStreak(8);
            Assert.That(p.TrySpend(p.ContinueCost(0)), Is.True);
            Assert.That(p.WinStreak, Is.EqualTo(8));
            Assert.That(p.StreakTier, Is.EqualTo(2));
        }

        // ---- win results ----

        [Test]
        public void WinResultReportsTierUps()
        {
            var tierUpsAt = new HashSet<int> { 3, 6, 10 };
            for (int win = 1; win <= 12; win++)
            {
                var r = p.RecordWin(false);
                Assert.That(r.TierBefore, Is.EqualTo(Progression.StreakTierFor(win - 1)), $"win {win}");
                Assert.That(r.TierAfter, Is.EqualTo(Progression.StreakTierFor(win)), $"win {win}");
                Assert.That(r.TierUp, Is.EqualTo(tierUpsAt.Contains(win)), $"win {win}");
                Assert.That(r.NextTierAt, Is.EqualTo(Progression.NextTierThreshold(win)), $"win {win}");
                Assert.That(r.Multiplier, Is.EqualTo(Progression.TierMultiplier(r.TierAfter)), $"win {win}");
            }
            Assert.That(tierChanges, Is.EqualTo(new List<(int, int)> { (0, 1), (1, 2), (2, 3) }));
        }

        [Test]
        public void TenthWinPaysTwoAndAHalf()
        {
            p = OnStreak(9);
            var r = p.RecordWin(false);
            Assert.That(r.TierUp, Is.True);
            Assert.That(r.TierAfter, Is.EqualTo(3));
            Assert.That(r.NextTierAt, Is.EqualTo(-1), "already at the top tier");
            Assert.That(r.CoinsEarned, Is.EqualTo(100));
            Assert.That(p.RecordWin(true).CoinsEarned, Is.EqualTo(200), "hard win at ×2.5");
        }

        [Test]
        public void TierChangeEventsFollowChanged()
        {
            p = OnStreak(12);
            int changesSeen = -1;
            p.StreakTierChanged += (_, __) => changesSeen = changes;
            p.RecordGiveUp();
            Assert.That(tierChanges, Is.EqualTo(new List<(int, int)> { (3, 2) }));
            Assert.That(changesSeen, Is.EqualTo(1), "Changed fires first, so listeners see the saved state");
        }

        // ---- tier-3 perk: one free Undo per level ----

        [Test]
        public void TopTierGetsOneFreeUndoPerLevel()
        {
            p = OnStreak(10);
            Assert.That(p.TierPerkUndo, Is.True);
            Assert.That(p.FreeUndoCharges, Is.EqualTo(1));
            Assert.That(p.UsableBoosterCount(BoosterType.Undo), Is.EqualTo(p.BoosterCount(BoosterType.Undo) + 1));
            Assert.That(p.UsableBoosterCount(BoosterType.Crane), Is.EqualTo(p.BoosterCount(BoosterType.Crane)), "Undo only");
        }

        [Test]
        public void BelowTopTierThereIsNoFreeUndo()
        {
            p = OnStreak(9);
            Assert.That(p.TierPerkUndo, Is.False);
            Assert.That(p.FreeUndoCharges, Is.EqualTo(0));
            Assert.That(p.UsableBoosterCount(BoosterType.Undo), Is.EqualTo(p.BoosterCount(BoosterType.Undo)));
        }

        [Test]
        public void FreeUndoIsSpentBeforeOwnedOnes()
        {
            p = OnStreak(10);
            int owned = p.BoosterCount(BoosterType.Undo);
            Assert.That(owned, Is.GreaterThan(0));
            Assert.That(p.TryConsumeBooster(BoosterType.Undo), Is.True);
            Assert.That(p.FreeUndoCharges, Is.EqualTo(0));
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(owned), "the owned stock is untouched");
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(p.TryConsumeBooster(BoosterType.Undo), Is.True);
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(owned - 1), "then owned ones are used");
        }

        [Test]
        public void FreeUndoWorksWithNoneOwned()
        {
            p = OnStreak(10);
            p.Data.boosters[(int)BoosterType.Undo] = 0;
            Assert.That(p.UsableBoosterCount(BoosterType.Undo), Is.EqualTo(1));
            Assert.That(p.TryConsumeBooster(BoosterType.Undo), Is.True);
            Assert.That(p.UsableBoosterCount(BoosterType.Undo), Is.EqualTo(0));
            Assert.That(p.TryConsumeBooster(BoosterType.Undo), Is.False);
        }

        [Test]
        public void FreeUndoIsNotBanked()
        {
            p = OnStreak(10);
            p.Data.boosters[(int)BoosterType.Undo] = 0;
            p.BeginLevel();
            p.BeginLevel();
            Assert.That(p.FreeUndoCharges, Is.EqualTo(1), "unused charges don't stack");
            Assert.That(changes, Is.EqualTo(0), "renewing an unused charge changes nothing");

            p.TryConsumeBooster(BoosterType.Undo);
            p.BeginLevel();
            Assert.That(p.FreeUndoCharges, Is.EqualTo(1), "the next level renews it");
            Assert.That(p.Data.boosters[(int)BoosterType.Undo], Is.EqualTo(0), "it never enters the inventory");
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void WinAndGiveUpRenewTheFreeUndo()
        {
            p = OnStreak(11);
            p.TryConsumeBooster(BoosterType.Undo);
            p.RecordWin(false);
            Assert.That(p.FreeUndoCharges, Is.EqualTo(1), "a win starts the next level with a fresh charge");

            p = OnStreak(30);
            p.TryConsumeBooster(BoosterType.Undo);
            p.RecordGiveUp();
            Assert.That(p.WinStreak, Is.EqualTo(6));
            Assert.That(p.FreeUndoCharges, Is.EqualTo(0), "the give-up dropped below the top tier");
        }

        [Test]
        public void FreeUndoNeedsUndoUnlocked()
        {
            p = Make(new ProfileData { winStreak = 10, levelIndex = 2 });
            Assert.That(p.IsUnlocked(BoosterType.Undo), Is.False);
            Assert.That(p.TierPerkUndo, Is.True);
            Assert.That(p.FreeUndoCharges, Is.EqualTo(0));
            Assert.That(p.TryConsumeBooster(BoosterType.Undo), Is.False);
        }
    }
}
