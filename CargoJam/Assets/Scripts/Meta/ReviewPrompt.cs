using System;
using System.Globalization;

namespace CargoJam.Meta
{
    /// <summary>
    /// When to ask for an App Store rating (docs/monetization-and-growth-plan.md, section 5): only
    /// right after a high point (a HARD win, or a win that reaches streak tier 2 or higher), never
    /// before <see cref="MinLevel"/>, at most <see cref="MaxAsks"/> times, and at least
    /// <see cref="MinDaysBetween"/> days apart. Apple also limits how often the sheet appears, so a
    /// "yes" here is a request, not a guarantee. Never called on a fail.
    /// </summary>
    public static class ReviewPrompt
    {
        public const string EnabledKey = "review_enabled";
        public const string MinLevelKey = "review_min_level";
        public const string MinDaysBetweenKey = "review_min_days_between";
        public const string MaxAsksKey = "review_max_asks";

        public static bool Enabled => Tuning.Bool(EnabledKey, true);
        /// <summary>The first level (1-based) whose win may ask.</summary>
        public static int MinLevel => Tuning.Int(MinLevelKey, 12, 1, 10000);
        public static int MinDaysBetween => Tuning.Int(MinDaysBetweenKey, 120, 1, 3650);
        public static int MaxAsks => Tuning.Int(MaxAsksKey, 3, 0, 100);

        /// <summary>True when this win is a good moment to ask.</summary>
        /// <param name="levelWon">The level just won, 1-based.</param>
        /// <param name="hard">It was a HARD level.</param>
        /// <param name="tierBefore">Streak tier before the win.</param>
        /// <param name="tierAfter">Streak tier after the win.</param>
        /// <param name="asksSoFar">How many times this install has asked.</param>
        /// <param name="lastAskDay">Local date of the last ask ("yyyy-MM-dd"), or empty.</param>
        /// <param name="today">Today's local date.</param>
        public static bool ShouldAsk(int levelWon, bool hard, int tierBefore, int tierAfter,
                                     int asksSoFar, string lastAskDay, DateTime today)
        {
            if (!Enabled || levelWon < MinLevel || asksSoFar >= MaxAsks) return false;
            bool highPoint = hard || (tierAfter >= 2 && tierAfter > tierBefore);
            if (!highPoint) return false;
            if (string.IsNullOrEmpty(lastAskDay)) return true;
            if (!DateTime.TryParseExact(lastAskDay, Progression.DateFormat, CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out var last))
                return true;
            return (today.Date - last.Date).TotalDays >= MinDaysBetween;
        }
    }
}
