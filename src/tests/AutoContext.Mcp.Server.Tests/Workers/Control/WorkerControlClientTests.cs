namespace AutoContext.Mcp.Server.Tests.Workers.Control;

using System.Text.Json;

using AutoContext.Mcp.Server.Tests.Testing.Utils;
using AutoContext.Mcp.Server.Workers.Control;
using AutoContext.Mcp.Server.Workers.Protocol;
using AutoContext.Framework.Transport;

public sealed class WorkerControlClientTests
{
    private static EnsureRunningRequest DeserializeRequest(byte[] bytes) =>
        JsonSerializer.Deserialize<EnsureRunningRequest>(bytes, WorkerJsonOptions.Instance)
            ?? throw new InvalidOperationException("Null request payload.");

    private static byte[] SerializeResponse(EnsureRunningResponse response) =>
        JsonSerializer.SerializeToUtf8Bytes(response, WorkerJsonOptions.Instance);

    /// <summary>
    /// Persistent in-process pipe server: accepts one client connection
    /// and answers an arbitrary number of length-framed requests using
    /// <paramref name="handler"/> until the client closes the pipe.
    /// </summary>
    private static Task RunPersistentAsync(
        string pipeName,
        Func<EnsureRunningRequest, EnsureRunningResponse> handler,
        CancellationToken ct,
        Action<int>? onRequest = null) =>
        Task.Run(async () =>
        {
            var server = PipeServerHarness.Create(pipeName);

            await using (server.ConfigureAwait(false))
            {
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                var channel = new LengthPrefixedFrameCodec(server);

                var i = 0;
                while (!ct.IsCancellationRequested)
                {
                    var requestBytes = await channel.ReadAsync(ct).ConfigureAwait(false);
                    if (requestBytes is null)
                    {
                        return; // client closed the pipe
                    }

                    var request = DeserializeRequest(requestBytes);
                    onRequest?.Invoke(i++);

                    var response = handler(request);
                    var responseBytes = SerializeResponse(response);
                    await channel.WriteAsync(responseBytes, ct).ConfigureAwait(false);
                }
            }
        }, ct);

    [Fact]
    public async Task Should_no_op_when_pipe_name_is_null_or_empty()
    {
        await using var client = new WorkerControlClient(pipeName: null);

        Assert.True(client.IsNoOp);

        // Must complete without throwing and without any pipe traffic.
        await client.EnsureRunningAsync("workspace", TestContext.Current.CancellationToken);
        await client.EnsureRunningAsync("workspace", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_round_trip_a_ready_response()
    {
        var pipeName = PipeServerHarness.UniquePipeName();
        await using var client = new WorkerControlClient(pipeName);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var serverTask = RunPersistentAsync(
            pipeName,
            request =>
            {
                Assert.Equal("ensureRunning", request.Type);
                Assert.Equal("workspace", request.WorkerId);
                return new EnsureRunningResponse { Status = EnsureRunningResponse.StatusReady };
            },
            cts.Token);

        await client.EnsureRunningAsync("workspace", cts.Token);

        await client.DisposeAsync();
        await serverTask;
    }

    [Fact]
    public async Task Should_throw_WorkerControlException_on_failed_response()
    {
        var pipeName = PipeServerHarness.UniquePipeName();
        await using var client = new WorkerControlClient(pipeName);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var serverTask = RunPersistentAsync(
            pipeName,
            _ => new EnsureRunningResponse
            {
                Status = EnsureRunningResponse.StatusFailed,
                Error = "spawn ENOENT",
            },
            cts.Token);

        var ex = await Assert.ThrowsAsync<WorkerControlException>(
            () => client.EnsureRunningAsync("dotnet", cts.Token));

        Assert.Equal("dotnet", ex.WorkerId);
        Assert.Contains("spawn ENOENT", ex.Message, StringComparison.Ordinal);

        await client.DisposeAsync();
        await serverTask;
    }

    [Fact]
    public async Task Should_reuse_a_single_connection_across_sequential_calls()
    {
        var pipeName = PipeServerHarness.UniquePipeName();
        await using var client = new WorkerControlClient(pipeName);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var seen = 0;
        var serverTask = RunPersistentAsync(
            pipeName,
            _ => new EnsureRunningResponse { Status = EnsureRunningResponse.StatusReady },
            cts.Token,
            onRequest: _ => Interlocked.Increment(ref seen));

        await client.EnsureRunningAsync("workspace", cts.Token);
        await client.EnsureRunningAsync("dotnet", cts.Token);
        await client.EnsureRunningAsync("web", cts.Token);

        // The persistent server only accepts one client connection; if
        // the client created multiple connections the pipe-server
        // accept call would have failed. Three round-trips on a single
        // connection prove the persistent-connection contract.
        Assert.Equal(3, seen);

        await client.DisposeAsync();
        await serverTask;
    }

    [Fact]
    public async Task Should_coalesce_concurrent_calls_for_the_same_worker()
    {
        var pipeName = PipeServerHarness.UniquePipeName();
        await using var client = new WorkerControlClient(pipeName);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = 0;

        var serverTask = RunPersistentAsync(
            pipeName,
            _ =>
            {
                Interlocked.Increment(ref observed);
                gate.Task.GetAwaiter().GetResult();
                return new EnsureRunningResponse { Status = EnsureRunningResponse.StatusReady };
            },
            cts.Token);

        var a = client.EnsureRunningAsync("workspace", cts.Token);
        var b = client.EnsureRunningAsync("workspace", cts.Token);

        // Release the (single) round-trip the server is parked on; if
        // coalescing did not work, both calls would queue and the test
        // would deadlock here because the server only services one
        // request before the gate is released a second time.
        gate.SetResult(true);

        await Task.WhenAll(a, b);

        Assert.Equal(1, observed);

        await client.DisposeAsync();
        await serverTask;
    }

    [Fact]
    public async Task Should_not_propagate_one_callers_cancellation_to_a_sibling_call()
    {
        var pipeName = PipeServerHarness.UniquePipeName();
        await using var client = new WorkerControlClient(pipeName);

        using var serverCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        serverCts.CancelAfter(TimeSpan.FromSeconds(5));

        // Server holds the response until released so two concurrent
        // callers definitely coalesce onto the same in-flight task.
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = RunPersistentAsync(
            pipeName,
            _ =>
            {
                gate.Task.GetAwaiter().GetResult();
                return new EnsureRunningResponse { Status = EnsureRunningResponse.StatusReady };
            },
            serverCts.Token);

        using var aCts = new CancellationTokenSource();
        var a = client.EnsureRunningAsync("workspace", aCts.Token);
        var b = client.EnsureRunningAsync("workspace", serverCts.Token);

        // Caller A bails out — must not abort caller B's view of the
        // shared round-trip.
        await aCts.CancelAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(() => a);

        // Now release the server response; B should see "ready".
        gate.SetResult(true);
        await b;

        await client.DisposeAsync();
        await serverTask;
    }

    [Fact]
    public async Task Should_throw_after_disposal()
    {
        var client = new WorkerControlClient(PipeServerHarness.UniquePipeName());
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => client.EnsureRunningAsync("workspace", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_throw_on_empty_worker_id()
    {
        await using var client = new WorkerControlClient(pipeName: null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.EnsureRunningAsync(string.Empty, TestContext.Current.CancellationToken));
    }
}
