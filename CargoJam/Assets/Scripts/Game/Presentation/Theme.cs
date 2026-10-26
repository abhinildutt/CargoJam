using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>The look of one route: environment colors plus a light tint.</summary>
    public readonly struct RouteTheme
    {
        /// <summary>Display name ("Harbor", "Desert", ...).</summary>
        public readonly string Name;
        /// <summary>The big ground plane around the yard.</summary>
        public readonly Color Ground;
        /// <summary>Yard cell tiles, directly under the parcels (the color that matters most for readability).</summary>
        public readonly Color Tile;
        public readonly Color Road;
        /// <summary>Camera clear color.</summary>
        public readonly Color Sky;
        /// <summary>Flat ambient light.</summary>
        public readonly Color Ambient;
        /// <summary>Tint of the sun (directional light).</summary>
        public readonly Color Sun;

        public RouteTheme(string name, Color ground, Color tile, Color road, Color sky, Color ambient, Color sun)
        {
            Name = name;
            Ground = ground;
            Tile = tile;
            Road = road;
            Sky = sky;
            Ambient = ambient;
            Sun = sun;
        }
    }

    /// <summary>
    /// Route themes: a new look every <see cref="LevelsPerRoute"/> levels, cycling Harbor, Desert,
    /// Snow, Night. Tiles stay desaturated and mid-to-light (dark on Night) so the six saturated parcel
    /// colors keep their contrast on every route.
    /// </summary>
    public static class Theme
    {
        /// <summary>Levels per route.</summary>
        public const int LevelsPerRoute = 20;

        // Matches the controller's Awake defaults, so Harbor looks exactly like the pre-theme game.
        static readonly Color DefaultAmbient = new Color(0.62f, 0.62f, 0.66f);

        static readonly RouteTheme[] Routes =
        {
            new RouteTheme("Harbor",
                ground: new Color(0.88f, 0.82f, 0.70f),
                tile: new Color(0.79f, 0.73f, 0.62f),
                road: new Color(0.33f, 0.33f, 0.37f),
                sky: new Color(0.84f, 0.80f, 0.70f),
                ambient: DefaultAmbient,
                sun: Color.white),
            new RouteTheme("Desert",
                ground: new Color(0.93f, 0.76f, 0.56f),
                tile: new Color(0.84f, 0.68f, 0.52f),
                road: new Color(0.42f, 0.33f, 0.28f),
                sky: new Color(0.95f, 0.78f, 0.58f),
                ambient: new Color(0.66f, 0.61f, 0.58f),
                sun: new Color(1f, 0.95f, 0.86f)),
            new RouteTheme("Snow",
                ground: new Color(0.93f, 0.96f, 0.99f),
                tile: new Color(0.76f, 0.83f, 0.92f),
                road: new Color(0.38f, 0.42f, 0.50f),
                sky: new Color(0.88f, 0.93f, 0.98f),
                ambient: new Color(0.62f, 0.65f, 0.72f),
                sun: new Color(0.95f, 0.97f, 1f)),
            new RouteTheme("Night",
                ground: new Color(0.17f, 0.21f, 0.36f),
                tile: new Color(0.27f, 0.32f, 0.50f),
                road: new Color(0.10f, 0.11f, 0.16f),
                sky: new Color(0.09f, 0.11f, 0.22f),
                // Bluish fill, warm key: parcels stay bright where the "street light" hits them.
                ambient: new Color(0.52f, 0.56f, 0.74f),
                sun: new Color(1f, 0.90f, 0.74f)),
        };

        static Light sun;
        static bool sunSearched;

        /// <summary>Number of distinct route looks before they repeat.</summary>
        public static int Count => Routes.Length;

        /// <summary>The theme for a 0-based level index.</summary>
        public static RouteTheme For(int levelIndex) => Routes[Mathf.Max(0, levelIndex / LevelsPerRoute) % Routes.Length];

        /// <summary>Recolors the environment, camera background, ambient and sun for a level.</summary>
        public static RouteTheme Apply(int levelIndex, MaterialBank materials, Camera camera)
        {
            var theme = For(levelIndex);
            materials.SetEnvironment(theme.Ground, theme.Tile, theme.Road);
            if (camera != null) camera.backgroundColor = theme.Sky;
            RenderSettings.ambientLight = theme.Ambient;

            // Found once: the controller makes one directional light and never replaces it.
            if (!sunSearched || sun == null)
            {
                sunSearched = true;
                sun = Object.FindAnyObjectByType<Light>();
            }
            if (sun != null) sun.color = theme.Sun;
            return theme;
        }
    }
}
