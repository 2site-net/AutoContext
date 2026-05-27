namespace AutoContext.Framework.Pipes.Tests;

using System.IO.Pipes;

using AutoContext.Framework.Tests.Support.Pipes;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging.Abstractions;

using AutoContext.Framework.Tests.Support.Encodings;

public sealed class PipeKeepAliveClientTests
{

    [Fact]
    public async Task Should_be_a_noop_when_pipe_name_is_empty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var client = new PipeKeepAliveClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<PipeKeepAliveClient>.Instance);

        var ex = await Record.ExceptionAsync(
            async () => await client.StartAsync(string.Empty, ReadOnlyMemory<byte>.Empty, cancellationToken: cancellationToken));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Should_write_the_handshake_to_the_listener()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = PipeTestServer.UniqueName("actx-pka-test");
        await using var server = PipeTestServer.Create(name, PipeDirection.In);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);
        var client = new PipeKeepAliveClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<PipeKeepAliveClient>.Instance);

        try
        {
            // The server-side read must run concurrently with StartAsync;
            // on Windows a small client write blocks until a reader is
            // pumping, so awaiting StartAsync sequentially deadlocks.
            var startTask = client.StartAsync(name, TestEncodings.Utf8NoBom.GetBytes("HEY"), cancellationToken: cancellationToken);
            await acceptTask;
            var buffer = new byte[3];
            await server.ReadExactlyAsync(buffer, cancellationToken);
            await startTask;

            Assert.Equal("HEY", TestEncodings.Utf8NoBom.GetString(buffer));
        }
        finally
        {
            await client.DisposeAsync();
            await server.DrainToDisconnectAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task Should_connect_without_writing_when_handshake_is_empty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = PipeTestServer.UniqueName("actx-pka-test");
        await using var server = PipeTestServer.Create(name, PipeDirection.In);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);
        var client = new PipeKeepAliveClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<PipeKeepAliveClient>.Instance);

        try
        {
            await client.StartAsync(name, ReadOnlyMemory<byte>.Empty, cancellationToken: cancellationToken);
            await acceptTask;

            Assert.True(server.IsConnected);
        }
        finally
        {
            await client.DisposeAsync();
            await server.DrainToDisconnectAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task Should_swallow_connect_failures()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var client = new PipeKeepAliveClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<PipeKeepAliveClient>.Instance);

        // No server listening — connect must time out and be swallowed.
        var ex = await Record.ExceptionAsync(
            async () => await client.StartAsync(PipeTestServer.UniqueName("actx-pka-test"), TestEncodings.Utf8NoBom.GetBytes("x"), connectTimeoutMs: 100, cancellationToken: cancellationToken));

        Assert.Null(ex);
    }

    [Fact]
    public async Task StartAsync_after_dispose_is_a_noop()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = new PipeKeepAliveClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<PipeKeepAliveClient>.Instance);
        await client.DisposeAsync();

        var ex = await Record.ExceptionAsync(
            async () => await client.StartAsync(PipeTestServer.UniqueName("actx-pka-test"), TestEncodings.Utf8NoBom.GetBytes("x"), connectTimeoutMs: 100, cancellationToken: cancellationToken));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Dispose_should_tear_down_the_held_socket()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = PipeTestServer.UniqueName("actx-pka-test");
        await using var server = PipeTestServer.Create(name, PipeDirection.In);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);
        var client = new PipeKeepAliveClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<PipeKeepAliveClient>.Instance);

        try
        {
            // Concurrent server read — see Should_write_the_handshake_to_the_listener.
            var startTask = client.StartAsync(name, TestEncodings.Utf8NoBom.GetBytes("h"), cancellationToken: cancellationToken);
            await acceptTask;
            var handshake = new byte[1];
            await server.ReadExactlyAsync(handshake, cancellationToken);
            await startTask;

            await client.DisposeAsync();

            // The server side must observe a clean disconnect (read returns 0).
            var buffer = new byte[1];
            var read = await server.ReadAsync(buffer, cancellationToken);
            Assert.Equal(0, read);
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispose_is_idempotent()
    {
        await using var client = new PipeKeepAliveClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<PipeKeepAliveClient>.Instance);

        var ex = await Record.ExceptionAsync(async () =>
        {
            await client.DisposeAsync();
            await client.DisposeAsync();
            await client.DisposeAsync();
        });

        Assert.Null(ex);
    }
}
