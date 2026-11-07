using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>The look of one route (design v2). Kept only so older callers compile.</summary>
    public readonly struct RouteTheme
    {
        public readonly string Name;
        public readonly Color Ground;
        public readonly Color Tile;
        public readonly Color Road;
        public readonly Color Sky;
        public readonly Color Ambient;
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
    /// Obsolete shim. Design v3 moved the world's look to <see cref="Biomes"/> and
    /// <see cref="WorldEnvironment"/>; <see cref="Apply"/> changes nothing so a late call can't undo
    /// the biome's lighting.
    /// </summary>
    public static class Theme
    {
        public const int LevelsPerRoute = 10;

        public static int Count => Biomes.Count;

        /// <summary>The biome palette of a 0-based level index, as a v2 route theme.</summary>
        public static RouteTheme For(int levelIndex)
        {
            var b = Biomes.For(CargoJam.Meta.Journey.StopFor(levelIndex));
            return new RouteTheme(b.Name, b.Ground, b.Tile, b.Road, b.Day.Sky, b.Day.AmbientEquator, b.Day.Sun);
        }

        /// <summary>No-op: the environment already applied the biome. Returns the level's palette.</summary>
        public static RouteTheme Apply(int levelIndex, MaterialBank materials, Camera camera) => For(levelIndex);
    }
}
