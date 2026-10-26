using System.Collections.Generic;
using CargoJam.Core;
using CargoJam.Game.Meta;
using CargoJam.Game.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CargoJam.Game
{
    /// <summary>
    /// Runs one level at a time: feeds taps to the <see cref="GameSession"/> and turns the events it
    /// returns into scheduled animations. Rules resolve instantly, so input never waits on visuals.
    /// The whole scene is built from code; the scene file only needs this component.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        const float HitRadius = 0.75f;

        Camera cam;
        Tweener tweener;
        Hud hud;
        MaterialBank materials;
        LevelLibrary levels;

        int levelIndex;
        GameSession session;
        Layout layout;
        Transform levelRoot;
        DockView dock;
        readonly Dictionary<int, ParcelView> parcels = new Dictionary<int, ParcelView>();
        readonly List<TruckView> trucks = new List<TruckView>();
        readonly List<Parcel> freeScratch = new List<Parcel>();
        int lastScreenWidth, lastScreenHeight;

        public GameSession Session => session;
        public int LevelIndex => levelIndex;

        void Awake()
        {
            Application.targetFrameRate = 60;
            cam = Camera.main != null ? Camera.main : CreateCamera();
            if (FindAnyObjectByType<Light>() == null) CreateLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);

            tweener = gameObject.AddComponent<Tweener>();
            hud = gameObject.AddComponent<Hud>();
            hud.Restart += () => LoadLevel(levelIndex);
            hud.Next += () => LoadLevel(levelIndex + 1);
            hud.Previous += () => LoadLevel(levelIndex - 1);

            materials = new MaterialBank(Resources.Load<Material>("Materials/Base"));
            levels = LevelLibrary.Load();
        }

        void Start()
        {
            LoadLevel(Progress.LevelIndex);
        }

        void Update()
        {
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) FitCamera();

            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            var screen = pointer.position.ReadValue();
            if (hud.Captures(screen)) return;
            if (TryPickCell(screen, out var cell)) Tap(cell);
        }

        public void LoadLevel(int index)
        {
            if (levels.Count == 0) return;
            levelIndex = ((index % levels.Count) + levels.Count) % levels.Count;
            Progress.LevelIndex = levelIndex;

            tweener.Clear();
            hud.HideCard();
            parcels.Clear();
            trucks.Clear();
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            levelRoot = new GameObject("Level").transform;

            session = new GameSession(levels.Get(levelIndex));
            layout = new Layout(session.Board.Width, session.Board.Height, session.Dock.Capacity);
            hud.LevelLabel = $"Level {levelIndex + 1}";

            Scenery.Build(levelRoot, layout, materials);
            dock = DockView.Create(levelRoot, layout, materials);
            foreach (var p in session.Board.Parcels)
                parcels[p.Id] = ParcelView.Create(p, levelRoot, layout, materials);
            foreach (var t in session.Trucks)
            {
                var view = TruckView.Create(t, levelRoot, materials);
                view.transform.position = layout.QueuePosition(t.Index);
                trucks.Add(view);
            }

            RefreshBoard();
            FitCamera();
        }

        /// <summary>Applies a tap to the rules and schedules what happened. Public so tools and tests can drive it.</summary>
        public void Tap(Cell cell)
        {
            var events = session.Tap(cell);
            if (events.Count == 0) return;
            foreach (var e in events) Play(e);
            RefreshBoard();
        }

        void Play(GameEvent e)
        {
            float now = tweener.Now;
            switch (e)
            {
                case ParcelBlocked blocked:
                    ShakeInPlace(parcels[blocked.Parcel.Id], now);
                    break;

                case DockFullRejected rejected:
                    ShakeInPlace(parcels[rejected.Parcel.Id], now);
                    dock.Flash(tweener);
                    break;

                case ParcelLoaded loaded:
                {
                    var pv = parcels[loaded.Parcel.Id];
                    var tv = trucks[loaded.Truck.Index];
                    var path = PathPoints(loaded.Path);
                    float slide = Timing.Slide(path.Count);
                    float start = Mathf.Max(now, tv.BayReadyAt - slide - Timing.HopToTruck);
                    LeaveYard(pv, path, start, slide);
                    HopIntoTruck(pv, tv, loaded.TruckSlot, start + slide);
                    break;
                }

                case ParcelHeld held:
                {
                    var pv = parcels[held.Parcel.Id];
                    var path = PathPoints(held.Path);
                    float slide = Timing.Slide(path.Count);
                    float start = Mathf.Max(now, dock.SlotFreeAt[held.DockSlot] - slide - Timing.HopToDock);
                    LeaveYard(pv, path, start, slide);
                    tweener.Arc(pv.transform, layout.DockRest(held.DockSlot), 0.5f, Layout.ParcelSize,
                        start + slide, Timing.HopToDock);
                    pv.SettledAt = start + slide + Timing.HopToDock;
                    break;
                }

                case ParcelLoadedFromDock fromDock:
                {
                    var pv = parcels[fromDock.Parcel.Id];
                    var tv = trucks[fromDock.Truck.Index];
                    float start = Mathf.Max(now, pv.SettledAt, tv.BayReadyAt) + fromDock.ChainIndex * Timing.ChainStagger;
                    dock.SlotFreeAt[fromDock.DockSlot] = start + 0.05f;
                    HopIntoTruck(pv, tv, fromDock.TruckSlot, start);
                    break;
                }

                case TruckDeparted departed:
                    Depart(trucks[departed.Truck.Index], now);
                    break;

                case TruckArrived arrived:
                {
                    var tv = trucks[arrived.Truck.Index];
                    tweener.Punch(tv.Body, Vector3.one, new Vector3(0.06f, -0.12f, 0.06f), tv.BayReadyAt, 0.25f);
                    break;
                }

                case LevelWon _:
                {
                    float lastDeparture = 0f;
                    foreach (var tv in trucks) lastDeparture = Mathf.Max(lastDeparture, tv.MoveFreeAt);
                    hud.ShowWon(Mathf.Max(now, lastDeparture) + Timing.WinOverlayDelay);
                    break;
                }

                case LevelFailed _:
                    hud.ShowFailed(now + Timing.FailOverlayDelay);
                    break;
            }
        }

        void LeaveYard(ParcelView pv, List<Vector3> path, float start, float slide)
        {
            pv.MarkLeftYard();
            tweener.Punch(pv.transform, Layout.ParcelSize, new Vector3(-0.12f, 0.18f, -0.12f), start, Timing.TapPunch);
            tweener.MoveAlong(pv.transform, path, start, slide);
        }

        void HopIntoTruck(ParcelView pv, TruckView tv, int slot, float start)
        {
            var local = tv.SlotLocal(slot);
            var target = layout.Bay + local;
            float landing = start + Timing.HopToTruck;
            var inTruckScale = Layout.ParcelSize * Layout.InTruckScale;
            tweener.Arc(pv.transform, target, 1.1f, inTruckScale, start, Timing.HopToTruck, () =>
            {
                if (!pv || !tv) return;
                // Ride along with the truck (and its squash) from here on.
                pv.transform.SetParent(tv.Body, false);
                pv.transform.localPosition = local;
                pv.transform.localScale = inTruckScale;
            });
            tweener.Punch(tv.Body, Vector3.one, new Vector3(0.04f, -0.1f, 0.04f), landing, 0.18f);
            pv.SettledAt = landing;
            tv.LastLandingAt = Mathf.Max(tv.LastLandingAt, landing);
        }

        void Depart(TruckView tv, float now)
        {
            float departAt = Mathf.Max(now, tv.LastLandingAt, tv.BayReadyAt, tv.MoveFreeAt) + Timing.DepartDelay;
            tweener.Punch(tv.Body, Vector3.one, new Vector3(-0.1f, 0.08f, 0f), departAt - 0.05f, 0.15f);
            tweener.MoveTo(tv.transform, layout.DepartPosition, departAt + 0.05f, Timing.Depart, Ease.InCubic,
                () => { if (tv) tv.gameObject.SetActive(false); });
            tv.MoveFreeAt = departAt + 0.05f + Timing.Depart;

            // Everyone behind it rolls up one place; the first one becomes the loading truck.
            for (int i = tv.Truck.Index + 1; i < trucks.Count; i++)
            {
                var next = trucks[i];
                int offset = i - (tv.Truck.Index + 1);
                float start = Mathf.Max(departAt + 0.1f, next.MoveFreeAt);
                tweener.MoveTo(next.transform, layout.QueuePosition(offset), start, Timing.QueueShift, Ease.OutCubic);
                next.MoveFreeAt = start + Timing.QueueShift;
                if (offset == 0) next.BayReadyAt = next.MoveFreeAt;
            }
        }

        void ShakeInPlace(ParcelView pv, float now)
        {
            if (!pv.InYard) return;
            tweener.Shake(pv.transform, layout.ParcelRest(pv.Parcel.Cell, pv.IsFree), 0.08f, now, Timing.BlockedShake);
        }

        /// <summary>World points for a yard path, ending just past the dock edge.</summary>
        List<Vector3> PathPoints(IReadOnlyList<Cell> path)
        {
            var points = new List<Vector3>(path.Count + 1);
            for (int i = 1; i < path.Count; i++) points.Add(layout.ParcelRest(path[i], true));
            points.Add(layout.YardExit(path[path.Count - 1]));
            return points;
        }

        void RefreshBoard()
        {
            session.Board.GetFreeParcels(freeScratch);
            var free = new HashSet<int>();
            foreach (var p in freeScratch) free.Add(p.Id);
            foreach (var p in session.Board.Parcels) parcels[p.Id].SetFree(free.Contains(p.Id), layout, tweener);
            dock.ShowFill(session.Dock.Count, session.Dock.Capacity);
        }

        /// <summary>
        /// Maps a screen press to a yard cell. A press that misses every parcel snaps to the nearest
        /// free one within <see cref="HitRadius"/> cells, so near-misses still count.
        /// </summary>
        bool TryPickCell(Vector2 screen, out Cell cell)
        {
            cell = default;
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, Layout.ParcelSize.y, 0f));
            if (!plane.Raycast(ray, out float distance)) return false;

            var hit = ray.GetPoint(distance);
            float fx = hit.x / Layout.CellSize + (session.Board.Width - 1) * 0.5f;
            float fy = -hit.z / Layout.CellSize;
            var direct = new Cell(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy));
            if (session.Board.Get(direct) != null)
            {
                cell = direct;
                return true;
            }

            session.Board.GetFreeParcels(freeScratch);
            float best = HitRadius * HitRadius;
            bool found = false;
            foreach (var p in freeScratch)
            {
                float dx = p.Cell.X - fx, dy = p.Cell.Y - fy;
                float d = dx * dx + dy * dy;
                if (d >= best) continue;
                best = d;
                cell = p.Cell;
                found = true;
            }
            return found;
        }

        void FitCamera()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            if (layout != null) CameraRig.Fit(cam, layout.ContentBounds, hud.GameViewport);
        }

        static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = go.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.86f, 0.83f, 0.78f);
            c.nearClipPlane = 0.3f;
            c.farClipPlane = 200f;
            return c;
        }

        static void CreateLight()
        {
            var go = new GameObject("Sun");
            go.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
        }
    }
}
