namespace AutoContext.Engine.Core.Features.McpTools.Snapshot;

/// <summary>
/// Immutable domain model for a single MCP tool the engine advertises,
/// composed at startup by <see cref="McpToolsRegistryLoader"/> from one
/// <c>tools</c> entry of <c>mcp-tools-registry.json</c>. Carries the
/// model-facing <see cref="Description"/> and <see cref="Parameters"/>
/// contract surfaced over MCP <c>tools/list</c>, the
/// <see cref="WorkerId"/> dispatch target, and the
/// <see cref="Editorconfig"/> keys the engine resolves before invoking
/// the worker.
/// </summary>
internal sealed record McpToolsRegistryTool
{
    /// <summary>The MCP tool name (snake_case); unique across the
    /// registry.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Identifier of the worker that runs the tool; foreign key into
    /// <c>workers.json</c>.
    /// </summary>
    public required string WorkerId { get; init; }

    /// <summary>
    /// The model-facing tool description surfaced over MCP
    /// <c>tools/list</c>.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The tool parameters, in registry declaration order; at least one.
    /// </summary>
    public required IReadOnlyList<McpToolsRegistryParameter> Parameters { get; init; }

    /// <summary>
    /// The EditorConfig keys the tool consumes, in declaration order;
    /// empty when it consumes none.
    /// </summary>
    public IReadOnlyList<string> Editorconfig { get; init; } = [];
}
