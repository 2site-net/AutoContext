namespace AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// Immutable state of a single MCP tool from the <c>mcpTools</c>
/// section of <c>.autocontext.json</c>: whether the whole tool is
/// disabled, and the version its state was captured against. Pure
/// data.
/// </summary>
internal sealed record ConfigMcpTool
{
    /// <summary>
    /// <see langword="true"/> when the whole tool is disabled.
    /// <see langword="null"/> when enabled.
    /// </summary>
    public bool? Disabled { get; init; }

    /// <summary>
    /// The MCP tool name this entry applies to.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The MAJOR.MINOR version the entry was captured against.
    /// <see langword="null"/> when unset.
    /// </summary>
    public string? Version { get; init; }
}
