namespace AutoContext.Mcp.Tools.Registry;

/// <summary>
/// One parameter inside an MCP Tool's input schema.
/// </summary>
public sealed record McpToolParameter
{
    /// <summary>JSON Schema primitive type (e.g. <c>"string"</c>, <c>"number"</c>).</summary>
    public required string Type { get; init; }

    /// <summary>Parameter description shown to Copilot.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// <c>true</c> on required parameters; absent (defaults to <c>false</c>) on optional ones.
    /// </summary>
    public bool Required { get; init; }
}
