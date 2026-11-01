using System;
using System.Collections.Generic;

namespace CargoJam.Meta
{
    /// <summary>One local notification to schedule.</summary>
    public readonly struct PlannedNotification
    {
        /// <summary>Stable id per slot (1, 2, 3, ...), so rescheduling replaces rather than stacks.</summary>
        public readonly int Id;
        public readonly string Title;
        public readonly string Body;
        /// <summary>Local wall-clock time to deliver it.</summary>
        public readonly DateTime FireAt;

        public PlannedNotification(int id, string title, string body, DateTime fireAt)
        {
            Id = id;
            Title = title;
            Body = body;
            FireAt = fireAt;
        }

        public override string ToString() => $"#{Id} {FireAt:yyyy-MM-dd HH:mm} {Title}: {Body}";
    }

    /// <summary>
    /// The local notifications to schedule when the app goes to the background
    /// (docs/monetization-and-growth-plan.md, section 5). At most one a day, at
    /// <see cref="Hour"/> local time, for the next <see cref="Days"/> days only, so a player who
    /// stops playing hears from us three times and then never again. Nothing is planned before the
    /// player has won <see cref="MinLevel"/> levels (that's also when permission is asked).
    /// Copy is plain and honest: no fake urgency (the streak doesn't decay with time) and no guilt.
    /// </summary>
    public static class NotificationPlan
    {
        public const string EnabledKey = "notify_enabled";
        public const string HourKey = "notify_hour";
        public const string DaysKey = "notify_days";
        public const string MinLevelKey = "notify_min_level";

        public static bool Enabled => Tuning.Bool(EnabledKey, true);
        /// <summary>Local hour (0–23) the notifications arrive.</summary>
        public static int Hour => Tuning.Int(HourKey, 19, 9, 21);
        /// <summary>How many days ahead to plan, one notification a day.</summary>
        public static int Days => Tuning.Int(DaysKey, 3, 0, 7);
        /// <summary>Levels the player must have won before any notification (and the permission ask).</summary>
        public static int MinLevel => Tuning.Int(MinLevelKey, 5, 0, 1000);

        /// <summary>A streak this long gets its own reminder (streak tier 1).</summary>
        public static int StreakWorthMentioning => EconomyConfig.StreakTier1At;

        /// <summary>
        /// Skip today's slot when it's less than this far away, so closing the app at 18:50 doesn't
        /// buzz ten minutes later.
        /// </summary>
        public static readonly TimeSpan MinLead = TimeSpan.FromHours(1);

        /// <summary>True once the player has won enough levels to be asked for notification permission.</summary>
        public static bool ShouldAskPermission(Progression progression) =>
            Enabled && progression != null && progression.LevelIndex >= MinLevel;

        /// <summary>What to schedule now, earliest first. Empty when disabled or too early in the game.</summary>
        /// <param name="now">Local wall-clock time.</param>
        public static List<PlannedNotification> For(Progression progression, DateTime now)
        {
            var plan = new List<PlannedNotification>();
            if (!ShouldAskPermission(progression) || Days <= 0) return plan;

            DateTime first = now.Date.AddHours(Hour);
            if (first - now < MinLead) first = first.AddDays(1);

            var daily = progression.DailyStatus();
            string claimedOn = progression.Data.lastDailyClaim ?? "";
            int level = progression.LevelIndex + 1;
            int stop = Journey.StopFor(progression.LevelIndex);

            var messages = new List<(string title, string body)>();
            // The calendar never resets, so a reward waits on any day after the last claim.
            if (Progression.DateKey(first) != claimedOn)
                messages.Add(("Daily reward ready", $"Day {daily.Day + 1} of your calendar is waiting at the depot."));
            if (progression.WinStreak >= StreakWorthMentioning)
                messages.Add(($"{progression.WinStreak}-win streak", $"Your streak is still rolling. Level {level} is loaded and ready."));
            messages.Add(("Fresh cargo", $"Level {level} in {Journey.NameOf(stop)} is loaded and ready to go."));

            for (int i = 0; i < Days && i < messages.Count; i++)
                plan.Add(new PlannedNotification(i + 1, messages[i].title, messages[i].body, first.AddDays(i)));
            return plan;
        }
    }
}
