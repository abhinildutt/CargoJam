using System;

namespace CargoJam.Core
{
    /// <summary>
    /// The level file format, kept flat so JsonUtility can read it.
    /// <code>
    /// { "id": 3, "holdingSlots": 5, "truckCapacity": 3,
    ///   "trucks": "RBRG",
    ///   "rows": ["RB.G",     // row 0, touching the dock
    ///            "GRBR"] }
    /// </code>
    /// Each row character is a color code from <see cref="ParcelColors.Codes"/> or '.' for an empty cell.
    /// A lowercase code is a mystery parcel of that color (hidden until it becomes free).
    /// <c>locks</c> padlocks the parcel at (x, y) until the parcel at (keyX, keyY) is loaded into a truck.
    /// <c>hard</c> marks a HARD level (banner, double coins).
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int id;
        public bool hard;
        public int holdingSlots = 5;
        public int truckCapacity = 3;
        public string trucks;
        public string[] rows;
        public LockData[] locks;
    }

    [Serializable]
    public class LockData
    {
        public int x;
        public int y;
        public int keyX;
        public int keyY;
    }
}
