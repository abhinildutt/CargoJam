using System.Linq;
using NUnit.Framework;

namespace CargoJam.Core.Tests
{
    public class GameSessionTests
    {
        [Test]
        public void MatchingParcelLoadsIntoTruck()
        {
            var s = TestLevels.Session("R", 5, 3, "RRR");
            var events = s.Tap(new Cell(0, 0));

            var loaded = events.OfType<ParcelLoaded>().Single();
            Assert.That(loaded.TruckSlot, Is.EqualTo(0));
            Assert.That(loaded.Truck, Is.SameAs(s.CurrentTruck));
            Assert.That(s.Board.Get(new Cell(0, 0)), Is.Null);
            Assert.That(s.CurrentTruck.Loaded, Is.EqualTo(1));
        }

        [Test]
        public void NonMatchingParcelGoesToFirstEmptySlot()
        {
            var s = TestLevels.Session("RB", 5, 3, "BRR", "BBR");
            var held = s.Tap(new Cell(0, 0)).OfType<ParcelHeld>().Single();
            Assert.That(held.DockSlot, Is.EqualTo(0));
            Assert.That(s.Dock.Count, Is.EqualTo(1));
            Assert.That(s.Dock[0], Is.SameAs(held.Parcel));
        }

        [Test]
        public void BlockedTapChangesNothing()
        {
            var s = TestLevels.Session("RR", 5, 3, "RRR", "RRR");
            string before = s.StateKey();
            var events = s.Tap(new Cell(1, 1));
            Assert.That(events.Single(), Is.InstanceOf<ParcelBlocked>());
            Assert.That(s.StateKey(), Is.EqualTo(before));
        }

        [Test]
        public void TappingEmptyCellDoesNothing()
        {
            var s = TestLevels.Session("R", 5, 3, "R.RR");
            Assert.That(s.Tap(new Cell(1, 0)), Is.Empty);
            Assert.That(s.Tap(new Cell(9, 9)), Is.Empty);
        }

        [Test]
        public void FullTruckDepartsNextArrivesAndPullsFromDock()
        {
            // Hold two blues, then fill red: the blue truck should take both held blues in a chain.
            var s = TestLevels.Session("RB", 5, 3, "BBRRRB");
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            s.Tap(new Cell(2, 0));
            s.Tap(new Cell(3, 0));
            var events = s.Tap(new Cell(4, 0));

            Assert.That(events.Select(e => e.GetType()), Is.EqualTo(new[]
            {
                typeof(ParcelLoaded),
                typeof(TruckDeparted),
                typeof(TruckArrived),
                typeof(ParcelLoadedFromDock),
                typeof(ParcelLoadedFromDock),
            }));
            var chain = events.OfType<ParcelLoadedFromDock>().ToArray();
            Assert.That(chain.Select(c => c.ChainIndex), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(chain.Select(c => c.DockSlot), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(s.Dock.Count, Is.Zero);
            Assert.That(s.CurrentTruck.Color, Is.EqualTo(ParcelColors.FromCode('B')));
            Assert.That(s.CurrentTruck.Loaded, Is.EqualTo(2));
        }

        [Test]
        public void ChainCanFillAndSendOffSeveralTrucks()
        {
            // Truck order R, B, B with 1-capacity trucks: holding both blues, then loading the red,
            // should send the red and both blue trucks off in one tap and win.
            var s = TestLevels.Session("RBB", 5, 1, "BBR");
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            var events = s.Tap(new Cell(2, 0));

            Assert.That(events.OfType<TruckDeparted>().Count(), Is.EqualTo(3));
            Assert.That(events.Last(), Is.InstanceOf<LevelWon>());
            Assert.That(s.Status, Is.EqualTo(GameStatus.Won));
            Assert.That(s.CurrentTruck, Is.Null);
        }

        [Test]
        public void LoadingTheLastParcelWins()
        {
            var s = TestLevels.Session("R", 5, 3, "RRR");
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            var events = s.Tap(new Cell(2, 0));

            Assert.That(events.Last(), Is.InstanceOf<LevelWon>());
            Assert.That(s.Status, Is.EqualTo(GameStatus.Won));
            Assert.That(s.Tap(new Cell(0, 0)), Is.Empty, "taps after the end are ignored");
        }

        [Test]
        public void FullDockWithNoFreeMatchFails()
        {
            // Two slots and the first truck wants red, but the only red is buried at (2,1).
            var s = TestLevels.Session("RBBBGG", 2, 1, "BGB", "GBR");
            Assert.That(s.Tap(new Cell(0, 0)).OfType<LevelFailed>(), Is.Empty);
            Assert.That(s.Status, Is.EqualTo(GameStatus.Playing));

            var events = s.Tap(new Cell(1, 0));
            Assert.That(s.Status, Is.EqualTo(GameStatus.Failed));
            Assert.That(events.Last(), Is.InstanceOf<LevelFailed>());
        }

        [Test]
        public void FullDockIsNotAFailWhileAMatchIsFree()
        {
            var s = TestLevels.Session("RBG", 2, 1, "BGR");
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            Assert.That(s.Dock.IsFull, Is.True);
            Assert.That(s.Status, Is.EqualTo(GameStatus.Playing), "the red at (2,0) is still free");
        }

        [Test]
        public void FullDockRejectsNonMatchingButAcceptsMatching()
        {
            var s = TestLevels.Session("RBGB", 2, 1, "BGRB");
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            Assert.That(s.Dock.IsFull, Is.True);
            Assert.That(s.Status, Is.EqualTo(GameStatus.Playing), "the red is still free");

            string before = s.StateKey();
            var rejected = s.Tap(new Cell(3, 0));
            Assert.That(rejected.Single(), Is.InstanceOf<DockFullRejected>());
            Assert.That(s.StateKey(), Is.EqualTo(before));

            var loaded = s.Tap(new Cell(2, 0));
            Assert.That(loaded.OfType<ParcelLoaded>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void CloneIsIndependent()
        {
            var s = TestLevels.Session("RB", 5, 3, "BRR", "BBR");
            var copy = s.Clone();
            s.Tap(new Cell(0, 0));
            Assert.That(copy.StateKey(), Is.Not.EqualTo(s.StateKey()));
            Assert.That(copy.Dock.Count, Is.Zero);
            Assert.That(copy.Board.Get(new Cell(0, 0)), Is.Not.Null);
        }
    }
}
