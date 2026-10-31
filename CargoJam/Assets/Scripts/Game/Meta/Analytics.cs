using System;
using System.Collections.Generic;
using System.Text;
using CargoJam.Meta;
using UnityEngine;

namespace CargoJam.Game.Meta
{
    /// <summary>One logged analytics event.</summary>
    public readonly struct AnalyticsEvent
    {
        public readonly string Name;
        public readonly (string key, object value)[] Fields;
        /// <summary>When it was logged (UTC).</summary>
        public readonly DateTime Time;

        public AnalyticsEvent(string name, (string key, object value)[] fields, DateTime time)
        {
            Name = name;
            Fields = fields ?? Array.Empty<(string, object)>();
            Time = time;
        }

        /// <summary>The value of a field, or null when absent.</summary>
        public object Get(string key)
        {
            foreach (var (k, v) in Fields)
                if (k == key) return v;
            return null;
        }

        public override string ToString()
        {
            var sb = new StringBuilder(Name);
            foreach (var (key, value) in Fields) sb.Append(' ').Append(key).Append('=').Append(value);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Local-only analytics: events go to the console in debug builds and into an in-memory ring
    /// buffer of the last <see cref="Capacity"/> events (<see cref="Recent"/>). No network; a real
    /// backend can subscribe to <see cref="Logged"/> later. Call from the main thread.
    ///
    /// Events the game sends (use the constants; suggested fields in brackets):
    /// <list type="bullet">
    /// <item><c>session_start</c> [coins, level, login_streak]: once per launch.</item>
    /// <item><c>level_start</c> [level, hard, attempt, stop, mood]: <see cref="LogLevelStart"/> fills stop and mood.</item>
    /// <item><c>level_complete</c> [level, hard, coins, streak, moves, seconds]</item>
    /// <item><c>level_fail</c> [level, parcels_left, continues_used]: the dock filled up.</item>
    /// <item><c>level_giveup</c> [level, streak_lost, from: fail|pause_restart|pause_home]</item>
    /// <item><c>continue_used</c> [level, cost, continues_used]</item>
    /// <item><c>booster_used</c> [booster, level]</item>
    /// <item><c>booster_bought</c> [booster, price, coins_after]</item>
    /// <item><c>chest_opened</c> [big, coins, booster]</item>
    /// <item><c>daily_claimed</c> [day, coins, booster, login_streak]</item>
    /// <item><c>destination_reached</c> [stop, leg]: a win arrived at a new destination (logged by <see cref="Observe"/>).</item>
    /// <item><c>streak_tier_changed</c> [from, to]: a win or give-up changed the streak tier (logged by <see cref="Observe"/>).</item>
    /// </list>
    /// </summary>
    public static class Analytics
    {
        public const string SessionStart = "session_start";
        public const string LevelStart = "level_start";
        public const string LevelComplete = "level_complete";
        public const string LevelFail = "level_fail";
        public const string LevelGiveUp = "level_giveup";
        public const string ContinueUsed = "continue_used";
        public const string BoosterUsed = "booster_used";
        public const string BoosterBought = "booster_bought";
        public const string ChestOpened = "chest_opened";
        public const string DailyClaimed = "daily_claimed";
        public const string DestinationReached = "destination_reached";
        public const string StreakTierChanged = "streak_tier_changed";

        /// <summary>How many recent events are kept in memory.</summary>
        public const int Capacity = 200;

        static readonly AnalyticsEvent[] ring = new AnalyticsEvent[Capacity];
        static int next;
        static int count;

        /// <summary>Raised for every logged event, after it is buffered.</summary>
        public static event Action<AnalyticsEvent> Logged;

        /// <summary>The buffered events, oldest first (a copy).</summary>
        public static IReadOnlyList<AnalyticsEvent> Recent
        {
            get
            {
                var list = new AnalyticsEvent[count];
                int start = (next - count + Capacity) % Capacity;
                for (int i = 0; i < count; i++) list[i] = ring[(start + i) % Capacity];
                return list;
            }
        }

        /// <summary>Records an event with optional fields, e.g. <c>Log(Analytics.LevelStart, ("level", 7))</c>.</summary>
        public static void Log(string name, params (string key, object value)[] fields)
        {
            if (string.IsNullOrEmpty(name)) return;
            var e = new AnalyticsEvent(name, fields, DateTime.UtcNow);
            ring[next] = e;
            next = (next + 1) % Capacity;
            if (count < Capacity) count++;
            if (Debug.isDebugBuild) Debug.Log("[analytics] " + e);
            Logged?.Invoke(e);
        }

        /// <summary>
        /// Logs <c>level_start</c> with the level's journey context: level (1-based), hard, attempt,
        /// stop (<see cref="Journey.StopFor"/>) and mood (<c>day</c>, <c>dusk</c> or <c>storm</c>).
        /// </summary>
        public static void LogLevelStart(int levelIndex, bool hard, int attempt) =>
            Log(LevelStart,
                ("level", levelIndex + 1),
                ("hard", hard),
                ("attempt", attempt),
                ("stop", Journey.StopFor(levelIndex)),
                ("mood", MoodKey(Journey.MoodFor(levelIndex))));

        /// <summary>
        /// Logs the events <paramref name="progression"/> raises itself: <c>destination_reached</c>
        /// and <c>streak_tier_changed</c>. Returns the unsubscribe action.
        /// </summary>
        public static Action Observe(Progression progression)
        {
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            Action<int, int> reached = (stop, leg) => Log(DestinationReached, ("stop", stop), ("leg", leg));
            Action<int, int> tier = (from, to) => Log(StreakTierChanged, ("from", from), ("to", to));
            progression.DestinationReached += reached;
            progression.StreakTierChanged += tier;
            return () =>
            {
                progression.DestinationReached -= reached;
                progression.StreakTierChanged -= tier;
            };
        }

        static string MoodKey(Mood mood) => mood switch
        {
            Mood.Dusk => "dusk",
            Mood.Storm => "storm",
            _ => "day",
        };

        /// <summary>Empties the buffer.</summary>
        public static void Clear()
        {
            Array.Clear(ring, 0, ring.Length);
            next = 0;
            count = 0;
        }

        // Statics survive play-mode entry when domain reload is off; start each run clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Clear();
            Logged = null;
        }
    }
}
