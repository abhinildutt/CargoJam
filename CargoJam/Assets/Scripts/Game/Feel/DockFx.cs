using System.Collections.Generic;
using CargoJam.Game.Presentation;
using CargoJam.Game.Presentation.Fx;
using UnityEngine;
using UnityEngine.Rendering;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// Dock feedback: a squash and pad glow when a parcel lands, and the danger vignette. The
    /// controller only reports the fill when a parcel is held, so this tracks which parcels are
    /// visibly on the dock (landed and not yet lifted off) and derives the danger level from that.
    /// </summary>
    public sealed class DockFx
    {
        /// <summary>Mirrors DockView's private pad scale, so punches settle exactly where it left them.</summary>
        static readonly Vector3 PadScale = new Vector3(0.92f, 0.04f, 0.92f);
        static readonly Vector3 SquashAmount = new Vector3(0.2f, -0.28f, 0.2f);
        const int PlateCount = 4;
        const float GlowTime = 0.32f;

        readonly FxMaterials materials;
        readonly FxOverlay overlay;
        readonly Transform plateRoot;
        readonly Transform[] plates = new Transform[PlateCount];
        readonly MeshRenderer[] plateRenderers = new MeshRenderer[PlateCount];
        int nextPlate;

        readonly HashSet<ParcelView> held = new HashSet<ParcelView>();
        LevelViews views;
        DockView dock;
        bool dockStale;
        int visible;
        int capacity;
        bool quiet;

        public DockFx(FxMaterials materials, FxOverlay overlay)
        {
            this.materials = materials;
            this.overlay = overlay;
            plateRoot = new GameObject("FX Pad Glow").transform;
            for (int i = 0; i < PlateCount; i++)
            {
                var box = MaterialBank.Box("Glow", plateRoot, Vector3.zero, Vector3.one, materials.PadGlow);
                var r = box.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                plates[i] = box.transform;
                plateRenderers[i] = r;
            }
        }

        /// <summary>Starts tracking a new level's dock.</summary>
        public void Begin(LevelViews levelViews)
        {
            views = levelViews;
            dock = views.Dock;
            dockStale = false;
            held.Clear();
            visible = 0;
            capacity = dock != null ? dock.SlotCount : 0;
            quiet = false;
            Refresh();
        }

        /// <summary>Stops tracking (level teardown).</summary>
        public void End()
        {
            views = null;
            dock = null;
            held.Clear();
            visible = 0;
            for (int i = 0; i < PlateCount; i++) plateRenderers[i].enabled = false;
            overlay.SetDanger(0);
        }

        /// <summary>Destroys the pooled glow plates.</summary>
        public void Destroy()
        {
            if (plateRoot != null) Object.Destroy(plateRoot.gameObject);
        }

        /// <summary>The authoritative capacity, from the controller.</summary>
        public void SetCapacity(int value)
        {
            capacity = value;
            Refresh();
        }

        /// <summary>Extra Slot / Continue: the controller rebuilt the dock with one more pad.</summary>
        public void SlotAdded()
        {
            capacity++;
            dockStale = true;
            Refresh();
        }

        /// <summary>Silences the vignette for the rest of the level (win).</summary>
        public void ClearDanger()
        {
            quiet = true;
            Refresh();
        }

        /// <summary>A parcel will land on the dock at <paramref name="at"/>.</summary>
        public void Landed(ParcelView parcel, float at)
        {
            if (views == null) return;
            held.Add(parcel);
            views.Tweener.At(at, () =>
            {
                visible++;
                Refresh();
                if (!parcel) return;
                Squash(parcel);
                Glow(parcel.transform.position, at);
            });
        }

        /// <summary>A parcel landed somewhere else; if it came from the dock, it lifts off at <paramref name="liftOff"/>.</summary>
        public void Left(ParcelView parcel, float liftOff)
        {
            if (views == null || !held.Remove(parcel)) return;
            views.Tweener.At(liftOff, () =>
            {
                visible = Mathf.Max(0, visible - 1);
                Refresh();
            });
        }

        /// <summary>World position of the newest (rightmost) pad.</summary>
        public bool TryNewestPad(out Vector3 position)
        {
            position = default;
            var d = CurrentDock();
            if (d == null || d.SlotCount == 0) return false;
            position = d.Pad(d.SlotCount - 1).position;
            return true;
        }

        void Refresh()
        {
            int level = 0;
            if (!quiet && visible > 0 && capacity > 0)
                level = visible >= capacity ? 2 : visible == capacity - 1 ? 1 : 0;
            overlay.SetDanger(level);
        }

        void Squash(ParcelView parcel)
        {
            var tr = parcel.transform;
            var root = views.Root;
            var rest = Layout.ParcelSize;
            // Stops as soon as the parcel hops on (it is reparented to a truck, or back in the yard).
            views.Tweener.Run(views.Tweener.Now, 0.2f,
                t =>
                {
                    if (!tr || tr.parent != root || parcel.InYard) return;
                    float k = Mathf.Sin(t * Mathf.PI) * (1f - t);
                    tr.localScale = rest + Vector3.Scale(rest, SquashAmount) * k;
                },
                null,
                () => { if (tr && tr.parent == root && !parcel.InYard) tr.localScale = rest; });
        }

        void Glow(Vector3 near, float at)
        {
            var d = CurrentDock();
            if (d == null || d.SlotCount == 0) return;

            int slot = 0;
            float best = float.MaxValue;
            for (int i = 0; i < d.SlotCount; i++)
            {
                var p = d.Pad(i).position;
                float dx = p.x - near.x, dz = p.z - near.z;
                float dist = dx * dx + dz * dz;
                if (dist < best)
                {
                    best = dist;
                    slot = i;
                }
            }
            var pad = d.Pad(slot);
            var tw = views.Tweener;
            tw.Punch(pad, PadScale, new Vector3(0.14f, 0f, 0.14f), at, 0.22f);

            // Flash the pad itself; ShowFill may recolor it meanwhile, in which case we leave it be.
            var padRenderer = pad.GetComponent<MeshRenderer>();
            var original = padRenderer.sharedMaterial;
            var glow = materials.PadGlow;
            padRenderer.sharedMaterial = glow;
            tw.At(at + 0.12f, () =>
            {
                if (padRenderer && padRenderer.sharedMaterial == glow) padRenderer.sharedMaterial = original;
            });

            // A bright plate just under the pad swells out past its edge and back: reads as a glow ring.
            int i0 = nextPlate;
            nextPlate = (nextPlate + 1) % PlateCount;
            var plate = plates[i0];
            var plateRenderer = plateRenderers[i0];
            var center = pad.position + Vector3.down * 0.004f;
            plate.position = center;
            plate.localScale = new Vector3(PadScale.x, 0.03f, PadScale.z);
            plateRenderer.enabled = true;
            tw.Run(at, GlowTime,
                t =>
                {
                    float s = 1f + 0.34f * Mathf.Sin(Mathf.Sqrt(t) * Mathf.PI);
                    plate.localScale = new Vector3(PadScale.x * s, 0.03f, PadScale.z * s);
                },
                null,
                () => plateRenderer.enabled = false);
        }

        /// <summary>
        /// The live dock. Extra Slot destroys and rebuilds it, so after that the newest DockView under
        /// the level root (the one with the most pads; the old one may linger until frame end) is used.
        /// </summary>
        DockView CurrentDock()
        {
            if (views == null) return null;
            if (dock != null && !dockStale) return dock;
            dockStale = false;
            DockView bestDock = null;
            foreach (var d in views.Root.GetComponentsInChildren<DockView>())
                if (bestDock == null || d.SlotCount > bestDock.SlotCount) bestDock = d;
            dock = bestDock;
            return dock;
        }
    }
}
