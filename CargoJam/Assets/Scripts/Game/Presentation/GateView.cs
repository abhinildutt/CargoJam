using System.Collections.Generic;
using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// A customs gate on its yard cell: a post, a boom arm and a big sign showing the loads it still
    /// needs. It opens (boom up, gate sinks) once the count reaches 0.
    /// </summary>
    /// <remarks>
    /// Two renderers (the post, and the boom with its sign), both with the gate color's parcel skin
    /// and no shadows, so they batch with the parcels. The sign is a plate of the gate's color with
    /// the count in big white figures (dark on yellow) on a white backing, like a road sign; the
    /// color's symbol sits on a label atop the post, away from the count so it never reads as part of
    /// it. The boom is striped in the gate's color. The view animates itself, so
    /// <see cref="SetCount"/> and <see cref="Open"/> need no tweener.
    /// </remarks>
    public sealed class GateView : MonoBehaviour
    {
        /// <summary>From <see cref="Open"/>'s time: the boom swings up, then the gate sinks and switches off.</summary>
        public const float BoomTime = 0.3f;
        public const float SinkTime = 0.35f;
        public const float OpenTime = BoomTime + SinkTime;
        const float BoomAngle = 80f;
        const float SinkDepth = 0.9f;
        const float PunchTime = 0.3f;
        /// <summary>How far the boom twitches up when the count drops (degrees).</summary>
        const float PunchAngle = 9f;

        /// <summary>The gate's parts in cell space (world units): the post's x and z, the boom's pivot height and length.</summary>
        const float PostX = -0.4f;
        const float GateZ = -0.12f;
        const float PivotY = 0.52f;
        const float BoomLength = 0.9f;
        const float BoomThickness = 0.09f;
        /// <summary>
        /// The sign on the boom: where its center sits along the boom, plate size, the white backing's
        /// margin, the count's height, and how far it tilts back so it faces the camera square on (the
        /// camera pitch).
        /// </summary>
        const float SignAlong = 0.48f;
        const float SignWidth = 0.8f;
        const float SignHeight = 0.5f;
        const float SignBacking = 0.035f;
        const float SignDigit = 0.34f;
        const float SignTilt = 58f;

        static readonly Mesh[] postMeshes = new Mesh[ParcelColors.Count];
        static readonly Dictionary<int, Mesh> boomMeshes = new Dictionary<int, Mesh>();

        readonly List<(int left, float at)> counts = new List<(int, float)>(4);
        Transform boom;
        MeshFilter boomFilter;
        Vector3 home;
        float punchAt = float.NegativeInfinity;
        float openAt = float.PositiveInfinity;

        /// <summary>Index into <see cref="LevelDefinition.Gates"/>.</summary>
        public int Index { get; private set; }

        public ParcelGate Gate { get; private set; }

        /// <summary>The count the sign shows (after any scheduled change).</summary>
        public int Left { get; private set; }

        public bool IsOpen { get; private set; }

        /// <summary>The boom with its sign, for effects that want to burst from it.</summary>
        public Transform Sign => boom;

        /// <param name="index">The gate's index in <see cref="LevelDefinition.Gates"/>.</param>
        /// <param name="left">Loads still needed (the session's <see cref="GameSession.GateLeft"/>).</param>
        public static GateView Create(int index, ParcelGate gate, int left, Transform parent, Layout layout, MaterialBank materials)
        {
            var go = new GameObject($"Gate {index} {ParcelColors.ToCode(gate.Color)}");
            go.transform.SetParent(parent, false);
            go.transform.position = layout.CellCenter(gate.Cell);

            var view = go.AddComponent<GateView>();
            view.enabled = false;
            view.Index = index;
            view.Gate = gate;
            view.Left = Mathf.Max(0, left);
            view.home = go.transform.localPosition;

            int color = Mathf.Clamp(gate.Color, 0, ParcelColors.Count - 1);
            var skin = materials.ParcelSkin(color, true);
            MeshKit.Show("Post", go.transform, PostMesh(color), skin, castShadows: false);
            var boomRenderer = MeshKit.Show("Boom", go.transform, BoomMesh(color, view.Left), skin, castShadows: false);
            view.boom = boomRenderer.transform;
            view.boom.localPosition = new Vector3(PostX, PivotY, GateZ);
            view.boomFilter = boomRenderer.GetComponent<MeshFilter>();
            if (left <= 0) view.OpenNow();
            return view;
        }

        /// <summary>The sign shows <paramref name="left"/> from Time.time <paramref name="at"/>, with a punch.</summary>
        public void SetCount(int left, float at)
        {
            if (IsOpen && left > 0) return;
            Left = Mathf.Max(0, left);
            // Build the mesh now (event time), so the swap itself never allocates.
            BoomMesh(Mathf.Clamp(Gate.Color, 0, ParcelColors.Count - 1), Left);
            // A full queue (never in play: one change per load) shows its oldest change early rather than growing.
            if (counts.Count == counts.Capacity)
            {
                ShowCount(counts[0].left);
                counts.RemoveAt(0);
            }
            counts.Add((Left, at));
            enabled = true;
        }

        /// <summary>Raises the boom and sinks the gate from <paramref name="at"/>, then deactivates it.</summary>
        public void Open(float at)
        {
            if (IsOpen) return;
            IsOpen = true;
            Left = 0;
            openAt = at;
            enabled = true;
        }

        void Update()
        {
            float now = Time.time;
            while (counts.Count > 0 && counts[0].at <= now)
            {
                ShowCount(counts[0].left);
                punchAt = counts[0].at;
                counts.RemoveAt(0);
            }

            float k = (now - punchAt) / PunchTime;
            bool punching = k >= 0f && k < 1f;

            if (now < openAt)
            {
                // The boom twitches up and settles as the count drops.
                float twitch = punching ? Mathf.Sin(k * Mathf.PI) * (1f - k) : 0f;
                boom.localRotation = Quaternion.Euler(0f, 0f, PunchAngle * twitch);
            }
            else
            {
                float u = now - openAt;
                float swing = Ease.OutBack(Mathf.Clamp01(u / BoomTime));
                boom.localRotation = Quaternion.Euler(0f, 0f, BoomAngle * swing);
                float sink = Ease.InCubic(Mathf.Clamp01((u - BoomTime) / SinkTime));
                transform.localPosition = home + Vector3.down * (SinkDepth * sink);
                if (u >= OpenTime)
                {
                    enabled = false;
                    gameObject.SetActive(false);
                }
                return;
            }
            if (!punching && counts.Count == 0) enabled = false;
        }

        void ShowCount(int left) => boomFilter.sharedMesh = BoomMesh(Mathf.Clamp(Gate.Color, 0, ParcelColors.Count - 1), left);

        /// <summary>A gate built with nothing left to count starts open (gates never close again).</summary>
        void OpenNow()
        {
            IsOpen = true;
            Left = 0;
            gameObject.SetActive(false);
        }

        // ---- Cached meshes -----------------------------------------------------------------------

        static readonly Vector2 Body = new Vector2(MaterialBank.SkinBodyU, 0.5f);
        static readonly Vector2 Paper = new Vector2(MaterialBank.SkinLabelU, 0.5f);
        static readonly Vector2 Ink = new Vector2(MaterialBank.SkinSymbolU, 0.5f);
        static readonly Vector2 White = new Vector2(MaterialBank.SkinTagU, 0.5f);
        static readonly Vector2 Steel = new Vector2(MaterialBank.SkinSteelU, 0.5f);
        static readonly Vector2 Dark = new Vector2(MaterialBank.SkinWebbingU, 0.5f);

        /// <summary>
        /// The footing, the post with its color housing at the pivot (the color's symbol on a label on
        /// top), and the rest on the far side. One mesh per color.
        /// </summary>
        static Mesh PostMesh(int color)
        {
            if (postMeshes[color] != null) return postMeshes[color];
            var b = new MeshBuilder();
            Box(b, new Vector3(PostX, 0.05f, GateZ), new Vector3(0.3f, 0.1f, 0.3f), 0.15f, Dark);
            Box(b, new Vector3(PostX, 0.3f, GateZ), new Vector3(0.12f, 0.46f, 0.12f), 0f, Steel);
            const float housing = 0.24f;
            Box(b, new Vector3(PostX, PivotY, GateZ), new Vector3(housing, housing * 0.8f, housing), 0.2f, Body);
            float cap = PivotY + housing * 0.4f;
            Box(b, new Vector3(PostX, cap + 0.006f, GateZ), new Vector3(housing - 0.05f, 0.012f, housing - 0.05f), 0f, Paper);
            Symbols.Append(b, 0, color, Matrix4x4.TRS(new Vector3(PostX, cap + 0.014f, GateZ), Quaternion.identity, Vector3.one * 0.2f), Ink);
            float restX = PostX + BoomLength - 0.04f;
            Box(b, new Vector3(restX, 0.03f, GateZ), new Vector3(0.16f, 0.06f, 0.16f), 0.15f, Dark);
            Box(b, new Vector3(restX, 0.24f, GateZ), new Vector3(0.06f, 0.42f, 0.06f), 0f, Steel);
            Box(b, new Vector3(restX, PivotY - BoomThickness * 0.5f - 0.02f, GateZ), new Vector3(0.13f, 0.04f, 0.11f), 0f, Dark);
            postMeshes[color] = b.ToMesh($"Gate Post {color}");
            return postMeshes[color];
        }

        /// <summary>
        /// The boom from its pivot along +x, striped in the gate's color and white, with a counterweight
        /// behind, and the sign standing on it: a white backing, a plate of the gate's color and the
        /// count in big figures, white (dark ink on yellow, where white would vanish). The sign faces
        /// the camera square on. One mesh per color and count.
        /// </summary>
        static Mesh BoomMesh(int color, int left)
        {
            int v = Mathf.Clamp(left, 0, Digits.Max);
            int key = color * (Digits.Max + 1) + v;
            if (boomMeshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            var b = new MeshBuilder();
            const int stripes = 6;
            float stripe = (BoomLength - 0.1f) / stripes;
            for (int i = 0; i < stripes; i++)
                Box(b, new Vector3(0.1f + (i + 0.5f) * stripe, 0f, 0f), new Vector3(stripe, BoomThickness, BoomThickness), 0f,
                    (i & 1) == 0 ? White : Body);
            Box(b, new Vector3(-0.12f, 0f, 0f), new Vector3(0.14f, 0.12f, 0.12f), 0.15f, Dark);

            // The sign, pivoting on its bottom edge on top of the boom; flat in its XZ with +z up the sign.
            var sign = Matrix4x4.TRS(new Vector3(SignAlong, BoomThickness * 0.5f, 0f), Quaternion.Euler(-(90f - SignTilt), 0f, 0f), Vector3.one);
            float c = SignHeight * 0.5f + SignBacking;
            void SignBox(Vector3 position, Vector3 scale, float bevel, Vector2 uv) =>
                MeshKit.AddBox(b, 0, sign * Matrix4x4.TRS(position, Quaternion.identity, scale), bevel, uv);
            SignBox(new Vector3(0f, 0.015f, c), new Vector3(SignWidth + SignBacking * 2f, 0.03f, SignHeight + SignBacking * 2f), 0.12f, White);
            SignBox(new Vector3(0f, 0.034f, c), new Vector3(SignWidth, 0.012f, SignHeight), 0.06f, Body);
            var figures = color == YellowIndex ? Ink : White;
            Digits.Append(b, 0, v, sign * Matrix4x4.TRS(new Vector3(0f, 0.044f, c), Quaternion.identity,
                new Vector3(SignDigit * 1.15f, SignDigit, SignDigit)), figures, Digits.BoldStroke);

            mesh = b.ToMesh($"Gate Boom {color} {v}");
            boomMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>ParcelColors' yellow, whose plate takes dark figures.</summary>
        const int YellowIndex = 3;

        static void Box(MeshBuilder b, Vector3 position, Vector3 scale, float bevel, Vector2 uv) =>
            MeshKit.AddBox(b, 0, Matrix4x4.TRS(position, Quaternion.identity, scale), bevel, uv);
    }
}
