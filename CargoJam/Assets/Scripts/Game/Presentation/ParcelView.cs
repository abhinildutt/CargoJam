using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    public sealed class ParcelView : MonoBehaviour
    {
        /// <summary>Edge chamfer of the parcel box, in unit-box space.</summary>
        const float Bevel = 0.08f;
        /// <summary>Half width of the packing tape running front to back over the lid, in unit-box space.</summary>
        const float TapeHalfWidth = 0.075f;
        /// <summary>How far the tape sits proud of the box, so it never z-fights.</summary>
        const float TapeLift = 0.004f;
        /// <summary>The shipping label on the lid: paper size, thickness, and the symbol's scale on it.</summary>
        const float LabelSize = 0.66f;
        const float LabelThickness = 0.012f;
        const float StampScale = 0.78f;
        /// <summary>A small hand-applied tilt per color, so labels don't read as a printed grid.</summary>
        static readonly float[] LabelYaw = { -7f, 5f, -4f, 6f, -5f, 3f };

        static readonly Mesh[] boxMeshes = new Mesh[6];
        static Mesh plainMesh;
        static Mesh mysteryMesh;
        static Mesh shackleMesh;
        static Mesh keyMesh;

        MeshRenderer body;
        MeshFilter bodyFilter;
        MaterialBank materials;
        Transform tarpMark;
        Transform padlock;
        Transform keyBadge;
        bool? free;
        bool hidden;

        public Parcel Parcel { get; private set; }

        /// <summary>When the parcel finishes its latest scheduled move.</summary>
        public float SettledAt { get; set; }

        public bool InYard { get; private set; } = true;
        public bool IsFree => free == true;
        public bool IsHidden => hidden;
        public bool IsLocked => padlock != null;
        public bool HasKeyBadge => keyBadge != null;

        /// <summary>The lock this parcel opened is gone (unlocked, or lifted by the Crane).</summary>
        public void RemoveKeyBadge()
        {
            if (keyBadge != null) Destroy(keyBadge.gameObject);
            keyBadge = null;
        }

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
            view.body = MeshKit.Show("Body", go.transform, BoxMesh(parcel.Color), materials.ParcelSkin(parcel.Color, false));
            view.bodyFilter = view.body.GetComponent<MeshFilter>();
            if (hidden) view.Hide();
            if (locked) view.AddPadlock();
            if (isKey) view.AddKeyBadge();
            return view;
        }

        /// <summary>
        /// A decorative parcel (menu stacks, loaded trucks): the same taped box with its symbol, one
        /// draw call. Its local scale is <see cref="Layout.ParcelSize"/> × <paramref name="scale"/>.
        /// </summary>
        public static Transform CreateCrate(int color, Transform parent, MaterialBank materials, float scale = 1f)
        {
            var r = MeshKit.Show("Crate", parent, BoxMesh(color), materials.ParcelSkin(color, true));
            r.transform.localScale = Layout.ParcelSize * scale;
            return r.transform;
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
        public void Highlight() => body.sharedMaterial = hidden ? materials.Tarp : materials.ParcelSkin(Parcel.Color, true);

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
            var flip = body.transform;
            tweener.Run(at, 0.3f, t =>
            {
                if (!this) return;
                float angle = t * 180f;
                if (t >= 0.5f && hidden)
                {
                    hidden = false;
                    if (tarpMark) Destroy(tarpMark.gameObject);
                    bodyFilter.sharedMesh = BoxMesh(Parcel.Color);
                    ApplyMaterial();
                    angle -= 180f;
                }
                flip.localRotation = Quaternion.Euler(0f, 0f, angle);
            }, null, () => { if (this) flip.localRotation = Quaternion.identity; });
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
            body.sharedMaterial = hidden ? (bright ? materials.Tarp : materials.TarpDim) : materials.ParcelSkin(Parcel.Color, bright);
        }

        void Hide()
        {
            hidden = true;
            // The plain box: the symbol's relief must not show through the tarp.
            bodyFilter.sharedMesh = PlainMesh();
            ApplyMaterial();
            // A blocky "?" made of five bars on the lid.
            var mark = MeshKit.Show("Mystery", transform, MysteryMesh(), materials.Symbol(-1), castShadows: false);
            tarpMark = mark.transform;
            tarpMark.localPosition = new Vector3(0f, 0.5f, 0f);
        }

        void AddPadlock()
        {
            padlock = new GameObject("Padlock").transform;
            padlock.SetParent(transform, false);
            padlock.localPosition = new Vector3(0f, 0.7f, -0.05f);
            padlock.localScale = Vector3.one * 1.35f;
            MaterialBank.Box("Body", padlock, Vector3.zero, new Vector3(0.46f, 0.3f, 0.34f), materials.Gold);
            MeshKit.Show("Shackle", padlock, ShackleMesh(), materials.Steel);
            MaterialBank.Box("Keyhole", padlock, new Vector3(0f, -0.02f, -0.172f), new Vector3(0.07f, 0.12f, 0.01f), materials.Wheel);
        }

        void AddKeyBadge()
        {
            var r = MeshKit.Show("Key", transform, KeyMesh(), materials.Gold);
            keyBadge = r.transform;
            keyBadge.localPosition = new Vector3(0.3f, 0.54f, 0.3f);
        }

        // ---- Cached meshes -----------------------------------------------------------------------

        /// <summary>The unit parcel box for a color: beveled body, packing tape and the color's symbol.</summary>
        static Mesh BoxMesh(int color)
        {
            int i = Mathf.Clamp(color, 0, boxMeshes.Length - 1);
            if (boxMeshes[i] != null) return boxMeshes[i];
            var b = new MeshBuilder();
            MeshKit.AddBox(b, 0, Matrix4x4.identity, Bevel, new Vector2(MaterialBank.SkinBodyU, 0.5f));
            AddTape(b, new Vector2(MaterialBank.SkinTapeU, 0.5f));
            // A paper shipping label over the tape, with the color's symbol stamped on it in ink.
            var label = Matrix4x4.TRS(new Vector3(0f, 0.5f + TapeLift * 2f + LabelThickness * 0.5f, 0f),
                Quaternion.Euler(0f, LabelYaw[i % LabelYaw.Length], 0f), Vector3.one);
            MeshKit.AddBox(b, 0, label * Matrix4x4.Scale(new Vector3(LabelSize, LabelThickness, LabelSize)), 0f,
                new Vector2(MaterialBank.SkinLabelU, 0.5f));
            Symbols.Append(b, 0, i, label * Matrix4x4.Translate(new Vector3(0f, LabelThickness * 0.5f, 0f)) * Matrix4x4.Scale(Vector3.one * StampScale),
                new Vector2(MaterialBank.SkinSymbolU, 0.5f));
            boxMeshes[i] = b.ToMesh($"Parcel Box {i}");
            return boxMeshes[i];
        }

        static Mesh PlainMesh()
        {
            if (plainMesh != null) return plainMesh;
            var b = new MeshBuilder();
            MeshKit.AddBox(b, 0, Matrix4x4.identity, Bevel, new Vector2(MaterialBank.SkinBodyU, 0.5f));
            plainMesh = b.ToMesh("Parcel Box Plain");
            return plainMesh;
        }

        /// <summary>A strip of tape over the lid, the two lid chamfers and down the front and back faces.</summary>
        static void AddTape(MeshBuilder b, Vector2 uv)
        {
            const float h = 0.5f;
            const float i = h - Bevel;
            const float w = TapeHalfWidth;
            const float e = TapeLift;
            var diag = new Vector3(0f, 1f, 1f).normalized;
            var diagDown = new Vector3(0f, -1f, 1f).normalized;
            for (int s = -1; s <= 1; s += 2)
            {
                // Front (s = -1) and back (s = +1) faces.
                var n = new Vector3(0f, 0f, s);
                b.Polygon(0, n, uv,
                    new Vector3(-w, -i, s * (h + e)), new Vector3(w, -i, s * (h + e)),
                    new Vector3(w, i, s * (h + e)), new Vector3(-w, i, s * (h + e)));
                // Lid chamfer, offset along its normal.
                var nTop = new Vector3(0f, diag.y, s * diag.z);
                var off = nTop * e;
                b.Polygon(0, nTop, uv,
                    new Vector3(-w, h, s * i) + off, new Vector3(w, h, s * i) + off,
                    new Vector3(w, i, s * h) + off, new Vector3(-w, i, s * h) + off);
                // Bottom chamfer.
                var nBottom = new Vector3(0f, diagDown.y, s * diagDown.z);
                off = nBottom * e;
                b.Polygon(0, nBottom, uv,
                    new Vector3(-w, -h, s * i) + off, new Vector3(w, -h, s * i) + off,
                    new Vector3(w, -i, s * h) + off, new Vector3(-w, -i, s * h) + off);
            }
            b.Polygon(0, Vector3.up, uv,
                new Vector3(-w, h + e, -i), new Vector3(w, h + e, -i),
                new Vector3(w, h + e, i), new Vector3(-w, h + e, i));
        }

        static Mesh MysteryMesh()
        {
            if (mysteryMesh != null) return mysteryMesh;
            var b = new MeshBuilder();
            void Bar(Vector3 p, Vector3 s) => MeshKit.AddBox(b, 0, Matrix4x4.TRS(p, Quaternion.identity, s), 0f, Vector2.zero);
            Bar(new Vector3(0f, 0f, 0.22f), new Vector3(0.36f, 0.04f, 0.1f));
            Bar(new Vector3(0.15f, 0f, 0.12f), new Vector3(0.1f, 0.04f, 0.26f));
            Bar(new Vector3(0.05f, 0f, 0.02f), new Vector3(0.24f, 0.04f, 0.1f));
            Bar(new Vector3(-0.02f, 0f, -0.08f), new Vector3(0.1f, 0.04f, 0.14f));
            Bar(new Vector3(-0.02f, 0f, -0.26f), new Vector3(0.1f, 0.04f, 0.1f));
            mysteryMesh = b.ToMesh("Mystery Mark");
            return mysteryMesh;
        }

        static Mesh ShackleMesh()
        {
            if (shackleMesh != null) return shackleMesh;
            var b = new MeshBuilder();
            void Bar(Vector3 p, Vector3 s) => MeshKit.AddBox(b, 0, Matrix4x4.TRS(p, Quaternion.identity, s), 0f, Vector2.zero);
            Bar(new Vector3(-0.15f, 0.24f, 0f), new Vector3(0.07f, 0.24f, 0.07f));
            Bar(new Vector3(0.15f, 0.24f, 0f), new Vector3(0.07f, 0.24f, 0.07f));
            Bar(new Vector3(0f, 0.36f, 0f), new Vector3(0.37f, 0.07f, 0.07f));
            shackleMesh = b.ToMesh("Padlock Shackle");
            return shackleMesh;
        }

        static Mesh KeyMesh()
        {
            if (keyMesh != null) return keyMesh;
            var b = new MeshBuilder();
            MeshKit.AddBox(b, 0, Matrix4x4.Scale(new Vector3(0.2f, 0.06f, 0.2f)), 0f, Vector2.zero);
            MeshKit.AddBox(b, 0, MeshKit.TRS(new Vector3(-0.16f, 0f, -0.16f), 45f, new Vector3(0.24f, 0.06f, 0.07f)), 0f, Vector2.zero);
            keyMesh = b.ToMesh("Key Badge");
            return keyMesh;
        }
    }
}
