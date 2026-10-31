using System.Collections.Generic;
using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// A flatbed truck driving along +X, built on the Kenney Car Kit <c>truck-flat</c> model: the cab
    /// and bed sides painted in the truck's parcel color, a dark deck the parcels sit on, neutral
    /// glass, chassis and wheels. The root takes the drive tweens; <see cref="Body"/> takes the
    /// squash, punch and dip; the wheels roll whenever the root moves.
    /// </summary>
    public sealed class TruckView : MonoBehaviour
    {
        // ---- Tuning ---------------------------------------------------------------------------------
        const string ModelPath = "Env/Cars/truck-flat";
        /// <summary>Model scale on its own axes: x = width, y = height, z = length (it drives along +X after the turn).</summary>
        static readonly Vector3 ModelScale = new Vector3(0.92f, 0.95f, 1.08f);
        /// <summary>Wheels scale uniformly (a non-uniform wheel would wobble as it spins).</summary>
        const float WheelScale = 0.95f;
        const float SlotSpacing = 0.5f;
        const float SymbolSize = 0.74f;

        // The Car Kit atlas: the white paint swatch (tinted per truck) and the dark grey the deck is moved to.
        static readonly Rect PaintCell = new Rect(0.75f, 0.25f, 0.125f, 0.25f);
        static readonly Vector2 DeckUv = new Vector2(0.1875f, 0.375f);

        // Fallbacks (model units, measured from truck-flat.fbx) if the deck or roof isn't found.
        const float DeckModelY = 0.80f, DeckRearModelZ = -1.37f, DeckFrontModelZ = 0.07f;
        const float RoofModelY = 1.30f, RoofModelZ = 0.32f;

        public Truck Truck { get; private set; }

        /// <summary>When this truck is parked at the loading bay and can receive parcels.</summary>
        public float BayReadyAt { get; set; }

        /// <summary>When the last parcel scheduled for this truck lands in its bed.</summary>
        public float LastLandingAt { get; set; }

        /// <summary>When the truck's latest scheduled drive finishes.</summary>
        public float MoveFreeAt { get; set; }

        Transform body;
        Transform frontAxle;
        Transform rearAxle;
        Vector3 lastPosition;
        float wheelAngle;

        public Transform Body => body;

        public static TruckView Create(Truck truck, Transform parent, MaterialBank materials)
        {
            var go = new GameObject($"Truck {truck.Index} {ParcelColors.ToCode(truck.Color)}");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<TruckView>();
            view.Truck = truck;

            // The body is a child so squash/stretch never fights the drive tweens on the root.
            view.body = new GameObject("Body").transform;
            view.body.SetParent(go.transform, false);
            var b = view.body;

            var model = Model;
            if (model != null)
            {
                var r = MeshKit.Show("Truck", b, model.Body, materials.TruckChassis);
                r.sharedMaterials = new[] { materials.TruckChassis, materials.TruckPaint(truck.Color) };
                view.frontAxle = MeshKit.Show("Front Axle", b, model.FrontAxle, materials.TruckChassis).transform;
                view.frontAxle.localPosition = model.FrontPivot;
                view.rearAxle = MeshKit.Show("Rear Axle", b, model.RearAxle, materials.TruckChassis).transform;
                view.rearAxle.localPosition = model.RearPivot;
                Symbols.Create(truck.Color, b, model.Roof + Vector3.up * 0.012f, SymbolSize, materials.Symbol(truck.Color));
            }
            else
            {
                BuildGreybox(b, truck.Color, materials);
            }
            view.lastPosition = go.transform.position;
            return view;
        }

        /// <summary>Bed slot position in the truck's local space.</summary>
        public Vector3 SlotLocal(int slot)
        {
            var m = Model;
            float bedCenter = m != null ? m.BedCenterX : -0.4f;
            float bedTop = m != null ? m.BedTop : 0.52f;
            float bedLength = m != null ? m.BedLength : 1.6f;
            float parcel = Layout.ParcelSize.x * Layout.InTruckScale;
            float spacing = Truck.Capacity > 1
                ? Mathf.Min(SlotSpacing, (bedLength - parcel) / (Truck.Capacity - 1))
                : 0f;
            float x = bedCenter + (slot - (Truck.Capacity - 1) * 0.5f) * spacing;
            return new Vector3(x, bedTop + Layout.ParcelSize.y * Layout.InTruckScale * 0.5f, 0f);
        }

        /// <summary>Rolls the wheels by however far the root moved along its forward axis since last frame.</summary>
        void LateUpdate()
        {
            if (frontAxle == null) return;
            var p = transform.position;
            float d = Vector3.Dot(p - lastPosition, transform.right);
            lastPosition = p;
            // A jump of several units is a placement or a loop wrap, not driving.
            if (d == 0f || d > 2f || d < -2f) return;
            wheelAngle = Mathf.Repeat(wheelAngle - d / Model.WheelRadius * Mathf.Rad2Deg, 360f);
            var spin = Quaternion.Euler(0f, 0f, wheelAngle);
            frontAxle.localRotation = spin;
            rearAxle.localRotation = spin;
        }

        static void BuildGreybox(Transform b, int color, MaterialBank materials)
        {
            MaterialBank.Box("Bed", b, new Vector3(-0.4f, 0.36f, 0f), new Vector3(1.8f, 0.32f, 1.1f), materials.TruckBody(color));
            MaterialBank.Box("Cab", b, new Vector3(0.95f, 0.55f, 0f), new Vector3(0.75f, 0.72f, 1.05f), materials.TruckCab(color));
            Symbols.Create(color, b, new Vector3(0.95f, 0.91f, 0f), 0.95f, materials.Symbol(color));
        }

        // ---- The baked model (shared by every truck) -------------------------------------------------

        sealed class Baked
        {
            public Mesh Body;
            public Mesh FrontAxle;
            public Mesh RearAxle;
            public Vector3 FrontPivot;
            public Vector3 RearPivot;
            public float WheelRadius;
            public float BedCenterX;
            public float BedTop;
            public float BedLength;
            public Vector3 Roof;
        }

        static Baked model;
        static bool modelTried;

        static Baked Model
        {
            get
            {
                // Rebake when the meshes were destroyed: with domain reload off, statics outlive the
                // play session that made them.
                if (!modelTried || (model != null && model.Body == null))
                {
                    modelTried = true;
                    model = Bake();
                }
                return model;
            }
        }

        /// <summary>
        /// Turns the Kenney model into truck-local meshes: the body split into a neutral submesh and a
        /// paint submesh (the atlas's white swatch), the deck moved to a dark swatch, and each axle's
        /// two wheels baked around their axle so they can spin.
        /// </summary>
        static Baked Bake()
        {
            var prefab = Resources.Load<GameObject>(ModelPath);
            if (prefab == null)
            {
                Debug.LogWarning($"TruckView: {ModelPath} not found, using the greybox truck.");
                return null;
            }
            var turn = Quaternion.Euler(0f, 90f, 0f);
            var place = Matrix4x4.TRS(Vector3.zero, turn, ModelScale);
            var rootOffset = Matrix4x4.Translate(-prefab.transform.position);

            var bodyBuilder = new MeshBuilder(2);
            var wheels = new List<(Mesh mesh, Matrix4x4 local)>();
            float deckY = float.MinValue, deckMinZ = float.MaxValue, deckMaxZ = float.MinValue;
            float roofY = float.MinValue, roofMinZ = float.MaxValue, roofMaxZ = float.MinValue;

            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var local = rootOffset * mf.transform.localToWorldMatrix;
                if (mf.name.StartsWith("wheel"))
                {
                    wheels.Add((mesh, local));
                    continue;
                }
                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"TruckView: {ModelPath} mesh is not readable; enable Read/Write on the model.");
                    return null;
                }

                var v = mesh.vertices;
                var uv = mesh.uv;
                var tris = mesh.triangles;
                var corners = new Vector3[3];
                for (int t = 0; t < tris.Length; t += 3)
                {
                    var uvc = Vector2.zero;
                    for (int k = 0; k < 3; k++)
                    {
                        corners[k] = local.MultiplyPoint3x4(v[tris[t + k]]);
                        if (uv.Length > 0) uvc += uv[tris[t + k]] / 3f;
                    }
                    var n = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]);
                    if (n.sqrMagnitude < 1e-12f) continue;
                    n.Normalize();
                    var center = (corners[0] + corners[1] + corners[2]) / 3f;
                    bool paint = PaintCell.Contains(uvc);
                    bool facingUp = n.y > 0.9f;
                    int sub = paint ? 1 : 0;
                    Vector2? uvOverride = null;
                    if (paint && facingUp && center.z < DeckFrontModelZ + 0.02f && center.y < 1.0f)
                    {
                        // The flat deck behind the cab: dark, so the parcels on it pop.
                        sub = 0;
                        uvOverride = DeckUv;
                        deckY = Mathf.Max(deckY, center.y);
                        for (int k = 0; k < 3; k++)
                        {
                            deckMinZ = Mathf.Min(deckMinZ, corners[k].z);
                            deckMaxZ = Mathf.Max(deckMaxZ, corners[k].z);
                        }
                    }
                    else if (paint && facingUp && center.y > 1.0f)
                    {
                        roofY = Mathf.Max(roofY, center.y);
                        for (int k = 0; k < 3; k++)
                        {
                            roofMinZ = Mathf.Min(roofMinZ, corners[k].z);
                            roofMaxZ = Mathf.Max(roofMaxZ, corners[k].z);
                        }
                    }
                    var wn = place.inverse.transpose.MultiplyVector(n).normalized;
                    bodyBuilder.Polygon(sub, wn, Vector2.zero, place.MultiplyPoint3x4(corners[0]),
                        place.MultiplyPoint3x4(corners[1]), place.MultiplyPoint3x4(corners[2]));
                    SetLastUvs(bodyBuilder, uvOverride, uv, tris, t);
                }
            }

            var baked = new Baked { Body = bodyBuilder.ToMesh("Truck Body") };
            if (deckY == float.MinValue)
            {
                deckY = DeckModelY; deckMinZ = DeckRearModelZ; deckMaxZ = DeckFrontModelZ;
            }
            if (roofY == float.MinValue)
            {
                roofY = RoofModelY; roofMinZ = roofMaxZ = RoofModelZ;
            }
            baked.BedTop = deckY * ModelScale.y;
            baked.BedCenterX = (deckMinZ + deckMaxZ) * 0.5f * ModelScale.z;
            baked.BedLength = (deckMaxZ - deckMinZ) * ModelScale.z;
            baked.Roof = new Vector3((roofMinZ + roofMaxZ) * 0.5f * ModelScale.z, roofY * ModelScale.y, 0f);

            // Wheels: grouped per axle (front = +z in the model), baked around the axle line.
            var front = new MeshBuilder();
            var rear = new MeshBuilder();
            float radius = 0f;
            Vector3 frontPivot = Vector3.zero, rearPivot = Vector3.zero;
            foreach (var (mesh, local) in wheels)
            {
                if (!mesh.isReadable) return null;
                var origin = local.MultiplyPoint3x4(Vector3.zero);
                bool isFront = origin.z > 0f;
                var pivot = new Vector3(origin.z * ModelScale.z, origin.y * ModelScale.y, 0f);
                if (isFront) frontPivot = pivot; else rearPivot = pivot;
                // Around the axle: lateral offset follows the body width; the wheel itself scales uniformly.
                var toAxle = Matrix4x4.Rotate(turn)
                             * Matrix4x4.Translate(new Vector3(origin.x * ModelScale.x, 0f, 0f))
                             * Matrix4x4.Scale(Vector3.one * WheelScale)
                             * Matrix4x4.Translate(-origin)
                             * local;
                (isFront ? front : rear).Append(0, mesh, toAxle);
                foreach (var p in mesh.vertices)
                {
                    var q = local.MultiplyPoint3x4(p) - origin;
                    radius = Mathf.Max(radius, Mathf.Abs(q.y) * WheelScale);
                }
            }
            baked.FrontAxle = front.ToMesh("Truck Front Axle");
            baked.RearAxle = rear.ToMesh("Truck Rear Axle");
            baked.FrontPivot = frontPivot;
            baked.RearPivot = rearPivot;
            baked.WheelRadius = radius > 0.01f ? radius : 0.28f;
            return baked;
        }

        /// <summary>Fixes the UVs of the triangle just added (Polygon writes one shared placeholder UV).</summary>
        static void SetLastUvs(MeshBuilder b, Vector2? uvOverride, Vector2[] uv, int[] tris, int t) =>
            b.SetTailUvs(uvOverride ?? (uv.Length > 0 ? uv[tris[t]] : Vector2.zero),
                uvOverride ?? (uv.Length > 0 ? uv[tris[t + 1]] : Vector2.zero),
                uvOverride ?? (uv.Length > 0 ? uv[tris[t + 2]] : Vector2.zero));
    }
}
