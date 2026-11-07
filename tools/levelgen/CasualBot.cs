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

        /// <summary>Fails with <see cref="FailReason.Stuck"/>: nothing tappable could leave and Extra Slot wouldn't help.</summary>
        public readonly int Stuck;

        /// <summary>Fails with <see cref="FailReason.ExpressMissed"/>.</summary>
        public readonly int ExpressFails;

        public BotStats(int runs, int fails, int stuck, int expressFails)
        {
            Runs = runs;
            Fails = fails;
            Stuck = stuck;
            ExpressFails = expressFails;
        }

        public double FailRate => Runs == 0 ? 0 : (double)Fails / Runs;
        public double StuckRate => Runs == 0 ? 0 : (double)Stuck / Runs;

        /// <summary>The share of fails that were express misses (0 when nothing failed).</summary>
        public double ExpressShare => Fails == 0 ? 0 : (double)ExpressFails / Fails;
    }

    /// <summary>
    /// A stand-in for a casual player: when a free parcel matches the loading truck it takes one 60% of
    /// the time, otherwise it taps any legal unit at random. An express parcel close to running out
    /// (3 or fewer moves left) gets taken first 75% of the time. Its fail rate is our difficulty measure.
    /// </summary>
    public static class CasualBot
    {
        public const double MatchChance = 0.6;
        public const double ExpressChance = 0.75;
        public const int ExpressUrgent = 3;

        /// <summary>
        /// Every legal move takes a unit out of the yard, so a game can't run longer than its parcels;
        /// this only guards against a rules change that lets a tap do nothing.
        /// </summary>
        const int MaxSteps = 2000;

        public static BotStats Grade(LevelDefinition level, int runs, int seed)
        {
            var rng = new Random(seed);
            int fails = 0, stuck = 0, express = 0;
            for (int i = 0; i < runs; i++)
            {
                var session = Play(level, rng);
                if (session.Status == GameStatus.Won) continue;
                fails++;
                // A game the step cap cut short counts as stuck, so it shows up in the grade.
                if (session.Status == GameStatus.Playing || session.FailReason == FailReason.Stuck) stuck++;
                if (session.FailReason == FailReason.ExpressMissed) express++;
            }
            return new BotStats(runs, fails, stuck, express);
        }

        public static GameSession Play(LevelDefinition level, Random rng)
        {
            var session = new GameSession(level);
            var moves = new List<Parcel>();
            var matches = new List<Parcel>();
            var urgent = new List<Parcel>();
            var legal = new List<Parcel>();

            for (int step = 0; step < MaxSteps && session.Status == GameStatus.Playing; step++)
            {
                session.GetMoves(moves);
                matches.Clear();
                urgent.Clear();
                legal.Clear();
                var truck = session.CurrentTruck;
                foreach (var p in moves)
                {
                    if (!session.CanTake(p)) continue;
                    legal.Add(p);
                    if (truck != null && truck.Accepts(p)) matches.Add(p);
                    if (session.IsExpress(p) && session.ExpressLeft(p) > 0 && session.ExpressLeft(p) <= ExpressUrgent) urgent.Add(p);
                }
                // The session fails on its own when nothing is legal, so this is only a safety net.
                if (legal.Count == 0) break;

                Parcel pick;
                if (urgent.Count > 0 && rng.NextDouble() < ExpressChance) pick = urgent[rng.Next(urgent.Count)];
                else if (matches.Count > 0 && rng.NextDouble() < MatchChance) pick = matches[rng.Next(matches.Count)];
                else pick = legal[rng.Next(legal.Count)];
                session.Tap(pick.Cell);
            }
            return session;
        }
    }
}
