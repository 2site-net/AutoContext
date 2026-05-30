namespace AutoContext.Mcp.Server.Tests.Config;

using AutoContext.Framework.Pipes;
using AutoContext.Framework.Tests.Support.Async;
using AutoContext.Mcp.Server.Config;
using AutoContext.Mcp.Server.Tests.Support.Config;
using AutoContext.Mcp.Server.Tests.Support.Shared;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class AutoContextConfigClientTests
{
    [Fact]
    public async Task Should_be_a_no_op_when_pipe_name_is_empty()
    {
        var snapshot = new AutoContextConfigSnapshot();
        await using var client = new AutoContextConfigClient(
            pipeName: string.Empty,
            snapshot,
            EmptyTestServiceProvider.EmptyServices(),
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
        var serverTask = AutoContextConfigPipeServerHarness.RunServerAsync(
            pipeName,
            [
                new JsonAutoContextConfigSnapshot
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
            EmptyTestServiceProvider.EmptyServices(),
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<AutoContextConfigClient>.Instance);

        await client.StartAsync(cts.Token);

        try
        {
            // Poll until the snapshot reflects the pushed frame.
            await AsyncTestHelpers.WaitUntilAsync(() => snapshot.IsToolDisabled("alpha"), cts.Token);

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
        var serverTask = AutoContextConfigPipeServerHarness.RunServerAsync(
            pipeName,
            [
                new JsonAutoContextConfigSnapshot { DisabledTools = ["alpha"] },
                new JsonAutoContextConfigSnapshot { DisabledTools = ["delta"] },
            ],
            release,
            cts.Token);

        await using var client = new AutoContextConfigClient(
            pipeName,
            snapshot,
            EmptyTestServiceProvider.EmptyServices(),
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<AutoContextConfigClient>.Instance);

        await client.StartAsync(cts.Token);

        try
        {
            await AsyncTestHelpers.WaitUntilAsync(() => snapshot.IsToolDisabled("delta"), cts.Token);

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
            EmptyTestServiceProvider.EmptyServices(),
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
}
