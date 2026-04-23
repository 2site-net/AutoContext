namespace AutoContext.Mcp.Server.Registry;

using System.Text.Json;

/// <summary>
/// Loads and parses <c>mcp-workers-registry.json</c> into a typed <see cref="McpWorkersCatalog"/>.
/// </summary>
public static class RegistryLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Parses the supplied JSON text into a <see cref="McpWorkersCatalog"/>.
    /// </summary>
    /// <exception cref="JsonException">If the JSON is malformed or fails type binding.</exception>
    public static McpWorkersCatalog Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<McpWorkersCatalog>(json, Options)
            ?? throw new JsonException("Registry deserialized to null.");
    }

    /// <summary>
    /// Reads the registry file at <paramref name="path"/> and parses it.
    /// </summary>
    public static async Task<McpWorkersCatalog> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var stream = File.OpenRead(path);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<McpWorkersCatalog>(stream, Options, ct).ConfigureAwait(false)
                ?? throw new JsonException($"Registry at '{path}' deserialized to null.");
        }
    }
}
