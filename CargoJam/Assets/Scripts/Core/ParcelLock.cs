namespace CargoJam.Core
{
    /// <summary>A padlock from the level file: the parcel at <see cref="Locked"/> waits for the one at <see cref="Key"/>.</summary>
    public readonly struct ParcelLock
    {
        /// <summary>The padlocked parcel's yard cell.</summary>
        public readonly Cell Locked;

        /// <summary>The key parcel's yard cell. Loading that parcel into a truck opens the lock.</summary>
        public readonly Cell Key;

        public ParcelLock(Cell locked, Cell key)
        {
            Locked = locked;
            Key = key;
        }

        public override string ToString() => $"lock {Locked} key {Key}";
    }
}
