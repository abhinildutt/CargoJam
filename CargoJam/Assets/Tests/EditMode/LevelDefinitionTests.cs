using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CargoJam.Core.Tests
{
    public class LevelDefinitionTests
    {
        [Test]
        public void ValidLevelParses()
        {
            var level = TestLevels.Make("RB", 5, 3, "RB.", "RB.", "RB.");
            Assert.That(level.Width, Is.EqualTo(3));
            Assert.That(level.Height, Is.EqualTo(3));
            Assert.That(level.ColorAt(2, 0), Is.EqualTo(ParcelColors.None));
            Assert.That(level.ColorAt(1, 0), Is.EqualTo(ParcelColors.FromCode('B')));
        }

        [Test]
        public void ColorCountsMustMatchTrucks()
        {
            var errors = LevelDefinition.Validate(
                new LevelData { trucks = "RB", rows = new[] { "RRRB" } }, out var level);
            Assert.That(level, Is.Null);
            Assert.That(errors, Has.Some.Contains("color B"));
        }

        [Test]
        public void RaggedRowsAreRejected()
        {
            var errors = LevelDefinition.Validate(
                new LevelData { trucks = "R", rows = new[] { "RR", "R" } }, out _);
            Assert.That(errors, Has.Some.Contains("row 1"));
        }

        [Test]
        public void UnknownColorsAreRejected()
        {
            var errors = LevelDefinition.Validate(
                new LevelData { trucks = "RX", rows = new[] { "RRZ" } }, out _);
            Assert.That(errors, Has.Some.Contains("'Z'"));
            Assert.That(errors, Has.Some.Contains("'X'"));
        }

        [Test]
        public void HardFlagMysteryCellsAndLocksAreExposed()
        {
            var data = TestLevels.Data("RB", 5, 3, "RBb", "rRB");
            data.hard = true;
            data.locks = new[] { TestLevels.Lock(0, 0, 1, 1) };
            var level = LevelDefinition.Parse(data);

            Assert.That(level.Hard, Is.True);
            Assert.That(level.IsMystery(2, 0), Is.True);
            Assert.That(level.IsMystery(0, 1), Is.True);
            Assert.That(level.IsMystery(1, 0), Is.False);
            Assert.That(level.ColorAt(2, 0), Is.EqualTo(ParcelColors.FromCode('B')), "a mystery keeps its real color");
            Assert.That(level.Locks.Single().Locked, Is.EqualTo(new Cell(0, 0)));
            Assert.That(level.Locks.Single().Key, Is.EqualTo(new Cell(1, 1)));
        }

        [Test]
        public void MissingLocksMeanNoLocks()
        {
            var level = TestLevels.Make("R", 5, 3, "RRR");
            Assert.That(level.Hard, Is.False);
            Assert.That(level.Locks, Is.Empty);
        }

        static List<string> LockErrors(params LockData[] locks)
        {
            var data = TestLevels.Data("RB", 5, 3, "RRB.", "RBB.");
            data.locks = locks;
            return LevelDefinition.Validate(data, out _);
        }

        [Test]
        public void LockCellsMustHoldParcels()
        {
            Assert.That(LockErrors(TestLevels.Lock(3, 0, 0, 0)), Has.Some.Contains("no parcel at locked cell (3,0)"));
            Assert.That(LockErrors(TestLevels.Lock(0, 0, 3, 1)), Has.Some.Contains("no parcel at key cell (3,1)"));
            Assert.That(LockErrors(TestLevels.Lock(0, 0, 9, 9)), Has.Some.Contains("no parcel at key cell (9,9)"));
            Assert.That(LockErrors(TestLevels.Lock(-1, 0, 0, 0)), Has.Some.Contains("locked cell (-1,0)"));
        }

        [Test]
        public void ParcelCantBeItsOwnKey()
        {
            Assert.That(LockErrors(TestLevels.Lock(1, 1, 1, 1)), Has.Some.Contains("its own key"));
        }

        [Test]
        public void ParcelHasAtMostOneLock()
        {
            var errors = LockErrors(TestLevels.Lock(0, 0, 1, 0), TestLevels.Lock(0, 0, 2, 0));
            Assert.That(errors, Has.Some.Contains("already has a lock"));
        }

        [Test]
        public void LockCyclesAreRejected()
        {
            Assert.That(LockErrors(TestLevels.Lock(0, 0, 1, 0), TestLevels.Lock(1, 0, 0, 0)),
                Has.Some.Contains("cycle"));
            Assert.That(LockErrors(TestLevels.Lock(0, 0, 1, 0), TestLevels.Lock(1, 0, 2, 0), TestLevels.Lock(2, 0, 0, 0)),
                Has.Some.Contains("cycle"));
        }

        [Test]
        public void LockChainsAndSharedKeysAreFine()
        {
            Assert.That(LockErrors(TestLevels.Lock(0, 0, 1, 0), TestLevels.Lock(1, 0, 2, 0)), Is.Empty);
            Assert.That(LockErrors(TestLevels.Lock(0, 0, 2, 0), TestLevels.Lock(1, 0, 2, 0)), Is.Empty);
        }

        [Test]
        public void LocksCantSitOnBollardsOrGates()
        {
            var data = TestLevels.Data("R", 5, 3, "RRR", "#+.");
            data.gates = new[] { TestLevels.Gate(1, 1, 'R', 1) };
            data.locks = new[] { TestLevels.Lock(0, 1, 0, 0), TestLevels.Lock(0, 0, 1, 1) };
            var errors = LevelDefinition.Validate(data, out _);
            Assert.That(errors, Has.Some.Contains("no parcel at locked cell (0,1)"));
            Assert.That(errors, Has.Some.Contains("no parcel at key cell (1,1)"));
        }

        // ---- Bollards and gates -----------------------------------------------------------------

        [Test]
        public void BollardsAndGatesParseAndHoldNoParcels()
        {
            // Three reds fill the red truck; '#' and '+' don't count toward it.
            var data = TestLevels.Data("R", 5, 3, "R#R", "+R.");
            data.gates = new[] { TestLevels.Gate(0, 1, 'R', 2) };
            var level = LevelDefinition.Parse(data);

            Assert.That(level.IsBollard(1, 0), Is.True);
            Assert.That(level.IsBollard(0, 0), Is.False);
            Assert.That(level.IsGateCell(0, 1), Is.True);
            Assert.That(level.ColorAt(1, 0), Is.EqualTo(ParcelColors.None));
            Assert.That(level.ColorAt(0, 1), Is.EqualTo(ParcelColors.None));
            var gate = level.Gates.Single();
            Assert.That(gate.Cell, Is.EqualTo(new Cell(0, 1)));
            Assert.That(gate.Color, Is.EqualTo(ParcelColors.FromCode('R')));
            Assert.That(gate.Count, Is.EqualTo(2));
            Assert.That(level.Uses(CargoKind.Bollard) && level.Uses(CargoKind.Gate), Is.True);
            Assert.That(level.CargoMask, Is.EqualTo((1 << (int)CargoKind.Bollard) | (1 << (int)CargoKind.Gate)));
        }

        [Test]
        public void ALevelNeedsAParcel()
        {
            var errors = LevelDefinition.Validate(TestLevels.Data("", 5, 3, "#.."), out _);
            Assert.That(errors, Has.Some.Contains("trucks are missing"));

            var data = TestLevels.Data("R", 5, 3, "#..");
            data.trucks = "R";
            errors = LevelDefinition.Validate(data, out var level);
            Assert.That(level, Is.Null);
            Assert.That(errors, Has.Some.Contains("no parcels"));
        }

        static List<string> GateErrors(string row1, params GateData[] gates)
        {
            var data = TestLevels.Data("RB", 5, 3, "RRB.", "RBB.", row1);
            data.gates = gates;
            return LevelDefinition.Validate(data, out _);
        }

        [Test]
        public void EveryGateCellHasExactlyOneEntry()
        {
            Assert.That(GateErrors("+..."), Has.Some.Contains("gate at (0,2) has no entry"));
            Assert.That(GateErrors("....", TestLevels.Gate(0, 2, 'R', 1)), Has.Some.Contains("no '+' at (0,2)"));
            Assert.That(GateErrors("+...", TestLevels.Gate(0, 0, 'R', 1)), Has.Some.Contains("no '+' at (0,0)"));
            Assert.That(GateErrors("+...", TestLevels.Gate(9, 9, 'R', 1)), Has.Some.Contains("no '+' at (9,9)"));
            Assert.That(GateErrors("+...", TestLevels.Gate(0, 2, 'R', 1), TestLevels.Gate(0, 2, 'B', 1)),
                Has.Some.Contains("already has a gate"));
            Assert.That(GateErrors("+..+", TestLevels.Gate(0, 2, 'R', 1), TestLevels.Gate(3, 2, 'B', 3)), Is.Empty);
        }

        [Test]
        public void GateColorAndCountMustWork()
        {
            Assert.That(GateErrors("+...", TestLevels.Gate(0, 2, 'X', 1)), Has.Some.Contains("unknown color 'X'"));
            Assert.That(GateErrors("+...", new GateData { x = 0, y = 2, color = null, count = 1 }), Has.Some.Contains("unknown color"));
            Assert.That(GateErrors("+...", TestLevels.Gate(0, 2, 'G', 1)), Has.Some.Contains("no G truck"));
            Assert.That(GateErrors("+...", TestLevels.Gate(0, 2, 'R', 0)), Has.Some.Contains("count 0"));
            Assert.That(GateErrors("+...", TestLevels.Gate(0, 2, 'R', 4)), Has.Some.Contains("count 4"));
            Assert.That(GateErrors("+...", TestLevels.Gate(0, 2, 'R', 3)), Is.Empty);
        }

        [Test]
        public void AtMostThreeGates()
        {
            Assert.That(GateErrors("+++.", TestLevels.Gate(0, 2, 'R', 1), TestLevels.Gate(1, 2, 'R', 1),
                TestLevels.Gate(2, 2, 'B', 1)), Is.Empty);
            Assert.That(GateErrors("++++", TestLevels.Gate(0, 2, 'R', 1), TestLevels.Gate(1, 2, 'R', 1),
                TestLevels.Gate(2, 2, 'B', 1), TestLevels.Gate(3, 2, 'B', 1)), Has.Some.Contains("at most 3"));
        }

        // ---- Frost, express, straps -------------------------------------------------------------
        // The board below: (0,0) has 2 parcel neighbours, (1,1) has 3, and (3,0) is empty.

        static LevelData Cargo() => TestLevels.Data("RB", 5, 3, "RRB.", "RBB.");

        static List<string> Errors(LevelData data) => LevelDefinition.Validate(data, out _);

        [Test]
        public void FrostNeedsAParcelOncePerCell()
        {
            var data = Cargo();
            data.frost = new[] { TestLevels.Frost(3, 0, 1) };
            Assert.That(Errors(data), Has.Some.Contains("frost 0: no parcel at (3,0)"));
            data.frost = new[] { TestLevels.Frost(0, 0, 1), TestLevels.Frost(0, 0, 1) };
            Assert.That(Errors(data), Has.Some.Contains("already frozen"));
            data.frost = new FrostData[] { null };
            Assert.That(Errors(data), Has.Some.Contains("frost 0 is empty"));
        }

        [Test]
        public void FrostThawsAfterOneToNineMovesAnywhere()
        {
            var data = Cargo();
            data.frost = new[] { TestLevels.Frost(1, 1, 0) };
            Assert.That(Errors(data), Has.Some.Contains("layers 0"));
            data.frost = new[] { TestLevels.Frost(1, 1, 10) };
            Assert.That(Errors(data), Has.Some.Contains("layers 10"));

            // The count is moves, not neighbours: a corner crate may wait out more moves than it has neighbours.
            data.frost = new[] { TestLevels.Frost(1, 1, 3), TestLevels.Frost(0, 0, 9) };
            var level = LevelDefinition.Parse(data);
            Assert.That(level.FrostAt(1, 1), Is.EqualTo(3));
            Assert.That(level.FrostAt(0, 0), Is.EqualTo(9));
            Assert.That(level.FrostAt(1, 0), Is.Zero);
            Assert.That(level.CargoMask, Is.EqualTo(1 << (int)CargoKind.Frost));
        }

        [Test]
        public void ExpressNeedsAParcelAndOneToNinetyNineTaps()
        {
            var data = Cargo();
            data.express = new[] { TestLevels.Express(3, 0, 5) };
            Assert.That(Errors(data), Has.Some.Contains("express 0: no parcel at (3,0)"));
            data.express = new[] { TestLevels.Express(0, 0, 0) };
            Assert.That(Errors(data), Has.Some.Contains("taps 0"));
            data.express = new[] { TestLevels.Express(0, 0, 100) };
            Assert.That(Errors(data), Has.Some.Contains("taps 100"));
            data.express = new[] { TestLevels.Express(0, 0, 5), TestLevels.Express(0, 0, 6) };
            Assert.That(Errors(data), Has.Some.Contains("already express"));

            data.express = new[] { TestLevels.Express(0, 0, 99), TestLevels.Express(1, 1, 1) };
            var level = LevelDefinition.Parse(data);
            Assert.That(level.ExpressAt(0, 0), Is.EqualTo(99));
            Assert.That(level.ExpressAt(1, 1), Is.EqualTo(1));
            Assert.That(level.ExpressAt(1, 0), Is.Zero);
            Assert.That(level.Uses(CargoKind.Express), Is.True);
        }

        [Test]
        public void ExpressParcelsArePlainOtherwise()
        {
            var mystery = TestLevels.Data("RB", 5, 3, "rRB.", "RBB.");
            mystery.express = new[] { TestLevels.Express(0, 0, 5) };
            Assert.That(Errors(mystery), Has.Some.Contains("express parcel at (0,0) is a mystery parcel"));

            var data = Cargo();
            data.express = new[] { TestLevels.Express(0, 0, 5) };
            data.locks = new[] { TestLevels.Lock(0, 0, 1, 0) };
            Assert.That(Errors(data), Has.Some.Contains("express parcel at (0,0) is padlocked or a key"));
            data.locks = new[] { TestLevels.Lock(1, 0, 0, 0) };
            Assert.That(Errors(data), Has.Some.Contains("express parcel at (0,0) is padlocked or a key"));

            data.locks = null;
            data.frost = new[] { TestLevels.Frost(0, 0, 1) };
            Assert.That(Errors(data), Has.Some.Contains("express parcel at (0,0) is frozen"));

            data.frost = null;
            data.straps = new[] { TestLevels.Strap(0, 0, 1, 0) };
            Assert.That(Errors(data), Has.Some.Contains("express parcel at (0,0) is strapped"));
        }

        [Test]
        public void StrapsJoinTwoAdjacentParcelsOfOneColor()
        {
            var data = Cargo();
            data.straps = new[] { TestLevels.Strap(0, 0, 3, 0) };
            Assert.That(Errors(data), Has.Some.Contains("strap 0: no parcel at (3,0)"));
            data.straps = new[] { TestLevels.Strap(0, 0, 1, 1) };
            Assert.That(Errors(data), Has.Some.Contains("not orthogonally adjacent"));
            data.straps = new[] { TestLevels.Strap(0, 0, 0, 0) };
            Assert.That(Errors(data), Has.Some.Contains("not orthogonally adjacent"));
            data.straps = new[] { TestLevels.Strap(1, 0, 2, 0) };
            Assert.That(Errors(data), Has.Some.Contains("different colors"));
            data.straps = new[] { TestLevels.Strap(0, 0, 1, 0), TestLevels.Strap(0, 0, 0, 1) };
            Assert.That(Errors(data), Has.Some.Contains("already strapped"));

            data.straps = new[] { TestLevels.Strap(0, 0, 1, 0), TestLevels.Strap(2, 1, 1, 1) };
            var level = LevelDefinition.Parse(data);
            Assert.That(level.Straps.Count, Is.EqualTo(2));
            Assert.That(level.Straps[1].A, Is.EqualTo(new Cell(2, 1)));
            Assert.That(level.Straps[1].B, Is.EqualTo(new Cell(1, 1)));
            Assert.That(level.Uses(CargoKind.Strap), Is.True);
        }

        [Test]
        public void StrappedParcelsArePlainOtherwise()
        {
            var mystery = TestLevels.Data("RB", 5, 3, "rRB.", "RBB.");
            mystery.straps = new[] { TestLevels.Strap(0, 0, 1, 0) };
            Assert.That(Errors(mystery), Has.Some.Contains("strapped parcel at (0,0) is a mystery parcel"));

            var data = Cargo();
            data.straps = new[] { TestLevels.Strap(0, 0, 1, 0) };
            data.locks = new[] { TestLevels.Lock(1, 0, 2, 0) };
            Assert.That(Errors(data), Has.Some.Contains("strapped parcel at (1,0) is padlocked or a key"));
            data.locks = new[] { TestLevels.Lock(2, 0, 1, 0) };
            Assert.That(Errors(data), Has.Some.Contains("strapped parcel at (1,0) is padlocked or a key"));

            data.locks = null;
            data.frost = new[] { TestLevels.Frost(0, 0, 1) };
            Assert.That(Errors(data), Has.Some.Contains("strapped parcel at (0,0) is frozen"));

            data.frost = null;
            data.express = new[] { TestLevels.Express(1, 0, 4) };
            Assert.That(Errors(data), Has.Some.Contains("express parcel at (1,0) is strapped"));
        }

        [Test]
        public void StrapsNeedTrucksThatHoldTwo()
        {
            var data = TestLevels.Data("RRRBBB", 5, 1, "RRB.", "RBB.");
            data.straps = new[] { TestLevels.Strap(0, 0, 1, 0) };
            Assert.That(Errors(data), Has.Some.Contains("truckCapacity of at least 2"));
        }

        [Test]
        public void MissingOrEmptyCargoArraysMeanNone()
        {
            var data = Cargo();
            data.frost = new FrostData[0];
            data.express = new ExpressData[0];
            data.straps = new StrapData[0];
            data.gates = new GateData[0];
            var level = LevelDefinition.Parse(data);
            Assert.That(level.Straps, Is.Empty);
            Assert.That(level.Gates, Is.Empty);
            Assert.That(level.CargoMask, Is.Zero);
            Assert.That(TestLevels.Make("RB", 5, 3, "RRB.", "RBB.").CargoMask, Is.Zero, "null arrays");
        }

        [Test]
        public void CargoMaskHasABitPerKindUsed()
        {
            var data = TestLevels.Data("RB", 5, 3, "RRB#", "rBB.", "+...");
            data.gates = new[] { TestLevels.Gate(0, 2, 'B', 1) };
            data.locks = new[] { TestLevels.Lock(2, 1, 2, 0) };
            data.frost = new[] { TestLevels.Frost(1, 1, 1) };
            data.express = new[] { TestLevels.Express(1, 0, 9) };
            data.straps = new[] { TestLevels.Strap(0, 0, 0, 1) };
            Assert.That(Errors(data), Has.Some.Contains("strapped parcel at (0,1) is a mystery parcel"), "sanity: mystery can't be strapped");

            data.straps = null;
            var level = LevelDefinition.Parse(data);
            foreach (CargoKind kind in System.Enum.GetValues(typeof(CargoKind)))
                Assert.That(level.Uses(kind), Is.EqualTo(kind != CargoKind.Strap), kind.ToString());
        }
    }
}
