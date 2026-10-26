using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CargoJam.Core.Tests
{
    public class BoardTests
    {
        [Test]
        public void ParcelInRowZeroIsFree()
        {
            var s = TestLevels.Session("R", 5, 3, "RRR", "...");
            var path = s.Board.FindExitPath(new Cell(1, 0));
            Assert.That(path, Is.EqualTo(new List<Cell> { new Cell(1, 0) }));
        }

        [Test]
        public void ParcelBehindAFullRowIsBlocked()
        {
            var s = TestLevels.Session("RR", 5, 3, "RRR", "RRR");
            Assert.That(s.Board.FindExitPath(new Cell(1, 1)), Is.Null);
            Assert.That(s.Board.IsFree(new Cell(1, 1)), Is.False);
        }

        [Test]
        public void PathGoesAroundObstacles()
        {
            // The parcel at (1,2) goes up the empty middle column; (0,2) is walled in by (0,1) and (1,2).
            var s = TestLevels.Session("RB", 5, 3,
                "R.B",
                "R.B",
                "RB.");
            var path = s.Board.FindExitPath(new Cell(1, 2));
            Assert.That(path, Is.EqualTo(new List<Cell> { new Cell(1, 2), new Cell(1, 1), new Cell(1, 0) }));
            Assert.That(s.Board.FindExitPath(new Cell(0, 2)), Is.Null);

            // (2,1) has a parcel above it, so it steps left into the open column.
            Assert.That(s.Board.FindExitPath(new Cell(2, 1)), Is.EqualTo(new List<Cell> { new Cell(2, 1), new Cell(1, 1), new Cell(1, 0) }));
        }

        [Test]
        public void FreeParcelsMatchesFindExitPath()
        {
            var s = TestLevels.Session("RGB", 5, 3,
                "R.GR",
                "GBB.",
                "B.RG");
            var free = new List<Parcel>();
            s.Board.GetFreeParcels(free);

            var expected = s.Board.Parcels.Where(p => s.Board.FindExitPath(p.Cell) != null).Select(p => p.Id);
            Assert.That(free.Select(p => p.Id), Is.EquivalentTo(expected));
            Assert.That(free.Select(p => p.Cell), Has.Member(new Cell(1, 1)), "(1,1) touches the empty (1,0)");
            Assert.That(free.Select(p => p.Cell), Has.No.Member(new Cell(3, 2)), "(3,1) is empty but sealed in");
            Assert.That(free.Select(p => p.Cell), Has.No.Member(new Cell(2, 1)));
        }
    }
}
