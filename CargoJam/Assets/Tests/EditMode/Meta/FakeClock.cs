using System;

namespace CargoJam.Meta.Tests
{
    /// <summary>A settable calendar for deterministic date tests.</summary>
    public sealed class FakeClock : IClock
    {
        public DateTime Today { get; set; }

        public FakeClock(int year = 2026, int month = 10, int day = 26)
        {
            Today = new DateTime(year, month, day);
        }

        public void Advance(int days) => Today = Today.AddDays(days);
    }
}
