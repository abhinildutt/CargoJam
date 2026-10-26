using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CargoJam.Core.Tests
{
    public class LevelDefinitionTests
    {
        const string LevelsFolder = "Assets/Resources/Levels";

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
        public void EveryLevelFileIsValidAndSolvable()
        {
            var files = Directory.GetFiles(LevelsFolder, "*.json").OrderBy(f => f).ToArray();
            Assert.That(files, Is.Not.Empty, "no level files found");

            foreach (var file in files)
            {
                var data = JsonUtility.FromJson<LevelData>(File.ReadAllText(file));
                var errors = LevelDefinition.Validate(data, out var level);
                Assert.That(errors, Is.Empty, $"{Path.GetFileName(file)}: {string.Join("; ", errors)}");

                var result = Solver.Solve(level);
                Assert.That(result.Solvable, Is.True,
                    $"{Path.GetFileName(file)} is not solvable (nodes {result.NodesVisited}, hit limit {result.HitNodeLimit})");
            }
        }
    }
}
