using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// World positions for one level. The yard sits on the XZ plane with row 0 at z = 0 and later
    /// rows toward the player (negative z). The dock and the truck road are beyond row 0.
    /// </summary>
    public sealed class Layout
    {
        public const float CellSize = 1f;
        public static readonly Vector3 ParcelSize = new Vector3(0.82f, 0.62f, 0.82f);
        public const float FreeLift = 0.07f;
        public const float InTruckScale = 0.5f;

        public const float DockEdgeZ = 0.62f;
        public const float DockZ = 1.6f;
        public const float DockSpacing = 1.05f;
        public const float RoadZ = 3.25f;
        public const float BayX = 1.25f;
        public const float QueueSpacing = 3.1f;
        public const float DepartDistance = 9f;

        public int Width { get; }
        public int Height { get; }
        public int DockSlots { get; }

        public Layout(int width, int height, int dockSlots)
        {
            Width = width;
            Height = height;
            DockSlots = dockSlots;
        }

        public Vector3 CellCenter(Cell c) => new Vector3((c.X - (Width - 1) * 0.5f) * CellSize, 0f, -c.Y * CellSize);

        /// <summary>Where a parcel sits on the ground; free parcels are lifted a little.</summary>
        public Vector3 ParcelRest(Cell c, bool free) =>
            CellCenter(c) + Vector3.up * (ParcelSize.y * 0.5f + (free ? FreeLift : 0f));

        /// <summary>The point just past the dock edge where a parcel leaves the yard.</summary>
        public Vector3 YardExit(Cell rowZeroCell) =>
            ParcelRest(rowZeroCell, true) + Vector3.forward * (DockEdgeZ + 0.15f);

        public Vector3 DockSlotCenter(int slot) => new Vector3((slot - (DockSlots - 1) * 0.5f) * DockSpacing, 0f, DockZ);

        public Vector3 DockRest(int slot) => DockSlotCenter(slot) + Vector3.up * (ParcelSize.y * 0.5f + 0.04f);

        public Vector3 Bay => new Vector3(BayX, 0f, RoadZ);

        /// <summary>Truck positions on the road: 0 is the loading bay, 1 the next truck, and so on.</summary>
        public Vector3 QueuePosition(int offset) => Bay + Vector3.left * (QueueSpacing * offset);

        public Vector3 DepartPosition => Bay + Vector3.right * DepartDistance;

        /// <summary>The area the camera must keep in view.</summary>
        public Bounds ContentBounds
        {
            get
            {
                float halfWidth = Mathf.Max(Width * CellSize * 0.5f, DockSlots * DockSpacing * 0.5f, BayX + 1.4f) + 0.25f;
                float near = -(Height - 1) * CellSize - 0.6f;
                float far = RoadZ + 0.8f;
                var b = new Bounds();
                b.SetMinMax(new Vector3(-halfWidth, 0f, near), new Vector3(halfWidth, 1f, far));
                return b;
            }
        }
    }
}
