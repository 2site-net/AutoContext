namespace SharpPilot.Mcp.DotNet.Tools.EditorConfig;

using System.Buffers.Binary;
using System.ComponentModel;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

using ModelContextProtocol.Server;

using SharpPilot.Mcp.DotNet.Protocol;

/// <summary>
/// Named pipe client that delegates EditorConfig resolution to the
/// <c>SharpPilot.WorkspaceServer</c> service process.
/// </summary>
[McpServerToolType]
public static class EditorConfigReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
    };

    private static string? _pipeName;
    private static string? _workspacePath;

    /// <summary>
    /// Gets the workspace root path, if configured.
    /// </summary>
    internal static string? WorkspacePath => _workspacePath;

    /// <summary>
    /// Configures the pipe name used to connect to the workspace service.
    /// </summary>
    internal static void Configure(string pipeName, string? workspacePath = null)
    {
        _pipeName = pipeName;
        _workspacePath = workspacePath;
    }

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
    public static async Task<string> ReadAsync(
        [Description("Absolute path to the file whose effective .editorconfig properties should be resolved.")]
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var properties = await ResolveAsync(path).ConfigureAwait(false);

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
    /// Resolves tool modes and EditorConfig data for a batch of MCP tools
    /// via the <c>mcp-tools</c> workspace service endpoint.
    /// </summary>
    internal static async Task<McpToolEditorConfigResult[]?> ResolveToolsAsync(
        string? filePath, McpToolEditorConfigEntry[] tools)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(_pipeName))
        {
            return null;
        }

        var request = new McpToolsRequest(filePath, tools);
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);

        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000).ConfigureAwait(false);

        await WriteMessageAsync(client, requestBytes).ConfigureAwait(false);

        var responseBytes = await ReadMessageAsync(client).ConfigureAwait(false);

        if (responseBytes is null || responseBytes.Length == 0)
        {
            return null;
        }

        var response = JsonSerializer.Deserialize<McpToolsResponse>(responseBytes, JsonOptions);

        return response?.McpTools;
    }

    /// <summary>
    /// Resolves the effective editorconfig properties for <paramref name="path"/>
    /// as a dictionary for programmatic use by checkers.
    /// </summary>
    internal static async Task<IReadOnlyDictionary<string, string>?> ResolveAsync(string? path, string[]? keys = null)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(_pipeName))
        {
            return null;
        }

        var request = new EditorConfigRequest(path, keys);
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);

        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000).ConfigureAwait(false);

        await WriteMessageAsync(client, requestBytes).ConfigureAwait(false);

        var responseBytes = await ReadMessageAsync(client).ConfigureAwait(false);

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

    private static async Task WriteMessageAsync(Stream stream, byte[] payload)
    {
        var message = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(message, payload.Length);
        payload.CopyTo(message.AsSpan(4));

        await stream.WriteAsync(message).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReadMessageAsync(Stream stream)
    {
        var header = new byte[4];

        if (!await ReadExactAsync(stream, header).ConfigureAwait(false))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);

        if (length <= 0)
        {
            return [];
        }

        var payload = new byte[length];

        return await ReadExactAsync(stream, payload).ConfigureAwait(false) ? payload : null;
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(offset, buffer.Length - offset)).ConfigureAwait(false);

            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
