using CargoJam.Meta;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>Lighting and atmosphere for one biome in one <see cref="Mood"/>.</summary>
    public struct LightingPreset
    {
        public Color Sun;
        public float SunIntensity;
        /// <summary>Sun rotation in degrees: x = elevation, y = azimuth.</summary>
        public Vector2 SunAngles;
        public Color AmbientSky;
        public Color AmbientEquator;
        public Color AmbientGround;
        public Color Fog;
        /// <summary>Exponential-squared fog density; 0 turns fog off.</summary>
        public float FogDensity;
        /// <summary>Camera clear color (only visible on the menu diorama).</summary>
        public Color Sky;
        /// <summary>Post-processing bloom intensity (threshold 1).</summary>
        public float Bloom;
        /// <summary>Lights in props (windows, streetlights) are on.</summary>
        public bool LampsOn;
    }

    /// <summary>
    /// The look of one journey destination: ground palette, props and lighting per mood. Plain data;
    /// <see cref="WorldEnvironment"/> builds the scene from it.
    /// </summary>
    public sealed class BiomeDefinition
    {
        /// <summary>Same as <see cref="Journey.IdOf"/>.</summary>
        public string Id;
        public string Name;
        public Color Ground;
        /// <summary>Yard tiles: desaturated so the six parcel colors keep their contrast.</summary>
        public Color Tile;
        public Color Road;
        /// <summary>Which prop set the environment builder uses (stream B defines the sets).</summary>
        public string PropSet;
        public LightingPreset Day;
        public LightingPreset Dusk;
        public LightingPreset Storm;

        public LightingPreset Lighting(Mood mood) => mood switch
        {
            Mood.Dusk => Dusk,
            Mood.Storm => Storm,
            _ => Day,
        };
    }

    /// <summary>The biome of every journey stop. Stream B fills in the real definitions.</summary>
    public static class Biomes
    {
        static BiomeDefinition[] all;

        public static BiomeDefinition For(int stop)
        {
            all ??= Build();
            return all[((stop % all.Length) + all.Length) % all.Length];
        }

        // Contract stub: neutral definitions so the game runs before stream B lands.
        static BiomeDefinition[] Build()
        {
            var result = new BiomeDefinition[Journey.StopCount];
            for (int i = 0; i < result.Length; i++)
            {
                var day = new LightingPreset
                {
                    Sun = Color.white,
                    SunIntensity = 1.15f,
                    SunAngles = new Vector2(55f, -35f),
                    AmbientSky = new Color(0.62f, 0.62f, 0.66f),
                    AmbientEquator = new Color(0.62f, 0.62f, 0.66f),
                    AmbientGround = new Color(0.62f, 0.62f, 0.66f),
                    Fog = Color.gray,
                    FogDensity = 0f,
                    Sky = new Color(0.84f, 0.80f, 0.70f),
                    Bloom = 0f,
                };
                result[i] = new BiomeDefinition
                {
                    Id = Journey.IdOf(i),
                    Name = Journey.NameOf(i),
                    Ground = new Color(0.88f, 0.82f, 0.70f),
                    Tile = new Color(0.79f, 0.73f, 0.62f),
                    Road = new Color(0.33f, 0.33f, 0.37f),
                    PropSet = Journey.IdOf(i),
                    Day = day,
                    Dusk = day,
                    Storm = day,
                };
            }
            return result;
        }
    }

    /// <summary>What <see cref="WorldEnvironment"/> is showing, returned by each show call.</summary>
    public struct EnvironmentInfo
    {
        public int LevelIndex;
        public int Stop;
        public Mood Mood;
        public BiomeDefinition Biome;
        /// <summary>The stop or mood differs from the previous show call (a full rebuild happened).</summary>
        public bool Changed;
        /// <summary>This level starts a new destination (see <see cref="Journey.IsArrival"/>).</summary>
        public bool Arrival;
    }
}
