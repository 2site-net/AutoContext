namespace AutoContext.Framework.Tests.Transport;

using System.Text;

using AutoContext.Framework.Tests.Testing.Utils;
using AutoContext.Framework.Transport;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class PipeTransientExchangeClientTests
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    [Fact]
    public void Should_reject_empty_pipe_name()
    {
        Assert.Throws<ArgumentException>(
            () => new PipeTransientExchangeClient(
                new PipeTransport(NullLogger<PipeTransport>.Instance),
                string.Empty));
    }

    [Fact]
    public async Task Should_round_trip_a_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = NewPipeName();
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var serverTask = bound.RunAsync(SingleShotEchoAsync, cts.Token);
        await using var client = new PipeTransientExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name);

        try
        {
            var response = await client.ExchangeAsync(Utf8NoBom.GetBytes("ping"), cancellationToken);

            Assert.Equal("pong:ping", Utf8NoBom.GetString(response));
        }
        finally
        {
            await cts.CancelAsync();
            await serverTask;
            await bound.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_open_a_fresh_connection_per_exchange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = NewPipeName();
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var connections = 0;
        var serverTask = bound.RunAsync(
            async (stream, ct) =>
            {
                Interlocked.Increment(ref connections);
                await SingleShotEchoAsync(stream, ct);
            },
            cts.Token);
        await using var client = new PipeTransientExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name);

        try
        {
            _ = await client.ExchangeAsync(Utf8NoBom.GetBytes("one"), cancellationToken);
            _ = await client.ExchangeAsync(Utf8NoBom.GetBytes("two"), cancellationToken);
            _ = await client.ExchangeAsync(Utf8NoBom.GetBytes("three"), cancellationToken);

            Assert.Equal(3, Volatile.Read(ref connections));
        }
        finally
        {
            await cts.CancelAsync();
            await serverTask;
            await bound.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_throw_IOException_when_peer_closes_without_responding()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = NewPipeName();
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var serverTask = bound.RunAsync(
            async (stream, ct) =>
            {
                var codec = new LengthPrefixedFrameCodec(stream);
                _ = await codec.ReadAsync(ct);
                // Close without writing.
            },
            cts.Token);
        await using var client = new PipeTransientExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name);

        try
        {
            await Assert.ThrowsAsync<IOException>(
                async () => await client.ExchangeAsync(Utf8NoBom.GetBytes("hi"), cancellationToken));
        }
        finally
        {
            await cts.CancelAsync();
            await serverTask;
            await bound.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispose_is_a_noop_and_idempotent()
    {
        await using var client = new PipeTransientExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NewPipeName());

        var ex = await Record.ExceptionAsync(async () =>
        {
            await client.DisposeAsync();
            await client.DisposeAsync();
        });

        Assert.Null(ex);
    }

    private static string NewPipeName() => TestPipeServer.UniqueName("actx-pte-test");

    private static async Task SingleShotEchoAsync(Stream stream, CancellationToken cancellationToken)
    {
        var codec = new LengthPrefixedFrameCodec(stream);
        var request = await codec.ReadAsync(cancellationToken);
        if (request is null)
        {
            return;
        }
        var response = Utf8NoBom.GetBytes("pong:" + Utf8NoBom.GetString(request));
        await codec.WriteAsync(response, cancellationToken);
    }
}
