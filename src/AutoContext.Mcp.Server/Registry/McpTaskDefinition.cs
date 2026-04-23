namespace AutoContext.Mcp.Server.Registry;

/// <summary>
/// One MCP Task — the worker-facing execution unit dispatched by
/// <c>McpToolClient</c> over a named pipe to its worker.
/// </summary>
public sealed record McpTaskDefinition
{
    /// <summary>MCP Task identifier (snake_case). Unique within its tool.</summary>
    public required string Name { get; init; }

    /// <summary>EditorConfig keys this task consumes (snake_case identifiers).</summary>
    public IReadOnlyList<string> EditorConfig { get; init; } = [];
}
