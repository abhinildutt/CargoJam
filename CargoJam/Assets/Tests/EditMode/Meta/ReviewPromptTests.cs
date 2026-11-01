using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CargoJam.Meta.Tests
{
    public class ReviewPromptTests
    {
        static readonly DateTime Today = new DateTime(2026, 11, 1);

        [TearDown]
        public void TearDown() => Tuning.Reset();

        static bool Ask(int level = 20, bool hard = true, int tierBefore = 0, int tierAfter = 0,
                        int asks = 0, string last = "") =>
            ReviewPrompt.ShouldAsk(level, hard, tierBefore, tierAfter, asks, last, Today);

        [Test]
        public void AsksAfterAHardWinPastTheMinimumLevel()
        {
            Assert.That(Ask(level: 12), Is.True);
            Assert.That(Ask(level: 11), Is.False);
        }

        [Test]
        public void AsksWhenAWinReachesStreakTierTwoOrHigher()
        {
            Assert.That(Ask(hard: false, tierBefore: 1, tierAfter: 2), Is.True);
            Assert.That(Ask(hard: false, tierBefore: 2, tierAfter: 3), Is.True);
            Assert.That(Ask(hard: false, tierBefore: 0, tierAfter: 1), Is.False, "tier 1 isn't a high point");
            Assert.That(Ask(hard: false, tierBefore: 2, tierAfter: 2), Is.False, "staying in a tier isn't either");
        }

        [Test]
        public void OrdinaryWinsNeverAsk()
        {
            Assert.That(Ask(hard: false), Is.False);
        }

        [Test]
        public void WaitsBetweenAsks()
        {
            Assert.That(Ask(asks: 1, last: Progression.DateKey(Today.AddDays(-119))), Is.False);
            Assert.That(Ask(asks: 1, last: Progression.DateKey(Today.AddDays(-120))), Is.True);
        }

        [Test]
        public void StopsAfterTheMaximumAsks()
        {
            Assert.That(Ask(asks: 3, last: "2020-01-01"), Is.False);
        }

        [Test]
        public void AnUnreadableLastDateDoesNotBlock()
        {
            Assert.That(Ask(asks: 1, last: "yesterday"), Is.True);
        }

        [Test]
        public void CanBeTurnedOffRemotely()
        {
            Tuning.Apply(new Dictionary<string, string> { [ReviewPrompt.EnabledKey] = "false" });
            Assert.That(Ask(), Is.False);
        }
    }
}
