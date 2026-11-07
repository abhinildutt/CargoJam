using System.Collections.Generic;

namespace CargoJam.Core
{
    /// <summary>Why a level was lost (<see cref="LevelFailed.Reason"/>, <see cref="GameSession.FailReason"/>).</summary>
    public enum FailReason
    {
        /// <summary>Not failed.</summary>
        None,
        /// <summary>No legal move, but one more holding slot would allow one: Continue is offered.</summary>
        OutOfSpace,
        /// <summary>No legal move, and another slot wouldn't help (e.g. every free parcel is restrained).</summary>
        Stuck,
        /// <summary>An express parcel ran out of moves while still in the yard.</summary>
        ExpressMissed,
    }

    /// <summary>
    /// What happened during a tap, in order. The session applies its rules instantly;
    /// views replay these events as animations.
    /// </summary>
    public abstract class GameEvent { }

    /// <summary>The tapped parcel has no path to the dock. Nothing changed.</summary>
    public sealed class ParcelBlocked : GameEvent
    {
        public Parcel Parcel { get; }
        public ParcelBlocked(Parcel parcel) { Parcel = parcel; }
    }

    /// <summary>The tapped parcel is free but doesn't match and every holding slot is taken. Nothing changed.</summary>
    public sealed class DockFullRejected : GameEvent
    {
        public Parcel Parcel { get; }
        public DockFullRejected(Parcel parcel) { Parcel = parcel; }
    }

    /// <summary>A parcel left the yard along <see cref="Path"/> straight into the loading truck.</summary>
    public sealed class ParcelLoaded : GameEvent
    {
        public Parcel Parcel { get; }
        public IReadOnlyList<Cell> Path { get; }
        public Truck Truck { get; }
        public int TruckSlot { get; }

        /// <summary>True when the Crane booster lifted it straight up instead of sliding along a path.</summary>
        public bool Lifted { get; }

        public ParcelLoaded(Parcel parcel, IReadOnlyList<Cell> path, Truck truck, int truckSlot, bool lifted = false)
        {
            Parcel = parcel;
            Path = path;
            Truck = truck;
            TruckSlot = truckSlot;
            Lifted = lifted;
        }
    }

    /// <summary>A parcel left the yard along <see cref="Path"/> into a holding slot.</summary>
    public sealed class ParcelHeld : GameEvent
    {
        public Parcel Parcel { get; }
        public IReadOnlyList<Cell> Path { get; }
        public int DockSlot { get; }

        /// <summary>True when the Crane booster lifted it straight up instead of sliding along a path.</summary>
        public bool Lifted { get; }

        public ParcelHeld(Parcel parcel, IReadOnlyList<Cell> path, int dockSlot, bool lifted = false)
        {
            Parcel = parcel;
            Path = path;
            DockSlot = dockSlot;
            Lifted = lifted;
        }
    }

    /// <summary>A held parcel jumped into a newly arrived truck. <see cref="ChainIndex"/> counts up within one arrival.</summary>
    public sealed class ParcelLoadedFromDock : GameEvent
    {
        public Parcel Parcel { get; }
        public int DockSlot { get; }
        public Truck Truck { get; }
        public int TruckSlot { get; }
        public int ChainIndex { get; }

        public ParcelLoadedFromDock(Parcel parcel, int dockSlot, Truck truck, int truckSlot, int chainIndex)
        {
            Parcel = parcel;
            DockSlot = dockSlot;
            Truck = truck;
            TruckSlot = truckSlot;
            ChainIndex = chainIndex;
        }
    }

    public sealed class TruckDeparted : GameEvent
    {
        public Truck Truck { get; }
        public TruckDeparted(Truck truck) { Truck = truck; }
    }

    public sealed class TruckArrived : GameEvent
    {
        public Truck Truck { get; }
        public TruckArrived(Truck truck) { Truck = truck; }
    }

    /// <summary>Undo: a held parcel left <see cref="DockSlot"/> and is back on its yard cell.</summary>
    public sealed class ParcelReturned : GameEvent
    {
        public Parcel Parcel { get; }
        public int DockSlot { get; }
        public ParcelReturned(Parcel parcel, int dockSlot) { Parcel = parcel; DockSlot = dockSlot; }
    }

    /// <summary>Extra Slot: the holding dock grew to <see cref="Capacity"/> slots.</summary>
    public sealed class DockExpanded : GameEvent
    {
        public int Capacity { get; }
        public DockExpanded(int capacity) { Capacity = capacity; }
    }

    /// <summary>A mystery parcel became free and its color is now known.</summary>
    public sealed class ParcelRevealed : GameEvent
    {
        public Parcel Parcel { get; }
        public ParcelRevealed(Parcel parcel) { Parcel = parcel; }
    }

    /// <summary>A locked parcel's key was loaded, so it can now be tapped.</summary>
    public sealed class ParcelUnlocked : GameEvent
    {
        public Parcel Parcel { get; }
        public ParcelUnlocked(Parcel parcel) { Parcel = parcel; }
    }

    /// <summary>
    /// A move went by while a crate was frozen: <see cref="Left"/> moves remain until it thaws, and at 0
    /// it has thawed (free from the next move on). With <see cref="Lifted"/> the Crane thawed it outright
    /// just before lifting it (then <see cref="Left"/> is 0).
    /// </summary>
    public sealed class FrostCracked : GameEvent
    {
        public Parcel Parcel { get; }
        public int Left { get; }
        public bool Lifted { get; }

        public FrostCracked(Parcel parcel, int left, bool lifted = false)
        {
            Parcel = parcel;
            Left = left;
            Lifted = lifted;
        }
    }

    /// <summary>A move went by while an express parcel was still in the yard; <see cref="Left"/> moves remain.</summary>
    public sealed class ExpressTicked : GameEvent
    {
        public Parcel Parcel { get; }
        public int Left { get; }
        public ExpressTicked(Parcel parcel, int left) { Parcel = parcel; Left = left; }
    }

    /// <summary>An express parcel ran out of moves in the yard. A <see cref="LevelFailed"/> follows.</summary>
    public sealed class ExpressMissed : GameEvent
    {
        public Parcel Parcel { get; }
        public ExpressMissed(Parcel parcel) { Parcel = parcel; }
    }

    /// <summary>A strapped pair left the yard together: <see cref="Lead"/> (the free member) first, then <see cref="Trail"/>.</summary>
    public sealed class StrapMoved : GameEvent
    {
        public Parcel Lead { get; }
        public Parcel Trail { get; }
        public StrapMoved(Parcel lead, Parcel trail) { Lead = lead; Trail = trail; }
    }

    /// <summary>The Crane cut a strap: <see cref="Lifted"/> leaves alone and <see cref="Partner"/> stays, unstrapped.</summary>
    public sealed class StrapCut : GameEvent
    {
        public Parcel Lifted { get; }
        public Parcel Partner { get; }
        public StrapCut(Parcel lifted, Parcel partner) { Lifted = lifted; Partner = partner; }
    }

    /// <summary>Undo returned a pair held together, and their strap is back.</summary>
    public sealed class StrapRestored : GameEvent
    {
        public Parcel A { get; }
        public Parcel B { get; }
        public StrapRestored(Parcel a, Parcel b) { A = a; B = b; }
    }

    /// <summary>A load of the gate's color counted gate <see cref="Gate"/> (an index into <see cref="LevelDefinition.Gates"/>) down.</summary>
    public sealed class GateProgress : GameEvent
    {
        public int Gate { get; }
        public int Left { get; }
        public Parcel Loaded { get; }

        public GateProgress(int gate, int left, Parcel loaded)
        {
            Gate = gate;
            Left = left;
            Loaded = loaded;
        }
    }

    /// <summary>Gate <see cref="Gate"/> reached 0 and opened; its cell is now empty for good.</summary>
    public sealed class GateOpened : GameEvent
    {
        public int Gate { get; }
        public GateOpened(int gate) { Gate = gate; }
    }

    public sealed class LevelWon : GameEvent { }

    /// <summary>The level is lost; <see cref="Reason"/> says why.</summary>
    public sealed class LevelFailed : GameEvent
    {
        public FailReason Reason { get; }
        public LevelFailed(FailReason reason = FailReason.OutOfSpace) { Reason = reason; }
    }
}
