namespace AutoContext.Framework.Workers.Tests;

using System.IO.Pipes;

using AutoContext.Framework.Tests.Support.Pipes;
using AutoContext.Framework.Workers.Tests.Support;

public sealed class WorkerHealthMonitorServiceTests
{

    [Fact]
    public async Task Should_send_client_id_and_keep_socket_open()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = PipeTestServer.UniqueName("actx-health-test");

        await using var server = PipeTestServer.Create(pipeName, PipeDirection.In);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);

        await using var client = WorkerHealthMonitorServiceTestFactory.Create(pipeName, "dotnet");
        await client.StartAsync(cancellationToken);

        await acceptTask;

        var clientId = await server.ReadClientIdAsync(cancellationToken);

        Assert.Multiple(
            () => Assert.Equal("dotnet", clientId),
            () => Assert.True(server.IsConnected, "Pipe should remain open for the host's lifetime."));
    }

    [Fact]
    public async Task Should_disconnect_when_stopped()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = PipeTestServer.UniqueName("actx-health-test");

        await using var server = PipeTestServer.Create(pipeName, PipeDirection.In);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);

        var client = WorkerHealthMonitorServiceTestFactory.Create(pipeName, "workspace");
        await client.StartAsync(cancellationToken);

        await acceptTask;
        await server.ReadClientIdAsync(cancellationToken);

        await client.StopAsync(cancellationToken);
        await client.DisposeAsync();

        // Reading from the server end after the client has gone away
        // should return 0 (clean disconnect).
        var buffer = new byte[1];
        var read = await server.ReadAsync(buffer, cancellationToken);
        Assert.Equal(0, read);
    }

    [Fact]
    public async Task Should_be_a_noop_when_pipe_name_is_empty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var client = WorkerHealthMonitorServiceTestFactory.Create(pipeName: string.Empty, clientId: "dotnet");
        await client.StartAsync(cancellationToken);

        // Stops without ever having dialled — the test passes if no
        // exception escapes and dispose finishes promptly.
        await client.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Should_dispose_cleanly_when_no_server_is_listening()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = PipeTestServer.UniqueName("actx-health-test");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        await using (var client = WorkerHealthMonitorServiceTestFactory.Create(pipeName, "dotnet"))
        {
            await client.StartAsync(cancellationToken);
            await Task.Delay(100, cancellationToken);
        }

        sw.Stop();

        // The connect timeout is 2s; allow generous CI slack.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(6),
            $"Dispose took {sw.Elapsed.TotalSeconds:F2}s — expected < 6s.");
    }
}
