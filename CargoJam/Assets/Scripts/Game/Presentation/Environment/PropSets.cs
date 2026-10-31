using System;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// What one biome puts in each band around the stage (§5): a code-built landmark, then Kenney
    /// props scattered by band. Scales are model multipliers (parcels are ~0.8, tiles 1, trucks ~3 long).
    /// </summary>
    public sealed class PropSet
    {
        public string Id;
        public Action<Dresser> Landmark;
        /// <summary>A shoreline this far behind the back band's front edge, water beyond it.</summary>
        public float ShoreOffset = -1f;
        /// <summary>Streetlights along the far curb, this far apart (0 = none).</summary>
        public float LampSpacing;
        /// <summary>Car Kit vehicles parked along the far side of the road.</summary>
        public string[] Parked = new string[0];
        public int ParkedCount;
        /// <summary>Back band, front half: mid-size props.</summary>
        public Pick[] Mid; public int MidCount;
        /// <summary>Back band, back half: the biggest props.</summary>
        public Pick[] Far; public int FarCount;
        /// <summary>Side gutters: low props only.</summary>
        public Pick[] Gutter; public int GutterCount;
        /// <summary>Foreground: ground detail that stays below the booster bar.</summary>
        public Pick[] Front; public int FrontCount;
        /// <summary>Falling snow.</summary>
        public bool Snow;

        public bool HasWater => ShoreOffset >= 0f;
    }

    /// <summary>The prop set of every biome, by <see cref="BiomeDefinition.PropSet"/>. One table per destination.</summary>
    public static class PropSets
    {
        static Pick P(string path, float min, float max, float weight = 1f, bool face = false) =>
            new Pick(path, min, max, weight, face);

        // Low ground detail shared by the greener biomes.
        static readonly Pick[] GreenFront =
        {
            P("Nature/grass", 1.4f, 1.9f, 3f), P("Nature/grass_large", 1.3f, 1.7f, 2f),
            P("Nature/grass_leafs", 1.6f, 2.2f, 2f), P("Nature/flower_yellowA", 1.4f, 1.8f),
            P("Nature/flower_purpleA", 1.4f, 1.8f), P("Nature/rock_smallA", 1.4f, 2f),
            P("Nature/plant_bushSmall", 1.4f, 1.8f),
        };

        static readonly Pick[] GreenGutter =
        {
            P("Nature/plant_bush", 1.6f, 2.2f, 2f), P("Nature/plant_bushLarge", 1.6f, 2.2f, 2f),
            P("Nature/grass_large", 1.5f, 2f), P("Nature/rock_smallC", 1.6f, 2.2f),
            P("Nature/flower_redA", 1.5f, 1.8f),
        };

        // ---- Harbor Town ------------------------------------------------------------------------

        static readonly PropSet Harbor = new PropSet
        {
            Id = "harbor",
            Landmark = Landmarks.Harbor,
            ShoreOffset = 3.6f,
            LampSpacing = 4.5f,
            Parked = new[] { "Cars/delivery", "Cars/van", "Cars/truck" }, ParkedCount = 2,
            Mid = new[] { P("Cars/box", 1.1f, 1.3f, 2f), P("Nature/plant_bushLarge", 1.6f, 2f) }, MidCount = 4,
            Far = new Pick[0], FarCount = 0,
            Gutter = new[] { P("Cars/box", 0.8f, 1f, 2f), P("Cars/cone", 0.9f, 1f), P("Nature/plant_bush", 1.6f, 2f) }, GutterCount = 6,
            Front = new[] { P("Nature/rock_smallA", 1.4f, 2f), P("Nature/grass", 1.4f, 1.8f, 2f), P("Cars/cone", 0.7f, 0.8f) }, FrontCount = 8,
        };

        // ---- Farmlands --------------------------------------------------------------------------

        static readonly PropSet Farm = new PropSet
        {
            Id = "farm",
            Landmark = Landmarks.Farm,
            Parked = new[] { "Cars/tractor", "Cars/truck-flat", "Cars/van" }, ParkedCount = 2,
            Mid = new[]
            {
                P("Nature/tree_oak", 1.8f, 2.3f, 2f), P("Nature/tree_default", 1.6f, 2f, 2f),
                P("Nature/plant_bushLarge", 1.8f, 2.4f), P("Nature/crop_pumpkin", 1.2f, 1.5f),
            }, MidCount = 7,
            Far = new[]
            {
                P("Nature/tree_oak", 2.2f, 2.8f, 2f), P("Nature/tree_fat", 2.2f, 2.8f, 2f),
                P("Nature/tree_default", 2f, 2.6f), P("Nature/tree_detailed", 2f, 2.6f),
            }, FarCount = 9,
            Gutter = new[]
            {
                P("Nature/fence_simple", 1.1f, 1.2f, 2f, true), P("Nature/plant_bush", 1.6f, 2.2f),
                P("Nature/crop_pumpkin", 1.1f, 1.4f), P("Nature/grass_large", 1.5f, 2f),
            }, GutterCount = 7,
            Front = GreenFront, FrontCount = 12,
        };

        // ---- Canyon Highway ---------------------------------------------------------------------

        static readonly PropSet Canyon = new PropSet
        {
            Id = "canyon",
            Landmark = Landmarks.Canyon,
            Parked = new[] { "Cars/truck", "Cars/suv", "Cars/sedan" }, ParkedCount = 1,
            Mid = new[]
            {
                P("Nature/cactus_tall", 2.2f, 3f, 3f), P("Nature/cactus_short", 2f, 2.6f, 2f),
                P("Nature/rock_tallA", 1.6f, 2.2f), P("Nature/rock_largeC", 1.8f, 2.4f),
            }, MidCount = 8,
            Far = new[]
            {
                P("Nature/rock_tallB", 2.4f, 3.2f, 2f), P("Nature/rock_tallE", 2.4f, 3.2f),
                P("Nature/cactus_tall", 2.6f, 3.4f, 2f), P("Nature/cliff_block_rock", 2f, 2.6f),
            }, FarCount = 7,
            Gutter = new[]
            {
                P("Nature/cactus_short", 1.4f, 1.8f, 2f), P("Nature/rock_smallC", 1.8f, 2.4f),
                P("Nature/rock_largeA", 1.2f, 1.6f), P("Nature/grass_large", 1.4f, 1.8f),
            }, GutterCount = 7,
            Front = new[]
            {
                P("Nature/rock_smallA", 1.4f, 2f, 2f), P("Nature/rock_smallFlatA", 1.6f, 2.2f, 2f),
                P("Nature/grass", 1.3f, 1.7f), P("Nature/cactus_short", 0.8f, 1f),
            }, FrontCount = 10,
        };

        // ---- Pine Ridge -------------------------------------------------------------------------

        static readonly PropSet Pine = new PropSet
        {
            Id = "pine",
            Landmark = Landmarks.Pine,
            Parked = new[] { "Cars/suv", "Cars/truck-flat" }, ParkedCount = 1,
            Mid = new[]
            {
                P("Nature/tree_pineTallA", 1.8f, 2.3f, 2f), P("Nature/tree_pineDefaultA", 1.7f, 2.2f, 2f),
                P("Nature/tree_pineRoundA", 1.7f, 2.2f), P("Nature/log_stack", 1.6f, 2f),
                P("Nature/rock_tallA", 1.3f, 1.7f), P("Nature/stump_round", 1.8f, 2.2f),
            }, MidCount = 10,
            Far = new[]
            {
                P("Nature/tree_pineTallB", 2.2f, 2.8f, 3f), P("Nature/tree_pineTallA", 2.4f, 3f, 2f),
                P("Nature/tree_pineDefaultA", 2.2f, 2.8f, 2f), P("Nature/tree_pineRoundA", 2.2f, 2.6f),
            }, FarCount = 16,
            Gutter = new[]
            {
                P("Nature/tree_pineSmallA", 1.1f, 1.4f, 2f), P("Nature/rock_smallC", 1.8f, 2.4f),
                P("Nature/stump_round", 1.6f, 2f), P("Nature/mushroom_redGroup", 1.6f, 2f),
                P("Nature/plant_bush", 1.6f, 2f),
            }, GutterCount = 7,
            Front = new[]
            {
                P("Nature/grass", 1.4f, 1.9f, 3f), P("Nature/mushroom_tanGroup", 1.4f, 1.8f),
                P("Nature/rock_smallA", 1.4f, 2f, 2f), P("Nature/stump_round", 1.2f, 1.5f),
            }, FrontCount = 11,
        };

        // ---- Metro City -------------------------------------------------------------------------

        static readonly PropSet Metro = new PropSet
        {
            Id = "metro",
            Landmark = Landmarks.Metro,
            LampSpacing = 3.4f,
            Parked = new[] { "Cars/taxi", "Cars/sedan", "Cars/suv", "Cars/van" }, ParkedCount = 3,
            Mid = new[] { P("Suburban/planter", 1.8f, 2.2f), P("Commercial/detail-parasol-a", 2.2f, 2.6f) }, MidCount = 3,
            Far = new Pick[0], FarCount = 0,
            Gutter = new[]
            {
                P("Suburban/planter", 1.6f, 2f, 2f), P("Nature/plant_bushLarge", 1.6f, 2f),
                P("Cars/cone", 0.8f, 0.9f),
            }, GutterCount = 6,
            Front = new[]
            {
                P("Suburban/planter", 1.4f, 1.7f), P("Nature/plant_bushSmall", 1.4f, 1.8f),
                P("Cars/cone", 0.6f, 0.7f),
            }, FrontCount = 6,
        };

        // ---- Frost Port -------------------------------------------------------------------------

        static readonly PropSet Frost = new PropSet
        {
            Id = "frost",
            Landmark = Landmarks.Frost,
            ShoreOffset = 3.8f,
            LampSpacing = 4.5f,
            Parked = new[] { "Cars/van", "Cars/suv", "Cars/delivery" }, ParkedCount = 2,
            Mid = new[]
            {
                P("Nature/tree_pineDefaultA", 1.6f, 2f, 2f), P("Nature/tree_pineRoundA", 1.5f, 1.9f),
                P("Nature/rock_largeC", 1.4f, 1.8f),
            }, MidCount = 5,
            Far = new Pick[0], FarCount = 0,
            Gutter = new[]
            {
                P("Nature/tree_pineSmallA", 1.1f, 1.4f, 2f), P("Nature/rock_smallC", 1.8f, 2.4f),
                P("Nature/plant_bush", 1.6f, 2f),
            }, GutterCount = 6,
            Front = new[]
            {
                P("Nature/rock_smallA", 1.4f, 2f, 2f), P("Nature/rock_smallFlatB", 1.6f, 2.2f),
                P("Nature/grass", 1.3f, 1.6f),
            }, FrontCount = 9,
            Snow = true,
        };

        public static PropSet For(string id) => id switch
        {
            "farm" => Farm,
            "canyon" => Canyon,
            "pine" => Pine,
            "metro" => Metro,
            "frost" => Frost,
            _ => Harbor,
        };

        /// <summary>Dresses a stage: landmark first (it claims the best spots), then road side, bands.</summary>
        public static void Dress(PropSet set, Dresser d)
        {
            var s = d.Stage;
            set.Landmark?.Invoke(d);

            // Streetlights along the far curb, phase varies with the seed.
            if (set.LampSpacing > 0f)
            {
                float span = s.HalfSpanAt(s.FarCurb) + 1f;
                float phase = d.Range(0f, set.LampSpacing);
                for (float x = -span + phase; x <= span; x += set.LampSpacing)
                {
                    var foot = new Vector3(x, 0f, s.FarCurb + 0.32f);
                    // On the menu the sidewalk runs through the depot's frame: keep the hero view open.
                    if (s.Menu && Mathf.Abs(x) < s.Content.max.x) continue;
                    if (d.IsFreeOfClaims(foot.x, foot.z, 0.3f)) d.Streetlight(foot);
                }
            }

            // Parked vehicles just behind the sidewalk.
            for (int i = 0; i < set.ParkedCount && set.Parked.Length > 0; i++)
            {
                float z = s.FarCurb + 1.25f;
                float half = s.HalfSpanAt(z);
                for (int tries = 0; tries < 8; tries++)
                {
                    float x = d.Range(-half, half);
                    if (!d.IsFree(x - 0.9f, z, 0.8f) || !d.IsFree(x + 0.9f, z, 0.8f)) continue;
                    d.ParkedVehicle(set.Parked[d.Index(set.Parked.Length)], new Vector3(x, 0f, z), d.Chance(0.5f));
                    break;
                }
            }

            float midEnd = s.BackNear + 4f;
            float back = Mathf.Min(s.BackFar, d.ShoreZ);
            d.Scatter(set.Mid, set.MidCount, -s.HalfSpanAt(midEnd), s.HalfSpanAt(midEnd), s.BackNear + 0.3f, Mathf.Min(midEnd, back));
            if (back > midEnd)
                d.Scatter(set.Far, set.FarCount, -s.HalfSpanAt(back), s.HalfSpanAt(back), midEnd, back);

            // Gutters, both sides, beside the board (not across the road).
            // (The last half unit before the road is the near sidewalk.)
            float gz0 = s.Content.min.z + 1.5f, gz1 = s.RoadZ - s.RoadHalfWidth - 0.7f;
            int perSide = set.GutterCount / 2;
            d.Scatter(set.Gutter, perSide, -s.GutterX - 2.4f, -s.GutterX, gz0, gz1);
            d.Scatter(set.Gutter, set.GutterCount - perSide, s.GutterX, s.GutterX + 2.4f, gz0, gz1);

            // Foreground: low detail, no shadows (cheap, and nothing to shade under the booster bar).
            float fz1 = s.FrontZ, fz0 = s.FrontZ - 6f;
            float fx = s.Content.max.x + 1.2f;
            d.Scatter(set.Front, set.FrontCount, -fx, fx, fz0, fz1, 0.25f, shadows: false);
        }
    }
}
