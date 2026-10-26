using System.Linq;
using NUnit.Framework;

namespace CargoJam.Core.Tests
{
    public class SolverTests
    {
        [Test]
        public void SolutionReplaysToAWin()
        {
            var level = TestLevels.Make("RBGR", 3, 3,
                "BGRR",
                "RGBR",
                "GRBR");
            var result = Solver.Solve(level);
            Assert.That(result.Solvable, Is.True);

            var session = new GameSession(level);
            foreach (var move in result.Moves) session.Tap(move);
            Assert.That(session.Status, Is.EqualTo(GameStatus.Won));
        }

        [Test]
        public void ImpossibleLevelIsReported()
        {
            // One slot, and the red the first truck needs sits under a blue and a green.
            var level = TestLevels.Make("RBG", 1, 1, "B", "G", "R");
            var result = Solver.Solve(level);
            Assert.That(result.Solvable, Is.False);
            Assert.That(result.HitNodeLimit, Is.False);
        }

        [Test]
        public void SolverRespectsLocks()
        {
            // The blue (2,1) waits for the blue key at (0,0), which can only board after the reds.
            var data = TestLevels.Data("RB", 5, 3, "BRRR", "B.B.");
            data.locks = new[] { TestLevels.Lock(2, 1, 0, 0) };
            var level = LevelDefinition.Parse(data);
            var result = Solver.Solve(level);
            Assert.That(result.Solvable, Is.True);

            var session = new GameSession(level);
            foreach (var move in result.Moves)
                Assert.That(session.Tap(move).OfType<ParcelBlocked>(), Is.Empty, $"solver tapped a locked parcel at {move}");
            Assert.That(session.Status, Is.EqualTo(GameStatus.Won));
        }

        [Test]
        public void LockCanMakeALevelImpossible()
        {
            // The red needs its truck first, but its key is the blue that boards second.
            var data = TestLevels.Data("RB", 5, 1, "RB");
            Assert.That(Solver.Solve(LevelDefinition.Parse(data)).Solvable, Is.True);

            data.locks = new[] { TestLevels.Lock(0, 0, 1, 0) };
            var result = Solver.Solve(LevelDefinition.Parse(data));
            Assert.That(result.Solvable, Is.False);
            Assert.That(result.HitNodeLimit, Is.False);
        }
    }
}
