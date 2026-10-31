using System.Collections.Generic;
using NUnit.Framework;

namespace CargoJam.Meta.Tests
{
    public class PostcardTests
    {
        FakeClock clock;
        Progression p;
        int changes;
        List<(int stop, int leg)> reached;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock();
            p = Make(new ProfileData());
        }

        Progression Make(ProfileData data)
        {
            var progression = new Progression(data, clock);
            changes = 0;
            reached = new List<(int, int)>();
            progression.Changed += () => changes++;
            progression.DestinationReached += (stop, leg) => reached.Add((stop, leg));
            return progression;
        }

        Progression At(int levelIndex) => Make(new ProfileData { levelIndex = levelIndex });

        // ---- earning ----

        [Test]
        public void NewProfileHasHarborTownOnly()
        {
            Assert.That(p.HasPostcard(0), Is.True, "Harbor Town is earned on first launch");
            for (int stop = 1; stop < Journey.StopCount; stop++) Assert.That(p.HasPostcard(stop), Is.False, $"stop {stop}");
            Assert.That(p.PostcardCount, Is.EqualTo(1));
            Assert.That(p.Data.postcards.Length, Is.EqualTo(Journey.StopCount));
        }

        [Test]
        public void ArrivalWinEarnsTheNextPostcard()
        {
            p = At(9); // level 10, Harbor Town's finale
            var r = p.RecordWin(true);
            Assert.That(r.NewLevelIndex, Is.EqualTo(10));
            Assert.That(r.ArrivedAt, Is.EqualTo(1));
            Assert.That(r.PostcardEarned, Is.EqualTo(1));
            Assert.That(p.HasPostcard(1), Is.True);
            Assert.That(changes, Is.EqualTo(1), "one Changed for the whole win");
            Assert.That(reached, Is.EqualTo(new List<(int, int)> { (1, 1) }));
        }

        [Test]
        public void OnlyArrivalWinsEarnPostcards()
        {
            for (int win = 1; win <= 25; win++)
            {
                var r = p.RecordWin(false);
                bool arrival = r.NewLevelIndex == 10 || r.NewLevelIndex == 20;
                Assert.That(r.ArrivedAt, Is.EqualTo(arrival ? Journey.StopFor(r.NewLevelIndex) : -1), $"win {win}");
                Assert.That(r.PostcardEarned, Is.EqualTo(r.ArrivedAt), $"win {win}");
            }
            Assert.That(p.PostcardCount, Is.EqualTo(3));
            Assert.That(p.HasPostcard(2), Is.True);
            Assert.That(p.HasPostcard(3), Is.False);
            Assert.That(reached, Is.EqualTo(new List<(int, int)> { (1, 1), (2, 2) }));
        }

        [Test]
        public void LoopingBackDoesNotAwardTwice()
        {
            p = At(59); // the last level of Frost Port, first loop
            for (int stop = 0; stop < Journey.StopCount; stop++) Assert.That(p.HasPostcard(stop), Is.True, $"stop {stop}");
            changes = 0;
            var r = p.RecordWin(true);
            Assert.That(r.ArrivedAt, Is.EqualTo(0), "back in Harbor Town");
            Assert.That(r.PostcardEarned, Is.EqualTo(-1), "already earned");
            Assert.That(reached, Is.EqualTo(new List<(int, int)> { (0, 6) }), "the leg keeps counting");
            Assert.That(p.PostcardCount, Is.EqualTo(Journey.StopCount));
        }

        [Test]
        public void MarkPostcardReportsNewOnlyOnce()
        {
            Assert.That(p.MarkPostcard(3), Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(p.MarkPostcard(3), Is.False);
            Assert.That(p.MarkPostcard(0), Is.False, "Harbor Town is already earned");
            Assert.That(changes, Is.EqualTo(1), "no change, no event");
            Assert.That(p.HasPostcard(3), Is.True);
        }

        [Test]
        public void MarkPostcardIgnoresUnknownStops()
        {
            Assert.That(p.MarkPostcard(-1), Is.False);
            Assert.That(p.MarkPostcard(Journey.StopCount), Is.False);
            Assert.That(p.HasPostcard(-1), Is.False);
            Assert.That(p.HasPostcard(Journey.StopCount), Is.False);
            Assert.That(changes, Is.EqualTo(0));
        }

        [Test]
        public void PostcardsSurviveReload()
        {
            p = At(19);
            p.RecordWin(false);
            var reloaded = new Progression(p.Data, clock);
            Assert.That(reloaded.HasPostcard(2), Is.True);
            Assert.That(reloaded.PostcardCount, Is.EqualTo(3));
        }

        // ---- arrival reveal bookkeeping ----

        [Test]
        public void ArrivalPlaysOnceThenIsMarkedShown()
        {
            p = At(9);
            p.RecordWin(false);
            Assert.That(p.ShouldPlayArrival(10), Is.True);
            p.MarkArrivalShown(10);
            Assert.That(p.Data.lastArrivalShown, Is.EqualTo(1));
            Assert.That(p.ShouldPlayArrival(10), Is.False, "a restart or relaunch doesn't replay it");
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void MarkingTheSameLegAgainChangesNothing()
        {
            p = At(10);
            p.MarkArrivalShown(10);
            changes = 0;
            p.MarkArrivalShown(10);
            p.MarkArrivalShown(13); // same leg, mid-destination
            Assert.That(changes, Is.EqualTo(0));
            Assert.That(p.Data.lastArrivalShown, Is.EqualTo(1));
        }

        [Test]
        public void MarkArrivalShownNeverMovesBackwards()
        {
            p = At(30);
            p.MarkArrivalShown(30);
            p.MarkArrivalShown(10);
            Assert.That(p.Data.lastArrivalShown, Is.EqualTo(3));
        }

        [Test]
        public void NonArrivalLevelsNeverPlayTheReveal()
        {
            p = At(14);
            Assert.That(p.ShouldPlayArrival(0), Is.False, "the very first level is not an arrival");
            Assert.That(p.ShouldPlayArrival(14), Is.False);
            Assert.That(p.ShouldPlayArrival(19), Is.False);
        }

        [Test]
        public void LoopsReplayTheReveal()
        {
            p = At(59);
            p.MarkArrivalShown(50);
            p.RecordWin(true);
            Assert.That(p.ShouldPlayArrival(60), Is.True, "Harbor Town again on the next loop");
            p.MarkArrivalShown(60);
            Assert.That(p.Data.lastArrivalShown, Is.EqualTo(6));
            Assert.That(p.ShouldPlayArrival(60), Is.False);
        }

        [Test]
        public void ARevealMarkedForALegNotReachedIsClampedOnLoad()
        {
            // E.g. a debug "reset level" that kept the profile: reveals for the legs ahead play again.
            var data = new ProfileData { levelIndex = 12, lastArrivalShown = 4 };
            p = Make(data);
            Assert.That(data.lastArrivalShown, Is.EqualTo(1));
        }
    }
}
