using System.Collections.Generic;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The area the environment dresses around: the part the camera frames (board and road, or the
    /// menu diorama) and the bands of §5 derived from it.
    /// </summary>
    public struct Stage
    {
        /// <summary>What the camera keeps in view; nothing is placed inside it.</summary>
        public Bounds Content;
        public float RoadZ;
        public float RoadHalfWidth;
        public bool Menu;

        /// <summary>Back band: behind the road, big pieces further back.</summary>
        public float BackNear => Content.max.z + 0.6f;
        public float BackFar => Content.max.z + 9f;
        /// <summary>The far curb of the road, where sidewalks and lamps start.</summary>
        public float FarCurb => RoadZ + RoadHalfWidth;
        /// <summary>Side gutters start here (|x|).</summary>
        public float GutterX => Content.max.x + 0.4f;
        /// <summary>Foreground band: in front of the board, low props only.</summary>
        public float FrontZ => Content.min.z - 0.8f;

        /// <summary>How far out to dress at depth z: the view widens toward the back.</summary>
        public float HalfSpanAt(float z) => Content.max.x + 1.6f + Mathf.Max(0f, z - Content.max.z) * 0.45f;
    }

    /// <summary>A Kenney model choice for a band: path, scale range and relative weight.</summary>
    public readonly struct Pick
    {
        public readonly string Path;
        public readonly float MinScale;
        public readonly float MaxScale;
        public readonly float Weight;
        /// <summary>Turn the model's front (+z) toward the camera, give or take a little.</summary>
        public readonly bool FaceCamera;

        public Pick(string path, float minScale, float maxScale, float weight = 1f, bool faceCamera = false)
        {
            Path = path;
            MinScale = minScale;
            MaxScale = maxScale;
            Weight = weight;
            FaceCamera = faceCamera;
        }
    }

    /// <summary>
    /// Placement helper for one environment build: a seeded random source, the occupied footprints,
    /// and shortcuts that drop geometry into the <see cref="StaticBatch"/>.
    /// </summary>
    public sealed class Dresser
    {
        /// <summary>
        /// Yaw that turns a city-kit building's shopfront toward the camera. In Unity the Kenney
        /// buildings' glazed fronts face -z already (cars, by contrast, drive toward +z).
        /// </summary>
        public const float FrontYaw = 0f;

        readonly List<Vector3> claims = new List<Vector3>();
        readonly List<Vector3> lamps = new List<Vector3>();

        public readonly System.Random Rng;
        public readonly StaticBatch Batch;
        public readonly EnvPalette Palette;
        public readonly Stage Stage;

        /// <summary>The landmark's focus point (for the arrival pan).</summary>
        public Vector3 Focus;
        /// <summary>Lantern / lamp positions that may get one of the few real point lights at dusk.</summary>
        public readonly List<Vector3> LightSpots = new List<Vector3>();
        /// <summary>Z of the shoreline when the biome has water, else +infinity.</summary>
        public float ShoreZ = float.PositiveInfinity;

        public Dresser(int seed, StaticBatch batch, EnvPalette palette, Stage stage)
        {
            Rng = new System.Random(seed);
            Batch = batch;
            Palette = palette;
            Stage = stage;
        }

        public IReadOnlyList<Vector3> Lamps => lamps;

        public float Range(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
        public bool Chance(float p) => Rng.NextDouble() < p;
        public int Index(int count) => Rng.Next(count);

        // ---- Footprints ---------------------------------------------------------------------------

        /// <summary>Marks a circle on the ground as taken.</summary>
        public void Claim(float x, float z, float radius) => claims.Add(new Vector3(x, z, radius));

        /// <summary>True when a circle is clear of claims, the content area, the road and the water.</summary>
        public bool IsFree(float x, float z, float radius)
        {
            var c = Stage.Content;
            if (x + radius > c.min.x - 0.1f && x - radius < c.max.x + 0.1f &&
                z + radius > c.min.z - 0.1f && z - radius < c.max.z + 0.1f) return false;
            if (z + radius > Stage.RoadZ - Stage.RoadHalfWidth - 0.2f && z - radius < Stage.FarCurb + 0.25f) return false;
            if (z + radius > ShoreZ - 0.2f) return false;
            foreach (var cl in claims)
            {
                float dx = cl.x - x, dz = cl.y - z, r = cl.z + radius;
                if (dx * dx + dz * dz < r * r) return false;
            }
            return true;
        }

        /// <summary>True when a circle overlaps no claimed footprint (ignores the content area and road).</summary>
        public bool IsFreeOfClaims(float x, float z, float radius)
        {
            foreach (var cl in claims)
            {
                float dx = cl.x - x, dz = cl.y - z, r = cl.z + radius;
                if (dx * dx + dz * dz < r * r) return false;
            }
            return true;
        }

        /// <summary>Picks a random free spot in a box of the ground plane.</summary>
        public bool TrySpot(float xMin, float xMax, float zMin, float zMax, float radius, out Vector3 spot, int tries = 14)
        {
            for (int i = 0; i < tries; i++)
            {
                float x = Range(xMin, xMax), z = Range(zMin, zMax);
                if (!IsFree(x, z, radius)) continue;
                spot = new Vector3(x, 0f, z);
                return true;
            }
            spot = default;
            return false;
        }

        // ---- Geometry -----------------------------------------------------------------------------

        public void Box(Material m, Vector3 center, Vector3 size, float yaw = 0f, float bevel = 0f, bool shadows = true) =>
            Batch.Add(MeshKit.Box(bevel), MeshKit.TRS(center, yaw, size), m, shadows);

        public void Box(Material m, Vector3 center, Vector3 size, Quaternion rotation, float bevel = 0f, bool shadows = true) =>
            Batch.Add(MeshKit.Box(bevel), Matrix4x4.TRS(center, rotation, size), m, shadows);

        /// <summary>A vertical cylinder standing on <paramref name="bottom"/>.</summary>
        public void Cylinder(Material m, Vector3 bottom, float radius, float height, int segments = 12, bool shadows = true) =>
            Batch.Add(MeshKit.Cylinder(segments),
                Matrix4x4.TRS(bottom + Vector3.up * height * 0.5f, Quaternion.identity, new Vector3(radius * 2f, height, radius * 2f)),
                m, shadows);

        /// <summary>A cone standing on <paramref name="bottom"/>.</summary>
        public void Cone(Material m, Vector3 bottom, float radius, float height, int segments = 10, bool shadows = true) =>
            Batch.Add(MeshKit.Cone(segments), Matrix4x4.TRS(bottom, Quaternion.identity, new Vector3(radius * 2f, height, radius * 2f)),
                m, shadows);

        /// <summary>A gable roof on <paramref name="bottom"/>; the ridge runs along z before <paramref name="yaw"/>.</summary>
        public void Roof(Material m, Vector3 bottom, Vector3 size, float yaw = 0f) =>
            Batch.Add(MeshKit.Prism(), MeshKit.TRS(bottom, yaw, size), m);

        /// <summary>A Kenney model at a ground position.</summary>
        public void Model(string path, Vector3 position, float yaw, float scale, bool shadows = true) =>
            Batch.Add(PropLibrary.Get(path), MeshKit.TRS(position, yaw, Vector3.one * scale), Palette, shadows);

        /// <summary>Places <paramref name="count"/> random picks in a box of the ground plane, keeping them apart.</summary>
        public void Scatter(Pick[] picks, int count, float xMin, float xMax, float zMin, float zMax,
            float spacing = 0.15f, bool shadows = true)
        {
            if (picks == null || picks.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                var pick = Choose(picks);
                var model = PropLibrary.Get(pick.Path);
                if (model == null) continue;
                float scale = Range(pick.MinScale, pick.MaxScale);
                float radius = model.Radius * scale + spacing;
                if (!TrySpot(xMin, xMax, zMin, zMax, radius, out var spot)) continue;
                Claim(spot.x, spot.z, radius);
                float yaw = pick.FaceCamera ? FrontYaw + Range(-10f, 10f) : Range(0f, 360f);
                Model(pick.Path, spot, yaw, scale, shadows);
            }
        }

        Pick Choose(Pick[] picks)
        {
            float total = 0f;
            foreach (var p in picks) total += p.Weight;
            float r = Range(0f, total);
            foreach (var p in picks)
            {
                r -= p.Weight;
                if (r <= 0f) return p;
            }
            return picks[picks.Length - 1];
        }

        // ---- Shared set pieces --------------------------------------------------------------------

        /// <summary>A streetlight: dark pole and arm, a lamp head that glows when the lamps are on.</summary>
        public void Streetlight(Vector3 foot, bool armTowardRoad = true)
        {
            var pal = Palette;
            float dir = armTowardRoad ? -1f : 1f;
            Cylinder(pal.Dark, foot, 0.06f, 2.1f, 6);
            Box(pal.Dark, foot + new Vector3(0f, 2.06f, dir * 0.22f), new Vector3(0.07f, 0.07f, 0.5f));
            var head = foot + new Vector3(0f, 1.98f, dir * 0.44f);
            Box(pal.Dark, head + Vector3.up * 0.06f, new Vector3(0.22f, 0.07f, 0.28f));
            Box(pal.Glow, head, new Vector3(0.18f, 0.06f, 0.22f), 0f, 0f, false);
            Claim(foot.x, foot.z, 0.3f);
            lamps.Add(head);
        }

        /// <summary>A parked Car Kit vehicle along the road, nose along ±x.</summary>
        public void ParkedVehicle(string path, Vector3 spot, bool facingRight)
        {
            var model = PropLibrary.Get(path);
            if (model == null) return;
            Model(path, spot, facingRight ? 90f : -90f, 1f);
            Claim(spot.x - 0.9f, spot.z, 0.85f);
            Claim(spot.x + 0.9f, spot.z, 0.85f);
        }
    }
}
