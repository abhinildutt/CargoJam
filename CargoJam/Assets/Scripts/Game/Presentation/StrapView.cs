using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The woven band and buckle over a strapped pair's lids, one mesh, parented to the member in the
    /// later row so it rides along with it. It draws with the pair's parcel skin (webbing and steel
    /// columns), so it adds one draw call that batches with the parcels.
    /// </summary>
    /// <remarks>
    /// The band spans both lids at the same height, so the controller should lift both members
    /// together (free when the pair is free). The views animate themselves, so <see cref="Snap"/>,
    /// <see cref="Release"/> and <see cref="Restore"/> need no tweener.
    /// </remarks>
    public sealed class StrapView : MonoBehaviour
    {
        /// <summary>The band runs this far to the side of the lids' centers, clear of the label's symbol (world units).</summary>
        const float SideOffset = 0.31f;
        const float BandWidth = 0.11f;
        const float BandThickness = 0.022f;
        /// <summary>Short ribs along the band's top, beveled so the light picks out a weave.</summary>
        const float RibLength = 0.085f;
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
            var r = MeshKit.Show("Band", go.transform, BandMesh(dx, dy), materials.ParcelSkin(owner.Parcel.Color, true));
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
        /// The band in the owner's unit-box space: across both lids to the side of center, down both
        /// outer faces, ribbed for a woven look, with a steel buckle over the gap between the boxes.
        /// Laid out in world units around the owner's center, then scaled into the unit box.
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
            var webbing = new Vector2(MaterialBank.SkinWebbingU, 0.5f);
            var steel = new Vector2(MaterialBank.SkinSteelU, 0.5f);
            float lid = size.y * 0.5f + 0.006f;
            float reach = Layout.CellSize * 0.5f + size.z * 0.5f + 0.012f;
            float bottom = -size.y * 0.5f + 0.06f;
            var b = new MeshBuilder();
            int ribs = Mathf.CeilToInt(reach * 2f / RibLength);
            float rib = reach * 2f / ribs;
            for (int i = 0; i < ribs; i++)
            {
                float z = -reach + (i + 0.5f) * rib;
                float lift = (i & 1) == 0 ? 0f : 0.003f;
                Box(b, frame, new Vector3(0f, lid + BandThickness * 0.5f + lift, z),
                    new Vector3(BandWidth, BandThickness, rib), 0.25f, webbing);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                float height = lid + BandThickness - bottom;
                Box(b, frame, new Vector3(0f, bottom + height * 0.5f, s * (reach + BandThickness * 0.5f)),
                    new Vector3(BandWidth, height, BandThickness), 0f, webbing);
            }

            // The buckle: a steel frame with a center bar, sitting on the band over the gap.
            float top = lid + BandThickness + 0.012f;
            const float w = 0.17f, l = 0.15f, bar = 0.03f;
            Box(b, frame, new Vector3(-w * 0.5f + bar * 0.5f, top, 0f), new Vector3(bar, 0.024f, l), 0.2f, steel);
            Box(b, frame, new Vector3(w * 0.5f - bar * 0.5f, top, 0f), new Vector3(bar, 0.024f, l), 0.2f, steel);
            Box(b, frame, new Vector3(0f, top, -l * 0.5f + bar * 0.5f), new Vector3(w, 0.024f, bar), 0.2f, steel);
            Box(b, frame, new Vector3(0f, top, l * 0.5f - bar * 0.5f), new Vector3(w, 0.024f, bar), 0.2f, steel);
            Box(b, frame, new Vector3(0f, top, 0f), new Vector3(w, 0.02f, bar * 0.8f), 0.2f, steel);

            meshes[key] = b.ToMesh($"Strap Band {dx},{dy}");
            return meshes[key];
        }

        static void Box(MeshBuilder b, Matrix4x4 frame, Vector3 position, Vector3 scale, float bevel, Vector2 uv) =>
            MeshKit.AddBox(b, 0, frame * Matrix4x4.TRS(position, Quaternion.identity, scale), bevel, uv);
    }
}
