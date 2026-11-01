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

        /// <summary>The most ice a crate can carry.</summary>
        public const int MaxIce = 3;
        /// <summary>
        /// The ice shell, in world units on the parcel: how far the collar stands proud of the box and
        /// how far it laps over the lid's edge (at three layers it reaches the label, never the symbol),
        /// each plus a share per layer, and per layer how much taller the collar grows and how much
        /// further down the sides the frost reaches.
        /// </summary>
        const float IceProud = 0.035f;
        const float IceProudPerLayer = 0.015f;
        const float IceCollarLap = 0.045f;
        const float IceCollarLapPerLayer = 0.042f;
        const float IceFrameBase = 0.04f;
        const float IceFramePerLayer = 0.04f;
        const float IceSkirtBase = 0.07f;
        const float IceSkirtPerLayer = 0.075f;
        /// <summary>How long a crack punches the collar, and how long the last layer takes to shatter away.</summary>
        const float CrackPunch = 0.2f;
        const float ShatterTime = 0.25f;

        /// <summary>Express tags at or below this count print in red ink and pulse.</summary>
        public const int ExpressUrgent = 2;
        /// <summary>
        /// The express label, in unit-box space on the lid: its size (a little bigger than a shipping
        /// label), the amber rim around it, the count's digit height (over 40% of the lid) and depth, and the
        /// paper chip in the top left corner that carries the color's symbol.
        /// </summary>
        const float ExpressLabel = 0.7f;
        const float ExpressRim = 0.04f;
        const float ExpressDigit = 0.44f;
        const float ExpressDigitZ = -0.075f;
        /// <summary>Two-digit counts are a little smaller and lower, so they fit beside the chip.</summary>
        const float ExpressDigitPair = 0.36f;
        const float ExpressDigitPairZ = -0.1f;
        const float ChipSize = 0.18f;
        static readonly Vector2 ChipAt = new Vector2(-0.245f, 0.245f);
        const float ChipStamp = 0.2f;
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
            // The parcel skin's ice and frost columns: batches with the parcels, and casts no shadow.
            var r = MeshKit.Show("Ice", transform, IceMesh(frostShown), materials.ParcelSkin(Parcel.Color, true), castShadows: false);
            iceCollar = r.transform;
            iceFilter = r.GetComponent<MeshFilter>();
            // A key on a frozen crate sits on top of the ice rather than inside it.
            if (keyBadge != null) keyBadge.localPosition += Vector3.up * (IceFrameHeight(MaxIce) / Layout.ParcelSize.y);
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
            AppendCarton(b);
            // A paper shipping label over the tape, with the color's symbol stamped on it in ink.
            var label = LabelFrame(i);
            MeshKit.AddBox(b, 0, label * Matrix4x4.Scale(new Vector3(LabelSize, LabelThickness, LabelSize)), 0f,
                new Vector2(MaterialBank.SkinLabelU, 0.5f));
            Symbols.Append(b, 0, i, label * Matrix4x4.Translate(new Vector3(0f, LabelThickness * 0.5f, 0f)) * Matrix4x4.Scale(Vector3.one * StampScale),
                new Vector2(MaterialBank.SkinSymbolU, 0.5f));
        }

        /// <summary>The beveled box and its packing tape.</summary>
        static void AppendCarton(MeshBuilder b)
        {
            MeshKit.AddBox(b, 0, Matrix4x4.identity, Bevel, new Vector2(MaterialBank.SkinBodyU, 0.5f));
            AddTape(b, new Vector2(MaterialBank.SkinTapeU, 0.5f));
        }

        /// <summary>The label's frame: centered on the lid, its thickness centered on y, with the color's hand-applied tilt.</summary>
        static Matrix4x4 LabelFrame(int i) =>
            Matrix4x4.TRS(new Vector3(0f, 0.5f + TapeLift * 2f + LabelThickness * 0.5f, 0f),
                Quaternion.Euler(0f, LabelYaw[i % LabelYaw.Length], 0f), Vector3.one);

        /// <summary>
        /// The parcel box wearing the express label instead of its shipping label, one cached mesh per
        /// color, count and ink. The label uses the skin's express, ink, paper and alert columns, so it
        /// shares the parcel's draw call.
        /// </summary>
        static Mesh ExpressMesh(int color, int left, bool urgent)
        {
            int i = Mathf.Clamp(color, 0, boxMeshes.Length - 1);
            int v = Mathf.Clamp(left, 0, Digits.Max);
            int key = (i * (Digits.Max + 1) + v) * 2 + (urgent ? 1 : 0);
            if (expressMeshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            var b = new MeshBuilder();
            AppendCarton(b);
            AppendExpressLabel(b, i, v, urgent);
            mesh = b.ToMesh($"Parcel Express {i} {v}{(urgent ? " urgent" : "")}");
            expressMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// The express label in place of the shipping label: a dark countdown display with an amber rim
        /// and the count in big amber figures (a red display with white figures when urgent), and the
        /// color's symbol stamped on a paper chip in its top left corner, so it still reads without
        /// color. Dark and amber stand out on every parcel color and in grayscale, so an express parcel
        /// reads at a glance whatever its count.
        /// </summary>
        static void AppendExpressLabel(MeshBuilder b, int color, int value, bool urgent)
        {
            var label = LabelFrame(color);
            var amber = new Vector2(MaterialBank.SkinExpressU, 0.5f);
            var display = new Vector2(urgent ? MaterialBank.SkinAlertU : MaterialBank.SkinExpressInkU, 0.5f);
            var figures = urgent ? new Vector2(MaterialBank.SkinTagU, 0.5f) : amber;
            float rimmed = ExpressLabel + ExpressRim * 2f;
            MeshKit.AddBox(b, 0, label * Matrix4x4.Scale(new Vector3(rimmed, LabelThickness, rimmed)), 0f, amber);
            MeshKit.AddBox(b, 0, label * Matrix4x4.TRS(new Vector3(0f, 0.003f, 0f), Quaternion.identity,
                new Vector3(ExpressLabel, LabelThickness, ExpressLabel)), 0f, display);
            float face = LabelThickness * 0.5f + 0.004f;
            var chip = label * Matrix4x4.Translate(new Vector3(ChipAt.x, face, ChipAt.y));
            MeshKit.AddBox(b, 0, chip * Matrix4x4.Scale(new Vector3(ChipSize, 0.01f, ChipSize)), 0f, new Vector2(MaterialBank.SkinLabelU, 0.5f));
            Symbols.Append(b, 0, color, chip * Matrix4x4.Translate(new Vector3(0f, 0.005f, 0f)) * Matrix4x4.Scale(Vector3.one * ChipStamp),
                new Vector2(MaterialBank.SkinSymbolU, 0.5f));
            bool pair = value >= 10;
            Digits.Append(b, 0, value, label * Matrix4x4.TRS(new Vector3(pair ? 0.03f : 0.045f, face, pair ? ExpressDigitPairZ : ExpressDigitZ),
                Quaternion.identity, Vector3.one * (pair ? ExpressDigitPair : ExpressDigit)), figures);
        }

        /// <summary>How high the ice frame on the lid stands at a number of layers (world units).</summary>
        static float IceFrameHeight(int layers) => IceFrameBase + IceFramePerLayer * Mathf.Clamp(layers, 1, MaxIce);

        /// <summary>
        /// The ice shell, readable from the game camera: a thick collar of clear ice hugging the top of
        /// the box and rising above the lid, so from above it rings the lid in pale cyan while the lid
        /// keeps its color and label; frost down the upper sides; and one ice crystal per layer on the
        /// collar's corners. Each layer makes the collar markedly wider (thin rim, band, thick slab) and
        /// taller, the frost reach further down, and adds a crystal, so 1, 2 and 3 read apart even in
        /// grayscale. Laid out in world units, then mapped
        /// into the unit box.
        /// </summary>
        static Mesh IceMesh(int layers)
        {
            int n = Mathf.Clamp(layers, 1, MaxIce);
            if (iceMeshes[n] != null) return iceMeshes[n];
            var size = Layout.ParcelSize;
            var toUnit = Matrix4x4.Scale(new Vector3(1f / size.x, 1f / size.y, 1f / size.z));
            var frost = new Vector2(MaterialBank.SkinFrostU, 0.5f);
            var clear = new Vector2(MaterialBank.SkinIceU, 0.5f);
            var snow = new Vector2(MaterialBank.SkinSnowU, 0.5f);
            var b = new MeshBuilder();
            void Block(Vector3 min, Vector3 max, float bevel, Vector2 uv) =>
                MeshKit.AddBox(b, 0, toUnit * Matrix4x4.TRS((min + max) * 0.5f, Quaternion.identity, max - min), bevel, uv);
            void Ring(float outer, float inner, float bottom, float top, float bevel, Vector2 uv)
            {
                Block(new Vector3(-outer, bottom, -outer), new Vector3(outer, top, -inner), bevel, uv);
                Block(new Vector3(-outer, bottom, inner), new Vector3(outer, top, outer), bevel, uv);
                Block(new Vector3(-outer, bottom, -inner), new Vector3(-inner, top, inner), bevel, uv);
                Block(new Vector3(inner, bottom, -inner), new Vector3(outer, top, inner), bevel, uv);
            }

            float box = size.x * 0.5f;
            float lid = size.y * 0.5f;
            float outer = box + IceProud + IceProudPerLayer * n;
            float inner = box - IceCollarLap - IceCollarLapPerLayer * n;
            float top = lid + IceFrameHeight(n);
            float skirt = lid - (IceSkirtBase + IceSkirtPerLayer * n);

            // Frost down the sides: the box seen through a skin of ice.
            Ring(box + 0.02f, box - 0.01f, skirt, lid - 0.02f, 0.2f, frost);
            // The collar of clear ice around the top, with a cap of snow along its crown.
            Ring(outer, inner, lid - 0.08f, top, 0.12f, clear);
            Ring(outer - 0.025f, inner + 0.025f, top - 0.01f, top + 0.016f, 0.2f, snow);

            // One crystal per layer, on the collar's corners: back left, back right, then front left.
            float corner = (outer + inner) * 0.5f;
            for (int k = 0; k < n; k++)
            {
                float x = (k == 1 ? 1f : -1f) * corner;
                float z = (k == 2 ? -1f : 1f) * corner;
                AddCrystal(b, toUnit, new Vector3(x, top, z), Mathf.Sign(x), clear, snow);
            }

            // Icicles hang from the frost's front edge, one more per layer.
            for (int k = 0; k <= n; k++)
            {
                float x = -box + (k + 0.5f) * (2f * box / (n + 1));
                float length = 0.11f + 0.04f * ((k + n) % 2);
                var tip = toUnit * Matrix4x4.TRS(new Vector3(x, skirt - length * 0.3f, -box - 0.01f),
                    Quaternion.Euler(0f, 0f, 45f), new Vector3(0.075f, 0.075f, 0.045f) * (length / 0.11f));
                MeshKit.AddBox(b, 0, tip, 0f, clear);
            }

            iceMeshes[n] = b.ToMesh($"Ice Shell {n}");
            return iceMeshes[n];
        }

        /// <summary>A pointed ice crystal (a tall shard and a short one) standing on <paramref name="foot"/>.</summary>
        static void AddCrystal(MeshBuilder b, Matrix4x4 toUnit, Vector3 foot, float outward, Vector2 clear, Vector2 snow)
        {
            MeshKit.AddBox(b, 0, toUnit * Matrix4x4.TRS(foot + new Vector3(0f, 0.14f, 0f),
                Quaternion.Euler(0f, 45f, -10f * outward), new Vector3(0.16f, 0.34f, 0.16f)), 0.3f, snow);
            MeshKit.AddBox(b, 0, toUnit * Matrix4x4.TRS(foot + new Vector3(-0.09f * outward, 0.06f, -0.06f),
                Quaternion.Euler(0f, 45f, 28f * outward), new Vector3(0.1f, 0.18f, 0.1f)), 0.3f, clear);
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
