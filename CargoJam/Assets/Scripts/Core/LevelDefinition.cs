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
        public int Width { get; }
        public int Height { get; }
        public int HoldingSlots { get; }
        public int TruckCapacity { get; }
        public IReadOnlyList<int> Trucks { get; }

        readonly int[] colors;

        LevelDefinition(int id, int width, int height, int holdingSlots, int truckCapacity, int[] trucks, int[] colors)
        {
            Id = id;
            Width = width;
            Height = height;
            HoldingSlots = holdingSlots;
            TruckCapacity = truckCapacity;
            Trucks = trucks;
            this.colors = colors;
        }

        /// <summary>Color at a cell, or <see cref="ParcelColors.None"/> when empty.</summary>
        public int ColorAt(int x, int y) => colors[y * Width + x];

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
            var parcelCounts = new int[ParcelColors.Count];

            for (int y = 0; y < height; y++)
            {
                string row = data.rows[y] ?? string.Empty;
                if (row.Length != width)
                {
                    errors.Add($"row {y} has {row.Length} cells, expected {width}");
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
                        errors.Add($"unknown color '{ch}' at ({x},{y})");
                        continue;
                    }
                    colors[y * width + x] = color;
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

            if (errors.Count == 0)
                definition = new LevelDefinition(data.id, width, height, data.holdingSlots, data.truckCapacity, trucks, colors);
            return errors;
        }
    }
}
