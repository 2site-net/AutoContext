namespace AutoContext.Framework.Pipes.Tests;

using System.Collections.Concurrent;
using System.IO.Pipes;

using AutoContext.Framework.Pipes;
using AutoContext.Framework.Tests.Support.Async;
using AutoContext.Framework.Tests.Support.Encodings;
using AutoContext.Framework.Tests.Support.Pipes;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class PipeStreamingClientTests
{
    private static readonly string[] FallbackEmptyName = ["a", "b"];
    private static readonly string[] FallbackOverflowDropsOldest = ["b", "c"];

    [Fact]
    public async Task Should_write_serialized_items_over_the_pipe()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = PipeTestServer.UniqueName("actx-psc-test");
        var captured = new MemoryStream();
        await using var server = PipeTestServer.Create(name, PipeDirection.In);
        var serverTask = server.ServeIntoAsync(captured, cancellationToken);
        var client = new PipeStreamingClient<string>(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name,
            s => TestEncodings.Utf8NoBom.GetBytes(s),
            NullLogger<PipeStreamingClient<string>>.Instance);

        try
        {
            client.Post("hello");
            client.Post("world");
            await AsyncTestHelpers.WaitUntilAsync(() => TestEncodings.Utf8NoBom.GetString(captured.ToArray()) == "helloworld", cancellationToken);

            Assert.Equal("helloworld", TestEncodings.Utf8NoBom.GetString(captured.ToArray()));
        }
        finally
        {
            await client.DisposeAsync();
            await serverTask;
        }
    }

    [Fact]
    public async Task Should_write_greeting_before_any_items()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = PipeTestServer.UniqueName("actx-psc-test");
        var captured = new MemoryStream();
        await using var server = PipeTestServer.Create(name, PipeDirection.In);
        var serverTask = server.ServeIntoAsync(captured, cancellationToken);
        var client = new PipeStreamingClient<string>(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            name,
            s => TestEncodings.Utf8NoBom.GetBytes(s),
            NullLogger<PipeStreamingClient<string>>.Instance,
            greeting: TestEncodings.Utf8NoBom.GetBytes("GREET\n"));

        try
        {
            client.Post("A");
            await AsyncTestHelpers.WaitUntilAsync(() => TestEncodings.Utf8NoBom.GetString(captured.ToArray()) == "GREET\nA", cancellationToken);

            Assert.Equal("GREET\nA", TestEncodings.Utf8NoBom.GetString(captured.ToArray()));
        }
        finally
        {
            await client.DisposeAsync();
            await serverTask;
        }
    }

    [Fact]
    public async Task Should_route_to_fallback_when_pipe_name_is_empty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fallback = new ConcurrentQueue<string>();
        var client = new PipeStreamingClient<string>(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            string.Empty,
            s => TestEncodings.Utf8NoBom.GetBytes(s),
            NullLogger<PipeStreamingClient<string>>.Instance,
            fallback: fallback.Enqueue);

        try
        {
            client.Post("a");
            client.Post("b");
            await AsyncTestHelpers.WaitUntilAsync(() => fallback.Count == 2, cancellationToken);

            Assert.Equal(FallbackEmptyName, fallback.ToArray());
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_drop_the_oldest_item_when_the_queue_overflows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fallback = new ConcurrentQueue<string>();

        // Point the client at a non-listening pipe name with a short
        // connect timeout. The drain task parks inside the connect
        // attempt, which gives the test thread a deterministic window
        // to overflow the queue before any item can be drained.
        var client = new PipeStreamingClient<string>(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            PipeTestServer.UniqueName("actx-psc-nolisten"),
            s => TestEncodings.Utf8NoBom.GetBytes(s),
            NullLogger<PipeStreamingClient<string>>.Instance,
            fallback: fallback.Enqueue,
            queueCapacity: 2,
            connectTimeoutMs: 200);

        try
        {
            client.Post("a");
            client.Post("b");
            client.Post("c");
            await AsyncTestHelpers.WaitUntilAsync(() => fallback.Count == 2, cancellationToken);

            Assert.Equal(FallbackOverflowDropsOldest, fallback.ToArray());
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_drop_items_posted_after_dispose()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fallback = new ConcurrentQueue<string>();
        var client = new PipeStreamingClient<string>(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            string.Empty,
            s => TestEncodings.Utf8NoBom.GetBytes(s),
            NullLogger<PipeStreamingClient<string>>.Instance,
            fallback: fallback.Enqueue);
        await client.DisposeAsync();

        var posted = client.Post("lost");
        await Task.Delay(20, cancellationToken);

        Assert.Multiple(
            () => Assert.False(posted),
            () => Assert.Empty(fallback));
    }

    [Fact]
    public async Task Dispose_is_idempotent()
    {
        await using var client = new PipeStreamingClient<string>(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            string.Empty,
            s => TestEncodings.Utf8NoBom.GetBytes(s),
            NullLogger<PipeStreamingClient<string>>.Instance);

        var ex = await Record.ExceptionAsync(async () =>
        {
            await client.DisposeAsync();
            await client.DisposeAsync();
            await client.DisposeAsync();
        });

        Assert.Null(ex);
    }
}
