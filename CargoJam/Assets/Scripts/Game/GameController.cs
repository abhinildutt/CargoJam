using System;
using System.Collections.Generic;
using CargoJam.Core;
using CargoJam.Game.Feel;
using CargoJam.Game.Meta;
using CargoJam.Game.Presentation;
using CargoJam.Meta;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CargoJam.Game
{
    public readonly struct LevelStartInfo
    {
        /// <summary>0-based progress index (keeps growing after the content runs out).</summary>
        public readonly int Index;
        public readonly bool Hard;
        public readonly int ParcelCount;

        public LevelStartInfo(int index, bool hard, int parcelCount)
        {
            Index = index;
            Hard = hard;
            ParcelCount = parcelCount;
        }

        /// <summary>The number players see.</summary>
        public int Number => Index + 1;

        /// <summary>Route theme: a new one every 20 levels.</summary>
        public int Route => Index / 20;
    }

    /// <summary>
    /// Runs one level at a time: feeds taps to the <see cref="GameSession"/> and turns the events it
    /// returns into scheduled animations. Rules resolve instantly, so input never waits on visuals.
    ///
    /// The UI drives it through the public API (start levels, boosters, continue) and listens to its
    /// events; sound, haptics and particles go through <see cref="ILevelFx"/>. With no UI installed it
    /// falls back to the IMGUI debug <see cref="Hud"/> and starts on its own.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        const float HitRadius = 0.75f;
        /// <summary>Levels before this index are never repeated when the content loops (tutorials, ramp).</summary>
        const int LoopStart = 20;

        Camera cam;
        Tweener tweener;
        Hud debugHud;
        IHudLayout hudLayout;
        ILevelFx fx;
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
        float inputBlockedUntil;
        LevelViews views;
        bool craneArmed;
        bool movedThisLevel;

        /// <summary>Loads in a row without anything going to the dock; drives the rising load pitch and callouts.</summary>
        int combo;

        // ---- Events for the UI ------------------------------------------------------------------

        public event Action<LevelStartInfo> LevelStarted;
        /// <summary>The level is won. Argument: Time.time when the celebration is done and the win panel should show.</summary>
        public event Action<float> LevelWon;
        /// <summary>The level is lost. Arguments: parcels still to deliver, and Time.time to show the fail panel.</summary>
        public event Action<int, float> LevelFailed;
        public event Action<int> ComboChanged;
        /// <summary>Holding dock fill: count, capacity.</summary>
        public event Action<int, int> DockChanged;
        /// <summary>A booster actually took effect (charge the player now).</summary>
        public event Action<BoosterType> BoosterApplied;
        /// <summary>The Crane was armed (true) or disarmed (false).</summary>
        public event Action<bool> CraneArmedChanged;

        // ---- State for the UI -------------------------------------------------------------------

        public GameSession Session => session;
        public int LevelIndex => levelIndex;
        public int LevelCount => levels?.Count ?? 0;
        public bool IsHard => session?.Level.Hard ?? false;
        public bool CraneArmed => craneArmed;
        /// <summary>True once the player has made a move this level (restarting then breaks a streak).</summary>
        public bool MovedThisLevel => movedThisLevel;
        public int ParcelsLeft => session == null ? 0 : session.Board.Count + session.Dock.Count;

        /// <summary>The UI turns this off while a popup or panel covers the board.</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>
        /// Set by the UI before Start. When nothing sets it, the IMGUI debug HUD is used and the
        /// controller starts the saved level itself.
        /// </summary>
        public IHudLayout HudLayout
        {
            get => hudLayout;
            set
            {
                hudLayout = value;
                lastScreenWidth = -1;
            }
        }

        /// <summary>The FX layer (defaults to <see cref="FeelDirector"/>).</summary>
        public ILevelFx Fx
        {
            get => fx;
            set => fx = value;
        }

        public Camera Camera => cam;
        public MaterialBank Materials => materials;

        void Awake()
        {
            Application.targetFrameRate = 60;
            cam = Camera.main != null ? Camera.main : CreateCamera();
            if (FindAnyObjectByType<Light>() == null) CreateLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);

            tweener = gameObject.AddComponent<Tweener>();
            fx = gameObject.AddComponent<FeelDirector>();
            materials = new MaterialBank(Resources.Load<Material>("Materials/Base"));
            levels = LevelLibrary.Load();
        }

        void Start()
        {
            if (hudLayout != null) return;

            // No UI took over: run with the debug HUD.
            debugHud = gameObject.AddComponent<Hud>();
            debugHud.Audio = (fx as FeelDirector)?.Audio;
            debugHud.Restart += Restart;
            debugHud.Next += () => StartLevel(levelIndex + 1);
            debugHud.Previous += () => StartLevel(Mathf.Max(0, levelIndex - 1));
            LevelWon += at => debugHud.ShowWon(at);
            LevelFailed += (left, at) => debugHud.ShowFailed(at);
            HudLayout = debugHud;
            StartLevel(Progress.LevelIndex);
        }

        void Update()
        {
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) FitCamera();

            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            if (!InputEnabled || session == null || Time.time < inputBlockedUntil) return;
            var screen = pointer.position.ReadValue();
            if (hudLayout != null && hudLayout.Captures(screen)) return;
            if (TryPickCell(screen, craneArmed, out var cell)) Tap(cell);
        }

        // ---- Level lifecycle --------------------------------------------------------------------

        /// <summary>Builds and starts the level at a 0-based progress index (content loops past the last level).</summary>
        public void StartLevel(int index)
        {
            if (levels.Count == 0) return;
            levelIndex = Mathf.Max(0, index);
            if (debugHud != null) Progress.LevelIndex = levelIndex;

            fx?.LevelCleared();
            tweener.Clear();
            debugHud?.HideCard();
            parcels.Clear();
            trucks.Clear();
            combo = 0;
            craneArmed = false;
            movedThisLevel = false;
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            levelRoot = new GameObject("Level").transform;

            session = new GameSession(levels.Get(ContentIndex(levelIndex)));
            layout = new Layout(session.Board.Width, session.Board.Height, session.Dock.Capacity);
            if (debugHud != null) debugHud.LevelLabel = $"Level {levelIndex + 1}{(session.Level.Hard ? "  HARD" : "")}";

            Scenery.Build(levelRoot, layout, materials);
            dock = DockView.Create(levelRoot, layout, materials);
            foreach (var p in session.Board.Parcels)
            {
                parcels[p.Id] = ParcelView.Create(p, levelRoot, layout, materials,
                    hidden: session.IsHidden(p), locked: session.IsLocked(p), isKey: session.IsKey(p));
            }
            foreach (var t in session.Trucks)
            {
                var view = TruckView.Create(t, levelRoot, materials);
                view.transform.position = layout.QueuePosition(t.Index);
                trucks.Add(view);
            }

            RefreshBoard();
            FitCamera();

            views = new LevelViews
            {
                LevelIndex = levelIndex,
                Hard = session.Level.Hard,
                Root = levelRoot,
                Camera = cam,
                Layout = layout,
                Materials = materials,
                Tweener = tweener,
                Dock = dock,
                Parcels = new List<ParcelView>(parcels.Values),
                Trucks = trucks,
            };
            float introSeconds = fx?.LevelBuilt(views) ?? 0f;
            inputBlockedUntil = Time.time + introSeconds;

            ComboChanged?.Invoke(0);
            DockChanged?.Invoke(session.Dock.Count, session.Dock.Capacity);
            LevelStarted?.Invoke(new LevelStartInfo(levelIndex, session.Level.Hard, session.Board.Count));
        }

        public void Restart() => StartLevel(levelIndex);

        /// <summary>Which level file a progress index plays: real order first, then a loop that skips the early ramp.</summary>
        public int ContentIndex(int index)
        {
            int count = levels.Count;
            if (index < count) return index;
            int loopStart = Mathf.Min(LoopStart, count - 1);
            return loopStart + (index - count) % (count - loopStart);
        }

        /// <summary>Whether the level at a progress index is a HARD one (for the home screen badge).</summary>
        public bool IsHardLevel(int index) => levels.Get(ContentIndex(index)).Hard;

        // ---- Boosters ---------------------------------------------------------------------------

        public bool CanUseBooster(BoosterType booster)
        {
            if (session == null || session.Status != GameStatus.Playing) return false;
            switch (booster)
            {
                case BoosterType.Undo: return session.CanUndo;
                case BoosterType.ExtraSlot: return session.CanAddHoldingSlot;
                default:
                    foreach (var p in session.Board.Parcels)
                        if (session.CanCrane(p.Cell)) return true;
                    return false;
            }
        }

        /// <summary>
        /// Undo and Extra Slot apply at once. Crane arms a mode where the next board tap lifts any parcel;
        /// using it again disarms. <see cref="BoosterApplied"/> fires when the effect happens.
        /// </summary>
        public bool UseBooster(BoosterType booster)
        {
            if (booster == BoosterType.Crane && craneArmed)
            {
                CancelCrane();
                return false;
            }
            if (!CanUseBooster(booster)) return false;

            switch (booster)
            {
                case BoosterType.Undo:
                    PlayAll(session.Undo());
                    break;
                case BoosterType.ExtraSlot:
                    PlayAll(session.AddHoldingSlot());
                    break;
                default:
                    craneArmed = true;
                    CraneArmedChanged?.Invoke(true);
                    return true;
            }
            fx?.BoosterUsed(booster, tweener.Now);
            BoosterApplied?.Invoke(booster);
            return true;
        }

        public void CancelCrane()
        {
            if (!craneArmed) return;
            craneArmed = false;
            CraneArmedChanged?.Invoke(false);
        }

        /// <summary>The fail screen's Continue: one more slot, and play resumes.</summary>
        public bool ContinueWithExtraSlot()
        {
            if (session == null || session.Status != GameStatus.Failed || !session.CanAddHoldingSlot) return false;
            PlayAll(session.AddHoldingSlot());
            if (session.Status != GameStatus.Playing) return false;
            fx?.BoosterUsed(BoosterType.ExtraSlot, tweener.Now);
            EaseCameraHome(0.4f);
            return true;
        }

        // ---- Hints for the tutorial -------------------------------------------------------------

        /// <summary>A good parcel to point at: a free one matching the truck, else any free one.</summary>
        public bool TryGetHintCell(out Cell cell)
        {
            cell = default;
            if (session == null) return false;
            session.Board.GetFreeParcels(freeScratch);
            Parcel best = null;
            foreach (var p in freeScratch)
            {
                if (session.IsLocked(p)) continue;
                if (session.CurrentTruck != null && session.CurrentTruck.Accepts(p))
                {
                    best = p;
                    break;
                }
                if (best == null) best = p;
            }
            if (best == null) return false;
            cell = best.Cell;
            return true;
        }

        /// <summary>Screen position (origin bottom-left) of the top of the parcel at a cell.</summary>
        public Vector2 CellToScreen(Cell cell)
        {
            var top = layout.ParcelRest(cell, true) + Vector3.up * (Layout.ParcelSize.y * 0.5f);
            return cam.WorldToScreenPoint(top);
        }

        // ---- Taps and event playback ------------------------------------------------------------

        /// <summary>Applies a tap to the rules and schedules what happened. Public so tools and tests can drive it.</summary>
        public void Tap(Cell cell)
        {
            if (session == null) return;
            IReadOnlyList<GameEvent> events;
            if (craneArmed)
            {
                if (!session.CanCrane(cell)) return;
                events = session.CraneLift(cell);
                if (events.Count > 0)
                {
                    craneArmed = false;
                    CraneArmedChanged?.Invoke(false);
                    fx?.BoosterUsed(BoosterType.Crane, tweener.Now);
                    BoosterApplied?.Invoke(BoosterType.Crane);
                }
            }
            else
            {
                events = session.Tap(cell);
            }
            PlayAll(events);
        }

        void PlayAll(IReadOnlyList<GameEvent> events)
        {
            if (events.Count == 0) return;
            int comboBefore = combo;
            int dockBefore = session.Dock.Count, capacityBefore = session.Dock.Capacity;
            bool held = false;
            foreach (var e in events)
            {
                Play(e);
                held |= e is ParcelHeld;
            }
            RefreshBoard();
            if (combo != comboBefore) ComboChanged?.Invoke(combo);
            // Holds report their own fill at landing time; anything else that changed the dock reports now.
            if (!held && (session.Dock.Count != dockBefore || session.Dock.Capacity != capacityBefore))
                fx?.DockFillChanged(session.Dock.Count, session.Dock.Capacity, tweener.Now);
            DockChanged?.Invoke(session.Dock.Count, session.Dock.Capacity);
        }

        void Play(GameEvent e)
        {
            float now = tweener.Now;
            switch (e)
            {
                case ParcelBlocked blocked:
                    ShakeInPlace(parcels[blocked.Parcel.Id], now);
                    fx?.TapRejected(parcels[blocked.Parcel.Id], false);
                    break;

                case DockFullRejected rejected:
                    ShakeInPlace(parcels[rejected.Parcel.Id], now);
                    dock.Flash(tweener);
                    fx?.TapRejected(parcels[rejected.Parcel.Id], true);
                    break;

                case ParcelLoaded loaded:
                {
                    movedThisLevel = true;
                    var pv = parcels[loaded.Parcel.Id];
                    var tv = trucks[loaded.Truck.Index];
                    var path = loaded.Lifted ? LiftPoints(pv) : PathPoints(loaded.Path);
                    float slide = loaded.Lifted ? Timing.CraneLift : Timing.Slide(path.Count);
                    float start = Mathf.Max(now, tv.BayReadyAt - slide - Timing.HopToTruck);
                    LeaveYard(pv, path, start, slide, loaded.Lifted);
                    HopIntoTruck(pv, tv, loaded.TruckSlot, start + slide);
                    break;
                }

                case ParcelHeld held:
                {
                    movedThisLevel = true;
                    var pv = parcels[held.Parcel.Id];
                    var path = held.Lifted ? LiftPoints(pv) : PathPoints(held.Path);
                    float slide = held.Lifted ? Timing.CraneLift : Timing.Slide(path.Count);
                    float start = Mathf.Max(now, dock.SlotFreeAt[held.DockSlot] - slide - Timing.HopToDock);
                    LeaveYard(pv, path, start, slide, held.Lifted);
                    tweener.Arc(pv.transform, layout.DockRest(held.DockSlot), 0.5f, Layout.ParcelSize,
                        start + slide, Timing.HopToDock);
                    pv.SettledAt = start + slide + Timing.HopToDock;
                    combo = 0;
                    fx?.ParcelLanded(pv, LandingSpot.Dock, pv.SettledAt, 0);
                    fx?.DockFillChanged(session.Dock.Count, session.Dock.Capacity, pv.SettledAt);
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

                case ParcelReturned returned:
                {
                    var pv = parcels[returned.Parcel.Id];
                    float start = Mathf.Max(now, pv.SettledAt);
                    float duration = Timing.HopToDock * 1.4f;
                    dock.SlotFreeAt[returned.DockSlot] = start + 0.05f;
                    var home = layout.ParcelRest(returned.Parcel.Cell, false);
                    tweener.Arc(pv.transform, home, 1.2f, Layout.ParcelSize, start, duration, () =>
                    {
                        if (pv) pv.MarkBackInYard();
                        RefreshBoard();
                    });
                    pv.SettledAt = start + duration;
                    fx?.ParcelLanded(pv, LandingSpot.Yard, pv.SettledAt, 0);
                    break;
                }

                case DockExpanded expanded:
                    ExpandDock(expanded.Capacity, now);
                    break;

                case ParcelRevealed revealed:
                {
                    var pv = parcels[revealed.Parcel.Id];
                    float at = now + 0.05f;
                    pv.Reveal(tweener, at);
                    fx?.ParcelRevealed(pv, at);
                    break;
                }

                case ParcelUnlocked unlocked:
                {
                    var pv = parcels[unlocked.Parcel.Id];
                    // The key has to land in its truck first.
                    float at = now + Timing.SlideMax + Timing.HopToTruck;
                    pv.Unlock(tweener, at);
                    fx?.ParcelUnlocked(pv, at);
                    break;
                }

                case TruckDeparted departed:
                    Depart(trucks[departed.Truck.Index], now);
                    break;

                case TruckArrived arrived:
                {
                    var tv = trucks[arrived.Truck.Index];
                    tweener.Punch(tv.Body, Vector3.one, new Vector3(0.06f, -0.12f, 0.06f), tv.BayReadyAt, 0.25f);
                    fx?.TruckArriving(tv, tv.BayReadyAt);
                    break;
                }

                case LevelWon _:
                    Celebrate(now);
                    break;

                case LevelFailed _:
                    ExplainFail(now);
                    break;
            }
        }

        void LeaveYard(ParcelView pv, List<Vector3> path, float start, float slide, bool lifted)
        {
            pv.MarkLeftYard();
            tweener.Punch(pv.transform, Layout.ParcelSize, new Vector3(-0.12f, 0.18f, -0.12f), start, Timing.TapPunch);
            tweener.MoveAlong(pv.transform, path, start, slide);
            // Feedback on the tap itself is immediate, even if the parcel waits for a truck to pull in.
            fx?.ParcelPicked(pv, lifted);
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
            combo++;
            fx?.ParcelLanded(pv, LandingSpot.Truck, landing, combo);
        }

        void Depart(TruckView tv, float now)
        {
            float departAt = Mathf.Max(now, tv.LastLandingAt, tv.BayReadyAt, tv.MoveFreeAt) + Timing.DepartDelay;
            tweener.Punch(tv.Body, Vector3.one, new Vector3(-0.1f, 0.08f, 0f), departAt - 0.05f, 0.15f);
            tweener.MoveTo(tv.transform, layout.DepartPosition, departAt + 0.05f, Timing.Depart, Ease.InCubic,
                () => { if (tv) tv.gameObject.SetActive(false); });
            tv.MoveFreeAt = departAt + 0.05f + Timing.Depart;
            fx?.TruckFull(tv, departAt);

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

        void ExpandDock(int capacity, float now)
        {
            var oldFreeAt = dock.SlotFreeAt;
            Destroy(dock.gameObject);
            layout = new Layout(layout.Width, layout.Height, capacity);
            dock = DockView.Create(levelRoot, layout, materials);
            for (int i = 0; i < oldFreeAt.Length && i < dock.SlotFreeAt.Length; i++) dock.SlotFreeAt[i] = oldFreeAt[i];
            dock.PopIn(tweener, now, capacity - 1);
            if (views != null)
            {
                views.Dock = dock;
                views.Layout = layout;
            }

            // Held parcels slide to their slots' new positions.
            for (int slot = 0; slot < session.Dock.Capacity; slot++)
            {
                var held = session.Dock[slot];
                if (held == null) continue;
                var pv = parcels[held.Id];
                float start = Mathf.Max(now, pv.SettledAt);
                tweener.MoveTo(pv.transform, layout.DockRest(slot), start, 0.25f, Ease.OutCubic);
                pv.SettledAt = start + 0.25f;
            }
            FitCamera();
        }

        /// <summary>Confetti, fanfare and a slow push-in once the last truck is on its way.</summary>
        void Celebrate(float now)
        {
            float lastDeparture = now;
            foreach (var tv in trucks) lastDeparture = Mathf.Max(lastDeparture, tv.MoveFreeAt - Timing.Depart * 0.5f);
            float at = lastDeparture + Timing.WinCelebrateDelay;
            var center = layout.ContentBounds.center;

            fx?.Win(at, center);
            PushCamera(center, 0.07f, at, 0.9f);
            LevelWon?.Invoke(at + Timing.WinOverlayDelay);
        }

        /// <summary>
        /// Shows why the level was lost before the panel covers it: the waiting truck bounces, the dock
        /// shakes, and the buried parcels it needed pulse while the camera leans toward the dock.
        /// </summary>
        void ExplainFail(float now)
        {
            float at = now + Timing.FailExplainDelay;
            var truck = session.CurrentTruck;
            var tv = truck != null ? trucks[truck.Index] : null;
            var needed = new List<ParcelView>();

            if (tv != null)
            {
                tweener.Punch(tv.Body, Vector3.one, new Vector3(0.08f, 0.12f, 0.08f), at, 0.3f);
                foreach (var p in session.Board.Parcels)
                {
                    if (p.Color != truck.Color) continue;
                    var pv = parcels[p.Id];
                    needed.Add(pv);
                    tweener.At(at, pv.Highlight);
                    for (int k = 0; k < 3; k++)
                        tweener.Punch(pv.transform, Layout.ParcelSize, new Vector3(0.3f, 0.3f, 0.3f), at + 0.15f + k * Timing.FailPulse, 0.3f);
                }
            }
            tweener.At(at, () => dock.Flash(tweener));
            fx?.Fail(at, needed, tv);
            PushCamera(layout.DockSlotCenter((layout.DockSlots - 1) / 2), 0.06f, at, 0.6f);
            LevelFailed?.Invoke(ParcelsLeft, at + Timing.FailOverlayDelay);
        }

        void PushCamera(Vector3 toward, float fraction, float start, float duration)
        {
            var from = cam.transform.position;
            tweener.MoveTo(cam.transform, Vector3.Lerp(from, toward, fraction), start, duration, Ease.OutCubic);
        }

        /// <summary>Glides the camera back to its fitted framing (after a fail push-in).</summary>
        void EaseCameraHome(float duration)
        {
            var current = cam.transform.position;
            FitCamera();
            var target = cam.transform.position;
            cam.transform.position = current;
            tweener.MoveTo(cam.transform, target, tweener.Now, duration, Ease.OutCubic);
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

        /// <summary>The Crane lifts straight up, then swings over the dock edge.</summary>
        List<Vector3> LiftPoints(ParcelView pv)
        {
            var up = layout.ParcelRest(pv.Parcel.Cell, true) + Vector3.up * 2.2f;
            var over = new Vector3(up.x, up.y, Layout.DockEdgeZ + 0.2f);
            return new List<Vector3> { up, over };
        }

        void RefreshBoard()
        {
            if (session == null) return;
            session.Board.GetFreeParcels(freeScratch);
            var free = new HashSet<int>();
            foreach (var p in freeScratch) free.Add(p.Id);
            foreach (var p in session.Board.Parcels)
            {
                var pv = parcels[p.Id];
                if (!pv.InYard) continue;
                pv.SetFree(free.Contains(p.Id) && !session.IsLocked(p), layout, tweener);
            }
            dock.ShowFill(session.Dock.Count, session.Dock.Capacity);
        }

        /// <summary>
        /// Maps a screen press to a yard cell. A press that misses every parcel snaps to the nearest
        /// free one within <see cref="HitRadius"/> cells, so near-misses still count. With the Crane
        /// armed any parcel counts as a target.
        /// </summary>
        bool TryPickCell(Vector2 screen, bool anyParcel, out Cell cell)
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

            if (anyParcel)
            {
                freeScratch.Clear();
                freeScratch.AddRange(session.Board.Parcels);
            }
            else
            {
                session.Board.GetFreeParcels(freeScratch);
            }
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
            if (layout == null) return;
            var viewport = hudLayout != null ? hudLayout.GameViewport : new Rect(0.03f, 0.03f, 0.94f, 0.85f);
            CameraRig.Fit(cam, layout.ContentBounds, viewport);
        }

        static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
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
