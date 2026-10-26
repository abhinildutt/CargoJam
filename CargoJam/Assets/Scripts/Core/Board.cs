using System.Collections.Generic;

namespace CargoJam.Core
{
    /// <summary>
    /// The parcel yard. A parcel is free when a path of empty cells (4-way) connects it to row 0,
    /// or when it already sits in row 0.
    /// </summary>
    public sealed class Board
    {
        static readonly int[] DX = { 0, -1, 1, 0 };
        static readonly int[] DY = { -1, 0, 0, 1 };

        readonly Parcel[] cells;

        public int Width { get; }
        public int Height { get; }
        public int Count { get; private set; }

        public Board(int width, int height)
        {
            Width = width;
            Height = height;
            cells = new Parcel[width * height];
        }

        public bool InBounds(Cell c) => c.X >= 0 && c.X < Width && c.Y >= 0 && c.Y < Height;

        public Parcel Get(Cell c) => InBounds(c) ? cells[Index(c)] : null;

        public IEnumerable<Parcel> Parcels
        {
            get
            {
                foreach (var p in cells)
                    if (p != null) yield return p;
            }
        }

        internal void Place(Parcel parcel)
        {
            int i = Index(parcel.Cell);
            if (cells[i] == null) Count++;
            cells[i] = parcel;
        }

        internal void Remove(Cell c)
        {
            int i = Index(c);
            if (cells[i] != null) Count--;
            cells[i] = null;
        }

        /// <summary>
        /// Shortest path from <paramref name="from"/> to row 0 through empty cells, including both ends.
        /// Returns null when the parcel is blocked.
        /// </summary>
        public List<Cell> FindExitPath(Cell from)
        {
            if (!InBounds(from)) return null;
            if (from.Y == 0) return new List<Cell> { from };

            var parent = new int[cells.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = -2;

            var queue = new Queue<Cell>();
            queue.Enqueue(from);
            parent[Index(from)] = -1;

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    var n = new Cell(c.X + DX[d], c.Y + DY[d]);
                    if (!InBounds(n)) continue;
                    int ni = Index(n);
                    if (parent[ni] != -2 || cells[ni] != null) continue;
                    parent[ni] = Index(c);
                    if (n.Y == 0) return BuildPath(parent, ni);
                    queue.Enqueue(n);
                }
            }
            return null;
        }

        public bool IsFree(Cell c) => Get(c) != null && FindExitPath(c) != null;

        /// <summary>Fills <paramref name="result"/> with every parcel that can currently be tapped.</summary>
        public void GetFreeParcels(List<Parcel> result)
        {
            result.Clear();
            var reachable = new bool[cells.Length];
            var queue = new Queue<Cell>();
            for (int x = 0; x < Width; x++)
            {
                var c = new Cell(x, 0);
                if (cells[Index(c)] != null) continue;
                reachable[Index(c)] = true;
                queue.Enqueue(c);
            }

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    var n = new Cell(c.X + DX[d], c.Y + DY[d]);
                    if (!InBounds(n)) continue;
                    int ni = Index(n);
                    if (reachable[ni] || cells[ni] != null) continue;
                    reachable[ni] = true;
                    queue.Enqueue(n);
                }
            }

            foreach (var p in cells)
            {
                if (p == null) continue;
                if (p.Cell.Y == 0 || TouchesReachable(p.Cell, reachable)) result.Add(p);
            }
        }

        bool TouchesReachable(Cell c, bool[] reachable)
        {
            for (int d = 0; d < 4; d++)
            {
                var n = new Cell(c.X + DX[d], c.Y + DY[d]);
                if (InBounds(n) && reachable[Index(n)]) return true;
            }
            return false;
        }

        List<Cell> BuildPath(int[] parent, int end)
        {
            var path = new List<Cell>();
            for (int i = end; i != -1; i = parent[i])
                path.Add(new Cell(i % Width, i / Width));
            path.Reverse();
            return path;
        }

        int Index(Cell c) => c.Y * Width + c.X;
    }
}
