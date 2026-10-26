using System.Collections.Generic;

namespace CargoJam.Core
{
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

        public ParcelLoaded(Parcel parcel, IReadOnlyList<Cell> path, Truck truck, int truckSlot)
        {
            Parcel = parcel;
            Path = path;
            Truck = truck;
            TruckSlot = truckSlot;
        }
    }

    /// <summary>A parcel left the yard along <see cref="Path"/> into a holding slot.</summary>
    public sealed class ParcelHeld : GameEvent
    {
        public Parcel Parcel { get; }
        public IReadOnlyList<Cell> Path { get; }
        public int DockSlot { get; }

        public ParcelHeld(Parcel parcel, IReadOnlyList<Cell> path, int dockSlot)
        {
            Parcel = parcel;
            Path = path;
            DockSlot = dockSlot;
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

    public sealed class LevelWon : GameEvent { }

    /// <summary>The holding dock is full and no free parcel matches the loading truck.</summary>
    public sealed class LevelFailed : GameEvent { }
}
