namespace SharpPilot.Mcp.DotNet.Tools.Checkers;

/// <summary>
/// Implemented by checkers that read specific <c>.editorconfig</c> properties
/// from the <c>data</c> bag passed to <see cref="IChecker.Check"/>.
/// The composite checker aggregates keys from all sub-checkers that implement
/// this interface and requests only the needed properties from the
/// workspace service.
/// </summary>
public interface IEditorConfigFilter
{
    /// <summary>
    /// EditorConfig property keys this checker needs.
    /// </summary>
    IReadOnlyList<string> EditorConfigKeys { get; }
}
