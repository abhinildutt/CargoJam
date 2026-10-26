using System;
using System.Linq;
using NUnit.Framework;

namespace CargoJam.Core.Tests
{
    public class BlockerTests
    {
        static Parcel At(GameSession s, int x, int y) => s.Board.Get(new Cell(x, y));

        static Type[] Types(System.Collections.Generic.IReadOnlyList<GameEvent> events) =>
            events.Select(e => e.GetType()).ToArray();

        // ---- Mystery ----------------------------------------------------------------------------

        [Test]
        public void MysteryParcelIsHiddenUntilItBecomesFree()
        {
            var s = TestLevels.Session("RB", 5, 3, "BRR", "rBB");
            var mystery = At(s, 0, 1);
            Assert.That(mystery.Color, Is.EqualTo(ParcelColors.FromCode('R')), "the rules know the real color");
            Assert.That(s.IsHidden(mystery), Is.True);

            // Loading the red above-right frees (1,1) but not the mystery.
            Assert.That(s.Tap(new Cell(1, 0)).OfType<ParcelRevealed>(), Is.Empty);
            Assert.That(s.IsHidden(mystery), Is.True);

            var events = s.Tap(new Cell(0, 0));
            Assert.That(Types(events), Is.EqualTo(new[] { typeof(ParcelHeld), typeof(ParcelRevealed) }));
            Assert.That(((ParcelRevealed)events[1]).Parcel, Is.SameAs(mystery));
            Assert.That(s.IsHidden(mystery), Is.False);
        }

        [Test]
        public void RevealComesBeforeTheFail()
        {
            var s = TestLevels.Session("RBBBGG", 2, 1, "BGB", "GbR");
            s.Tap(new Cell(0, 0));
            var events = s.Tap(new Cell(1, 0));
            Assert.That(Types(events), Is.EqualTo(new[] { typeof(ParcelHeld), typeof(ParcelRevealed), typeof(LevelFailed) }));
        }

        [Test]
        public void RevealComesAfterTheChain()
        {
            // The red load sends the red truck off and the blue truck pulls the held blue; only then
            // does the reveal of the mystery under the red follow.
            var s = TestLevels.Session("RBBB", 5, 1, "BR", "Bb");
            s.Tap(new Cell(0, 0));
            var events = s.Tap(new Cell(1, 0));
            Assert.That(Types(events), Is.EqualTo(new[]
            {
                typeof(ParcelLoaded), typeof(TruckDeparted), typeof(TruckArrived),
                typeof(ParcelLoadedFromDock), typeof(TruckDeparted), typeof(TruckArrived),
                typeof(ParcelRevealed),
            }));
        }

        [Test]
        public void MysteryThatStartsFreeIsRevealedSilently()
        {
            var s = TestLevels.Session("R", 5, 3, "rRR");
            Assert.That(s.IsHidden(At(s, 0, 0)), Is.False);
            Assert.That(s.Tap(new Cell(1, 0)).OfType<ParcelRevealed>(), Is.Empty);
        }

        [Test]
        public void OnceRevealedAlwaysRevealed()
        {
            var s = TestLevels.Session("RB", 5, 3, "BRR", "rBB");
            var mystery = At(s, 0, 1);
            s.Tap(new Cell(0, 0));
            Assert.That(s.IsHidden(mystery), Is.False);

            // Undo puts the blue back on top, so the mystery is blocked again but stays revealed.
            Assert.That(s.Undo().OfType<ParcelRevealed>(), Is.Empty);
            Assert.That(s.Board.IsFree(mystery.Cell), Is.False);
            Assert.That(s.IsHidden(mystery), Is.False);
            Assert.That(s.Tap(new Cell(0, 0)).OfType<ParcelRevealed>(), Is.Empty);
        }

        [Test]
        public void CraneRevealsAHiddenParcelFirst()
        {
            var s = TestLevels.Session("RB", 5, 3, "BRR", "rBB");
            var mystery = At(s, 0, 1);
            var events = s.CraneLift(mystery.Cell);
            Assert.That(Types(events), Is.EqualTo(new[] { typeof(ParcelRevealed), typeof(ParcelLoaded) }));
            Assert.That(((ParcelLoaded)events[1]).Lifted, Is.True);
            Assert.That(s.IsHidden(mystery), Is.False);
        }

        [Test]
        public void CloneKeepsBlockerState()
        {
            var s = TestLevels.Session(new[] { TestLevels.Lock(0, 0, 1, 0) }, "RB", 5, 3, "BRRB", "rB..");
            var copy = s.Clone();
            s.Tap(new Cell(1, 0)); // unlocks (0,0)
            s.CraneLift(new Cell(0, 1));
            Assert.That(copy.IsLocked(At(copy, 0, 0)), Is.True);
            Assert.That(copy.IsHidden(At(copy, 0, 1)), Is.True);
        }

        // ---- Locks ------------------------------------------------------------------------------

        [Test]
        public void LockedParcelCantBeTappedEvenWhenFree()
        {
            var s = TestLevels.Session(new[] { TestLevels.Lock(0, 0, 1, 0) }, "RB", 5, 3, "BRRR", "BB..");
            var locked = At(s, 0, 0);
            Assert.That(s.IsLocked(locked), Is.True);
            Assert.That(s.IsKey(At(s, 1, 0)), Is.True);
            Assert.That(s.IsKey(At(s, 2, 0)), Is.False);

            string before = s.StateKey();
            var events = s.Tap(locked.Cell);
            Assert.That(((ParcelBlocked)events.Single()).Parcel, Is.SameAs(locked));
            Assert.That(s.StateKey(), Is.EqualTo(before));
        }

        [Test]
        public void LoadingTheKeyUnlocksRightAfterTheLoad()
        {
            var s = TestLevels.Session(new[] { TestLevels.Lock(0, 0, 1, 0) }, "RB", 5, 3, "BRRR", "BB..");
            var locked = At(s, 0, 0);
            var key = At(s, 1, 0);

            var events = s.Tap(key.Cell);
            Assert.That(Types(events), Is.EqualTo(new[] { typeof(ParcelLoaded), typeof(ParcelUnlocked) }));
            Assert.That(((ParcelUnlocked)events[1]).Parcel, Is.SameAs(locked));
            Assert.That(s.IsLocked(locked), Is.False);
            Assert.That(s.IsKey(key), Is.False);
            Assert.That(s.Tap(locked.Cell).OfType<ParcelHeld>(), Is.Not.Empty);
        }

        [Test]
        public void UnlockFollowsTheKeyLoadEvenWhenTheTruckLeaves()
        {
            var s = TestLevels.Session(new[] { TestLevels.Lock(0, 0, 3, 0) }, "RB", 5, 3, "BRRR", "BB..");
            s.Tap(new Cell(1, 0));
            s.Tap(new Cell(2, 0));
            var events = s.Tap(new Cell(3, 0));
            Assert.That(Types(events).Take(3), Is.EqualTo(new[] { typeof(ParcelLoaded), typeof(ParcelUnlocked), typeof(TruckDeparted) }));
        }

        [Test]
        public void HoldingTheKeyDoesNotUnlockLoadingItFromTheDockDoes()
        {
            // The blue key waits in the dock for the blue truck; the locked blue opens when it boards.
            var s = TestLevels.Session(new[] { TestLevels.Lock(2, 1, 0, 0) }, "RB", 5, 3, "BRRR", "B.B.");
            var locked = At(s, 2, 1);
            var key = At(s, 0, 0);

            Assert.That(s.Tap(key.Cell).OfType<ParcelUnlocked>(), Is.Empty);
            Assert.That(s.IsLocked(locked), Is.True);
            Assert.That(s.IsKey(key), Is.True, "still the key while held");

            s.Tap(new Cell(1, 0));
            s.Tap(new Cell(2, 0));
            var events = s.Tap(new Cell(3, 0)).ToList();
            int fromDock = events.FindIndex(e => e is ParcelLoadedFromDock d && d.Parcel == key);
            Assert.That(fromDock, Is.GreaterThanOrEqualTo(0));
            Assert.That(events[fromDock + 1], Is.InstanceOf<ParcelUnlocked>());
            Assert.That(s.IsLocked(locked), Is.False);

            s.Tap(new Cell(0, 1));
            Assert.That(s.Tap(locked.Cell).Last(), Is.InstanceOf<LevelWon>());
        }

        [Test]
        public void ALockedMatchDoesNotSaveAFullDock()
        {
            var s = TestLevels.Session(new[] { TestLevels.Lock(0, 0, 2, 0) }, "RBG", 1, 1, "RBG");
            var events = s.Tap(new Cell(1, 0));
            Assert.That(Types(events), Is.EqualTo(new[] { typeof(ParcelHeld), typeof(LevelFailed) }));
            Assert.That(s.HasFreeMatch(), Is.False);

            var unlocked = TestLevels.Session("RBG", 1, 1, "RBG");
            unlocked.Tap(new Cell(1, 0));
            Assert.That(unlocked.Status, Is.EqualTo(GameStatus.Playing), "control: without the lock the red saves it");
        }

        [Test]
        public void NothingTappableWithRoomInTheDockIsAFail()
        {
            // The blue key is held waiting for the blue truck, but the red truck needs the locked red.
            var s = TestLevels.Session(new[] { TestLevels.Lock(0, 0, 1, 0) }, "RB", 5, 1, "RB");
            var events = s.Tap(new Cell(1, 0));
            Assert.That(Types(events), Is.EqualTo(new[] { typeof(ParcelHeld), typeof(LevelFailed) }));
            Assert.That(s.Status, Is.EqualTo(GameStatus.Failed));
        }

        [Test]
        public void CraneOnALockedParcelUsesUpTheLock()
        {
            var s = TestLevels.Session(new[] { TestLevels.Lock(1, 1, 0, 0) }, "RB", 5, 3, "BRRR", "BB..");
            var locked = At(s, 1, 1);
            var key = At(s, 0, 0);
            Assert.That(s.CanCrane(locked.Cell), Is.True);

            var events = s.CraneLift(locked.Cell);
            Assert.That(Types(events), Is.EqualTo(new[] { typeof(ParcelUnlocked), typeof(ParcelHeld) }));
            Assert.That(((ParcelHeld)events[1]).Lifted, Is.True);
            Assert.That(s.IsLocked(locked), Is.False);
            Assert.That(s.IsKey(key), Is.False, "nothing left for the key to open");
        }

        [Test]
        public void ChainedLocksOpenInOrder()
        {
            // (0,0) waits for (1,0), which waits for (2,0).
            var locks = new[] { TestLevels.Lock(0, 0, 1, 0), TestLevels.Lock(1, 0, 2, 0) };
            var s = TestLevels.Session(locks, "R", 5, 3, "RRR");
            Assert.That(s.Tap(new Cell(1, 0)).Single(), Is.InstanceOf<ParcelBlocked>());
            Assert.That(s.IsKey(At(s, 1, 0)), Is.True, "a locked parcel can still be a key");

            Assert.That(s.Tap(new Cell(2, 0)).OfType<ParcelUnlocked>().Single().Parcel.Cell, Is.EqualTo(new Cell(1, 0)));
            Assert.That(s.Tap(new Cell(1, 0)).OfType<ParcelUnlocked>().Single().Parcel.Cell, Is.EqualTo(new Cell(0, 0)));
            Assert.That(s.Tap(new Cell(0, 0)).Last(), Is.InstanceOf<LevelWon>());
        }
    }
}
