namespace AutoContext.Mcp.Shared.WorkspaceServer.McpTools;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

/// <summary>
/// Request to resolve tool modes and EditorConfig data for a batch of MCP tools.
/// </summary>
/// <param name="Tools">Tool names whose enabled/disabled status should be resolved.</param>
/// <param name="FilePath">Absolute path to the file being checked, or <see langword="null" /> when not applicable.</param>
/// <param name="EditorConfigKeys">Optional flat set of EditorConfig keys to resolve for <paramref name="FilePath"/>.</param>
internal sealed record McpToolsRequest(
    string[] Tools,
    string? FilePath = null,
    [property: JsonPropertyName("editorconfig-keys")] string[]? EditorConfigKeys = null)
{
    /// <summary>Gets the request type discriminator.</summary>
    [SuppressMessage("Performance", "CA1822",
        Justification = "Must be an instance property for JSON serialization.")]
    public string Type
        => "mcp-tools";
}
