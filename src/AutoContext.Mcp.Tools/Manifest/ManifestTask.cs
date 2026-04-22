namespace AutoContext.Mcp.Tools.Manifest;

/// <summary>
/// One MCP Task — the worker-facing execution unit dispatched by
/// <c>McpToolClient</c> over a named pipe to its group's worker.
/// </summary>
public sealed record ManifestTask
{
    /// <summary>MCP Task identifier (snake_case). Unique within its group.</summary>
    public required string Name { get; init; }

    /// <summary>Semantic version (MAJOR.MINOR.PATCH).</summary>
    public required string Version { get; init; }

    /// <summary>
    /// Optional UI description; may be omitted on single-task tools where the
    /// tool description suffices.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Parallelism grouping. Same nonzero priority → run concurrently; ascending
    /// order between groups; <c>0</c>/omitted → sequential after prioritized groups.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>EditorConfig keys this task consumes (snake_case identifiers).</summary>
    public IReadOnlyList<string> EditorConfig { get; init; } = [];
}
