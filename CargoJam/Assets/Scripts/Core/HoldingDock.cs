namespace CargoJam.Core
{
    /// <summary>
    /// Slots where non-matching parcels wait. Parcels keep their slot until they leave. The dock can
    /// grow (Extra Slot) but never shrinks, so slot indexes stay stable.
    /// </summary>
    public sealed class HoldingDock
    {
        Parcel[] slots;

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

        /// <summary>Adds one empty slot at the end. Existing parcels keep their slot indexes.</summary>
        internal void AddSlot()
        {
            System.Array.Resize(ref slots, slots.Length + 1);
        }

        /// <summary>The slot holding <paramref name="parcel"/>, or -1 when it isn't held.</summary>
        internal int IndexOf(Parcel parcel)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == parcel) return i;
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
