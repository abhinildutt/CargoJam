using System.Collections.Generic;
using System.Text;

namespace CargoJam.Core
{
    public enum GameStatus
    {
        Playing,
        Won,
        Failed,
    }

    /// <summary>
    /// The rules of Cargo Jam. Deterministic and engine-free: every <see cref="Tap"/> (or booster) updates
    /// state immediately and returns what happened, so views can animate while input stays live.
    /// </summary>
    public sealed class GameSession
    {
        /// <summary>The most holding slots Extra Slot can grow the dock to.</summary>
        public const int MaxHoldingSlots = 7;

        readonly List<Truck> trucks;
        readonly List<Parcel> freeScratch = new List<Parcel>();

        // Parcels never change, so clones share these two; ids index the per-parcel state arrays below.
        readonly Parcel[] parcelsById;
        readonly int[] lockedIds;

        // Per parcel id: still under a tarp; the key parcel's id while padlocked, else -1.
        readonly bool[] hidden;
        readonly int[] lockKey;
        int hiddenCount;

        // Held parcels in the order they were held, for Undo. Parcels drop out when they leave the dock.
        readonly List<Parcel> holdOrder;

        public LevelDefinition Level { get; }
        public Board Board { get; }
        public HoldingDock Dock { get; }
        public IReadOnlyList<Truck> Trucks => trucks;
        public int CurrentTruckIndex { get; private set; }
        public GameStatus Status { get; private set; }

        /// <summary>The truck at the loading bay, or null once every truck has left.</summary>
        public Truck CurrentTruck => CurrentTruckIndex < trucks.Count ? trucks[CurrentTruckIndex] : null;

        public GameSession(LevelDefinition level)
        {
            Level = level;
            Board = new Board(level.Width, level.Height);
            Dock = new HoldingDock(level.HoldingSlots);
            trucks = new List<Truck>(level.Trucks.Count);
            holdOrder = new List<Parcel>(MaxHoldingSlots);

            var idAt = new int[level.Width * level.Height];
            var parcels = new List<Parcel>();
            for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                idAt[y * level.Width + x] = -1;
                int color = level.ColorAt(x, y);
                if (color == ParcelColors.None) continue;
                var parcel = new Parcel(parcels.Count, color, new Cell(x, y));
                idAt[y * level.Width + x] = parcel.Id;
                parcels.Add(parcel);
                Board.Place(parcel);
            }
            parcelsById = parcels.ToArray();

            hidden = new bool[parcelsById.Length];
            foreach (var p in parcelsById)
            {
                if (!level.IsMystery(p.Cell.X, p.Cell.Y)) continue;
                hidden[p.Id] = true;
                hiddenCount++;
            }

            lockKey = new int[parcelsById.Length];
            for (int i = 0; i < lockKey.Length; i++) lockKey[i] = -1;
            lockedIds = new int[level.Locks.Count];
            for (int i = 0; i < level.Locks.Count; i++)
            {
                var l = level.Locks[i];
                int locked = idAt[l.Locked.Y * level.Width + l.Locked.X];
                lockedIds[i] = locked;
                lockKey[locked] = idAt[l.Key.Y * level.Width + l.Key.X];
            }

            for (int i = 0; i < level.Trucks.Count; i++)
                trucks.Add(new Truck(i, level.Trucks[i], level.TruckCapacity));

            // Mystery parcels that start free were never really hidden.
            RevealFree(null);
        }

        GameSession(GameSession source)
        {
            Level = source.Level;
            Board = new Board(source.Board.Width, source.Board.Height);
            foreach (var p in source.Board.Parcels) Board.Place(p);
            Dock = new HoldingDock(source.Dock.Capacity);
            for (int i = 0; i < source.Dock.Capacity; i++)
                if (source.Dock[i] != null) Dock.PlaceAt(i, source.Dock[i]);
            trucks = new List<Truck>(source.trucks.Count);
            foreach (var t in source.trucks) trucks.Add(t.Copy());
            CurrentTruckIndex = source.CurrentTruckIndex;
            Status = source.Status;

            parcelsById = source.parcelsById;
            lockedIds = source.lockedIds;
            hidden = (bool[])source.hidden.Clone();
            lockKey = (int[])source.lockKey.Clone();
            hiddenCount = source.hiddenCount;
            holdOrder = new List<Parcel>(source.holdOrder);
        }

        public GameSession Clone() => new GameSession(this);

        // ---- Blockers ---------------------------------------------------------------------------

        /// <summary>Locked parcels can't be tapped until their key parcel has been loaded into a truck.</summary>
        public bool IsLocked(Parcel parcel) => lockKey[parcel.Id] >= 0;

        /// <summary>Mystery parcels hide their color until they first become free.</summary>
        public bool IsHidden(Parcel parcel) => hidden[parcel.Id];

        /// <summary>True while the parcel is the key of a parcel that is still locked.</summary>
        public bool IsKey(Parcel parcel)
        {
            foreach (int locked in lockedIds)
                if (lockKey[locked] == parcel.Id) return true;
            return false;
        }

        // ---- Taps -------------------------------------------------------------------------------

        public IReadOnlyList<GameEvent> Tap(Cell cell)
        {
            var events = new List<GameEvent>();
            if (Status != GameStatus.Playing) return events;

            var parcel = Board.Get(cell);
            if (parcel == null) return events;

            var path = IsLocked(parcel) ? null : Board.FindExitPath(cell);
            if (path == null)
            {
                events.Add(new ParcelBlocked(parcel));
                return events;
            }

            if (!CanTakeFromYard(parcel))
            {
                events.Add(new DockFullRejected(parcel));
                return events;
            }

            MoveOut(parcel, path, false, events);
            FinishMove(events);
            return events;
        }

        /// <summary>True when some tappable parcel would go straight into the loading truck.</summary>
        public bool HasFreeMatch()
        {
            var truck = CurrentTruck;
            if (truck == null) return false;
            Board.GetFreeParcels(freeScratch);
            foreach (var p in freeScratch)
                if (!IsLocked(p) && truck.Accepts(p)) return true;
            return false;
        }

        // ---- Boosters ---------------------------------------------------------------------------

        /// <summary>Undo is possible while playing with at least one parcel held.</summary>
        public bool CanUndo => Status == GameStatus.Playing && holdOrder.Count > 0;

        /// <summary>
        /// Sends the most recently held parcel still in the dock back to its yard cell
        /// (<see cref="ParcelReturned"/>). Cells only ever empty, so the cell is always free to take it.
        /// </summary>
        public IReadOnlyList<GameEvent> Undo()
        {
            var events = new List<GameEvent>();
            if (!CanUndo) return events;

            var parcel = holdOrder[holdOrder.Count - 1];
            holdOrder.RemoveAt(holdOrder.Count - 1);
            int slot = Dock.IndexOf(parcel);
            Dock.RemoveAt(slot);
            Board.Place(parcel);
            events.Add(new ParcelReturned(parcel, slot));

            FinishMove(events);
            return events;
        }

        /// <summary>True while the dock is below <see cref="MaxHoldingSlots"/>, in Playing or Failed state.</summary>
        public bool CanAddHoldingSlot =>
            (Status == GameStatus.Playing || Status == GameStatus.Failed) && Dock.Capacity < MaxHoldingSlots;

        /// <summary>
        /// Adds a holding slot (<see cref="DockExpanded"/>). Also the fail "Continue": called while
        /// <see cref="GameStatus.Failed"/>, it resumes play.
        /// </summary>
        public IReadOnlyList<GameEvent> AddHoldingSlot()
        {
            var events = new List<GameEvent>();
            if (!CanAddHoldingSlot) return events;

            Dock.AddSlot();
            events.Add(new DockExpanded(Dock.Capacity));
            Status = GameStatus.Playing;
            return events;
        }

        /// <summary>The Crane can lift any parcel on the board, blocked, hidden or locked, if the truck or dock can take it.</summary>
        public bool CanCrane(Cell cell)
        {
            if (Status != GameStatus.Playing) return false;
            var parcel = Board.Get(cell);
            return parcel != null && CanTakeFromYard(parcel);
        }

        /// <summary>
        /// Like <see cref="Tap"/> but ignores blocking and locks; resulting events have Lifted = true and a
        /// one-cell path. A hidden parcel is revealed and a locked one unlocked (its lock is used up) first.
        /// </summary>
        public IReadOnlyList<GameEvent> CraneLift(Cell cell)
        {
            var events = new List<GameEvent>();
            if (!CanCrane(cell)) return events;

            var parcel = Board.Get(cell);
            if (hidden[parcel.Id])
            {
                hidden[parcel.Id] = false;
                hiddenCount--;
                events.Add(new ParcelRevealed(parcel));
            }
            if (IsLocked(parcel))
            {
                lockKey[parcel.Id] = -1;
                events.Add(new ParcelUnlocked(parcel));
            }

            MoveOut(parcel, new List<Cell>(1) { cell }, true, events);
            FinishMove(events);
            return events;
        }

        // ---- Solver support ---------------------------------------------------------------------

        /// <summary>A compact description of the state that matters to the rules, for solvers and tests.</summary>
        public string StateKey()
        {
            var sb = new StringBuilder(Board.Width * Board.Height + 24 + lockedIds.Length);
            for (int y = 0; y < Board.Height; y++)
            for (int x = 0; x < Board.Width; x++)
            {
                var p = Board.Get(new Cell(x, y));
                sb.Append(p == null ? '.' : ParcelColors.ToCode(p.Color));
            }

            var held = new int[ParcelColors.Count];
            for (int i = 0; i < Dock.Capacity; i++)
                if (Dock[i] != null) held[Dock[i].Color]++;
            sb.Append('|');
            foreach (int n in held) sb.Append(n);
            sb.Append('/').Append(Dock.Capacity);

            sb.Append('|').Append(CurrentTruckIndex).Append(':').Append(CurrentTruck?.Loaded ?? 0);

            // Locks change which taps are legal, and a Crane can open one without its key leaving the yard.
            if (lockedIds.Length > 0)
            {
                sb.Append('|');
                foreach (int locked in lockedIds) sb.Append(lockKey[locked] >= 0 ? '1' : '0');
            }
            return sb.ToString();
        }

        // ---- Internals --------------------------------------------------------------------------

        /// <summary>A parcel leaving the yard needs the loading truck to take it, or a free holding slot.</summary>
        bool CanTakeFromYard(Parcel parcel)
        {
            var truck = CurrentTruck;
            return (truck != null && truck.Accepts(parcel)) || !Dock.IsFull;
        }

        void MoveOut(Parcel parcel, List<Cell> path, bool lifted, List<GameEvent> events)
        {
            Board.Remove(parcel.Cell);
            var truck = CurrentTruck;
            if (truck != null && truck.Accepts(parcel))
            {
                events.Add(new ParcelLoaded(parcel, path, truck, truck.Load(), lifted));
                UnlockWithKey(parcel, events);
                if (truck.IsFull) DepartAndRefill(events);
            }
            else
            {
                events.Add(new ParcelHeld(parcel, path, Dock.Add(parcel), lifted));
                holdOrder.Add(parcel);
            }
        }

        /// <summary>Reveals what the move uncovered, then checks for a win or a fail.</summary>
        void FinishMove(List<GameEvent> events)
        {
            RevealFree(events);
            CheckForEnd(events);
        }

        void UnlockWithKey(Parcel key, List<GameEvent> events)
        {
            foreach (int locked in lockedIds)
            {
                if (lockKey[locked] != key.Id) continue;
                lockKey[locked] = -1;
                events.Add(new ParcelUnlocked(parcelsById[locked]));
            }
        }

        /// <summary>Reveals every hidden parcel that is now free. With no event list, reveals silently.</summary>
        void RevealFree(List<GameEvent> events)
        {
            if (hiddenCount == 0) return;
            Board.GetFreeParcels(freeScratch);
            foreach (var p in freeScratch)
            {
                if (!hidden[p.Id]) continue;
                hidden[p.Id] = false;
                hiddenCount--;
                events?.Add(new ParcelRevealed(p));
            }
        }

        void DepartAndRefill(List<GameEvent> events)
        {
            while (CurrentTruck != null && CurrentTruck.IsFull)
            {
                events.Add(new TruckDeparted(CurrentTruck));
                CurrentTruckIndex++;

                var next = CurrentTruck;
                if (next == null) return;
                events.Add(new TruckArrived(next));

                int chain = 0;
                for (int slot = 0; slot < Dock.Capacity && !next.IsFull; slot++)
                {
                    var held = Dock[slot];
                    if (held == null || !next.Accepts(held)) continue;
                    Dock.RemoveAt(slot);
                    holdOrder.Remove(held);
                    events.Add(new ParcelLoadedFromDock(held, slot, next, next.Load(), chain++));
                    UnlockWithKey(held, events);
                }
            }
        }

        void CheckForEnd(List<GameEvent> events)
        {
            if (Board.Count == 0 && Dock.Count == 0)
            {
                Status = GameStatus.Won;
                events.Add(new LevelWon());
            }
            else if (Dock.IsFull ? !HasFreeMatch() : !HasTappableParcel())
            {
                Status = GameStatus.Failed;
                events.Add(new LevelFailed());
            }
        }

        /// <summary>
        /// With room in the dock, any free unlocked parcel can be tapped. Only locks can take that away
        /// (every free parcel padlocked, keys out of reach), and that dead end counts as a fail.
        /// </summary>
        bool HasTappableParcel()
        {
            if (Board.Count == 0 || lockedIds.Length == 0) return true;
            Board.GetFreeParcels(freeScratch);
            foreach (var p in freeScratch)
                if (!IsLocked(p)) return true;
            return false;
        }
    }
}
