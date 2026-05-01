namespace AutoContext.Mcp.Server.Tests.Config;

using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Framework.Transport;
using AutoContext.Mcp.Server.Config;
using AutoContext.Mcp.Server.Tests.Testing.Utils;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class AutoContextConfigClientTests
{
    private static byte[] SerializeDto(AutoContextConfigSnapshotDto dto) =>
        JsonSerializer.SerializeToUtf8Bytes(dto);

    private static ServiceProvider EmptyServices() =>
        new ServiceCollection().BuildServiceProvider();

    /// <summary>
    /// Spins up a one-shot named-pipe server: accepts a single
    /// client connection, then writes <paramref name="frames"/> in
    /// order (each as a length-prefixed message) and returns once
    /// the test signals via <paramref name="release"/>.
    /// </summary>
    private static Task RunServerAsync(
        string pipeName,
        IReadOnlyList<AutoContextConfigSnapshotDto> frames,
        TaskCompletionSource release,
        CancellationToken ct) =>
        Task.Run(async () =>
        {
            var server = PipeServerHarness.Create(pipeName, PipeDirection.Out);

            await using (server.ConfigureAwait(false))
            {
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                var channel = new LengthPrefixedFrameCodec(server);

                foreach (var frame in frames)
                {
                    await channel.WriteAsync(SerializeDto(frame), ct).ConfigureAwait(false);
                }

                // Hold the connection open until the test releases us
                // so the client's read loop has a chance to observe
                // and apply every frame.
                using (ct.Register(() => release.TrySetResult()))
                {
                    await release.Task.ConfigureAwait(false);
                }
            }
        }, ct);

    [Fact]
    public async Task Should_be_a_no_op_when_pipe_name_is_empty()
    {
        var snapshot = new AutoContextConfigSnapshot();
        await using var client = new AutoContextConfigClient(
            pipeName: string.Empty,
            snapshot,
            EmptyServices(),
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<AutoContextConfigClient>.Instance);

        await client.StartAsync(TestContext.Current.CancellationToken);
        await client.StopAsync(TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Empty(snapshot.DisabledTools),
            () => Assert.Empty(snapshot.DisabledTasks));
    }

    [Fact]
    public async Task Should_apply_initial_snapshot_frame_to_the_snapshot()
    {
        var pipeName = PipeServerHarness.UniquePipeName();
        var snapshot = new AutoContextConfigSnapshot();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = RunServerAsync(
            pipeName,
            [
                new AutoContextConfigSnapshotDto
                {
                    DisabledTools = ["alpha", "beta"],
                    DisabledTasks = new Dictionary<string, List<string>>
                    {
                        ["beta"] = ["scan"],
                    },
                },
            ],
            release,
            cts.Token);

        await using var client = new AutoContextConfigClient(
            pipeName,
            snapshot,
            EmptyServices(),
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<AutoContextConfigClient>.Instance);

        await client.StartAsync(cts.Token);

        try
        {
            // Poll until the snapshot reflects the pushed frame.
            await WaitUntilAsync(() => snapshot.IsToolDisabled("alpha"), cts.Token);

            Assert.Multiple(
                () => Assert.True(snapshot.IsToolDisabled("alpha")),
                () => Assert.True(snapshot.IsToolDisabled("beta")),
                () => Assert.True(snapshot.IsTaskDisabled("beta", "scan")),
                () => Assert.False(snapshot.IsToolDisabled("gamma")));
        }
        finally
        {
            release.TrySetResult();
            await client.StopAsync(cts.Token);
            await serverTask;
        }
    }

    [Fact]
    public async Task Should_apply_subsequent_frames_to_the_snapshot()
    {
        var pipeName = PipeServerHarness.UniquePipeName();
        var snapshot = new AutoContextConfigSnapshot();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = RunServerAsync(
            pipeName,
            [
                new AutoContextConfigSnapshotDto { DisabledTools = ["alpha"] },
                new AutoContextConfigSnapshotDto { DisabledTools = ["delta"] },
            ],
            release,
            cts.Token);

        await using var client = new AutoContextConfigClient(
            pipeName,
            snapshot,
            EmptyServices(),
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<AutoContextConfigClient>.Instance);

        await client.StartAsync(cts.Token);

        try
        {
            await WaitUntilAsync(() => snapshot.IsToolDisabled("delta"), cts.Token);

            Assert.Multiple(
                () => Assert.False(snapshot.IsToolDisabled("alpha")),
                () => Assert.True(snapshot.IsToolDisabled("delta")));
        }
        finally
        {
            release.TrySetResult();
            await client.StopAsync(cts.Token);
            await serverTask;
        }
    }

    [Fact]
    public async Task Should_complete_StopAsync_when_the_server_never_appears()
    {
        // No server running on this pipe — the client's connect attempt
        // will time out (5 s cap inside the implementation), but the
        // test cancels it first via StopAsync, which must return
        // promptly without hanging.
        var pipeName = PipeServerHarness.UniquePipeName();
        var snapshot = new AutoContextConfigSnapshot();

        await using var client = new AutoContextConfigClient(
            pipeName,
            snapshot,
            EmptyServices(),
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<AutoContextConfigClient>.Instance);

        await client.StartAsync(TestContext.Current.CancellationToken);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await client.StopAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(4),
            $"StopAsync should be prompt; took {stopwatch.Elapsed}.");
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken ct)
    {
        while (!predicate())
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(20, ct).ConfigureAwait(false);
        }
    }
}
