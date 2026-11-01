using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CargoJam.Meta.Tests
{
    public class TuningTests
    {
        [TearDown]
        public void TearDown() => Tuning.Reset();

        static void Set(params (string key, string value)[] values)
        {
            var dict = new Dictionary<string, string>();
            foreach (var (key, value) in values) dict[key] = value;
            Tuning.Apply(dict);
        }

        [Test]
        public void WithoutOverridesTheEconomyIsAsShipped()
        {
            Assert.That(EconomyConfig.StartingCoins, Is.EqualTo(300));
            Assert.That(EconomyConfig.WinCoins, Is.EqualTo(40));
            Assert.That(EconomyConfig.HardWinCoins, Is.EqualTo(80));
            Assert.That(EconomyConfig.ChestCoins, Is.EqualTo(100));
            Assert.That(EconomyConfig.BigChestCoins, Is.EqualTo(300));
            Assert.That(EconomyConfig.BoosterPrice(BoosterType.Undo), Is.EqualTo(120));
            Assert.That(EconomyConfig.BoosterPrice(BoosterType.Crane), Is.EqualTo(200));
            Assert.That(EconomyConfig.BoosterPrice(BoosterType.ExtraSlot), Is.EqualTo(240));
            Assert.That(EconomyConfig.ContinueCost(0), Is.EqualTo(300));
            Assert.That(EconomyConfig.ContinueCost(1), Is.EqualTo(600));
            Assert.That(EconomyConfig.StreakTierMultiplier(1), Is.EqualTo(1.5f));
            Assert.That(EconomyConfig.StreakTierMultiplier(2), Is.EqualTo(2f));
            Assert.That(EconomyConfig.StreakTierMultiplier(3), Is.EqualTo(2.5f));
        }

        [Test]
        public void OverridesChangeTheEconomy()
        {
            Set((EconomyConfig.Keys.WinCoins, "55"),
                (EconomyConfig.Keys.CranePrice, "180"),
                (EconomyConfig.Keys.SecondContinueCost, "450"),
                (EconomyConfig.Keys.StreakTier3Multiplier, "3"));
            Assert.That(EconomyConfig.WinCoins, Is.EqualTo(55));
            Assert.That(EconomyConfig.BoosterPrice(BoosterType.Crane), Is.EqualTo(180));
            Assert.That(EconomyConfig.ContinueCost(0), Is.EqualTo(300));
            Assert.That(EconomyConfig.ContinueCost(1), Is.EqualTo(450));
            Assert.That(EconomyConfig.StreakTierMultiplier(3), Is.EqualTo(3f));
        }

        [Test]
        public void AWinPaysTheTunedCoins()
        {
            Set((EconomyConfig.Keys.WinCoins, "50"));
            var p = new Progression(new ProfileData { coins = 0 }, new FakeClock());
            var result = p.RecordWin(false);
            Assert.That(result.BaseCoins, Is.EqualTo(50));
            Assert.That(p.Coins, Is.EqualTo(50));
        }

        [Test]
        public void BadValuesFallBackOrClamp()
        {
            Set((EconomyConfig.Keys.WinCoins, "lots"),
                (EconomyConfig.Keys.UndoPrice, "-5"),
                (EconomyConfig.Keys.StreakTier1Multiplier, "NaN"),
                (EconomyConfig.Keys.StreakTier2Multiplier, "0.2"));
            Assert.That(EconomyConfig.WinCoins, Is.EqualTo(40));
            Assert.That(EconomyConfig.BoosterPrice(BoosterType.Undo), Is.EqualTo(0));
            Assert.That(EconomyConfig.StreakTierMultiplier(1), Is.EqualTo(1.5f));
            Assert.That(EconomyConfig.StreakTierMultiplier(2), Is.EqualTo(1f));
        }

        [Test]
        public void FloatsUseTheInvariantCulture()
        {
            Set(("x", "2.75"));
            Assert.That(Tuning.Float("x", 0f), Is.EqualTo(2.75f));
        }

        [Test]
        public void BoolsAcceptWordsAndDigits()
        {
            Set(("a", "false"), ("b", "1"), ("c", "maybe"));
            Assert.That(Tuning.Bool("a", true), Is.False);
            Assert.That(Tuning.Bool("b", false), Is.True);
            Assert.That(Tuning.Bool("c", true), Is.True);
            Assert.That(Tuning.Bool("missing", true), Is.True);
        }

        [Test]
        public void ApplyReplacesEverything()
        {
            Set(("a", "1"), ("b", "2"));
            Set(("b", "3"));
            Assert.That(Tuning.Has("a"), Is.False);
            Assert.That(Tuning.Int("b", 0), Is.EqualTo(3));
            Tuning.Reset();
            Assert.That(Tuning.Overrides, Is.Empty);
        }

        [Test]
        public void SerializeRoundTrips()
        {
            var values = new Dictionary<string, string>
            {
                ["economy_win_coins"] = "45",
                ["notify_hour"] = "18",
                ["odd"] = "a=b",
            };
            var back = Tuning.Deserialize(Tuning.Serialize(values));
            Assert.That(back, Is.EquivalentTo(values));
        }

        [Test]
        public void SerializeDropsUnencodableEntries()
        {
            var values = new Dictionary<string, string> { ["a=b"] = "1", ["c"] = "line\nbreak", ["ok"] = "1" };
            Assert.That(Tuning.Serialize(values), Is.EqualTo("ok=1"));
        }

        [Test]
        public void DeserializeIgnoresJunk()
        {
            var values = Tuning.Deserialize("no equals\n=nokey\n  key = value  \r\nkey=later");
            Assert.That(values.Count, Is.EqualTo(1));
            Assert.That(values["key"], Is.EqualTo("later"));
        }

        [Test]
        public void KnownKeysListEveryKeyConstantOnce()
        {
            var constants = new List<string>();
            foreach (var type in new[] { typeof(EconomyConfig.Keys), typeof(ReviewPrompt), typeof(NotificationPlan) })
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                    if (field.IsLiteral && field.FieldType == typeof(string) &&
                        (type == typeof(EconomyConfig.Keys) || field.Name.EndsWith("Key")))
                        constants.Add((string)field.GetRawConstantValue());
            Assert.That(TuningKeys.All, Is.EquivalentTo(constants));
            Assert.That(TuningKeys.All.Distinct().Count(), Is.EqualTo(TuningKeys.All.Length));
        }

        [Test]
        public void KeysAreSafeRemoteConfigNames()
        {
            foreach (var key in TuningKeys.All)
                Assert.That(Regex.IsMatch(key, "^[a-z0-9_]{1,64}$"), Is.True, key);
        }
    }
}
