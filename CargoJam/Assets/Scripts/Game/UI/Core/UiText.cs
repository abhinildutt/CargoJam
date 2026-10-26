using CargoJam.Meta;

namespace CargoJam.Game.UI
{
    /// <summary>Player-facing names and copy for boosters and routes.</summary>
    public static class UiText
    {
        static readonly string[] Themes = { "Harbor", "Desert", "Snow", "Night" };

        public const int LevelsPerRoute = 20;

        /// <summary>Theme name of a 0-based route (cycles every four routes).</summary>
        public static string Theme(int route) => Themes[((route % Themes.Length) + Themes.Length) % Themes.Length];

        /// <summary>"Route 1 · Harbor" for a 0-based level index.</summary>
        public static string RouteName(int levelIndex)
        {
            int route = levelIndex / LevelsPerRoute;
            return $"Route {route + 1} · {Theme(route)}";
        }

        /// <summary>1-based position inside the route, e.g. 7 for "7/20".</summary>
        public static int RouteStep(int levelIndex) => levelIndex % LevelsPerRoute + 1;

        public static string BoosterName(BoosterType booster)
        {
            switch (booster)
            {
                case BoosterType.Undo: return "Undo";
                case BoosterType.Crane: return "Crane";
                default: return "Extra Slot";
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

        public static string StreakLoss(int streak) => $"You'll lose your {streak}-win streak!";

        public static string Multiplier(float m) => m >= 1.99f ? "x2" : m >= 1.49f ? "x1.5" : "x1";
    }
}
