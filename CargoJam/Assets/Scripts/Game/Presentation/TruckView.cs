using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>A greybox truck driving along +X: colored cab at the front, open bed behind it.</summary>
    public sealed class TruckView : MonoBehaviour
    {
        const float BedCenterX = -0.4f;
        const float BedTop = 0.52f;
        const float SlotSpacing = 0.55f;

        public Truck Truck { get; private set; }

        /// <summary>When this truck is parked at the loading bay and can receive parcels.</summary>
        public float BayReadyAt { get; set; }

        /// <summary>When the last parcel scheduled for this truck lands in its bed.</summary>
        public float LastLandingAt { get; set; }

        /// <summary>When the truck's latest scheduled drive finishes.</summary>
        public float MoveFreeAt { get; set; }

        Transform body;

        public Transform Body => body;

        public static TruckView Create(Truck truck, Transform parent, MaterialBank materials)
        {
            var go = new GameObject($"Truck {truck.Index} {ParcelColors.ToCode(truck.Color)}");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<TruckView>();
            view.Truck = truck;

            // The body is a child so squash/stretch never fights the drive tweens on the root.
            view.body = new GameObject("Body").transform;
            view.body.SetParent(go.transform, false);
            var b = view.body;

            MaterialBank.Box("Bed", b, new Vector3(BedCenterX, 0.36f, 0f), new Vector3(1.8f, 0.32f, 1.1f), materials.TruckBody(truck.Color));
            MaterialBank.Box("Cab", b, new Vector3(0.95f, 0.55f, 0f), new Vector3(0.75f, 0.72f, 1.05f), materials.TruckCab(truck.Color));
            MaterialBank.Box("Window", b, new Vector3(1.33f, 0.68f, 0f), new Vector3(0.04f, 0.28f, 0.8f), materials.Window);
            Symbols.Create(truck.Color, b, new Vector3(0.95f, 0.91f, 0f), 0.95f, materials.Symbol(truck.Color));
            foreach (float x in new[] { -0.95f, 0.95f })
            foreach (float z in new[] { -0.5f, 0.5f })
                MaterialBank.Box("Wheel", b, new Vector3(x, 0.17f, z), new Vector3(0.38f, 0.34f, 0.16f), materials.Wheel);

            return view;
        }

        /// <summary>Bed slot position in the truck's local space.</summary>
        public Vector3 SlotLocal(int slot)
        {
            float x = BedCenterX + (slot - (Truck.Capacity - 1) * 0.5f) * SlotSpacing;
            return new Vector3(x, BedTop + Layout.ParcelSize.y * Layout.InTruckScale * 0.5f, 0f);
        }
    }
}
