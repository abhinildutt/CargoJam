namespace CargoJam.Core
{
    /// <summary>
    /// A customs gate from the level file: the obstacle at <see cref="Cell"/> opens once
    /// <see cref="Count"/> parcels of <see cref="Color"/> have been loaded into trucks.
    /// </summary>
    public readonly struct ParcelGate
    {
        /// <summary>The gate's yard cell ('+' in the file).</summary>
        public readonly Cell Cell;

        /// <summary>The parcel color (see <see cref="ParcelColors"/>) whose loads count it down.</summary>
        public readonly int Color;

        /// <summary>Loads needed to open it.</summary>
        public readonly int Count;

        public ParcelGate(Cell cell, int color, int count)
        {
            Cell = cell;
            Color = color;
            Count = count;
        }

        public override string ToString() => $"gate {Cell} {ParcelColors.ToCode(Color)}x{Count}";
    }
}
