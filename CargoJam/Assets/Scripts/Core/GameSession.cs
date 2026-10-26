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
    /// The rules of Cargo Jam. Deterministic and engine-free: every <see cref="Tap"/> updates state
    /// immediately and returns what happened, so views can animate while input stays live.
    /// </summary>
    public sealed class GameSession
    {
        readonly List<Truck> trucks;
        readonly List<Parcel> freeScratch = new List<Parcel>();

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

            int id = 0;
            for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                int color = level.ColorAt(x, y);
                if (color != ParcelColors.None) Board.Place(new Parcel(id++, color, new Cell(x, y)));
            }

            for (int i = 0; i < level.Trucks.Count; i++)
                trucks.Add(new Truck(i, level.Trucks[i], level.TruckCapacity));
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
        }

        public GameSession Clone() => new GameSession(this);

        public IReadOnlyList<GameEvent> Tap(Cell cell)
        {
            var events = new List<GameEvent>();
            if (Status != GameStatus.Playing) return events;

            var parcel = Board.Get(cell);
            if (parcel == null) return events;

            var path = Board.FindExitPath(cell);
            if (path == null)
            {
                events.Add(new ParcelBlocked(parcel));
                return events;
            }

            var truck = CurrentTruck;
            if (truck != null && truck.Accepts(parcel))
            {
                Board.Remove(cell);
                events.Add(new ParcelLoaded(parcel, path, truck, truck.Load()));
                if (truck.IsFull) DepartAndRefill(events);
            }
            else
            {
                if (Dock.IsFull)
                {
                    events.Add(new DockFullRejected(parcel));
                    return events;
                }
                Board.Remove(cell);
                events.Add(new ParcelHeld(parcel, path, Dock.Add(parcel)));
            }

            CheckForEnd(events);
            return events;
        }

        /// <summary>True when some tappable parcel would go straight into the loading truck.</summary>
        public bool HasFreeMatch()
        {
            var truck = CurrentTruck;
            if (truck == null) return false;
            Board.GetFreeParcels(freeScratch);
            foreach (var p in freeScratch)
                if (truck.Accepts(p)) return true;
            return false;
        }

        /// <summary>A compact description of the state that matters to the rules, for solvers and tests.</summary>
        public string StateKey()
        {
            var sb = new StringBuilder(Board.Width * Board.Height + 16);
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

            sb.Append('|').Append(CurrentTruckIndex).Append(':').Append(CurrentTruck?.Loaded ?? 0);
            return sb.ToString();
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
                    events.Add(new ParcelLoadedFromDock(held, slot, next, next.Load(), chain++));
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
            else if (Dock.IsFull && !HasFreeMatch())
            {
                Status = GameStatus.Failed;
                events.Add(new LevelFailed());
            }
        }
    }
}
