namespace AutoContext.Engine.Tests.Support.Pipes;

using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Framework.Pipes;

/// <summary>
/// Client-side handshake and JSON-RPC helpers for the engine
/// integration suite. The cross-process companion to
/// <c>EngineRpcTestClient</c> in the in-process Engine.Core test
/// assembly — duplicated here because that helper is
/// <c>internal</c> to its own assembly and the integration suite
/// runs in a separate test project.
/// </summary>
public static class EngineWireTestClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReadResponseTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Connects to <paramref name="kind"/> on the engine identified
    /// by <paramref name="workspacePath"/> + <paramref name="instanceId"/>.
    /// </summary>
    public static async Task<NamedPipeClientStream> ConnectAsync(
        EndpointKind kind,
        string workspacePath,
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        var hash = WorkspaceHash.Compute(workspacePath);
        var pipeName = new Endpoint(kind, hash.Value, instanceId).ToString();
        var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        try
        {
            await client.ConnectAsync(ConnectTimeout, cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Connects to <paramref name="kind"/> on <paramref name="engine"/>'s
    /// endpoint, resolving the workspace + instance id from the
    /// spawned process.
    /// </summary>
    public static Task<NamedPipeClientStream> ConnectAsync(
        EndpointKind kind,
        EngineTestProcess engine,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return ConnectAsync(kind, engine.WorkspacePath, engine.InstanceId, cancellationToken);
    }

    /// <summary>Writes the mandatory <c>Engine.Hello</c> first frame.</summary>
    public static async Task SendHelloAsync(
        LengthPrefixedFrameCodec codec,
        int protocolVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codec);

        var paramsElement = JsonSerializer.SerializeToElement(
            new JsonHandshakeParams { ProtocolVersion = protocolVersion },
            ProtocolJsonContext.Default.JsonHandshakeParams);

        var request = new JsonRpcRequest
        {
            JsonRpc = JsonRpcVersion.Value,
            Id = JsonDocument.Parse("1").RootElement,
            Method = ProtocolMethods.Hello,
            Params = paramsElement,
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);

        await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes a parameter-less JSON-RPC request with the given id and method.</summary>
    public static async Task SendRequestAsync(
        LengthPrefixedFrameCodec codec,
        int id,
        string method,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codec);

        var idElement = JsonDocument
            .Parse(id.ToString(CultureInfo.InvariantCulture))
            .RootElement;
        var request = new JsonRpcRequest
        {
            JsonRpc = JsonRpcVersion.Value,
            Id = idElement,
            Method = method,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);
        await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes a JSON-RPC request carrying <paramref name="parameters"/>.</summary>
    public static async Task SendRequestAsync(
        LengthPrefixedFrameCodec codec,
        int id,
        string method,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codec);

        var idElement = JsonDocument
            .Parse(id.ToString(CultureInfo.InvariantCulture))
            .RootElement;
        var request = new JsonRpcRequest
        {
            JsonRpc = JsonRpcVersion.Value,
            Id = idElement,
            Method = method,
            Params = parameters,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);
        await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Completes the <c>Engine.Hello</c> handshake and the
    /// <c>Engine.Shutdown</c> exchange against <paramref name="engine"/>'s
    /// rpc endpoint, then awaits the process exit. The graceful-shutdown
    /// dance every multi-engine integration test repeats.
    /// </summary>
    public static async Task ShutdownGracefullyAsync(
        EngineTestProcess engine,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var rpc = await ConnectAsync(EndpointKind.Rpc, engine, cancellationToken)
            .ConfigureAwait(false);
        await using var rpcDisposer = rpc.ConfigureAwait(false);
        var codec = new LengthPrefixedFrameCodec(rpc);

        await SendHelloAsync(codec, ProtocolVersion.Current, cancellationToken).ConfigureAwait(false);
        await ReadResponseAsync(codec, "Engine.Hello response", cancellationToken).ConfigureAwait(false);
        await SendRequestAsync(codec, id: 2, ProtocolMethods.Shutdown, cancellationToken).ConfigureAwait(false);
        await ReadResponseAsync(codec, "Engine.Shutdown response", cancellationToken).ConfigureAwait(false);

        await engine.Process
            .WaitForExitAsync(cancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads exactly one JSON-RPC response frame, naming <paramref name="context"/>
    /// in any timeout failure so a wedged engine fails fast with a diagnostic
    /// rather than hanging the run.
    /// </summary>
    public static Task<JsonRpcResponse> ReadResponseAsync(
        LengthPrefixedFrameCodec codec,
        string context,
        CancellationToken cancellationToken)
        => ReadResponseAsync(codec, ReadResponseTimeout, context, cancellationToken);

    /// <summary>
    /// Reads exactly one JSON-RPC response frame, bounding the read with
    /// <paramref name="timeout"/> rather than the default deadline. Used when
    /// a single response is expected to take longer than usual — for example
    /// the first <c>McpTools.Invoke</c> that lazily cold-spawns a worker
    /// process. Reading the slow response inside one bounded wait keeps the
    /// request/response stream in sync; a caller that timed out and retried on
    /// the same pipe could read the late frame as the next response.
    /// </summary>
    public static async Task<JsonRpcResponse> ReadResponseAsync(
        LengthPrefixedFrameCodec codec,
        TimeSpan timeout,
        string context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codec);

        var bytes = await ReadFrameBytesAsync(codec, timeout, context, cancellationToken)
            .ConfigureAwait(false);

        var response = JsonSerializer.Deserialize(
            bytes, ProtocolJsonContext.Default.JsonRpcResponse);
        Assert.NotNull(response);
        return response!;
    }

    /// <summary>
    /// Reads exactly one server-streaming JSON-RPC frame, naming
    /// <paramref name="context"/> in any timeout failure.
    /// </summary>
    public static async Task<JsonRpcStreamFrame> ReadStreamFrameAsync(
        LengthPrefixedFrameCodec codec,
        string context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codec);

        var bytes = await ReadFrameBytesAsync(codec, ReadResponseTimeout, context, cancellationToken)
            .ConfigureAwait(false);

        var frame = JsonSerializer.Deserialize(
            bytes, ProtocolJsonContext.Default.JsonRpcStreamFrame);
        Assert.NotNull(frame);
        return frame!;
    }

    /// <summary>
    /// Reads one length-prefixed frame, racing the pipe read against a
    /// <see cref="Task.Delay(TimeSpan, CancellationToken)"/> backstop so the
    /// harness never hangs silently when the engine stops responding. The
    /// backstop fires independently of whether the pipe read observes
    /// cancellation; a timeout throws a <see cref="TimeoutException"/> naming
    /// <paramref name="context"/>, while caller cancellation surfaces as an
    /// <see cref="OperationCanceledException"/> instead of a false timeout.
    /// </summary>
    private static async Task<byte[]> ReadFrameBytesAsync(
        LengthPrefixedFrameCodec codec,
        TimeSpan timeout,
        string context,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var readTask = codec.ReadAsync(cts.Token);
        var completed = await Task
            .WhenAny(readTask, Task.Delay(timeout, cts.Token))
            .ConfigureAwait(false);

        if (completed != readTask)
        {
            // The read lost the race (timed out or the caller cancelled).
            // Cancel to nudge the pipe read, then observe the orphaned task so
            // its eventual fault — when the owning stream is disposed during
            // unwind — does not escalate to an UnobservedTaskException.
            await cts.CancelAsync().ConfigureAwait(false);
            ObserveOrphanedRead(readTask);

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException(
                $"No frame within {timeout.TotalSeconds:0}s waiting for {context}.");
        }

        var bytes = await readTask.ConfigureAwait(false);
        Assert.NotNull(bytes);
        return bytes!;
    }

    private static void ObserveOrphanedRead(Task<byte[]?> readTask)
        => _ = readTask.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
