using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The flat woven band and buckle over a strapped pair's lids, one mesh, parented to the member in
    /// the later row so it rides along with it. It draws with the pair's parcel skin (strap, stitching
    /// and steel columns) and casts no shadow, so it adds one draw call that batches with the parcels.
    /// </summary>
    /// <remarks>
    /// The band spans both lids at the same height, so the controller should lift both members
    /// together (free when the pair is free). The views animate themselves, so <see cref="Snap"/>,
    /// <see cref="Release"/> and <see cref="Restore"/> need no tweener.
    /// </remarks>
    public sealed class StrapView : MonoBehaviour
    {
        /// <summary>The band runs this far to the side of the lids' centers, clear of the label's symbol (world units).</summary>
        const float SideOffset = 0.28f;
        const float BandWidth = 0.2f;
        const float BandThickness = 0.02f;
        /// <summary>The pale stitched edges that make the band read as flat webbing on any parcel color.</summary>
        const float StitchWidth = 0.024f;
        /// <summary>How much of each outer lid chamfer the band's folded end covers, so it never hangs past the parcels.</summary>
        const float FoldShare = 0.75f;
        const float SnapTime = 0.25f;
        const float ReleaseTime = 0.15f;
        const float RestoreTime = 0.25f;

        enum Anim { None, Snap, Release, Restore }

        /// <summary>Cached band meshes per partner direction (owner-relative dx, dy each in -1..1).</summary>
        static readonly Mesh[] meshes = new Mesh[9];

        Transform band;
        Anim anim;
        float animAt;

        /// <summary>Index into <see cref="LevelDefinition.Straps"/>.</summary>
        public int Index { get; private set; }

        public ParcelView A { get; private set; }
        public ParcelView B { get; private set; }

        /// <summary>False once the Crane cut it (until Undo restores it).</summary>
        public bool Intact { get; private set; } = true;

        /// <summary>The band is on the pair (not cut, not slipped off as the pair left the yard).</summary>
        public bool Shown { get; private set; } = true;

        /// <param name="index">The strap's index in <see cref="LevelDefinition.Straps"/>.</param>
        /// <param name="a">The parcel at <see cref="ParcelStrap.A"/>.</param>
        /// <param name="b">The parcel at <see cref="ParcelStrap.B"/>.</param>
        public static StrapView Create(int index, ParcelView a, ParcelView b, MaterialBank materials)
        {
            var owner = b.Parcel.Cell.Y >= a.Parcel.Cell.Y ? b : a;
            var partner = owner == b ? a : b;
            var go = new GameObject($"Strap {index}");
            go.transform.SetParent(owner.transform, false);

            var view = go.AddComponent<StrapView>();
            view.enabled = false;
            view.Index = index;
            view.A = a;
            view.B = b;
            int dx = Mathf.Clamp(partner.Parcel.Cell.X - owner.Parcel.Cell.X, -1, 1);
            int dy = Mathf.Clamp(partner.Parcel.Cell.Y - owner.Parcel.Cell.Y, -1, 1);
            var r = MeshKit.Show("Band", go.transform, BandMesh(dx, dy), materials.ParcelSkin(owner.Parcel.Color, true), castShadows: false);
            view.band = r.transform;
            return view;
        }

        /// <summary>The Crane cut it: the band springs up and away from <paramref name="at"/>.</summary>
        public void Snap(float at)
        {
            Intact = false;
            Hide(Anim.Snap, at);
        }

        /// <summary>
        /// The pair left the yard: the band slips off from <paramref name="at"/> (the members may end up
        /// in different places). <see cref="Intact"/> is unchanged; <see cref="Restore"/> brings it back.
        /// </summary>
        public void Release(float at) => Hide(Anim.Release, at);

        /// <summary>Undo put the pair back: the band reappears from <paramref name="at"/>.</summary>
        public void Restore(float at)
        {
            Intact = true;
            if (Shown) return;
            Shown = true;
            gameObject.SetActive(true);
            // Hidden until its time comes, then it grows back on.
            band.localPosition = Vector3.zero;
            band.localScale = Vector3.zero;
            anim = Anim.Restore;
            animAt = at;
            enabled = true;
        }

        void Hide(Anim kind, float at)
        {
            if (!Shown) return;
            Shown = false;
            anim = kind;
            animAt = at;
            enabled = true;
        }

        void Update()
        {
            float now = Time.time;
            if (now < animAt)
            {
                if (anim == Anim.Restore) band.localScale = Vector3.zero;
                return;
            }
            float duration = anim == Anim.Snap ? SnapTime : anim == Anim.Release ? ReleaseTime : RestoreTime;
            float t = Mathf.Clamp01((now - animAt) / duration);
            switch (anim)
            {
                case Anim.Snap:
                    band.localPosition = new Vector3(0f, 0.35f * Ease.OutCubic(t), 0f);
                    band.localScale = new Vector3(1f + 0.2f * t, 1f, 1f + 0.2f * t) * (1f - Ease.InCubic(t));
                    break;
                case Anim.Release:
                    band.localPosition = Vector3.zero;
                    band.localScale = Vector3.one * (1f - t);
                    break;
                case Anim.Restore:
                    band.localPosition = Vector3.zero;
                    band.localScale = Vector3.one * Ease.OutBack(t);
                    break;
            }
            if (t < 1f) return;
            enabled = false;
            if (anim == Anim.Restore) return;
            band.localPosition = Vector3.zero;
            band.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// The band in the owner's unit-box space: one flat strip of webbing across both lids to the side
        /// of center, with pale stitched edges, tucked down over the lid chamfers at its ends (inside the
        /// parcels' outline), and a steel buckle over the gap between the boxes. Laid out in world units
        /// around the owner's center, then scaled into the unit box.
        /// </summary>
        static Mesh BandMesh(int dx, int dy)
        {
            int key = (dx + 1) * 3 + (dy + 1);
            if (meshes[key] != null) return meshes[key];
            var size = Layout.ParcelSize;
            var toUnit = Matrix4x4.Scale(new Vector3(1f / size.x, 1f / size.y, 1f / size.z));
            // Cell +x is world +x; cell +y (later rows) is world -z.
            var axis = new Vector3(dx, 0f, -dy).normalized;
            if (axis == Vector3.zero) axis = Vector3.right;
            // Rows of a horizontal pair: band toward the camera (-z). Columns: band to the left (-x).
            var side = Mathf.Abs(axis.x) > 0.5f ? Vector3.back : Vector3.left;
            var mid = new Vector3(dx, 0f, -dy) * (Layout.CellSize * 0.5f) + side * SideOffset;
            var frame = toUnit * Matrix4x4.TRS(mid, Quaternion.LookRotation(axis, Vector3.up), Vector3.one);

            // In the frame: z runs along the pair, x across the band, y up from the parcels' centers.
            var webbing = new Vector2(MaterialBank.SkinStrapU, 0.5f);
            var stitch = new Vector2(MaterialBank.SkinTagU, 0.5f);
            var steel = new Vector2(MaterialBank.SkinSteelU, 0.5f);
            float lid = size.y * 0.5f + 0.004f;
            // The flat top of each lid ends where its chamfer starts (the box bevel, in world units).
            float chamferRun = 0.08f * size.z, chamferDrop = 0.08f * size.y;
            float flatEnd = Layout.CellSize * 0.5f + size.z * 0.5f - chamferRun;
            var b = new MeshBuilder();
            float y = lid + BandThickness * 0.5f;
            Box(b, frame, new Vector3(0f, y, 0f), new Vector3(BandWidth, BandThickness, flatEnd * 2f), 0.1f, webbing);
            for (int e = -1; e <= 1; e += 2)
            {
                float x = e * (BandWidth - StitchWidth) * 0.5f;
                Box(b, frame, new Vector3(x, y + BandThickness * 0.5f + 0.002f, 0f),
                    new Vector3(StitchWidth, 0.004f, flatEnd * 2f - 0.04f), 0f, stitch);
            }
            // The ends fold down over the outer lid chamfers, following their slope, and stop on them. The
            // far end of a pair in one column stays flat: a fold there would poke up past the far lid's
            // back edge as seen from the camera.
            float angle = Mathf.Atan2(chamferDrop, chamferRun) * Mathf.Rad2Deg;
            float fold = Mathf.Sqrt(chamferRun * chamferRun + chamferDrop * chamferDrop) * FoldShare;
            bool column = Mathf.Abs(axis.z) > 0.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                if (column && s * axis.z > 0f) continue;
                var start = new Vector3(0f, y, s * flatEnd);
                var dir = new Vector3(0f, -Mathf.Sin(angle * Mathf.Deg2Rad), s * Mathf.Cos(angle * Mathf.Deg2Rad));
                MeshKit.AddBox(b, 0, frame * Matrix4x4.TRS(start + dir * (fold * 0.5f), Quaternion.Euler(s * angle, 0f, 0f),
                    new Vector3(BandWidth, BandThickness, fold)), 0f, webbing);
            }

            // The buckle: a chunky steel frame with a center bar, sitting on the band over the gap.
            float top = lid + BandThickness + 0.014f;
            const float w = 0.27f, l = 0.22f, bar = 0.045f, h = 0.03f;
            Box(b, frame, new Vector3(-w * 0.5f + bar * 0.5f, top, 0f), new Vector3(bar, h, l), 0.25f, steel);
            Box(b, frame, new Vector3(w * 0.5f - bar * 0.5f, top, 0f), new Vector3(bar, h, l), 0.25f, steel);
            Box(b, frame, new Vector3(0f, top, -l * 0.5f + bar * 0.5f), new Vector3(w, h, bar), 0.25f, steel);
            Box(b, frame, new Vector3(0f, top, l * 0.5f - bar * 0.5f), new Vector3(w, h, bar), 0.25f, steel);
            Box(b, frame, new Vector3(0f, top + 0.004f, 0f), new Vector3(w, h, bar * 0.9f), 0.25f, steel);

            meshes[key] = b.ToMesh($"Strap Band {dx},{dy}");
            return meshes[key];
        }

        static void Box(MeshBuilder b, Matrix4x4 frame, Vector3 position, Vector3 scale, float bevel, Vector2 uv) =>
            MeshKit.AddBox(b, 0, frame * Matrix4x4.TRS(position, Quaternion.identity, scale), bevel, uv);
    }
}
