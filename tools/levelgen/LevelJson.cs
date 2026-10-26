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
            if (d.locks != null && d.locks.Length > 0)
            {
                sb.Append(",\n  \"locks\": [\n");
                for (int i = 0; i < d.locks.Length; i++)
                {
                    var l = d.locks[i];
                    sb.Append($"    {{ \"x\": {l.x}, \"y\": {l.y}, \"keyX\": {l.keyX}, \"keyY\": {l.keyY} }}");
                    sb.Append(i + 1 < d.locks.Length ? ",\n" : "\n");
                }
                sb.Append("  ]");
            }
            sb.Append("\n}\n");
            return sb.ToString();
        }

        public static LevelData Read(string path)
        {
            var options = new JsonSerializerOptions { IncludeFields = true };
            return JsonSerializer.Deserialize<LevelData>(File.ReadAllText(path), options);
        }
    }
}
