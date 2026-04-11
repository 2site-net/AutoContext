namespace AutoContext.Mcp.Shared.EditorConfig;

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Mcp.Shared.EditorConfig.Protocol;

/// <summary>
/// Named pipe client that delegates EditorConfig resolution to the
/// <c>AutoContext.WorkspaceServer</c> service process.
/// </summary>
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
