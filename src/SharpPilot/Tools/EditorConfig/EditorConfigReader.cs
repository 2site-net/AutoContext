namespace SharpPilot.Tools.EditorConfig;

using System.ComponentModel;
using System.Text;

using global::EditorConfig.Core;

using ModelContextProtocol.Server;

/// <summary>
/// Resolves the effective <c>.editorconfig</c> properties for a given file path.
/// Uses <see cref="EditorConfigParser"/> to walk the directory tree, evaluate
/// glob patterns, and cascade sections — returning the final resolved key-value
/// pairs that apply to the file.
/// </summary>
[McpServerToolType]
public static class EditorConfigReader
{
    /// <summary>
    /// Resolves the effective editorconfig properties for <paramref name="path"/>.
    /// </summary>
    [McpServerTool(Name = "get_editorconfig", ReadOnly = true, Idempotent = true)]
    [Description(
        "Resolves the effective .editorconfig properties for a given file path. " +
        "Walks up the directory tree, evaluates glob patterns and section cascading, " +
        "and returns the final resolved key-value pairs that apply to the file. " +
        "Use this tool to understand the coding style rules (indent style, charset, " +
        "end-of-line, etc.) that apply to a specific file.")]
    public static string Read(
        [Description("Absolute path to the file whose effective .editorconfig properties should be resolved.")]
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var parser = new EditorConfigParser();
        var config = parser.Parse(path);

        if (config.Properties.Count == 0)
        {
            return "⚠️ No .editorconfig properties apply to this file.";
        }

        var sb = new StringBuilder();

        foreach (var kv in config.Properties)
        {
            sb.Append(kv.Key);
            sb.Append(" = ");
            sb.AppendLine(kv.Value);
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Resolves the effective editorconfig properties for <paramref name="path"/>
    /// as a dictionary for programmatic use by checkers.
    /// </summary>
    internal static IReadOnlyDictionary<string, string>? Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parser = new EditorConfigParser();
        var config = parser.Parse(path);

        return config.Properties.Count == 0 ? null : config.Properties;
    }
}
