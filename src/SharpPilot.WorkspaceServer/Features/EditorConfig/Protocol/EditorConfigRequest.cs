namespace SharpPilot.WorkspaceServer.Features.EditorConfig.Protocol;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Request to resolve the effective <c>.editorconfig</c> properties for a file.
/// </summary>
/// <param name="FilePath">Absolute path to the file whose effective .editorconfig properties should be resolved.</param>
/// <param name="Keys">Optional subset of property keys to return. When empty or null, all properties are returned.</param>
internal sealed record EditorConfigRequest(string FilePath, string[]? Keys = null)
{
    /// <summary>Gets the request type discriminator.</summary>
    [SuppressMessage("Performance", "CA1822",
        Justification = "Must be an instance property for JSON serialization.")]
    public string Type => "editorconfig";
}
