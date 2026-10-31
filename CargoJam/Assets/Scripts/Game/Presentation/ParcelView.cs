using System.Collections.Generic;
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

        /// <summary>The most ice a crate can carry (one collar band per layer).</summary>
        public const int MaxIce = 3;
        /// <summary>Ice collar bands, in unit-box space: the lowest band's center, the step up, each band's size.</summary>
        const float IceBottom = -0.32f;
        const float IceStep = 0.27f;
        const float IceBandHeight = 0.22f;
        const float IceOuter = 0.56f;
        const float IceWall = 0.07f;
        /// <summary>How long a crack punches the collar, and how long the last layer takes to shatter away.</summary>
        const float CrackPunch = 0.2f;
        const float ShatterTime = 0.25f;

        /// <summary>Express tags at or below this count print in red ink and pulse.</summary>
        public const int ExpressUrgent = 2;
        /// <summary>
        /// The express tag, in world units on the parcel: card height, minimum width, digit height,
        /// padding around the digits, the dark border, and how far it tilts up off the lid toward the
        /// camera (90° minus the camera pitch, so it faces the camera square on).
        /// </summary>
        const float TagHeight = 0.24f;
        const float TagMinWidth = 0.24f;
        const float TagDigit = 0.15f;
        const float TagPad = 0.05f;
        const float TagBorder = 0.018f;
        const float TagTilt = 32f;
        /// <summary>The bump each tick gives the parcel, and the urgent pulse's size and rate (radians per second).</summary>
        const float ExpressKick = 0.22f;
        const float PulseAmount = 0.045f;
        const float PulseRate = 9f;

        static readonly Mesh[] boxMeshes = new Mesh[6];
        static readonly Mesh[] iceMeshes = new Mesh[MaxIce + 1];
        static readonly Dictionary<int, Mesh> expressMeshes = new Dictionary<int, Mesh>();
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
        Transform iceCollar;
        MeshFilter iceFilter;
        bool? free;
        bool hidden;
        /// <summary>Ice layers by the rules (set when <see cref="Crack"/> is called) and as drawn (set at its time).</summary>
        int frost;
        int frostShown;
        bool express;
        int expressLeft;
        int expressShown;
        float kickAt = float.NegativeInfinity;

        public Parcel Parcel { get; private set; }

        /// <summary>When the parcel finishes its latest scheduled move.</summary>
        public float SettledAt { get; set; }

        public bool InYard { get; private set; } = true;
        public bool IsFree => free == true;
        public bool IsHidden => hidden;
        public bool IsLocked => padlock != null;
        public bool HasKeyBadge => keyBadge != null;

        /// <summary>Ice layers left (0 when not frozen), as of the latest <see cref="Crack"/>.</summary>
        public int FrostLeft => frost;
        public bool IsFrozen => frost > 0;

        /// <summary>Whether this parcel wears an express tag, and its moves left as of the latest <see cref="SetExpress"/>.</summary>
        public bool IsExpress => express;
        public int ExpressLeft => expressLeft;

        /// <summary>The express tag reads red and pulses: few moves left and still in the yard.</summary>
        bool Urgent => express && InYard && expressShown <= ExpressUrgent;

        /// <summary>The lock this parcel opened is gone (unlocked, or lifted by the Crane).</summary>
        public void RemoveKeyBadge()
        {
            if (keyBadge != null) Destroy(keyBadge.gameObject);
            keyBadge = null;
        }

        /// <summary>A yard parcel at its cell, dressed for its starting state (<paramref name="look"/>).</summary>
        public static ParcelView Create(Parcel parcel, Transform parent, Layout layout, MaterialBank materials,
            in ParcelLook look)
        {
            var go = new GameObject($"Parcel {parcel.Id} {ParcelColors.ToCode(parcel.Color)}");
            go.transform.SetParent(parent, false);
            go.transform.position = layout.ParcelRest(parcel.Cell, false);
            go.transform.localScale = Layout.ParcelSize;

            var view = go.AddComponent<ParcelView>();
            // Update only runs while an express tag is animating.
            view.enabled = false;
            view.Parcel = parcel;
            view.materials = materials;
            view.body = MeshKit.Show("Body", go.transform, BoxMesh(parcel.Color), materials.ParcelSkin(parcel.Color, false));
            view.bodyFilter = view.body.GetComponent<MeshFilter>();
            if (look.Hidden) view.Hide();
            if (look.Locked) view.AddPadlock();
            if (look.IsKey) view.AddKeyBadge();
            if (look.Frost > 0) view.AddIce(look.Frost);
            if (look.Express > 0) view.AddExpress(look.Express);
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

        /// <summary>It left the yard: full color, and an express tag stops warning (normal ink, no pulse).</summary>
        public void MarkLeftYard()
        {
            InYard = false;
            free = null;
            ApplyMaterial(forceBright: true);
            RefreshExpress();
        }

        /// <summary>Undo put it back on its yard cell (a low express tag resumes its warning).</summary>
        public void MarkBackInYard()
        {
            InYard = true;
            free = null;
            RefreshExpress();
        }

        /// <summary>
        /// A layer of ice cracked: from <paramref name="at"/> the collar shows <paramref name="left"/>
        /// bands, with a punch. At 0 it shatters away and the body gets its full color back (if free).
        /// </summary>
        public void Crack(Tweener tweener, float at, int left)
        {
            if (iceCollar == null) return;
            left = Mathf.Clamp(left, 0, MaxIce);
            frost = left;
            var collar = iceCollar;
            if (left > 0)
            {
                var filter = iceFilter;
                var mesh = IceMesh(left);
                tweener.At(at, () =>
                {
                    if (!this || !filter) return;
                    frostShown = left;
                    filter.sharedMesh = mesh;
                });
                tweener.Punch(collar, Vector3.one, new Vector3(0.1f, 0.35f, 0.1f), at, CrackPunch);
                return;
            }
            iceCollar = null;
            iceFilter = null;
            tweener.Run(at, ShatterTime, t =>
            {
                if (!collar) return;
                float burst = 1f + 0.25f * Mathf.Sin(t * Mathf.PI * 0.5f);
                collar.localScale = Vector3.one * (burst * (1f - Ease.InCubic(t)));
            }, () =>
            {
                if (!this) return;
                frostShown = 0;
                ApplyMaterial(forceBright: !InYard);
            }, () => { if (collar) Destroy(collar.gameObject); });
        }

        /// <summary>
        /// An express tick: from <paramref name="at"/> the tag shows <paramref name="left"/> moves, with
        /// a bump. At <see cref="ExpressUrgent"/> or fewer it prints in red and the parcel pulses until
        /// it leaves the yard. 0 (missed) stays red.
        /// </summary>
        public void SetExpress(int left, Tweener tweener, float at)
        {
            if (!express) return;
            int value = Mathf.Clamp(left, 0, Digits.Max);
            expressLeft = value;
            // Build the mesh now (event time), so the swap itself never allocates.
            ExpressMesh(Parcel.Color, value, value <= ExpressUrgent);
            tweener.At(at, () =>
            {
                if (!this) return;
                expressShown = value;
                kickAt = Time.time;
                RefreshExpress();
            });
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
                    bodyFilter.sharedMesh = BodyMesh();
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
            // A frozen crate keeps the dim skin even if it has a path: it can't be tapped yet.
            bool bright = forceBright || (free == true && frostShown == 0);
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

        void AddIce(int layers)
        {
            frost = frostShown = Mathf.Clamp(layers, 1, MaxIce);
            var r = MeshKit.Show("Ice", transform, IceMesh(frostShown), materials.Ice);
            iceCollar = r.transform;
            iceFilter = r.GetComponent<MeshFilter>();
            ApplyMaterial();
        }

        void AddExpress(int left)
        {
            express = true;
            expressLeft = expressShown = Mathf.Clamp(left, 0, Digits.Max);
            RefreshExpress();
        }

        /// <summary>Shows the tag's current count and ink, and lets <see cref="Update"/> run its bump or pulse.</summary>
        void RefreshExpress()
        {
            if (!express) return;
            bodyFilter.sharedMesh = BodyMesh();
            enabled = true;
        }

        /// <summary>
        /// The express bump and urgent pulse. They scale the body only: the root belongs to the
        /// controller's tweens. Switches itself off once there is nothing to animate.
        /// </summary>
        void Update()
        {
            float now = Time.time;
            float k = (now - kickAt) / ExpressKick;
            bool kicking = k >= 0f && k < 1f;
            bool urgent = Urgent;
            var bodyTransform = body.transform;
            if (!kicking && !urgent)
            {
                bodyTransform.localScale = Vector3.one;
                enabled = false;
                return;
            }
            float s = kicking ? (urgent ? 0.14f : 0.08f) * Mathf.Sin(k * Mathf.PI) * (1f - k) : 0f;
            if (urgent) s += PulseAmount * (0.5f - 0.5f * Mathf.Cos(now * PulseRate));
            bodyTransform.localScale = new Vector3(1f + s, 1f + s * 0.6f, 1f + s);
        }

        Mesh BodyMesh() =>
            hidden ? PlainMesh() : express ? ExpressMesh(Parcel.Color, expressShown, Urgent) : BoxMesh(Parcel.Color);

        // ---- Cached meshes -----------------------------------------------------------------------

        /// <summary>The unit parcel box for a color: beveled body, packing tape and the color's symbol.</summary>
        static Mesh BoxMesh(int color)
        {
            int i = Mathf.Clamp(color, 0, boxMeshes.Length - 1);
            if (boxMeshes[i] != null) return boxMeshes[i];
            var b = new MeshBuilder();
            AppendBox(b, i);
            boxMeshes[i] = b.ToMesh($"Parcel Box {i}");
            return boxMeshes[i];
        }

        static void AppendBox(MeshBuilder b, int i)
        {
            MeshKit.AddBox(b, 0, Matrix4x4.identity, Bevel, new Vector2(MaterialBank.SkinBodyU, 0.5f));
            AddTape(b, new Vector2(MaterialBank.SkinTapeU, 0.5f));
            // A paper shipping label over the tape, with the color's symbol stamped on it in ink.
            var label = Matrix4x4.TRS(new Vector3(0f, 0.5f + TapeLift * 2f + LabelThickness * 0.5f, 0f),
                Quaternion.Euler(0f, LabelYaw[i % LabelYaw.Length], 0f), Vector3.one);
            MeshKit.AddBox(b, 0, label * Matrix4x4.Scale(new Vector3(LabelSize, LabelThickness, LabelSize)), 0f,
                new Vector2(MaterialBank.SkinLabelU, 0.5f));
            Symbols.Append(b, 0, i, label * Matrix4x4.Translate(new Vector3(0f, LabelThickness * 0.5f, 0f)) * Matrix4x4.Scale(Vector3.one * StampScale),
                new Vector2(MaterialBank.SkinSymbolU, 0.5f));
        }

        /// <summary>
        /// The parcel box with an express tag on the lid's front right corner, one cached mesh per
        /// color, count and ink. The tag uses the skin's tag, ink, webbing and alert columns, so it
        /// shares the parcel's draw call.
        /// </summary>
        static Mesh ExpressMesh(int color, int left, bool urgent)
        {
            int i = Mathf.Clamp(color, 0, boxMeshes.Length - 1);
            int v = Mathf.Clamp(left, 0, Digits.Max);
            int key = (i * (Digits.Max + 1) + v) * 2 + (urgent ? 1 : 0);
            if (expressMeshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            var b = new MeshBuilder();
            AppendBox(b, i);
            AppendTag(b, v, urgent);
            mesh = b.ToMesh($"Parcel Express {i} {v}{(urgent ? " urgent" : "")}");
            expressMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// A card hinged on the lid's front edge and tilted up to face the camera: a dark (red when
        /// urgent) border, the white card and the count. Laid out in world units, then mapped into the
        /// unit box so the parcel's squat scale doesn't squash it.
        /// </summary>
        static void AppendTag(MeshBuilder b, int value, bool urgent)
        {
            var size = Layout.ParcelSize;
            var toUnit = Matrix4x4.Scale(new Vector3(1f / size.x, 1f / size.y, 1f / size.z));
            float flat = (0.5f - Bevel) * size.z;
            float w = Mathf.Max(TagMinWidth, Digits.Width(value) * TagDigit + TagPad * 2f);
            var hinge = new Vector3(size.x * 0.5f - 0.03f - w * 0.5f, size.y * 0.5f + 0.003f, -flat);
            var frame = toUnit * Matrix4x4.TRS(hinge, Quaternion.Euler(-TagTilt, 0f, 0f), Vector3.one)
                * Matrix4x4.Translate(new Vector3(0f, 0f, TagHeight * 0.5f + TagBorder));
            var ink = new Vector2(urgent ? MaterialBank.SkinAlertU : MaterialBank.SkinSymbolU, 0.5f);
            var border = new Vector2(urgent ? MaterialBank.SkinAlertU : MaterialBank.SkinWebbingU, 0.5f);
            MeshKit.AddBox(b, 0, frame * Matrix4x4.TRS(new Vector3(0f, 0.006f, 0f), Quaternion.identity,
                new Vector3(w + TagBorder * 2f, 0.012f, TagHeight + TagBorder * 2f)), 0f, border);
            MeshKit.AddBox(b, 0, frame * Matrix4x4.TRS(new Vector3(0f, 0.019f, 0f), Quaternion.identity,
                new Vector3(w, 0.014f, TagHeight)), 0f, new Vector2(MaterialBank.SkinTagU, 0.5f));
            Digits.Append(b, 0, value, frame * Matrix4x4.TRS(new Vector3(0f, 0.029f, 0f), Quaternion.identity,
                Vector3.one * TagDigit), ink);
        }

        /// <summary>
        /// The ice collar: one band of thick ice around the box's sides per layer (lowest first), each
        /// with a crystal chip on every face. The lid stays clear so the label reads.
        /// </summary>
        static Mesh IceMesh(int layers)
        {
            int n = Mathf.Clamp(layers, 1, MaxIce);
            if (iceMeshes[n] != null) return iceMeshes[n];
            var b = new MeshBuilder();
            for (int k = 0; k < n; k++) AddIceBand(b, IceBottom + k * IceStep, k);
            iceMeshes[n] = b.ToMesh($"Ice Collar {n}");
            return iceMeshes[n];
        }

        static void AddIceBand(MeshBuilder b, float y, int band)
        {
            const float mid = IceOuter - IceWall * 0.5f;
            for (int side = 0; side < 4; side++)
            {
                // Side 0 is the front (-z); the others turn around the box.
                var turn = Quaternion.Euler(0f, side * 90f, 0f);
                MeshKit.AddBox(b, 0, Matrix4x4.TRS(turn * new Vector3(0f, y, -mid), turn,
                    new Vector3(IceOuter * 2f, IceBandHeight, IceWall)), 0.12f, Vector2.zero);
                float along = (band + side) % 2 == 0 ? 0.24f : -0.24f;
                MeshKit.AddBox(b, 0, Matrix4x4.TRS(turn * new Vector3(along, y - IceBandHeight * 0.1f, -IceOuter - 0.005f),
                    turn * Quaternion.Euler(0f, 0f, 45f), new Vector3(0.13f, 0.13f, 0.06f)), 0f, Vector2.zero);
            }
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
