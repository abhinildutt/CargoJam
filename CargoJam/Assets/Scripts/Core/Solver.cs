using System.Collections.Generic;

namespace CargoJam.Core
{
    /// <summary>
    /// Depth-first search over real <see cref="GameSession"/> rules. Used to check that levels can be
    /// beaten; later it can grade difficulty for level design.
    /// </summary>
    public static class Solver
    {
        public sealed class Result
        {
            public bool Solvable;
            /// <summary>True when the search gave up, so "not solvable" is not proven.</summary>
            public bool HitNodeLimit;
            public int NodesVisited;
            /// <summary>Taps that win the level, in order, when <see cref="Solvable"/>.</summary>
            public List<Cell> Moves = new List<Cell>();
        }

        public static Result Solve(LevelDefinition level, int nodeLimit = 200_000)
        {
            var result = new Result();
            var visited = new HashSet<string>();
            result.Solvable = Search(new GameSession(level), visited, result, nodeLimit);
            if (result.Solvable) result.Moves.Reverse();
            return result;
        }

        static bool Search(GameSession session, HashSet<string> visited, Result result, int nodeLimit)
        {
            if (session.Status == GameStatus.Won) return true;
            if (session.Status == GameStatus.Failed) return false;
            if (result.NodesVisited >= nodeLimit)
            {
                result.HitNodeLimit = true;
                return false;
            }
            if (!visited.Add(session.StateKey())) return false;
            result.NodesVisited++;

            var free = new List<Parcel>();
            session.Board.GetFreeParcels(free);
            var truck = session.CurrentTruck;

            // Loading a matching parcel never costs a slot, so try those first.
            free.Sort((a, b) => Rank(a, truck).CompareTo(Rank(b, truck)));

            foreach (var parcel in free)
            {
                if (session.Dock.IsFull && (truck == null || !truck.Accepts(parcel))) continue;
                var next = session.Clone();
                next.Tap(parcel.Cell);
                if (Search(next, visited, result, nodeLimit))
                {
                    result.Moves.Add(parcel.Cell);
                    return true;
                }
            }
            return false;
        }

        static int Rank(Parcel p, Truck truck) => truck != null && truck.Accepts(p) ? 0 : 1;
    }
}
