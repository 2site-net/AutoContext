namespace AutoContext.Framework.Tests.Transport;

using System.IO.Pipes;

using AutoContext.Framework.Tests.Testing.Utils;
using AutoContext.Framework.Transport;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class PipeTransportTests
{
    [Fact]
    public async Task Should_reject_empty_pipe_name()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transport = new PipeTransport(NullLogger<PipeTransport>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await transport.ConnectAsync(string.Empty, cancellationToken: cancellationToken));
    }

    [Fact]
    public async Task Should_throw_when_token_is_already_canceled()
    {
        var transport = new PipeTransport(NullLogger<PipeTransport>.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await transport.ConnectAsync(NewPipeName(), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Should_connect_when_a_server_is_listening()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = NewPipeName();
        await using var server = TestPipeServer.Create(name);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);
        var transport = new PipeTransport(NullLogger<PipeTransport>.Instance);

        await using var client = await transport.ConnectAsync(name, cancellationToken: cancellationToken);
        await acceptTask;

        Assert.True(client.CanWrite);
    }

    [Fact]
    public async Task Should_throw_when_no_server_is_listening_within_timeout()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transport = new PipeTransport(NullLogger<PipeTransport>.Instance);

        await Assert.ThrowsAsync<TimeoutException>(
            async () => await transport.ConnectAsync(NewPipeName(), timeoutMs: 100, cancellationToken: cancellationToken));
    }

    private static string NewPipeName() => TestPipeServer.UniqueName("actx-pt-test");
}
