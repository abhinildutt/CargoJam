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
    /// '#' is a bollard (a permanent obstacle) and '+' a customs gate (an obstacle until it opens, see
    /// <c>gates</c>); neither holds a parcel.
    /// <c>locks</c> padlocks the parcel at (x, y) until the parcel at (keyX, keyY) is loaded into a truck.
    /// <c>frost</c>, <c>express</c>, <c>straps</c> and <c>gates</c> follow the same pattern; a missing
    /// array means none (null under System.Text.Json, empty under JsonUtility).
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
        public FrostData[] frost;
        public ExpressData[] express;
        public StrapData[] straps;
        public GateData[] gates;
    }

    [Serializable]
    public class LockData
    {
        public int x;
        public int y;
        public int keyX;
        public int keyY;
    }

    /// <summary>A frozen crate: the parcel at (x, y) is held by <c>layers</c> (1..3) of ice.</summary>
    [Serializable]
    public class FrostData
    {
        public int x;
        public int y;
        public int layers;
    }

    /// <summary>An express parcel: the parcel at (x, y) must leave the yard within <c>taps</c> moves.</summary>
    [Serializable]
    public class ExpressData
    {
        public int x;
        public int y;
        public int taps;
    }

    /// <summary>A strapped pair: the parcels at (x, y) and (x2, y2) travel together.</summary>
    [Serializable]
    public class StrapData
    {
        public int x;
        public int y;
        public int x2;
        public int y2;
    }

    /// <summary>
    /// A customs gate on the '+' cell at (x, y): it opens once <c>count</c> parcels of <c>color</c> (a
    /// one-letter code, a string so JsonUtility can read it) have been loaded into trucks.
    /// </summary>
    [Serializable]
    public class GateData
    {
        public int x;
        public int y;
        public string color;
        public int count;
    }
}
