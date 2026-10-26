namespace CargoJam.Core.Tests
{
    static class TestLevels
    {
        public static LevelDefinition Make(string trucks, int holdingSlots, int truckCapacity, params string[] rows)
        {
            return LevelDefinition.Parse(new LevelData
            {
                id = 0,
                trucks = trucks,
                holdingSlots = holdingSlots,
                truckCapacity = truckCapacity,
                rows = rows,
            });
        }

        public static GameSession Session(string trucks, int holdingSlots, int truckCapacity, params string[] rows)
        {
            return new GameSession(Make(trucks, holdingSlots, truckCapacity, rows));
        }
    }
}
