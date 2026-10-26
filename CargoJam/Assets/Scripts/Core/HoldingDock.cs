namespace CargoJam.Core
{
    /// <summary>Fixed slots where non-matching parcels wait. Parcels keep their slot until they leave.</summary>
    public sealed class HoldingDock
    {
        readonly Parcel[] slots;

        public int Capacity => slots.Length;
        public int Count { get; private set; }
        public bool IsFull => Count == slots.Length;

        public HoldingDock(int capacity)
        {
            slots = new Parcel[capacity];
        }

        public Parcel this[int slot] => slots[slot];

        /// <summary>Puts the parcel in the first empty slot and returns that slot, or -1 when full.</summary>
        internal int Add(Parcel parcel)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) continue;
                slots[i] = parcel;
                Count++;
                return i;
            }
            return -1;
        }

        internal void PlaceAt(int slot, Parcel parcel)
        {
            if (slots[slot] == null) Count++;
            slots[slot] = parcel;
        }

        internal void RemoveAt(int slot)
        {
            if (slots[slot] == null) return;
            slots[slot] = null;
            Count--;
        }
    }
}
