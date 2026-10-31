using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The code-built set piece of each destination, placed first in the back band. Sizes are in
    /// world units (a parcel is ~0.8, a truck ~3 long and ~1.2 tall). Each sets the dresser's
    /// <see cref="Dresser.Focus"/> for the arrival pan.
    /// </summary>
    public static class Landmarks
    {
        /// <summary>Landmark x: off to a seeded side during levels, near the middle on the menu.</summary>
        static float SideX(Dresser d, float min, float max) =>
            d.Stage.Menu ? d.Range(-0.6f, 0.6f) : (d.Chance(0.5f) ? -1f : 1f) * d.Range(min, max);

        static readonly Color[] ContainerColors =
        {
            new Color(0.66f, 0.36f, 0.30f), new Color(0.34f, 0.46f, 0.60f), new Color(0.30f, 0.55f, 0.53f),
            new Color(0.80f, 0.70f, 0.52f), new Color(0.60f, 0.62f, 0.66f), new Color(0.74f, 0.52f, 0.30f),
        };

        static Material Container(Dresser d, int i) =>
            d.Palette.Solid($"container {i}", ContainerColors[i % ContainerColors.Length]);

        // ---- Harbor Town: gantry cranes on the quay, container stacks, a moored ship ----------------

        public static void Harbor(Dresser d)
        {
            var s = d.Stage;
            float shore = d.ShoreZ;
            float ax = SideX(d, 0.4f, 1.8f);
            Crane(d, new Vector3(ax - 2.7f, 0f, shore - 0.8f));
            Crane(d, new Vector3(ax + 2.7f, 0f, shore - 0.8f));

            // One row of container stacks between the road and the cranes.
            float rowZ = s.BackNear + 1.25f;
            for (float x = ax - 6.6f; x <= ax + 6.6f; x += 2.2f)
            {
                if (d.Chance(0.2f) || !d.IsFree(x, rowZ, 0.95f)) continue;
                int height = 1 + d.Index(3);
                for (int k = 0; k < height; k++)
                {
                    d.Box(Container(d, d.Index(ContainerColors.Length)),
                        new Vector3(x + d.Range(-0.06f, 0.06f), 0.42f + k * 0.84f, rowZ),
                        new Vector3(2f, 0.82f, 0.82f), d.Range(-2f, 2f), 0.025f);
                }
                d.Claim(x, rowZ, 1.05f);
            }

            // Bollards along the quay edge.
            for (float x = -14f; x <= 14f; x += 1.8f)
                d.Cylinder(d.Palette.Dark, new Vector3(x, 0f, shore - 0.25f), 0.1f, 0.22f, 6, false);

            Ship(d, new Vector3(ax + d.Range(-1.5f, 1.5f), 0f, shore + 2.2f));
            d.Focus = new Vector3(ax, 2.2f, shore - 0.8f);
        }

        /// <summary>A ship-to-shore crane: portal legs on the quay, the boom reaching out over the water (+z).</summary>
        static void Crane(Dresser d, Vector3 c)
        {
            var m = d.Palette.Accent;
            var dark = d.Palette.Dark;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                d.Box(m, c + new Vector3(sx * 0.8f, 1.6f, sz * 0.65f), new Vector3(0.16f, 3.2f, 0.16f));
            for (int sz = -1; sz <= 1; sz += 2)
            {
                d.Box(m, c + new Vector3(0f, 3.25f, sz * 0.65f), new Vector3(1.8f, 0.2f, 0.2f));
                d.Box(m, c + new Vector3(0f, 0.9f, sz * 0.65f), new Vector3(1.6f, 0.1f, 0.1f));
            }
            for (int sx = -1; sx <= 1; sx += 2)
                d.Box(m, c + new Vector3(sx * 0.8f, 3.25f, 0f), new Vector3(0.2f, 0.2f, 1.5f));
            d.Box(m, c + new Vector3(0f, 3.5f, 1.6f), new Vector3(0.32f, 0.26f, 6.4f));
            d.Box(dark, c + new Vector3(0f, 3.55f, -1.5f), new Vector3(0.9f, 0.5f, 0.7f));
            d.Box(d.Palette.Trim, c + new Vector3(0f, 3.62f, -0.35f), new Vector3(0.7f, 0.45f, 0.9f));
            d.Box(d.Palette.Trim, c + new Vector3(0.25f, 3.1f, 0.9f), new Vector3(0.42f, 0.36f, 0.42f));
            d.Box(dark, c + new Vector3(0f, 2.62f, 3.2f), new Vector3(0.04f, 1.6f, 0.04f), 0f, 0f, false);
            d.Box(dark, c + new Vector3(0f, 1.8f, 3.2f), new Vector3(0.9f, 0.08f, 0.32f));
            d.Claim(c.x, c.z, 1.25f);
        }

        static void Ship(Dresser d, Vector3 p)
        {
            var pal = d.Palette;
            var hull = pal.Solid("hull", new Color(0.24f, 0.30f, 0.40f));
            var bottom = pal.Solid("hull bottom", new Color(0.60f, 0.30f, 0.28f));
            d.Box(bottom, p + new Vector3(0f, -0.42f, 0f), new Vector3(6.9f, 0.3f, 1.7f), 0f, 0.02f, false);
            d.Box(hull, p + new Vector3(0f, 0.05f, 0f), new Vector3(7.2f, 0.7f, 1.8f), 0f, 0.03f);
            for (int i = 0; i < 4; i++)
            for (int side = -1; side <= 1; side += 2)
            {
                if (d.Chance(0.25f)) continue;
                d.Box(Container(d, d.Index(ContainerColors.Length)),
                    p + new Vector3(-1.2f + i * 1.12f, 0.68f, side * 0.42f), new Vector3(1.05f, 0.56f, 0.8f), 0f, 0.03f);
            }
            d.Box(pal.Trim, p + new Vector3(-2.9f, 0.95f, 0f), new Vector3(1f, 1.1f, 1.5f), 0f, 0.02f);
            d.Box(pal.Glow, p + new Vector3(-2.9f, 1.25f, -0.76f), new Vector3(0.8f, 0.16f, 0.02f), 0f, 0f, false);
            d.Cylinder(pal.Accent, p + new Vector3(-2.9f, 1.5f, 0.3f), 0.18f, 0.6f, 10);
            d.LightSpots.Add(p + new Vector3(-2.9f, 1.4f, -1.2f));
        }

        // ---- Farmlands: a red barn and silo, a crop field, hay bales, a fence --------------------

        public static void Farm(Dresser d)
        {
            var s = d.Stage;
            var pal = d.Palette;
            float ax = SideX(d, 1.4f, 2.8f);
            float side = ax >= 0f ? 1f : -1f;
            var c = new Vector3(ax, 0f, s.BackNear + 2.8f);

            // Barn: red walls and gables, dark roof, white door frame with an X brace, glowing loft window.
            GableHouse(d, c, 0f, 3f, 1.9f, 2.4f, 1.2f, pal.Wall, pal.Roof);
            var door = pal.Solid("barn door", d.Palette.Biome.Wall * 0.72f);
            d.Box(pal.Trim, c + new Vector3(0f, 0.76f, -1.21f), new Vector3(1.5f, 1.52f, 0.04f));
            d.Box(door, c + new Vector3(0f, 0.7f, -1.235f), new Vector3(1.24f, 1.32f, 0.04f));
            d.Box(pal.Trim, c + new Vector3(0f, 0.7f, -1.26f), new Vector3(1.66f, 0.09f, 0.03f), Quaternion.Euler(0f, 0f, 46f));
            d.Box(pal.Trim, c + new Vector3(0f, 0.7f, -1.26f), new Vector3(1.66f, 0.09f, 0.03f), Quaternion.Euler(0f, 0f, -46f));
            d.Box(pal.Trim, c + new Vector3(0f, 2.3f, -1.215f), new Vector3(0.6f, 0.5f, 0.04f));
            d.Box(pal.Glow, c + new Vector3(0f, 2.3f, -1.24f), new Vector3(0.44f, 0.34f, 0.03f), 0f, 0f, false);
            d.Claim(c.x, c.z, 2f);
            d.LightSpots.Add(c + new Vector3(0f, 2.3f, -1.6f));

            // Silo on the outer side.
            var silo = c + new Vector3(side * 2.4f, 0f, 0.5f);
            d.Cylinder(pal.Metal, silo, 0.62f, 3f, 14);
            d.Cone(pal.Roof, silo + Vector3.up * 3f, 0.66f, 0.55f, 14);
            d.Claim(silo.x, silo.z, 0.8f);

            // A crop field on the inner side.
            var field = new Vector3(ax - side * 3.9f, 0f, s.BackNear + 1.9f);
            if (d.IsFree(field.x, field.z, 1.6f))
            {
                d.Box(pal.Solid("field", d.Palette.Biome.Dirt), field + Vector3.up * 0.015f, new Vector3(4f, 0.03f, 2.4f), 0f, 0f, false);
                string crop = d.Chance(0.5f) ? "Nature/crops_wheatStageB" : "Nature/crops_cornStageD";
                float cropScale = crop.EndsWith("D") ? 0.95f : 1.25f;
                for (int row = 0; row < 3; row++)
                for (int k = 0; k < 5; k++)
                    d.Model(crop, field + new Vector3(-1.6f + k * 0.8f, 0.03f, -0.75f + row * 0.75f), d.Range(-15f, 15f), cropScale);
                d.Claim(field.x - 1f, field.z, 1.3f);
                d.Claim(field.x + 1f, field.z, 1.3f);
            }

            // Round hay bales lying on their sides.
            var hay = pal.Solid("hay", new Color(0.88f, 0.76f, 0.48f));
            for (int i = 0; i < 6; i++)
            {
                if (!d.TrySpot(-s.HalfSpanAt(s.BackNear + 3f), s.HalfSpanAt(s.BackNear + 3f), s.BackNear + 0.4f, s.BackNear + 4.5f, 0.5f, out var spot)) continue;
                d.Claim(spot.x, spot.z, 0.5f);
                d.Batch.Add(MeshKit.Cylinder(14),
                    Matrix4x4.TRS(spot + Vector3.up * 0.4f, Quaternion.Euler(0f, d.Range(0f, 180f), 90f), new Vector3(0.8f, 0.58f, 0.8f)), hay);
            }

            // A fence along the front of the farm.
            float fz = s.BackNear + 0.35f;
            for (float x = ax - 4.8f; x <= ax + 4.8f; x += 1.2f)
                if (d.IsFree(x, fz, 0.3f)) d.Model("Nature/fence_simple", new Vector3(x, 0f, fz + 0.55f), 180f, 1.2f);

            d.Focus = c + Vector3.up * 1.6f;
        }

        // ---- Canyon Highway: a gas station, red mesas on the horizon ------------------------------

        public static void Canyon(Dresser d)
        {
            var s = d.Stage;
            var pal = d.Palette;
            float ax = SideX(d, 1.2f, 2.6f);
            var c = new Vector3(ax, 0f, s.BackNear + 2f);
            GasStation(d, c);

            // Mesas in the back half of the band: the camera's view ends ~9 units behind the road,
            // so anything further back would never show during a level.
            var band = pal.Solid("rock band", Color.Lerp(pal.Biome.Rock, new Color(0.95f, 0.82f, 0.68f), 0.35f));
            float side = ax >= 0f ? 1f : -1f;
            float mz = s.BackNear + 4.4f;
            Mesa(d, band, new Vector3(-side * d.Range(3f, 4.5f), 0f, mz + d.Range(0f, 1f)), d.Range(4f, 5.5f), d.Range(2.4f, 3.2f));
            Mesa(d, band, new Vector3(side * d.Range(6f, 7f), 0f, mz + d.Range(0.5f, 1.5f)), d.Range(4f, 5.5f), d.Range(2.6f, 3.4f));
            Mesa(d, band, new Vector3(d.Range(-1f, 1f), 0f, mz + d.Range(2.6f, 3.4f)), d.Range(5f, 6.5f), d.Range(3f, 3.8f));
            d.Focus = c + Vector3.up * 1.2f;
        }

        /// <summary>A stepped mesa: three slabs, each narrower, alternating rock and a paler band.</summary>
        static void Mesa(Dresser d, Material band, Vector3 p, float width, float height)
        {
            float[] h = { 0.42f, 0.34f, 0.24f };
            float[] w = { 1f, 0.8f, 0.64f };
            float depth = width * 0.6f;
            float y = 0f;
            for (int i = 0; i < 3; i++)
            {
                float hi = height * h[i];
                d.Box(i == 1 ? band : d.Palette.Rock, p + new Vector3(d.Range(-0.3f, 0.3f), y + hi * 0.5f, d.Range(-0.2f, 0.2f)),
                    new Vector3(width * w[i], hi, depth * w[i]), d.Range(-6f, 6f), 0.02f);
                y += hi;
            }
            d.Claim(p.x, p.z, width * 0.5f);
        }

        static void GasStation(Dresser d, Vector3 c)
        {
            var pal = d.Palette;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                d.Box(pal.Trim, c + new Vector3(sx * 1.4f, 0.8f, sz * 0.8f), new Vector3(0.14f, 1.6f, 0.14f));
            d.Box(pal.Trim, c + new Vector3(0f, 1.7f, 0f), new Vector3(3.4f, 0.16f, 2.2f));
            d.Box(pal.Accent, c + new Vector3(0f, 1.58f, 0f), new Vector3(3.48f, 0.14f, 2.28f));
            d.Box(pal.Glow, c + new Vector3(0f, 1.505f, 0f), new Vector3(2.8f, 0.02f, 1.6f), 0f, 0f, false);
            d.Box(pal.Curb, c + new Vector3(0f, 0.04f, 0f), new Vector3(2.3f, 0.08f, 0.55f));
            for (int sx = -1; sx <= 1; sx += 2)
            {
                d.Box(pal.Accent, c + new Vector3(sx * 0.7f, 0.38f, 0f), new Vector3(0.3f, 0.6f, 0.22f), 0f, 0.05f);
                d.Box(pal.Trim, c + new Vector3(sx * 0.7f, 0.7f, 0f), new Vector3(0.32f, 0.06f, 0.24f));
            }
            // The shop behind the canopy, with a lit window band.
            var shop = c + new Vector3(0f, 0f, 2.1f);
            d.Box(pal.Wall, shop + new Vector3(0f, 0.65f, 0f), new Vector3(3f, 1.3f, 1.6f), 0f, 0.01f);
            d.Box(pal.Roof, shop + new Vector3(0f, 1.36f, 0f), new Vector3(3.2f, 0.12f, 1.8f));
            d.Box(pal.Glow, shop + new Vector3(-0.3f, 0.75f, -0.81f), new Vector3(1.8f, 0.42f, 0.03f), 0f, 0f, false);
            d.Box(pal.Dark, shop + new Vector3(1.05f, 0.45f, -0.81f), new Vector3(0.5f, 0.9f, 0.03f));
            // Price sign on a pole.
            var pole = c + new Vector3(2.3f, 0f, -0.7f);
            d.Box(pal.Dark, pole + new Vector3(0f, 1.3f, 0f), new Vector3(0.12f, 2.6f, 0.12f));
            d.Box(pal.Accent, pole + new Vector3(0f, 2.75f, 0f), new Vector3(0.9f, 0.7f, 0.14f), 0f, 0.04f);
            d.Box(pal.Glow, pole + new Vector3(0f, 2.75f, -0.08f), new Vector3(0.7f, 0.5f, 0.02f), 0f, 0f, false);
            d.Claim(c.x, c.z, 1.9f);
            d.Claim(shop.x, shop.z, 1.7f);
            d.Claim(pole.x, pole.z, 0.5f);
            d.LightSpots.Add(c + new Vector3(0f, 1.3f, 0f));
        }

        // ---- Pine Ridge: a log cabin among pines, snowy peaks beyond ------------------------------

        public static void Pine(Dresser d)
        {
            var s = d.Stage;
            var pal = d.Palette;
            float ax = SideX(d, 1.4f, 3f);
            var c = new Vector3(ax, 0f, s.BackNear + 2.3f);

            // The cabin's ridge runs along x, so its long side and roof face the road.
            GableHouse(d, c, 90f, 1.9f, 1.25f, 2.6f, 0.85f, pal.Wall, pal.Roof);
            var logs = pal.Solid("logs", pal.Biome.Wall * 0.72f);
            for (float y = 0.18f; y < 1.2f; y += 0.24f)
                d.Box(logs, c + new Vector3(0f, y, -0.955f), new Vector3(2.62f, 0.05f, 0.03f), 0f, 0f, false);
            d.Box(pal.Dark, c + new Vector3(0.6f, 0.43f, -0.97f), new Vector3(0.5f, 0.86f, 0.04f));
            d.Box(pal.Trim, c + new Vector3(-0.5f, 0.72f, -0.965f), new Vector3(0.52f, 0.44f, 0.04f));
            d.Box(pal.Glow, c + new Vector3(-0.5f, 0.72f, -0.99f), new Vector3(0.4f, 0.32f, 0.03f), 0f, 0f, false);
            d.Box(pal.Rock, c + new Vector3(-0.85f, 1.7f, 0.3f), new Vector3(0.32f, 1.1f, 0.32f));
            d.Model("Nature/log_stackLarge", c + new Vector3(1.85f, 0f, -0.3f), 90f, 1.7f);
            d.Claim(c.x, c.z, 1.7f);
            d.Claim(c.x + 1.85f, c.z - 0.3f, 0.7f);
            d.LightSpots.Add(c + new Vector3(-0.5f, 0.8f, -1.4f));

            // "Distant" peaks with snow caps at the back of the band (the view ends ~9 units behind the
            // road, so they sit just inside it; their flanks fill the top of the screen).
            var peak = pal.Solid("peak", Color.Lerp(pal.Biome.Rock, new Color(0.42f, 0.48f, 0.58f), 0.5f));
            float pz = s.BackNear + 6f;
            Peak(d, peak, new Vector3(-6.5f + d.Range(-1f, 1f), 0f, pz + d.Range(0f, 1.5f)), d.Range(4f, 5f), d.Range(4.5f, 5.5f));
            Peak(d, peak, new Vector3(d.Range(-1.5f, 2f), 0f, pz + d.Range(1.5f, 2.5f)), d.Range(4.5f, 5.5f), d.Range(5.5f, 6.5f));
            Peak(d, peak, new Vector3(6.5f + d.Range(-1f, 1f), 0f, pz + d.Range(0f, 1.5f)), d.Range(4f, 5f), d.Range(4.5f, 5.5f));
            d.Focus = c + Vector3.up * 1.2f;
        }

        static void Peak(Dresser d, Material rock, Vector3 p, float radius, float height)
        {
            const float cap = 0.36f;
            d.Cone(rock, p, radius, height, 7, false);
            d.Cone(d.Palette.Snow, p + Vector3.up * (height * (1f - cap) - 0.02f), radius * cap * 1.04f, height * cap + 0.05f, 7, false);
        }

        // ---- Metro City: storefronts along the back sidewalk, towers behind, a crosswalk ----------

        static readonly string[] Shops =
        {
            "Commercial/building-a", "Commercial/building-b", "Commercial/building-c", "Commercial/building-d",
            "Commercial/building-e", "Commercial/building-f", "Commercial/building-g", "Commercial/building-h",
        };

        static readonly string[] Towers =
        {
            "Commercial/building-skyscraper-a", "Commercial/building-skyscraper-b", "Commercial/building-skyscraper-c",
            "Commercial/building-skyscraper-e",
        };

        public static void Metro(Dresser d)
        {
            var s = d.Stage;
            float front = s.BackNear + 0.9f;
            Row(d, Shops, front, s.HalfSpanAt(front + 2f) + 1.5f, 2.9f, 3.1f, 0.15f, 0.6f);
            // Towers just behind, so their lower floors still show above the shops.
            Row(d, Towers, front + 3.4f, s.HalfSpanAt(front + 5f) + 2f, 2.2f, 2.7f, 0.5f, 1.3f);

            // A crosswalk over the road, clear of the loading bay.
            float cx = s.Menu ? 3.4f : Layout.BayX + 4.4f;
            float z0 = s.RoadZ - s.RoadHalfWidth + 0.22f, step = (s.RoadHalfWidth * 2f - 0.44f) / 4f;
            for (int i = 0; i < 5; i++)
                d.Box(d.Palette.LaneMark, new Vector3(cx, 0.006f, z0 + i * step), new Vector3(1f, 0.012f, 0.18f), 0f, 0f, false);

            d.Focus = new Vector3(0f, 2.6f, front + 1.5f);
        }

        /// <summary>A row of city-kit buildings side by side, fronts toward the road.</summary>
        static void Row(Dresser d, string[] paths, float frontZ, float half, float minScale, float maxScale, float minGap, float maxGap)
        {
            float x = -half + d.Range(0f, 1f);
            for (int guard = 0; guard < 24 && x < half; guard++)
            {
                var path = paths[d.Index(paths.Length)];
                var model = PropLibrary.Get(path);
                if (model == null) break;
                float scale = d.Range(minScale, maxScale);
                float w = model.Bounds.size.x * scale, depth = model.Bounds.size.z * scale;
                var center = new Vector3(x + w * 0.5f, 0f, frontZ + depth * 0.5f);
                d.Model(path, center, Dresser.FrontYaw, scale);
                d.Claim(center.x, center.z, Mathf.Max(w, depth) * 0.5f);
                x += w + d.Range(minGap, maxGap);
            }
        }

        // ---- Frost Port: a lighthouse on the quay, ice floes, snug cottages ------------------------

        static readonly Pick[] Cottages =
        {
            new Pick("Suburban/building-type-a", 2.2f, 2.5f, 1f, true), new Pick("Suburban/building-type-c", 2.2f, 2.5f, 1f, true),
            new Pick("Suburban/building-type-e", 2.2f, 2.5f, 1f, true), new Pick("Suburban/building-type-d", 2f, 2.3f, 1f, true),
        };

        public static void Frost(Dresser d)
        {
            var s = d.Stage;
            var pal = d.Palette;
            float shore = d.ShoreZ;
            float ax = d.Stage.Menu ? 2.8f : (d.Chance(0.5f) ? -1f : 1f) * d.Range(2.2f, 3.4f);
            var c = new Vector3(ax, 0f, shore - 0.9f);
            Lighthouse(d, c);

            for (int i = 0; i < 8; i++)
            {
                var floe = new Vector3(d.Range(-9f, 9f), -0.52f, shore + d.Range(0.9f, 5f));
                d.Cylinder(pal.Snow, floe, d.Range(0.35f, 1.1f), 0.14f, 7, false);
            }

            float half = s.HalfSpanAt(shore);
            d.Scatter(Cottages, 3, -half, half, s.BackNear + 0.8f, shore - 1.2f, 0.3f);
            d.Focus = c + Vector3.up * 2.6f;
        }

        static void Lighthouse(Dresser d, Vector3 c)
        {
            var pal = d.Palette;
            d.Model("Nature/rock_largeC", c + new Vector3(0.2f, 0f, 0.35f), d.Range(0f, 360f), 2.2f);
            d.Cylinder(pal.Rock, c, 0.85f, 0.45f, 10);
            for (int i = 0; i < 4; i++)
                d.Cylinder(i % 2 == 0 ? pal.Trim : pal.Accent, c + Vector3.up * (0.45f + i * 0.85f), 0.62f - i * 0.05f, 0.85f, 12);
            d.Cylinder(pal.Dark, c + Vector3.up * 3.85f, 0.62f, 0.08f, 12);
            d.Cylinder(pal.Glow, c + Vector3.up * 3.93f, 0.34f, 0.5f, 10, false);
            d.Cone(pal.Accent, c + Vector3.up * 4.43f, 0.48f, 0.5f, 12);
            d.Claim(c.x, c.z, 1.2f);
            d.LightSpots.Insert(0, c + Vector3.up * 4.15f);
        }

        // ---- Shared -------------------------------------------------------------------------------

        /// <summary>
        /// A gabled building: walls, wall-colored gable ends and two roof slabs with an overhang. Built
        /// with the ridge along local z, then turned by <paramref name="yaw"/>.
        /// </summary>
        public static void GableHouse(Dresser d, Vector3 c, float yaw, float width, float wallHeight, float depth,
            float roofHeight, Material wall, Material roof)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            d.Box(wall, c + q * new Vector3(0f, wallHeight * 0.5f, 0f), new Vector3(width, wallHeight, depth), q, 0.01f);
            d.Batch.Add(MeshKit.Prism(), Matrix4x4.TRS(c + q * new Vector3(0f, wallHeight, 0f), q,
                new Vector3(width, roofHeight, depth)), wall);

            const float overhang = 0.18f;
            const float thickness = 0.1f;
            float halfW = width * 0.5f;
            float angle = Mathf.Atan2(roofHeight, halfW);
            float run = halfW + overhang;
            float length = run / Mathf.Cos(angle);
            float eaveY = wallHeight - roofHeight * overhang / halfW;
            for (int s = -1; s <= 1; s += 2)
            {
                var normal = new Vector3(s * Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                var mid = new Vector3(s * run * 0.5f, (wallHeight + roofHeight + eaveY) * 0.5f, 0f) + normal * thickness * 0.5f;
                var rot = q * Quaternion.Euler(0f, 0f, -s * angle * Mathf.Rad2Deg);
                d.Box(roof, c + q * mid, new Vector3(length, thickness, depth + overhang * 2f), rot);
            }
        }
    }
}
