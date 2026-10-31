using UnityEngine;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The design v3 color tokens for things drawn from C# (icons, fly-coins, FX callouts). Mirrors
    /// the custom properties on .cj-root in CargoJamBase.uss; keep the two in step. Every color the
    /// UI draws from code lives here, including the postcard illustrations and the wordmark.
    /// </summary>
    public static class UiPalette
    {
        public static readonly Color Ink = Hex(0x1E2A3B);
        public static readonly Color Ink2 = Hex(0x4A5869);
        public static readonly Color Ink3 = Hex(0x8A94A1);
        public static readonly Color Surface = Hex(0xFBF7F0);
        public static readonly Color Surface2 = Hex(0xF1EADF);
        public static readonly Color Stroke = new Color(30 / 255f, 42 / 255f, 59 / 255f, 0.10f);
        public static readonly Color OnGlass = Color.white;
        public static readonly Color Glass = new Color(18 / 255f, 26 / 255f, 38 / 255f, 0.46f);
        public static readonly Color GlassStroke = new Color(1f, 1f, 1f, 0.14f);
        public static readonly Color Primary = Hex(0x2FA866);
        public static readonly Color PrimaryPress = Hex(0x258A53);
        public static readonly Color Secondary = Hex(0x2E3E56);
        public static readonly Color SecondaryPress = Hex(0x24324A);
        public static readonly Color Gold = Hex(0xE9B44C);
        public static readonly Color GoldDeep = Hex(0xC8912E);
        public static readonly Color GoldLight = Hex(0xF6D58E);
        public static readonly Color Danger = Hex(0xD9534F);
        public static readonly Color Scrim = new Color(12 / 255f, 18 / 255f, 28 / 255f, 0.55f);
        /// <summary>Clear version of the scrim, for the top of fade-to-scrim gradients.</summary>
        public static readonly Color ScrimClear = new Color(12 / 255f, 18 / 255f, 28 / 255f, 0f);

        /// <summary>Chest wood (multi-color icon).</summary>
        public static readonly Color Wood = Hex(0xA8643A);
        public static readonly Color WoodDark = Hex(0x74431F);
        public static readonly Color WoodLight = Hex(0xC27C47);
        /// <summary>Gift wrap (multi-color icon).</summary>
        public static readonly Color GiftWrap = Danger;
        public static readonly Color GiftWrapDark = Hex(0xB4403C);

        /// <summary>The parcel colors in ParcelColors order (R B G Y P O), for the wordmark.</summary>
        public static readonly Color[] Parcels =
        {
            Hex(0xE5544A), Hex(0x3F7FE6), Hex(0x3DB36B), Hex(0xF2C230), Hex(0x8E5BD6), Hex(0xF08A3C),
        };

        /// <summary>Spark colors for reward bursts: golds and whites, no rainbow.</summary>
        public static readonly Color[] Sparks = { Gold, Color.white, GoldLight, GoldDeep };

        /// <summary>Muted scene colors for the six postcard illustrations (Journey screen).</summary>
        public static class Art
        {
            // Harbor Town
            public static readonly Color HarborSky = Hex(0x9FC3D6);
            public static readonly Color HarborWater = Hex(0x6E9DAE);
            public static readonly Color HarborQuay = Hex(0xD8CCB6);
            public static readonly Color HarborCrane = Hex(0x3E5866);
            public static readonly Color[] Containers = { Hex(0xB26A52), Hex(0x557F90), Hex(0xC3A060), Hex(0x6F8F70) };

            // Farmlands
            public static readonly Color FarmSky = Hex(0xBBD6E2);
            public static readonly Color FarmField = Hex(0x9DB86E);
            public static readonly Color FarmFieldFar = Hex(0x86A35E);
            public static readonly Color Barn = Hex(0xB5563F);
            public static readonly Color BarnRoof = Hex(0x7E3A2C);
            public static readonly Color Hay = Hex(0xD9B45A);

            // Canyon Highway
            public static readonly Color CanyonSky = Hex(0xF1D3A8);
            public static readonly Color Mesa = Hex(0xC46A45);
            public static readonly Color MesaFar = Hex(0xD99067);
            public static readonly Color Sand = Hex(0xE6C08A);
            public static readonly Color Cactus = Hex(0x6E8F4E);

            // Pine Ridge
            public static readonly Color PineSky = Hex(0xB9CBD3);
            public static readonly Color Peak = Hex(0x8796A5);
            public static readonly Color PeakSnow = Hex(0xEEF2F5);
            public static readonly Color Pine = Hex(0x365240);
            public static readonly Color PineLight = Hex(0x4A6B52);
            public static readonly Color Meadow = Hex(0x7E9466);

            // Metro City
            public static readonly Color MetroSky = Hex(0xA9B6CF);
            public static readonly Color Tower = Hex(0x56637A);
            public static readonly Color TowerFar = Hex(0x7C879D);
            public static readonly Color Window = Hex(0xFFD27A);
            public static readonly Color Street = Hex(0x5D6269);

            // Frost Port
            public static readonly Color FrostSky = Hex(0xCFE0EC);
            public static readonly Color Snow = Hex(0xF4F7FA);
            public static readonly Color Ice = Hex(0xA8C9DD);
            public static readonly Color Lighthouse = Hex(0xC9554B);
            public static readonly Color LighthouseBand = Color.white;

            /// <summary>Locked stops: every shape in one flat tone over the inset color.</summary>
            public static readonly Color Silhouette = new Color(30 / 255f, 42 / 255f, 59 / 255f, 0.16f);
        }

        /// <summary>The same color with a different alpha.</summary>
        public static Color A(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}
