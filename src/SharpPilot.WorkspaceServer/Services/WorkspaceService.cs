namespace SharpPilot.WorkspaceServer.Services;

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SharpPilot.WorkspaceServer.Services.Protocol;

/// <summary>
/// Named pipe server that dispatches requests to feature-specific handlers
/// using a length-prefixed binary protocol.  Each connection handles a
/// single request: 4-byte little-endian length + UTF-8 JSON request,
/// followed by a 4-byte little-endian length + UTF-8 JSON response.
/// </summary>
internal sealed partial class WorkspaceService(
    IConfiguration configuration,
    IEnumerable<IRequestHandler> handlers,
    ILogger<WorkspaceService> logger) : BackgroundService
{
    private static readonly byte[] FallbackResponse = """{"properties":{}}"""u8.ToArray();

    private readonly Dictionary<string, IRequestHandler> _handlers =
        handlers.ToDictionary(h => h.RequestType);

    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
    };

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeName = configuration["pipe"]
            ?? throw new InvalidOperationException("Missing required configuration: --pipe");

        LogStarting(logger, pipeName);

        var connections = new List<Task>();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var pipe = await AcceptConnectionAsync(pipeName, stoppingToken).ConfigureAwait(false);

                if (pipe is null)
                {
                    break;
                }

                connections.Add(HandleConnectionAsync(pipe));
            }
        }
        finally
        {
            await Task.WhenAll(connections).ConfigureAwait(false);
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

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe)
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

    private byte[] ProcessRequest(ReadOnlySpan<byte> json)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<WorkspaceRequest>(json, JsonOptions);

            if (envelope?.Type is not null && _handlers.TryGetValue(envelope.Type, out var handler))
            {
                return handler.Process(json);
            }

            // Untyped requests default to the editorconfig handler (backward compatibility).
            return _handlers.TryGetValue("editorconfig", out var fallback)
                ? fallback.Process(json)
                : FallbackResponse;
        }
        catch (JsonException)
        {
            return FallbackResponse;
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Workspace service listening on pipe: {PipeName}")]
    private static partial void LogStarting(ILogger logger, string pipeName);
}
