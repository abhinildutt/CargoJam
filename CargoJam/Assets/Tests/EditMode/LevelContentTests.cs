using System.Collections.Generic;
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
        const int LevelCount = 100;

        /// <summary>The level each cargo kind first appears on (design v4 §4), indexed by <see cref="CargoKind"/>.</summary>
        static readonly int[] DebutLevel = { 5, 7, 12, 17, 22, 27, 42 };

        static string[] Files() => Directory.GetFiles(LevelsFolder, "*.json").OrderBy(f => f).ToArray();

        static LevelData Read(string file) => JsonUtility.FromJson<LevelData>(File.ReadAllText(file));

        [Test]
        public void ThereAreAHundredLevels()
        {
            var files = Files();
            Assert.That(files.Length, Is.EqualTo(LevelCount));
            Assert.That(files.Length % CargoJam.Meta.Journey.LevelsPerStop, Is.Zero);
            for (int i = 0; i < files.Length; i++)
                Assert.That(Read(files[i]).id, Is.EqualTo(i + 1), $"{Path.GetFileName(files[i])} has the wrong id");
        }

        [Test]
        public void EveryLevelFileIsValidAndSolvable()
        {
            var files = Files();
            Assert.That(files, Is.Not.Empty, "no level files found");

            foreach (var file in files)
            {
                var data = Read(file);
                var errors = LevelDefinition.Validate(data, out var level);
                Assert.That(errors, Is.Empty, $"{Path.GetFileName(file)}: {string.Join("; ", errors)}");

                // Validation alone allows a level that opens with nothing to tap (every free parcel frozen, say).
                Assert.That(new GameSession(level).HasLegalMove(), Is.True, $"{Path.GetFileName(file)} starts with no legal move");

                var result = Solver.Solve(level);
                Assert.That(result.Solvable, Is.True,
                    $"{Path.GetFileName(file)} is not solvable (nodes {result.NodesVisited}, hit limit {result.HitNodeLimit})");
            }
        }

        /// <summary>
        /// Each destination ends on a storm finale (design v3), which is meant to be a HARD level. The
        /// files line up with the journey only while the level count and the controller's loop start are
        /// multiples of Journey.LevelsPerStop, so the count is checked here too.
        /// </summary>
        [Test]
        public void EveryStormFinaleIsAHardLevel()
        {
            var files = Files();
            Assert.That(files.Length % CargoJam.Meta.Journey.LevelsPerStop, Is.Zero,
                "level count must be a multiple of Journey.LevelsPerStop so looping keeps finales on HARD levels");

            for (int i = 0; i < files.Length; i++)
            {
                if (CargoJam.Meta.Journey.MoodFor(i) != CargoJam.Meta.Mood.Storm) continue;
                Assert.That(Read(files[i]).hard, Is.True, $"{Path.GetFileName(files[i])} is a storm finale but not HARD");
            }
        }

        [Test]
        public void EachCargoKindFirstAppearsOnItsDebutLevel()
        {
            var files = Files();
            var first = new Dictionary<CargoKind, int>();
            foreach (var file in files)
            {
                var data = Read(file);
                var level = LevelDefinition.Parse(data);
                for (int k = 0; k < DebutLevel.Length; k++)
                {
                    var kind = (CargoKind)k;
                    if (level.Uses(kind) && !first.ContainsKey(kind)) first[kind] = data.id;
                }
            }

            for (int k = 0; k < DebutLevel.Length; k++)
            {
                var kind = (CargoKind)k;
                Assert.That(first.ContainsKey(kind), Is.True, $"no level uses {kind}");
                Assert.That(first[kind], Is.EqualTo(DebutLevel[k]), $"{kind} first appears on level {first[kind]}");
            }
        }

        [Test]
        public void DebutLevelsAreGentle()
        {
            var files = Files();
            for (int k = 0; k < DebutLevel.Length; k++)
            {
                int id = DebutLevel[k];
                var data = Read(files[id - 1]);
                var level = LevelDefinition.Parse(data);
                string name = $"level {id} ({(CargoKind)k} debut)";

                Assert.That(data.hard, Is.False, $"{name} is HARD");
                Assert.That(data.holdingSlots, Is.EqualTo(5), $"{name} must have 5 slots");
                Assert.That(id % CargoJam.Meta.Journey.LevelsPerStop, Is.Not.EqualTo(1), $"{name} is an arrival level");
                // A debut never shares the screen with a booster unlock.
                foreach (CargoJam.Meta.BoosterType booster in System.Enum.GetValues(typeof(CargoJam.Meta.BoosterType)))
                    Assert.That(CargoJam.Meta.EconomyConfig.UnlockLevel(booster), Is.Not.EqualTo(id), $"{name} unlocks {booster}");

                var colors = new HashSet<int>();
                for (int y = 0; y < level.Height; y++)
                for (int x = 0; x < level.Width; x++)
                    if (level.ColorAt(x, y) != ParcelColors.None) colors.Add(level.ColorAt(x, y));
                Assert.That(colors.Count, Is.LessThanOrEqualTo(3), $"{name} has {colors.Count} colors");

                // Only the new cargo, plus bollards.
                int allowed = (1 << k) | (1 << (int)CargoKind.Bollard);
                Assert.That(level.CargoMask & ~allowed, Is.Zero, $"{name} uses other cargo");
            }
        }
    }
}
