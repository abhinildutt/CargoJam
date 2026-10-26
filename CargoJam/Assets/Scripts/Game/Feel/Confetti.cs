using CargoJam.Game.Presentation;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>A one-shot burst of colored flakes with simple gravity and drag. Destroys itself when done.</summary>
    public sealed class Confetti : MonoBehaviour
    {
        const float Lifetime = 1.8f;
        const float Gravity = 9f;
        const float Drag = 1.6f;

        Transform[] flakes;
        Vector3[] velocity;
        Vector3[] spin;
        float age;

        public static void Burst(Transform parent, Vector3 origin, int count, MaterialBank materials, int colorCount)
        {
            var go = new GameObject("Confetti");
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            var c = go.AddComponent<Confetti>();
            c.flakes = new Transform[count];
            c.velocity = new Vector3[count];
            c.spin = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var flake = MaterialBank.Box("Flake", go.transform, Vector3.zero,
                    new Vector3(0.16f, 0.02f, 0.1f), materials.Parcel(i % colorCount, true));
                flake.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                flake.transform.localRotation = Random.rotation;
                c.flakes[i] = flake.transform;
                var dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 1.6f + 0.6f;
                c.velocity[i] = dir.normalized * Random.Range(5f, 9f);
                c.spin[i] = Random.insideUnitSphere * 720f;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            float shrink = Mathf.Clamp01((Lifetime - age) / 0.4f);
            for (int i = 0; i < flakes.Length; i++)
            {
                velocity[i] += Vector3.down * (Gravity * dt);
                velocity[i] *= 1f - Drag * dt;
                flakes[i].localPosition += velocity[i] * dt;
                flakes[i].localRotation *= Quaternion.Euler(spin[i] * dt);
                flakes[i].localScale = new Vector3(0.16f, 0.02f, 0.1f) * shrink;
            }
            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
