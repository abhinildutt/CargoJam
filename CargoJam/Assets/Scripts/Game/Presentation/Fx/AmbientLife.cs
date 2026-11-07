using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CargoJam.Game.Presentation.Fx
{
    /// <summary>What drifts through the scenery for a biome and mood.</summary>
    public enum AmbientKind
    {
        None,
        /// <summary>Harbor: a few gulls gliding and flapping high over the back band.</summary>
        Gulls,
        /// <summary>Farmlands: warm pollen and leaf specks on the breeze.</summary>
        Pollen,
        /// <summary>Canyon: slow dust motes low over the ground.</summary>
        Dust,
        /// <summary>Pine by day: needles twirling down.</summary>
        Needles,
        /// <summary>Pine at dusk: blinking fireflies.</summary>
        Fireflies,
        /// <summary>Frost: gently falling snow.</summary>
        Snow,
        /// <summary>Storm: rain streaks.</summary>
        Rain,
        /// <summary>Frost storm: driving snow.</summary>
        Blizzard,
    }

    /// <summary>
    /// Ambient life around the board: one pooled set of camera-facing quads written into a single
    /// dynamic mesh (one draw call), stepped from its own Update with no per-frame allocations. It
    /// survives level teardown and keeps running between levels of the same environment. Motes live
    /// only in the back band, the side gutters and the sky above them, and any that drift over the
    /// board are recycled, so the board is never obscured. In storms it also asks for an occasional
    /// soft lightning flash (at most one every <see cref="LightningMin"/> seconds).
    /// </summary>
    public sealed class AmbientLife : MonoBehaviour
    {
        const int MaxQuads = 96;
        const float LightningMin = 8f;
        const float LightningMax = 14f;
        const float ThunderDelay = 0.75f;

        struct Spec
        {
            public int Count;
            public int QuadsPer;
            public Color Color;
            public float YMin, YMax;
            public float SizeMin, SizeMax;
            /// <summary>Respawns at the top when it reaches the ground, instead of by age.</summary>
            public bool Falls;
            public float LifeMin, LifeMax;
            /// <summary>Share spawned in the back band (the rest go to the side gutters).</summary>
            public float Back;
        }

        struct Mote
        {
            public Vector3 Pos;
            public Vector3 Vel;
            public float Age, Life, Size, Phase, Angle, Spin;
        }

        static readonly Vector3 Normal = new Vector3(0f, 0.7071f, -0.7071f);

        Mesh mesh;
        MeshRenderer meshRenderer;
        Vector3[] verts;
        readonly Mote[] motes = new Mote[MaxQuads];
        readonly Material[] materials = new Material[Enum.GetValues(typeof(AmbientKind)).Length];
        FxMaterials fx;
        Camera cam;

        AmbientKind kind;
        Spec spec;
        Bounds area;
        bool keepClear;
        bool storm;
        float nextLightning = -1f, flickerAt = -1f, thunderAt = -1f;

        /// <summary>Called for each lightning flash with its strength (0..1).</summary>
        public Action<float> Lightning;
        /// <summary>Called a moment after each main flash, for a distant rumble.</summary>
        public Action Thunder;

        public AmbientKind Kind => kind;

        /// <summary>Creates the (idle) ambient layer at the top level of the scene.</summary>
        public static AmbientLife Create(FxMaterials fx)
        {
            var go = new GameObject("FX Ambient");
            var life = go.AddComponent<AmbientLife>();
            life.fx = fx;
            life.Build(go);
            return life;
        }

        /// <summary>The ambient kind for a biome id (Journey ids) and mood.</summary>
        public static AmbientKind KindFor(string biomeId, CargoJam.Meta.Mood mood)
        {
            bool isStorm = mood == CargoJam.Meta.Mood.Storm;
            switch (biomeId)
            {
                case "frost": return isStorm ? AmbientKind.Blizzard : AmbientKind.Snow;
                case "harbor": return isStorm ? AmbientKind.Rain : AmbientKind.Gulls;
                case "farm": return isStorm ? AmbientKind.Rain : AmbientKind.Pollen;
                case "canyon": return isStorm ? AmbientKind.Rain : AmbientKind.Dust;
                case "pine":
                    return isStorm ? AmbientKind.Rain : mood == CargoJam.Meta.Mood.Dusk ? AmbientKind.Fireflies : AmbientKind.Needles;
                default: return isStorm ? AmbientKind.Rain : AmbientKind.None;
            }
        }

        /// <summary>
        /// Switches to <paramref name="next"/> around <paramref name="bounds"/> (the board's content
        /// bounds, or the menu diorama's). With <paramref name="clear"/> nothing may drift over it.
        /// Calling again with the same kind only updates the area, so the motes carry on.
        /// </summary>
        public void Show(AmbientKind next, bool stormy, Bounds bounds, bool clear, Camera camera)
        {
            cam = camera;
            area = bounds;
            keepClear = clear;
            if (stormy != storm)
            {
                storm = stormy;
                nextLightning = storm ? Time.unscaledTime + UnityEngine.Random.Range(3f, 6f) : -1f;
                flickerAt = thunderAt = -1f;
            }
            if (next == kind) return;

            kind = next;
            spec = SpecFor(kind);
            for (int i = 0; i < verts.Length; i++) verts[i] = Vector3.zero;
            if (kind == AmbientKind.None)
            {
                Upload();
                meshRenderer.enabled = false;
                return;
            }
            meshRenderer.sharedMaterial = MaterialFor(kind);
            meshRenderer.enabled = true;
            for (int i = 0; i < spec.Count; i++) Spawn(ref motes[i], true);
        }

        /// <summary>Stops everything (no environment shown).</summary>
        public void Stop() => Show(AmbientKind.None, false, area, keepClear, cam);

        // ---- Build --------------------------------------------------------------------------------

        void Build(GameObject go)
        {
            mesh = new Mesh { name = "Ambient" };
            mesh.MarkDynamic();
            verts = new Vector3[MaxQuads * 4];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            // Both windings, so the quads show whichever way they face.
            var tris = new int[MaxQuads * 12];
            for (int q = 0; q < MaxQuads; q++)
            {
                int v = q * 4, t = q * 12;
                for (int k = 0; k < 4; k++) normals[v + k] = Normal;
                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(0f, 1f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(1f, 0f);
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
                tris[t + 6] = v; tris[t + 7] = v + 2; tris[t + 8] = v + 1;
                tris[t + 9] = v; tris[t + 10] = v + 3; tris[t + 11] = v + 2;
            }
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            // World-space quads anywhere around the scene: one generous fixed box, never recalculated.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(400f, 200f, 400f));

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;
        }

        Material MaterialFor(AmbientKind k)
        {
            int i = (int)k;
            if (materials[i] == null) materials[i] = fx.Make(SpecFor(k).Color);
            return materials[i];
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            foreach (var m in materials)
                if (m != null) Destroy(m);
        }

        static Spec SpecFor(AmbientKind k)
        {
            switch (k)
            {
                case AmbientKind.Gulls:
                    return new Spec { Count = 4, QuadsPer = 2, Color = new Color(0.96f, 0.96f, 0.94f), YMin = 4.5f, YMax = 7f,
                        SizeMin = 0.32f, SizeMax = 0.42f, Back = 1f };
                case AmbientKind.Pollen:
                    return new Spec { Count = 26, QuadsPer = 1, Color = new Color(0.93f, 0.82f, 0.45f), YMin = 0.3f, YMax = 4f,
                        SizeMin = 0.06f, SizeMax = 0.11f, LifeMin = 6f, LifeMax = 10f, Back = 0.75f };
                case AmbientKind.Dust:
                    return new Spec { Count = 30, QuadsPer = 1, Color = new Color(0.86f, 0.72f, 0.56f), YMin = 0.2f, YMax = 3f,
                        SizeMin = 0.05f, SizeMax = 0.09f, LifeMin = 5f, LifeMax = 9f, Back = 0.75f };
                case AmbientKind.Needles:
                    return new Spec { Count = 16, QuadsPer = 1, Color = new Color(0.42f, 0.36f, 0.22f), YMin = 0f, YMax = 6f,
                        SizeMin = 0.13f, SizeMax = 0.18f, Falls = true, Back = 0.7f };
                case AmbientKind.Fireflies:
                    return new Spec { Count = 20, QuadsPer = 1, Color = new Color(1f, 0.93f, 0.55f), YMin = 0.3f, YMax = 2.5f,
                        SizeMin = 0.06f, SizeMax = 0.09f, LifeMin = 7f, LifeMax = 12f, Back = 0.7f };
                case AmbientKind.Snow:
                    return new Spec { Count = 46, QuadsPer = 1, Color = new Color(0.97f, 0.98f, 1f), YMin = 0f, YMax = 8f,
                        SizeMin = 0.07f, SizeMax = 0.13f, Falls = true, Back = 0.7f };
                case AmbientKind.Rain:
                    return new Spec { Count = 64, QuadsPer = 1, Color = new Color(0.72f, 0.78f, 0.88f), YMin = 0f, YMax = 9f,
                        SizeMin = 0.45f, SizeMax = 0.65f, Falls = true, Back = 0.7f };
                case AmbientKind.Blizzard:
                    return new Spec { Count = 72, QuadsPer = 1, Color = new Color(0.95f, 0.97f, 1f), YMin = 0f, YMax = 8f,
                        SizeMin = 0.06f, SizeMax = 0.1f, Falls = true, Back = 0.7f };
                default:
                    return default;
            }
        }

        // ---- Simulation ---------------------------------------------------------------------------

        void Spawn(ref Mote m, bool anywhere)
        {
            var b = area;
            float x, z;
            if (spec.Back >= 1f || UnityEngine.Random.value < spec.Back || !keepClear)
            {
                // Back band (design v3 §5): from just past the road to the far props, a little wider than the board.
                x = UnityEngine.Random.Range(b.min.x - 5f, b.max.x + 5f);
                z = keepClear
                    ? UnityEngine.Random.Range(b.max.z + 0.8f, b.max.z + 9f)
                    : UnityEngine.Random.Range(b.min.z - 2f, b.max.z + 9f);
            }
            else
            {
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                x = side * UnityEngine.Random.Range(b.max.x + 0.6f, b.max.x + 4f);
                z = UnityEngine.Random.Range(b.min.z, b.max.z);
            }
            float y = spec.Falls && !anywhere ? spec.YMax : UnityEngine.Random.Range(spec.YMin, spec.YMax);
            m.Pos = new Vector3(x, y, z);
            m.Size = UnityEngine.Random.Range(spec.SizeMin, spec.SizeMax);
            m.Phase = UnityEngine.Random.Range(0f, 100f);
            m.Angle = UnityEngine.Random.Range(0f, 360f);
            m.Spin = UnityEngine.Random.Range(-120f, 120f);
            m.Life = UnityEngine.Random.Range(spec.LifeMin, Mathf.Max(spec.LifeMin, spec.LifeMax));
            m.Age = anywhere && m.Life > 0f ? UnityEngine.Random.Range(0f, m.Life) : 0f;

            switch (kind)
            {
                case AmbientKind.Gulls:
                    float dir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                    m.Vel = new Vector3(dir * UnityEngine.Random.Range(1.1f, 1.7f), 0f, 0f);
                    break;
                case AmbientKind.Rain:
                    m.Vel = new Vector3(1.1f, -UnityEngine.Random.Range(9f, 11f), 0f);
                    break;
                case AmbientKind.Blizzard:
                    m.Vel = new Vector3(UnityEngine.Random.Range(2f, 2.8f), -UnityEngine.Random.Range(1.8f, 2.6f), 0.3f);
                    break;
                default:
                    m.Vel = Vector3.zero;
                    break;
            }
        }

        void Update()
        {
            StepLightning();
            if (kind == AmbientKind.None || cam == null) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float now = Time.time;
            var camTr = cam.transform;
            var right = camTr.right;
            var up = camTr.up;
            var forward = camTr.forward;
            float wrap = Mathf.Max(Mathf.Abs(area.min.x), Mathf.Abs(area.max.x)) + 9f;

            int quad = 0;
            for (int i = 0; i < spec.Count; i++)
            {
                ref var m = ref motes[i];
                float scale = 1f;
                switch (kind)
                {
                    case AmbientKind.Gulls:
                        m.Pos += m.Vel * dt;
                        m.Pos.y += Mathf.Sin(now * 0.8f + m.Phase) * 0.12f * dt;
                        if (m.Pos.x > wrap) m.Pos.x = -wrap;
                        else if (m.Pos.x < -wrap) m.Pos.x = wrap;
                        WriteGull(ref quad, m, now, right, up, forward);
                        continue;
                    case AmbientKind.Snow:
                        m.Vel = new Vector3(0.25f + Mathf.Sin(now * 1.3f + m.Phase) * 0.35f, -0.75f, 0f);
                        break;
                    case AmbientKind.Needles:
                        m.Vel = new Vector3(Mathf.Sin(now * 1.1f + m.Phase) * 0.3f, -0.55f, 0f);
                        break;
                    case AmbientKind.Pollen:
                        m.Vel = new Vector3(0.45f + 0.3f * Mathf.Sin(now * 0.7f + m.Phase),
                            0.12f * Mathf.Sin(now * 1.3f + m.Phase * 2f), 0.1f * Mathf.Cos(now * 0.5f + m.Phase));
                        break;
                    case AmbientKind.Dust:
                        m.Vel = new Vector3(0.35f + 0.2f * Mathf.Sin(now * 0.5f + m.Phase),
                            0.06f * Mathf.Sin(now * 0.9f + m.Phase), 0f);
                        break;
                    case AmbientKind.Fireflies:
                        m.Vel = new Vector3(Mathf.Sin(now * 0.7f + m.Phase) * 0.35f, Mathf.Sin(now * 1.1f + m.Phase) * 0.2f,
                            Mathf.Cos(now * 0.6f + m.Phase) * 0.3f);
                        float blink = Mathf.Max(0f, Mathf.Sin(now * 1.8f + m.Phase));
                        scale = 0.25f + 0.75f * blink * blink;
                        break;
                }
                m.Pos += m.Vel * dt;
                m.Angle += m.Spin * dt;

                bool dead;
                if (spec.Falls)
                {
                    dead = m.Pos.y < 0f;
                }
                else
                {
                    m.Age += dt;
                    dead = m.Age >= m.Life;
                    // Fade in and out by size over the first and last second.
                    scale *= Mathf.Clamp01(m.Age) * Mathf.Clamp01(m.Life - m.Age);
                }
                if (dead || (keepClear && OverBoard(m.Pos)) || Mathf.Abs(m.Pos.x) > wrap)
                {
                    Spawn(ref m, false);
                    scale = 0f;
                }

                if (kind == AmbientKind.Rain)
                {
                    var along = m.Vel.normalized * (m.Size * 0.5f);
                    var across = Vector3.Cross(along, forward).normalized * 0.013f;
                    WriteQuad(quad++, m.Pos, along, across);
                }
                else
                {
                    float half = m.Size * 0.5f * scale;
                    float a = m.Angle * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                    var axisA = (right * cos + up * sin) * half;
                    var axisB = (up * cos - right * sin) * half;
                    // Needles are long and thin.
                    if (kind == AmbientKind.Needles) axisB *= 0.16f;
                    WriteQuad(quad++, m.Pos, axisA, axisB);
                }
            }
            Upload();
        }

        bool OverBoard(Vector3 p)
        {
            var b = area;
            return p.x > b.min.x - 0.3f && p.x < b.max.x + 0.3f && p.z > b.min.z - 0.8f && p.z < b.max.z + 0.5f;
        }

        void WriteGull(ref int quad, in Mote m, float now, Vector3 right, Vector3 up, Vector3 forward)
        {
            // Bursts of flapping between long glides; the wings form a shallow V seen from the camera.
            float flapping = Mathf.Clamp01(Mathf.Sin(now * 0.45f + m.Phase) * 2f);
            float wing = Mathf.Lerp(0.25f, Mathf.Sin(now * 9f + m.Phase), flapping);
            float span = m.Size;
            var center = m.Pos;
            for (int side = -1; side <= 1; side += 2)
            {
                var tip = center + right * (side * span) + up * (wing * span * 0.45f);
                var along = (tip - center) * 0.5f;
                var mid = center + along;
                var across = Vector3.Cross(along, forward).normalized * (span * 0.09f);
                WriteQuad(quad++, mid, along, across);
            }
        }

        void WriteQuad(int quad, Vector3 c, Vector3 a, Vector3 b)
        {
            int v = quad * 4;
            verts[v] = c - a - b;
            verts[v + 1] = c - a + b;
            verts[v + 2] = c + a + b;
            verts[v + 3] = c + a - b;
        }

        void Upload() =>
            mesh.SetVertices(verts, 0, verts.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

        void StepLightning()
        {
            if (!storm || nextLightning < 0f) return;
            float now = Time.unscaledTime;
            if (now >= nextLightning)
            {
                Lightning?.Invoke(0.32f);
                flickerAt = now + 0.14f;
                thunderAt = now + ThunderDelay;
                nextLightning = now + UnityEngine.Random.Range(LightningMin, LightningMax);
            }
            if (flickerAt >= 0f && now >= flickerAt)
            {
                flickerAt = -1f;
                Lightning?.Invoke(0.18f);
            }
            if (thunderAt >= 0f && now >= thunderAt)
            {
                thunderAt = -1f;
                Thunder?.Invoke();
            }
        }
    }
}
