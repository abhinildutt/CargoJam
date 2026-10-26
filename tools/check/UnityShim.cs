// Stand-ins for the few UnityEngine APIs the engine-free tests touch, so tools/check can run them
// on plain .NET. Behaviour matches Unity closely enough for test data (public fields, defaults kept).
namespace UnityEngine
{
    public static class JsonUtility
    {
        static readonly System.Text.Json.JsonSerializerOptions Options = new System.Text.Json.JsonSerializerOptions
        {
            IncludeFields = true,
        };

        public static T FromJson<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, Options);
        public static string ToJson(object obj, bool pretty = false) =>
            System.Text.Json.JsonSerializer.Serialize(obj, obj.GetType(),
                new System.Text.Json.JsonSerializerOptions { IncludeFields = true, WriteIndented = pretty });
    }
}
