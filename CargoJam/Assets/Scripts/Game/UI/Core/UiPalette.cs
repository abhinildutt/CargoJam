using UnityEngine;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The design v3 color tokens for things drawn from C# (icons, fly-coins, FX callouts). Mirrors
    /// the custom properties on .cj-root in CargoJamBase.uss; keep the two in step.
    /// </summary>
    public static class UiPalette
    {
        public static readonly Color Ink = Hex(0x1E2A3B);
        public static readonly Color Ink2 = Hex(0x4A5869);
        public static readonly Color Ink3 = Hex(0x8A94A1);
        public static readonly Color Surface = Hex(0xFBF7F0);
        public static readonly Color Surface2 = Hex(0xF1EADF);
        public static readonly Color OnGlass = Color.white;
        public static readonly Color Glass = new Color(18 / 255f, 26 / 255f, 38 / 255f, 0.46f);
        public static readonly Color Primary = Hex(0x2FA866);
        public static readonly Color PrimaryPress = Hex(0x258A53);
        public static readonly Color Secondary = Hex(0x2E3E56);
        public static readonly Color SecondaryPress = Hex(0x24324A);
        public static readonly Color Gold = Hex(0xE9B44C);
        public static readonly Color GoldDeep = Hex(0xC8912E);
        public static readonly Color Danger = Hex(0xD9534F);
        public static readonly Color Scrim = new Color(12 / 255f, 18 / 255f, 28 / 255f, 0.55f);

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}
