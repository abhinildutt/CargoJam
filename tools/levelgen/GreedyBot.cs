using System;
using System.Collections.Generic;
using CargoJam.Core;

namespace CargoJam.LevelGen
{
    /// <summary>
    /// A reactive player who never plans: it loads a free match whenever there is one, otherwise it parks
    /// the parcel whose truck it can see coming soonest (any parcel when none is in sight), breaking
    /// ties at random. If this player wins a level, the level isn't a puzzle; its fail rate is a floor
    /// that medium and harder levels must clear (see LevelPlan.GreedyFloor).
    /// </summary>
    public static class GreedyBot
    {
        const int MaxSteps = 2000;

        public static double FailRate(LevelDefinition level, int runs, int seed)
        {
            var rng = new Random(seed);
            int fails = 0;
            for (int i = 0; i < runs; i++)
                if (Play(level, rng).Status != GameStatus.Won) fails++;
            return runs == 0 ? 0 : (double)fails / runs;
        }

        public static GameSession Play(LevelDefinition level, Random rng)
        {
            var session = new GameSession(level);
            var moves = new List<Parcel>();
            var best = new List<Parcel>();
            for (int step = 0; step < MaxSteps && session.Status == GameStatus.Playing; step++)
            {
                session.GetMoves(moves);
                moves.RemoveAll(p => !session.CanTake(p));
                if (moves.Count == 0) break;

                var truck = session.CurrentTruck;
                best.Clear();
                foreach (var p in moves)
                    if (truck != null && truck.Accepts(p)) best.Add(p);
                if (best.Count == 0)
                {
                    int soonest = int.MaxValue;
                    foreach (var p in moves)
                    {
                        int due = session.IsHidden(p) ? int.MaxValue - 1 : Due(session, p.Color);
                        if (due < soonest)
                        {
                            soonest = due;
                            best.Clear();
                        }
                        if (due == soonest) best.Add(p);
                    }
                }
                session.Tap(best[rng.Next(best.Count)].Cell);
            }
            return session;
        }

        /// <summary>How many trucks until one of this color loads, among those in sight; past them when none.</summary>
        static int Due(GameSession s, int color)
        {
            for (int t = 1; t <= SmartBot.VisibleTrucks && s.CurrentTruckIndex + t < s.Trucks.Count; t++)
                if (s.Trucks[s.CurrentTruckIndex + t].Color == color) return t;
            return SmartBot.VisibleTrucks + 1;
        }
    }
}
