using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// Collects static geometry per (material, shadow mode) and combines each bucket into one mesh,
    /// so the whole environment costs one draw call per material. The output renderers and meshes are
    /// reused between builds.
    /// </summary>
    public sealed class StaticBatch
    {
        readonly Transform root;
        readonly Dictionary<(Material, bool), List<CombineInstance>> buckets =
            new Dictionary<(Material, bool), List<CombineInstance>>();
        readonly List<MeshRenderer> outputs = new List<MeshRenderer>();

        public StaticBatch(Transform root)
        {
            this.root = root;
        }

        public int RendererCount { get; private set; }
        public int VertexCount { get; private set; }

        public void Clear()
        {
            foreach (var list in buckets.Values) list.Clear();
        }

        public void Add(Mesh mesh, int subMesh, Matrix4x4 world, Material material, bool castShadows = true)
        {
            if (mesh == null || material == null) return;
            var key = (material, castShadows);
            if (!buckets.TryGetValue(key, out var list))
            {
                list = new List<CombineInstance>();
                buckets[key] = list;
            }
            list.Add(new CombineInstance { mesh = mesh, subMeshIndex = subMesh, transform = world });
        }

        /// <summary>Adds every submesh of a procedural mesh with one material.</summary>
        public void Add(Mesh mesh, Matrix4x4 world, Material material, bool castShadows = true)
        {
            if (mesh == null) return;
            for (int s = 0; s < mesh.subMeshCount; s++) Add(mesh, s, world, material, castShadows);
        }

        /// <summary>Adds a Kenney model, each part on the palette's material for it.</summary>
        public void Add(PropModel model, Matrix4x4 world, EnvPalette palette, bool castShadows = true)
        {
            if (model == null) return;
            foreach (var part in model.Parts)
                Add(part.Mesh, part.SubMesh, world * part.Local, palette.Resolve(model, part), castShadows);
        }

        /// <summary>Rebuilds the combined meshes from what was added since <see cref="Clear"/>.</summary>
        public void Build()
        {
            int used = 0;
            int vertices = 0;
            foreach (var pair in buckets)
            {
                var list = pair.Value;
                if (list.Count == 0) continue;
                var r = Output(used++);
                var filter = r.GetComponent<MeshFilter>();
                var mesh = filter.sharedMesh;
                mesh.Clear();
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(list.ToArray(), true, true, false);
                mesh.RecalculateBounds();
                vertices += mesh.vertexCount;
                r.sharedMaterial = pair.Key.Item1;
                r.shadowCastingMode = pair.Key.Item2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.gameObject.name = pair.Key.Item1.name;
                r.gameObject.SetActive(true);
            }
            for (int i = used; i < outputs.Count; i++)
            {
                outputs[i].GetComponent<MeshFilter>().sharedMesh.Clear();
                outputs[i].gameObject.SetActive(false);
            }
            RendererCount = used;
            VertexCount = vertices;
        }

        MeshRenderer Output(int index)
        {
            while (outputs.Count <= index)
            {
                var mesh = new Mesh { name = "Combined", indexFormat = IndexFormat.UInt32 };
                var r = MeshKit.Show("Combined", root, mesh, null);
                r.receiveShadows = true;
                outputs.Add(r);
            }
            return outputs[index];
        }

        /// <summary>Destroys the output renderers and their meshes.</summary>
        public void Destroy()
        {
            foreach (var r in outputs)
            {
                if (r == null) continue;
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                if (mesh != null) Object.Destroy(mesh);
                Object.Destroy(r.gameObject);
            }
            outputs.Clear();
            Clear();
        }
    }
}
