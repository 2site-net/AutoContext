namespace AutoContext.Mcp.Tools.Registry;

/// <summary>
/// Strongly-typed representation of <c>mcp-workers-registry.json</c>.
/// </summary>
public sealed record McpWorkersCatalog
{
    /// <summary>
    /// Registry format version (currently <c>"1"</c>).
    /// </summary>
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// Worker entries — one per <c>AutoContext.Worker.*</c> process.
    /// </summary>
    public required IReadOnlyList<McpWorker> Workers { get; init; }
}
