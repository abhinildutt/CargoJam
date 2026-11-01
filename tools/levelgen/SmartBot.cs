using System;
using System.Collections.Generic;
using CargoJam.Core;

namespace CargoJam.LevelGen
{
    /// <summary>How a batch of thoughtful-bot games went.</summary>
    public readonly struct SmartStats
    {
        public readonly int Runs;
        public readonly int Fails;
        public readonly int ExpressFails;
        /// <summary>Fails with <see cref="FailReason.Stuck"/>: nothing could leave and Extra Slot wouldn't help.</summary>
        public readonly int Stuck;

        /// <summary>Moves made with no free match for the loading truck: real choices about what to park.</summary>
        public readonly int Decisions;
        public readonly int Moves;

        public SmartStats(int runs, int fails, int expressFails, int stuck, int decisions, int moves)
        {
            Stuck = stuck;
            Runs = runs;
            Fails = fails;
            ExpressFails = expressFails;
            Decisions = decisions;
            Moves = moves;
        }

        public double FailRate => Runs == 0 ? 0 : (double)Fails / Runs;

        /// <summary>The share of moves where nothing could go straight into the truck, so the player had to choose.</summary>
        public double DecisionShare => Moves == 0 ? 0 : (double)Decisions / Moves;

        public double StuckRate => Runs == 0 ? 0 : (double)Stuck / Runs;

        public double ExpressShare => Fails == 0 ? 0 : (double)ExpressFails / Fails;
    }

    /// <summary>
    /// A stand-in for a careful player, and the difficulty measure. It looks at what it can see (the
    /// yard, the dock, the loading truck and the <see cref="VisibleTrucks"/> after it), weighs every legal
    /// tap one move ahead (two when there is no free match or the dock is nearly full) and picks the
    /// best-looking one, with some human error: a softmax over the scores at <see cref="Temperature"/>.
    /// It never sees a mystery parcel's color before it is revealed.
    /// </summary>
    public static class SmartBot
    {
        /// <summary>Trucks shown after the loading one (the road shows the next one or two).</summary>
        public const int VisibleTrucks = 2;

        /// <summary>Score units per "unit of preference" in the softmax: higher plays sloppier.</summary>
        public const double Temperature = 3.0;

        const int MaxSteps = 2000;
        const int BranchCap = 6;

        public static SmartStats Grade(LevelDefinition level, int runs, int seed)
        {
            var rng = new Random(seed);
            int fails = 0, express = 0, stuck = 0, decisions = 0, moves = 0;
            for (int i = 0; i < runs; i++)
            {
                var session = Play(level, rng, out int d, out int m);
                decisions += d;
                moves += m;
                if (session.Status == GameStatus.Won) continue;
                fails++;
                if (session.FailReason == FailReason.ExpressMissed) express++;
                if (session.Status == GameStatus.Playing || session.FailReason == FailReason.Stuck) stuck++;
            }
            return new SmartStats(runs, fails, express, stuck, decisions, moves);
        }

        public static GameSession Play(LevelDefinition level, Random rng, out int decisions, out int moves)
        {
            var session = new GameSession(level);
            var legal = new List<Parcel>();
            var scores = new List<double>();
            decisions = moves = 0;
            for (int step = 0; step < MaxSteps && session.Status == GameStatus.Playing; step++)
            {
                Legal(session, legal);
                if (legal.Count == 0) break;
                if (!session.HasFreeMatch()) decisions++;
                moves++;

                bool careful = !session.HasFreeMatch() || session.Dock.Capacity - session.Dock.Count <= 1;
                scores.Clear();
                foreach (var p in legal)
                {
                    var next = session.Clone();
                    next.Tap(p.Cell);
                    scores.Add(careful ? Lookahead(next) : Score(next));
                }
                session.Tap(legal[Pick(scores, rng)].Cell);
            }
            return session;
        }

        static void Legal(GameSession session, List<Parcel> result)
        {
            session.GetMoves(result);
            result.RemoveAll(p => !session.CanTake(p));
        }

        /// <summary>The best score one more move can reach, over the few most promising moves.</summary>
        static double Lookahead(GameSession session)
        {
            if (session.Status != GameStatus.Playing) return Score(session);
            var legal = new List<Parcel>();
            Legal(session, legal);
            var children = new List<(double score, GameSession next)>(legal.Count);
            foreach (var p in legal)
            {
                var next = session.Clone();
                next.Tap(p.Cell);
                children.Add((Score(next), next));
            }
            if (children.Count == 0) return Score(session);
            children.Sort((a, b) => b.score.CompareTo(a.score));
            double best = double.MinValue;
            for (int i = 0; i < children.Count && i < BranchCap; i++) best = Math.Max(best, children[i].score);
            return best;
        }

        static int Pick(List<double> scores, Random rng)
        {
            double max = double.MinValue;
            foreach (double s in scores) max = Math.Max(max, s);
            double total = 0;
            var weights = new double[scores.Count];
            for (int i = 0; i < scores.Count; i++)
            {
                weights[i] = Math.Exp((scores[i] - max) / Temperature);
                total += weights[i];
            }
            double r = rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                r -= weights[i];
                if (r <= 0) return i;
            }
            return weights.Length - 1;
        }

        /// <summary>
        /// How good a position looks to a careful player: parcels shipped, a roomy dock, and free parcels
        /// for the trucks it can see; express parcels close to running out count against it.
        /// </summary>
        static double Score(GameSession s)
        {
            if (s.Status == GameStatus.Won) return 1e6;
            if (s.Status == GameStatus.Failed) return -1e6;

            double score = 0;
            var trucks = s.Trucks;
            int index = s.CurrentTruckIndex;
            int shipped = index * s.Level.TruckCapacity + (s.CurrentTruck?.Loaded ?? 0);
            score += 10 * shipped;

            int free = s.Dock.Capacity - s.Dock.Count;
            score -= 6 * s.Dock.Count;
            if (free == 0) score -= 25;
            else if (free == 1) score -= 8;

            // Free parcels the next visible trucks want, up to what those trucks still need.
            var moves = new List<Parcel>();
            s.GetMoves(moves);
            for (int t = 0; t <= VisibleTrucks && index + t < trucks.Count; t++)
            {
                var truck = trucks[index + t];
                int need = truck.Capacity - truck.Loaded;
                int have = 0;
                for (int i = 0; i < s.Dock.Capacity; i++)
                    if (s.Dock[i] != null && s.Dock[i].Color == truck.Color) have++;
                foreach (var p in moves)
                    if (!s.IsHidden(p) && p.Color == truck.Color) have++;
                double weight = t == 0 ? 4 : t == 1 ? 2 : 1;
                score += weight * Math.Min(need, have);
            }

            foreach (var p in s.Board.Parcels)
            {
                int left = s.ExpressLeft(p);
                if (left > 0 && left <= 3) score -= left <= 1 ? 30 : 12;
            }
            return score;
        }
    }
}
