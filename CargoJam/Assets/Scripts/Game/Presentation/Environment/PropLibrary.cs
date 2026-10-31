using System.Collections.Generic;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>One imported Kenney model, flattened to (mesh, submesh, model-space matrix, source material) parts.</summary>
    public sealed class PropModel
    {
        public struct Part
        {
            public Mesh Mesh;
            public int SubMesh;
            /// <summary>Model space (pivot at the bottom center, facing +z).</summary>
            public Matrix4x4 Local;
            /// <summary>The imported material's name ("leafsGreen", "colormap", ...).</summary>
            public string MaterialName;
            public Texture Texture;
            public Color Color;
        }

        public readonly string Path;
        public readonly List<Part> Parts = new List<Part>();
        /// <summary>Model-space bounds of all parts.</summary>
        public Bounds Bounds;
        /// <summary>City kit buildings: their windows glow at dusk.</summary>
        public readonly bool Building;

        public PropModel(string path)
        {
            Path = path;
            Building = path.StartsWith("Suburban/building") || path.StartsWith("Commercial/");
        }

        /// <summary>Footprint radius on the ground at scale 1.</summary>
        public float Radius => Mathf.Max(Bounds.extents.x, Bounds.extents.z);
    }

    /// <summary>
    /// Loads Kenney models from <c>Resources/Env</c> once and caches them as parts, so placing a prop
    /// is just adding matrices to a <see cref="StaticBatch"/> (nothing is instantiated). The models
    /// import with Read/Write on, which <see cref="Mesh.CombineMeshes(CombineInstance[])"/> needs in builds.
    /// </summary>
    public static class PropLibrary
    {
        static readonly Dictionary<string, PropModel> cache = new Dictionary<string, PropModel>();

        /// <summary>A model by its path under <c>Resources/Env</c> ("Nature/tree_default"), or null if missing.</summary>
        public static PropModel Get(string path)
        {
            if (cache.TryGetValue(path, out var cached)) return cached;
            var model = Load(path);
            cache[path] = model;
            return model;
        }

        static PropModel Load(string path)
        {
            var prefab = Resources.Load<GameObject>("Env/" + path);
            if (prefab == null)
            {
                Debug.LogWarning($"PropLibrary: Env/{path} not found.");
                return null;
            }
            var model = new PropModel(path);
            var offset = Matrix4x4.Translate(-prefab.transform.position);
            bool any = false;
            var bounds = new Bounds();
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mesh == null || mr == null) continue;
                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"PropLibrary: Env/{path} is not readable; enable Read/Write on its import.");
                    return null;
                }
                var local = offset * mf.transform.localToWorldMatrix;
                var mats = mr.sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var src = mats.Length > 0 ? mats[Mathf.Min(s, mats.Length - 1)] : null;
                    model.Parts.Add(new PropModel.Part
                    {
                        Mesh = mesh,
                        SubMesh = s,
                        Local = local,
                        MaterialName = src != null ? src.name.Replace(" (Instance)", "") : "",
                        Texture = src != null ? MainTexture(src) : null,
                        Color = src != null ? MainColor(src) : Color.white,
                    });
                }
                var mb = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = local.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            model.Bounds = bounds;
            return any ? model : null;
        }

        static Texture MainTexture(Material m)
        {
            if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return m.GetTexture("_BaseMap");
            if (m.HasProperty("_MainTex")) return m.GetTexture("_MainTex");
            return null;
        }

        static Color MainColor(Material m)
        {
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.white;
        }
    }
}
