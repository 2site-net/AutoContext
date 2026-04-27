namespace AutoContext.Mcp.Server.Registry;

using System.Text.Json;

using Microsoft.Extensions.Logging;

/// <summary>
/// Loads and parses <c>mcp-workers-registry.json</c> into a typed <see cref="McpWorkersCatalog"/>.
/// </summary>
public static partial class RegistryLoader
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
    public static McpWorkersCatalog Parse(
        string json,
        string source = "(memory)",
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (logger is not null)
        {
            LogRegistryParsing(logger, source);
        }

        try
        {
            return JsonSerializer.Deserialize<McpWorkersCatalog>(json, Options)
                ?? throw new JsonException("Registry deserialized to null.");
        }
        catch (JsonException ex)
        {
            if (logger is not null)
            {
                LogRegistryParseFailed(logger, source, ex);
            }

            throw;
        }
    }

    /// <summary>
    /// Reads the registry file at <paramref name="path"/> and parses it.
    /// </summary>
    public static async Task<McpWorkersCatalog> LoadAsync(
        string path,
        CancellationToken ct,
        ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (logger is not null)
        {
            LogRegistryLoadingFromPath(logger, path);
        }

        var stream = File.OpenRead(path);
        await using (stream.ConfigureAwait(false))
        {
            try
            {
                return await JsonSerializer.DeserializeAsync<McpWorkersCatalog>(stream, Options, ct).ConfigureAwait(false)
                    ?? throw new JsonException($"Registry at '{path}' deserialized to null.");
            }
            catch (JsonException ex)
            {
                if (logger is not null)
                {
                    LogRegistryParseFailed(logger, path, ex);
                }

                throw;
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Parsing MCP registry from source '{Source}'.")]
    private static partial void LogRegistryParsing(ILogger logger, string source);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Loading MCP registry from path '{Path}'.")]
    private static partial void LogRegistryLoadingFromPath(ILogger logger, string path);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "Failed to parse MCP registry from source '{Source}'.")]
    private static partial void LogRegistryParseFailed(ILogger logger, string source, Exception exception);
}
