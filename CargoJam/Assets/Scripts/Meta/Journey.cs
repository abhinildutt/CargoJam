using System;

namespace CargoJam.Meta
{
    /// <summary>The light a level is played in, by its place within a destination.</summary>
    public enum Mood
    {
        Day,
        /// <summary>Golden hour: lit windows and streetlights.</summary>
        Dusk,
        /// <summary>The destination's finale (its HARD level).</summary>
        Storm,
    }

    /// <summary>
    /// The delivery journey: six destinations of <see cref="LevelsPerStop"/> levels each, looping after
    /// the last. The single source of truth for which destination and mood a level belongs to; the
    /// environment, UI and meta all ask here. Engine-free.
    /// </summary>
    public static class Journey
    {
        public const int LevelsPerStop = 10;
        /// <summary>Steps (0-based, within a stop) from which a level is played at dusk.</summary>
        public const int DuskFromStep = 5;

        static readonly string[] Names =
        {
            "Harbor Town",
            "Farmlands",
            "Canyon Highway",
            "Pine Ridge",
            "Metro City",
            "Frost Port",
        };

        static readonly string[] Ids = { "harbor", "farm", "canyon", "pine", "metro", "frost" };

        public static int StopCount => Names.Length;

        /// <summary>The destination (0..StopCount-1) of a 0-based progress index; loops after the last.</summary>
        public static int StopFor(int levelIndex) => Math.Max(0, levelIndex) / LevelsPerStop % StopCount;

        /// <summary>How many destinations have been reached in total, counting loops (0 for the first stop).</summary>
        public static int LegFor(int levelIndex) => Math.Max(0, levelIndex) / LevelsPerStop;

        /// <summary>The level's position within its destination, 0..LevelsPerStop-1.</summary>
        public static int StepInStop(int levelIndex) => Math.Max(0, levelIndex) % LevelsPerStop;

        public static Mood MoodFor(int levelIndex)
        {
            int step = StepInStop(levelIndex);
            if (step == LevelsPerStop - 1) return Mood.Storm;
            return step >= DuskFromStep ? Mood.Dusk : Mood.Day;
        }

        /// <summary>True on the first level of every destination after the first one (level 11, 21, ...).</summary>
        public static bool IsArrival(int levelIndex) => levelIndex > 0 && StepInStop(levelIndex) == 0;

        /// <summary>Levels left to win before arriving at the next destination (1..LevelsPerStop).</summary>
        public static int LevelsToNextStop(int levelIndex) => LevelsPerStop - StepInStop(levelIndex);

        public static int NextStop(int levelIndex) => (StopFor(levelIndex) + 1) % StopCount;

        public static string NameOf(int stop) => Names[((stop % StopCount) + StopCount) % StopCount];

        /// <summary>A stable lowercase id ("harbor", "farm", ...) for resource lookups.</summary>
        public static string IdOf(int stop) => Ids[((stop % StopCount) + StopCount) % StopCount];

        public static string MoodName(Mood mood) => mood switch
        {
            Mood.Dusk => "Golden hour",
            Mood.Storm => "Storm",
            _ => "Daytime",
        };
    }
}
