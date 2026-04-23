namespace AutoContext.Mcp.Tools.Registry;

/// <summary>
/// One worker entry inside the registry — the worker's full project name,
/// the pipe endpoint it listens on, and the MCP Tool definitions it exposes.
/// </summary>
public sealed record McpWorker
{
    /// <summary>Full project name (e.g. <c>"AutoContext.Worker.DotNet"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Transport-agnostic endpoint identifier (today: a named pipe name).</summary>
    public required string Endpoint { get; init; }

    /// <summary>MCP Tool definitions exposed by this worker.</summary>
    public required IReadOnlyList<McpToolDefinition> Tools { get; init; }
}
