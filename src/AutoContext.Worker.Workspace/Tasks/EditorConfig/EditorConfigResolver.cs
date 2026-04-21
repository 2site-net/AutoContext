namespace AutoContext.Worker.Workspace.Tasks.EditorConfig;

using global::EditorConfig.Core;

/// <summary>
/// Resolves the effective <c>.editorconfig</c> properties for a given file path.
/// Walks up the directory tree, evaluates glob patterns, and applies section
/// cascading via <see cref="EditorConfigParser"/>.
/// </summary>
internal static class EditorConfigResolver
{
    /// <summary>
    /// Resolves all effective editorconfig properties for <paramref name="filePath"/>.
    /// </summary>
    internal static Dictionary<string, string> Resolve(string filePath)
    {
        var parser = new EditorConfigParser();
        var config = parser.Parse(filePath);

        return config.Properties.Count == 0
            ? []
            : new Dictionary<string, string>(config.Properties);
    }
}
