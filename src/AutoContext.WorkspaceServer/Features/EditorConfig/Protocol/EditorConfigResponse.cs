namespace AutoContext.WorkspaceServer.Features.EditorConfig.Protocol;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Response containing the resolved <c>.editorconfig</c> properties.
/// </summary>
/// <param name="Properties">The resolved key-value pairs that apply to the requested file.</param>
internal sealed record EditorConfigResponse(Dictionary<string, string> Properties)
{
    /// <summary>Gets the response type discriminator.</summary>
    [SuppressMessage("Performance", "CA1822",
        Justification = "Must be an instance property for JSON serialization.")]
    public string Type => "editorconfig";
}
