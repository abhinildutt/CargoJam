namespace CargoJam.Core
{
    public sealed class Parcel
    {
        public int Id { get; }
        public int Color { get; }

        /// <summary>Where the parcel sat in the yard. Kept after it leaves so views can find it.</summary>
        public Cell Cell { get; }

        public Parcel(int id, int color, Cell cell)
        {
            Id = id;
            Color = color;
            Cell = cell;
        }

        public override string ToString() => $"Parcel#{Id} {ParcelColors.ToCode(Color)} {Cell}";
    }
}
