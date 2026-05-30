namespace AutoContext.Mcp.Server.Registry;

/// <summary>
/// One MCP Tool definition — the Copilot-facing surface that dispatches one
/// or more <see cref="JsonMcpTaskDefinition"/> entries when invoked.
/// </summary>
public sealed record JsonMcpToolDefinition
{
    /// <summary>MCP Tool name (snake_case). Unique across the whole registry.</summary>
    public required string Name { get; init; }

    /// <summary>MCP Tool description shown to Copilot.</summary>
    public required string Description { get; init; }

    /// <summary>Parameter map (camelCase names) → parameter spec.</summary>
    public required IReadOnlyDictionary<string, JsonMcpToolParameter> Parameters { get; init; }

    /// <summary>MCP Tasks dispatched when this tool is invoked.</summary>
    public required IReadOnlyList<JsonMcpTaskDefinition> Tasks { get; init; }
}
