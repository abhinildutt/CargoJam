using System;
using System.Collections.Generic;
using CargoJam.Core;

namespace CargoJam.LevelGen
{
    /// <summary>
    /// Makes one candidate board for a plan. It first picks a legal "dig order" (each parcel free once the
    /// ones before it are gone, padlocked parcels only after their key), then colors the parcels in
    /// truck-sized groups along that order. With no noise the truck order follows the dig order exactly
    /// and the level needs no dock at all; noise shuffles parcels away from their truck, which is what
    /// makes the dock fill up.
    /// </summary>
    public static class BoardMaker
    {
        static readonly int[] DX = { 0, -1, 1, 0 };
        static readonly int[] DY = { -1, 0, 0, 1 };

        public static LevelData Make(LevelPlan plan, Random rng, double noise)
        {
            int w = plan.Width, h = plan.Height, n = w * h;
            var filled = new bool[n];
            for (int i = 0; i < n; i++) filled[i] = true;

            // Holes never go in row 0, so every level opens on a full front row.
            int holes = n - plan.Parcels;
            while (holes > 0)
            {
                int i = w + rng.Next(n - w);
                if (!filled[i]) continue;
                filled[i] = false;
                holes--;
            }

            var keyOf = new int[n];
            for (int i = 0; i < n; i++) keyOf[i] = -1;
            var lockCells = new List<int>();
            if (!PickLocks(plan, rng, filled, keyOf, lockCells)) return null;

            var order = DigOrder(w, h, filled, keyOf, rng);
            if (order == null) return null;

            // Noise moves a parcel up to `noise` places away from where the dig order wants it.
            var sortKey = new double[n];
            for (int i = 0; i < order.Count; i++) sortKey[order[i]] = i + noise * rng.NextDouble();
            var sorted = new List<int>(order);
            sorted.Sort((a, b) => sortKey[a].CompareTo(sortKey[b]));

            // A key must board before its lock's truck needs the locked parcel; noise may have swapped them.
            foreach (int locked in lockCells)
            {
                int li = sorted.IndexOf(locked), ki = sorted.IndexOf(keyOf[locked]);
                if (ki > li) (sorted[li], sorted[ki]) = (sorted[ki], sorted[li]);
            }

            var truckColors = TruckColors(plan, rng, plan.Parcels / LevelPlan.TruckCapacity);
            var color = new int[n];
            for (int i = 0; i < n; i++) color[i] = ParcelColors.None;
            for (int i = 0; i < sorted.Count; i++) color[sorted[i]] = truckColors[i / LevelPlan.TruckCapacity];

            var mystery = PickMystery(plan, rng, filled, keyOf, lockCells);
            if (mystery == null) return null;

            var rows = new string[h];
            var chars = new char[w];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    char c = filled[i] ? ParcelColors.ToCode(color[i]) : '.';
                    chars[x] = mystery[i] ? char.ToLowerInvariant(c) : c;
                }
                rows[y] = new string(chars);
            }

            var trucks = new char[truckColors.Length];
            for (int i = 0; i < trucks.Length; i++) trucks[i] = ParcelColors.ToCode(truckColors[i]);

            var locks = new LockData[lockCells.Count];
            for (int i = 0; i < locks.Length; i++)
            {
                int l = lockCells[i], k = keyOf[l];
                locks[i] = new LockData { x = l % w, y = l / w, keyX = k % w, keyY = k / w };
            }

            return new LevelData
            {
                id = plan.Id,
                hard = plan.Hard,
                holdingSlots = plan.Slots,
                truckCapacity = LevelPlan.TruckCapacity,
                trucks = new string(trucks),
                rows = rows,
                locks = locks.Length > 0 ? locks : null,
            };
        }

        /// <summary>Padlocks sit in the front three rows where they're seen; keys are anywhere in the top half.</summary>
        static bool PickLocks(LevelPlan plan, Random rng, bool[] filled, int[] keyOf, List<int> lockCells)
        {
            int w = plan.Width, h = plan.Height;
            var used = new bool[filled.Length];
            for (int i = 0; i < plan.Locks; i++)
            {
                int locked = -1, key = -1;
                for (int attempt = 0; attempt < 200 && locked < 0; attempt++)
                {
                    int l = rng.Next(w * Math.Min(3, h));
                    int k = rng.Next(w * Math.Max(2, (h + 1) / 2));
                    if (l == k || !filled[l] || !filled[k] || used[l] || used[k]) continue;
                    // Keys right next to their lock make the lock pointless.
                    if (Math.Abs(l % w - k % w) + Math.Abs(l / w - k / w) < 2) continue;
                    locked = l;
                    key = k;
                }
                if (locked < 0) return false;
                used[locked] = used[key] = true;
                keyOf[locked] = key;
                lockCells.Add(locked);
            }
            return true;
        }

        /// <summary>A random legal removal order, or null when the locks wall everything in.</summary>
        static List<int> DigOrder(int w, int h, bool[] filled, int[] keyOf, Random rng)
        {
            int n = w * h;
            var gone = new bool[n];
            for (int i = 0; i < n; i++) gone[i] = !filled[i];
            var order = new List<int>();
            var free = new List<int>();
            int remaining = 0;
            foreach (bool f in filled) if (f) remaining++;

            while (remaining > 0)
            {
                FreeCells(w, h, gone, free);
                for (int i = free.Count - 1; i >= 0; i--)
                {
                    int key = keyOf[free[i]];
                    if (key >= 0 && !gone[key]) free.RemoveAt(i);
                }
                if (free.Count == 0) return null;
                int pick = free[rng.Next(free.Count)];
                gone[pick] = true;
                order.Add(pick);
                remaining--;
            }
            return order;
        }

        /// <summary>Filled cells with a path of empty cells to row 0 (or in row 0), like Board.GetFreeParcels.</summary>
        static void FreeCells(int w, int h, bool[] gone, List<int> result)
        {
            result.Clear();
            int n = w * h;
            var reach = new bool[n];
            var queue = new Queue<int>();
            for (int x = 0; x < w; x++)
            {
                if (!gone[x]) continue;
                reach[x] = true;
                queue.Enqueue(x);
            }
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nx = c % w + DX[d], ny = c / w + DY[d];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (reach[ni] || !gone[ni]) continue;
                    reach[ni] = true;
                    queue.Enqueue(ni);
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (gone[i]) continue;
                bool isFree = i < w;
                for (int d = 0; d < 4 && !isFree; d++)
                {
                    int nx = i % w + DX[d], ny = i / w + DY[d];
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && reach[ny * w + nx]) isFree = true;
                }
                if (isFree) result.Add(i);
            }
        }

        /// <summary>Every color gets at least one truck, and the same color rarely comes twice in a row.</summary>
        static int[] TruckColors(LevelPlan plan, Random rng, int count)
        {
            var palette = new List<int>();
            for (int c = 0; c < ParcelColors.Count; c++) palette.Add(c);
            Shuffle(palette, rng);
            var colors = palette.GetRange(0, plan.Colors);

            var trucks = new int[count];
            for (int i = 0; i < count; i++) trucks[i] = i < colors.Count ? colors[i] : colors[rng.Next(colors.Count)];
            Shuffle(trucks, rng);
            for (int pass = 0; pass < 4; pass++)
            for (int i = 1; i < count; i++)
            {
                if (trucks[i] != trucks[i - 1]) continue;
                int j = rng.Next(count);
                (trucks[i], trucks[j]) = (trucks[j], trucks[i]);
            }
            return trucks;
        }

        /// <summary>Mystery parcels start buried (never in row 0) and stay clear of padlocks and keys.</summary>
        static bool[] PickMystery(LevelPlan plan, Random rng, bool[] filled, int[] keyOf, List<int> lockCells)
        {
            int w = plan.Width, h = plan.Height, n = w * h;
            var mystery = new bool[n];
            if (plan.Mystery == 0) return mystery;

            var gone = new bool[n];
            for (int i = 0; i < n; i++) gone[i] = !filled[i];
            var free = new List<int>();
            FreeCells(w, h, gone, free);
            var excluded = new bool[n];
            foreach (int i in free) excluded[i] = true;
            foreach (int l in lockCells)
            {
                excluded[l] = true;
                excluded[keyOf[l]] = true;
            }

            int placed = 0;
            for (int attempt = 0; attempt < 500 && placed < plan.Mystery; attempt++)
            {
                int i = w + rng.Next(n - w);
                if (!filled[i] || excluded[i] || mystery[i]) continue;
                mystery[i] = true;
                placed++;
            }
            return placed == plan.Mystery ? mystery : null;
        }

        static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
