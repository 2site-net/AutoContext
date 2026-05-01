namespace AutoContext.Framework.Tests.Hosting;

using System.IO.Pipes;
using System.Text;

using AutoContext.Framework.Hosting;
using AutoContext.Framework.Tests.Testing.Utils;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class HealthMonitorClientTests
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    [Fact]
    public async Task Should_send_client_id_and_keep_socket_open()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeName = NewPipeName();

        await using var server = CreateServer(pipeName);
        var acceptTask = server.WaitForConnectionAsync(ct);

        await using var client = NewClient(pipeName, "dotnet");
        await client.StartAsync(ct);

        await acceptTask;

        var clientId = await ReadClientIdAsync(server, ct);

        Assert.Multiple(
            () => Assert.Equal("dotnet", clientId),
            () => Assert.True(server.IsConnected, "Pipe should remain open for the host's lifetime."));
    }

    [Fact]
    public async Task Should_disconnect_when_stopped()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeName = NewPipeName();

        await using var server = CreateServer(pipeName);
        var acceptTask = server.WaitForConnectionAsync(ct);

        var client = NewClient(pipeName, "workspace");
        await client.StartAsync(ct);

        await acceptTask;
        await ReadClientIdAsync(server, ct);

        await client.StopAsync(ct);
        await client.DisposeAsync();

        // Reading from the server end after the client has gone away
        // should return 0 (clean disconnect).
        var buffer = new byte[1];
        var read = await server.ReadAsync(buffer, ct);
        Assert.Equal(0, read);
    }

    [Fact]
    public async Task Should_be_a_noop_when_pipe_name_is_empty()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var client = NewClient(pipeName: string.Empty, clientId: "dotnet");
        await client.StartAsync(ct);

        // Stops without ever having dialled — the test passes if no
        // exception escapes and dispose finishes promptly.
        await client.StopAsync(ct);
    }

    [Fact]
    public async Task Should_dispose_cleanly_when_no_server_is_listening()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeName = NewPipeName();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        await using (var client = NewClient(pipeName, "dotnet"))
        {
            await client.StartAsync(ct);
            await Task.Delay(100, ct);
        }

        sw.Stop();

        // The connect timeout is 2s; allow generous CI slack.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(6),
            $"Dispose took {sw.Elapsed.TotalSeconds:F2}s — expected < 6s.");
    }

    private static HealthMonitorClient NewClient(string pipeName, string clientId) =>
        new(pipeName, clientId, NullLogger<HealthMonitorClient>.Instance);

    private static NamedPipeServerStream CreateServer(string pipeName) =>
        TestPipeServer.Create(pipeName, PipeDirection.In);

    private static string NewPipeName() => TestPipeServer.UniqueName("actx-health-test");

    private static async Task<string> ReadClientIdAsync(Stream server, CancellationToken ct)
    {
        // The client writes the id with no length prefix, so we read
        // until either the buffer fills or the client falls silent.
        var buffer = new byte[64];
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            using var readTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
            readTimeoutCts.CancelAfter(TimeSpan.FromMilliseconds(100));
            try
            {
                var read = await server.ReadAsync(buffer.AsMemory(totalRead), readTimeoutCts.Token);
                if (read == 0)
                {
                    break;
                }
                totalRead += read;
            }
            catch (OperationCanceledException) when (!timeoutCts.IsCancellationRequested)
            {
                // No more bytes within the inner window — assume the
                // client id has been fully transmitted.
                break;
            }
        }

        return Utf8NoBom.GetString(buffer, 0, totalRead);
    }
}
