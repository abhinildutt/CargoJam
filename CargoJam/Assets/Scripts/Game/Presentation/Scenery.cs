using System.Collections.Generic;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The level's own set dressing: the depot's concrete apron with the yard tiles, the dock edge and
    /// the road with its paint. Biome colors come from the shared materials, which
    /// <see cref="WorldEnvironment"/> recolors; the ground around it belongs to the environment.
    /// About six draw calls in all: the apron, tiles and curbs are one mesh, the road another.
    /// </summary>
    public static class Scenery
    {
        public const float RoadHalfWidth = 0.85f;
        const float RoadLength = 80f;
        const float TileSize = 0.94f;
        const float TileTop = 0.006f;
        const float ApronTop = -0.002f;

        static readonly Dictionary<float, Mesh> roads = new Dictionary<float, Mesh>();
        static Mesh yardMesh;

        public static void Build(Transform parent, Layout layout, MaterialBank materials)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(parent, false);

            // Apron (0), tiles (1) and curbs (2) in one mesh.
            var b = new MeshBuilder(3);
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
                var c = layout.CellCenter(new Core.Cell(x, y));
                Slab(b, 1, c.x - TileSize * 0.5f, c.x + TileSize * 0.5f, c.z - TileSize * 0.5f, c.z + TileSize * 0.5f,
                    ApronTop - 0.01f, TileTop);
            }
            // One level exists at a time, so the yard mesh is refilled rather than reallocated.
            yardMesh = b.ToMesh("Yard", yardMesh);
            var yard = MeshKit.Show("Yard", root, yardMesh, materials.Apron, castShadows: false);
            yard.sharedMaterials = new[] { materials.Apron, materials.Tile, materials.Curb };

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

        /// <summary>A box between two x, two z and two y values (no bottom needed, but cheap enough to keep).</summary>
        static void Slab(MeshBuilder b, int submesh, float x0, float x1, float z0, float z1, float y0, float y1)
        {
            var center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            var size = new Vector3(x1 - x0, y1 - y0, z1 - z0);
            MeshKit.AddBox(b, submesh, Matrix4x4.TRS(center, Quaternion.identity, size), 0f, Vector2.zero);
        }
    }
}
