namespace AutoContext.Mcp.Tools.Manifest;

/// <summary>
/// The MCP-facing contract for an MCP Tool: how it appears to Copilot.
/// </summary>
public sealed record ManifestToolDefinition
{
    /// <summary>MCP Tool name (snake_case). Unique across the whole manifest.</summary>
    public required string Name { get; init; }

    /// <summary>MCP Tool description shown to Copilot.</summary>
    public required string Description { get; init; }

    /// <summary>Parameter map (camelCase names) → parameter spec.</summary>
    public required IReadOnlyDictionary<string, ManifestParameter> Parameters { get; init; }
}
