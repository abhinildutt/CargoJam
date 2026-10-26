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

        /// <summary>The parcel at (x, y) stays locked until the parcel at (keyX, keyY) is loaded.</summary>
        public static LockData Lock(int x, int y, int keyX, int keyY)
        {
            return new LockData { x = x, y = y, keyX = keyX, keyY = keyY };
        }
    }
}
