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
