namespace CargoJam.Core
{
    /// <summary>Parcel colors are small ints; level files spell them with one letter each.</summary>
    public static class ParcelColors
    {
        public const int None = -1;

        /// <summary>Red, Blue, Green, Yellow, Purple, Orange.</summary>
        public const string Codes = "RBGYPO";

        public static int Count => Codes.Length;

        public static int FromCode(char code) => Codes.IndexOf(char.ToUpperInvariant(code));

        public static char ToCode(int color) => color >= 0 && color < Codes.Length ? Codes[color] : '.';
    }
}
