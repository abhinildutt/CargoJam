using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    public sealed class ParcelView : MonoBehaviour
    {
        MeshRenderer body;
        MaterialBank materials;
        Transform symbol;
        Transform tarpMark;
        Transform padlock;
        bool? free;
        bool hidden;

        public Parcel Parcel { get; private set; }

        /// <summary>When the parcel finishes its latest scheduled move.</summary>
        public float SettledAt { get; set; }

        public bool InYard { get; private set; } = true;
        public bool IsFree => free == true;
        public bool IsHidden => hidden;
        public bool IsLocked => padlock != null;

        public static ParcelView Create(Parcel parcel, Transform parent, Layout layout, MaterialBank materials,
            bool hidden = false, bool locked = false, bool isKey = false)
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
            view.symbol = Symbols.Create(parcel.Color, go.transform, new Vector3(0f, 0.5f, 0f), 1f, materials.Symbol(parcel.Color));
            if (hidden) view.Hide();
            if (locked) view.AddPadlock();
            if (isKey) view.AddKeyBadge();
            return view;
        }

        /// <summary>Free parcels are brighter and lifted so players can read the board at a glance.</summary>
        public void SetFree(bool value, Layout layout, Tweener tweener)
        {
            if (free == value) return;
            bool first = free == null;
            free = value;
            ApplyMaterial();

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
        public void Highlight() => body.sharedMaterial = hidden ? materials.Tarp : materials.Parcel(Parcel.Color, true);

        public void MarkLeftYard()
        {
            InYard = false;
            free = null;
            ApplyMaterial(forceBright: true);
        }

        /// <summary>Undo put it back on its yard cell.</summary>
        public void MarkBackInYard()
        {
            InYard = true;
            free = null;
        }

        /// <summary>Mystery reveal: the tarp comes off and the real color shows, with a flip.</summary>
        public void Reveal(Tweener tweener, float at)
        {
            if (!hidden) return;
            tweener.Run(at, 0.3f, t =>
            {
                if (!this) return;
                float angle = t * 180f;
                if (t >= 0.5f && hidden)
                {
                    hidden = false;
                    if (tarpMark) Destroy(tarpMark.gameObject);
                    symbol.gameObject.SetActive(true);
                    ApplyMaterial();
                    angle -= 180f;
                }
                body.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                symbol.localRotation = body.transform.localRotation;
            }, null, () => { if (this) body.transform.localRotation = symbol.localRotation = Quaternion.identity; });
        }

        /// <summary>The padlock springs up and away.</summary>
        public void Unlock(Tweener tweener, float at)
        {
            if (padlock == null) return;
            var lockTransform = padlock;
            padlock = null;
            var start = lockTransform.localPosition;
            tweener.Run(at, 0.35f, t =>
            {
                if (!lockTransform) return;
                lockTransform.localPosition = start + new Vector3(0f, 1.6f * t, 0f);
                lockTransform.localScale = Vector3.one * (1f - t);
            }, null, () => { if (lockTransform) Destroy(lockTransform.gameObject); });
        }

        void ApplyMaterial(bool forceBright = false)
        {
            bool bright = forceBright || free == true;
            body.sharedMaterial = hidden ? (bright ? materials.Tarp : materials.TarpDim) : materials.Parcel(Parcel.Color, bright);
        }

        void Hide()
        {
            hidden = true;
            symbol.gameObject.SetActive(false);
            ApplyMaterial();
            // A blocky "?" made of five bars on the lid.
            tarpMark = new GameObject("Mystery").transform;
            tarpMark.SetParent(transform, false);
            tarpMark.localPosition = new Vector3(0f, 0.5f, 0f);
            var m = materials.Symbol(-1);
            MaterialBank.Box("Top", tarpMark, new Vector3(0f, 0f, 0.22f), new Vector3(0.36f, 0.04f, 0.1f), m);
            MaterialBank.Box("Right", tarpMark, new Vector3(0.15f, 0f, 0.12f), new Vector3(0.1f, 0.04f, 0.26f), m);
            MaterialBank.Box("Middle", tarpMark, new Vector3(0.05f, 0f, 0.02f), new Vector3(0.24f, 0.04f, 0.1f), m);
            MaterialBank.Box("Stem", tarpMark, new Vector3(-0.02f, 0f, -0.08f), new Vector3(0.1f, 0.04f, 0.14f), m);
            MaterialBank.Box("Dot", tarpMark, new Vector3(-0.02f, 0f, -0.26f), new Vector3(0.1f, 0.04f, 0.1f), m);
        }

        void AddPadlock()
        {
            padlock = new GameObject("Padlock").transform;
            padlock.SetParent(transform, false);
            padlock.localPosition = new Vector3(0f, 0.62f, -0.05f);
            MaterialBank.Box("Body", padlock, Vector3.zero, new Vector3(0.46f, 0.3f, 0.34f), materials.Gold);
            MaterialBank.Box("Shackle L", padlock, new Vector3(-0.15f, 0.24f, 0f), new Vector3(0.07f, 0.24f, 0.07f), materials.Steel);
            MaterialBank.Box("Shackle R", padlock, new Vector3(0.15f, 0.24f, 0f), new Vector3(0.07f, 0.24f, 0.07f), materials.Steel);
            MaterialBank.Box("Shackle Top", padlock, new Vector3(0f, 0.36f, 0f), new Vector3(0.37f, 0.07f, 0.07f), materials.Steel);
            MaterialBank.Box("Keyhole", padlock, new Vector3(0f, -0.02f, -0.172f), new Vector3(0.07f, 0.12f, 0.01f), materials.Wheel);
        }

        void AddKeyBadge()
        {
            var key = new GameObject("Key").transform;
            key.SetParent(transform, false);
            key.localPosition = new Vector3(0.3f, 0.54f, 0.3f);
            MaterialBank.Box("Bow", key, Vector3.zero, new Vector3(0.2f, 0.06f, 0.2f), materials.Gold);
            MaterialBank.Box("Blade", key, new Vector3(-0.16f, 0f, -0.16f), new Vector3(0.24f, 0.06f, 0.07f), materials.Gold)
                .transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        }
    }
}
