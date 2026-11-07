using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CargoJam.Meta.Tests
{
    public class NotificationPlanTests
    {
        FakeClock clock;

        [SetUp]
        public void SetUp() => clock = new FakeClock(2026, 11, 1);

        [TearDown]
        public void TearDown() => Tuning.Reset();

        Progression Player(int levelIndex = 8, int streak = 0, string claimedOn = "") =>
            new Progression(new ProfileData
            {
                levelIndex = levelIndex,
                winStreak = streak,
                bestStreak = streak,
                lastDailyClaim = claimedOn,
                dailyIndex = 2,
            }, clock);

        DateTime At(int hour, int minute = 0) => clock.Today.AddHours(hour).AddMinutes(minute);

        [Test]
        public void NothingBeforeTheMinimumLevel()
        {
            Assert.That(NotificationPlan.For(Player(levelIndex: 4), At(10)), Is.Empty);
            Assert.That(NotificationPlan.ShouldAskPermission(Player(levelIndex: 4)), Is.False);
            Assert.That(NotificationPlan.ShouldAskPermission(Player(levelIndex: 5)), Is.True);
        }

        [Test]
        public void OnePerDayAtSevenForThreeDays()
        {
            var plan = NotificationPlan.For(Player(streak: 4), At(10));
            Assert.That(plan.Count, Is.EqualTo(3));
            for (int i = 0; i < plan.Count; i++)
            {
                Assert.That(plan[i].Id, Is.EqualTo(i + 1));
                Assert.That(plan[i].FireAt, Is.EqualTo(clock.Today.AddDays(i).AddHours(19)));
            }
        }

        [Test]
        public void UnclaimedDailyRewardComesFirst()
        {
            var plan = NotificationPlan.For(Player(), At(10));
            Assert.That(plan[0].Title, Is.EqualTo("Daily reward ready"));
            Assert.That(plan[0].Body, Does.Contain("Day 3"));
        }

        [Test]
        public void AlreadyClaimedTodaySkipsTheDailyReminderTonight()
        {
            var plan = NotificationPlan.For(Player(claimedOn: Progression.DateKey(clock.Today)), At(10));
            Assert.That(plan[0].Title, Is.Not.EqualTo("Daily reward ready"));
            Assert.That(plan.Count, Is.EqualTo(1), "no streak, so only the generic reminder is left");
        }

        [Test]
        public void TooCloseToTonightMovesToTomorrow()
        {
            var plan = NotificationPlan.For(Player(), At(18, 30));
            Assert.That(plan[0].FireAt, Is.EqualTo(clock.Today.AddDays(1).AddHours(19)));
            // Claimed today, but the first slot is tomorrow, when the next reward is waiting.
            plan = NotificationPlan.For(Player(claimedOn: Progression.DateKey(clock.Today)), At(22));
            Assert.That(plan[0].Title, Is.EqualTo("Daily reward ready"));
        }

        [Test]
        public void StreakReminderOnlyForAStreakWorthMentioning()
        {
            Assert.That(NotificationPlan.For(Player(streak: 2), At(10)).Exists(n => n.Title.Contains("streak")), Is.False);
            Assert.That(NotificationPlan.For(Player(streak: 3), At(10)).Exists(n => n.Title == "3-win streak"), Is.True);
        }

        [Test]
        public void GenericReminderNamesTheNextLevelAndDestination()
        {
            var plan = NotificationPlan.For(Player(levelIndex: 12), At(10));
            Assert.That(plan[plan.Count - 1].Body, Is.EqualTo("Level 13 in Farmlands is loaded and ready to go."));
        }

        [Test]
        public void TunableHourDaysAndSwitch()
        {
            Tuning.Apply(new Dictionary<string, string> { [NotificationPlan.HourKey] = "20", [NotificationPlan.DaysKey] = "1" });
            var plan = NotificationPlan.For(Player(streak: 5), At(10));
            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.That(plan[0].FireAt.Hour, Is.EqualTo(20));

            Tuning.Apply(new Dictionary<string, string> { [NotificationPlan.HourKey] = "3" });
            Assert.That(NotificationPlan.For(Player(), At(1))[0].FireAt.Hour, Is.EqualTo(9), "clamped to daytime");

            Tuning.Apply(new Dictionary<string, string> { [NotificationPlan.EnabledKey] = "false" });
            Assert.That(NotificationPlan.For(Player(), At(10)), Is.Empty);
        }
    }
}
