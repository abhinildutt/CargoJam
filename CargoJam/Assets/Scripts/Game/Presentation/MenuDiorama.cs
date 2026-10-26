using System.Collections.Generic;
using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The living backdrop behind the home screen: a depot with a warehouse, a road where a convoy of
    /// loaded trucks keeps rolling past, and stacks of crates in the foreground that hop now and then.
    /// Built from the same parts as a level, so it always matches the current route theme.
    /// </summary>
    public sealed class MenuDiorama : MonoBehaviour
    {
        const float RoadZ = 0f;
        const float TruckSpeed = 2.4f;
        const float LoopHalfWidth = 11f;
        const int TruckCount = 4;
        const float CrateScale = 0.82f;

        /// <summary>What the camera keeps in view.</summary>
        public static Bounds Bounds
        {
            get
            {
                var b = new Bounds();
                b.SetMinMax(new Vector3(-4.4f, 0f, -3.3f), new Vector3(4.4f, 1.8f, 3.4f));
                return b;
            }
        }

        readonly List<Transform> trucks = new List<Transform>();
        readonly List<Transform> truckBodies = new List<Transform>();
        readonly List<Transform> hoppers = new List<Transform>();
        readonly List<Vector3> hopperRest = new List<Vector3>();
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
            MaterialBank.Box("Ground", root, new Vector3(0f, -0.06f, 0f), new Vector3(60f, 0.1f, 60f), m.Ground);
            MaterialBank.Box("Road", root, new Vector3(0f, -0.005f, RoadZ), new Vector3(60f, 0.02f, 1.8f), m.Road);
            for (float x = -14f; x <= 14f; x += 1.6f)
                MaterialBank.Box("Lane", root, new Vector3(x, 0.01f, RoadZ), new Vector3(0.7f, 0.01f, 0.08f), m.Symbol(0));

            BuildWarehouse(root, m);
            BuildConvoy(root, m);

            // Foreground crate stacks, a pyramid either side, in the parcel colors.
            BuildStack(root, m, new Vector3(-2.75f, 0f, -2.35f), new[] { 0, 1, 3, 4, 2, 5 }, 8f);
            BuildStack(root, m, new Vector3(2.75f, 0f, -2.35f), new[] { 2, 5, 1, 0, 3, 4 }, -6f);
            // A couple of loose crates by the dock.
            Crate(root, m, 3, new Vector3(-0.9f, 0f, 1.75f), 0.7f, 12f);
            Crate(root, m, 1, new Vector3(-0.15f, 0f, 1.85f), 0.7f, -9f);
        }

        void BuildWarehouse(Transform root, MaterialBank m)
        {
            var depot = new GameObject("Warehouse").transform;
            depot.SetParent(root, false);
            depot.localPosition = new Vector3(0f, 0f, 2.9f);
            MaterialBank.Box("Dock", depot, new Vector3(0f, 0.12f, -0.55f), new Vector3(7.6f, 0.24f, 1.3f), m.Tile);
            MaterialBank.Box("Hall", depot, new Vector3(0f, 0.85f, 0.35f), new Vector3(7f, 1.5f, 1.2f), m.Steel);
            MaterialBank.Box("Roof", depot, new Vector3(0f, 1.66f, 0.3f), new Vector3(7.5f, 0.18f, 1.5f), m.Road);
            MaterialBank.Box("Sign", depot, new Vector3(0f, 1.95f, 0.1f), new Vector3(2.4f, 0.42f, 0.12f), m.Gold);
            for (int i = 0; i < 3; i++)
            {
                float x = -2.3f + i * 2.3f;
                MaterialBank.Box("Door", depot, new Vector3(x, 0.72f, -0.26f), new Vector3(1.3f, 1.05f, 0.04f), m.Wheel);
                MaterialBank.Box("Stripe", depot, new Vector3(x, 1.28f, -0.27f), new Vector3(1.4f, 0.08f, 0.04f), m.Edge);
            }
        }

        void BuildConvoy(Transform root, MaterialBank m)
        {
            float spacing = LoopHalfWidth * 2f / TruckCount;
            for (int i = 0; i < TruckCount; i++)
            {
                // Real truck views, so the convoy looks exactly like the trucks in a level.
                int color = (i * 2) % ParcelColors.Count;
                var view = TruckView.Create(new Truck(i, color, 3), root, m);
                view.transform.localPosition = new Vector3(-LoopHalfWidth + i * spacing, 0f, RoadZ);
                for (int slot = 0; slot < 3; slot++)
                {
                    int crateColor = slot == 1 ? color : (color + slot + 1) % ParcelColors.Count;
                    var crate = Crate(view.Body, m, crateColor, Vector3.zero, Layout.InTruckScale, 0f);
                    crate.localPosition = view.SlotLocal(slot) - Vector3.up * Layout.ParcelSize.y * Layout.InTruckScale * 0.5f;
                }
                trucks.Add(view.transform);
                truckBodies.Add(view.Body);
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
                    var crate = Crate(root, m, colors[c++ % colors.Length], pos, CrateScale, yaw * 0.4f + (i - 1) * 2f);
                    if (row == 2 || (row == 1 && i == 0))
                    {
                        hoppers.Add(crate);
                        hopperRest.Add(crate.localPosition);
                    }
                }
            }
        }

        /// <summary>A parcel-style crate with its color symbol, resting on <paramref name="bottomCenter"/>.</summary>
        static Transform Crate(Transform parent, MaterialBank m, int color, Vector3 bottomCenter, float scale, float yaw)
        {
            var crate = new GameObject("Crate").transform;
            crate.SetParent(parent, false);
            crate.localScale = Layout.ParcelSize * scale;
            crate.localPosition = bottomCenter + Vector3.up * Layout.ParcelSize.y * scale * 0.5f;
            crate.localRotation = Quaternion.Euler(0f, yaw, 0f);
            MaterialBank.Box("Body", crate, Vector3.zero, Vector3.one, m.Parcel(color, true));
            Symbols.Create(color, crate, new Vector3(0f, 0.5f, 0f), 1f, m.Symbol(color));
            return crate;
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
                truckBodies[i].localPosition = new Vector3(0f, 0.025f * Mathf.Abs(Mathf.Sin(clock * 11f + i * 1.7f)), 0f);
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
    }
}
