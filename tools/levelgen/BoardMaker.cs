using System;
using System.Collections.Generic;
using CargoJam.Core;

namespace CargoJam.LevelGen
{
    /// <summary>
    /// Makes one candidate board for a plan. It lays out gate walls, bollards and holes, picks the
    /// blockers (padlocks, mystery parcels, frozen crates), then picks a legal "dig order": each parcel is
    /// free once the ones before it are gone, padlocked parcels come after their key, frozen crates once
    /// their thaw count of parcels has gone (one parcel a move), and each gate is a virtual cell "dug" when it opens. Then it
    /// colors the parcels in truck-sized groups along that order and places the cargo that depends on
    /// color or order (gate colors, express parcels, straps). With no noise the truck order follows the
    /// dig order exactly and the level needs no dock at all; noise shuffles parcels away from their truck,
    /// which is what makes the dock fill up.
    /// </summary>
    public static class BoardMaker
    {
        const byte Hole = 0, Parcel = 1, Bollard = 2, Gate = 3;

        static readonly int[] DX = { 0, -1, 1, 0 };
        static readonly int[] DY = { -1, 0, 0, 1 };

        public static LevelData Make(LevelPlan plan, Random rng, double noise, double spread = 0)
        {
            int w = plan.Width, h = plan.Height, n = w * h;
            var kind = new byte[n];
            for (int i = 0; i < n; i++) kind[i] = Parcel;

            var gateCells = new List<int>();
            if (plan.Gates > 0 && !PlaceGateWalls(plan, rng, kind, gateCells)) return null;
            if (plan.Pens) PlacePens(plan, rng, kind);
            int bollards = PlaceBollards(plan, rng, kind);
            if (bollards < 0) return null;

            // Holes never go in row 0, so every level opens on a full front row. Bollards took some over.
            int open = 0;
            for (int i = 0; i < n; i++) if (kind[i] == Parcel) open++;
            int parcels = (open - Math.Max(0, plan.Holes - bollards)) / LevelPlan.TruckCapacity * LevelPlan.TruckCapacity;
            if (parcels < LevelPlan.TruckCapacity * plan.Colors) return null;
            for (int holes = open - parcels, guard = 0; holes > 0; guard++)
            {
                if (guard > 10_000) return null;
                int i = w + rng.Next(n - w);
                if (kind[i] != Parcel) continue;
                kind[i] = Hole;
                holes--;
            }
            if (gateCells.Count > 0 && !EachGateWallsSomethingOff(w, h, kind, gateCells)) return null;

            var keyOf = new int[n];
            for (int i = 0; i < n; i++) keyOf[i] = -1;
            var lockCells = new List<int>();
            if (!PickLocks(plan, rng, kind, keyOf, lockCells)) return null;
            var isKey = new bool[n];
            foreach (int l in lockCells) isKey[keyOf[l]] = true;

            var mystery = PickMystery(plan, rng, kind, keyOf, lockCells);
            if (mystery == null) return null;

            var frost = PickFrost(plan, rng, kind, mystery, keyOf, isKey);
            if (frost == null) return null;

            var gateMin = new int[gateCells.Count];
            for (int g = 0; g < gateMin.Length; g++)
                gateMin[g] = LevelPlan.TruckCapacity * (g + 1 + (plan.GateMinCount > 3 ? 3 : 0) + rng.Next(plan.GateMaxCount > 3 ? 3 : 2));
            var order = DigOrder(w, h, kind, keyOf, frost, gateCells, gateMin, rng);
            if (order == null) return null;

            // Spread deals the trucks round-robin along the dig order (truck k gets dig positions k,
            // k+T, k+2T: one near the front, one mid-yard, one deep), so every truck sends the player
            // digging and the yard never just opens up; 0 keeps each truck's three together. Noise then
            // moves a parcel up to `noise` places away from where that order wants it.
            int digCount = order.Count;
            int trucks = Math.Max(1, digCount / LevelPlan.TruckCapacity);
            var sortKey = new double[n];
            for (int i = 0; i < order.Count; i++)
            {
                double roundRobin = (i % trucks) * LevelPlan.TruckCapacity + (double)i / trucks;
                sortKey[order[i]] = (1 - spread) * i + spread * roundRobin + noise * rng.NextDouble();
            }
            ClampBehindGates(w, h, kind, gateCells, sortKey);
            var sorted = new List<int>(order);
            sorted.Sort((a, b) => sortKey[a].CompareTo(sortKey[b]));

            // A key must board before its lock's truck needs the locked parcel; noise may have swapped them.
            foreach (int locked in lockCells)
            {
                int li = sorted.IndexOf(locked), ki = sorted.IndexOf(keyOf[locked]);
                if (ki > li) (sorted[li], sorted[ki]) = (sorted[ki], sorted[li]);
            }

            // Gates leave the truck order; remember how many parcels board before each one opens.
            var parcelsBefore = new int[gateCells.Count];
            var truckOrder = new List<int>(parcels);
            foreach (int c in sorted)
            {
                if (kind[c] == Gate) parcelsBefore[gateCells.IndexOf(c)] = truckOrder.Count;
                else truckOrder.Add(c);
            }

            var truckColors = TruckColors(plan, rng, parcels / LevelPlan.TruckCapacity);
            var color = new int[n];
            var group = new int[n];
            for (int i = 0; i < n; i++) color[i] = ParcelColors.None;
            for (int i = 0; i < truckOrder.Count; i++)
            {
                color[truckOrder[i]] = truckColors[i / LevelPlan.TruckCapacity];
                group[truckOrder[i]] = i / LevelPlan.TruckCapacity;
            }

            var gates = PickGateColors(plan, rng, truckColors, gateCells, parcelsBefore, w);
            if (gates == null) return null;
            SharpenFrost(rng, frost, group);

            var digOnly = new List<int>(parcels);
            foreach (int c in order) if (kind[c] == Parcel) digOnly.Add(c);
            var expressTaps = PickExpress(plan, rng, digOnly, mystery, keyOf, isKey, frost, group);
            if (expressTaps == null) return null;

            var straps = PickStraps(plan, rng, w, h, kind, color, group, mystery, keyOf, isKey, frost, expressTaps);
            if (straps == null) return null;

            var data = Build(plan, kind, color, mystery, truckColors, lockCells, keyOf, frost, expressTaps, straps, gates);
            var errors = LevelDefinition.Validate(data, out var level);
            if (errors.Count > 0) return null;
            // A valid level can still open with nothing to tap (say, every free parcel frozen).
            var session = new GameSession(level);
            if (!session.HasLegalMove()) return null;
            // The first truck's color mostly out of reach, so the opening asks for a plan.
            var moves = new List<Parcel>();
            session.GetMoves(moves);
            int matches = 0;
            foreach (var p in moves) if (session.CurrentTruck.Accepts(p)) matches++;
            if (matches > plan.MaxOpeningMatches) return null;
            // Past its debut a key starts buried, so reaching it in time is a choice, not a freebie.
            if (plan.Debut == null)
                foreach (var l in level.Locks)
                    if (session.Board.IsFree(l.Key)) return null;
            return data;
        }

        static LevelData Build(LevelPlan plan, byte[] kind, int[] color, bool[] mystery, int[] truckColors,
                               List<int> lockCells, int[] keyOf, int[] frost, int[] expressTaps,
                               List<(int a, int b)> straps, GateData[] gates)
        {
            int w = plan.Width, h = plan.Height;
            var rows = new string[h];
            var chars = new char[w];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    char c = kind[i] switch
                    {
                        Parcel => ParcelColors.ToCode(color[i]),
                        Bollard => '#',
                        Gate => '+',
                        _ => '.',
                    };
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

            var frostData = new List<FrostData>();
            var expressData = new List<ExpressData>();
            for (int i = 0; i < kind.Length; i++)
            {
                if (frost[i] > 0) frostData.Add(new FrostData { x = i % w, y = i / w, layers = frost[i] });
                if (expressTaps[i] > 0) expressData.Add(new ExpressData { x = i % w, y = i / w, taps = expressTaps[i] });
            }
            var strapData = new StrapData[straps.Count];
            for (int i = 0; i < strapData.Length; i++)
            {
                var (a, b) = straps[i];
                strapData[i] = new StrapData { x = a % w, y = a / w, x2 = b % w, y2 = b / w };
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
                frost = frostData.Count > 0 ? frostData.ToArray() : null,
                express = expressData.Count > 0 ? expressData.ToArray() : null,
                straps = strapData.Length > 0 ? strapData : null,
                gates = gates.Length > 0 ? gates : null,
            };
        }

        // ---- layout -----------------------------------------------------------------------------

        /// <summary>
        /// Gate walls: a row of bollards across row r (2 &lt;= r &lt;= h-3) with the gate in its one gap. Each
        /// gate gets its own wall, two rows apart at least, so each one opens its own region (rows 2 and 4
        /// fit a 7-row yard).
        /// </summary>
        static bool PlaceGateWalls(LevelPlan plan, Random rng, byte[] kind, List<int> gateCells)
        {
            int w = plan.Width, h = plan.Height;
            int lo = 2, hi = h - 3;
            int spare = hi - lo - 2 * (plan.Gates - 1);
            if (spare < 0) return false;
            // Walls front to back; each takes a random share of the spare rows in front of it.
            int r = lo;
            for (int g = 0; g < plan.Gates; g++)
            {
                int skip = rng.Next(spare + 1);
                r += skip;
                spare -= skip;
                int gx = rng.Next(w);
                for (int x = 0; x < w; x++) kind[r * w + x] = x == gx ? Gate : Bollard;
                gateCells.Add(r * w + gx);
                r += 2;
            }
            return true;
        }

        /// <summary>
        /// Pens: walls of bollards running from row 2 or 3 to the back, splitting the back of the yard into
        /// narrow lanes (5 wide: 2|2; 6 wide: 3|2; 7 wide: 2|1|2) that open only from their front, so the
        /// yard can't hollow out and every truck to the last has to be dug for. Half the time a wall
        /// stops one short of the back row, leaving a way round underneath.
        /// </summary>
        static void PlacePens(LevelPlan plan, Random rng, byte[] kind)
        {
            int w = plan.Width, h = plan.Height;
            int[] walls = w switch { 5 => new[] { 2 }, 6 => new[] { 3 }, 7 => new[] { 2, 4 }, _ => Array.Empty<int>() };
            int top = 2 + rng.Next(2);
            foreach (int x in walls)
            {
                int bottom = rng.Next(2) == 0 ? h - 1 : h - 2;
                for (int y = top; y <= bottom; y++)
                    if (kind[y * w + x] == Parcel) kind[y * w + x] = Bollard;
            }
        }

        /// <summary>
        /// Free-standing bollards, mirrored left-right; none in row 0 before L35. Each candidate must
        /// leave every other cell reachable once the yard is dug (the dig-order legality check). Returns
        /// how many were placed, or -1.
        /// </summary>
        static int PlaceBollards(LevelPlan plan, Random rng, byte[] kind)
        {
            int w = plan.Width, h = plan.Height, n = w * h;
            int placed = 0, minRow = plan.BollardsInRow0 ? 0 : 1;
            for (int attempt = 0; attempt < 400 && placed < plan.Bollards; attempt++)
            {
                int x = rng.Next(w), y = minRow + rng.Next(h - minRow);
                int mx = w - 1 - x;
                int a = y * w + x, b = y * w + mx;
                if (kind[a] != Parcel || kind[b] != Parcel) continue;
                // A single centre bollard only tops up an odd count.
                if (a == b && plan.Bollards - placed != 1) continue;
                if (a != b && plan.Bollards - placed < 2) continue;
                // Never a whole front row of bollards.
                if (y == 0 && FrontRowBollards(kind, w) + (a == b ? 1 : 2) >= w - 1) continue;
                kind[a] = kind[b] = Bollard;
                if (!AllReachable(w, h, kind))
                {
                    kind[a] = kind[b] = Parcel;
                    continue;
                }
                placed += a == b ? 1 : 2;
            }
            return placed == plan.Bollards ? placed : -1;
        }

        static int FrontRowBollards(byte[] kind, int w)
        {
            int count = 0;
            for (int x = 0; x < w; x++) if (kind[x] == Bollard) count++;
            return count;
        }

        /// <summary>True when every non-bollard cell connects to the front row once everything else is gone (gates open).</summary>
        static bool AllReachable(int w, int h, byte[] kind) => CountReachable(w, h, kind, gatesOpen: true, parcelsOnly: false) == CountCells(kind, b => b != Bollard);

        /// <summary>
        /// True when each gate walls something off on its own: with only that gate shut (the others open),
        /// some parcel can't be reached.
        /// </summary>
        static bool EachGateWallsSomethingOff(int w, int h, byte[] kind, List<int> gateCells)
        {
            int parcels = CountCells(kind, b => b == Parcel);
            var trial = new byte[kind.Length];
            foreach (int shut in gateCells)
            {
                Array.Copy(kind, trial, kind.Length);
                foreach (int g in gateCells) if (g != shut) trial[g] = Hole;
                if (CountReachable(w, h, trial, gatesOpen: false, parcelsOnly: true) == parcels) return false;
            }
            return true;
        }

        static int CountCells(byte[] kind, Func<byte, bool> match)
        {
            int count = 0;
            foreach (byte b in kind) if (match(b)) count++;
            return count;
        }

        static int CountReachable(int w, int h, byte[] kind, bool gatesOpen, bool parcelsOnly)
        {
            bool Passable(int i) => kind[i] != Bollard && (gatesOpen || kind[i] != Gate);
            var reach = new bool[kind.Length];
            var queue = new Queue<int>();
            for (int x = 0; x < w; x++)
            {
                if (!Passable(x)) continue;
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
                    if (reach[ni] || !Passable(ni)) continue;
                    reach[ni] = true;
                    queue.Enqueue(ni);
                }
            }
            int count = 0;
            for (int i = 0; i < kind.Length; i++)
                if (reach[i] && (!parcelsOnly || kind[i] == Parcel)) count++;
            return count;
        }

        // ---- blockers ---------------------------------------------------------------------------

        /// <summary>Padlocks sit in the front three rows where they're seen; keys are anywhere in the top half.</summary>
        static bool PickLocks(LevelPlan plan, Random rng, byte[] kind, int[] keyOf, List<int> lockCells)
        {
            int w = plan.Width, h = plan.Height;
            var used = new bool[kind.Length];
            for (int i = 0; i < plan.Locks; i++)
            {
                int locked = -1, key = -1;
                for (int attempt = 0; attempt < 200 && locked < 0; attempt++)
                {
                    int l = rng.Next(w * Math.Min(3, h));
                    int k = rng.Next(w * Math.Max(2, (h + 1) / 2));
                    if (l == k || kind[l] != Parcel || kind[k] != Parcel || used[l] || used[k]) continue;
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

        /// <summary>Mystery parcels start buried (never free at the start) and stay clear of padlocks and keys.</summary>
        static bool[] PickMystery(LevelPlan plan, Random rng, byte[] kind, int[] keyOf, List<int> lockCells)
        {
            int w = plan.Width, h = plan.Height, n = w * h;
            var mystery = new bool[n];
            if (plan.Mystery == 0) return mystery;

            var excluded = new bool[n];
            var free = new List<int>();
            FreeCells(w, h, kind, new bool[n], free);
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
                if (kind[i] != Parcel || excluded[i] || mystery[i]) continue;
                mystery[i] = true;
                placed++;
            }
            return placed == plan.Mystery ? mystery : null;
        }

        /// <summary>
        /// Frozen crates, each thawing after FrostMinMoves..FrostMaxMoves moves. The ice only matters on a
        /// crate that would otherwise be free soon, so three in four go in the front two rows (front row
        /// or right behind it), the rest anywhere. They stay off padlocks, keys and mystery parcels unless
        /// a twist asks for one; the row-0 twist (L35) puts one in the front row for sure. At most a third
        /// of the front row is frozen, so a level always opens with moves to make. Returns the thaw count
        /// per cell, or null.
        /// </summary>
        static int[] PickFrost(LevelPlan plan, Random rng, byte[] kind, bool[] mystery, int[] keyOf, bool[] isKey)
        {
            int w = plan.Width, h = plan.Height, n = w * h;
            var frost = new int[n];
            if (plan.Frost == 0) return frost;

            bool Plain(int i) => kind[i] == Parcel && !mystery[i] && keyOf[i] < 0 && !isKey[i];
            bool TryFreeze(int i)
            {
                if (frost[i] > 0) return false;
                frost[i] = plan.FrostMinMoves + rng.Next(plan.FrostMaxMoves - plan.FrostMinMoves + 1);
                return true;
            }

            var twistCells = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (plan.FrostOnMystery && kind[i] == Parcel && mystery[i]) twistCells.Add(i);
                if (plan.FrostOnKey && isKey[i]) twistCells.Add(i);
            }
            int placed = 0;
            if (plan.FrostOnMystery || plan.FrostOnKey)
            {
                if (twistCells.Count == 0) return null;
                if (TryFreeze(twistCells[rng.Next(twistCells.Count)])) placed++;
            }
            if (plan.FrostInRow0)
            {
                bool frozen = false;
                for (int attempt = 0; attempt < 20 && !frozen; attempt++)
                {
                    int x = rng.Next(w);
                    frozen = Plain(x) && TryFreeze(x);
                }
                if (!frozen) return null;
                placed++;
            }

            int front = Math.Min(2, h) * w;
            for (int attempt = 0; attempt < 500 && placed < plan.Frost; attempt++)
            {
                int i = rng.NextDouble() < 0.75 ? rng.Next(front) : rng.Next(n);
                if (!Plain(i)) continue;
                if (TryFreeze(i)) placed++;
            }
            if (placed < plan.Frost) return null;

            int frontFrozen = 0, frontParcels = 0;
            for (int x = 0; x < w; x++)
            {
                if (kind[x] != Parcel) continue;
                frontParcels++;
                if (frost[x] > 0) frontFrozen++;
            }
            return frontFrozen * 3 <= frontParcels ? frost : null;
        }

        // ---- dig order --------------------------------------------------------------------------

        /// <summary>
        /// A random legal removal order, or null when nothing can move. A frozen crate becomes diggable
        /// once its thaw count of parcels is gone (each a move; a strapped pair placed later only makes
        /// moves fewer, and the solver checks the result); a padlocked parcel after its key; gate g is a
        /// virtual cell "dug" (opened) once <paramref name="gateMin"/>[g] parcels are gone.
        /// </summary>
        static List<int> DigOrder(int w, int h, byte[] kind, int[] keyOf, int[] frost, List<int> gateCells, int[] gateMin, Random rng)
        {
            int n = w * h;
            var gone = new bool[n];
            var order = new List<int>();
            var free = new List<int>();
            int remaining = gateCells.Count, dug = 0;
            foreach (byte b in kind) if (b == Parcel) remaining++;

            while (remaining > 0)
            {
                FreeCells(w, h, kind, gone, free);
                for (int i = free.Count - 1; i >= 0; i--)
                {
                    int c = free[i];
                    int key = keyOf[c];
                    if ((key >= 0 && !gone[key]) || frost[c] > dug)
                        free.RemoveAt(i);
                }
                for (int g = 0; g < gateCells.Count; g++)
                    if (!gone[gateCells[g]] && dug >= gateMin[g]) free.Add(gateCells[g]);
                if (free.Count == 0) return null;
                int pick = free[rng.Next(free.Count)];
                gone[pick] = true;
                order.Add(pick);
                if (kind[pick] == Parcel) dug++;
                remaining--;
            }
            return order;
        }

        /// <summary>
        /// Parcels not yet gone with a path to row 0 (or in row 0), like Board.GetFreeParcels: holes and
        /// gone cells are open, bollards and unopened gates are walls.
        /// </summary>
        static void FreeCells(int w, int h, byte[] kind, bool[] gone, List<int> result)
        {
            result.Clear();
            int n = w * h;
            bool Open(int i) => kind[i] == Hole || ((kind[i] == Parcel || kind[i] == Gate) && gone[i]);
            var reach = new bool[n];
            var queue = new Queue<int>();
            for (int x = 0; x < w; x++)
            {
                if (!Open(x)) continue;
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
                    if (reach[ni] || !Open(ni)) continue;
                    reach[ni] = true;
                    queue.Enqueue(ni);
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (kind[i] != Parcel || gone[i]) continue;
                bool isFree = i < w;
                for (int d = 0; d < 4 && !isFree; d++)
                {
                    int nx = i % w + DX[d], ny = i / w + DY[d];
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && reach[ny * w + nx]) isFree = true;
                }
                if (isFree) result.Add(i);
            }
        }

        /// <summary>
        /// Noise must not pull a parcel that sits behind a gate ahead of that gate in the truck order (its
        /// truck would wait on a gate that waits on later trucks). Opens the gates in sort order and pushes
        /// each newly reachable region's parcels just behind the gate that opened it.
        /// </summary>
        static void ClampBehindGates(int w, int h, byte[] kind, List<int> gateCells, double[] sortKey)
        {
            if (gateCells.Count == 0) return;
            var gates = new List<int>(gateCells);
            gates.Sort((a, b) => sortKey[a].CompareTo(sortKey[b]));
            var open = new byte[kind.Length];
            Array.Copy(kind, open, kind.Length);
            var before = Reachable(w, h, open);
            foreach (int g in gates)
            {
                open[g] = Hole;
                var after = Reachable(w, h, open);
                for (int i = 0; i < kind.Length; i++)
                {
                    if (kind[i] == Parcel && after[i] && !before[i])
                        sortKey[i] = Math.Max(sortKey[i], sortKey[g] + 0.5);
                }
                before = after;
            }
        }

        /// <summary>Cells reachable from the front row through anything but bollards and closed gates.</summary>
        static bool[] Reachable(int w, int h, byte[] kind)
        {
            var reach = new bool[kind.Length];
            var queue = new Queue<int>();
            for (int x = 0; x < w; x++)
            {
                if (kind[x] == Bollard || kind[x] == Gate) continue;
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
                    if (reach[ni] || kind[ni] == Bollard || kind[ni] == Gate) continue;
                    reach[ni] = true;
                    queue.Enqueue(ni);
                }
            }
            return reach;
        }

        // ---- color and order-dependent cargo ----------------------------------------------------

        /// <summary>
        /// The colors share the trucks as evenly as the count allows (one color flooding the yard makes
        /// every move a match), and the same color rarely comes twice in a row.
        /// </summary>
        static int[] TruckColors(LevelPlan plan, Random rng, int count)
        {
            var palette = new List<int>();
            for (int c = 0; c < ParcelColors.Count; c++) palette.Add(c);
            Shuffle(palette, rng);
            var colors = palette.GetRange(0, plan.Colors);

            var trucks = new int[count];
            for (int i = 0; i < count; i++) trucks[i] = colors[i % colors.Count];
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

        /// <summary>
        /// Gate color C comes from the trucks that fill before the gate opens in the order; count = 3 x
        /// those C trucks, capped (3 at the debut, 6 after). A bigger count is preferred up to the cap, and
        /// two gates take two colors. Null when no truck fills before a gate.
        /// </summary>
        static GateData[] PickGateColors(LevelPlan plan, Random rng, int[] truckColors, List<int> gateCells, int[] parcelsBefore, int w)
        {
            var gates = new GateData[gateCells.Count];
            var used = new List<int>();
            for (int g = 0; g < gates.Length; g++)
            {
                int full = parcelsBefore[g] / LevelPlan.TruckCapacity;
                var perColor = new int[ParcelColors.Count];
                for (int t = 0; t < full; t++) perColor[truckColors[t]]++;
                var candidates = new List<int>();
                int best = 0;
                // The first truck's color would open the gate as a side effect of the opening.
                bool others = false;
                for (int c = 0; c < perColor.Length; c++)
                    if (perColor[c] > 0 && !used.Contains(c) && c != truckColors[0]) others = true;
                for (int c = 0; c < perColor.Length; c++)
                {
                    if (perColor[c] == 0 || used.Contains(c)) continue;
                    if (others && c == truckColors[0]) continue;
                    int count = Math.Min(plan.GateMaxCount, LevelPlan.TruckCapacity * perColor[c]);
                    if (count > best)
                    {
                        best = count;
                        candidates.Clear();
                    }
                    if (count == best) candidates.Add(c);
                }
                if (candidates.Count == 0 || best < plan.GateMinCount) return null;
                int color = candidates[rng.Next(candidates.Count)];
                used.Add(color);
                int cell = gateCells[g];
                gates[g] = new GateData { x = cell % w, y = cell / w, color = ParcelColors.ToCode(color).ToString(), count = best };
            }
            return gates;
        }

        /// <summary>
        /// Ice only matters on a crate its truck wants before it thaws. A crate for one of the first
        /// trucks (group g, whose truck can't load before about 3g moves) gets a thaw count past that,
        /// so the player has to work around it; later crates keep their count.
        /// </summary>
        static void SharpenFrost(Random rng, int[] frost, int[] group)
        {
            for (int c = 0; c < frost.Length; c++)
            {
                if (frost[c] == 0) continue;
                // Thaw a few moves after its truck could first want it, so the player has to wait it out
                // or plan around it.
                int due = LevelPlan.TruckCapacity * group[c] + 4 + rng.Next(3);
                if (due <= LevelDefinition.MaxFrostMoves) frost[c] = Math.Max(frost[c], due);
            }
        }

        /// <summary>
        /// Express parcels sit at dig position p (2 &lt;= p &lt;= min(6, n/3)) on a plain parcel, with
        /// taps = p + 1 + slack and never less than p + 2. Returns taps per cell (0 = none), or null.
        /// </summary>
        static int[] PickExpress(LevelPlan plan, Random rng, List<int> digOrder, bool[] mystery, int[] keyOf, bool[] isKey, int[] frost, int[] group)
        {
            var taps = new int[mystery.Length];
            if (plan.Express == 0) return taps;
            // An early dig position keeps the timer short (playtesters found 10+ moves never bit).
            int last = Math.Min(6, digOrder.Count / 3);
            if (last < 2) return null;
            int placed = 0;
            for (int attempt = 0; attempt < 200 && placed < plan.Express; attempt++)
            {
                int p = 2 + rng.Next(last - 1);
                int c = digOrder[p];
                if (taps[c] > 0 || mystery[c] || keyOf[c] >= 0 || isKey[c] || frost[c] > 0) continue;
                // Prefer one whose truck is still two or more trucks away when it has to go, so shipping it
                // in time means parking it: a slot spent for a while.
                if (attempt < 150 && group[c] < p / LevelPlan.TruckCapacity + 2) continue;
                taps[c] = Math.Min(LevelDefinition.MaxExpressTaps, Math.Max(p + 2, p + 1 + plan.ExpressSlack));
                placed++;
            }
            return placed == plan.Express ? taps : null;
        }

        /// <summary>
        /// Straps join two orthogonally adjacent plain parcels of one color, preferably from the same
        /// truck group. Null when there aren't enough pairs.
        /// </summary>
        static List<(int a, int b)> PickStraps(LevelPlan plan, Random rng, int w, int h, byte[] kind, int[] color, int[] group,
                                               bool[] mystery, int[] keyOf, bool[] isKey, int[] frost, int[] express)
        {
            var straps = new List<(int a, int b)>();
            if (plan.Straps == 0) return straps;
            var strapped = new bool[kind.Length];
            bool Plain(int i) => kind[i] == Parcel && !mystery[i] && keyOf[i] < 0 && !isKey[i] && frost[i] == 0 && express[i] == 0 && !strapped[i];

            var same = new List<(int, int)>();
            var any = new List<(int, int)>();
            while (straps.Count < plan.Straps)
            {
                same.Clear();
                any.Clear();
                for (int i = 0; i < kind.Length; i++)
                {
                    if (!Plain(i)) continue;
                    // Right and down neighbours, so each pair is seen once.
                    int x = i % w, y = i / w;
                    if (x + 1 < w) Consider(i, i + 1);
                    if (y + 1 < h) Consider(i, i + w);
                }
                // Pairs for a truck past the first two, so finding room for both is a question.
                var late = same.FindAll(pr => group[pr.Item1] >= 2);
                var pool = late.Count > 0 ? late : same.Count > 0 ? same : any;
                if (pool.Count == 0) return null;
                var pick = pool[rng.Next(pool.Count)];
                strapped[pick.Item1] = strapped[pick.Item2] = true;
                straps.Add(pick);
            }
            return straps;

            void Consider(int a, int b)
            {
                if (!Plain(b) || color[a] != color[b]) return;
                any.Add((a, b));
                if (group[a] == group[b]) same.Add((a, b));
            }
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
