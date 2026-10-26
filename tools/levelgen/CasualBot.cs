using System;
using System.Collections.Generic;
using CargoJam.Core;

namespace CargoJam.LevelGen
{
    /// <summary>How a batch of casual-bot games went.</summary>
    public readonly struct BotStats
    {
        public readonly int Runs;
        public readonly int Fails;

        /// <summary>Fails where the dock still had room: every free parcel was padlocked.</summary>
        public readonly int Stuck;

        public BotStats(int runs, int fails, int stuck)
        {
            Runs = runs;
            Fails = fails;
            Stuck = stuck;
        }

        public double FailRate => Runs == 0 ? 0 : (double)Fails / Runs;
        public double StuckRate => Runs == 0 ? 0 : (double)Stuck / Runs;
    }

    /// <summary>
    /// A stand-in for a casual player: when a free parcel matches the loading truck it takes one 60% of
    /// the time, otherwise it taps any legal parcel at random. Its fail rate is our difficulty measure.
    /// </summary>
    public static class CasualBot
    {
        public const double MatchChance = 0.6;

        public static BotStats Grade(LevelDefinition level, int runs, int seed)
        {
            var rng = new Random(seed);
            int fails = 0, stuck = 0;
            for (int i = 0; i < runs; i++)
            {
                var session = Play(level, rng);
                if (session.Status != GameStatus.Failed) continue;
                fails++;
                if (!session.Dock.IsFull) stuck++;
            }
            return new BotStats(runs, fails, stuck);
        }

        public static GameSession Play(LevelDefinition level, Random rng)
        {
            var session = new GameSession(level);
            var free = new List<Parcel>();
            var matches = new List<Parcel>();
            var legal = new List<Parcel>();

            while (session.Status == GameStatus.Playing)
            {
                session.Board.GetFreeParcels(free);
                matches.Clear();
                legal.Clear();
                var truck = session.CurrentTruck;
                foreach (var p in free)
                {
                    if (session.IsLocked(p)) continue;
                    bool match = truck != null && truck.Accepts(p);
                    if (match) matches.Add(p);
                    if (match || !session.Dock.IsFull) legal.Add(p);
                }
                // The session fails on its own when nothing is legal, so this is only a safety net.
                if (legal.Count == 0) break;

                var pick = matches.Count > 0 && rng.NextDouble() < MatchChance
                    ? matches[rng.Next(matches.Count)]
                    : legal[rng.Next(legal.Count)];
                session.Tap(pick.Cell);
            }
            return session;
        }
    }
}
