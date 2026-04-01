namespace SharpPilot.WorkspaceServer;

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Named pipe server that resolves EditorConfig properties using a
/// length-prefixed binary protocol.  Each connection handles a single
/// request: 4-byte little-endian length + UTF-8 JSON request, followed
/// by a 4-byte little-endian length + UTF-8 JSON response.
/// </summary>
internal sealed partial class WorkspaceService(
    IConfiguration configuration,
    EditorConfigResolver resolver,
    ILogger<WorkspaceService> logger) : BackgroundService
{
    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeName = configuration["pipe"]
            ?? throw new InvalidOperationException("Missing required configuration: --pipe");

        LogStarting(logger, pipeName);

        var handlers = new List<Task>();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var pipe = await AcceptConnectionAsync(pipeName, stoppingToken).ConfigureAwait(false);

                if (pipe is null)
                {
                    break;
                }

                handlers.Add(HandleConnectionAsync(pipe, resolver));
            }
        }
        finally
        {
            await Task.WhenAll(handlers).ConfigureAwait(false);
        }
    }

    private static async Task<NamedPipeServerStream?> AcceptConnectionAsync(
        string pipeName,
        CancellationToken ct)
    {
        NamedPipeServerStream? pipe = null;
        CancellationTokenRegistration registration = default;

        try
        {
            pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            // On Windows, WaitForConnectionAsync does not reliably respond
            // to the cancellation token.  Disposing the pipe from the
            // cancellation callback forces the wait to throw.
            registration = ct.Register(pipe.Dispose);

            await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);

            return pipe;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException) when (ct.IsCancellationRequested)
        {
            // Pipe was disposed by the cancellation callback.
            return null;
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            await registration.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task HandleConnectionAsync(
        NamedPipeServerStream pipe,
        EditorConfigResolver resolver)
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

                var responseBytes = ProcessRequest(requestBytes, resolver);

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

    private static byte[] ProcessRequest(ReadOnlySpan<byte> json, EditorConfigResolver resolver)
    {
        try
        {
            var request = JsonSerializer.Deserialize<EditorConfigRequest>(json, JsonOptions);

            if (request is null || string.IsNullOrWhiteSpace(request.FilePath))
            {
                return JsonSerializer.SerializeToUtf8Bytes(new EditorConfigResponse([]), JsonOptions);
            }

            var properties = resolver.Resolve(request.FilePath, request.Keys);
            var response = new EditorConfigResponse(properties);

            return JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToUtf8Bytes(new EditorConfigResponse([]), JsonOptions);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Workspace service listening on pipe: {PipeName}")]
    private static partial void LogStarting(ILogger logger, string pipeName);
}
