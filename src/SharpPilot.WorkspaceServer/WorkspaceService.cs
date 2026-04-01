namespace SharpPilot.WorkspaceServer;

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

/// <summary>
/// Named pipe server that resolves EditorConfig properties using a
/// length-prefixed binary protocol.  Each connection handles a single
/// request: 4-byte little-endian length + UTF-8 JSON request, followed
/// by a 4-byte little-endian length + UTF-8 JSON response.
/// </summary>
internal sealed class WorkspaceService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _pipeName;
    private readonly CancellationToken _ct;

    internal WorkspaceService(string pipeName, CancellationToken ct)
    {
        _pipeName = pipeName;
        _ct = ct;
    }

    /// <summary>
    /// Starts listening for connections on the named pipe. Does not return
    /// until <see cref="_ct"/> is cancelled.
    /// </summary>
    internal async Task RunAsync()
    {
        var handlers = new List<Task>();

        try
        {
            while (!_ct.IsCancellationRequested)
            {
                var pipe = await AcceptConnectionAsync().ConfigureAwait(false);

                if (pipe is null)
                {
                    break;
                }

                handlers.Add(HandleConnectionAsync(pipe));
            }
        }
        finally
        {
            await Task.WhenAll(handlers).ConfigureAwait(false);
        }
    }

    private async Task<NamedPipeServerStream?> AcceptConnectionAsync()
    {
        NamedPipeServerStream? pipe = null;
        CancellationTokenRegistration registration = default;

        try
        {
            pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            // On Windows, WaitForConnectionAsync does not reliably respond
            // to the cancellation token.  Disposing the pipe from the
            // cancellation callback forces the wait to throw.
            registration = _ct.Register(pipe.Dispose);

            await pipe.WaitForConnectionAsync(_ct).ConfigureAwait(false);

            return pipe;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException) when (_ct.IsCancellationRequested)
        {
            // Pipe was disposed by the cancellation callback.
            return null;
        }
        catch (ObjectDisposedException) when (_ct.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            await registration.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task HandleConnectionAsync(NamedPipeServerStream pipe)
    {
        try
        {
            await using (pipe.ConfigureAwait(false))
            {
                var requestBytes = await ReadMessageAsync(pipe).ConfigureAwait(false);

                if (requestBytes is null)
                {
                    return;
                }

                var responseBytes = ProcessRequest(requestBytes);

                await WriteMessageAsync(pipe, responseBytes).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested — exit silently.
        }
        catch (IOException)
        {
            // Client disconnected — exit silently.
        }
        catch (ObjectDisposedException)
        {
            // Pipe disposed during shutdown — exit silently.
        }
    }

    /// <summary>
    /// Reads a length-prefixed message: 4-byte LE int32 length, then that
    /// many bytes of payload.  Returns <c>null</c> when the connection is
    /// closed before a full header is received.
    /// </summary>
    internal static async Task<byte[]?> ReadMessageAsync(Stream stream, CancellationToken ct = default)
    {
        var header = new byte[4];
        var headerRead = await ReadExactAsync(stream, header, ct).ConfigureAwait(false);

        if (!headerRead)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);

        if (length <= 0)
        {
            return [];
        }

        var payload = new byte[length];
        var payloadRead = await ReadExactAsync(stream, payload, ct).ConfigureAwait(false);

        return payloadRead ? payload : null;
    }

    /// <summary>
    /// Writes a length-prefixed message: 4-byte LE int32 length, then the
    /// payload bytes.
    /// </summary>
    internal static async Task WriteMessageAsync(Stream stream, byte[] payload, CancellationToken ct = default)
    {
        var message = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(message, payload.Length);
        payload.CopyTo(message.AsSpan(4));

        await stream.WriteAsync(message, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(offset, buffer.Length - offset), ct).ConfigureAwait(false);

            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    private static byte[] ProcessRequest(ReadOnlySpan<byte> json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<EditorConfigRequest>(json, s_jsonOptions);

            if (request is null || string.IsNullOrWhiteSpace(request.FilePath))
            {
                return JsonSerializer.SerializeToUtf8Bytes(new EditorConfigResponse([]), s_jsonOptions);
            }

            var properties = EditorConfigResolver.Resolve(request.FilePath, request.Keys);
            var response = new EditorConfigResponse(properties);

            return JsonSerializer.SerializeToUtf8Bytes(response, s_jsonOptions);
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToUtf8Bytes(new EditorConfigResponse([]), s_jsonOptions);
        }
    }
}
