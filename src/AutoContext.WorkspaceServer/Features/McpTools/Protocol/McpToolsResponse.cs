namespace AutoContext.WorkspaceServer.Features.McpTools.Protocol;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Response containing the resolved mode and data for each requested MCP tool.
/// </summary>
/// <param name="McpTools">Per-tool results with mode and optional data.</param>
internal sealed record McpToolsResponse(McpToolEditorConfigResult[] McpTools)
{
    /// <summary>Gets the response type discriminator.</summary>
    [SuppressMessage("Performance", "CA1822",
        Justification = "Must be an instance property for JSON serialization.")]
    public string Type => "mcp-tools";
}
