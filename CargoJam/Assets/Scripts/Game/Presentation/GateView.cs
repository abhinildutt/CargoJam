using System.Collections.Generic;
using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// A customs gate on its yard cell: a post, a boom arm and a sign showing the gate's color and the
    /// loads it still needs. It opens (boom up, gate sinks) once the count reaches 0.
    /// </summary>
    /// <remarks>
    /// Three renderers (post, boom, sign), all with the gate color's parcel skin, so they batch with
    /// the parcels. The sign is a parcel label: the color's symbol stamped beside the count, on a
    /// plate of the gate's color, and the boom is striped in it. The view animates itself, so
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
        const float PunchTime = 0.25f;

        /// <summary>The gate's parts in cell space (world units): the post's x and z, the boom's pivot height and length.</summary>
        const float PostX = -0.38f;
        const float GateZ = -0.12f;
        const float PivotY = 0.5f;
        const float BoomLength = 0.86f;
        const float BoomThickness = 0.07f;
        /// <summary>The sign: plate size, and how far it tilts back so it faces the camera square on (the camera pitch).</summary>
        const float SignWidth = 0.62f;
        const float SignHeight = 0.36f;
        const float SignTilt = 58f;

        static Mesh postMesh;
        static Mesh boomMesh;
        static readonly Dictionary<int, Mesh> signMeshes = new Dictionary<int, Mesh>();

        readonly List<(int left, float at)> counts = new List<(int, float)>(4);
        Transform boom;
        Transform sign;
        MeshFilter signFilter;
        Vector3 home;
        float punchAt = float.NegativeInfinity;
        float openAt = float.PositiveInfinity;

        /// <summary>Index into <see cref="LevelDefinition.Gates"/>.</summary>
        public int Index { get; private set; }

        public ParcelGate Gate { get; private set; }

        /// <summary>The count the sign shows (after any scheduled change).</summary>
        public int Left { get; private set; }

        public bool IsOpen { get; private set; }

        /// <summary>The sign, for effects that want to burst from it.</summary>
        public Transform Sign => sign;

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
            MeshKit.Show("Post", go.transform, PostMesh(), skin);
            var boomRenderer = MeshKit.Show("Boom", go.transform, BoomMesh(), skin);
            view.boom = boomRenderer.transform;
            view.boom.localPosition = new Vector3(PostX, PivotY, GateZ);
            var signRenderer = MeshKit.Show("Sign", view.boom, SignMesh(color, view.Left), skin);
            view.sign = signRenderer.transform;
            view.sign.localPosition = new Vector3(BoomLength * 0.55f, BoomThickness * 0.5f, 0f);
            view.sign.localRotation = Quaternion.Euler(-(90f - SignTilt), 0f, 0f);
            view.signFilter = signRenderer.GetComponent<MeshFilter>();
            if (left <= 0) view.OpenNow();
            return view;
        }

        /// <summary>The sign shows <paramref name="left"/> from Time.time <paramref name="at"/>, with a punch.</summary>
        public void SetCount(int left, float at)
        {
            if (IsOpen && left > 0) return;
            Left = Mathf.Max(0, left);
            // Build the mesh now (event time), so the swap itself never allocates.
            SignMesh(Mathf.Clamp(Gate.Color, 0, ParcelColors.Count - 1), Left);
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
            sign.localScale = Vector3.one * (1f + (punching ? 0.3f * Mathf.Sin(k * Mathf.PI) * (1f - k) : 0f));

            if (now >= openAt)
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

        void ShowCount(int left) => signFilter.sharedMesh = SignMesh(Mathf.Clamp(Gate.Color, 0, ParcelColors.Count - 1), left);

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

        /// <summary>The footing, the post with its color housing at the pivot, and the rest on the far side.</summary>
        static Mesh PostMesh()
        {
            if (postMesh != null) return postMesh;
            var b = new MeshBuilder();
            Box(b, new Vector3(PostX, 0.04f, GateZ), new Vector3(0.26f, 0.08f, 0.26f), 0.15f, Dark);
            Box(b, new Vector3(PostX, 0.29f, GateZ), new Vector3(0.1f, 0.44f, 0.1f), 0f, Steel);
            Box(b, new Vector3(PostX, PivotY, GateZ), new Vector3(0.17f, 0.15f, 0.17f), 0.2f, Body);
            float restX = PostX + BoomLength - 0.04f;
            Box(b, new Vector3(restX, 0.03f, GateZ), new Vector3(0.14f, 0.06f, 0.14f), 0.15f, Dark);
            Box(b, new Vector3(restX, 0.24f, GateZ), new Vector3(0.05f, 0.42f, 0.05f), 0f, Steel);
            Box(b, new Vector3(restX, PivotY - BoomThickness * 0.5f - 0.02f, GateZ), new Vector3(0.12f, 0.04f, 0.1f), 0f, Dark);
            postMesh = b.ToMesh("Gate Post");
            return postMesh;
        }

        /// <summary>The boom from its pivot along +x, striped in the gate's color and white, with a counterweight behind.</summary>
        static Mesh BoomMesh()
        {
            if (boomMesh != null) return boomMesh;
            var b = new MeshBuilder();
            const int stripes = 6;
            float stripe = (BoomLength - 0.08f) / stripes;
            for (int i = 0; i < stripes; i++)
                Box(b, new Vector3(0.08f + (i + 0.5f) * stripe, 0f, 0f), new Vector3(stripe, BoomThickness, BoomThickness), 0f,
                    (i & 1) == 0 ? White : Body);
            Box(b, new Vector3(-0.1f, 0f, 0f), new Vector3(0.12f, 0.1f, 0.1f), 0.15f, Dark);
            boomMesh = b.ToMesh("Gate Boom");
            return boomMesh;
        }

        /// <summary>
        /// The sign, pivoting on its bottom edge: a plate of the gate's color, label paper, the color's
        /// symbol and the count in stamp ink. Flat in XZ with +z up the sign; one mesh per color and count.
        /// </summary>
        static Mesh SignMesh(int color, int left)
        {
            int v = Mathf.Clamp(left, 0, Digits.Max);
            int key = color * (Digits.Max + 1) + v;
            if (signMeshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            var b = new MeshBuilder();
            float c = SignHeight * 0.5f;
            Box(b, new Vector3(0f, 0.015f, c), new Vector3(SignWidth, 0.03f, SignHeight), 0.15f, Body);
            Box(b, new Vector3(0f, 0.036f, c), new Vector3(SignWidth - 0.06f, 0.012f, SignHeight - 0.06f), 0f, Paper);
            Symbols.Append(b, 0, color, Matrix4x4.TRS(new Vector3(-0.14f, 0.046f, c), Quaternion.identity, Vector3.one * 0.24f), Ink);
            Digits.Append(b, 0, v, Matrix4x4.TRS(new Vector3(0.14f, 0.046f, c), Quaternion.identity, Vector3.one * 0.19f), Ink);
            mesh = b.ToMesh($"Gate Sign {color} {v}");
            signMeshes[key] = mesh;
            return mesh;
        }

        static void Box(MeshBuilder b, Vector3 position, Vector3 scale, float bevel, Vector2 uv) =>
            MeshKit.AddBox(b, 0, Matrix4x4.TRS(position, Quaternion.identity, scale), bevel, uv);
    }
}
