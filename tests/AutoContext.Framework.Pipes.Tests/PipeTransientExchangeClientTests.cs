namespace AutoContext.Framework.Pipes.Tests;

using AutoContext.Framework.Pipes;
using AutoContext.Framework.Tests.Support.Encodings;
using AutoContext.Framework.Tests.Support.Pipes;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class PipeTransientExchangeClientTests
{

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
        var name = PipeTestServer.UniqueName("actx-pte-test");
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var serverTask = bound.RunAsync(PipeEchoServer.EchoOnceAsync, cts.Token);
        await using var client = new PipeTransientExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name);

        try
        {
            var response = await client.ExchangeAsync(TestEncodings.Utf8NoBom.GetBytes("ping"), cancellationToken);

            Assert.Equal("pong:ping", TestEncodings.Utf8NoBom.GetString(response));
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
        var name = PipeTestServer.UniqueName("actx-pte-test");
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var connections = 0;
        var serverTask = bound.RunAsync(
            async (stream, ct) =>
            {
                Interlocked.Increment(ref connections);
                await PipeEchoServer.EchoOnceAsync(stream, ct);
            },
            cts.Token);
        await using var client = new PipeTransientExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name);

        try
        {
            _ = await client.ExchangeAsync(TestEncodings.Utf8NoBom.GetBytes("one"), cancellationToken);
            _ = await client.ExchangeAsync(TestEncodings.Utf8NoBom.GetBytes("two"), cancellationToken);
            _ = await client.ExchangeAsync(TestEncodings.Utf8NoBom.GetBytes("three"), cancellationToken);

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
        var name = PipeTestServer.UniqueName("actx-pte-test");
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
                async () => await client.ExchangeAsync(TestEncodings.Utf8NoBom.GetBytes("hi"), cancellationToken));
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
            PipeTestServer.UniqueName("actx-pte-test"));

        var ex = await Record.ExceptionAsync(async () =>
        {
            await client.DisposeAsync();
            await client.DisposeAsync();
        });

        Assert.Null(ex);
    }
}
