using CargoJam.Core;
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

        // ---- Cargo (docs/design-v4-cargo-variety.md, section 5) ----

        /// <summary>The cargo's name on its "New cargo!" card.</summary>
        public static string CargoName(CargoKind kind)
        {
            switch (kind)
            {
                case CargoKind.Bollard: return "Bollard";
                case CargoKind.Frost: return "Frozen crate";
                case CargoKind.Mystery: return "Mystery parcel";
                case CargoKind.Express: return "Express parcel";
                case CargoKind.Lock: return "Padlocked parcel";
                case CargoKind.Strap: return "Strapped pair";
                default: return "Customs gate";
            }
        }

        /// <summary>The one line under the name on the "New cargo!" card.</summary>
        public static string CargoHelp(CargoKind kind)
        {
            switch (kind)
            {
                case CargoKind.Bollard: return "Bollards never move. Plan around them.";
                case CargoKind.Frost: return "Frozen crates thaw after their number of moves. Make other moves first.";
                case CargoKind.Mystery: return "Covered parcels show their color once they're free.";
                case CargoKind.Express: return "Ship express parcels before their number runs out. Every move counts.";
                case CargoKind.Lock: return "Load the key parcel into a truck to open the padlock.";
                case CargoKind.Strap: return "Strapped parcels travel together and need two spaces.";
                default: return "Gates open once enough parcels of their color are loaded.";
            }
        }

        public static IconKind CargoIcon(CargoKind kind)
        {
            switch (kind)
            {
                case CargoKind.Bollard: return IconKind.Bollard;
                case CargoKind.Frost: return IconKind.Frost;
                case CargoKind.Mystery: return IconKind.Mystery;
                case CargoKind.Express: return IconKind.Express;
                case CargoKind.Lock: return IconKind.Lock;
                case CargoKind.Strap: return IconKind.Strap;
                default: return IconKind.Gate;
            }
        }

        // ---- Fail sheet ----

        /// <summary>The fail sheet's title for why the level ended.</summary>
        public static string FailTitle(FailReason reason)
        {
            switch (reason)
            {
                case FailReason.Stuck: return "No moves left";
                case FailReason.ExpressMissed: return "Express missed";
                default: return "Out of space";
            }
        }

        /// <summary>The line under the fail title that explains the reason, or null when the title says it all.</summary>
        public static string FailSubline(FailReason reason) =>
            reason == FailReason.ExpressMissed ? "An express parcel ran out of moves." : null;

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
