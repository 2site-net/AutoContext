namespace SharpPilot.Mcp.DotNet.Protocol;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Request to resolve EditorConfig properties for a file.
/// </summary>
/// <param name="FilePath">Absolute path to the file being checked.</param>
/// <param name="Keys">Optional subset of keys to resolve.</param>
internal sealed record EditorConfigRequest(string FilePath, string[]? Keys = null)
{
    /// <summary>Gets the request type discriminator.</summary>
    [SuppressMessage("Performance", "CA1822",
        Justification = "Must be an instance property for JSON serialization.")]
    public string Type => "editorconfig";
}
