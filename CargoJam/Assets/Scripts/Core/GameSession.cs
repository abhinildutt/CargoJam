using System;
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
        // Express parcels that ran out during the move being finished; CheckForEnd fails on them.
        readonly List<Parcel> missed = new List<Parcel>();

        // Parcels never change, so clones share these; ids index the per-parcel state arrays below.
        readonly Parcel[] parcelsById;
        readonly int[] lockedIds;
        readonly int[] frozenIds;   // parcels that start frozen, by id
        readonly int[] expressIds;  // express parcels, by id
        readonly int[] strapOf;     // per parcel id: its index in Level.Straps, or -1
        readonly int[] strapIds;    // per strap s: the member ids at 2s and 2s + 1

        // Per parcel id: still under a tarp; the key parcel's id while padlocked, else -1; moves left until
        // the ice thaws (0 = not frozen); moves left while express (0 = not express); held as part of a
        // strapped pair (for Undo).
        readonly bool[] hidden;
        readonly int[] lockKey;
        readonly int[] frost;
        readonly int[] expressLeft;
        readonly bool[] heldAsPair;
        int hiddenCount;

        // Per strap: still strapped. Per gate: loads still needed, 0 once open.
        readonly bool[] strapIntact;
        readonly int[] gateLeft;

        // Held parcels in the order they were held, for Undo. Parcels drop out when they leave the dock.
        readonly List<Parcel> holdOrder;

        public LevelDefinition Level { get; }
        public Board Board { get; }
        public HoldingDock Dock { get; }
        public IReadOnlyList<Truck> Trucks => trucks;
        public int CurrentTruckIndex { get; private set; }
        public GameStatus Status { get; private set; }

        /// <summary>Why the level was lost, or <see cref="Core.FailReason.None"/> while it isn't.</summary>
        public FailReason FailReason { get; private set; }

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
                if (level.IsBollard(x, y)) Board.SetObstacle(new Cell(x, y), true);
                int color = level.ColorAt(x, y);
                if (color == ParcelColors.None) continue;
                var parcel = new Parcel(parcels.Count, color, new Cell(x, y));
                idAt[y * level.Width + x] = parcel.Id;
                parcels.Add(parcel);
                Board.Place(parcel);
            }
            parcelsById = parcels.ToArray();
            int count = parcelsById.Length;

            hidden = new bool[count];
            frost = new int[count];
            expressLeft = new int[count];
            heldAsPair = new bool[count];
            var frozen = new List<int>();
            var express = new List<int>();
            foreach (var p in parcelsById)
            {
                if (level.IsMystery(p.Cell.X, p.Cell.Y))
                {
                    hidden[p.Id] = true;
                    hiddenCount++;
                }
                frost[p.Id] = level.FrostAt(p.Cell.X, p.Cell.Y);
                if (frost[p.Id] > 0) frozen.Add(p.Id);
                expressLeft[p.Id] = level.ExpressAt(p.Cell.X, p.Cell.Y);
                if (expressLeft[p.Id] > 0) express.Add(p.Id);
            }
            frozenIds = frozen.ToArray();
            expressIds = express.ToArray();

            lockKey = new int[count];
            for (int i = 0; i < lockKey.Length; i++) lockKey[i] = -1;
            lockedIds = new int[level.Locks.Count];
            for (int i = 0; i < level.Locks.Count; i++)
            {
                var l = level.Locks[i];
                int locked = idAt[l.Locked.Y * level.Width + l.Locked.X];
                lockedIds[i] = locked;
                lockKey[locked] = idAt[l.Key.Y * level.Width + l.Key.X];
            }

            strapOf = new int[count];
            for (int i = 0; i < strapOf.Length; i++) strapOf[i] = -1;
            strapIds = new int[level.Straps.Count * 2];
            strapIntact = new bool[level.Straps.Count];
            for (int s = 0; s < level.Straps.Count; s++)
            {
                var strap = level.Straps[s];
                int a = idAt[strap.A.Y * level.Width + strap.A.X];
                int b = idAt[strap.B.Y * level.Width + strap.B.X];
                strapIds[2 * s] = a;
                strapIds[2 * s + 1] = b;
                strapOf[a] = s;
                strapOf[b] = s;
                strapIntact[s] = true;
            }

            gateLeft = new int[level.Gates.Count];
            for (int g = 0; g < gateLeft.Length; g++)
            {
                gateLeft[g] = level.Gates[g].Count;
                Board.SetObstacle(level.Gates[g].Cell, true);
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
            Board.CopyObstaclesFrom(source.Board);
            foreach (var p in source.Board.Parcels) Board.Place(p);
            Dock = new HoldingDock(source.Dock.Capacity);
            for (int i = 0; i < source.Dock.Capacity; i++)
                if (source.Dock[i] != null) Dock.PlaceAt(i, source.Dock[i]);
            trucks = new List<Truck>(source.trucks.Count);
            foreach (var t in source.trucks) trucks.Add(t.Copy());
            CurrentTruckIndex = source.CurrentTruckIndex;
            Status = source.Status;
            FailReason = source.FailReason;

            parcelsById = source.parcelsById;
            lockedIds = source.lockedIds;
            frozenIds = source.frozenIds;
            expressIds = source.expressIds;
            strapOf = source.strapOf;
            strapIds = source.strapIds;

            hidden = (bool[])source.hidden.Clone();
            lockKey = (int[])source.lockKey.Clone();
            frost = (int[])source.frost.Clone();
            expressLeft = (int[])source.expressLeft.Clone();
            heldAsPair = (bool[])source.heldAsPair.Clone();
            hiddenCount = source.hiddenCount;
            strapIntact = (bool[])source.strapIntact.Clone();
            gateLeft = (int[])source.gateLeft.Clone();
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

        /// <summary>True while something holds the parcel in place even when it has a path: a padlock or ice.</summary>
        public bool IsRestrained(Parcel parcel) => lockKey[parcel.Id] >= 0 || frost[parcel.Id] > 0;

        /// <summary>
        /// Moves left until a frozen crate thaws, or 0 when it isn't (or is no longer) frozen. Every move
        /// that takes something out of the yard counts it down, the same moves that tick express parcels;
        /// at 0 it is an ordinary parcel from the next move on. Undo never refreezes it or refunds a move.
        /// </summary>
        public int FrostLeft(Parcel parcel) => frost[parcel.Id];

        /// <summary>
        /// True for a parcel the level marks express, whatever its count. Tells a missed express parcel
        /// (<see cref="ExpressLeft"/> 0) from a plain one.
        /// </summary>
        public bool IsExpress(Parcel parcel) => Level.ExpressAt(parcel.Cell.X, parcel.Cell.Y) > 0;

        /// <summary>
        /// Moves left for an express parcel, counting the one that ships it; 0 for a parcel that isn't
        /// express (or one that was missed, see <see cref="IsExpress"/>). Once the parcel leaves the yard
        /// the count stops changing; Undo never refunds it.
        /// </summary>
        public int ExpressLeft(Parcel parcel) => expressLeft[parcel.Id];

        /// <summary>
        /// The parcel strapped to this one right now, or null when it isn't strapped: never was, the pair
        /// has left the yard (Undo of a pair held together straps it again), or the Crane cut the strap.
        /// </summary>
        public Parcel StrapPartner(Parcel parcel)
        {
            int s = strapOf[parcel.Id];
            return s >= 0 && strapIntact[s] ? parcelsById[OtherMember(s, parcel.Id)] : null;
        }

        /// <summary>The parcel's strap as an index into <see cref="LevelDefinition.Straps"/>, intact or not; -1 when none.</summary>
        public int StrapIndex(Parcel parcel) => strapOf[parcel.Id];

        /// <summary>Loads still needed to open gate <paramref name="gate"/> (an index into <see cref="LevelDefinition.Gates"/>); 0 once open.</summary>
        public int GateLeft(int gate) => gateLeft[gate];

        /// <summary>True once gate <paramref name="gate"/> has opened (gates never close again).</summary>
        public bool IsGateOpen(int gate) => gateLeft[gate] == 0;

        // ---- Moves ------------------------------------------------------------------------------

        /// <summary>True when the parcel, or its strap partner, has a path out of the yard.</summary>
        public bool IsUnitFree(Parcel parcel)
        {
            if (Board.IsFree(parcel.Cell)) return true;
            var partner = StrapPartner(parcel);
            return partner != null && Board.IsFree(partner.Cell);
        }

        /// <summary>
        /// Fills <paramref name="result"/> with one parcel per unit that can be tapped now: free and not
        /// restrained. For a strapped pair this is its free member, listed once. Whether the truck or
        /// dock can take it is <see cref="CanTake"/>'s question.
        /// </summary>
        public void GetMoves(List<Parcel> result)
        {
            Board.GetFreeParcels(result);
            int kept = 0;
            for (int i = 0; i < result.Count; i++)
            {
                var p = result[i];
                if (IsRestrained(p)) continue;
                // Both members free: the one listed first stands for the pair.
                var partner = StrapPartner(p);
                if (partner != null && result.IndexOf(partner, 0, kept) >= 0) continue;
                result[kept++] = p;
            }
            result.RemoveRange(kept, result.Count - kept);
        }

        /// <summary>
        /// True when the parcel's unit can leave the yard now. A single parcel needs the loading truck to
        /// accept it or a free holding slot; a strapped pair needs two places between the truck (when its
        /// color matches) and the dock. <paramref name="extraSlots"/> pretends the dock is that much bigger
        /// (to ask whether Extra Slot would help).
        /// </summary>
        public bool CanTake(Parcel parcel, int extraSlots = 0)
        {
            if (StrapPartner(parcel) == null) return CanTakeOne(parcel, extraSlots);
            var truck = CurrentTruck;
            int room = truck != null && truck.Color == parcel.Color ? truck.Capacity - truck.Loaded : 0;
            return Math.Min(room, 2) + FreeSlots(extraSlots) >= 2;
        }

        /// <summary>True when some unit from <see cref="GetMoves"/> passes <see cref="CanTake"/>.</summary>
        public bool HasLegalMove() => AnyMove(0);

        /// <summary>True when some tappable unit would go straight into the loading truck.</summary>
        public bool HasFreeMatch()
        {
            var truck = CurrentTruck;
            if (truck == null) return false;
            GetMoves(freeScratch);
            foreach (var p in freeScratch)
                if (truck.Accepts(p)) return true;
            return false;
        }

        // ---- Taps -------------------------------------------------------------------------------

        public IReadOnlyList<GameEvent> Tap(Cell cell)
        {
            var events = new List<GameEvent>();
            if (Status != GameStatus.Playing) return events;

            var parcel = Board.Get(cell);
            if (parcel == null) return events;

            // A strapped pair leaves together, led by a member with a path: the tapped one if it has
            // one, else its partner. (Strapped parcels are never restrained.)
            var lead = parcel;
            var trail = StrapPartner(parcel);
            var path = IsRestrained(parcel) ? null : Board.FindExitPath(cell);
            if (path == null && trail != null)
            {
                path = Board.FindExitPath(trail.Cell);
                if (path != null)
                {
                    lead = trail;
                    trail = parcel;
                }
            }
            if (path == null)
            {
                events.Add(new ParcelBlocked(parcel));
                return events;
            }

            if (!CanTake(lead))
            {
                events.Add(new DockFullRejected(parcel));
                return events;
            }

            if (trail == null)
            {
                MoveOut(lead, path, false, false, events);
            }
            else
            {
                // The trail follows the lead: its own cell, then the lead's path.
                strapIntact[strapOf[lead.Id]] = false;
                events.Add(new StrapMoved(lead, trail));
                var trailPath = new List<Cell>(path.Count + 1) { trail.Cell };
                trailPath.AddRange(path);
                MoveOut(lead, path, false, true, events);
                MoveOut(trail, trailPath, false, true, events);
            }
            FinishMove(events, true);
            return events;
        }

        // ---- Boosters ---------------------------------------------------------------------------

        /// <summary>Undo is possible while playing with at least one parcel held.</summary>
        public bool CanUndo => Status == GameStatus.Playing && holdOrder.Count > 0;

        /// <summary>
        /// Sends the most recently held parcel still in the dock back to its yard cell
        /// (<see cref="ParcelReturned"/>). Cells only ever empty, so the cell is always free to take it.
        /// If that parcel was held together with its strap partner, which was held just before it and is
        /// still in the dock, both return and the strap is restored (<see cref="StrapRestored"/>).
        /// Nothing else is undone: thaw and express counts stay spent, gates stay open.
        /// </summary>
        public IReadOnlyList<GameEvent> Undo()
        {
            var events = new List<GameEvent>();
            if (!CanUndo) return events;

            var parcel = holdOrder[holdOrder.Count - 1];
            holdOrder.RemoveAt(holdOrder.Count - 1);
            bool pair = heldAsPair[parcel.Id];
            ReturnToYard(parcel, events);

            int s = strapOf[parcel.Id];
            if (pair && holdOrder.Count > 0)
            {
                var partner = parcelsById[OtherMember(s, parcel.Id)];
                if (holdOrder[holdOrder.Count - 1] == partner && heldAsPair[partner.Id])
                {
                    holdOrder.RemoveAt(holdOrder.Count - 1);
                    ReturnToYard(partner, events);
                    strapIntact[s] = true;
                    events.Add(new StrapRestored(parcel, partner));
                }
            }

            FinishMove(events, false);
            return events;
        }

        /// <summary>
        /// True while the dock is below <see cref="MaxHoldingSlots"/> and the level is being played, or
        /// was lost <see cref="Core.FailReason.OutOfSpace"/> (the only fail a slot can fix).
        /// </summary>
        public bool CanAddHoldingSlot =>
            (Status == GameStatus.Playing || (Status == GameStatus.Failed && FailReason == FailReason.OutOfSpace))
            && Dock.Capacity < MaxHoldingSlots;

        /// <summary>
        /// Adds a holding slot (<see cref="DockExpanded"/>). Also the fail "Continue": called after an
        /// <see cref="Core.FailReason.OutOfSpace"/> fail, it resumes play. Never ticks express parcels or
        /// frozen crates.
        /// </summary>
        public IReadOnlyList<GameEvent> AddHoldingSlot()
        {
            var events = new List<GameEvent>();
            if (!CanAddHoldingSlot) return events;

            Dock.AddSlot();
            events.Add(new DockExpanded(Dock.Capacity));
            Status = GameStatus.Playing;
            FailReason = FailReason.None;
            FinishMove(events, false);
            return events;
        }

        /// <summary>
        /// The Crane can lift any parcel on the board (blocked, hidden, locked, frozen or strapped) if the
        /// truck or dock can take that one parcel. Bollards and gates hold no parcel, so never qualify.
        /// </summary>
        public bool CanCrane(Cell cell)
        {
            if (Status != GameStatus.Playing) return false;
            var parcel = Board.Get(cell);
            return parcel != null && CanTakeOne(parcel, 0);
        }

        /// <summary>
        /// Like <see cref="Tap"/> but ignores blocking and restraints; resulting events have Lifted = true
        /// and a one-cell path. First a hidden parcel is revealed, a locked one unlocked (its lock is used
        /// up), a frozen one thawed outright, and a strap cut (only the lifted parcel leaves). It counts
        /// as a move, so express parcels and other frozen crates tick.
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
            if (frost[parcel.Id] > 0)
            {
                frost[parcel.Id] = 0;
                events.Add(new FrostCracked(parcel, 0, true));
            }
            var partner = StrapPartner(parcel);
            if (partner != null)
            {
                strapIntact[strapOf[parcel.Id]] = false;
                events.Add(new StrapCut(parcel, partner));
            }

            MoveOut(parcel, new List<Cell>(1) { cell }, true, false, events);
            FinishMove(events, true);
            return events;
        }

        // ---- Solver support ---------------------------------------------------------------------

        /// <summary>A compact description of the state that matters to the rules, for solvers and tests.</summary>
        public string StateKey()
        {
            var sb = new StringBuilder(Board.Width * Board.Height + 24 + lockedIds.Length
                + frozenIds.Length + expressIds.Length * 3 + gateLeft.Length * 2 + strapIntact.Length);
            for (int y = 0; y < Board.Height; y++)
            for (int x = 0; x < Board.Width; x++)
            {
                var c = new Cell(x, y);
                var p = Board.Get(c);
                // Bollards and closed gates are walls; an open gate is just an empty cell.
                sb.Append(Board.IsObstacle(c) ? '#' : p == null ? '.' : ParcelColors.ToCode(p.Color));
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
            if (frozenIds.Length > 0)
            {
                // One digit per crate: thaw counts run 0..9.
                sb.Append("|F");
                foreach (int id in frozenIds) sb.Append((char)('0' + frost[id]));
            }
            if (expressIds.Length > 0)
            {
                // Once out of the yard an express parcel is safe, so its count no longer matters.
                sb.Append("|E");
                foreach (int id in expressIds)
                {
                    if (InYard(parcelsById[id])) sb.Append(expressLeft[id]);
                    else sb.Append('-');
                    sb.Append(',');
                }
            }
            if (gateLeft.Length > 0)
            {
                sb.Append("|G");
                foreach (int n in gateLeft) sb.Append(n).Append(',');
            }
            if (strapIntact.Length > 0)
            {
                sb.Append("|S");
                foreach (bool intact in strapIntact) sb.Append(intact ? '1' : '0');
            }
            return sb.ToString();
        }

        // ---- Internals --------------------------------------------------------------------------

        int OtherMember(int strap, int id) => strapIds[2 * strap] == id ? strapIds[2 * strap + 1] : strapIds[2 * strap];

        bool InYard(Parcel parcel) => Board.Get(parcel.Cell) == parcel;

        int FreeSlots(int extraSlots) => Dock.Capacity - Dock.Count + extraSlots;

        /// <summary>One parcel leaving the yard needs the loading truck to take it, or a free holding slot.</summary>
        bool CanTakeOne(Parcel parcel, int extraSlots)
        {
            var truck = CurrentTruck;
            return (truck != null && truck.Accepts(parcel)) || FreeSlots(extraSlots) >= 1;
        }

        bool AnyMove(int extraSlots)
        {
            GetMoves(freeScratch);
            foreach (var p in freeScratch)
                if (CanTake(p, extraSlots)) return true;
            return false;
        }

        /// <summary>
        /// Takes one parcel out of the yard: into the loading truck if it accepts it, else into the dock.
        /// <paramref name="pair"/> marks a strapped pair's member, for Undo.
        /// </summary>
        void MoveOut(Parcel parcel, List<Cell> path, bool lifted, bool pair, List<GameEvent> events)
        {
            Board.Remove(parcel.Cell);
            var truck = CurrentTruck;
            if (truck != null && truck.Accepts(parcel))
            {
                events.Add(new ParcelLoaded(parcel, path, truck, truck.Load(), lifted));
                OnLoaded(parcel, events);
                if (truck.IsFull) DepartAndRefill(events);
            }
            else
            {
                events.Add(new ParcelHeld(parcel, path, Dock.Add(parcel), lifted));
                holdOrder.Add(parcel);
                heldAsPair[parcel.Id] = pair;
            }
        }

        void ReturnToYard(Parcel parcel, List<GameEvent> events)
        {
            int slot = Dock.IndexOf(parcel);
            Dock.RemoveAt(slot);
            Board.Place(parcel);
            heldAsPair[parcel.Id] = false;
            events.Add(new ParcelReturned(parcel, slot));
        }

        /// <summary>
        /// Ends a move: ticks express parcels, then frozen crates (only for moves that took something out
        /// of the yard), reveals what the move uncovered, then checks for a win or a fail. A crate that
        /// thaws here is free for the next move, so the end check already counts it as a legal move.
        /// </summary>
        void FinishMove(List<GameEvent> events, bool tick)
        {
            missed.Clear();
            if (tick)
            {
                TickExpress(events);
                TickFrost(events);
            }
            RevealFree(events);
            CheckForEnd(events);
        }

        /// <summary>A parcel went into a truck (from the yard or the dock): it may be a key, and it counts for gates.</summary>
        void OnLoaded(Parcel parcel, List<GameEvent> events)
        {
            UnlockWithKey(parcel, events);
            for (int g = 0; g < gateLeft.Length; g++)
            {
                if (gateLeft[g] == 0 || Level.Gates[g].Color != parcel.Color) continue;
                gateLeft[g]--;
                events.Add(new GateProgress(g, gateLeft[g], parcel));
                if (gateLeft[g] > 0) continue;
                Board.SetObstacle(Level.Gates[g].Cell, false);
                events.Add(new GateOpened(g));
            }
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

        /// <summary>One move went by: every express parcel still in the yard loses one; at 0 it is missed.</summary>
        void TickExpress(List<GameEvent> events)
        {
            foreach (int id in expressIds)
            {
                var p = parcelsById[id];
                if (expressLeft[id] == 0 || !InYard(p)) continue;
                expressLeft[id]--;
                events.Add(new ExpressTicked(p, expressLeft[id]));
                if (expressLeft[id] == 0) missed.Add(p);
            }
        }

        /// <summary>One move went by: every frozen crate (all still in the yard) is a move closer to thawing; at 0 it thaws.</summary>
        void TickFrost(List<GameEvent> events)
        {
            foreach (int id in frozenIds)
            {
                if (frost[id] == 0) continue;
                frost[id]--;
                events.Add(new FrostCracked(parcelsById[id], frost[id], false));
            }
        }

        /// <summary>Reveals every hidden parcel that is now free (frozen or not). With no event list, reveals silently.</summary>
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
                    OnLoaded(held, events);
                }
            }
        }

        /// <summary>
        /// Won when the board and dock are empty. Otherwise a missed express parcel fails the level; failing
        /// that, so does having no legal move: <see cref="Core.FailReason.OutOfSpace"/> when one more slot
        /// would allow one (Continue is offered), else <see cref="Core.FailReason.Stuck"/>.
        /// </summary>
        void CheckForEnd(List<GameEvent> events)
        {
            if (Board.Count == 0 && Dock.Count == 0)
            {
                Status = GameStatus.Won;
                events.Add(new LevelWon());
            }
            else if (missed.Count > 0)
            {
                foreach (var p in missed) events.Add(new ExpressMissed(p));
                Fail(FailReason.ExpressMissed, events);
            }
            else if (!AnyMove(0))
            {
                Fail(AnyMove(1) ? FailReason.OutOfSpace : FailReason.Stuck, events);
            }
        }

        void Fail(FailReason reason, List<GameEvent> events)
        {
            Status = GameStatus.Failed;
            FailReason = reason;
            events.Add(new LevelFailed(reason));
        }
    }
}
