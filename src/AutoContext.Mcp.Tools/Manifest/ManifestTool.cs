namespace AutoContext.Mcp.Tools.Manifest;

/// <summary>
/// One MCP Tool — the Copilot-facing surface that dispatches one or more
/// <see cref="ManifestTask"/> entries when invoked.
/// </summary>
public sealed record ManifestTool
{
    /// <summary>UI label for the MCP Tool (display only).</summary>
    public required string Tag { get; init; }

    /// <summary>AutoContext-side description shown in the extension UI.</summary>
    public required string Description { get; init; }

    /// <summary>The MCP-facing contract passed to the SDK at registration time.</summary>
    public required ManifestToolDefinition Definition { get; init; }

    /// <summary>
    /// Capability flags the workspace must have for this tool to be registered.
    /// Empty list = always register.
    /// </summary>
    public IReadOnlyList<string> WorkspaceFlags { get; init; } = [];

    /// <summary>
    /// MCP Task names dispatched when this tool is invoked. Each must match a
    /// <see cref="ManifestTask.Name"/> in the sibling <see cref="ManifestGroup.Tasks"/>.
    /// </summary>
    public required IReadOnlyList<string> Tasks { get; init; }
}
