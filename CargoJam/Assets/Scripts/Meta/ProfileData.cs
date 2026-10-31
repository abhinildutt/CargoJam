using System;

namespace CargoJam.Meta
{
    /// <summary>
    /// Everything persisted about the player. Flat public fields so Unity's JsonUtility can save it;
    /// the Game layer stores it in PlayerPrefs. Arrays are indexed by <see cref="BoosterType"/>.
    /// </summary>
    [Serializable]
    public class ProfileData
    {
        public int version = 1;

        public int coins = EconomyConfig.StartingCoins;
        /// <summary>0-based index of the level to play next.</summary>
        public int levelIndex;

        public int winStreak;
        public int bestStreak;

        /// <summary>Wins toward the next chest, 0..ChestEvery-1 (ChestEvery means one is ready).</summary>
        public int chestProgress;
        public int chestsOpened;

        public int[] boosters = new int[EconomyConfig.BoosterKinds];
        public bool[] unlocked = new bool[EconomyConfig.BoosterKinds];

        /// <summary>Next day of the 7-day calendar, 0..6.</summary>
        public int dailyIndex;
        /// <summary>Local date of the last daily claim, "yyyy-MM-dd", or empty.</summary>
        public string lastDailyClaim = "";
        public int loginStreak;
        /// <summary>Local date of the last day the game was opened, "yyyy-MM-dd", or empty.</summary>
        public string lastOpenDay = "";

        public int totalWins;
        public int totalGiveUps;
        /// <summary>Lifetime coins granted (wins, chests, daily rewards, AddCoins); spending doesn't lower it.</summary>
        public int coinsEarned;

        // ---- v2 (design v3: journey) ----

        /// <summary>Postcards earned, indexed by <see cref="Journey"/> stop. A save from v1 loads this as null or empty.</summary>
        public bool[] postcards = new bool[Journey.StopCount];
        /// <summary>Progress leg (<see cref="Journey.LegFor"/>) whose arrival reveal has played; -1 = none yet.</summary>
        public int lastArrivalShown = -1;
    }

    /// <summary>The local calendar date, injectable for tests.</summary>
    public interface IClock
    {
        DateTime Today { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime Today => DateTime.Now.Date;
    }
}
