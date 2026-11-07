using System;
using System.Collections.Generic;
using System.Globalization;

namespace CargoJam.Meta
{
    /// <summary>
    /// Remote overrides for tunable numbers (docs/monetization-and-growth-plan.md, section 7). Every
    /// reader passes its built-in default, so with no overrides the game plays exactly as shipped.
    /// Values arrive as strings from whichever remote-config backend is wired up, are parsed with the
    /// invariant culture, and fall back to the default when missing or unparsable. The Game layer
    /// applies the last fetched set at launch, before the profile loads. Call from the main thread.
    /// </summary>
    public static class Tuning
    {
        static Dictionary<string, string> overrides = new Dictionary<string, string>();

        /// <summary>Raised after <see cref="Apply"/> or <see cref="Reset"/> changes the overrides.</summary>
        public static event Action Changed;

        /// <summary>The active overrides (a copy).</summary>
        public static IReadOnlyDictionary<string, string> Overrides => new Dictionary<string, string>(overrides);

        /// <summary>Replaces every override with <paramref name="values"/> (null clears them). Blank keys are skipped.</summary>
        public static void Apply(IEnumerable<KeyValuePair<string, string>> values)
        {
            var next = new Dictionary<string, string>();
            if (values != null)
                foreach (var pair in values)
                    if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                        next[pair.Key.Trim()] = pair.Value.Trim();
            overrides = next;
            Changed?.Invoke();
        }

        /// <summary>Drops every override, back to the shipped defaults.</summary>
        public static void Reset() => Apply(null);

        public static bool Has(string key) => key != null && overrides.ContainsKey(key);

        /// <summary>The override for <paramref name="key"/> clamped to <paramref name="min"/>..<paramref name="max"/>, or <paramref name="fallback"/>.</summary>
        public static int Int(string key, int fallback, int min = int.MinValue, int max = int.MaxValue)
        {
            if (key == null || !overrides.TryGetValue(key, out var text)) return fallback;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) return fallback;
            return Math.Clamp(value, min, max);
        }

        /// <summary>The override for <paramref name="key"/> clamped to <paramref name="min"/>..<paramref name="max"/>, or <paramref name="fallback"/>.</summary>
        public static float Float(string key, float fallback, float min = float.MinValue, float max = float.MaxValue)
        {
            if (key == null || !overrides.TryGetValue(key, out var text)) return fallback;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
                float.IsNaN(value) || float.IsInfinity(value))
                return fallback;
            return Math.Clamp(value, min, max);
        }

        /// <summary>The override for <paramref name="key"/> ("true"/"false" or "1"/"0"), or <paramref name="fallback"/>.</summary>
        public static bool Bool(string key, bool fallback)
        {
            if (key == null || !overrides.TryGetValue(key, out var text)) return fallback;
            if (bool.TryParse(text, out bool value)) return value;
            if (text == "1") return true;
            if (text == "0") return false;
            return fallback;
        }

        /// <summary>
        /// Encodes overrides as "key=value" lines, for caching between launches. Keys can't contain
        /// '=' or a newline and values can't contain a newline; such entries are dropped.
        /// </summary>
        public static string Serialize(IEnumerable<KeyValuePair<string, string>> values)
        {
            var lines = new List<string>();
            if (values != null)
                foreach (var pair in values)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null) continue;
                    if (pair.Key.IndexOf('=') >= 0 || pair.Key.IndexOfAny(Newlines) >= 0 || pair.Value.IndexOfAny(Newlines) >= 0) continue;
                    lines.Add(pair.Key.Trim() + "=" + pair.Value.Trim());
                }
            lines.Sort(StringComparer.Ordinal);
            return string.Join("\n", lines);
        }

        /// <summary>Decodes <see cref="Serialize"/>'s format. Lines without '=' are ignored; a later duplicate key wins.</summary>
        public static Dictionary<string, string> Deserialize(string text)
        {
            var values = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return values;
            foreach (var raw in text.Split(Newlines, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string key = raw.Substring(0, eq).Trim();
                if (key.Length == 0) continue;
                values[key] = raw.Substring(eq + 1).Trim();
            }
            return values;
        }

        static readonly char[] Newlines = { '\n', '\r' };
    }

    /// <summary>
    /// Every key the game reads through <see cref="Tuning"/>, for remote-config backends that fetch
    /// values one key at a time. A test keeps this in step with the key constants.
    /// </summary>
    public static class TuningKeys
    {
        public static readonly string[] All =
        {
            EconomyConfig.Keys.StartingCoins,
            EconomyConfig.Keys.WinCoins,
            EconomyConfig.Keys.HardWinCoins,
            EconomyConfig.Keys.ChestCoins,
            EconomyConfig.Keys.BigChestCoins,
            EconomyConfig.Keys.UndoPrice,
            EconomyConfig.Keys.CranePrice,
            EconomyConfig.Keys.ExtraSlotPrice,
            EconomyConfig.Keys.ContinueCost,
            EconomyConfig.Keys.SecondContinueCost,
            EconomyConfig.Keys.StreakTier1Multiplier,
            EconomyConfig.Keys.StreakTier2Multiplier,
            EconomyConfig.Keys.StreakTier3Multiplier,
            ReviewPrompt.EnabledKey,
            ReviewPrompt.MinLevelKey,
            ReviewPrompt.MinDaysBetweenKey,
            ReviewPrompt.MaxAsksKey,
            NotificationPlan.EnabledKey,
            NotificationPlan.HourKey,
            NotificationPlan.DaysKey,
            NotificationPlan.MinLevelKey,
        };
    }
}
