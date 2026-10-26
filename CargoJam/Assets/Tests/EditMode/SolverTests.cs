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
    }
}
