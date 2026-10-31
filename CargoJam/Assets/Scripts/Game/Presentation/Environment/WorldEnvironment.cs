using System.Collections;
using System.Collections.Generic;
using CargoJam.Meta;
using UnityEngine;
using UnityEngine.Rendering;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The scenery, lighting and atmosphere around the board and on the home screen. Lives outside
    /// the level root (it survives level rebuilds). When the stop or mood changes it relights
    /// everything (sun, trilight ambient, fog, post-processing, lamps); every show re-places the props
    /// from the level's seed and recombines them into one mesh per material.
    /// </summary>
    public sealed class WorldEnvironment : MonoBehaviour
    {
        const int SeedPrime = 7919;
        const float GroundY = -0.012f;
        const float WaterY = -0.45f;
        const float GroundHalfSize = 60f;
        /// <summary>At most this many real point lights (dusk and storm only); the rest of the glow is emissive.</summary>
        const int MaxPointLights = 2;
        static readonly Color LampLight = new Color(1f, 0.76f, 0.48f);

        Light sun;
        Camera cam;
        MaterialBank materials;
        int lastStop = -1;
        Mood lastMood;
        StaticBatch batch;
        PostFx post;
        readonly Light[] pointLights = new Light[MaxPointLights];
        readonly Dictionary<string, EnvPalette> palettes = new Dictionary<string, EnvPalette>();

        LightingPreset lighting;
        float fogFarZ;
        bool fogDirty;
        Vector3 fogCamPosition;
        Quaternion fogCamRotation;

        /// <summary>World-space points the arrival reveal pans the camera across (back band, left to right).</summary>
        public Vector3[] RevealPoints { get; private set; } = new Vector3[0];

        /// <summary>The biome on show (null before the first show call).</summary>
        public BiomeDefinition Biome { get; private set; }

        /// <summary>What the last show call returned.</summary>
        public EnvironmentInfo Current { get; private set; }

        public void Init(Light sun, Camera camera, MaterialBank materials)
        {
            this.sun = sun;
            cam = camera;
            this.materials = materials;
            batch ??= new StaticBatch(transform);
            post ??= new PostFx(transform);
        }

        /// <summary>Shows the environment for a level whose board uses <paramref name="layout"/>.</summary>
        public EnvironmentInfo ShowLevel(int levelIndex, Layout layout) => Show(levelIndex, new Stage
        {
            Content = layout.ContentBounds,
            RoadZ = Layout.RoadZ,
            RoadHalfWidth = Scenery.RoadHalfWidth,
            Menu = false,
        });

        /// <summary>Shows the home-screen diorama for the destination of <paramref name="levelIndex"/>.</summary>
        public EnvironmentInfo ShowMenu(int levelIndex) => Show(levelIndex, new Stage
        {
            Content = MenuDiorama.Bounds,
            RoadZ = MenuDiorama.RoadZ,
            RoadHalfWidth = Scenery.RoadHalfWidth,
            Menu = true,
        });

        /// <summary>Removes everything this environment built.</summary>
        public void Clear()
        {
            batch?.Destroy();
            foreach (var l in pointLights) if (l != null) l.enabled = false;
            RenderSettings.fog = false;
            RevealPoints = new Vector3[0];
            lastStop = -1;
        }

        /// <summary>
        /// Loads the Kenney models a level's destination dresses with, one per frame, so its first
        /// visit doesn't stall in the frame the arrival pan starts. Call it a few seconds ahead
        /// (the controller does on the win before an arrival).
        /// </summary>
        public void Preload(int levelIndex)
        {
            var set = PropSets.For(Biomes.For(Journey.StopFor(levelIndex)).PropSet);
            StartCoroutine(PreloadModels(set));
        }

        static IEnumerator PreloadModels(PropSet set)
        {
            foreach (var picks in new[] { set.Mid, set.Far, set.Gutter })
            {
                if (picks == null) continue;
                foreach (var pick in picks)
                {
                    PropLibrary.Get(pick.Path);
                    yield return null;
                }
            }
            foreach (var path in set.Parked)
            {
                PropLibrary.Get(path);
                yield return null;
            }
        }

        void OnDestroy()
        {
            batch?.Destroy();
            post?.Destroy();
            foreach (var p in palettes.Values) p.Destroy();
            palettes.Clear();
        }

        EnvironmentInfo Show(int levelIndex, Stage stage)
        {
            int stop = Journey.StopFor(levelIndex);
            var mood = Journey.MoodFor(levelIndex);
            bool changed = stop != lastStop || mood != lastMood;
            lastStop = stop;
            lastMood = mood;

            var biome = Biomes.For(stop);
            Biome = biome;
            lighting = biome.Lighting(mood);
            var set = PropSets.For(biome.PropSet);
            var palette = PaletteFor(biome);
            if (changed) ApplyLook(biome, palette, set);
            Dress(levelIndex, stage, set, palette);

            var info = new EnvironmentInfo
            {
                LevelIndex = levelIndex,
                Stop = stop,
                Mood = mood,
                Biome = biome,
                Changed = changed,
                Arrival = Journey.IsArrival(levelIndex),
            };
            Current = info;
            return info;
        }

        EnvPalette PaletteFor(BiomeDefinition biome)
        {
            if (!palettes.TryGetValue(biome.Id, out var p))
            {
                p = new EnvPalette(materials, biome);
                palettes[biome.Id] = p;
            }
            return p;
        }

        // ---- Look: light, ambient, fog, post, lamps ----------------------------------------------

        void ApplyLook(BiomeDefinition biome, EnvPalette palette, PropSet set)
        {
            var l = lighting;
            materials.SetBiome(biome, l);
            palette.SetMood(l);

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(l.SunAngles.x, l.SunAngles.y, 0f);
                sun.color = l.Sun;
                sun.intensity = l.SunIntensity;
                sun.shadowStrength = l.ShadowStrength;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = l.AmbientSky;
            RenderSettings.ambientEquatorColor = l.AmbientEquator;
            RenderSettings.ambientGroundColor = l.AmbientGround;

            RenderSettings.fog = l.FogDensity > 0f;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = l.Fog;
            fogDirty = true;

            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = l.Sky;
            }
            post?.Apply(l);
        }

        /// <summary>
        /// Linear fog that starts just past the board's far edge (so the board itself never gets
        /// fogged) and is full 1 / FogDensity units further back. Recomputed when the camera moves.
        /// </summary>
        void LateUpdate()
        {
            if (cam == null || !RenderSettings.fog) return;
            var t = cam.transform;
            if (!fogDirty && t.position == fogCamPosition && t.rotation == fogCamRotation) return;
            fogDirty = false;
            fogCamPosition = t.position;
            fogCamRotation = t.rotation;
            float depth = Vector3.Dot(new Vector3(0f, 0f, fogFarZ) - t.position, t.forward);
            float start = Mathf.Max(0f, depth + 0.5f);
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = start + 1f / Mathf.Max(0.001f, lighting.FogDensity);
        }

        // ---- Props --------------------------------------------------------------------------------

        void Dress(int levelIndex, Stage stage, PropSet set, EnvPalette palette)
        {
            batch.Clear();
            var d = new Dresser(levelIndex * SeedPrime, batch, palette, stage);
            if (set.HasWater) d.ShoreZ = stage.BackNear + set.ShoreOffset;
            Terrain(d);
            PropSets.Dress(set, d);
            batch.Build();

            fogFarZ = stage.Content.max.z;
            fogDirty = true;
            RevealPoints = Reveal(d);
            PlaceLights(d);
        }

        /// <summary>The ground plane (cut at the shoreline), water and quay wall, and the far sidewalk.</summary>
        void Terrain(Dresser d)
        {
            var s = d.Stage;
            float far = float.IsInfinity(d.ShoreZ) ? GroundHalfSize : d.ShoreZ;
            float near = -GroundHalfSize;
            var ground = MeshKit.Quad();
            batch.Add(ground, Matrix4x4.TRS(new Vector3(0f, GroundY, (near + far) * 0.5f), Quaternion.identity,
                new Vector3(GroundHalfSize * 2f, 1f, far - near)), materials.Ground, false);

            if (!float.IsInfinity(d.ShoreZ))
            {
                batch.Add(ground, Matrix4x4.TRS(new Vector3(0f, WaterY, d.ShoreZ + GroundHalfSize * 0.5f), Quaternion.identity,
                    new Vector3(GroundHalfSize * 2f, 1f, GroundHalfSize)), materials.Water, false);
                // Quay wall: the land's edge dropping to the water.
                d.Box(materials.Curb, new Vector3(0f, (GroundY + WaterY) * 0.5f - 0.1f, d.ShoreZ - 0.12f),
                    new Vector3(GroundHalfSize * 2f, -WaterY + 0.2f, 0.24f), 0f, 0f, false);
                d.Box(d.Palette.Solid("quay cap", Color.Lerp(d.Palette.Biome.Curb, Color.white, 0.25f)),
                    new Vector3(0f, 0.02f, d.ShoreZ - 0.18f), new Vector3(GroundHalfSize * 2f, 0.05f, 0.36f), 0f, 0f, false);
            }

            // A raised sidewalk along the far side of the road.
            d.Box(materials.Curb, new Vector3(0f, 0.025f, s.FarCurb + 0.32f), new Vector3(GroundHalfSize * 2f, 0.05f, 0.64f), 0f, 0f, false);
        }

        /// <summary>2–4 points across the back band, left to right, with the landmark among them.</summary>
        static Vector3[] Reveal(Dresser d)
        {
            var s = d.Stage;
            float z = s.BackNear + 3f;
            float half = Mathf.Min(s.HalfSpanAt(z) - 1f, 5f);
            var points = new List<Vector3>
            {
                new Vector3(-half, 1.4f, z),
                new Vector3(half, 1.4f, z),
            };
            if (d.Focus != Vector3.zero) points.Add(d.Focus);
            else points.Add(new Vector3(0f, 1.4f, z + 1f));
            points.Sort((a, b) => a.x.CompareTo(b.x));
            return points.ToArray();
        }

        /// <summary>At dusk and in storms, real warm lights at the landmark and the lamp nearest the bay.</summary>
        void PlaceLights(Dresser d)
        {
            int used = 0;
            if (lighting.LampsOn)
            {
                // The landmark's own light (lantern, window, canopy), then the streetlights nearest
                // to where the trucks load.
                if (d.LightSpots.Count > 0) SetPointLight(used++, d.LightSpots[0], 2.2f, 6f);
                float bayX = d.Stage.Menu ? 0f : Layout.BayX;
                int taken = -1;
                while (used < MaxPointLights)
                {
                    int best = -1;
                    float bestDistance = float.MaxValue;
                    for (int i = 0; i < d.Lamps.Count; i++)
                    {
                        float dist = Mathf.Abs(d.Lamps[i].x - bayX);
                        if (i != taken && dist < bestDistance) { bestDistance = dist; best = i; }
                    }
                    if (best < 0) break;
                    SetPointLight(used++, d.Lamps[best] + Vector3.down * 0.2f, 1.6f, 4.5f);
                    taken = best;
                }
            }
            for (int i = used; i < MaxPointLights; i++)
                if (pointLights[i] != null) pointLights[i].enabled = false;
        }

        void SetPointLight(int index, Vector3 position, float intensity, float range)
        {
            var l = pointLights[index];
            if (l == null)
            {
                var go = new GameObject($"Lamp Light {index}");
                go.transform.SetParent(transform, false);
                l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                pointLights[index] = l;
            }
            l.transform.position = position;
            l.color = LampLight;
            l.intensity = intensity * Mathf.Clamp01(lighting.Glow / 2.2f + 0.2f);
            l.range = range;
            l.enabled = true;
        }
    }
}
