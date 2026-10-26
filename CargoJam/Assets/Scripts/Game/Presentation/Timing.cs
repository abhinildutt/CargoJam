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

        public const float WinOverlayDelay = 0.35f;
        public const float FailOverlayDelay = 0.6f;

        public static float Slide(int pointCount) =>
            UnityEngine.Mathf.Clamp(pointCount * SlidePerCell, SlideMin, SlideMax);
    }
}
