using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    public sealed class ParcelView : MonoBehaviour
    {
        MeshRenderer body;
        MaterialBank materials;
        bool? free;

        public Parcel Parcel { get; private set; }

        /// <summary>When the parcel finishes its latest scheduled move.</summary>
        public float SettledAt { get; set; }

        public static ParcelView Create(Parcel parcel, Transform parent, Layout layout, MaterialBank materials)
        {
            var go = new GameObject($"Parcel {parcel.Id} {ParcelColors.ToCode(parcel.Color)}");
            go.transform.SetParent(parent, false);
            go.transform.position = layout.ParcelRest(parcel.Cell, false);
            go.transform.localScale = Layout.ParcelSize;

            var view = go.AddComponent<ParcelView>();
            view.Parcel = parcel;
            view.materials = materials;
            view.body = MaterialBank.Box("Body", go.transform, Vector3.zero, Vector3.one, materials.Parcel(parcel.Color, false))
                .GetComponent<MeshRenderer>();
            Symbols.Create(parcel.Color, go.transform, new Vector3(0f, 0.5f, 0f), 1f, materials.Symbol(parcel.Color));
            return view;
        }

        public bool InYard { get; private set; } = true;
        public bool IsFree => free == true;

        /// <summary>Free parcels are brighter and lifted so players can read the board at a glance.</summary>
        public void SetFree(bool value, Layout layout, Tweener tweener)
        {
            if (free == value) return;
            bool first = free == null;
            free = value;
            body.sharedMaterial = materials.Parcel(Parcel.Color, value);

            var target = layout.ParcelRest(Parcel.Cell, value);
            if (first)
            {
                transform.position = target;
                return;
            }
            var from = transform.position;
            // Stops as soon as the parcel is tapped, so it never fights the slide out of the yard.
            tweener.Run(tweener.Now, Timing.Lift, t =>
            {
                if (this && InYard) transform.position = Vector3.LerpUnclamped(from, target, Ease.OutBack(t));
            });
        }

        /// <summary>Shows the parcel at full color even while blocked, to point it out.</summary>
        public void Highlight() => body.sharedMaterial = materials.Parcel(Parcel.Color, true);

        public void MarkLeftYard()
        {
            InYard = false;
            body.sharedMaterial = materials.Parcel(Parcel.Color, true);
        }
    }
}
