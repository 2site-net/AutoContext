namespace AutoContext.Engine.Core.Workspace.Config;

/// <summary>
/// Immutable state of a single MCP tool from the <c>mcpTools</c>
/// section of <c>.autocontext.json</c>: whether the whole tool is
/// disabled, the version its state was captured against, and the
/// individual tasks turned off within it. Pure data.
/// </summary>
internal sealed record McpToolConfig
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
    /// The individual tasks whose state is recorded for this tool, in
    /// the order they appear on disk.
    /// </summary>
    public McpTask[] Tasks { get; init; } = [];

    /// <summary>
    /// The MAJOR.MINOR version the entry was captured against.
    /// <see langword="null"/> when unset.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Immutable state of a single task within an MCP tool. Tasks are
    /// independent of the parent tool's disabled state. Pure data.
    /// </summary>
    internal sealed record McpTask
    {
        /// <summary>
        /// <see langword="true"/> when the task is disabled.
        /// <see langword="null"/> when enabled.
        /// </summary>
        public bool? Disabled { get; init; }

        /// <summary>
        /// The task name this entry applies to.
        /// </summary>
        public string? Name { get; init; }
    }
}
