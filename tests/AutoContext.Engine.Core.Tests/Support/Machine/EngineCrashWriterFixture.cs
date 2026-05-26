namespace AutoContext.Engine.Core.Tests.Support.Machine;

using System.Text.Json;

using AutoContext.Engine.Core.Machine;

/// <summary>
/// Shared helpers for tests that exercise
/// <see cref="EngineCrashWriter"/>. Each call to
/// <see cref="CreateOptions"/> mints a fresh
/// <see cref="EngineOptions.CacheRootOverride"/> under a
/// throwaway temporary directory so concurrent test cases do
/// not collide on the per-instance subtree.
/// </summary>
public static class EngineCrashWriterFixture
{
    public static EngineOptions CreateOptions(string? cacheRootOverride = null) =>
        new()
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
            CacheRootOverride = cacheRootOverride ?? CreateTempCacheRoot(),
        };

    public static string CreateTempCacheRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"autocontext-crashwriter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public static EngineCrashWriter CreateWriter(EngineOptions options) => new(options);

    /// <summary>
    /// Reads <see cref="EngineCrashWriter.CrashLogPath"/> as
    /// NDJSON and deserializes each non-empty line into a
    /// generic dictionary so tests can assert on individual
    /// fields without coupling to the writer's private record
    /// types.
    /// </summary>
    internal static IReadOnlyList<JsonElement> ReadRecords(EngineCrashWriter writer)
    {
        if (!File.Exists(writer.CrashLogPath))
        {
            return [];
        }

        var lines = File.ReadAllLines(writer.CrashLogPath);
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
