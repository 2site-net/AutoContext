namespace SharpPilot.Mcp.DotNet.Tools.EditorConfig;

using System.ComponentModel;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

using ModelContextProtocol.Server;

using SharpPilot.EditorConfig;

/// <summary>
/// Named pipe client that delegates EditorConfig resolution to the
/// <c>SharpPilot.EditorConfig</c> service process.
/// </summary>
[McpServerToolType]
public static class EditorConfigReader
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string? s_pipeName;

    /// <summary>
    /// Configures the pipe name used to connect to the EditorConfig service.
    /// </summary>
    internal static void Configure(string pipeName) =>
        s_pipeName = pipeName;

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

        var properties = Resolve(path);

        if (properties is null || properties.Count == 0)
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

    /// <summary>
    /// Resolves the effective editorconfig properties for <paramref name="path"/>
    /// as a dictionary for programmatic use by checkers.
    /// </summary>
    internal static IReadOnlyDictionary<string, string>? Resolve(string? path, string[]? keys = null)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(s_pipeName))
        {
            return null;
        }

        var request = new { filePath = path, keys };
        var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, s_jsonOptions));

        using var client = new NamedPipeClientStream(".", s_pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        client.Connect(5000);

        EditorConfigService.WriteMessageAsync(client, requestBytes).GetAwaiter().GetResult();

        var responseBytes = EditorConfigService.ReadMessageAsync(client).GetAwaiter().GetResult();

        if (responseBytes is null || responseBytes.Length == 0)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(responseBytes);
        var properties = doc.RootElement.GetProperty("properties");

        if (properties.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        var result = new Dictionary<string, string>();

        foreach (var prop in properties.EnumerateObject())
        {
            result[prop.Name] = prop.Value.GetString() ?? string.Empty;
        }

        return result.Count == 0 ? null : result;
    }
}
