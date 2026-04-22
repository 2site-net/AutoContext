namespace AutoContext.Mcp.Tools.Manifest;

/// <summary>
/// One group within a worker — bundles MCP Tools (Copilot-facing) plus the
/// MCP Tasks they dispatch (worker-facing).
/// </summary>
public sealed record ManifestGroup
{
    /// <summary>UI label for the group (display only).</summary>
    public required string Tag { get; init; }

    /// <summary>Group-level description shown in the extension UI.</summary>
    public required string Description { get; init; }

    /// <summary>Transport-agnostic endpoint identifier (today: a named pipe name).</summary>
    public required string Endpoint { get; init; }

    /// <summary>MCP Tool definitions — what Copilot sees.</summary>
    public required IReadOnlyList<ManifestTool> Tools { get; init; }

    /// <summary>MCP Task definitions — what the worker executes.</summary>
    public required IReadOnlyList<ManifestTask> Tasks { get; init; }
}
