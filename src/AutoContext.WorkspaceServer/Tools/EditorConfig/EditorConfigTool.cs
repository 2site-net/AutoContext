namespace AutoContext.WorkspaceServer.Tools.EditorConfig;

using System.ComponentModel;
using System.Text;

using ModelContextProtocol.Server;

using AutoContext.WorkspaceServer.Hosting.EditorConfig;

/// <summary>
/// MCP tool that resolves the effective <c>.editorconfig</c> properties for a
/// file by delegating to <see cref="EditorConfigResolver"/>.
/// </summary>
[McpServerToolType]
internal sealed class EditorConfigTool(EditorConfigResolver resolver)
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
    public string Read(
        [Description("Absolute path to the file whose effective .editorconfig properties should be resolved.")]
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var properties = resolver.Resolve(path);

        if (properties.Count == 0)
        {
            return "⚠️ No .editorconfig properties apply to this file.";
        }

        var sb = new StringBuilder();

        foreach (var kv in properties)
        {
            sb.Append(kv.Key);
            sb.Append(" = ");
            sb.AppendLine(kv.Value);
        }

        return sb.ToString().TrimEnd();
    }
}
