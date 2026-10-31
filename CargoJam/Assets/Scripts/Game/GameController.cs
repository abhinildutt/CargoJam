using System;
using System.Collections.Generic;
using CargoJam.Core;
using CargoJam.Game.Feel;
using CargoJam.Game.Meta;
using CargoJam.Game.Presentation;
using CargoJam.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace CargoJam.Game
{
    public readonly struct LevelStartInfo
    {
        /// <summary>0-based progress index (keeps growing after the content runs out).</summary>
        public readonly int Index;
        public readonly bool Hard;
        public readonly int ParcelCount;
        /// <summary>The level's <see cref="LevelDefinition.CargoMask"/>: bit (1 &lt;&lt; (int)kind) per <see cref="CargoKind"/> used.</summary>
        public readonly int CargoMask;

        public LevelStartInfo(int index, bool hard, int parcelCount, int cargoMask = 0)
        {
            Index = index;
            Hard = hard;
            ParcelCount = parcelCount;
            CargoMask = cargoMask;
        }

        public bool Uses(CargoKind kind) => (CargoMask & (1 << (int)kind)) != 0;

        /// <summary>The number players see.</summary>
        public int Number => Index + 1;

        /// <summary>Journey destination (see <see cref="Journey"/>).</summary>
        public int Stop => Journey.StopFor(Index);
        public Mood Mood => Journey.MoodFor(Index);
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
        Light sun;
        WorldEnvironment environment;
        IMenuLayout menuLayout;
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
        readonly List<GateView> gates = new List<GateView>();
        readonly List<StrapView> straps = new List<StrapView>();
        readonly List<Parcel> freeScratch = new List<Parcel>();
        readonly HashSet<int> freeIds = new HashSet<int>();
        /// <summary>
        /// Parcels the rules have freed that must still look blocked until a time: ice still on show, or a
        /// gate's boom still down in front of them. <see cref="RefreshBoard"/> runs again when each ends.
        /// </summary>
        readonly Dictionary<int, float> heldDownUntil = new Dictionary<int, float>();
        /// <summary>Per gate: when its sign last changed, and when its boom is up (after it opened).</summary>
        float[] gateCountAt = Array.Empty<float>();
        float[] gateClearAt = Array.Empty<float>();
        int lastScreenWidth, lastScreenHeight;
        float inputBlockedUntil;
        LevelViews views;
        MenuDiorama menu;

        /// <summary>Where the home screen's 3D backdrop sits on screen (0..1 viewport) when no <see cref="IMenuLayout"/> says.</summary>
        static readonly Rect DefaultMenuViewport = new Rect(0.02f, 0.31f, 0.96f, 0.27f);
        bool craneArmed;
        bool movedThisLevel;

        /// <summary>Loads in a row without anything going to the dock; drives the rising load pitch and callouts.</summary>
        int combo;

        /// <summary>What the music's tension layer was last told (see <see cref="UpdateTension"/>).</summary>
        bool tense;

        // Playback state for the batch of events from one move (reset by PlayAll).
        /// <summary>When the move's first parcel starts to slide (-1 until one does): express ticks land just after.</summary>
        float moveStartAt;
        /// <summary>The parcel that most recently started to leave the yard, and when (ice cracks as it goes).</summary>
        int lastLeaveId;
        float lastLeaveAt;
        /// <summary>A strapped pair on its way out: its band slips off as the lead starts; the trail follows one cell behind.</summary>
        StrapView pairStrap;
        int pairLeadId = -1, pairTrailId = -1;
        float pairTrailAt;
        /// <summary>Gates that opened in this batch (parcels behind them wait for the boom).</summary>
        readonly List<int> gatesOpened = new List<int>();
        /// <summary>Express parcels that ran out of moves in this batch.</summary>
        readonly List<Parcel> missedExpress = new List<Parcel>();
        /// <summary>When this batch's effect on tension shows: the latest hold landing, dock-chain load or express tick.</summary>
        float tensionAt;

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
        public WorldEnvironment Environment => environment;

        /// <summary>Set by the UI: where the home screen wants the menu diorama framed.</summary>
        public IMenuLayout MenuLayout
        {
            get => menuLayout;
            set
            {
                menuLayout = value;
                lastScreenWidth = -1;
            }
        }

        /// <summary>
        /// Set by the UI: whether the destination arrival reveal should play for a level index (it plays
        /// once per destination reached, not on every retry). Unset means it always plays on arrivals.
        /// </summary>
        public Predicate<int> ShouldRevealArrival { get; set; }

        /// <summary>What the environment showed for the current level or menu.</summary>
        public EnvironmentInfo EnvironmentShown { get; private set; }

        void Awake()
        {
            Application.targetFrameRate = 60;
            cam = Camera.main != null ? Camera.main : CreateCamera();
            sun = FindAnyObjectByType<Light>();
            if (sun == null) sun = CreateLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);

            tweener = gameObject.AddComponent<Tweener>();
            fx = gameObject.AddComponent<FeelDirector>();
            materials = new MaterialBank(Resources.Load<Material>("Materials/Base"));
            levels = LevelLibrary.Load();

            environment = new GameObject("Environment").AddComponent<WorldEnvironment>();
            environment.Init(sun, cam, materials);
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
            if (ScreenInfo.Width != lastScreenWidth || ScreenInfo.Height != lastScreenHeight) FitCamera();

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

            ClearLevel();
            debugHud?.HideCard();
            levelRoot = new GameObject("Level").transform;

            session = new GameSession(levels.Get(ContentIndex(levelIndex)));
            layout = new Layout(session.Board.Width, session.Board.Height, session.Dock.Capacity);
            if (debugHud != null) debugHud.LevelLabel = $"Level {levelIndex + 1}{(session.Level.Hard ? "  HARD" : "")}";

            var level = session.Level;
            Scenery.Build(levelRoot, layout, materials, Bollards(level));
            var env = environment.ShowLevel(levelIndex, layout);
            if (env.Arrival && ShouldRevealArrival != null) env.Arrival = ShouldRevealArrival(levelIndex);
            EnvironmentShown = env;
            dock = DockView.Create(levelRoot, layout, materials);
            foreach (var p in session.Board.Parcels)
            {
                var look = new ParcelLook
                {
                    Hidden = session.IsHidden(p),
                    Locked = session.IsLocked(p),
                    IsKey = session.IsKey(p),
                    Frost = session.FrostLeft(p),
                    Express = session.IsExpress(p) ? session.ExpressLeft(p) : 0,
                };
                parcels[p.Id] = ParcelView.Create(p, levelRoot, layout, materials, look);
            }
            for (int i = 0; i < level.Gates.Count; i++)
                gates.Add(GateView.Create(i, level.Gates[i], session.GateLeft(i), levelRoot, layout, materials));
            gateCountAt = new float[gates.Count];
            gateClearAt = new float[gates.Count];
            for (int i = 0; i < level.Straps.Count; i++)
            {
                var a = parcels[session.Board.Get(level.Straps[i].A).Id];
                var b = parcels[session.Board.Get(level.Straps[i].B).Id];
                straps.Add(StrapView.Create(i, a, b, materials));
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
                Gates = gates,
                Straps = straps,
                NextHard = IsHardLevel(levelIndex + 1),
                Environment = environment,
            };
            float introSeconds = fx?.LevelBuilt(views) ?? 0f;
            introSeconds += fx?.EnvironmentChanged(env) ?? 0f;
            inputBlockedUntil = Time.time + introSeconds;
            // A level can open tense (a short express count); the layer comes in as the board lands.
            UpdateTension(inputBlockedUntil);

            ComboChanged?.Invoke(0);
            DockChanged?.Invoke(session.Dock.Count, session.Dock.Capacity);
            LevelStarted?.Invoke(new LevelStartInfo(levelIndex, session.Level.Hard, session.Board.Count, session.Level.CargoMask));
        }

        public void Restart() => StartLevel(levelIndex);

        /// <summary>
        /// Swaps the board for the home screen's living depot backdrop, themed for the level the player
        /// is on. No level is loaded while it shows; <see cref="StartLevel"/> replaces it.
        /// </summary>
        public void ShowMenuScene(int progressIndex)
        {
            ClearLevel();
            levelIndex = Mathf.Max(0, progressIndex);
            menu = MenuDiorama.Create(materials);
            var env = environment.ShowMenu(levelIndex);
            env.Arrival = false;
            EnvironmentShown = env;
            fx?.EnvironmentChanged(env);
            FitCamera();
        }

        public bool MenuSceneActive => menu != null;

        /// <summary>Tears down whatever is on stage: the current level or the menu backdrop.</summary>
        void ClearLevel()
        {
            if (tense)
            {
                tense = false;
                fx?.TensionChanged(false, tweener.Now);
            }
            fx?.LevelCleared();
            tweener.Clear();
            parcels.Clear();
            trucks.Clear();
            gates.Clear();
            straps.Clear();
            heldDownUntil.Clear();
            gateCountAt = gateClearAt = Array.Empty<float>();
            ResetBatch(0f);
            combo = 0;
            craneArmed = false;
            movedThisLevel = false;
            session = null;
            layout = null;
            views = null;
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            levelRoot = null;
            if (menu != null) Destroy(menu.gameObject);
            menu = null;
        }

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

        /// <summary>
        /// Whether the fail screen should offer Continue (+1 slot). Only after running
        /// <see cref="FailReason.OutOfSpace"/>: a board stuck behind ice, padlocks or gates, or a missed
        /// express parcel, isn't helped by another slot.
        /// </summary>
        public bool CanContinue => session != null && session.Status == GameStatus.Failed
            && session.FailReason == FailReason.OutOfSpace && session.CanAddHoldingSlot;

        /// <summary>The fail screen's Continue: one more slot, and play resumes.</summary>
        public bool ContinueWithExtraSlot()
        {
            if (!CanContinue) return false;
            PlayAll(session.AddHoldingSlot());
            if (session.Status != GameStatus.Playing) return false;
            fx?.BoosterUsed(BoosterType.ExtraSlot, tweener.Now);
            EaseCameraHome(0.4f);
            return true;
        }

        // ---- Hints for the tutorial -------------------------------------------------------------

        /// <summary>
        /// A good move to point at, among the units that can be tapped and taken now: an express parcel
        /// about to run out first, then one the truck takes whole, else any.
        /// </summary>
        public bool TryGetHintCell(out Cell cell)
        {
            cell = default;
            if (session == null) return false;
            session.GetMoves(freeScratch);
            Parcel best = null;
            int bestRank = 0;
            foreach (var p in freeScratch)
            {
                if (!session.CanTake(p)) continue;
                int rank = session.IsExpress(p) && session.ExpressLeft(p) <= ParcelView.ExpressUrgent ? 3
                    : TruckTakesWhole(p) ? 2 : 1;
                if (rank <= bestRank) continue;
                best = p;
                bestRank = rank;
            }
            if (best == null) return false;
            cell = best.Cell;
            return true;
        }

        /// <summary>
        /// Where the hand should point to teach a cargo kind on its debut level (see design v4, section 5):
        /// a tappable, takeable unit that shows the kind off. False when there is nothing to point at
        /// (always for bollards).
        /// </summary>
        public bool TryGetCargoHintCell(CargoKind kind, out Cell cell)
        {
            cell = default;
            if (session == null || session.Status != GameStatus.Playing || kind == CargoKind.Bollard) return false;
            session.GetMoves(freeScratch);
            foreach (var p in freeScratch)
            {
                if (!session.CanTake(p) || !Teaches(kind, p)) continue;
                cell = p.Cell;
                return true;
            }
            return false;
        }

        /// <summary>Whether tapping <paramref name="p"/> (a unit from GetMoves) shows off a cargo kind.</summary>
        bool Teaches(CargoKind kind, Parcel p)
        {
            var partner = session.StrapPartner(p);
            switch (kind)
            {
                case CargoKind.Frost:
                    // A neighbour of the frozen crate (either member of a pair cracks its own neighbours).
                    return NextToFrost(p) || (partner != null && NextToFrost(partner));
                case CargoKind.Mystery:
                    // The parcel covering it.
                    return NextToHidden(p) || (partner != null && NextToHidden(partner));
                case CargoKind.Express:
                    return session.IsExpress(p) && session.ExpressLeft(p) > 0;
                case CargoKind.Lock:
                    return session.IsKey(p);
                case CargoKind.Strap:
                    // GetMoves lists the pair by its free member.
                    return partner != null;
                case CargoKind.Gate:
                    return TruckTakesWhole(p) && ClosedGateWants(p.Color);
                default:
                    return false;
            }
        }

        /// <summary>The loading truck takes this unit without the dock: its color, with room for a whole pair.</summary>
        bool TruckTakesWhole(Parcel p)
        {
            var truck = session.CurrentTruck;
            if (truck == null || !truck.Accepts(p)) return false;
            return session.StrapPartner(p) == null || truck.Capacity - truck.Loaded >= 2;
        }

        bool ClosedGateWants(int color)
        {
            var level = session.Level;
            for (int g = 0; g < level.Gates.Count; g++)
                if (level.Gates[g].Color == color && !session.IsGateOpen(g)) return true;
            return false;
        }

        bool NextToFrost(Parcel p)
        {
            for (int d = 0; d < 4; d++)
            {
                var n = session.Board.Get(Neighbour(p.Cell, d));
                if (n != null && session.FrostLeft(n) > 0) return true;
            }
            return false;
        }

        bool NextToHidden(Parcel p)
        {
            for (int d = 0; d < 4; d++)
            {
                var n = session.Board.Get(Neighbour(p.Cell, d));
                if (n != null && session.IsHidden(n)) return true;
            }
            return false;
        }

        static Cell Neighbour(Cell c, int direction) => direction switch
        {
            0 => new Cell(c.X, c.Y - 1),
            1 => new Cell(c.X - 1, c.Y),
            2 => new Cell(c.X + 1, c.Y),
            _ => new Cell(c.X, c.Y + 1),
        };

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
            ResetBatch(tweener.Now);
            foreach (var e in events)
            {
                Play(e);
                held |= e is ParcelHeld;
            }
            // A pair whose lead never got scheduled (not expected) still drops its band.
            if (pairStrap != null) pairStrap.Release(tweener.Now);
            pairStrap = null;
            HoldBehindOpeningGates();
            RefreshBoard();
            if (combo != comboBefore) ComboChanged?.Invoke(combo);
            // Holds report their own fill at landing time; anything else that changed the dock reports now.
            if (!held && (session.Dock.Count != dockBefore || session.Dock.Capacity != capacityBefore))
                fx?.DockFillChanged(session.Dock.Count, session.Dock.Capacity, tweener.Now);
            DockChanged?.Invoke(session.Dock.Count, session.Dock.Capacity);
            UpdateTension(tensionAt);
        }

        /// <summary>
        /// Tells the music whether the board is tense (design v4, section 7): while playing with the dock
        /// one short of full, or an express parcel in the yard nearly out of moves. Only changes are sent;
        /// a win or fail turns it off (and so does tearing the level down, see <see cref="ClearLevel"/>).
        /// </summary>
        void UpdateTension(float at)
        {
            bool wanted = session != null && session.Status == GameStatus.Playing
                && (session.Dock.Count >= session.Dock.Capacity - 1 || ExpressNearlyOut());
            if (wanted == tense) return;
            tense = wanted;
            fx?.TensionChanged(tense, Mathf.Max(at, tweener.Now));
        }

        bool ExpressNearlyOut()
        {
            foreach (var p in session.Board.Parcels)
                if (session.IsExpress(p) && session.ExpressLeft(p) <= ParcelView.ExpressUrgent) return true;
            return false;
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
                    float hop = LeaveYard(pv, path, slide, loaded.Lifted, tv.BayReadyAt - Timing.HopToTruck, now);
                    HopIntoTruck(pv, tv, loaded.TruckSlot, hop);
                    break;
                }

                case ParcelHeld held:
                {
                    movedThisLevel = true;
                    var pv = parcels[held.Parcel.Id];
                    var path = held.Lifted ? LiftPoints(pv) : PathPoints(held.Path);
                    float slide = held.Lifted ? Timing.CraneLift : Timing.Slide(path.Count);
                    float hop = LeaveYard(pv, path, slide, held.Lifted, dock.SlotFreeAt[held.DockSlot] - Timing.HopToDock, now);
                    tweener.Arc(pv.transform, layout.DockRest(held.DockSlot), 0.5f, Layout.ParcelSize, hop, Timing.HopToDock);
                    pv.SettledAt = hop + Timing.HopToDock;
                    combo = 0;
                    tensionAt = Mathf.Max(tensionAt, pv.SettledAt);
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
                    tensionAt = Mathf.Max(tensionAt, start);
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
                    // A parcel uncovered by a gate opening flips once the boom is up.
                    float at = Mathf.Max(now + 0.05f, GateClearAt(revealed.Parcel));
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

                case FrostCracked cracked:
                {
                    var pv = parcels[cracked.Parcel.Id];
                    // A neighbour cracks it as it starts to slide away; the Crane thaws its target at once.
                    float at = now;
                    if (cracked.By != null)
                        at = (cracked.By.Id == lastLeaveId ? lastLeaveAt : now) + Timing.CrackDelay;
                    pv.Crack(tweener, at, cracked.LayersLeft);
                    fx?.FrostCracked(pv, cracked.LayersLeft, at);
                    // Thawed by a neighbour: it lifts as the collar bursts, not before.
                    if (cracked.LayersLeft == 0 && cracked.By != null) HoldDown(cracked.Parcel.Id, at);
                    break;
                }

                case ExpressTicked ticked:
                {
                    var pv = parcels[ticked.Parcel.Id];
                    // Just after the move starts (which may wait for a truck to pull in).
                    float at = (moveStartAt >= 0f ? Mathf.Max(now, moveStartAt) : now) + Timing.SlideMin;
                    pv.SetExpress(ticked.Left, tweener, at);
                    fx?.ExpressTicked(pv, ticked.Left, at);
                    tensionAt = Mathf.Max(tensionAt, at);
                    break;
                }

                case ExpressMissed missed:
                    // Its tag already reads a red 0 from the last tick; the fail explanation points at it.
                    missedExpress.Add(missed.Parcel);
                    break;

                case StrapMoved moved:
                    // The lead's ParcelLoaded/Held comes next: the band slips off as it starts to slide.
                    pairStrap = StrapFor(moved.Lead);
                    pairLeadId = moved.Lead.Id;
                    pairTrailId = moved.Trail.Id;
                    break;

                case StrapCut cut:
                {
                    var strap = StrapFor(cut.Lifted);
                    if (strap == null) break;
                    strap.Snap(now);
                    fx?.StrapSnapped(strap, now);
                    break;
                }

                case StrapRestored restored:
                {
                    // Both ParcelReturned events came first: the band goes back on once both have landed.
                    var strap = StrapFor(restored.A);
                    if (strap == null) break;
                    strap.Restore(Mathf.Max(parcels[restored.A.Id].SettledAt, parcels[restored.B.Id].SettledAt));
                    break;
                }

                case GateProgress progress:
                {
                    if (progress.Gate < 0 || progress.Gate >= gates.Count) break;
                    var gv = gates[progress.Gate];
                    // The count drops when the loaded parcel lands in its truck (its hop is already scheduled).
                    float at = Mathf.Max(now, parcels[progress.Loaded.Id].SettledAt);
                    gv.SetCount(progress.Left, at);
                    fx?.GateProgress(gv, progress.Left, at);
                    gateCountAt[progress.Gate] = at;
                    break;
                }

                case GateOpened opened:
                {
                    if (opened.Gate < 0 || opened.Gate >= gates.Count) break;
                    var gv = gates[opened.Gate];
                    // Right after its last GateProgress: the sign reads 0, then the boom lifts.
                    float at = Mathf.Max(now, gateCountAt[opened.Gate]) + Timing.GateOpenDelay;
                    gv.Open(at);
                    fx?.GateOpened(gv, at);
                    gateClearAt[opened.Gate] = at + GateView.BoomTime;
                    gatesOpened.Add(opened.Gate);
                    break;
                }

                case LevelWon _:
                    Celebrate(now);
                    break;

                case LevelFailed failed:
                    ExplainFail(now, failed.Reason);
                    break;

                default:
                    Debug.LogWarning($"GameController: no playback for {e.GetType().Name}; it was skipped.");
                    break;
            }
        }

        /// <summary>Starts a new batch of events (one move, Undo or Extra Slot).</summary>
        void ResetBatch(float now)
        {
            moveStartAt = -1f;
            lastLeaveId = -1;
            lastLeaveAt = now;
            pairStrap = null;
            pairLeadId = pairTrailId = -1;
            pairTrailAt = now;
            gatesOpened.Clear();
            missedExpress.Clear();
            tensionAt = now;
        }

        /// <summary>The view of the strap a parcel belongs to (intact or not), or null.</summary>
        StrapView StrapFor(Parcel parcel)
        {
            int s = session.StrapIndex(parcel);
            return s >= 0 && s < straps.Count ? straps[s] : null;
        }

        /// <summary>
        /// Slides a parcel out of the yard and returns when it may hop on: as it reaches the edge, but not
        /// before <paramref name="hopReady"/> (its truck has pulled in, or its dock slot is clear). A
        /// parcel waits on its cell so it arrives just in time; a strapped pair's trail instead follows
        /// its lead one cell behind, and waits at the edge if it must.
        /// </summary>
        float LeaveYard(ParcelView pv, List<Vector3> path, float slide, bool lifted, float hopReady, float now)
        {
            int id = pv.Parcel.Id;
            float start = id == pairTrailId ? pairTrailAt : Mathf.Max(now, hopReady - slide);
            pv.MarkLeftYard();
            tweener.Punch(pv.transform, Layout.ParcelSize, new Vector3(-0.12f, 0.18f, -0.12f), start, Timing.TapPunch);
            tweener.MoveAlong(pv.transform, path, start, slide);
            // Feedback on the tap itself is immediate, even if the parcel waits for a truck to pull in.
            fx?.ParcelPicked(pv, lifted);

            if (moveStartAt < 0f || start < moveStartAt) moveStartAt = start;
            lastLeaveId = id;
            lastLeaveAt = start;
            if (id == pairLeadId)
            {
                pairStrap?.Release(start);
                pairStrap = null;
                pairLeadId = -1;
                // One cell of the lead's pace behind it (the trail's path is one cell longer).
                pairTrailAt = start + slide / Mathf.Max(1, path.Count);
            }
            else if (id == pairTrailId)
            {
                pairTrailId = -1;
            }
            return Mathf.Max(start + slide, hopReady);
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

            var lastTruck = trucks.Count > 0 ? trucks[trucks.Count - 1].transform.position : center;
            fx?.WinSequence(lastDeparture, lastTruck);
            fx?.Win(at, center);

            // The next level opens a new destination: load its models now, while the win plays.
            if (Journey.IsArrival(levelIndex + 1)) environment.Preload(levelIndex + 1);
            PushCamera(center, 0.07f, at, 0.9f);
            LevelWon?.Invoke(at + Timing.WinOverlayDelay);
        }

        /// <summary>
        /// Shows why the level was lost before the panel covers it, by <paramref name="reason"/>:
        /// <list type="bullet">
        /// <item>OutOfSpace: the waiting truck bounces, the dock shakes, and the buried parcels it needed
        /// pulse while the camera leans toward the dock.</item>
        /// <item>ExpressMissed: the express parcel that ran out pulses.</item>
        /// <item>Stuck: what holds the free parcels back pulses (ice, padlocks, a pair with no room for
        /// two), and so do the gates still closed.</item>
        /// </list>
        /// </summary>
        void ExplainFail(float now, FailReason reason)
        {
            float at = now + Timing.FailExplainDelay;
            var truck = session.CurrentTruck;
            var tv = truck != null ? trucks[truck.Index] : null;
            var needed = new List<ParcelView>();
            var lookAt = layout.DockSlotCenter((layout.DockSlots - 1) / 2);

            switch (reason)
            {
                case FailReason.ExpressMissed:
                    foreach (var p in missedExpress) needed.Add(parcels[p.Id]);
                    break;

                case FailReason.Stuck:
                    session.Board.GetFreeParcels(freeScratch);
                    foreach (var p in freeScratch)
                    {
                        var partner = session.StrapPartner(p);
                        if (!session.IsRestrained(p) && partner == null) continue;
                        AddOnce(needed, parcels[p.Id]);
                        if (partner != null) AddOnce(needed, parcels[partner.Id]);
                    }
                    foreach (var gv in gates)
                        if (!gv.IsOpen)
                            for (int k = 0; k < 3; k++)
                                tweener.Punch(gv.transform, Vector3.one, new Vector3(0.12f, 0.18f, 0.12f), at + 0.15f + k * Timing.FailPulse, 0.3f);
                    break;

                default:
                    if (tv == null) break;
                    tweener.Punch(tv.Body, Vector3.one, new Vector3(0.08f, 0.12f, 0.08f), at, 0.3f);
                    foreach (var p in session.Board.Parcels)
                        if (p.Color == truck.Color) needed.Add(parcels[p.Id]);
                    break;
            }

            foreach (var pv in needed)
            {
                tweener.At(at, pv.Highlight);
                for (int k = 0; k < 3; k++)
                    tweener.Punch(pv.transform, Layout.ParcelSize, new Vector3(0.3f, 0.3f, 0.3f), at + 0.15f + k * Timing.FailPulse, 0.3f);
            }
            if (reason == FailReason.OutOfSpace)
                tweener.At(at, () => dock.Flash(tweener));
            else if (needed.Count > 0)
                lookAt = needed[0].transform.position;
            fx?.Fail(at, reason, needed, tv);
            PushCamera(lookAt, 0.06f, at, 0.6f);
            tensionAt = Mathf.Max(tensionAt, at);
            LevelFailed?.Invoke(ParcelsLeft, at + Timing.FailOverlayDelay);
        }

        static void AddOnce(List<ParcelView> list, ParcelView pv)
        {
            if (!list.Contains(pv)) list.Add(pv);
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

        /// <summary>
        /// Lifts and brightens what can be tapped. A strapped pair counts as free when either member has
        /// a path (<see cref="GameSession.IsUnitFree"/>), so both members lift together under their band;
        /// padlocked and frozen parcels stay down (<see cref="GameSession.IsRestrained"/>), as do parcels
        /// held down until their ice or gate is out of the way on screen.
        /// </summary>
        void RefreshBoard()
        {
            if (session == null) return;
            float now = tweener.Now;
            session.Board.GetFreeParcels(freeScratch);
            freeIds.Clear();
            foreach (var p in freeScratch)
                if (!heldDownUntil.TryGetValue(p.Id, out float until) || until <= now) freeIds.Add(p.Id);
            foreach (var p in session.Board.Parcels)
            {
                var pv = parcels[p.Id];
                if (pv.HasKeyBadge && !session.IsKey(p)) pv.RemoveKeyBadge();
                if (!pv.InYard) continue;
                var partner = session.StrapPartner(p);
                bool unitFree = freeIds.Contains(p.Id) || (partner != null && freeIds.Contains(partner.Id));
                pv.SetFree(unitFree && !session.IsRestrained(p), layout, tweener);
            }
            dock.ShowFill(session.Dock.Count, session.Dock.Capacity);
        }

        /// <summary>Keeps a parcel looking blocked until <paramref name="until"/>, then refreshes the board.</summary>
        void HoldDown(int parcelId, float until)
        {
            if (until <= tweener.Now) return;
            if (heldDownUntil.TryGetValue(parcelId, out float current) && current >= until) return;
            heldDownUntil[parcelId] = until;
            tweener.At(until, RefreshBoard);
        }

        /// <summary>
        /// When a parcel freed by this batch's gate openings may look free (and flip, if hidden): once the
        /// boom of a gate on its way out is up. 0 when no gate it needs opened in this batch.
        /// </summary>
        float GateClearAt(Parcel parcel)
        {
            if (gatesOpened.Count == 0) return 0f;
            var path = session.Board.FindExitPath(parcel.Cell);
            if (path == null) return 0f;
            float clear = 0f;
            foreach (int g in gatesOpened)
                if (path.Contains(session.Level.Gates[g].Cell)) clear = Mathf.Max(clear, gateClearAt[g]);
            return clear;
        }

        /// <summary>Parcels that a gate opening in this batch has just freed stay down until its boom is up.</summary>
        void HoldBehindOpeningGates()
        {
            if (gatesOpened.Count == 0) return;
            session.Board.GetFreeParcels(freeScratch);
            foreach (var p in freeScratch)
            {
                var pv = parcels[p.Id];
                if (!pv.InYard || pv.IsFree) continue;
                HoldDown(p.Id, GateClearAt(p));
            }
        }

        /// <summary>The level's bollard cells, for the yard mesh.</summary>
        List<Cell> Bollards(LevelDefinition level)
        {
            var bollards = new List<Cell>();
            for (int y = 0; y < session.Board.Height; y++)
            for (int x = 0; x < session.Board.Width; x++)
                if (level.IsBollard(x, y)) bollards.Add(new Cell(x, y));
            return bollards;
        }

        /// <summary>
        /// Maps a screen press to a yard cell. A press that misses every parcel snaps to the nearest
        /// tappable unit (<see cref="GameSession.GetMoves"/>) within <see cref="HitRadius"/> cells, so
        /// near-misses still count. With the Crane armed any parcel counts as a target. A press on a
        /// bollard or closed gate is nobody's.
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
            if (session.Board.IsObstacle(direct)) return false;
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
                session.GetMoves(freeScratch);
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
            lastScreenWidth = ScreenInfo.Width;
            lastScreenHeight = ScreenInfo.Height;
            if (menu != null)
            {
                CameraRig.Fit(cam, MenuDiorama.Bounds, menuLayout != null ? menuLayout.MenuViewport : DefaultMenuViewport);
                return;
            }
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
            // Bloom, tonemapping and vignette come from the environment's volume (design v3).
            c.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            return c;
        }

        static Light CreateLight()
        {
            var go = new GameObject("Sun");
            go.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            return light;
        }
    }
}
