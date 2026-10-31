using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using CargoJam.Core;

namespace CargoJam.LevelGen
{
    /// <summary>Reads and writes level files in the same hand-readable shape the game ships.</summary>
    public static class LevelJson
    {
        public static string Write(LevelData d)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"id\": ").Append(d.id.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"hard\": ").Append(d.hard ? "true" : "false").Append(",\n");
            sb.Append("  \"holdingSlots\": ").Append(d.holdingSlots).Append(",\n");
            sb.Append("  \"truckCapacity\": ").Append(d.truckCapacity).Append(",\n");
            sb.Append("  \"trucks\": \"").Append(d.trucks).Append("\",\n");
            sb.Append("  \"rows\": [\n");
            for (int i = 0; i < d.rows.Length; i++)
                sb.Append("    \"").Append(d.rows[i]).Append(i + 1 < d.rows.Length ? "\",\n" : "\"\n");
            sb.Append("  ]");
            AppendArray(sb, "locks", d.locks, l => $"{{ \"x\": {l.x}, \"y\": {l.y}, \"keyX\": {l.keyX}, \"keyY\": {l.keyY} }}");
            AppendArray(sb, "frost", d.frost, f => $"{{ \"x\": {f.x}, \"y\": {f.y}, \"layers\": {f.layers} }}");
            AppendArray(sb, "express", d.express, e => $"{{ \"x\": {e.x}, \"y\": {e.y}, \"taps\": {e.taps} }}");
            AppendArray(sb, "straps", d.straps, s => $"{{ \"x\": {s.x}, \"y\": {s.y}, \"x2\": {s.x2}, \"y2\": {s.y2} }}");
            AppendArray(sb, "gates", d.gates, g => $"{{ \"x\": {g.x}, \"y\": {g.y}, \"color\": \"{g.color}\", \"count\": {g.count} }}");
            sb.Append("\n}\n");
            return sb.ToString();
        }

        /// <summary>Appends <c>,"name": [ ... ]</c> with one entry per line; nothing when the array is empty.</summary>
        static void AppendArray<T>(StringBuilder sb, string name, T[] items, Func<T, string> entry)
        {
            if (items == null || items.Length == 0) return;
            sb.Append(",\n  \"").Append(name).Append("\": [\n");
            for (int i = 0; i < items.Length; i++)
                sb.Append("    ").Append(entry(items[i])).Append(i + 1 < items.Length ? ",\n" : "\n");
            sb.Append("  ]");
        }

        public static LevelData Read(string path)
        {
            var options = new JsonSerializerOptions { IncludeFields = true };
            return JsonSerializer.Deserialize<LevelData>(File.ReadAllText(path), options);
        }
    }
}
