namespace AutoContext.WorkspaceServer.Hosting.EditorConfig;

using System.Diagnostics.CodeAnalysis;

using global::EditorConfig.Core;

/// <summary>
/// Resolves the effective <c>.editorconfig</c> properties for a given file path.
/// Uses <see cref="EditorConfigParser"/> to walk the directory tree, evaluate
/// glob patterns, and cascade sections — returning the final resolved key-value pairs.
/// </summary>
[SuppressMessage("Performance", "CA1822",
    Justification = "Registered as a DI service; instance method allows future state.")]
internal sealed class EditorConfigResolver
{
    /// <summary>
    /// Resolves all effective editorconfig properties for <paramref name="filePath"/>.
    /// </summary>
    internal Dictionary<string, string> Resolve(string filePath)
    {
        var parser = new EditorConfigParser();
        var config = parser.Parse(filePath);

        return config.Properties.Count == 0
            ? []
            : new Dictionary<string, string>(config.Properties);
    }
}
