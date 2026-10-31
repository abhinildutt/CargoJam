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
        /// <summary>Shadow darkness, 0..1.</summary>
        public float ShadowStrength;
        public Color AmbientSky;
        public Color AmbientEquator;
        public Color AmbientGround;
        public Color Fog;
        /// <summary>
        /// Linear fog strength: fog starts just past the board's far edge and is full
        /// 1 / FogDensity units further back (the board itself never gets fog). 0 turns fog off.
        /// </summary>
        public float FogDensity;
        /// <summary>Camera clear color (only visible on the menu diorama).</summary>
        public Color Sky;
        /// <summary>Post-processing bloom intensity (threshold 1, capped at 0.35).</summary>
        public float Bloom;
        /// <summary>Lights in props (windows, streetlights) are on.</summary>
        public bool LampsOn;
        /// <summary>HDR multiplier of the lit windows and lamp heads when <see cref="LampsOn"/>.</summary>
        public float Glow;
        /// <summary>Color adjustments: post exposure (EV), contrast and saturation (-100..100), color filter.</summary>
        public float Exposure;
        public float Contrast;
        public float Saturation;
        public Color Filter;
        /// <summary>Vignette intensity (capped at 0.22).</summary>
        public float Vignette;
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
        /// <summary>The terrain around the depot (grass, sand, snow, pavement).</summary>
        public Color Ground;
        /// <summary>Yard tiles: desaturated so the six parcel colors keep their contrast.</summary>
        public Color Tile;
        public Color Road;
        /// <summary>Which prop set the environment builder uses (see <see cref="PropSets"/>).</summary>
        public string PropSet;
        public LightingPreset Day;
        public LightingPreset Dusk;
        public LightingPreset Storm;

        /// <summary>The concrete slab under the yard and dock.</summary>
        public Color Apron;
        /// <summary>Road paint (edge lines, dashes, crosswalks).</summary>
        public Color LaneMark;
        /// <summary>Curbs, sidewalks and quay edges.</summary>
        public Color Curb;

        /// <summary>Nature-kit recolors: leaves, grass tufts, bark and planks, rocks, soil.</summary>
        public Color Foliage;
        public Color Grass;
        public Color Wood;
        public Color Rock;
        public Color Dirt;

        /// <summary>Code-built buildings: walls, roofs, trims and one landmark accent (cranes, barn, canopy).</summary>
        public Color Wall;
        public Color Roof;
        public Color Trim;
        public Color Accent;
        public Color Water;
        /// <summary>Multiplies the textured city and car kits (a gentle per-biome grade).</summary>
        public Color KitTint;

        public LightingPreset Lighting(Mood mood) => mood switch
        {
            Mood.Dusk => Dusk,
            Mood.Storm => Storm,
            _ => Day,
        };
    }

    /// <summary>
    /// The biome of every journey stop. One table per destination below: palette first, then the
    /// three moods. Ground and tiles near the yard stay at least 25% less saturated than the parcel
    /// colors; props further back may be richer.
    /// </summary>
    public static class Biomes
    {
        static BiomeDefinition[] all;

        static BiomeDefinition[] All =>
            all ??= new[] { HarborTown(), Farmlands(), CanyonHighway(), PineRidge(), MetroCity(), FrostPort() };

        public static int Count => All.Length;

        public static BiomeDefinition For(int stop) => All[((stop % All.Length) + All.Length) % All.Length];

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static LightingPreset Light(Color sun, float intensity, float elevation, float azimuth,
            Color sky, Color equator, Color ground, Color fog, float fogDensity,
            float bloom = 0.12f, bool lamps = false, float glow = 0f, float shadows = 0.6f,
            float exposure = 0f, float contrast = 6f, float saturation = 4f, Color? filter = null,
            float vignette = 0.14f) => new LightingPreset
        {
            Sun = sun,
            SunIntensity = intensity,
            SunAngles = new Vector2(elevation, azimuth),
            ShadowStrength = shadows,
            AmbientSky = sky,
            AmbientEquator = equator,
            AmbientGround = ground,
            Fog = fog,
            FogDensity = fogDensity,
            Sky = fog,
            Bloom = bloom,
            LampsOn = lamps,
            Glow = glow,
            Exposure = exposure,
            Contrast = contrast,
            Saturation = saturation,
            Filter = filter ?? Color.white,
            Vignette = vignette,
        };

        // ---- 0 · Harbor Town: quay, containers, cranes, water --------------------------------------

        static BiomeDefinition HarborTown() => new BiomeDefinition
        {
            Id = Journey.IdOf(0), Name = Journey.NameOf(0), PropSet = "harbor",
            Ground = C(0.78f, 0.76f, 0.72f),
            Apron = C(0.69f, 0.68f, 0.66f),
            Tile = C(0.82f, 0.79f, 0.73f),
            Road = C(0.31f, 0.32f, 0.35f),
            LaneMark = C(0.93f, 0.91f, 0.84f),
            Curb = C(0.86f, 0.84f, 0.80f),
            Foliage = C(0.40f, 0.60f, 0.46f), Grass = C(0.55f, 0.68f, 0.50f), Wood = C(0.58f, 0.44f, 0.33f),
            Rock = C(0.60f, 0.63f, 0.66f), Dirt = C(0.64f, 0.56f, 0.46f),
            Wall = C(0.88f, 0.84f, 0.76f), Roof = C(0.40f, 0.45f, 0.52f), Trim = C(0.95f, 0.94f, 0.90f),
            Accent = C(0.82f, 0.46f, 0.30f), Water = C(0.18f, 0.44f, 0.55f), KitTint = C(1f, 1f, 1f),
            Day = Light(C(1f, 0.96f, 0.88f), 1.15f, 50f, -30f,
                C(0.66f, 0.73f, 0.82f), C(0.60f, 0.61f, 0.62f), C(0.40f, 0.39f, 0.37f),
                C(0.76f, 0.84f, 0.90f), 0.02f, exposure: 0.1f),
            Dusk = Light(C(1f, 0.74f, 0.50f), 1.3f, 26f, -52f,
                C(0.48f, 0.48f, 0.64f), C(0.62f, 0.52f, 0.46f), C(0.30f, 0.27f, 0.27f),
                C(0.90f, 0.70f, 0.56f), 0.035f, bloom: 0.3f, lamps: true, glow: 2.2f, shadows: 0.5f,
                exposure: 0.15f, contrast: 10f, filter: C(1f, 0.96f, 0.90f), vignette: 0.2f),
            Storm = Light(C(0.72f, 0.78f, 0.88f), 0.55f, 62f, -15f,
                C(0.46f, 0.52f, 0.62f), C(0.42f, 0.46f, 0.52f), C(0.25f, 0.27f, 0.31f),
                C(0.42f, 0.49f, 0.57f), 0.07f, bloom: 0.25f, lamps: true, glow: 1.8f, shadows: 0.3f,
                exposure: 0.3f, contrast: 8f, saturation: -6f, filter: C(0.92f, 0.96f, 1f), vignette: 0.22f),
        };

        // ---- 1 · Farmlands: fields, barns, fences, hay bales, trees --------------------------------

        static BiomeDefinition Farmlands() => new BiomeDefinition
        {
            Id = Journey.IdOf(1), Name = Journey.NameOf(1), PropSet = "farm",
            Ground = C(0.62f, 0.70f, 0.50f),
            Apron = C(0.72f, 0.68f, 0.60f),
            Tile = C(0.83f, 0.78f, 0.67f),
            Road = C(0.36f, 0.34f, 0.33f),
            LaneMark = C(0.94f, 0.90f, 0.78f),
            Curb = C(0.70f, 0.60f, 0.47f),
            Foliage = C(0.38f, 0.60f, 0.34f), Grass = C(0.50f, 0.68f, 0.40f), Wood = C(0.62f, 0.46f, 0.32f),
            Rock = C(0.66f, 0.65f, 0.62f), Dirt = C(0.58f, 0.44f, 0.32f),
            Wall = C(0.68f, 0.30f, 0.26f), Roof = C(0.36f, 0.30f, 0.30f), Trim = C(0.95f, 0.93f, 0.88f),
            Accent = C(0.90f, 0.78f, 0.46f), Water = C(0.30f, 0.52f, 0.60f), KitTint = C(1f, 0.98f, 0.94f),
            Day = Light(C(1f, 0.95f, 0.84f), 1.2f, 52f, -32f,
                C(0.66f, 0.74f, 0.84f), C(0.62f, 0.63f, 0.56f), C(0.40f, 0.42f, 0.32f),
                C(0.82f, 0.86f, 0.84f), 0.018f, exposure: 0.1f, saturation: 6f),
            Dusk = Light(C(1f, 0.72f, 0.46f), 1.3f, 24f, -55f,
                C(0.50f, 0.48f, 0.62f), C(0.64f, 0.52f, 0.42f), C(0.32f, 0.28f, 0.22f),
                C(0.94f, 0.72f, 0.52f), 0.035f, bloom: 0.3f, lamps: true, glow: 2.2f, shadows: 0.5f,
                exposure: 0.15f, contrast: 10f, filter: C(1f, 0.95f, 0.88f), vignette: 0.2f),
            Storm = Light(C(0.74f, 0.78f, 0.86f), 0.55f, 60f, -18f,
                C(0.46f, 0.52f, 0.60f), C(0.42f, 0.46f, 0.48f), C(0.26f, 0.28f, 0.26f),
                C(0.44f, 0.50f, 0.55f), 0.07f, bloom: 0.25f, lamps: true, glow: 1.8f, shadows: 0.3f,
                exposure: 0.3f, contrast: 8f, saturation: -6f, filter: C(0.93f, 0.96f, 1f), vignette: 0.22f),
        };

        // ---- 2 · Canyon Highway: red mesas, cacti, gas station --------------------------------------

        static BiomeDefinition CanyonHighway() => new BiomeDefinition
        {
            Id = Journey.IdOf(2), Name = Journey.NameOf(2), PropSet = "canyon",
            // Sandy beige, not orange: orange parcels must stand out from the ground.
            Ground = C(0.86f, 0.75f, 0.62f),
            Apron = C(0.76f, 0.71f, 0.64f),
            Tile = C(0.84f, 0.78f, 0.69f),
            Road = C(0.38f, 0.33f, 0.31f),
            LaneMark = C(0.96f, 0.86f, 0.62f),
            Curb = C(0.80f, 0.70f, 0.58f),
            Foliage = C(0.44f, 0.58f, 0.40f), Grass = C(0.66f, 0.66f, 0.44f), Wood = C(0.60f, 0.44f, 0.32f),
            Rock = C(0.74f, 0.45f, 0.33f), Dirt = C(0.78f, 0.58f, 0.42f),
            Wall = C(0.92f, 0.88f, 0.80f), Roof = C(0.70f, 0.36f, 0.28f), Trim = C(0.96f, 0.95f, 0.92f),
            Accent = C(0.30f, 0.58f, 0.62f), Water = C(0.30f, 0.52f, 0.60f), KitTint = C(1f, 0.96f, 0.90f),
            Day = Light(C(1f, 0.92f, 0.78f), 1.25f, 56f, -28f,
                C(0.70f, 0.74f, 0.82f), C(0.68f, 0.62f, 0.56f), C(0.46f, 0.38f, 0.32f),
                C(0.92f, 0.84f, 0.74f), 0.02f, exposure: 0.05f, contrast: 8f),
            Dusk = Light(C(1f, 0.66f, 0.40f), 1.35f, 20f, -58f,
                C(0.52f, 0.46f, 0.62f), C(0.70f, 0.50f, 0.40f), C(0.34f, 0.26f, 0.22f),
                C(0.96f, 0.66f, 0.48f), 0.035f, bloom: 0.32f, lamps: true, glow: 2.2f, shadows: 0.5f,
                exposure: 0.15f, contrast: 10f, filter: C(1f, 0.94f, 0.86f), vignette: 0.2f),
            Storm = Light(C(0.78f, 0.76f, 0.80f), 0.6f, 58f, -20f,
                C(0.50f, 0.50f, 0.58f), C(0.48f, 0.44f, 0.46f), C(0.30f, 0.26f, 0.26f),
                C(0.54f, 0.50f, 0.52f), 0.065f, bloom: 0.25f, lamps: true, glow: 1.8f, shadows: 0.3f,
                exposure: 0.28f, contrast: 8f, saturation: -6f, filter: C(0.96f, 0.96f, 1f), vignette: 0.22f),
        };

        // ---- 3 · Pine Ridge: pines, rocks, a log cabin, distant peaks -------------------------------

        static BiomeDefinition PineRidge() => new BiomeDefinition
        {
            Id = Journey.IdOf(3), Name = Journey.NameOf(3), PropSet = "pine",
            Ground = C(0.54f, 0.62f, 0.48f),
            Apron = C(0.68f, 0.68f, 0.65f),
            Tile = C(0.80f, 0.78f, 0.72f),
            Road = C(0.30f, 0.31f, 0.33f),
            LaneMark = C(0.93f, 0.92f, 0.86f),
            Curb = C(0.62f, 0.58f, 0.52f),
            Foliage = C(0.24f, 0.46f, 0.36f), Grass = C(0.44f, 0.58f, 0.40f), Wood = C(0.52f, 0.38f, 0.28f),
            Rock = C(0.56f, 0.58f, 0.60f), Dirt = C(0.50f, 0.42f, 0.34f),
            Wall = C(0.62f, 0.44f, 0.30f), Roof = C(0.30f, 0.32f, 0.34f), Trim = C(0.90f, 0.88f, 0.82f),
            Accent = C(0.92f, 0.94f, 0.96f), Water = C(0.24f, 0.44f, 0.52f), KitTint = C(0.97f, 1f, 0.97f),
            Day = Light(C(1f, 0.97f, 0.90f), 1.15f, 50f, -34f,
                C(0.64f, 0.74f, 0.84f), C(0.56f, 0.62f, 0.58f), C(0.34f, 0.38f, 0.30f),
                C(0.74f, 0.82f, 0.86f), 0.022f, exposure: 0.12f),
            Dusk = Light(C(1f, 0.72f, 0.50f), 1.25f, 24f, -52f,
                C(0.46f, 0.46f, 0.64f), C(0.58f, 0.50f, 0.46f), C(0.26f, 0.26f, 0.24f),
                C(0.84f, 0.66f, 0.58f), 0.035f, bloom: 0.3f, lamps: true, glow: 2.2f, shadows: 0.5f,
                exposure: 0.18f, contrast: 10f, filter: C(1f, 0.95f, 0.90f), vignette: 0.2f),
            Storm = Light(C(0.70f, 0.76f, 0.86f), 0.55f, 62f, -14f,
                C(0.44f, 0.50f, 0.60f), C(0.40f, 0.45f, 0.48f), C(0.22f, 0.25f, 0.24f),
                C(0.40f, 0.47f, 0.53f), 0.07f, bloom: 0.25f, lamps: true, glow: 1.8f, shadows: 0.3f,
                exposure: 0.32f, contrast: 8f, saturation: -6f, filter: C(0.92f, 0.96f, 1f), vignette: 0.22f),
        };

        // ---- 4 · Metro City: storefronts, streetlights, crosswalks, lit windows ---------------------

        static BiomeDefinition MetroCity() => new BiomeDefinition
        {
            Id = Journey.IdOf(4), Name = Journey.NameOf(4), PropSet = "metro",
            Ground = C(0.74f, 0.74f, 0.75f),
            Apron = C(0.66f, 0.66f, 0.68f),
            Tile = C(0.81f, 0.80f, 0.78f),
            Road = C(0.26f, 0.27f, 0.30f),
            LaneMark = C(0.95f, 0.95f, 0.92f),
            Curb = C(0.84f, 0.84f, 0.83f),
            Foliage = C(0.36f, 0.56f, 0.40f), Grass = C(0.50f, 0.64f, 0.46f), Wood = C(0.54f, 0.42f, 0.32f),
            Rock = C(0.62f, 0.62f, 0.64f), Dirt = C(0.56f, 0.48f, 0.40f),
            Wall = C(0.82f, 0.78f, 0.72f), Roof = C(0.34f, 0.36f, 0.40f), Trim = C(0.94f, 0.94f, 0.92f),
            Accent = C(0.30f, 0.50f, 0.66f), Water = C(0.24f, 0.42f, 0.52f), KitTint = C(0.98f, 0.98f, 1f),
            Day = Light(C(1f, 0.97f, 0.90f), 1.15f, 54f, -30f,
                C(0.66f, 0.72f, 0.82f), C(0.60f, 0.61f, 0.63f), C(0.38f, 0.38f, 0.40f),
                C(0.78f, 0.82f, 0.88f), 0.02f, exposure: 0.1f),
            Dusk = Light(C(1f, 0.70f, 0.48f), 1.2f, 22f, -54f,
                C(0.42f, 0.42f, 0.62f), C(0.58f, 0.48f, 0.46f), C(0.24f, 0.22f, 0.26f),
                C(0.78f, 0.60f, 0.58f), 0.035f, bloom: 0.35f, lamps: true, glow: 2.6f, shadows: 0.5f,
                exposure: 0.18f, contrast: 12f, filter: C(1f, 0.95f, 0.92f), vignette: 0.2f),
            Storm = Light(C(0.68f, 0.74f, 0.86f), 0.5f, 62f, -16f,
                C(0.42f, 0.48f, 0.60f), C(0.38f, 0.42f, 0.50f), C(0.22f, 0.23f, 0.28f),
                C(0.38f, 0.44f, 0.54f), 0.07f, bloom: 0.3f, lamps: true, glow: 2.2f, shadows: 0.3f,
                exposure: 0.32f, contrast: 8f, saturation: -6f, filter: C(0.92f, 0.95f, 1f), vignette: 0.22f),
        };

        // ---- 5 · Frost Port: snow, ice, a lighthouse, warm windows ----------------------------------

        static BiomeDefinition FrostPort() => new BiomeDefinition
        {
            Id = Journey.IdOf(5), Name = Journey.NameOf(5), PropSet = "frost",
            Ground = C(0.90f, 0.93f, 0.96f),
            Apron = C(0.70f, 0.74f, 0.80f),
            Tile = C(0.80f, 0.84f, 0.90f),
            Road = C(0.36f, 0.40f, 0.47f),
            LaneMark = C(0.94f, 0.96f, 0.98f),
            Curb = C(0.80f, 0.84f, 0.88f),
            Foliage = C(0.50f, 0.64f, 0.62f), Grass = C(0.80f, 0.86f, 0.88f), Wood = C(0.50f, 0.40f, 0.34f),
            Rock = C(0.64f, 0.68f, 0.74f), Dirt = C(0.70f, 0.72f, 0.76f),
            Wall = C(0.86f, 0.80f, 0.72f), Roof = C(0.46f, 0.30f, 0.30f), Trim = C(0.96f, 0.97f, 0.98f),
            Accent = C(0.76f, 0.30f, 0.28f), Water = C(0.16f, 0.32f, 0.44f), KitTint = C(0.94f, 0.97f, 1f),
            Day = Light(C(0.96f, 0.97f, 1f), 1.05f, 44f, -32f,
                C(0.74f, 0.80f, 0.90f), C(0.66f, 0.70f, 0.76f), C(0.56f, 0.60f, 0.66f),
                // Snow sits near the bloom threshold: keep the day glow faint.
                C(0.84f, 0.89f, 0.95f), 0.022f, bloom: 0.06f, exposure: 0f, contrast: 8f),
            Dusk = Light(C(1f, 0.74f, 0.56f), 1.2f, 18f, -56f,
                C(0.46f, 0.48f, 0.68f), C(0.62f, 0.56f, 0.60f), C(0.40f, 0.40f, 0.48f),
                C(0.82f, 0.68f, 0.70f), 0.035f, bloom: 0.32f, lamps: true, glow: 2.4f, shadows: 0.5f,
                exposure: 0.12f, contrast: 10f, filter: C(1f, 0.96f, 0.94f), vignette: 0.2f),
            Storm = Light(C(0.74f, 0.80f, 0.92f), 0.5f, 60f, -16f,
                C(0.50f, 0.56f, 0.68f), C(0.46f, 0.50f, 0.58f), C(0.36f, 0.40f, 0.46f),
                C(0.52f, 0.58f, 0.66f), 0.075f, bloom: 0.25f, lamps: true, glow: 2f, shadows: 0.3f,
                exposure: 0.25f, contrast: 8f, saturation: -8f, filter: C(0.92f, 0.96f, 1f), vignette: 0.22f),
        };
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
