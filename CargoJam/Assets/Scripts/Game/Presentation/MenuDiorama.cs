using System.Collections.Generic;
using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The living backdrop behind the home screen: a depot with a loading dock, two loaded flatbeds
    /// parked in its yard, a road where a convoy keeps rolling past, and parcel stacks in the
    /// foreground that hop now and then. Built from the shared materials, so it takes the biome's
    /// colors; the destination's landmark, props, ground and light come from
    /// <see cref="WorldEnvironment.ShowMenu"/>, which dresses around <see cref="Bounds"/>.
    /// </summary>
    public sealed class MenuDiorama : MonoBehaviour
    {
        /// <summary>The convoy road (along x).</summary>
        public const float RoadZ = -1.3f;
        const float TruckSpeed = 2.2f;
        const float LoopHalfWidth = 12f;
        const int ConvoyCount = 3;
        const float CrateScale = 0.82f;
        const float YardFarZ = 3.05f;
        const float DepotZ = 3.7f;

        /// <summary>What the camera keeps in view (the environment leaves this area to the diorama).</summary>
        public static Bounds Bounds
        {
            get
            {
                var b = new Bounds();
                b.SetMinMax(new Vector3(-4.6f, 0f, -3.5f), new Vector3(4.6f, 2.2f, 4.4f));
                return b;
            }
        }

        readonly List<Transform> trucks = new List<Transform>();
        readonly List<Transform> truckBodies = new List<Transform>();
        readonly List<Transform> hoppers = new List<Transform>();
        readonly List<Vector3> hopperRest = new List<Vector3>();
        Mesh depotMesh;
        float clock;

        public static MenuDiorama Create(MaterialBank materials)
        {
            var go = new GameObject("Menu Diorama");
            var menu = go.AddComponent<MenuDiorama>();
            menu.Build(materials);
            return menu;
        }

        void Build(MaterialBank m)
        {
            var root = transform;
            Scenery.BuildRoad(root, RoadZ, m);
            BuildDepot(root, m);
            BuildParked(root, m);
            BuildConvoy(root, m);

            // Foreground parcel stacks, a pyramid either side.
            BuildStack(root, m, new Vector3(-3f, 0f, -2.9f), new[] { 0, 1, 3, 4, 2, 5 }, 8f);
            BuildStack(root, m, new Vector3(3f, 0f, -2.9f), new[] { 2, 5, 1, 0, 3, 4 }, -6f);
        }

        /// <summary>The yard apron, loading dock and warehouse, as one mesh (one draw per material).</summary>
        void BuildDepot(Transform root, MaterialBank m)
        {
            // Submeshes: 0 apron, 1 walls, 2 roof, 3 safety yellow, 4 doors, 5 lamps, 6 sign.
            var b = new MeshBuilder(7);
            float yardNear = RoadZ + Scenery.RoadHalfWidth + 0.64f;
            Box(b, 0, new Vector3(0f, -0.017f, (yardNear + YardFarZ) * 0.5f), new Vector3(9.6f, 0.03f, YardFarZ - yardNear));

            // Loading dock platform with a yellow safety edge.
            Box(b, 0, new Vector3(0f, 0.14f, DepotZ - 0.35f), new Vector3(7.6f, 0.28f, 0.7f));
            Box(b, 3, new Vector3(0f, 0.285f, DepotZ - 0.67f), new Vector3(7.6f, 0.03f, 0.07f));
            // Warehouse hall and roof.
            Box(b, 1, new Vector3(0f, 0.85f, DepotZ + 0.65f), new Vector3(7.2f, 1.7f, 1.3f), 0.01f);
            Box(b, 2, new Vector3(0f, 1.77f, DepotZ + 0.62f), new Vector3(7.6f, 0.16f, 1.6f));
            for (int i = 0; i < 3; i++)
            {
                float x = -2.4f + i * 2.4f;
                Box(b, 4, new Vector3(x, 0.86f, DepotZ - 0.01f), new Vector3(1.45f, 1.1f, 0.04f));
                Box(b, 3, new Vector3(x, 1.48f, DepotZ - 0.02f), new Vector3(1.55f, 0.08f, 0.04f));
                Box(b, 5, new Vector3(x, 1.6f, DepotZ - 0.1f), new Vector3(0.34f, 0.06f, 0.16f));
            }
            Box(b, 6, new Vector3(0f, 2.1f, DepotZ + 0.2f), new Vector3(2.8f, 0.5f, 0.12f), 0.04f);
            Box(b, 2, new Vector3(-1.1f, 1.92f, DepotZ + 0.25f), new Vector3(0.08f, 0.3f, 0.08f));
            Box(b, 2, new Vector3(1.1f, 1.92f, DepotZ + 0.25f), new Vector3(0.08f, 0.3f, 0.08f));

            depotMesh = b.ToMesh("Depot");
            var r = MeshKit.Show("Depot", root, depotMesh, m.Apron);
            r.sharedMaterials = new[] { m.Apron, m.Steel, m.Road, m.Edge, m.Wheel, m.Glow, m.Gold };
        }

        static void Box(MeshBuilder b, int submesh, Vector3 center, Vector3 size, float bevel = 0f) =>
            MeshKit.AddBox(b, submesh, Matrix4x4.TRS(center, Quaternion.identity, size), bevel, Vector2.zero);

        /// <summary>Two loaded flatbeds parked in the yard at easy angles.</summary>
        void BuildParked(Transform root, MaterialBank m)
        {
            Park(root, m, 1, new Vector3(-2.15f, 0f, 1.45f), 188f, new[] { 1, 1, 1 });
            Park(root, m, 5, new Vector3(2.25f, 0f, 1.6f), -8f, new[] { 5, 5, -1 });
        }

        static void Park(Transform root, MaterialBank m, int color, Vector3 position, float yaw, int[] load)
        {
            var view = TruckView.Create(new Truck(100 + color, color, 3), root, m);
            view.transform.localPosition = position;
            view.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Load(view, m, load);
        }

        void BuildConvoy(Transform root, MaterialBank m)
        {
            float spacing = LoopHalfWidth * 2f / ConvoyCount;
            for (int i = 0; i < ConvoyCount; i++)
            {
                int color = (i * 2 + 2) % ParcelColors.Count;
                var view = TruckView.Create(new Truck(i, color, 3), root, m);
                view.transform.localPosition = new Vector3(-LoopHalfWidth + i * spacing, 0f, RoadZ);
                Load(view, m, new[] { color, color, color });
                trucks.Add(view.transform);
                truckBodies.Add(view.Body);
            }
        }

        /// <summary>Parcels on a truck's bed slots (-1 leaves a slot empty).</summary>
        static void Load(TruckView view, MaterialBank m, int[] colors)
        {
            for (int slot = 0; slot < colors.Length; slot++)
            {
                if (colors[slot] < 0) continue;
                var crate = ParcelView.CreateCrate(colors[slot], view.Body, m, Layout.InTruckScale);
                crate.localPosition = view.SlotLocal(slot);
            }
        }

        void BuildStack(Transform root, MaterialBank m, Vector3 center, int[] colors, float yaw)
        {
            float step = CrateScale * 1.06f;
            float h = Layout.ParcelSize.y * CrateScale;
            int c = 0;
            // 3 on the bottom, 2 in the middle, 1 on top.
            for (int row = 0; row < 3; row++)
            {
                int count = 3 - row;
                for (int i = 0; i < count; i++)
                {
                    var pos = center + new Vector3((i - (count - 1) * 0.5f) * step, row * h, row * 0.05f);
                    var crate = ParcelView.CreateCrate(colors[c++ % colors.Length], root, m, CrateScale);
                    crate.localPosition = pos + Vector3.up * h * 0.5f;
                    crate.localRotation = Quaternion.Euler(0f, yaw * 0.4f + (i - 1) * 2f, 0f);
                    if (row == 2 || (row == 1 && i == 0))
                    {
                        hoppers.Add(crate);
                        hopperRest.Add(crate.localPosition);
                    }
                }
            }
        }

        void Update()
        {
            clock += Time.deltaTime;
            float span = LoopHalfWidth * 2f;
            for (int i = 0; i < trucks.Count; i++)
            {
                var t = trucks[i];
                var p = t.localPosition;
                p.x += TruckSpeed * Time.deltaTime;
                if (p.x > LoopHalfWidth) p.x -= span;
                t.localPosition = p;
                // A little road rumble, out of step between trucks.
                truckBodies[i].localPosition = new Vector3(0f, 0.02f * Mathf.Abs(Mathf.Sin(clock * 11f + i * 1.7f)), 0f);
            }

            for (int i = 0; i < hoppers.Count; i++)
            {
                // Each crate hops briefly every few seconds, staggered so something is always moving.
                float phase = Mathf.Repeat(clock * 0.45f + i * 0.31f, 1f);
                float hop = phase < 0.12f ? Mathf.Sin(phase / 0.12f * Mathf.PI) * 0.3f : 0f;
                // A small squash as it lands, recovering quickly.
                float squash = phase < 0.12f ? 0f : 0.1f * Mathf.Max(0f, 1f - (phase - 0.12f) / 0.06f);
                hoppers[i].localPosition = hopperRest[i] + Vector3.up * hop;
                var s = Layout.ParcelSize * CrateScale;
                hoppers[i].localScale = new Vector3(s.x * (1f + squash * 0.5f), s.y * (1f - squash), s.z * (1f + squash * 0.5f));
            }
        }

        void OnDestroy()
        {
            if (depotMesh != null) Destroy(depotMesh);
        }
    }
}
