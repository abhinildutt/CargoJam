using System.Collections.Generic;
using NUnit.Framework;

namespace CargoJam.Meta.Tests
{
    public class CoinFlowTests
    {
        Progression p;
        List<(int amount, string reason)> moves;

        [SetUp]
        public void SetUp()
        {
            p = new Progression(new ProfileData { coins = 1000, levelIndex = 30 }, new FakeClock());
            moves = new List<(int, string)>();
            p.CoinsMoved += (amount, reason) => moves.Add((amount, reason));
        }

        [Test]
        public void WinIsASource()
        {
            var result = p.RecordWin(false);
            Assert.That(moves, Is.EqualTo(new[] { (result.CoinsEarned, CoinReason.Win) }));
        }

        [Test]
        public void SpendsAreNegativeWithTheirReason()
        {
            p.Data.unlocked[(int)BoosterType.Crane] = true;
            p.TrySpend(300, CoinReason.Continue);
            p.TryBuyBooster(BoosterType.Crane);
            Assert.That(moves, Is.EqualTo(new[] { (-300, CoinReason.Continue), (-200, "booster_crane") }));
        }

        [Test]
        public void FailedOrEmptySpendsRaiseNothing()
        {
            p.TrySpend(5000);
            p.TrySpend(0);
            p.AddCoins(0);
            Assert.That(moves, Is.Empty);
        }

        [Test]
        public void GrantsDefaultToGrantAndCanCarryAReason()
        {
            p.AddCoins(10);
            p.AddCoins(300, CoinReason.Refund);
            Assert.That(moves, Is.EqualTo(new[] { (10, CoinReason.Grant), (300, CoinReason.Refund) }));
        }

        [Test]
        public void ChestAndDailyAreSources()
        {
            p.Data.chestProgress = EconomyConfig.ChestEvery;
            var chest = p.OpenChest();
            var daily = p.ClaimDaily();
            Assert.That(moves[0], Is.EqualTo((chest.Coins, chest.Big ? CoinReason.BigChest : CoinReason.Chest)));
            Assert.That(moves[1], Is.EqualTo((daily.Reward.Coins, CoinReason.Daily)));
        }

        [Test]
        public void ABoosterOnlyDailyRewardMovesNoCoins()
        {
            p.Data.dailyIndex = 1;
            p.ClaimDaily();
            Assert.That(moves, Is.Empty);
        }
    }
}
