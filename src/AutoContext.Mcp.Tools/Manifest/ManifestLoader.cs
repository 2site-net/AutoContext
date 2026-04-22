namespace AutoContext.Mcp.Tools.Manifest;

using System.Text.Json;

/// <summary>
/// Loads and parses <c>.mcp-tools.json</c> into a typed <see cref="Manifest"/>.
/// </summary>
public static class ManifestLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Parses the supplied JSON text into a <see cref="Manifest"/>.
    /// </summary>
    /// <exception cref="JsonException">If the JSON is malformed or fails type binding.</exception>
    public static Manifest Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<Manifest>(json, Options)
            ?? throw new JsonException("Manifest deserialized to null.");
    }

    /// <summary>
    /// Reads the manifest file at <paramref name="path"/> and parses it.
    /// </summary>
    public static async Task<Manifest> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var stream = File.OpenRead(path);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<Manifest>(stream, Options, ct).ConfigureAwait(false)
                ?? throw new JsonException($"Manifest at '{path}' deserialized to null.");
        }
    }
}
