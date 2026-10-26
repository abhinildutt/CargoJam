using UnityEngine;

namespace CargoJam.Game
{
    /// <summary>
    /// The screen size and safe area the game lays itself out for. Normally just <see cref="Screen"/>;
    /// the editor's portrait preview overrides it to render an iPhone layout offscreen, whatever size
    /// the Game view happens to be.
    /// </summary>
    public static class ScreenInfo
    {
        static bool overridden;
        static int width, height;
        static Rect safeArea;

        public static int Width => overridden ? width : Screen.width;
        public static int Height => overridden ? height : Screen.height;
        public static Rect SafeArea => overridden ? safeArea : Screen.safeArea;
        public static bool IsOverridden => overridden;

        public static void Override(int screenWidth, int screenHeight, Rect safe)
        {
            overridden = true;
            width = screenWidth;
            height = screenHeight;
            safeArea = safe;
        }

        public static void ClearOverride() => overridden = false;
    }
}
