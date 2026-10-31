namespace CargoJam.Core.Tests
{
    static class TestLevels
    {
        public static LevelData Data(string trucks, int holdingSlots, int truckCapacity, params string[] rows)
        {
            return new LevelData
            {
                id = 0,
                trucks = trucks,
                holdingSlots = holdingSlots,
                truckCapacity = truckCapacity,
                rows = rows,
            };
        }

        public static LevelDefinition Make(string trucks, int holdingSlots, int truckCapacity, params string[] rows)
        {
            return LevelDefinition.Parse(Data(trucks, holdingSlots, truckCapacity, rows));
        }

        public static GameSession Session(string trucks, int holdingSlots, int truckCapacity, params string[] rows)
        {
            return new GameSession(Make(trucks, holdingSlots, truckCapacity, rows));
        }

        /// <summary>A session whose level has padlocks; build them with <see cref="Lock"/>.</summary>
        public static GameSession Session(LockData[] locks, string trucks, int holdingSlots, int truckCapacity, params string[] rows)
        {
            var data = Data(trucks, holdingSlots, truckCapacity, rows);
            data.locks = locks;
            return new GameSession(LevelDefinition.Parse(data));
        }

        /// <summary>A session from level data built with <see cref="Data"/> and the cargo helpers below.</summary>
        public static GameSession Session(LevelData data)
        {
            return new GameSession(LevelDefinition.Parse(data));
        }

        /// <summary>The parcel at (x, y) stays locked until the parcel at (keyX, keyY) is loaded.</summary>
        public static LockData Lock(int x, int y, int keyX, int keyY)
        {
            return new LockData { x = x, y = y, keyX = keyX, keyY = keyY };
        }

        /// <summary>The parcel at (x, y) is frozen under <paramref name="layers"/> layers of ice.</summary>
        public static FrostData Frost(int x, int y, int layers)
        {
            return new FrostData { x = x, y = y, layers = layers };
        }

        /// <summary>The parcel at (x, y) is express with <paramref name="taps"/> moves to leave the yard.</summary>
        public static ExpressData Express(int x, int y, int taps)
        {
            return new ExpressData { x = x, y = y, taps = taps };
        }

        /// <summary>The parcels at (x, y) and (x2, y2) are strapped together.</summary>
        public static StrapData Strap(int x, int y, int x2, int y2)
        {
            return new StrapData { x = x, y = y, x2 = x2, y2 = y2 };
        }

        /// <summary>The '+' at (x, y) opens after <paramref name="count"/> loads of <paramref name="color"/>.</summary>
        public static GateData Gate(int x, int y, char color, int count)
        {
            return new GateData { x = x, y = y, color = color.ToString(), count = count };
        }
    }
}
