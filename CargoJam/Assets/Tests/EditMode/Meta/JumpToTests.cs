using NUnit.Framework;

namespace CargoJam.Meta.Tests
{
    public class JumpToTests
    {
        // (0-based debut level, cargo kind): kind 1 debuts on level 7, kind 3 on level 17.
        static readonly (int, int)[] Debuts = { (6, 1), (16, 3) };

        [Test]
        public void JumpMarksOnlyEarlierDebutsSeenSoTheLevelsOwnCardStillShows()
        {
            var p = new Progression(new ProfileData(), new FakeClock());
            p.JumpTo(16, Debuts);
            Assert.That(p.LevelIndex, Is.EqualTo(16));
            Assert.That(p.HasSeenCargo(1), Is.True);
            Assert.That(p.HasSeenCargo(3), Is.False);
        }

        [Test]
        public void JumpingBackClearsSeenForCargoThatDebutsLater()
        {
            var p = new Progression(new ProfileData(), new FakeClock());
            p.JumpTo(30, Debuts);
            p.JumpTo(6, Debuts);
            Assert.That(p.HasSeenCargo(1), Is.False);
            Assert.That(p.HasSeenCargo(3), Is.False);
        }

        [Test]
        public void JumpUnlocksBoostersAvailableByThenAndKeepsCoins()
        {
            var p = new Progression(new ProfileData(), new FakeClock());
            int coins = p.Coins;
            int changes = 0;
            p.Changed += () => changes++;
            p.JumpTo(EconomyConfig.UnlockLevel(BoosterType.Crane) - 1, Debuts);
            Assert.That(p.IsUnlocked(BoosterType.Undo), Is.True);
            Assert.That(p.IsUnlocked(BoosterType.Crane), Is.True);
            Assert.That(p.IsUnlocked(BoosterType.ExtraSlot), Is.False);
            Assert.That(p.Coins, Is.EqualTo(coins));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void JumpMarksEarlierArrivalsShownButNotTheOneItLandsOn()
        {
            var p = new Progression(new ProfileData(), new FakeClock());
            p.JumpTo(2 * Journey.LevelsPerStop, Debuts);
            Assert.That(p.ShouldPlayArrival(Journey.LevelsPerStop), Is.False);
            Assert.That(p.ShouldPlayArrival(2 * Journey.LevelsPerStop), Is.True);
        }
    }
}
