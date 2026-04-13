namespace AutoContext.Mcp.Shared.McpTools;

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

/// <summary>
/// Named pipe client for the <c>mcp-tools</c> endpoint on the
/// <c>AutoContext.WorkspaceServer</c> service process.
/// </summary>
public sealed class McpToolsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
    };

    private readonly string? _pipeName;
    private readonly string? _workspacePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolsClient"/> class.
    /// </summary>
    /// <param name="pipeName">Named pipe used to connect to the workspace service, or <see langword="null"/> when not available.</param>
    /// <param name="workspacePath">Absolute path to the workspace root, or <see langword="null"/> when not available.</param>
    public McpToolsClient(string? pipeName = null, string? workspacePath = null)
    {
        _pipeName = pipeName;
        _workspacePath = workspacePath;
    }

    /// <summary>
    /// Gets the workspace root path, if configured.
    /// </summary>
    internal string? WorkspacePath => _workspacePath;

    /// <summary>
    /// Resolves tool modes and EditorConfig data for a batch of MCP tools
    /// via the <c>mcp-tools</c> workspace service endpoint.
    /// </summary>
    internal async Task<McpToolResult[]?> ResolveToolsAsync(
        string? filePath, McpToolEntry[] tools)
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
