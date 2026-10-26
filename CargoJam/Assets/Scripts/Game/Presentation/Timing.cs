namespace CargoJam.Game.Presentation
{
    /// <summary>Every animation duration in one place, for the week-2 feel pass.</summary>
    public static class Timing
    {
        public const float SlidePerCell = 0.06f;
        public const float SlideMin = 0.06f;
        public const float SlideMax = 0.3f;
        public const float HopToDock = 0.24f;
        public const float HopToTruck = 0.3f;
        public const float ChainStagger = 0.08f;
        public const float TapPunch = 0.12f;

        public const float DepartDelay = 0.1f;
        public const float Depart = 0.45f;
        public const float QueueShift = 0.4f;

        public const float Lift = 0.12f;
        public const float BlockedShake = 0.22f;

        public const float WinCelebrateDelay = 0.1f;
        /// <summary>From the confetti to the win card.</summary>
        public const float WinOverlayDelay = 1.1f;

        /// <summary>From the losing tap to the fail explanation, so the last parcel lands first.</summary>
        public const float FailExplainDelay = 0.5f;
        public const float FailPulse = 0.4f;
        /// <summary>From the fail explanation to the fail card.</summary>
        public const float FailOverlayDelay = 1.5f;

        public static float Slide(int pointCount) =>
            UnityEngine.Mathf.Clamp(pointCount * SlidePerCell, SlideMin, SlideMax);
    }
}
