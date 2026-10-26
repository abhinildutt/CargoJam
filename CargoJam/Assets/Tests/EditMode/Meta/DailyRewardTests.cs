using System;
using System.Globalization;
using NUnit.Framework;

namespace CargoJam.Meta.Tests
{
    public class DailyRewardTests
    {
        FakeClock clock;
        Progression p;
        int changes;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock(2026, 10, 26);
            p = new Progression(new ProfileData(), clock);
            changes = 0;
            p.Changed += () => changes++;
        }

        // ---- daily calendar ----

        [Test]
        public void FreshProfileCanClaimDayOne()
        {
            var s = p.DailyStatus();
            Assert.That(s.Available, Is.True);
            Assert.That(s.Day, Is.EqualTo(0));
            Assert.That(s.Reward.Coins, Is.EqualTo(50));
            Assert.That(s.Reward.Booster, Is.Null);
            Assert.That(s.Reward.IsChest, Is.False);
        }

        [Test]
        public void ClaimGrantsRewardAndRecordsTheDate()
        {
            var claimed = p.ClaimDaily();
            Assert.That(claimed, Is.Not.Null);
            Assert.That(claimed.Day, Is.EqualTo(0));
            Assert.That(claimed.Available, Is.False);
            Assert.That(claimed.Reward.Coins, Is.EqualTo(50));
            Assert.That(p.Coins, Is.EqualTo(350));
            Assert.That(p.Data.coinsEarned, Is.EqualTo(50));
            Assert.That(p.Data.lastDailyClaim, Is.EqualTo("2026-10-26"));
            Assert.That(p.Data.dailyIndex, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void SecondClaimOnTheSameDayIsNull()
        {
            p.ClaimDaily();
            Assert.That(p.ClaimDaily(), Is.Null);
            Assert.That(p.Coins, Is.EqualTo(350));
            Assert.That(p.Data.dailyIndex, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));
            var s = p.DailyStatus();
            Assert.That(s.Available, Is.False);
            Assert.That(s.Day, Is.EqualTo(1), "status previews tomorrow's reward");
        }

        [Test]
        public void NextDayOffersTheNextReward()
        {
            p.ClaimDaily();
            clock.Advance(1);
            var s = p.DailyStatus();
            Assert.That(s.Available, Is.True);
            Assert.That(s.Day, Is.EqualTo(1));
            Assert.That(s.Reward.Booster, Is.EqualTo(BoosterType.Undo));
            p.ClaimDaily();
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(1));
            Assert.That(p.Coins, Is.EqualTo(350), "booster days give no coins");
        }

        [Test]
        public void FullWeekGrantsTheCalendarAndWraps()
        {
            for (int day = 0; day < 7; day++)
            {
                var claimed = p.ClaimDaily();
                Assert.That(claimed.Day, Is.EqualTo(day));
                clock.Advance(1);
            }
            // 50 + 100 + 150 coins, plus the day-7 chest's 200.
            Assert.That(p.Coins, Is.EqualTo(300 + 50 + 100 + 150 + 200));
            // Day 2/4/6 boosters plus one of each from the chest.
            Assert.That(p.BoosterCount(BoosterType.Undo), Is.EqualTo(2));
            Assert.That(p.BoosterCount(BoosterType.Crane), Is.EqualTo(2));
            Assert.That(p.BoosterCount(BoosterType.ExtraSlot), Is.EqualTo(2));
            Assert.That(p.Data.dailyIndex, Is.EqualTo(0));
            Assert.That(p.DailyStatus().Reward.Coins, Is.EqualTo(50), "day 8 is day 1 again");
        }

        [Test]
        public void DaySevenIsAChest()
        {
            p.Data.dailyIndex = 6;
            var claimed = p.ClaimDaily();
            Assert.That(claimed.Reward.IsChest, Is.True);
            Assert.That(claimed.Reward.Coins, Is.EqualTo(200));
            for (int i = 0; i < EconomyConfig.BoosterKinds; i++)
                Assert.That(p.BoosterCount((BoosterType)i), Is.EqualTo(1));
            Assert.That(p.Data.dailyIndex, Is.EqualTo(0));
        }

        [Test]
        public void MissedDaysDontResetTheCalendar()
        {
            p.ClaimDaily();
            clock.Advance(1);
            p.ClaimDaily();
            clock.Advance(5);
            var s = p.DailyStatus();
            Assert.That(s.Available, Is.True);
            Assert.That(s.Day, Is.EqualTo(2));
        }

        [Test]
        public void ClaimingAfterMidnightIsAllowedEvenIfOnlyHoursPassed()
        {
            clock.Today = new DateTime(2026, 10, 26, 23, 59, 0);
            p.ClaimDaily();
            clock.Today = new DateTime(2026, 10, 27, 0, 1, 0);
            Assert.That(p.ClaimDaily(), Is.Not.Null);
        }

        [Test]
        public void GoingBackInTimeStillAllowsOneClaimPerDate()
        {
            // A clock set back by the player can't re-claim the date already claimed, but a different
            // date is claimable: the calendar only remembers the last claim.
            p.ClaimDaily();
            clock.Advance(-1);
            Assert.That(p.DailyStatus().Available, Is.True);
            clock.Advance(1);
            Assert.That(p.ClaimDaily(), Is.Null);
        }

        [Test]
        public void DateKeyIsInvariantCulture()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("th-TH"); // Buddhist calendar: year 2569
                Assert.That(Progression.DateKey(new DateTime(2026, 3, 5)), Is.EqualTo("2026-03-05"));
                p.ClaimDaily();
                Assert.That(p.Data.lastDailyClaim, Is.EqualTo("2026-10-26"));
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [Test]
        public void DailyStatusReportsLoginStreak()
        {
            p.RegisterOpen();
            clock.Advance(1);
            p.RegisterOpen();
            Assert.That(p.DailyStatus().LoginStreak, Is.EqualTo(2));
            Assert.That(p.ClaimDaily().LoginStreak, Is.EqualTo(2));
        }

        // ---- login streak ----

        [Test]
        public void FirstOpenStartsTheStreakAtOne()
        {
            p.RegisterOpen();
            Assert.That(p.LoginStreak, Is.EqualTo(1));
            Assert.That(p.Data.lastOpenDay, Is.EqualTo("2026-10-26"));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void SecondOpenOnTheSameDayChangesNothing()
        {
            p.RegisterOpen();
            p.RegisterOpen();
            Assert.That(p.LoginStreak, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ConsecutiveDaysGrowTheStreak()
        {
            for (int day = 1; day <= 4; day++)
            {
                p.RegisterOpen();
                Assert.That(p.LoginStreak, Is.EqualTo(day));
                clock.Advance(1);
            }
        }

        [Test]
        public void SkippingADayRestartsTheStreak()
        {
            p.RegisterOpen();
            clock.Advance(1);
            p.RegisterOpen();
            clock.Advance(2);
            p.RegisterOpen();
            Assert.That(p.LoginStreak, Is.EqualTo(1));
        }

        [Test]
        public void StreakCrossesMonthAndYearBoundaries()
        {
            clock.Today = new DateTime(2026, 12, 31);
            p.RegisterOpen();
            clock.Advance(1);
            p.RegisterOpen();
            Assert.That(p.LoginStreak, Is.EqualTo(2));
            Assert.That(p.Data.lastOpenDay, Is.EqualTo("2027-01-01"));
        }

        [Test]
        public void StreakSurvivesReload()
        {
            p.RegisterOpen();
            clock.Advance(1);
            var reloaded = new Progression(p.Data, clock);
            reloaded.RegisterOpen();
            Assert.That(reloaded.LoginStreak, Is.EqualTo(2));
        }

        [Test]
        public void GarbageLastOpenDayRestartsTheStreak()
        {
            var data = new ProfileData { lastOpenDay = "not a date", loginStreak = 9 };
            var prog = new Progression(data, clock);
            prog.RegisterOpen();
            Assert.That(prog.LoginStreak, Is.EqualTo(1));
        }
    }
}
