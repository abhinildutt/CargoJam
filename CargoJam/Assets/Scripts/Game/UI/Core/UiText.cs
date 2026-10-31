using CargoJam.Meta;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// Player-facing copy. Design v3 copy rules: sentence case, no exclamation marks on routine
    /// labels (one is fine on a genuine celebration), neutral wording, no guilt.
    /// Destination facts come from <see cref="Journey"/>; this only words them.
    /// </summary>
    public static class UiText
    {
        public static string BoosterName(BoosterType booster)
        {
            switch (booster)
            {
                case BoosterType.Undo: return "Undo";
                case BoosterType.Crane: return "Crane";
                default: return "Extra slot";
            }
        }

        public static string BoosterHelp(BoosterType booster)
        {
            switch (booster)
            {
                case BoosterType.Undo: return "Sends the last parcel in the dock back to its spot in the yard.";
                case BoosterType.Crane: return "Lifts any parcel, even a blocked one, straight to the truck or the dock.";
                default: return "Adds one more dock slot for the rest of the level.";
            }
        }

        public static IconKind BoosterIcon(BoosterType booster)
        {
            switch (booster)
            {
                case BoosterType.Undo: return IconKind.Undo;
                case BoosterType.Crane: return IconKind.Crane;
                default: return IconKind.PlusSlot;
            }
        }

        /// <summary>"×1.5", "×2", "×2.5".</summary>
        public static string Multiplier(float m)
        {
            float r = (float)System.Math.Round(m * 10f) / 10f;
            return "×" + r.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>"Destination 2 of 6 · Golden hour".</summary>
        public static string StopCaption(int levelIndex) =>
            $"Destination {Journey.StopFor(levelIndex) + 1} of {Journey.StopCount} · {Journey.MoodName(Journey.MoodFor(levelIndex))}";

        /// <summary>"3 deliveries to Metro City" for the level about to be played.</summary>
        public static string DeliveriesTo(int levelIndex) =>
            Deliveries(Journey.LevelsToNextStop(levelIndex), Journey.NextStop(levelIndex));

        /// <summary>"3 deliveries to Metro City", "1 delivery to Metro City".</summary>
        public static string Deliveries(int count, int stop) =>
            $"{count} {(count == 1 ? "delivery" : "deliveries")} to {Journey.NameOf(stop)}";

        public static string ParcelsToGo(int n) => n == 1 ? "1 parcel to go" : $"{n} parcels to go";

        public static string WinsInARow(int n) => n == 1 ? "1 win in a row" : $"{n} wins in a row";

        /// <summary>The neutral line shown when giving up now would drop a streak tier, or null.</summary>
        public static string StreakDropWarning(Progression progression) =>
            progression != null && progression.GiveUpDropsTier
                ? "Streak will drop to " + Multiplier(progression.MultiplierAfterGiveUp)
                : null;

        /// <summary>The first 1-based level number of a stop in the lap that contains <paramref name="levelIndex"/>.</summary>
        public static int FirstLevelOf(int stop, int levelIndex)
        {
            int lap = Journey.LegFor(levelIndex) / Journey.StopCount;
            return (lap * Journey.StopCount + stop) * Journey.LevelsPerStop + 1;
        }
    }
}
