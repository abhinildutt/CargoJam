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
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int id;
        public int holdingSlots = 5;
        public int truckCapacity = 3;
        public string trucks;
        public string[] rows;
    }
}
