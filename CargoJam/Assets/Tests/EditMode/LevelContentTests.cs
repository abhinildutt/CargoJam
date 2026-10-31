using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CargoJam.Core.Tests
{
    /// <summary>Checks over the shipped level files in Resources/Levels.</summary>
    public class LevelContentTests
    {
        const string LevelsFolder = "Assets/Resources/Levels";

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

        /// <summary>
        /// Each destination ends on a storm finale (design v3), which is meant to be a HARD level. The
        /// files line up with the journey only while the level count and the controller's loop start
        /// (20) are multiples of Journey.LevelsPerStop, so check both here.
        /// </summary>
        [Test]
        public void EveryStormFinaleIsAHardLevel()
        {
            var files = Directory.GetFiles(LevelsFolder, "*.json").OrderBy(f => f).ToArray();
            Assert.That(files.Length % CargoJam.Meta.Journey.LevelsPerStop, Is.Zero,
                "level count must be a multiple of Journey.LevelsPerStop so looping keeps finales on HARD levels");

            for (int i = 0; i < files.Length; i++)
            {
                if (CargoJam.Meta.Journey.MoodFor(i) != CargoJam.Meta.Mood.Storm) continue;
                var data = JsonUtility.FromJson<LevelData>(File.ReadAllText(files[i]));
                Assert.That(data.hard, Is.True, $"{Path.GetFileName(files[i])} is a storm finale but not HARD");
            }
        }
    }
}
