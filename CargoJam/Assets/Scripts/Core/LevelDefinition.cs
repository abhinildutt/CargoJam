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

        readonly int[] colors;
        readonly bool[] mystery;

        LevelDefinition(LevelData data, int width, int height, int[] trucks, int[] colors, bool[] mystery, ParcelLock[] locks)
        {
            Id = data.id;
            Hard = data.hard;
            Width = width;
            Height = height;
            HoldingSlots = data.holdingSlots;
            TruckCapacity = data.truckCapacity;
            Trucks = trucks;
            Locks = locks;
            this.colors = colors;
            this.mystery = mystery;
        }

        /// <summary>Color at a cell, or <see cref="ParcelColors.None"/> when empty.</summary>
        public int ColorAt(int x, int y) => colors[y * Width + x];

        /// <summary>True when the cell holds a mystery parcel (a lowercase letter in the file).</summary>
        public bool IsMystery(int x, int y) => mystery[y * Width + x];

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
            var colors = new int[width * height];
            var mystery = new bool[width * height];
            var parcelCounts = new int[ParcelColors.Count];

            for (int y = 0; y < height; y++)
            {
                string row = data.rows[y] ?? string.Empty;
                if (row.Length != width)
                {
                    errors.Add($"row {y} has {row.Length} cells, expected {width}");
                    // Keep the cells empty so lock checks don't report the same row again.
                    for (int x = 0; x < width; x++) colors[y * width + x] = ParcelColors.None;
                    continue;
                }
                for (int x = 0; x < width; x++)
                {
                    char ch = row[x];
                    if (ch == '.')
                    {
                        colors[y * width + x] = ParcelColors.None;
                        continue;
                    }
                    int color = ParcelColors.FromCode(ch);
                    if (color < 0)
                    {
                        colors[y * width + x] = ParcelColors.None;
                        errors.Add($"unknown color '{ch}' at ({x},{y})");
                        continue;
                    }
                    colors[y * width + x] = color;
                    mystery[y * width + x] = char.IsLower(ch);
                    parcelCounts[color]++;
                }
            }

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

            for (int c = 0; c < ParcelColors.Count; c++)
            {
                int needed = truckCounts[c] * data.truckCapacity;
                if (parcelCounts[c] != needed)
                    errors.Add($"color {ParcelColors.ToCode(c)} has {parcelCounts[c]} parcels but its trucks hold {needed}");
            }

            var locks = ValidateLocks(data.locks, width, height, colors, errors);

            if (errors.Count == 0)
                definition = new LevelDefinition(data, width, height, trucks, colors, mystery, locks);
            return errors;
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
