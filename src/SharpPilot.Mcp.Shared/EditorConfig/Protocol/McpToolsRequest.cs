namespace SharpPilot.Mcp.Shared.EditorConfig.Protocol;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Request to determine the run mode for a set of MCP tools.
/// </summary>
/// <param name="FilePath">Absolute path to the file being checked.</param>
/// <param name="McpTools">The tools whose mode should be resolved.</param>
internal sealed record McpToolsRequest(string FilePath, McpToolEditorConfigEntry[] McpTools)
{
    /// <summary>Gets the request type discriminator.</summary>
    [SuppressMessage("Performance", "CA1822",
        Justification = "Must be an instance property for JSON serialization.")]
    public string Type
        => "mcp-tools";
}
