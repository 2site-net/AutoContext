namespace AutoContext.Framework.Pipes.Tests;

using System.Diagnostics;

using AutoContext.Framework.Pipes;
using AutoContext.Framework.Tests.Support.Encodings;
using AutoContext.Framework.Tests.Support.Pipes;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class PipePersistentExchangeClientTests
{

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
    [Trait("Category", "Integration")]
    public async Task Should_round_trip_a_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = PipeTestServer.UniqueName("actx-ppe-test");
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var serverTask = bound.RunAsync(PipeEchoServer.EchoLoopAsync, cts.Token);
        await using var client = new PipePersistentExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name,
            NullLogger<PipePersistentExchangeClient>.Instance);

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
    [Trait("Category", "Integration")]
    public async Task Should_reuse_the_connection_across_multiple_exchanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = PipeTestServer.UniqueName("actx-ppe-test");
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var connections = 0;
        var serverTask = bound.RunAsync(
            async (stream, ct) =>
            {
                Interlocked.Increment(ref connections);
                await PipeEchoServer.EchoLoopAsync(stream, ct);
            },
            cts.Token);
        await using var client = new PipePersistentExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name,
            NullLogger<PipePersistentExchangeClient>.Instance);

        try
        {
            _ = await client.ExchangeAsync(TestEncodings.Utf8NoBom.GetBytes("one"), cancellationToken);
            _ = await client.ExchangeAsync(TestEncodings.Utf8NoBom.GetBytes("two"), cancellationToken);
            _ = await client.ExchangeAsync(TestEncodings.Utf8NoBom.GetBytes("three"), cancellationToken);

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
    [Trait("Category", "Integration")]
    public async Task Should_complete_dispose_while_an_exchange_is_stuck()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = PipeTestServer.UniqueName("actx-ppe-test");
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var accepted = new SemaphoreSlim(initialCount: 0, maxCount: 1);
        // Server accepts and reads the request, then answers nothing and
        // holds the connection open for the rest of the test.
        var serverTask = bound.RunAsync(
            async (stream, ct) =>
            {
                var codec = new LengthPrefixedFrameCodec(stream);
                _ = await codec.ReadAsync(ct);
                accepted.Release();
                await Task.Delay(Timeout.Infinite, ct);
            },
            cts.Token);
        var client = new PipePersistentExchangeClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name,
            NullLogger<PipePersistentExchangeClient>.Instance);

        try
        {
            // Never completes on its own: the exchange holds the gate
            // waiting for a response the server will never send, and it
            // carries no token of its own to cancel it.
            var stuck = client.ExchangeAsync(
                TestEncodings.Utf8NoBom.GetBytes("hi"), CancellationToken.None);
            await accepted.WaitAsync(cancellationToken);

            var started = Stopwatch.GetTimestamp();
            await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            var elapsed = Stopwatch.GetElapsedTime(started);

            // Dispose closes the handle and returns; it does not wait on
            // the exchange, so teardown is not paced by the dead peer.
            Assert.True(elapsed < TimeSpan.FromSeconds(1), $"Dispose took {elapsed}.");

            // Dispose aborts the exchange by closing the handle, and which
            // side of that race the pending read observes is timing: a read
            // already inside the OS call surfaces a torn pipe, one that has
            // not re-entered it sees the closed stream instead. Both are the
            // abort. The shape that must never appear is the gate reporting
            // itself disposed — that would mean teardown tore the semaphore
            // down under the exchange's release and masked the real cause.
            var abort = await Assert.ThrowsAnyAsync<Exception>(async () => await stuck);

            Assert.True(
                abort is IOException or ObjectDisposedException,
                $"Expected the abort to surface as a pipe failure, got {abort}.");
            Assert.DoesNotContain("semaphore", abort.Message, StringComparison.OrdinalIgnoreCase);
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
            PipeTestServer.UniqueName("actx-ppe-test"),
            NullLogger<PipePersistentExchangeClient>.Instance);
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await client.ExchangeAsync([0x01], CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_throw_IOException_when_peer_closes_without_responding()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = PipeTestServer.UniqueName("actx-ppe-test");
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
                async () => await client.ExchangeAsync(TestEncodings.Utf8NoBom.GetBytes("hi"), cancellationToken));
        }
        finally
        {
            await cts.CancelAsync();
            await serverTask;
            await bound.DisposeAsync();
        }
    }
}
