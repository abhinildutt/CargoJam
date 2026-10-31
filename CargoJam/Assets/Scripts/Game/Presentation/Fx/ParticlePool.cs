using UnityEngine;
using UnityEngine.Rendering;

namespace CargoJam.Game.Presentation.Fx
{
    /// <summary>How one burst of particles behaves. A plain struct so emitting allocates nothing.</summary>
    public struct BurstSpec
    {
        public int Count;
        /// <summary>Random start offset radius around the origin.</summary>
        public float Radius;
        public float SpeedMin, SpeedMax;
        /// <summary>Extra upward speed added to every particle.</summary>
        public float Up;
        /// <summary>0 = spherical spread, 1 = flattened onto the ground plane (dust).</summary>
        public float Flatten;
        public float Gravity;
        /// <summary>Fraction of velocity lost per second.</summary>
        public float Drag;
        public float Life, LifeJitter;
        public Vector3 Size;
        /// <summary>Size multiplier reached at the end of life (above 1 for puffs that billow).</summary>
        public float Grow;
        /// <summary>Max spin in degrees per second.</summary>
        public float Spin;

        /// <summary>A quick star-like sparkle burst.</summary>
        public static BurstSpec Sparkle(int count) => new BurstSpec
        {
            Count = count, Radius = 0.15f, SpeedMin = 2.5f, SpeedMax = 5f, Up = 2f, Gravity = 6f, Drag = 3f,
            Life = 0.5f, LifeJitter = 0.25f, Size = new Vector3(0.11f, 0.11f, 0.11f), Grow = 0.6f, Spin = 540f,
        };

        /// <summary>Soft billowing puff that hugs the ground.</summary>
        public static BurstSpec Dust(int count) => new BurstSpec
        {
            Count = count, Radius = 0.25f, SpeedMin = 0.8f, SpeedMax = 2f, Up = 0.6f, Flatten = 0.7f, Gravity = -0.4f,
            Drag = 3.5f, Life = 0.55f, LifeJitter = 0.2f, Size = new Vector3(0.2f, 0.2f, 0.2f), Grow = 2.2f, Spin = 90f,
        };

        /// <summary>Hot sparks thrown up and falling back.</summary>
        public static BurstSpec Sparks(int count) => new BurstSpec
        {
            Count = count, Radius = 0.08f, SpeedMin = 2.5f, SpeedMax = 4.5f, Up = 3f, Gravity = 14f, Drag = 1.2f,
            Life = 0.55f, LifeJitter = 0.2f, Size = new Vector3(0.07f, 0.07f, 0.16f), Grow = 0.4f, Spin = 720f,
        };

        /// <summary>Flat paper flakes for celebrations.</summary>
        public static BurstSpec Confetti(int count, float speed) => new BurstSpec
        {
            Count = count, SpeedMin = speed * 0.6f, SpeedMax = speed, Up = speed * 0.45f, Gravity = 9f, Drag = 1.6f,
            Life = 1.8f, LifeJitter = 0.3f, Size = new Vector3(0.16f, 0.02f, 0.1f), Grow = 1f, Spin = 720f,
        };

        /// <summary>
        /// The v3 win confetti: fewer, larger cards that float down slowly and turn lazily, so the
        /// celebration reads as refined rather than busy.
        /// </summary>
        public static BurstSpec Ribbons(int count, float speed) => new BurstSpec
        {
            Count = count, Radius = 0.3f, SpeedMin = speed * 0.55f, SpeedMax = speed, Up = speed * 0.5f, Gravity = 4.5f,
            Drag = 2.2f, Life = 2.4f, LifeJitter = 0.2f, Size = new Vector3(0.28f, 0.02f, 0.17f), Grow = 1f, Spin = 300f,
        };

        /// <summary>Soft glints shed behind something moving: barely any speed, a gentle lift.</summary>
        public static BurstSpec Trail(int count) => new BurstSpec
        {
            Count = count, Radius = 0.22f, SpeedMin = 0.1f, SpeedMax = 0.5f, Up = 0.6f, Gravity = 0.4f, Drag = 2f,
            Life = 0.65f, LifeJitter = 0.25f, Size = new Vector3(0.1f, 0.1f, 0.1f), Grow = 0.3f, Spin = 360f,
        };
    }

    /// <summary>
    /// A fixed pool of small cube particles with simple ballistic motion. One Update loop, no
    /// per-frame allocations, no shadows. When the pool runs out the oldest particle is recycled.
    /// Lives outside the level root so it survives level rebuilds.
    /// </summary>
    public sealed class ParticlePool : MonoBehaviour
    {
        struct Particle
        {
            public Vector3 Velocity;
            public Vector3 SpinRate;
            public Vector3 Size;
            public float Born, Life, Grow, Gravity, Drag;
            public bool Alive;
        }

        Transform[] transforms;
        MeshRenderer[] renderers;
        Particle[] particles;
        int next;
        int aliveCount;

        /// <summary>Creates a pool of <paramref name="capacity"/> particles under <paramref name="parent"/>.</summary>
        public static ParticlePool Create(Transform parent, int capacity, Material initial)
        {
            var go = new GameObject("FX Particles");
            go.transform.SetParent(parent, false);
            var pool = go.AddComponent<ParticlePool>();
            pool.transforms = new Transform[capacity];
            pool.renderers = new MeshRenderer[capacity];
            pool.particles = new Particle[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var box = MaterialBank.Box("P", go.transform, Vector3.zero, Vector3.one, initial);
                var r = box.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.enabled = false;
                pool.transforms[i] = box.transform;
                pool.renderers[i] = r;
            }
            return pool;
        }

        /// <summary>Emits a burst using one material.</summary>
        public void Emit(Vector3 origin, in BurstSpec spec, Material material) => Emit(origin, spec, material, null, 0);

        /// <summary>Emits a burst cycling through <paramref name="palette"/> (first <paramref name="paletteCount"/> entries).</summary>
        public void Emit(Vector3 origin, in BurstSpec spec, Material[] palette, int paletteCount) =>
            Emit(origin, spec, null, palette, paletteCount);

        void Emit(Vector3 origin, in BurstSpec spec, Material single, Material[] palette, int paletteCount)
        {
            float now = Time.time;
            for (int n = 0; n < spec.Count; n++)
            {
                int i = next;
                next = (next + 1) % particles.Length;
                if (!particles[i].Alive) aliveCount++;

                var dir = Random.insideUnitSphere;
                dir.y *= 1f - spec.Flatten;
                if (dir.sqrMagnitude < 1e-4f) dir = Vector3.up;
                dir.Normalize();
                var velocity = dir * Random.Range(spec.SpeedMin, spec.SpeedMax) + Vector3.up * spec.Up;

                ref var p = ref particles[i];
                p.Alive = true;
                p.Born = now;
                p.Life = Mathf.Max(0.05f, spec.Life + Random.Range(-spec.LifeJitter, spec.LifeJitter) * spec.Life);
                p.Velocity = velocity;
                p.SpinRate = Random.insideUnitSphere * spec.Spin;
                p.Size = spec.Size * Random.Range(0.75f, 1.25f);
                p.Grow = spec.Grow;
                p.Gravity = spec.Gravity;
                p.Drag = spec.Drag;

                var tr = transforms[i];
                tr.position = origin + Random.insideUnitSphere * spec.Radius;
                tr.rotation = Random.rotation;
                tr.localScale = Vector3.zero;
                var r = renderers[i];
                r.sharedMaterial = single != null ? single : palette[n % Mathf.Max(1, paletteCount)];
                r.enabled = true;
            }
        }

        /// <summary>Hides every live particle at once (level teardown).</summary>
        public void Clear()
        {
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Alive = false;
                renderers[i].enabled = false;
            }
            aliveCount = 0;
        }

        void Update()
        {
            if (aliveCount == 0) return;
            float now = Time.time;
            float dt = Time.deltaTime;
            for (int i = 0; i < particles.Length; i++)
            {
                ref var p = ref particles[i];
                if (!p.Alive) continue;
                float k = (now - p.Born) / p.Life;
                if (k >= 1f)
                {
                    p.Alive = false;
                    renderers[i].enabled = false;
                    aliveCount--;
                    continue;
                }
                p.Velocity += Vector3.down * (p.Gravity * dt);
                p.Velocity *= Mathf.Max(0f, 1f - p.Drag * dt);
                var tr = transforms[i];
                tr.position += p.Velocity * dt;
                tr.rotation *= Quaternion.Euler(p.SpinRate * dt);
                // Pop in fast, then shrink away over the last third; Grow scales across the life.
                float pop = Mathf.Min(1f, k * 12f);
                float fade = Mathf.Min(1f, (1f - k) * 3f);
                tr.localScale = p.Size * (pop * fade * Mathf.Lerp(1f, p.Grow, k));
            }
        }
    }
}
