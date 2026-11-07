using NUnit.Framework;

namespace CargoJam.Meta.Tests
{
    // Table-driven with plain [Test] methods: tools/check's runner doesn't expand [TestCase].
    public class JourneyTests
    {
        // ---- mapping (0-based level indexes; level N is index N-1) ----

        [Test]
        public void MapsLevelToStopLegStepAndMood()
        {
            // index, stop, leg, step, mood
            var table = new (int index, int stop, int leg, int step, Mood mood)[]
            {
                (0, 0, 0, 0, Mood.Day),
                (4, 0, 0, 4, Mood.Day),
                (5, 0, 0, 5, Mood.Dusk),
                (8, 0, 0, 8, Mood.Dusk),
                (9, 0, 0, 9, Mood.Storm),
                (10, 1, 1, 0, Mood.Day),
                (59, 5, 5, 9, Mood.Storm),
                (60, 0, 6, 0, Mood.Day),
                (61, 0, 6, 1, Mood.Day),
            };
            foreach (var row in table)
            {
                Assert.That(Journey.StopFor(row.index), Is.EqualTo(row.stop), $"stop of {row.index}");
                Assert.That(Journey.LegFor(row.index), Is.EqualTo(row.leg), $"leg of {row.index}");
                Assert.That(Journey.StepInStop(row.index), Is.EqualTo(row.step), $"step of {row.index}");
                Assert.That(Journey.MoodFor(row.index), Is.EqualTo(row.mood), $"mood of {row.index}");
            }
        }

        [Test]
        public void SixStopsOfTenLevels()
        {
            Assert.That(Journey.StopCount, Is.EqualTo(6));
            Assert.That(Journey.LevelsPerStop, Is.EqualTo(10));
            Assert.That(Journey.NameOf(0), Is.EqualTo("Harbor Town"));
            Assert.That(Journey.NameOf(5), Is.EqualTo("Frost Port"));
        }

        [Test]
        public void LoopsAfterTheLastStop()
        {
            for (int index = 0; index < 60; index++)
            {
                Assert.That(Journey.StopFor(index + 60), Is.EqualTo(Journey.StopFor(index)), $"index {index}");
                Assert.That(Journey.MoodFor(index + 60), Is.EqualTo(Journey.MoodFor(index)), $"index {index}");
                Assert.That(Journey.LegFor(index + 60), Is.EqualTo(Journey.LegFor(index) + 6), $"index {index}");
            }
            Assert.That(Journey.StopFor(125), Is.EqualTo(0), "third loop");
        }

        [Test]
        public void NegativeIndexesClampToTheStart()
        {
            Assert.That(Journey.StopFor(-5), Is.EqualTo(0));
            Assert.That(Journey.LegFor(-5), Is.EqualTo(0));
            Assert.That(Journey.MoodFor(-5), Is.EqualTo(Mood.Day));
            Assert.That(Journey.IsArrival(-10), Is.False);
        }

        [Test]
        public void NamesAndIdsWrapForAnyStop()
        {
            Assert.That(Journey.NameOf(6), Is.EqualTo(Journey.NameOf(0)));
            Assert.That(Journey.NameOf(-1), Is.EqualTo(Journey.NameOf(5)));
            Assert.That(Journey.IdOf(1), Is.EqualTo("farm"));
            Assert.That(Journey.MoodName(Mood.Dusk), Is.EqualTo("Golden hour"));
        }

        // ---- arrivals ----

        [Test]
        public void ArrivalIsTheFirstLevelOfEveryStopAfterTheVeryFirst()
        {
            int[] arrivals = { 10, 20, 50, 60, 70 };
            int[] notArrivals = { 0, 1, 9, 11, 59, 61 };
            foreach (int index in arrivals) Assert.That(Journey.IsArrival(index), Is.True, $"index {index}");
            foreach (int index in notArrivals) Assert.That(Journey.IsArrival(index), Is.False, $"index {index}");
        }

        [Test]
        public void CountsLevelsToTheNextStop()
        {
            var table = new (int index, int left, int nextStop)[]
            {
                (0, 10, 1),
                (4, 6, 1),
                (9, 1, 1),
                (10, 10, 2),
                (59, 1, 0),
                (60, 10, 1),
                (61, 9, 1),
            };
            foreach (var row in table)
            {
                Assert.That(Journey.LevelsToNextStop(row.index), Is.EqualTo(row.left), $"left from {row.index}");
                Assert.That(Journey.NextStop(row.index), Is.EqualTo(row.nextStop), $"next from {row.index}");
                Assert.That(Journey.IsArrival(row.index + row.left), Is.True, $"winning {row.left} from {row.index} arrives");
                Assert.That(Journey.StopFor(row.index + row.left), Is.EqualTo(row.nextStop), $"arrives at next from {row.index}");
            }
        }
    }
}
