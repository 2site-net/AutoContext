namespace AutoContext.Framework.Tests.Pipes;

using System.Text;

using AutoContext.Framework.Tests.Testing.Utils;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class PipePersistentExchangeClientTests
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    [Fact]
    public void Should_reject_empty_pipe_name()
    {
        Assert.Throws<ArgumentException>(
            () => new PipePersistentExchangeClient(
                new PipeTransport(NullLogger<PipeTransport>.Instance),
                string.Empty,
                NullLogger<PipePersistentExchangeClient>.Instance));
    }

    [Fact]
    public async Task Should_round_trip_a_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = NewPipeName();
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var serverTask = bound.RunAsync(EchoLoopAsync, cts.Token);
        await using var client = new PipePersistentExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name,
            NullLogger<PipePersistentExchangeClient>.Instance);

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
    public async Task Should_reuse_the_connection_across_multiple_exchanges()
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
                await EchoLoopAsync(stream, ct);
            },
            cts.Token);
        await using var client = new PipePersistentExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name,
            NullLogger<PipePersistentExchangeClient>.Instance);

        try
        {
            _ = await client.ExchangeAsync(Utf8NoBom.GetBytes("one"), cancellationToken);
            _ = await client.ExchangeAsync(Utf8NoBom.GetBytes("two"), cancellationToken);
            _ = await client.ExchangeAsync(Utf8NoBom.GetBytes("three"), cancellationToken);

            Assert.Equal(1, Volatile.Read(ref connections));
        }
        finally
        {
            await cts.CancelAsync();
            await serverTask;
            await bound.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_throw_when_used_after_dispose()
    {
        var client = new PipePersistentExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NewPipeName(),
            NullLogger<PipePersistentExchangeClient>.Instance);
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await client.ExchangeAsync([0x01], CancellationToken.None));
    }

    [Fact]
    public async Task Should_throw_IOException_when_peer_closes_without_responding()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = NewPipeName();
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Server reads the request then closes without writing back.
        var serverTask = bound.RunAsync(
            async (stream, ct) =>
            {
                var codec = new LengthPrefixedFrameCodec(stream);
                _ = await codec.ReadAsync(ct);
            },
            cts.Token);
        await using var client = new PipePersistentExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name,
            NullLogger<PipePersistentExchangeClient>.Instance);

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

    private static string NewPipeName() => TestPipeServer.UniqueName("actx-ppe-test");

    private static async Task EchoLoopAsync(Stream stream, CancellationToken cancellationToken)
    {
        var codec = new LengthPrefixedFrameCodec(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            var request = await codec.ReadAsync(cancellationToken);
            if (request is null)
            {
                return;
            }

            var response = Utf8NoBom.GetBytes("pong:" + Utf8NoBom.GetString(request));
            await codec.WriteAsync(response, cancellationToken);
        }
    }
}
