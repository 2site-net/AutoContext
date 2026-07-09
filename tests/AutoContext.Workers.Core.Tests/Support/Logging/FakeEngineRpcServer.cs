namespace AutoContext.Workers.Core.Tests.Support.Logging;

using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Channels;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;
using AutoContext.Framework.Tests.Support.Pipes;

/// <summary>
/// In-process fake of the engine's <c>rpc</c> endpoint for the
/// worker-log tests: accepts one connection, answers the
/// <c>Engine.Hello</c> handshake, and collects every
/// <c>Engine.WriteLog</c> notification the worker sends so a test can
/// await and assert on the delivered records.
/// </summary>
internal sealed class FakeEngineRpcServer(
    string pipeName, int handshakeProtocolVersion = ProtocolVersion.Current) : IAsyncDisposable
{
    private static readonly JsonElement FallbackId = JsonDocument.Parse("1").RootElement.Clone();

    private readonly NamedPipeServerStream _server = PipeTestServer.Create(pipeName, PipeDirection.InOut);
    private readonly Channel<JsonLogRecord> _received =
        Channel.CreateUnbounded<JsonLogRecord>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource _cts = new();
    private Task? _serveTask;

    public void Start()
        => _serveTask = Task.Run(() => ServeAsync(_cts.Token));

    public async Task<IReadOnlyList<JsonLogRecord>> WaitForRecordsAsync(int count, CancellationToken cancellationToken)
    {
        var records = new List<JsonLogRecord>(count);

        for (var i = 0; i < count; i++)
        {
            records.Add(await _received.Reader.ReadAsync(cancellationToken).ConfigureAwait(false));
        }

        return records;
    }

    public async Task<IReadOnlyList<JsonLogRecord>> WaitUntilAsync(
        Func<JsonLogRecord, bool> predicate, CancellationToken cancellationToken)
    {
        var records = new List<JsonLogRecord>();

        while (true)
        {
            var record = await _received.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            records.Add(record);

            if (predicate(record))
            {
                return records;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);

        if (_serveTask is not null)
        {
            try
            {
                await _serveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected — the serve loop observed cancellation.
            }
        }

        await _server.DisposeAsync().ConfigureAwait(false);
        _cts.Dispose();
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            var codec = new LengthPrefixedFrameCodec(_server);

            var helloBytes = await codec.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (helloBytes is null)
            {
                return;
            }

            var hello = JsonSerializer.Deserialize(helloBytes, ProtocolJsonContext.Default.JsonRpcRequest);
            await codec.WriteAsync(BuildHelloResponse(hello), cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                var frameBytes = await codec.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (frameBytes is null)
                {
                    break;
                }

                var notification = JsonSerializer.Deserialize(
                    frameBytes, ProtocolJsonContext.Default.JsonRpcNotification);

                if (notification?.Params is { } payload
                    && payload.Deserialize(ProtocolJsonContext.Default.JsonLogRecord) is { } record)
                {
                    _received.Writer.TryWrite(record);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected — the test disposed the server.
        }
        catch (IOException)
        {
            // The worker closed the pipe — treat as end of stream.
        }
        finally
        {
            _received.Writer.TryComplete();
        }
    }

    private byte[] BuildHelloResponse(JsonRpcRequest? hello)
    {
        var result = JsonSerializer.SerializeToElement(
            new JsonHandshakeResult { ProtocolVersion = handshakeProtocolVersion, EngineVersion = "test-engine" },
            ProtocolJsonContext.Default.JsonHandshakeResult);

        var response = new JsonRpcResponse
        {
            Id = hello?.Id ?? FallbackId,
            Result = result,
        };

        return JsonSerializer.SerializeToUtf8Bytes(response, ProtocolJsonContext.Default.JsonRpcResponse);
    }
}
