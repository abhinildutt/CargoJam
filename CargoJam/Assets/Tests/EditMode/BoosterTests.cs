using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CargoJam.Core.Tests
{
    public class BoosterTests
    {
        // ---- Undo -------------------------------------------------------------------------------

        [Test]
        public void UndoNeedsAHeldParcel()
        {
            var s = TestLevels.Session("RB", 5, 3, "BRR", "BBR");
            Assert.That(s.CanUndo, Is.False);
            Assert.That(s.Undo(), Is.Empty);

            s.Tap(new Cell(1, 0)); // a red goes straight into the truck, not the dock
            Assert.That(s.CanUndo, Is.False);
        }

        [Test]
        public void UndoPutsTheHeldParcelBackOnItsCell()
        {
            var s = TestLevels.Session("RB", 5, 3, "BRR", "BBR");
            string before = s.StateKey();
            var held = s.Tap(new Cell(0, 0)).OfType<ParcelHeld>().Single();
            Assert.That(s.CanUndo, Is.True);

            var events = s.Undo();
            var returned = (ParcelReturned)events.Single();
            Assert.That(returned.Parcel, Is.SameAs(held.Parcel));
            Assert.That(returned.DockSlot, Is.EqualTo(0));
            Assert.That(s.Board.Get(new Cell(0, 0)), Is.SameAs(held.Parcel));
            Assert.That(s.Dock.Count, Is.Zero);
            Assert.That(s.CanUndo, Is.False);
            Assert.That(s.StateKey(), Is.EqualTo(before));
            Assert.That(s.Board.IsFree(new Cell(0, 1)), Is.False, "the returned parcel blocks again");
        }

        [Test]
        public void UndoGoesInReverseHoldOrder()
        {
            var s = TestLevels.Session("RB", 5, 3, "BBR", "BRR");
            var first = s.Tap(new Cell(0, 0)).OfType<ParcelHeld>().Single();
            var second = s.Tap(new Cell(1, 0)).OfType<ParcelHeld>().Single();

            var a = s.Undo().OfType<ParcelReturned>().Single();
            Assert.That(a.Parcel, Is.SameAs(second.Parcel));
            Assert.That(a.DockSlot, Is.EqualTo(1));

            var b = s.Undo().OfType<ParcelReturned>().Single();
            Assert.That(b.Parcel, Is.SameAs(first.Parcel));
            Assert.That(b.DockSlot, Is.EqualTo(0));
            Assert.That(s.CanUndo, Is.False);
        }

        [Test]
        public void UndoSkipsParcelsThatLeftForATruck()
        {
            // Hold green, then blue; loading the red brings the blue truck, which takes the held blue.
            // Undo must return the green, the most recent parcel still in the dock.
            var s = TestLevels.Session("RBYG", 5, 1, "GBRY");
            var green = s.Tap(new Cell(0, 0)).OfType<ParcelHeld>().Single();
            s.Tap(new Cell(1, 0));
            var chain = s.Tap(new Cell(2, 0));
            Assert.That(chain.OfType<ParcelLoadedFromDock>().Single().Parcel.Cell, Is.EqualTo(new Cell(1, 0)));

            var returned = s.Undo().OfType<ParcelReturned>().Single();
            Assert.That(returned.Parcel, Is.SameAs(green.Parcel));
            Assert.That(returned.DockSlot, Is.EqualTo(0));
            Assert.That(s.CanUndo, Is.False);
            Assert.That(s.Undo(), Is.Empty);
        }

        [Test]
        public void UndoFromAFullDockKeepsPlaying()
        {
            var s = TestLevels.Session("RBG", 2, 1, "BGR");
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            Assert.That(s.Dock.IsFull, Is.True);

            var events = s.Undo();
            Assert.That(events.Single(), Is.InstanceOf<ParcelReturned>());
            Assert.That(s.Status, Is.EqualTo(GameStatus.Playing));
            Assert.That(s.Dock.IsFull, Is.False);
        }

        [Test]
        public void UndoIsOffOnceTheLevelHasEnded()
        {
            var s = TestLevels.Session("RBBBGG", 2, 1, "BGB", "GBR");
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            Assert.That(s.Status, Is.EqualTo(GameStatus.Failed));
            Assert.That(s.CanUndo, Is.False);
            Assert.That(s.Undo(), Is.Empty);
        }

        // ---- Extra Slot -------------------------------------------------------------------------

        [Test]
        public void ExtraSlotGrowsTheDockAndKeepsSlotIndexes()
        {
            var s = TestLevels.Session("RBBG", 2, 3, "BBGR", "GGRR", "BBBB");
            var a = s.Tap(new Cell(0, 0)).OfType<ParcelHeld>().Single();
            var b = s.Tap(new Cell(1, 0)).OfType<ParcelHeld>().Single();
            Assert.That(s.Dock.IsFull, Is.True);

            var expanded = (DockExpanded)s.AddHoldingSlot().Single();
            Assert.That(expanded.Capacity, Is.EqualTo(3));
            Assert.That(s.Dock.Capacity, Is.EqualTo(3));
            Assert.That(s.Dock[0], Is.SameAs(a.Parcel));
            Assert.That(s.Dock[1], Is.SameAs(b.Parcel));

            var c = s.Tap(new Cell(2, 0)).OfType<ParcelHeld>().Single();
            Assert.That(c.DockSlot, Is.EqualTo(2));
        }

        [Test]
        public void ExtraSlotWhileFailedResumesPlay()
        {
            var s = TestLevels.Session("RBBBGG", 2, 1, "BGB", "GBR");
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            Assert.That(s.Status, Is.EqualTo(GameStatus.Failed));
            Assert.That(s.CanAddHoldingSlot, Is.True);

            var events = s.AddHoldingSlot();
            Assert.That(events.Single(), Is.InstanceOf<DockExpanded>());
            Assert.That(s.Status, Is.EqualTo(GameStatus.Playing));

            // The new slot takes the blue on top of the red, and the red is then free to load.
            Assert.That(s.Tap(new Cell(2, 0)).OfType<ParcelHeld>().Single().DockSlot, Is.EqualTo(2));
            Assert.That(s.Tap(new Cell(2, 1)).OfType<ParcelLoaded>(), Is.Not.Empty);
        }

        [Test]
        public void ExtraSlotStopsAtTheMaximum()
        {
            var s = TestLevels.Session("R", GameSession.MaxHoldingSlots - 1, 3, "RRR");
            Assert.That(s.CanAddHoldingSlot, Is.True);
            s.AddHoldingSlot();
            Assert.That(s.Dock.Capacity, Is.EqualTo(GameSession.MaxHoldingSlots));
            Assert.That(s.CanAddHoldingSlot, Is.False);
            Assert.That(s.AddHoldingSlot(), Is.Empty);
        }

        [Test]
        public void ExtraSlotIsOffAfterAWin()
        {
            var s = TestLevels.Session("R", 5, 3, "RRR");
            foreach (var x in new[] { 0, 1, 2 }) s.Tap(new Cell(x, 0));
            Assert.That(s.Status, Is.EqualTo(GameStatus.Won));
            Assert.That(s.CanAddHoldingSlot, Is.False);
        }

        [Test]
        public void StateKeyTracksDockCapacity()
        {
            var s = TestLevels.Session("RB", 5, 3, "BRR", "BBR");
            string before = s.StateKey();
            s.AddHoldingSlot();
            Assert.That(s.StateKey(), Is.Not.EqualTo(before));
        }

        // ---- Crane ------------------------------------------------------------------------------

        [Test]
        public void CraneLiftsABlockedParcelIntoTheTruck()
        {
            var s = TestLevels.Session("RB", 5, 3, "BBB", "RRR");
            var cell = new Cell(1, 1);
            Assert.That(s.Tap(cell).Single(), Is.InstanceOf<ParcelBlocked>());
            Assert.That(s.CanCrane(cell), Is.True);

            var loaded = (ParcelLoaded)s.CraneLift(cell).Single();
            Assert.That(loaded.Lifted, Is.True);
            Assert.That(loaded.Path, Is.EqualTo(new List<Cell> { cell }));
            Assert.That(s.Board.Get(cell), Is.Null);
            Assert.That(s.CurrentTruck.Loaded, Is.EqualTo(1));
        }

        [Test]
        public void CraneSendsANonMatchingParcelToTheDock()
        {
            var s = TestLevels.Session("RB", 5, 3, "RRR", "BBB");
            var held = (ParcelHeld)s.CraneLift(new Cell(1, 1)).Single();
            Assert.That(held.Lifted, Is.True);
            Assert.That(held.DockSlot, Is.EqualTo(0));
            Assert.That(held.Path, Is.EqualTo(new List<Cell> { new Cell(1, 1) }));
            Assert.That(s.CanUndo, Is.True, "a lifted parcel is held like any other");
        }

        [Test]
        public void CraneFollowsTheFullDockRule()
        {
            var s = TestLevels.Session("RBGB", 2, 1, "BGRB");
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            Assert.That(s.Dock.IsFull, Is.True);

            Assert.That(s.CanCrane(new Cell(3, 0)), Is.False, "blue doesn't match and the dock is full");
            Assert.That(s.CraneLift(new Cell(3, 0)), Is.Empty);
            Assert.That(s.CanCrane(new Cell(2, 0)), Is.True);
            Assert.That(s.CanCrane(new Cell(0, 0)), Is.False, "nothing there");
        }

        [Test]
        public void CraneRunsChainsLikeATap()
        {
            // Two held blues, and the red truck's parcel is buried: lifting it sends three trucks off.
            var s = TestLevels.Session("RBBBB", 5, 1, "BBB", "RB.");
            s.Tap(new Cell(1, 0));
            s.Tap(new Cell(2, 0));
            Assert.That(s.Board.IsFree(new Cell(0, 1)), Is.False);

            var events = s.CraneLift(new Cell(0, 1));
            Assert.That(events.First(), Is.InstanceOf<ParcelLoaded>());
            Assert.That(events.OfType<ParcelLoadedFromDock>().Count(), Is.EqualTo(2));
            Assert.That(events.OfType<TruckDeparted>().Count(), Is.EqualTo(3));
            Assert.That(s.Status, Is.EqualTo(GameStatus.Playing));

            s.Tap(new Cell(0, 0));
            Assert.That(s.Tap(new Cell(1, 1)).Last(), Is.InstanceOf<LevelWon>());
        }

        [Test]
        public void CraneCanFailTheLevel()
        {
            // Lifting the buried blue fills the last slot while the only red is still blocked.
            var s = TestLevels.Session("RBBBGG", 2, 1, "BGB", "GBR");
            s.Tap(new Cell(0, 0));
            var events = s.CraneLift(new Cell(1, 1));
            Assert.That(events.Select(e => e.GetType()), Is.EqualTo(new[] { typeof(ParcelHeld), typeof(LevelFailed) }));
            Assert.That(s.Status, Is.EqualTo(GameStatus.Failed));
        }

        [Test]
        public void CraneCanWinTheLevel()
        {
            var s = TestLevels.Session("R", 5, 3, "RRR");
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            var events = s.CraneLift(new Cell(2, 0));
            Assert.That(events.First(), Is.InstanceOf<ParcelLoaded>());
            Assert.That(events.Last(), Is.InstanceOf<LevelWon>());
        }

        [Test]
        public void CraneIsOffWhenTheLevelIsOver()
        {
            var s = TestLevels.Session("RBBBGG", 2, 1, "BGB", "GBR");
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            Assert.That(s.Status, Is.EqualTo(GameStatus.Failed));
            Assert.That(s.CanCrane(new Cell(2, 1)), Is.False);
            Assert.That(s.CraneLift(new Cell(2, 1)), Is.Empty);
        }
    }
}
