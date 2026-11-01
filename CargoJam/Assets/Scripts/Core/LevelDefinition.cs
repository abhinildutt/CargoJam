using System;
using System.Collections.Generic;

namespace CargoJam.Core
{
    public sealed class LevelFormatException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public LevelFormatException(IReadOnlyList<string> errors)
            : base("Invalid level: " + string.Join("; ", errors))
        {
            Errors = errors;
        }
    }

    /// <summary>A validated level, ready to start a <see cref="GameSession"/> from.</summary>
    public sealed class LevelDefinition
    {
        /// <summary>The most customs gates a level may have.</summary>
        public const int MaxGates = 3;

        /// <summary>The most moves a frozen crate may take to thaw (one figure on its ice).</summary>
        public const int MaxFrostMoves = 9;

        /// <summary>The most moves an express parcel may start with.</summary>
        public const int MaxExpressTaps = 99;

        public int Id { get; }

        /// <summary>A marked HARD level (banner, double coins).</summary>
        public bool Hard { get; }

        public int Width { get; }
        public int Height { get; }
        public int HoldingSlots { get; }
        public int TruckCapacity { get; }
        public IReadOnlyList<int> Trucks { get; }

        /// <summary>Padlocks, in file order. Every locked and key cell holds a parcel and there are no cycles.</summary>
        public IReadOnlyList<ParcelLock> Locks { get; }

        /// <summary>Strapped pairs, in file order. Each parcel is in at most one.</summary>
        public IReadOnlyList<ParcelStrap> Straps { get; }

        /// <summary>Customs gates, in file order; a gate index (as in <see cref="GateProgress"/>) indexes this list.</summary>
        public IReadOnlyList<ParcelGate> Gates { get; }

        /// <summary>Bit (1 &lt;&lt; (int)kind) is set for every <see cref="CargoKind"/> this level uses.</summary>
        public int CargoMask { get; }

        readonly int[] colors;
        readonly bool[] mystery;
        readonly bool[] bollards;
        readonly bool[] gateCells;
        readonly int[] frost;
        readonly int[] express;

        LevelDefinition(LevelData data, int[] trucks, Cells cells, ParcelLock[] locks, ParcelStrap[] straps, ParcelGate[] gates)
        {
            Id = data.id;
            Hard = data.hard;
            Width = cells.Width;
            Height = cells.Height;
            HoldingSlots = data.holdingSlots;
            TruckCapacity = data.truckCapacity;
            Trucks = trucks;
            Locks = locks;
            Straps = straps;
            Gates = gates;
            colors = cells.Colors;
            mystery = cells.Mystery;
            bollards = cells.Bollards;
            gateCells = cells.GateCells;
            frost = cells.Frost;
            express = cells.Express;

            int mask = 0;
            if (Array.IndexOf(bollards, true) >= 0) mask |= 1 << (int)CargoKind.Bollard;
            if (Array.Exists(frost, n => n > 0)) mask |= 1 << (int)CargoKind.Frost;
            if (Array.IndexOf(mystery, true) >= 0) mask |= 1 << (int)CargoKind.Mystery;
            if (Array.Exists(express, n => n > 0)) mask |= 1 << (int)CargoKind.Express;
            if (locks.Length > 0) mask |= 1 << (int)CargoKind.Lock;
            if (straps.Length > 0) mask |= 1 << (int)CargoKind.Strap;
            if (gates.Length > 0) mask |= 1 << (int)CargoKind.Gate;
            CargoMask = mask;
        }

        /// <summary>Color at a cell, or <see cref="ParcelColors.None"/> when it holds no parcel (empty, bollard or gate).</summary>
        public int ColorAt(int x, int y) => colors[y * Width + x];

        /// <summary>True when the cell holds a mystery parcel (a lowercase letter in the file).</summary>
        public bool IsMystery(int x, int y) => mystery[y * Width + x];

        /// <summary>True when the cell is a bollard ('#' in the file), a permanent obstacle.</summary>
        public bool IsBollard(int x, int y) => bollards[y * Width + x];

        /// <summary>True when the cell is a customs gate ('+' in the file); see <see cref="Gates"/>.</summary>
        public bool IsGateCell(int x, int y) => gateCells[y * Width + x];

        /// <summary>Moves the parcel at a cell starts frozen for, or 0 when it isn't frozen.</summary>
        public int FrostAt(int x, int y) => frost[y * Width + x];

        /// <summary>Starting moves for the express parcel at a cell, or 0 when it isn't express.</summary>
        public int ExpressAt(int x, int y) => express[y * Width + x];

        /// <summary>True when the level uses this kind of cargo (see <see cref="CargoMask"/>).</summary>
        public bool Uses(CargoKind kind) => (CargoMask & (1 << (int)kind)) != 0;

        public static LevelDefinition Parse(LevelData data)
        {
            var errors = Validate(data, out var definition);
            if (errors.Count > 0) throw new LevelFormatException(errors);
            return definition;
        }

        /// <summary>
        /// Returns every problem with the level. When there are none, <paramref name="definition"/> is set.
        /// The key rule: each color has exactly enough parcels to fill the trucks of that color.
        /// </summary>
        public static List<string> Validate(LevelData data, out LevelDefinition definition)
        {
            definition = null;
            var errors = new List<string>();

            if (data == null)
            {
                errors.Add("no data");
                return errors;
            }
            if (data.rows == null || data.rows.Length == 0) errors.Add("rows are missing");
            if (string.IsNullOrEmpty(data.trucks)) errors.Add("trucks are missing");
            if (data.holdingSlots < 1) errors.Add("holdingSlots must be at least 1");
            if (data.truckCapacity < 1) errors.Add("truckCapacity must be at least 1");
            if (errors.Count > 0) return errors;

            int height = data.rows.Length;
            int width = data.rows[0].Length;
            var cells = new Cells(width, height);
            var colors = cells.Colors;
            var parcelCounts = new int[ParcelColors.Count];
            int parcelTotal = 0;

            for (int y = 0; y < height; y++)
            {
                string row = data.rows[y] ?? string.Empty;
                if (row.Length != width)
                {
                    // The cells stay empty so lock checks don't report the same row again.
                    errors.Add($"row {y} has {row.Length} cells, expected {width}");
                    continue;
                }
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    char ch = row[x];
                    if (ch == '.') continue;
                    if (ch == '#')
                    {
                        cells.Bollards[i] = true;
                        continue;
                    }
                    if (ch == '+')
                    {
                        cells.GateCells[i] = true;
                        continue;
                    }
                    int color = ParcelColors.FromCode(ch);
                    if (color < 0)
                    {
                        errors.Add($"unknown color '{ch}' at ({x},{y})");
                        continue;
                    }
                    colors[i] = color;
                    cells.Mystery[i] = char.IsLower(ch);
                    parcelCounts[color]++;
                    parcelTotal++;
                }
            }
            if (parcelTotal == 0) errors.Add("the level has no parcels");

            var trucks = new int[data.trucks.Length];
            var truckCounts = new int[ParcelColors.Count];
            for (int i = 0; i < data.trucks.Length; i++)
            {
                int color = ParcelColors.FromCode(data.trucks[i]);
                if (color < 0)
                {
                    errors.Add($"unknown truck color '{data.trucks[i]}' at position {i}");
                    continue;
                }
                trucks[i] = color;
                truckCounts[color]++;
            }

            // Only parcel cells count: '#' and '+' hold none, and a strapped pair is simply two parcels.
            for (int c = 0; c < ParcelColors.Count; c++)
            {
                int needed = truckCounts[c] * data.truckCapacity;
                if (parcelCounts[c] != needed)
                    errors.Add($"color {ParcelColors.ToCode(c)} has {parcelCounts[c]} parcels but its trucks hold {needed}");
            }

            var locks = ValidateLocks(data.locks, width, height, colors, errors);
            foreach (var l in locks)
            {
                if (cells.InBounds(l.Locked.X, l.Locked.Y)) cells.LockedOrKey[l.Locked.Y * width + l.Locked.X] = true;
                if (cells.InBounds(l.Key.X, l.Key.Y)) cells.LockedOrKey[l.Key.Y * width + l.Key.X] = true;
            }

            var gates = ValidateGates(data.gates, cells, truckCounts, parcelCounts, errors);
            ReadFrost(data.frost, cells, errors);
            ReadExpress(data.express, cells, errors);
            var straps = ReadStraps(data.straps, cells, errors);
            CheckPlainParcels(data.truckCapacity, straps.Length, cells, errors);

            if (errors.Count == 0)
                definition = new LevelDefinition(data, trucks, cells, locks, straps, gates);
            return errors;
        }

        /// <summary>Per-cell layers read from the rows and the cargo arrays, indexed y * width + x.</summary>
        sealed class Cells
        {
            public readonly int Width;
            public readonly int Height;
            public readonly int[] Colors;
            public readonly bool[] Mystery;
            public readonly bool[] Bollards;
            public readonly bool[] GateCells;
            public readonly int[] Frost;
            public readonly int[] Express;

            // Only for the cross checks: padlocked or key cells, and strapped cells.
            public readonly bool[] LockedOrKey;
            public readonly bool[] Strapped;

            public Cells(int width, int height)
            {
                Width = width;
                Height = height;
                int n = width * height;
                Colors = new int[n];
                for (int i = 0; i < n; i++) Colors[i] = ParcelColors.None;
                Mystery = new bool[n];
                Bollards = new bool[n];
                GateCells = new bool[n];
                Frost = new int[n];
                Express = new int[n];
                LockedOrKey = new bool[n];
                Strapped = new bool[n];
            }

            public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

            public bool HasParcel(int x, int y) => InBounds(x, y) && Colors[y * Width + x] != ParcelColors.None;

            /// <summary>What makes the parcel at cell <paramref name="i"/> more than a plain parcel, or null.</summary>
            public string Special(int i, bool express, bool strapped)
            {
                if (Mystery[i]) return "a mystery parcel";
                if (LockedOrKey[i]) return "padlocked or a key";
                if (Frost[i] > 0) return "frozen";
                if (express && Express[i] > 0) return "an express parcel";
                if (strapped && Strapped[i]) return "strapped";
                return null;
            }
        }

        static ParcelGate[] ValidateGates(GateData[] data, Cells cells, int[] truckCounts, int[] parcelCounts, List<string> errors)
        {
            int count = data?.Length ?? 0;
            if (count > MaxGates) errors.Add($"{count} gates, at most {MaxGates} allowed");

            // Cell index -> the gate entry on that '+', so every '+' gets exactly one.
            var gateOn = new int[cells.Colors.Length];
            for (int i = 0; i < gateOn.Length; i++) gateOn[i] = -1;

            var gates = count == 0 ? Array.Empty<ParcelGate>() : new ParcelGate[count];
            for (int i = 0; i < count; i++)
            {
                var g = data[i];
                if (g == null)
                {
                    errors.Add($"gate {i} is empty");
                    continue;
                }

                int color = g.color != null && g.color.Length == 1 ? ParcelColors.FromCode(g.color[0]) : ParcelColors.None;
                gates[i] = new ParcelGate(new Cell(g.x, g.y), color, g.count);

                if (!cells.InBounds(g.x, g.y) || !cells.GateCells[g.y * cells.Width + g.x])
                    errors.Add($"gate {i}: no '+' at ({g.x},{g.y})");
                else if (gateOn[g.y * cells.Width + g.x] >= 0)
                    errors.Add($"gate {i}: ({g.x},{g.y}) already has a gate");
                else
                    gateOn[g.y * cells.Width + g.x] = i;

                if (color < 0)
                {
                    errors.Add($"gate {i}: unknown color '{g.color}'");
                    continue;
                }
                if (truckCounts[color] == 0)
                    errors.Add($"gate {i}: no {ParcelColors.ToCode(color)} truck to count it down");
                if (g.count < 1 || g.count > parcelCounts[color])
                    errors.Add($"gate {i}: count {g.count} must be 1..{parcelCounts[color]} (the {ParcelColors.ToCode(color)} parcels)");
            }

            for (int i = 0; i < gateOn.Length; i++)
                if (cells.GateCells[i] && gateOn[i] < 0)
                    errors.Add($"gate at ({i % cells.Width},{i / cells.Width}) has no entry in gates");
            return gates;
        }

        static void ReadFrost(FrostData[] data, Cells cells, List<string> errors)
        {
            if (data == null) return;
            for (int i = 0; i < data.Length; i++)
            {
                var f = data[i];
                if (f == null)
                {
                    errors.Add($"frost {i} is empty");
                    continue;
                }
                if (!cells.HasParcel(f.x, f.y))
                {
                    errors.Add($"frost {i}: no parcel at ({f.x},{f.y})");
                    continue;
                }
                int cell = f.y * cells.Width + f.x;
                if (cells.Frost[cell] > 0)
                {
                    errors.Add($"frost {i}: parcel at ({f.x},{f.y}) is already frozen");
                    continue;
                }
                if (f.layers < 1 || f.layers > MaxFrostMoves)
                {
                    errors.Add($"frost {i}: layers {f.layers} must be 1..{MaxFrostMoves} moves");
                    continue;
                }
                cells.Frost[cell] = f.layers;
            }
        }

        static void ReadExpress(ExpressData[] data, Cells cells, List<string> errors)
        {
            if (data == null) return;
            for (int i = 0; i < data.Length; i++)
            {
                var e = data[i];
                if (e == null)
                {
                    errors.Add($"express {i} is empty");
                    continue;
                }
                if (!cells.HasParcel(e.x, e.y))
                {
                    errors.Add($"express {i}: no parcel at ({e.x},{e.y})");
                    continue;
                }
                int cell = e.y * cells.Width + e.x;
                if (cells.Express[cell] > 0)
                {
                    errors.Add($"express {i}: parcel at ({e.x},{e.y}) is already express");
                    continue;
                }
                if (e.taps < 1 || e.taps > MaxExpressTaps)
                {
                    errors.Add($"express {i}: taps {e.taps} must be 1..{MaxExpressTaps}");
                    continue;
                }
                cells.Express[cell] = e.taps;
            }
        }

        static ParcelStrap[] ReadStraps(StrapData[] data, Cells cells, List<string> errors)
        {
            if (data == null || data.Length == 0) return Array.Empty<ParcelStrap>();

            var straps = new ParcelStrap[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var s = data[i];
                if (s == null)
                {
                    errors.Add($"strap {i} is empty");
                    continue;
                }
                straps[i] = new ParcelStrap(new Cell(s.x, s.y), new Cell(s.x2, s.y2));
                bool ok = true;
                if (!cells.HasParcel(s.x, s.y))
                {
                    errors.Add($"strap {i}: no parcel at ({s.x},{s.y})");
                    ok = false;
                }
                if (!cells.HasParcel(s.x2, s.y2))
                {
                    errors.Add($"strap {i}: no parcel at ({s.x2},{s.y2})");
                    ok = false;
                }
                if (!ok) continue;

                if (Math.Abs(s.x - s.x2) + Math.Abs(s.y - s.y2) != 1)
                {
                    errors.Add($"strap {i}: ({s.x},{s.y}) and ({s.x2},{s.y2}) are not orthogonally adjacent");
                    continue;
                }
                int a = s.y * cells.Width + s.x;
                int b = s.y2 * cells.Width + s.x2;
                if (cells.Colors[a] != cells.Colors[b])
                {
                    errors.Add($"strap {i}: ({s.x},{s.y}) and ({s.x2},{s.y2}) are different colors");
                    continue;
                }
                if (cells.Strapped[a] || cells.Strapped[b])
                {
                    errors.Add($"strap {i}: a parcel at ({s.x},{s.y}) or ({s.x2},{s.y2}) is already strapped");
                    continue;
                }
                cells.Strapped[a] = true;
                cells.Strapped[b] = true;
            }
            return straps;
        }

        /// <summary>
        /// Express parcels and strapped parcels must otherwise be plain: not a mystery, padlocked, a key
        /// or frozen, and not both express and strapped.
        /// </summary>
        static void CheckPlainParcels(int truckCapacity, int strapCount, Cells cells, List<string> errors)
        {
            if (strapCount > 0 && truckCapacity < 2)
                errors.Add($"straps need truckCapacity of at least 2, not {truckCapacity}");

            for (int i = 0; i < cells.Colors.Length; i++)
            {
                int x = i % cells.Width, y = i / cells.Width;
                if (cells.Express[i] > 0)
                {
                    string why = cells.Special(i, false, true);
                    if (why != null) errors.Add($"express parcel at ({x},{y}) is {why}");
                }
                else if (cells.Strapped[i])
                {
                    string why = cells.Special(i, false, false);
                    if (why != null) errors.Add($"strapped parcel at ({x},{y}) is {why}");
                }
            }
        }

        static ParcelLock[] ValidateLocks(LockData[] data, int width, int height, int[] colors, List<string> errors)
        {
            if (data == null || data.Length == 0) return Array.Empty<ParcelLock>();

            bool HasParcel(int x, int y) =>
                x >= 0 && x < width && y >= 0 && y < height && colors[y * width + x] != ParcelColors.None;

            var locks = new ParcelLock[data.Length];
            // Cell index -> index of the lock on that cell, so cycles can be followed key by key.
            var lockOn = new int[width * height];
            for (int i = 0; i < lockOn.Length; i++) lockOn[i] = -1;

            int before = errors.Count;
            for (int i = 0; i < data.Length; i++)
            {
                var l = data[i];
                if (l == null)
                {
                    errors.Add($"lock {i} is empty");
                    continue;
                }
                locks[i] = new ParcelLock(new Cell(l.x, l.y), new Cell(l.keyX, l.keyY));
                bool ok = true;
                if (!HasParcel(l.x, l.y))
                {
                    errors.Add($"lock {i}: no parcel at locked cell ({l.x},{l.y})");
                    ok = false;
                }
                if (!HasParcel(l.keyX, l.keyY))
                {
                    errors.Add($"lock {i}: no parcel at key cell ({l.keyX},{l.keyY})");
                    ok = false;
                }
                if (l.x == l.keyX && l.y == l.keyY)
                {
                    errors.Add($"lock {i}: parcel at ({l.x},{l.y}) can't be its own key");
                    ok = false;
                }
                if (!ok) continue;

                int cell = l.y * width + l.x;
                if (lockOn[cell] >= 0)
                {
                    errors.Add($"lock {i}: parcel at ({l.x},{l.y}) already has a lock");
                    continue;
                }
                lockOn[cell] = i;
            }
            if (errors.Count > before) return locks;

            // Each parcel has at most one lock, so following "locked -> its key -> the key's lock" is a
            // single chain; meeting a lock already on the chain means nothing in it can ever open.
            var seenOnChain = new int[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                int current = i;
                while (current >= 0)
                {
                    if (seenOnChain[current] == i + 1)
                    {
                        errors.Add($"locks form a cycle through ({locks[current].Locked.X},{locks[current].Locked.Y})");
                        return locks;
                    }
                    seenOnChain[current] = i + 1;
                    var key = locks[current].Key;
                    current = lockOn[key.Y * width + key.X];
                }
            }
            return locks;
        }
    }
}
