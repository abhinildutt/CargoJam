using System.Collections.Generic;
using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The level's own set dressing: the depot's concrete apron with the yard tiles, the dock edge and
    /// the road with its paint. Biome colors come from the shared materials, which
    /// <see cref="WorldEnvironment"/> recolors; the ground around it belongs to the environment.
    /// About six draw calls in all: the apron, tiles and curbs are one mesh, the road another.
    /// Bollards are concrete posts merged into the yard mesh (curb material, plus one draw call for
    /// their dark band); the tile under each one is left out.
    /// </summary>
    public static class Scenery
    {
        public const float RoadHalfWidth = 0.85f;
        const float RoadLength = 80f;
        const float TileSize = 0.94f;
        const float TileTop = 0.006f;
        const float ApronTop = -0.002f;
        /// <summary>A bollard: post radius and height, its dark band, and the square footing it stands on.</summary>
        const float BollardRadius = 0.24f;
        const float BollardHeight = 0.6f;
        const float BollardCap = 0.09f;
        const float BandBottom = 0.38f;
        const float BandHeight = 0.1f;
        const float FootingSize = 0.66f;
        const int BollardSegments = 12;

        static readonly Dictionary<float, Mesh> roads = new Dictionary<float, Mesh>();
        static Mesh yardMesh;

        /// <param name="bollards">
        /// The level's bollard cells (<see cref="LevelDefinition.IsBollard"/>), or null for none. They
        /// never move, so they are baked into the yard mesh.
        /// </param>
        public static void Build(Transform parent, Layout layout, MaterialBank materials, IReadOnlyList<Cell> bollards = null)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(parent, false);
            bool hasBollards = bollards != null && bollards.Count > 0;

            // Apron (0), tiles (1) and curbs (2) in one mesh, plus the bollards' bands (3) when there are any.
            var b = new MeshBuilder(hasBollards ? 4 : 3);
            float half = Mathf.Max(layout.Width * Layout.CellSize * 0.5f,
                (layout.DockSlots + 1) * Layout.DockSpacing * 0.5f) + 0.35f;
            float near = -(layout.Height - 1) * Layout.CellSize - 0.85f;
            float far = Layout.RoadZ - RoadHalfWidth;
            Slab(b, 0, -half, half, near, far, ApronTop - 0.03f, ApronTop);

            // A low curb framing the apron's sides and front.
            const float rim = 0.08f, rimTop = 0.03f;
            Slab(b, 2, -half - rim, -half, near - rim, far, ApronTop - 0.03f, rimTop);
            Slab(b, 2, half, half + rim, near - rim, far, ApronTop - 0.03f, rimTop);
            Slab(b, 2, -half, half, near - rim, near, ApronTop - 0.03f, rimTop);
            // The near sidewalk along the road, either side of the apron.
            Slab(b, 2, -RoadLength * 0.5f, -half - rim, far - 0.45f, far, ApronTop - 0.03f, rimTop);
            Slab(b, 2, half + rim, RoadLength * 0.5f, far - 0.45f, far, ApronTop - 0.03f, rimTop);

            for (int y = 0; y < layout.Height; y++)
            for (int x = 0; x < layout.Width; x++)
            {
                var cell = new Cell(x, y);
                if (hasBollards && Contains(bollards, cell)) continue;
                var c = layout.CellCenter(cell);
                Slab(b, 1, c.x - TileSize * 0.5f, c.x + TileSize * 0.5f, c.z - TileSize * 0.5f, c.z + TileSize * 0.5f,
                    ApronTop - 0.01f, TileTop);
            }
            if (hasBollards)
                for (int i = 0; i < bollards.Count; i++) AddBollard(b, layout.CellCenter(bollards[i]));
            // One level exists at a time, so the yard mesh is refilled rather than reallocated.
            yardMesh = b.ToMesh("Yard", yardMesh);
            var yard = MeshKit.Show("Yard", root, yardMesh, materials.Apron, castShadows: false);
            yard.sharedMaterials = hasBollards
                ? new[] { materials.Apron, materials.Tile, materials.Curb, materials.Wheel }
                : new[] { materials.Apron, materials.Tile, materials.Curb };

            float yardWidth = layout.Width * Layout.CellSize;
            var edge = MaterialBank.Box("Dock Edge", root, new Vector3(0f, 0.01f, Layout.DockEdgeZ - 0.1f),
                new Vector3(yardWidth + 0.2f, 0.04f, 0.1f), materials.Edge);
            edge.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            BuildRoad(root, Layout.RoadZ, materials);
        }

        /// <summary>
        /// The road along x at <paramref name="z"/>: asphalt plus white edge lines and center dashes.
        /// The mesh is cached per z.
        /// </summary>
        public static MeshRenderer BuildRoad(Transform parent, float z, MaterialBank materials)
        {
            if (!roads.TryGetValue(z, out var mesh) || mesh == null)
            {
                var b = new MeshBuilder(2);
                float h = RoadHalfWidth;
                Slab(b, 0, -RoadLength * 0.5f, RoadLength * 0.5f, z - h, z + h, -0.015f, 0.005f);
                const float line = 0.06f, paintTop = 0.009f;
                Slab(b, 1, -RoadLength * 0.5f, RoadLength * 0.5f, z - h + 0.1f, z - h + 0.1f + line, 0f, paintTop);
                Slab(b, 1, -RoadLength * 0.5f, RoadLength * 0.5f, z + h - 0.1f - line, z + h - 0.1f, 0f, paintTop);
                for (float x = -RoadLength * 0.5f; x < RoadLength * 0.5f; x += 1.4f)
                    Slab(b, 1, x, x + 0.6f, z - 0.035f, z + 0.035f, 0f, paintTop);
                mesh = b.ToMesh($"Road {z:0.##}");
                roads[z] = mesh;
            }
            var r = MeshKit.Show("Road", parent, mesh, materials.Road, castShadows: false);
            r.sharedMaterials = new[] { materials.Road, materials.LaneMark };
            return r;
        }

        static bool Contains(IReadOnlyList<Cell> cells, Cell cell)
        {
            for (int i = 0; i < cells.Count; i++)
                if (cells[i].Equals(cell)) return true;
            return false;
        }

        /// <summary>
        /// A round concrete post on a square footing, with a domed cap and a dark band (submesh 3, so
        /// it never reads as a parcel or as yellow dock paint), at a cell's center.
        /// </summary>
        static void AddBollard(MeshBuilder b, Vector3 c)
        {
            const float f = FootingSize * 0.5f;
            Slab(b, 2, c.x - f, c.x + f, c.z - f, c.z + f, ApronTop - 0.01f, 0.03f);
            Cylinder(b, 2, c, 0.03f, BollardHeight, BollardRadius, BollardRadius);
            Cylinder(b, 2, c, BollardHeight, BollardHeight + BollardCap, BollardRadius, BollardRadius * 0.62f);
            Cylinder(b, 3, c, BandBottom, BandBottom + BandHeight, BollardRadius + 0.012f, BollardRadius + 0.012f);
        }

        /// <summary>A faceted (possibly tapered) cylinder standing on the ground between two heights.</summary>
        static void Cylinder(MeshBuilder b, int submesh, Vector3 c, float y0, float y1, float bottomRadius, float topRadius)
        {
            var m = Matrix4x4.TRS(new Vector3(c.x, (y0 + y1) * 0.5f, c.z), Quaternion.identity, new Vector3(1f, y1 - y0, 1f));
            MeshKit.AddCylinder(b, submesh, m, BollardSegments, bottomRadius, topRadius, Vector2.zero);
        }

        /// <summary>A box between two x, two z and two y values (no bottom needed, but cheap enough to keep).</summary>
        static void Slab(MeshBuilder b, int submesh, float x0, float x1, float z0, float z1, float y0, float y1)
        {
            var center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            var size = new Vector3(x1 - x0, y1 - y0, z1 - z0);
            MeshKit.AddBox(b, submesh, Matrix4x4.TRS(center, Quaternion.identity, size), 0f, Vector2.zero);
        }
    }
}
