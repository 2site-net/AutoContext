namespace SharpPilot.WorkspaceServer.Features.EditorConfig;

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
    /// Resolves the effective editorconfig properties for <paramref name="filePath"/>.
    /// When <paramref name="keys"/> is provided, only matching keys are returned.
    /// </summary>
    internal Dictionary<string, string> Resolve(string filePath, string[]? keys = null)
    {
        var parser = new EditorConfigParser();
        var config = parser.Parse(filePath);

        if (config.Properties.Count == 0)
        {
            return [];
        }

        if (keys is not { Length: > 0 })
        {
            return new Dictionary<string, string>(config.Properties);
        }

        var keySet = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, string>();

        foreach (var kv in config.Properties)
        {
            if (keySet.Contains(kv.Key))
            {
                result[kv.Key] = kv.Value;
            }
        }

        return result;
    }
}
