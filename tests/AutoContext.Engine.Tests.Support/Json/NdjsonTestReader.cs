namespace AutoContext.Engine.Tests.Support.Json;

using System.Text.Json;

public static class NdjsonTestReader
{
    public static List<JsonElement> Read(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var lines = File.ReadAllLines(path);
        var records = new List<JsonElement>(lines.Length);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                records.Add(JsonDocument.Parse(line).RootElement.Clone());
            }
        }

        return records;
    }
}
