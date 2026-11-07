using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// Accumulates flat-shaded geometry (one submesh per slot) and turns it into a readable Mesh.
    /// Everything procedural in the world goes through here, so meshes can be cached and combined.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<List<int>> submeshes = new List<List<int>>();

        public MeshBuilder(int submeshCount = 1)
        {
            for (int i = 0; i < submeshCount; i++) submeshes.Add(new List<int>());
        }

        public int VertexCount => vertices.Count;

        /// <summary>A flat polygon (convex, 3 or 4 corners) with one normal; winding is fixed to face <paramref name="normal"/>.</summary>
        public void Polygon(int submesh, Vector3 normal, Vector2 uv, params Vector3[] corners)
        {
            int start = vertices.Count;
            foreach (var c in corners)
            {
                vertices.Add(c);
                normals.Add(normal);
                uvs.Add(uv);
            }
            var tris = submeshes[submesh];
            for (int i = 1; i + 1 < corners.Length; i++)
            {
                // Unity front faces have Cross(b - a, c - a) pointing out of the surface.
                var n = Vector3.Cross(corners[i] - corners[0], corners[i + 1] - corners[0]);
                if (Vector3.Dot(n, normal) >= 0f)
                {
                    tris.Add(start); tris.Add(start + i); tris.Add(start + i + 1);
                }
                else
                {
                    tris.Add(start); tris.Add(start + i + 1); tris.Add(start + i);
                }
            }
        }

        /// <summary>Overwrites the UVs of the last three vertices added (per-corner UVs for a triangle).</summary>
        public void SetTailUvs(Vector2 a, Vector2 b, Vector2 c)
        {
            int n = uvs.Count;
            uvs[n - 3] = a;
            uvs[n - 2] = b;
            uvs[n - 1] = c;
        }

        /// <summary>Copies another mesh's geometry in, transformed (all of its submeshes into one slot).</summary>
        public void Append(int submesh, Mesh mesh, Matrix4x4 matrix, Vector2? uvOverride = null)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var uv = mesh.uv;
            int start = vertices.Count;
            var normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < v.Length; i++)
            {
                vertices.Add(matrix.MultiplyPoint3x4(v[i]));
                normals.Add(i < n.Length ? normalMatrix.MultiplyVector(n[i]).normalized : Vector3.up);
                uvs.Add(uvOverride ?? (i < uv.Length ? uv[i] : Vector2.zero));
            }
            bool flip = matrix.determinant < 0f;
            var tris = submeshes[submesh];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var t = mesh.GetTriangles(s);
                for (int i = 0; i < t.Length; i += 3)
                {
                    tris.Add(start + t[i]);
                    tris.Add(start + (flip ? t[i + 2] : t[i + 1]));
                    tris.Add(start + (flip ? t[i + 1] : t[i + 2]));
                }
            }
        }

        /// <summary>Builds a new mesh, or refills <paramref name="reuse"/> in place.</summary>
        public Mesh ToMesh(string name, Mesh reuse = null)
        {
            var mesh = reuse != null ? reuse : new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// Cached procedural meshes (all readable, so they can be combined at runtime): beveled boxes,
    /// cylinders, cones and gable roofs. Unit sized and centered unless noted.
    /// </summary>
    public static class MeshKit
    {
        static readonly Dictionary<int, Mesh> boxes = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Mesh> cylinders = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Mesh> cones = new Dictionary<int, Mesh>();
        static Mesh prism;
        static Mesh quad;

        /// <summary>A 1×1×1 box centered on the origin with chamfered edges (<paramref name="bevel"/> in unit space).</summary>
        public static Mesh Box(float bevel = 0f)
        {
            int key = Mathf.RoundToInt(bevel * 1000f);
            if (boxes.TryGetValue(key, out var m) && m != null) return m;
            var b = new MeshBuilder();
            AddBox(b, 0, Matrix4x4.identity, bevel, Vector2.zero);
            m = b.ToMesh($"Box {bevel:0.###}");
            boxes[key] = m;
            return m;
        }

        /// <summary>
        /// Adds a unit box (centered, 1×1×1 before <paramref name="matrix"/>) with chamfered edges.
        /// 6 faces, 12 edge strips and 8 corner triangles, each with its own flat normal.
        /// </summary>
        public static void AddBox(MeshBuilder b, int submesh, Matrix4x4 matrix, float bevel, Vector2 uv)
        {
            const float h = 0.5f;
            float i = h - Mathf.Clamp(bevel, 0f, 0.49f);
            var nm = matrix.inverse.transpose;
            void Poly(Vector3 n, params Vector3[] c)
            {
                for (int k = 0; k < c.Length; k++) c[k] = matrix.MultiplyPoint3x4(c[k]);
                b.Polygon(submesh, nm.MultiplyVector(n).normalized, uv, c);
            }

            // Faces.
            for (int axis = 0; axis < 3; axis++)
            for (int s = -1; s <= 1; s += 2)
            {
                var n = Vector3.zero; n[axis] = s;
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                var c = new Vector3[4];
                for (int k = 0; k < 4; k++)
                {
                    var p = Vector3.zero;
                    p[axis] = s * h;
                    p[u] = (k == 0 || k == 3) ? -i : i;
                    p[v] = (k < 2) ? -i : i;
                    c[k] = p;
                }
                Poly(n, c);
            }
            if (bevel <= 0f) return;

            // Edge chamfers: between faces on axes a and c, running along axis d.
            for (int a = 0; a < 3; a++)
            for (int c2 = a + 1; c2 < 3; c2++)
            {
                int d = 3 - a - c2;
                for (int sa = -1; sa <= 1; sa += 2)
                for (int sc = -1; sc <= 1; sc += 2)
                {
                    var n = Vector3.zero; n[a] = sa; n[c2] = sc;
                    var p0 = Vector3.zero; p0[a] = sa * h; p0[c2] = sc * i; p0[d] = -i;
                    var p1 = p0; p1[d] = i;
                    var p2 = Vector3.zero; p2[a] = sa * i; p2[c2] = sc * h; p2[d] = i;
                    var p3 = p2; p3[d] = -i;
                    Poly(n.normalized, p0, p1, p2, p3);
                }
            }

            // Corner triangles.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Poly(new Vector3(sx, sy, sz).normalized,
                    new Vector3(sx * h, sy * i, sz * i),
                    new Vector3(sx * i, sy * h, sz * i),
                    new Vector3(sx * i, sy * i, sz * h));
            }
        }

        /// <summary>A cylinder of radius 0.5 and height 1 along +Y, centered on the origin, with caps.</summary>
        public static Mesh Cylinder(int segments = 16)
        {
            if (cylinders.TryGetValue(segments, out var m) && m != null) return m;
            var b = new MeshBuilder();
            AddCylinder(b, 0, Matrix4x4.identity, segments, 0.5f, 0.5f, Vector2.zero);
            m = b.ToMesh($"Cylinder {segments}");
            cylinders[segments] = m;
            return m;
        }

        /// <summary>A cone of base radius 0.5 and height 1 along +Y, base at y = 0.</summary>
        public static Mesh Cone(int segments = 12)
        {
            if (cones.TryGetValue(segments, out var m) && m != null) return m;
            var b = new MeshBuilder();
            AddCylinder(b, 0, Matrix4x4.Translate(new Vector3(0f, 0.5f, 0f)), segments, 0.5f, 0f, Vector2.zero);
            m = b.ToMesh($"Cone {segments}");
            cones[segments] = m;
            return m;
        }

        /// <summary>Adds a (possibly tapered) cylinder of height 1 centered on the origin, faceted.</summary>
        public static void AddCylinder(MeshBuilder b, int submesh, Matrix4x4 matrix, int segments,
            float bottomRadius, float topRadius, Vector2 uv)
        {
            var nm = matrix.inverse.transpose;
            float slope = bottomRadius - topRadius;
            for (int s = 0; s < segments; s++)
            {
                float a0 = s * Mathf.PI * 2f / segments, a1 = (s + 1) * Mathf.PI * 2f / segments;
                float am = (a0 + a1) * 0.5f;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                var side = new Vector3(Mathf.Cos(am), slope, Mathf.Sin(am)).normalized;
                var bl = d0 * bottomRadius + Vector3.down * 0.5f;
                var br = d1 * bottomRadius + Vector3.down * 0.5f;
                var tl = d0 * topRadius + Vector3.up * 0.5f;
                var tr = d1 * topRadius + Vector3.up * 0.5f;
                var n = nm.MultiplyVector(side).normalized;
                if (topRadius > 0.0001f)
                {
                    b.Polygon(submesh, n, uv, matrix.MultiplyPoint3x4(bl), matrix.MultiplyPoint3x4(br),
                        matrix.MultiplyPoint3x4(tr), matrix.MultiplyPoint3x4(tl));
                    b.Polygon(submesh, nm.MultiplyVector(Vector3.up).normalized, uv,
                        matrix.MultiplyPoint3x4(Vector3.up * 0.5f), matrix.MultiplyPoint3x4(tl), matrix.MultiplyPoint3x4(tr));
                }
                else
                {
                    b.Polygon(submesh, n, uv, matrix.MultiplyPoint3x4(bl), matrix.MultiplyPoint3x4(br),
                        matrix.MultiplyPoint3x4(Vector3.up * 0.5f));
                }
                b.Polygon(submesh, nm.MultiplyVector(Vector3.down).normalized, uv,
                    matrix.MultiplyPoint3x4(Vector3.down * 0.5f), matrix.MultiplyPoint3x4(bl), matrix.MultiplyPoint3x4(br));
            }
        }

        /// <summary>A gable roof: a triangular prism 1 wide (x), 1 tall (ridge at y = 1, along z), 1 deep, base at y = 0.</summary>
        public static Mesh Prism()
        {
            if (prism != null) return prism;
            var b = new MeshBuilder();
            var l0 = new Vector3(-0.5f, 0f, -0.5f); var r0 = new Vector3(0.5f, 0f, -0.5f); var t0 = new Vector3(0f, 1f, -0.5f);
            var l1 = new Vector3(-0.5f, 0f, 0.5f); var r1 = new Vector3(0.5f, 0f, 0.5f); var t1 = new Vector3(0f, 1f, 0.5f);
            b.Polygon(0, Vector3.back, Vector2.zero, l0, t0, r0);
            b.Polygon(0, Vector3.forward, Vector2.zero, l1, r1, t1);
            b.Polygon(0, new Vector3(-1f, 0.5f, 0f).normalized, Vector2.zero, l0, l1, t1, t0);
            b.Polygon(0, new Vector3(1f, 0.5f, 0f).normalized, Vector2.zero, r0, t0, t1, r1);
            b.Polygon(0, Vector3.down, Vector2.zero, l0, r0, r1, l1);
            prism = b.ToMesh("Prism");
            return prism;
        }

        /// <summary>A 1×1 upward-facing quad on the XZ plane, centered on the origin.</summary>
        public static Mesh Quad()
        {
            if (quad != null) return quad;
            var b = new MeshBuilder();
            b.Polygon(0, Vector3.up, Vector2.zero,
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f));
            quad = b.ToMesh("Quad");
            return quad;
        }

        /// <summary>Puts a mesh on a new child object (no collider).</summary>
        public static MeshRenderer Show(string name, Transform parent, Mesh mesh, Material material,
            bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }

        /// <summary>A TRS matrix with a yaw (degrees about Y).</summary>
        public static Matrix4x4 TRS(Vector3 position, float yaw, Vector3 scale) =>
            Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), scale);
    }
}
