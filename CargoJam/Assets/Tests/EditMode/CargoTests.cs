using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CargoJam.Core.Tests
{
    /// <summary>Design v4 cargo rules: frozen crates, express parcels, strapped pairs and customs gates.</summary>
    public class CargoTests
    {
        static Parcel At(GameSession s, int x, int y) => s.Board.Get(new Cell(x, y));

        static Type[] Types(IReadOnlyList<GameEvent> events) => events.Select(e => e.GetType()).ToArray();

        static Type[] Seq(params Type[] types) => types;

        // ---- Frost ------------------------------------------------------------------------------

        /// <summary>Three reds in row 0; the middle one is frozen.</summary>
        static GameSession FrozenMiddle(int layers)
        {
            var data = TestLevels.Data("R", 5, 3, "RRR");
            data.frost = new[] { TestLevels.Frost(1, 0, layers) };
            return TestLevels.Session(data);
        }

        [Test]
        public void TapOnAFrozenCrateIsBlockedAndChangesNothing()
        {
            var s = FrozenMiddle(1);
            var crate = At(s, 1, 0);
            Assert.That(s.IsRestrained(crate), Is.True);
            Assert.That(s.FrostLeft(crate), Is.EqualTo(1));
            Assert.That(s.Board.IsFree(crate.Cell), Is.True, "it has a path; the ice holds it");

            var moves = new List<Parcel>();
            s.GetMoves(moves);
            Assert.That(moves, Has.No.Member(crate));
            Assert.That(moves.Count, Is.EqualTo(2));

            string before = s.StateKey();
            var events = s.Tap(crate.Cell);
            Assert.That(((ParcelBlocked)events.Single()).Parcel, Is.SameAs(crate));
            Assert.That(s.StateKey(), Is.EqualTo(before));
        }

        [Test]
        public void LoadingANeighbourCracksTheIce()
        {
            var s = FrozenMiddle(1);
            var crate = At(s, 1, 0);
            var by = At(s, 0, 0);

            var events = s.Tap(by.Cell);
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(FrostCracked))));
            var crack = (FrostCracked)events[1];
            Assert.That(crack.Parcel, Is.SameAs(crate));
            Assert.That(crack.LayersLeft, Is.Zero);
            Assert.That(crack.By, Is.SameAs(by));
            Assert.That(s.IsRestrained(crate), Is.False);
            Assert.That(s.Tap(crate.Cell).OfType<ParcelLoaded>(), Is.Not.Empty);
        }

        [Test]
        public void HoldingANeighbourCracksTheIce()
        {
            var data = TestLevels.Data("BR", 5, 3, "RRR", "BBB");
            data.frost = new[] { TestLevels.Frost(1, 0, 1) };
            var s = TestLevels.Session(data);
            var events = s.Tap(new Cell(0, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelHeld), typeof(FrostCracked))));
            Assert.That(s.FrostLeft(At(s, 1, 0)), Is.Zero);
        }

        [Test]
        public void TwoLayersNeedTwoNeighboursToLeave()
        {
            var s = FrozenMiddle(2);
            var crate = At(s, 1, 0);

            Assert.That(((FrostCracked)s.Tap(new Cell(0, 0))[1]).LayersLeft, Is.EqualTo(1));
            Assert.That(s.Tap(crate.Cell).Single(), Is.InstanceOf<ParcelBlocked>());

            var events = s.Tap(new Cell(2, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(FrostCracked))));
            Assert.That(((FrostCracked)events[1]).LayersLeft, Is.Zero);

            Assert.That(Types(s.Tap(crate.Cell)),
                Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(TruckDeparted), typeof(LevelWon))));
        }

        [Test]
        public void CraneThawsAFrozenCrateOutright()
        {
            var s = FrozenMiddle(2);
            var crate = At(s, 1, 0);
            Assert.That(s.CanCrane(crate.Cell), Is.True);

            var events = s.CraneLift(crate.Cell);
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(FrostCracked), typeof(ParcelLoaded))));
            var thaw = (FrostCracked)events[0];
            Assert.That(thaw.LayersLeft, Is.Zero);
            Assert.That(thaw.By, Is.Null);
            Assert.That(((ParcelLoaded)events[1]).Lifted, Is.True);
        }

        [Test]
        public void UndoNeverRefreezes()
        {
            var data = TestLevels.Data("BR", 5, 3, "RRR", "BBB");
            data.frost = new[] { TestLevels.Frost(1, 0, 2) };
            var s = TestLevels.Session(data);
            var crate = At(s, 1, 0);

            Assert.That(Types(s.CraneLift(crate.Cell)), Is.EqualTo(Seq(typeof(FrostCracked), typeof(ParcelHeld))));
            Assert.That(Types(s.Undo()), Is.EqualTo(Seq(typeof(ParcelReturned))));
            Assert.That(s.Board.Get(crate.Cell), Is.SameAs(crate));
            Assert.That(s.FrostLeft(crate), Is.Zero);
            Assert.That(s.IsRestrained(crate), Is.False);
        }

        [Test]
        public void OnlyFrozenFreeParcelsIsStuck()
        {
            // Holding the blue fills the only slot and cracks one of two layers; the frozen red is all
            // that is free, and more room wouldn't thaw it.
            var data = TestLevels.Data("RB", 1, 2,
                "#B#",
                "RRB");
            data.frost = new[] { TestLevels.Frost(1, 1, 2) };
            var s = TestLevels.Session(data);

            var events = s.Tap(new Cell(1, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelHeld), typeof(FrostCracked), typeof(LevelFailed))));
            Assert.That(((LevelFailed)events[2]).Reason, Is.EqualTo(FailReason.Stuck));
            Assert.That(s.FailReason, Is.EqualTo(FailReason.Stuck));
            Assert.That(s.CanAddHoldingSlot, Is.False);
        }

        [Test]
        public void FrozenMysteryIsRevealedOnceItHasAPath()
        {
            var data = TestLevels.Data("BR", 5, 2,
                "#B.",
                "RrB");
            data.frost = new[] { TestLevels.Frost(1, 1, 2) };
            var s = TestLevels.Session(data);
            var crate = At(s, 1, 1);
            Assert.That(s.IsHidden(crate), Is.True);

            var events = s.Tap(new Cell(1, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(FrostCracked), typeof(ParcelRevealed))));
            Assert.That(((ParcelRevealed)events[2]).Parcel, Is.SameAs(crate));
            Assert.That(s.IsHidden(crate), Is.False);
            Assert.That(s.FrostLeft(crate), Is.EqualTo(1));
            Assert.That(s.IsRestrained(crate), Is.True);
        }

        [Test]
        public void CloneAndStateKeyTrackFrost()
        {
            var s = FrozenMiddle(2);
            var crate = At(s, 1, 0);
            var copy = s.Clone();
            s.Tap(new Cell(0, 0));

            Assert.That(copy.FrostLeft(crate), Is.EqualTo(2));
            Assert.That(s.FrostLeft(crate), Is.EqualTo(1));
            Assert.That(copy.StateKey(), Does.EndWith("|F2"));
            Assert.That(s.StateKey(), Does.EndWith("|F1"));
        }

        // ---- Express ----------------------------------------------------------------------------

        [Test]
        public void ExpressTicksOnLoadsHoldsAndCraneLifts()
        {
            var data = TestLevels.Data("RB", 5, 3, "RRBR", "BB..");
            data.express = new[] { TestLevels.Express(1, 1, 9) };
            var s = TestLevels.Session(data);
            var express = At(s, 1, 1);
            Assert.That(s.IsExpress(express), Is.True);
            Assert.That(s.ExpressLeft(express), Is.EqualTo(9));
            Assert.That(s.IsExpress(At(s, 0, 0)), Is.False);
            Assert.That(s.ExpressLeft(At(s, 0, 0)), Is.Zero);

            Assert.That(Types(s.Tap(new Cell(0, 0))), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(ExpressTicked))));
            var held = s.Tap(new Cell(2, 0));
            Assert.That(Types(held), Is.EqualTo(Seq(typeof(ParcelHeld), typeof(ExpressTicked))));
            var tick = (ExpressTicked)held[1];
            Assert.That(tick.Parcel, Is.SameAs(express));
            Assert.That(tick.Left, Is.EqualTo(7));
            Assert.That(Types(s.CraneLift(new Cell(3, 0))), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(ExpressTicked))));
            Assert.That(s.ExpressLeft(express), Is.EqualTo(6));
        }

        /// <summary>Red then blue trucks of 2, one slot; both lower parcels are express with 9 moves.</summary>
        static GameSession TwoExpress()
        {
            var data = TestLevels.Data("RB", 1, 2,
                "BR",
                "BR");
            data.express = new[] { TestLevels.Express(0, 1, 9), TestLevels.Express(1, 1, 9) };
            return TestLevels.Session(data);
        }

        [Test]
        public void BlockedRejectedUndoAndExtraSlotDontTick()
        {
            var s = TwoExpress();
            var blue = At(s, 0, 1);
            var red = At(s, 1, 1);

            Assert.That(Types(s.Tap(red.Cell)), Is.EqualTo(Seq(typeof(ParcelBlocked))));
            Assert.That(Types(s.Tap(new Cell(0, 0))),
                Is.EqualTo(Seq(typeof(ParcelHeld), typeof(ExpressTicked), typeof(ExpressTicked))));
            Assert.That(Types(s.Undo()), Is.EqualTo(Seq(typeof(ParcelReturned))));
            Assert.That(s.ExpressLeft(blue), Is.EqualTo(8));
            Assert.That(s.ExpressLeft(red), Is.EqualTo(8));

            s.Tap(new Cell(0, 0));
            string before = s.StateKey();
            Assert.That(Types(s.Tap(blue.Cell)), Is.EqualTo(Seq(typeof(DockFullRejected))));
            Assert.That(s.StateKey(), Is.EqualTo(before));
            Assert.That(Types(s.AddHoldingSlot()), Is.EqualTo(Seq(typeof(DockExpanded))));
            Assert.That(s.ExpressLeft(blue), Is.EqualTo(7));
            Assert.That(s.ExpressLeft(red), Is.EqualTo(7));
        }

        [Test]
        public void OwnDepartureAndDockChainsDontTick()
        {
            var s = TwoExpress();
            var blue = At(s, 0, 1);
            var red = At(s, 1, 1);
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            Assert.That(s.ExpressLeft(red), Is.EqualTo(7));

            // The express red ships and fills the truck; the blue truck chains the held blue. One tick, for
            // the express blue still in the yard.
            var events = s.Tap(red.Cell);
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(TruckDeparted), typeof(TruckArrived),
                typeof(ParcelLoadedFromDock), typeof(ExpressTicked))));
            Assert.That(((ExpressTicked)events[4]).Parcel, Is.SameAs(blue));
            Assert.That(s.ExpressLeft(red), Is.EqualTo(7), "safe once out of the yard");
            Assert.That(s.ExpressLeft(blue), Is.EqualTo(6));

            Assert.That(Types(s.Tap(blue.Cell)), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(TruckDeparted), typeof(LevelWon))));
        }

        [Test]
        public void ExpressMissFailsAfterTicksAndReveals()
        {
            var data = TestLevels.Data("RB", 5, 2,
                "RR",
                "bB");
            data.express = new[] { TestLevels.Express(1, 1, 1) };
            var s = TestLevels.Session(data);
            var express = At(s, 1, 1);

            var events = s.Tap(new Cell(0, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(ExpressTicked), typeof(ParcelRevealed),
                typeof(ExpressMissed), typeof(LevelFailed))));
            Assert.That(((ExpressMissed)events[3]).Parcel, Is.SameAs(express));
            Assert.That(((LevelFailed)events[4]).Reason, Is.EqualTo(FailReason.ExpressMissed));
            Assert.That(s.FailReason, Is.EqualTo(FailReason.ExpressMissed));
            Assert.That(s.ExpressLeft(express), Is.Zero);
            Assert.That(s.IsExpress(express), Is.True, "a missed express parcel still reads as express");

            // No Continue after an express miss.
            Assert.That(s.CanAddHoldingSlot, Is.False);
            Assert.That(s.AddHoldingSlot(), Is.Empty);
            Assert.That(s.Status, Is.EqualTo(GameStatus.Failed));
        }

        [Test]
        public void ShippingTheExpressOnItsLastMoveWins()
        {
            var data = TestLevels.Data("R", 5, 3, "RRR");
            data.express = new[] { TestLevels.Express(2, 0, 3) };
            var s = TestLevels.Session(data);
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            Assert.That(s.ExpressLeft(At(s, 2, 0)), Is.EqualTo(1));

            var events = s.Tap(new Cell(2, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(TruckDeparted), typeof(LevelWon))));
            Assert.That(s.FailReason, Is.EqualTo(FailReason.None));
        }

        [Test]
        public void UndoKeepsTheExpressCountAndItResumesTicking()
        {
            var data = TestLevels.Data("RB", 5, 2,
                "RB",
                "RB");
            data.express = new[] { TestLevels.Express(1, 0, 9) };
            var s = TestLevels.Session(data);
            var express = At(s, 1, 0);

            Assert.That(Types(s.Tap(express.Cell)), Is.EqualTo(Seq(typeof(ParcelHeld))), "its own move doesn't tick it");
            Assert.That(s.StateKey(), Does.EndWith("|E-,"));
            Assert.That(Types(s.Tap(new Cell(0, 0))), Is.EqualTo(Seq(typeof(ParcelLoaded))), "held: no tick");

            Assert.That(Types(s.Undo()), Is.EqualTo(Seq(typeof(ParcelReturned))));
            Assert.That(s.ExpressLeft(express), Is.EqualTo(9));
            Assert.That(s.StateKey(), Does.EndWith("|E9,"));

            var events = s.Tap(new Cell(0, 1));
            Assert.That(events.Last(), Is.InstanceOf<ExpressTicked>());
            Assert.That(s.ExpressLeft(express), Is.EqualTo(8));
        }

        [Test]
        public void SolverShipsAnExpressParcelEarly()
        {
            // Loading the three reds first would miss the express blue under them.
            var data = TestLevels.Data("RB", 5, 3,
                "RRR",
                "BBB");
            data.express = new[] { TestLevels.Express(0, 1, 2) };
            var level = LevelDefinition.Parse(data);

            var greedy = new GameSession(level);
            greedy.Tap(new Cell(0, 0));
            Assert.That(greedy.Tap(new Cell(1, 0)).OfType<ExpressMissed>(), Is.Not.Empty);

            var result = Solver.Solve(level);
            Assert.That(result.Solvable, Is.True);
            var session = new GameSession(level);
            foreach (var move in result.Moves) session.Tap(move);
            Assert.That(session.Status, Is.EqualTo(GameStatus.Won));

            data.express = new[] { TestLevels.Express(1, 1, 1) };
            var impossible = Solver.Solve(LevelDefinition.Parse(data));
            Assert.That(impossible.Solvable, Is.False);
            Assert.That(impossible.HitNodeLimit, Is.False);
        }

        // ---- Straps -----------------------------------------------------------------------------

        /// <summary>A vertical red pair at (0,0)-(0,1) next to two blues; trucks of 2.</summary>
        static GameSession RedPair(string trucks, int holdingSlots = 5)
        {
            var data = TestLevels.Data(trucks, holdingSlots, 2,
                "RB",
                "RB");
            data.straps = new[] { TestLevels.Strap(0, 0, 0, 1) };
            return TestLevels.Session(data);
        }

        [Test]
        public void TappingTheBlockedMemberLetsTheFreeOneLead()
        {
            var s = RedPair("RB");
            var top = At(s, 0, 0);
            var bottom = At(s, 0, 1);
            Assert.That(s.StrapPartner(top), Is.SameAs(bottom));
            Assert.That(s.StrapIndex(bottom), Is.Zero);
            Assert.That(s.Board.IsFree(bottom.Cell), Is.False);
            Assert.That(s.IsUnitFree(bottom), Is.True);

            // The truck has room for exactly two: both load, then it leaves.
            var events = s.Tap(bottom.Cell);
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(StrapMoved), typeof(ParcelLoaded), typeof(ParcelLoaded),
                typeof(TruckDeparted), typeof(TruckArrived))));
            var moved = (StrapMoved)events[0];
            Assert.That(moved.Lead, Is.SameAs(top));
            Assert.That(moved.Trail, Is.SameAs(bottom));
            var lead = (ParcelLoaded)events[1];
            var trail = (ParcelLoaded)events[2];
            Assert.That(lead.Parcel, Is.SameAs(top));
            Assert.That(lead.Path, Is.EqualTo(new[] { new Cell(0, 0) }));
            Assert.That(trail.Parcel, Is.SameAs(bottom));
            Assert.That(trail.Path, Is.EqualTo(new[] { new Cell(0, 1), new Cell(0, 0) }), "its cell, then the lead's path");
            Assert.That(s.StrapPartner(top), Is.Null);
        }

        [Test]
        public void RoomForOneSendsTheTrailToTheDockAfterTheChain()
        {
            var data = TestLevels.Data("RBR", 5, 2,
                "BRRB",
                ".RR.");
            data.straps = new[] { TestLevels.Strap(2, 0, 2, 1) };
            var s = TestLevels.Session(data);
            var lead = At(s, 2, 0);
            var trail = At(s, 2, 1);
            s.Tap(new Cell(0, 0));
            s.Tap(new Cell(1, 0));
            Assert.That(s.CanTake(lead), Is.True, "room 1 plus a free slot");

            var events = s.Tap(lead.Cell);
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(StrapMoved), typeof(ParcelLoaded), typeof(TruckDeparted),
                typeof(TruckArrived), typeof(ParcelLoadedFromDock), typeof(ParcelHeld))));
            Assert.That(((ParcelLoaded)events[1]).Parcel, Is.SameAs(lead));
            Assert.That(((ParcelHeld)events[5]).Parcel, Is.SameAs(trail));

            // The lead is in a truck, so Undo brings back the trail alone and the strap stays cut.
            Assert.That(Types(s.Undo()), Is.EqualTo(Seq(typeof(ParcelReturned))));
            Assert.That(s.Board.Get(trail.Cell), Is.SameAs(trail));
            Assert.That(s.StrapPartner(trail), Is.Null);
            Assert.That(s.StateKey(), Does.EndWith("|S0"));
        }

        [Test]
        public void RoomForOneSendsTheTrailToTheNextTruckWhenItMatches()
        {
            var data = TestLevels.Data("RRB", 5, 2,
                "BRRB",
                ".RR.");
            data.straps = new[] { TestLevels.Strap(2, 0, 2, 1) };
            var s = TestLevels.Session(data);
            s.Tap(new Cell(1, 0));

            var events = s.Tap(new Cell(2, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(StrapMoved), typeof(ParcelLoaded), typeof(TruckDeparted),
                typeof(TruckArrived), typeof(ParcelLoaded))));
            Assert.That(((ParcelLoaded)events[4]).Truck.Index, Is.EqualTo(1));
            Assert.That(s.Dock.Count, Is.Zero);
        }

        [Test]
        public void MismatchedPairTakesTwoSlotsTicksOnceAndUndoesTogether()
        {
            var data = TestLevels.Data("BR", 5, 2,
                "RB",
                "RB");
            data.straps = new[] { TestLevels.Strap(0, 0, 0, 1) };
            data.express = new[] { TestLevels.Express(1, 1, 9) };
            var s = TestLevels.Session(data);
            var top = At(s, 0, 0);
            var bottom = At(s, 0, 1);
            var copy = s.Clone();

            var events = s.Tap(top.Cell);
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(StrapMoved), typeof(ParcelHeld), typeof(ParcelHeld),
                typeof(ExpressTicked))));
            Assert.That(((ParcelHeld)events[1]).DockSlot, Is.EqualTo(0));
            Assert.That(((ParcelHeld)events[2]).DockSlot, Is.EqualTo(1));
            Assert.That(s.StateKey(), Does.EndWith("|S0"));
            Assert.That(copy.StrapPartner(top), Is.SameAs(bottom), "the clone keeps its strap");
            Assert.That(copy.StateKey(), Does.EndWith("|S1"));

            var undo = s.Undo();
            Assert.That(Types(undo), Is.EqualTo(Seq(typeof(ParcelReturned), typeof(ParcelReturned), typeof(StrapRestored))));
            Assert.That(((ParcelReturned)undo[0]).Parcel, Is.SameAs(bottom));
            Assert.That(((ParcelReturned)undo[1]).Parcel, Is.SameAs(top));
            Assert.That(s.StrapPartner(top), Is.SameAs(bottom));
            Assert.That(s.StateKey(), Does.EndWith("|S1"));
            Assert.That(s.ExpressLeft(At(s, 1, 1)), Is.EqualTo(8), "no refund");
            Assert.That(s.CanUndo, Is.False);
        }

        [Test]
        public void PairNeedingTwoSlotsWithOneFreeIsRejected()
        {
            var data = TestLevels.Data("BR", 1, 2,
                "RBB",
                "R..");
            data.straps = new[] { TestLevels.Strap(0, 0, 0, 1) };
            var s = TestLevels.Session(data);
            var top = At(s, 0, 0);
            Assert.That(s.CanTake(top), Is.False);
            Assert.That(s.CanTake(top, 1), Is.True);
            Assert.That(s.CanTake(At(s, 1, 0)), Is.True);

            string before = s.StateKey();
            Assert.That(((DockFullRejected)s.Tap(top.Cell).Single()).Parcel, Is.SameAs(top));
            Assert.That(s.StateKey(), Is.EqualTo(before));
            Assert.That(s.Status, Is.EqualTo(GameStatus.Playing));
        }

        [Test]
        public void PairShortOfOneSlotIsOutOfSpaceAndExtraSlotSavesIt()
        {
            var data = TestLevels.Data("BGR", 2, 2,
                "GR#",
                "#RB",
                "G.B");
            data.straps = new[] { TestLevels.Strap(1, 0, 1, 1) };
            var s = TestLevels.Session(data);

            var events = s.Tap(new Cell(0, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelHeld), typeof(LevelFailed))));
            Assert.That(s.FailReason, Is.EqualTo(FailReason.OutOfSpace));
            Assert.That(s.CanAddHoldingSlot, Is.True);

            Assert.That(Types(s.AddHoldingSlot()), Is.EqualTo(Seq(typeof(DockExpanded))));
            Assert.That(s.Status, Is.EqualTo(GameStatus.Playing));
            Assert.That(s.FailReason, Is.EqualTo(FailReason.None));
            Assert.That(Types(s.Tap(new Cell(1, 1))), Is.EqualTo(Seq(typeof(StrapMoved), typeof(ParcelHeld), typeof(ParcelHeld))));

            s.Tap(new Cell(2, 1));
            s.Tap(new Cell(2, 2));
            Assert.That(s.Tap(new Cell(0, 2)).Last(), Is.InstanceOf<LevelWon>());
        }

        [Test]
        public void GetMovesListsAPairOnce()
        {
            var data = TestLevels.Data("R", 5, 2, "RR");
            data.straps = new[] { TestLevels.Strap(0, 0, 1, 0) };
            var s = TestLevels.Session(data);
            var moves = new List<Parcel>();
            s.GetMoves(moves);
            Assert.That(moves, Is.EqualTo(new[] { At(s, 0, 0) }));
            Assert.That(s.HasLegalMove(), Is.True);
            Assert.That(s.HasFreeMatch(), Is.True);

            var blocked = RedPair("RB");
            blocked.GetMoves(moves);
            Assert.That(moves.Select(p => p.Cell), Is.EquivalentTo(new[] { new Cell(0, 0), new Cell(1, 0) }));
        }

        [Test]
        public void CraneCutsTheStrapAndLiftsOnlyOne()
        {
            var s = RedPair("RB");
            var top = At(s, 0, 0);
            var bottom = At(s, 0, 1);

            var events = s.CraneLift(bottom.Cell);
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(StrapCut), typeof(ParcelLoaded))));
            var cut = (StrapCut)events[0];
            Assert.That(cut.Lifted, Is.SameAs(bottom));
            Assert.That(cut.Partner, Is.SameAs(top));
            Assert.That(s.StrapPartner(top), Is.Null);
            Assert.That(s.Board.Get(top.Cell), Is.SameAs(top));

            Assert.That(Types(s.Tap(top.Cell)), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(TruckDeparted), typeof(TruckArrived))));
        }

        [Test]
        public void EachPairMemberCracksItsOwnNeighbours()
        {
            var data = TestLevels.Data("RB", 5, 2,
                "RR",
                "BB");
            data.straps = new[] { TestLevels.Strap(0, 0, 1, 0) };
            data.frost = new[] { TestLevels.Frost(0, 1, 1), TestLevels.Frost(1, 1, 1) };
            var s = TestLevels.Session(data);
            var lead = At(s, 0, 0);
            var trail = At(s, 1, 0);

            var events = s.Tap(lead.Cell);
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(StrapMoved), typeof(ParcelLoaded), typeof(FrostCracked),
                typeof(ParcelLoaded), typeof(FrostCracked), typeof(TruckDeparted), typeof(TruckArrived))));
            Assert.That(((FrostCracked)events[2]).By, Is.SameAs(lead));
            Assert.That(((FrostCracked)events[2]).Parcel.Cell, Is.EqualTo(new Cell(0, 1)));
            Assert.That(((FrostCracked)events[4]).By, Is.SameAs(trail));
            Assert.That(((FrostCracked)events[4]).Parcel.Cell, Is.EqualTo(new Cell(1, 1)));
        }

        [Test]
        public void SolverSolvesStrapLevels()
        {
            var data = TestLevels.Data("BGR", 3, 2,
                "GR#",
                "#RB",
                "G.B");
            data.straps = new[] { TestLevels.Strap(1, 0, 1, 1) };
            var level = LevelDefinition.Parse(data);
            var result = Solver.Solve(level);
            Assert.That(result.Solvable, Is.True);
            var s = new GameSession(level);
            foreach (var move in result.Moves) s.Tap(move);
            Assert.That(s.Status, Is.EqualTo(GameStatus.Won));
        }

        // ---- Gates ------------------------------------------------------------------------------

        [Test]
        public void LoadsCountGatesDownIncludingDockChainsButHoldsDont()
        {
            var data = TestLevels.Data("BR", 5, 2, "RBBR", "+...");
            data.gates = new[] { TestLevels.Gate(0, 1, 'R', 2) };
            var s = TestLevels.Session(data);
            var gateCell = new Cell(0, 1);
            var firstRed = At(s, 0, 0);
            Assert.That(s.Board.IsObstacle(gateCell), Is.True);
            Assert.That(s.GateLeft(0), Is.EqualTo(2));
            Assert.That(s.IsGateOpen(0), Is.False);

            Assert.That(Types(s.Tap(firstRed.Cell)), Is.EqualTo(Seq(typeof(ParcelHeld))));
            Assert.That(s.GateLeft(0), Is.EqualTo(2));
            s.Tap(new Cell(1, 0));

            var chain = s.Tap(new Cell(2, 0));
            Assert.That(Types(chain), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(TruckDeparted), typeof(TruckArrived),
                typeof(ParcelLoadedFromDock), typeof(GateProgress))));
            var progress = (GateProgress)chain[4];
            Assert.That(progress.Gate, Is.Zero);
            Assert.That(progress.Left, Is.EqualTo(1));
            Assert.That(progress.Loaded, Is.SameAs(firstRed));

            var last = s.Tap(new Cell(3, 0));
            Assert.That(Types(last), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(GateProgress), typeof(GateOpened),
                typeof(TruckDeparted), typeof(LevelWon))));
            Assert.That(((GateOpened)last[2]).Gate, Is.Zero);
            Assert.That(s.IsGateOpen(0), Is.True);
            Assert.That(s.Board.IsObstacle(gateCell), Is.False);
        }

        static GameSession GateInRowZero()
        {
            // The gate walls in a mystery blue; row 0's gate is no way out while closed.
            var data = TestLevels.Data("RB", 5, 2,
                "R+R",
                "#b#",
                "#B#");
            data.gates = new[] { TestLevels.Gate(1, 0, 'R', 2) };
            return TestLevels.Session(data);
        }

        [Test]
        public void OpeningAGateFreesItsCellAndTheRevealSeesIt()
        {
            var s = GateInRowZero();
            var hidden = At(s, 1, 1);
            Assert.That(s.IsHidden(hidden), Is.True);
            Assert.That(s.Board.FindExitPath(hidden.Cell), Is.Null);
            Assert.That(s.StateKey(), Does.StartWith("R#R#B##B#|"));

            Assert.That(Types(s.Tap(new Cell(0, 0))), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(GateProgress))));
            var events = s.Tap(new Cell(2, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(GateProgress), typeof(GateOpened),
                typeof(TruckDeparted), typeof(TruckArrived), typeof(ParcelRevealed))));
            Assert.That(s.IsHidden(hidden), Is.False);
            Assert.That(s.Board.FindExitPath(hidden.Cell), Is.EqualTo(new[] { new Cell(1, 1), new Cell(1, 0) }));
            Assert.That(s.StateKey(), Does.StartWith("...#B##B#|"));
        }

        [Test]
        public void TwoGatesCountInIndexOrder()
        {
            var data = TestLevels.Data("R", 5, 2,
                "RR",
                "+.",
                "+.");
            data.gates = new[] { TestLevels.Gate(0, 1, 'R', 1), TestLevels.Gate(0, 2, 'R', 2) };
            var s = TestLevels.Session(data);
            var events = s.Tap(new Cell(0, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(GateProgress), typeof(GateOpened),
                typeof(GateProgress))));
            Assert.That(((GateProgress)events[1]).Gate, Is.Zero);
            Assert.That(((GateProgress)events[3]).Gate, Is.EqualTo(1));
            Assert.That(((GateProgress)events[3]).Left, Is.EqualTo(1));
        }

        [Test]
        public void EventsComeInTheDesignOrder()
        {
            // One load cracks a neighbour's ice, opens a padlock, opens a gate, then ticks an express.
            var data = TestLevels.Data("RB", 5, 3,
                "RRR+",
                "BBB#");
            data.locks = new[] { TestLevels.Lock(2, 0, 0, 0) };
            data.frost = new[] { TestLevels.Frost(0, 1, 1) };
            data.express = new[] { TestLevels.Express(2, 1, 9) };
            data.gates = new[] { TestLevels.Gate(3, 0, 'R', 1) };
            var s = TestLevels.Session(data);

            Assert.That(Types(s.Tap(new Cell(0, 0))), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(FrostCracked),
                typeof(ParcelUnlocked), typeof(GateProgress), typeof(GateOpened), typeof(ExpressTicked))));
        }

        [Test]
        public void CraneLoadsCountAndGatesNeverClose()
        {
            var data = TestLevels.Data("RB", 5, 2,
                "RBR",
                "B+.");
            data.gates = new[] { TestLevels.Gate(1, 1, 'R', 1) };
            var s = TestLevels.Session(data);
            s.Tap(new Cell(1, 0));

            var events = s.CraneLift(new Cell(0, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelLoaded), typeof(GateProgress), typeof(GateOpened))));
            Assert.That(Types(s.Undo()), Is.EqualTo(Seq(typeof(ParcelReturned))));
            Assert.That(s.IsGateOpen(0), Is.True);
            Assert.That(s.GateLeft(0), Is.Zero);
            Assert.That(s.Board.IsObstacle(new Cell(1, 1)), Is.False);
        }

        [Test]
        public void BollardsAndGatesCantBeCraned()
        {
            var data = TestLevels.Data("R", 5, 3, "R#R", "+R.");
            data.gates = new[] { TestLevels.Gate(0, 1, 'R', 1) };
            var s = TestLevels.Session(data);
            foreach (var cell in new[] { new Cell(1, 0), new Cell(0, 1) })
            {
                Assert.That(s.CanCrane(cell), Is.False);
                Assert.That(s.CraneLift(cell), Is.Empty);
                Assert.That(s.Tap(cell), Is.Empty);
            }
        }

        [Test]
        public void AGateThatCantOpenIsAStuckDeadEnd()
        {
            // The second red is behind its own gate, which needs both reds.
            var data = TestLevels.Data("RB", 5, 2,
                "R+BB",
                "#R##");
            data.gates = new[] { TestLevels.Gate(1, 0, 'R', 2) };
            var s = TestLevels.Session(data);
            s.Tap(new Cell(0, 0));
            Assert.That(Types(s.Tap(new Cell(2, 0))), Is.EqualTo(Seq(typeof(ParcelHeld))));

            var events = s.Tap(new Cell(3, 0));
            Assert.That(Types(events), Is.EqualTo(Seq(typeof(ParcelHeld), typeof(LevelFailed))));
            Assert.That(s.FailReason, Is.EqualTo(FailReason.Stuck));
            Assert.That(s.CanAddHoldingSlot, Is.False);
            Assert.That(Solver.Solve(s.Level).Solvable, Is.False);
        }

        [Test]
        public void CloneAndStateKeyTrackGates()
        {
            var s = GateInRowZero();
            s.Tap(new Cell(0, 0));
            var copy = s.Clone();
            s.Tap(new Cell(2, 0));

            Assert.That(copy.IsGateOpen(0), Is.False);
            Assert.That(copy.GateLeft(0), Is.EqualTo(1));
            Assert.That(copy.Board.IsObstacle(new Cell(1, 0)), Is.True);
            Assert.That(copy.StateKey(), Does.EndWith("|G1,"));
            Assert.That(s.IsGateOpen(0), Is.True);
            Assert.That(s.StateKey(), Does.EndWith("|G0,"));
        }
    }
}
