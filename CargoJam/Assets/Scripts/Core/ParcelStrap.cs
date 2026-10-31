namespace CargoJam.Core
{
    /// <summary>A strap from the level file: the parcels at <see cref="A"/> and <see cref="B"/> travel together.</summary>
    public readonly struct ParcelStrap
    {
        /// <summary>One strapped parcel's yard cell.</summary>
        public readonly Cell A;

        /// <summary>The other strapped parcel's yard cell, orthogonally next to <see cref="A"/>.</summary>
        public readonly Cell B;

        public ParcelStrap(Cell a, Cell b)
        {
            A = a;
            B = b;
        }

        public override string ToString() => $"strap {A} {B}";
    }
}
