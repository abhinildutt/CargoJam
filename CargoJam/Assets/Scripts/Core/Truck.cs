namespace CargoJam.Core
{
    public sealed class Truck
    {
        /// <summary>Position in the level's truck order, starting at 0.</summary>
        public int Index { get; }
        public int Color { get; }
        public int Capacity { get; }
        public int Loaded { get; private set; }

        public bool IsFull => Loaded >= Capacity;

        public Truck(int index, int color, int capacity)
        {
            Index = index;
            Color = color;
            Capacity = capacity;
        }

        public bool Accepts(Parcel parcel) => !IsFull && parcel.Color == Color;

        /// <summary>Returns the bed slot the parcel went into.</summary>
        internal int Load() => Loaded++;

        internal Truck Copy() => new Truck(Index, Color, Capacity) { Loaded = Loaded };
    }
}
