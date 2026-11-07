using NUnit.Framework;
using UnityEngine;

namespace CargoJam.Meta.Tests
{
    public class ProfileMigrationTests
    {
        /// <summary>A save as v1 wrote it (every v1 field, none of v2's), parsed the way ProfileStore does.</summary>
        static ProfileData LoadV1(int levelIndex, int winStreak = 0) => JsonUtility.FromJson<ProfileData>(
            "{\"version\":1,\"coins\":420,\"levelIndex\":" + levelIndex + ",\"winStreak\":" + winStreak +
            ",\"bestStreak\":" + winStreak + ",\"chestProgress\":2,\"chestsOpened\":1," +
            "\"boosters\":[1,0,0],\"unlocked\":[true,false,false],\"dailyIndex\":3," +
            "\"lastDailyClaim\":\"2026-10-25\",\"loginStreak\":2,\"lastOpenDay\":\"2026-10-25\"," +
            "\"totalWins\":" + levelIndex + ",\"totalGiveUps\":4,\"coinsEarned\":900}");

        static bool[] Postcards(params int[] stops)
        {
            var cards = new bool[Journey.StopCount];
            foreach (int stop in stops) cards[stop] = true;
            return cards;
        }

        [Test]
        public void FreshProfilesAreCurrent()
        {
            var data = new ProfileData();
            Assert.That(data.version, Is.EqualTo(ProfileData.CurrentVersion));
            Assert.That(ProfileData.CurrentVersion, Is.EqualTo(2));
            Assert.That(ProfileMigration.NeedsUpgrade(data), Is.False);
            Assert.That(ProfileMigration.Upgrade(data), Is.False);
            Assert.That(data.lastArrivalShown, Is.EqualTo(-1));
        }

        [Test]
        public void V1SaveGetsEveryPostcardReachedSoFar()
        {
            var data = LoadV1(23); // level 24: Canyon Highway
            Assert.That(data.version, Is.EqualTo(1));
            Assert.That(ProfileMigration.NeedsUpgrade(data), Is.True);

            Assert.That(ProfileMigration.Upgrade(data), Is.True);
            Assert.That(data.version, Is.EqualTo(2));
            Assert.That(data.postcards, Is.EqualTo(Postcards(0, 1, 2)));
            Assert.That(data.lastArrivalShown, Is.EqualTo(2));
        }

        [Test]
        public void V1SaveKeepsItsOtherFields()
        {
            var data = LoadV1(23, winStreak: 5);
            ProfileMigration.Upgrade(data);
            Assert.That(data.coins, Is.EqualTo(420));
            Assert.That(data.levelIndex, Is.EqualTo(23));
            Assert.That(data.winStreak, Is.EqualTo(5));
            Assert.That(data.boosters, Is.EqualTo(new[] { 1, 0, 0 }));
            Assert.That(data.lastDailyClaim, Is.EqualTo("2026-10-25"));
            Assert.That(data.coinsEarned, Is.EqualTo(900));
        }

        [Test]
        public void V1SaveAtTheStartGetsHarborTown()
        {
            var data = LoadV1(0);
            ProfileMigration.Upgrade(data);
            Assert.That(data.postcards, Is.EqualTo(Postcards(0)));
            Assert.That(data.lastArrivalShown, Is.EqualTo(0));
        }

        [Test]
        public void V1SaveOnALaterLoopHasEveryPostcard()
        {
            var data = LoadV1(75); // second loop, Farmlands again
            ProfileMigration.Upgrade(data);
            Assert.That(data.postcards, Is.EqualTo(Postcards(0, 1, 2, 3, 4, 5)));
            Assert.That(data.lastArrivalShown, Is.EqualTo(7));
        }

        [Test]
        public void V1PlayerStandingOnAnArrivalGetsNoReveal()
        {
            var data = LoadV1(30); // level 31: the first level of Pine Ridge
            var p = new Progression(data, new FakeClock());
            Assert.That(p.ShouldPlayArrival(30), Is.False, "they're already there");
            Assert.That(p.HasPostcard(3), Is.True);

            for (int i = 0; i < 10; i++) p.RecordWin(false);
            Assert.That(p.ShouldPlayArrival(40), Is.True, "the next destination reveals as usual");
        }

        [Test]
        public void ProgressionUpgradesAV1SaveItself()
        {
            var data = LoadV1(12);
            var p = new Progression(data, new FakeClock());
            Assert.That(data.version, Is.EqualTo(2));
            Assert.That(p.PostcardCount, Is.EqualTo(2));
            Assert.That(p.ShouldPlayArrival(10), Is.False);
        }

        [Test]
        public void UpgradeIsIdempotent()
        {
            var data = LoadV1(23);
            ProfileMigration.Upgrade(data);
            data.lastArrivalShown = 2;
            Assert.That(ProfileMigration.Upgrade(data), Is.False);
            Assert.That(data.postcards, Is.EqualTo(Postcards(0, 1, 2)));
        }

        [Test]
        public void NullPostcardsAreRebuilt()
        {
            var data = JsonUtility.FromJson<ProfileData>("{\"version\":1,\"levelIndex\":14,\"postcards\":null}");
            Assert.That(ProfileMigration.Upgrade(data), Is.True);
            Assert.That(data.postcards, Is.EqualTo(Postcards(0, 1)));
        }

        [Test]
        public void ShortPostcardArraysAreResizedKeepingEntries()
        {
            var data = new ProfileData { levelIndex = 3, postcards = new[] { true, false, true }, lastArrivalShown = -1 };
            Assert.That(ProfileMigration.NeedsUpgrade(data), Is.True, "wrong length even at v2");
            Assert.That(ProfileMigration.Upgrade(data), Is.True);
            Assert.That(data.postcards, Is.EqualTo(Postcards(0, 2)));
        }

        [Test]
        public void LongPostcardArraysAreTruncated()
        {
            var data = new ProfileData { postcards = new[] { true, true, false, false, false, false, true, true } };
            ProfileMigration.Upgrade(data);
            Assert.That(data.postcards, Is.EqualTo(Postcards(0, 1)));
        }

        [Test]
        public void ArraysOfTheWrongLengthAreRepairedByProgressionToo()
        {
            var data = new ProfileData { levelIndex = 21, postcards = new bool[2] };
            var p = new Progression(data, new FakeClock());
            Assert.That(data.postcards.Length, Is.EqualTo(Journey.StopCount));
            Assert.That(p.PostcardCount, Is.EqualTo(3));
        }

        [Test]
        public void NullProfileIsLeftAlone()
        {
            Assert.That(ProfileMigration.NeedsUpgrade(null), Is.False);
            Assert.That(ProfileMigration.Upgrade(null), Is.False);
            Assert.That(ProfileMigration.MarkReachedPostcards(null), Is.False);
        }

        [Test]
        public void V2RoundTripsThroughJson()
        {
            var p = new Progression(LoadV1(19), new FakeClock());
            p.RecordWin(true); // arrive at Canyon Highway
            p.MarkArrivalShown(20);

            var loaded = JsonUtility.FromJson<ProfileData>(JsonUtility.ToJson(p.Data));
            Assert.That(ProfileMigration.NeedsUpgrade(loaded), Is.False);
            Assert.That(loaded.version, Is.EqualTo(2));
            Assert.That(loaded.postcards, Is.EqualTo(Postcards(0, 1, 2)));
            Assert.That(loaded.lastArrivalShown, Is.EqualTo(2));
        }
    }
}
