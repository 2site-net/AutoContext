namespace AutoContext.Framework.Tests.Pipes;

using System.IO.Pipes;

using AutoContext.Framework.Tests.Testing.Utils;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class PipeListenerTests
{
    [Fact]
    public void Should_reject_empty_pipe_name_in_the_constructor()
    {
        Assert.Throws<ArgumentException>(
            () => new PipeListener(string.Empty, NullLogger<PipeListener>.Instance));
    }

    [Fact]
    public async Task Should_bind_successfully_and_produce_a_bound_listener()
    {
        var listener = new PipeListener(NewPipeName(), NullLogger<PipeListener>.Instance);

        var bound = listener.Bind();

        Assert.NotNull(bound);
        await bound.DisposeAsync();
    }

    [Fact]
    public async Task Should_reject_a_second_bind_on_the_same_listener()
    {
        var listener = new PipeListener(NewPipeName(), NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(listener.Bind);

            Assert.Contains("already been bound", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            await bound.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_invoke_handler_for_every_accepted_connection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = NewPipeName();
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var received = 0;
        var acceptedBoth = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runTask = bound.RunAsync(
            async (stream, ct) =>
            {
                var count = Interlocked.Increment(ref received);

                if (count == 2)
                {
                    acceptedBoth.TrySetResult();
                }

                // Drain bytes until peer disconnects.
                var buffer = new byte[1];

                while (await stream.ReadAsync(buffer, ct) > 0)
                {
                }
            },
            cts.Token);

        try
        {
            await using var c1 = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            await using var c2 = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);

            await c1.ConnectAsync(cancellationToken);
            await c2.ConnectAsync(cancellationToken);

            await acceptedBoth.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            Assert.Equal(2, Volatile.Read(ref received));
        }
        finally
        {
            await cts.CancelAsync();
            await runTask;
            await bound.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_reject_a_second_call_to_RunAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var listener = new PipeListener(NewPipeName(), NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var first = bound.RunAsync((_, _) => Task.CompletedTask, cts.Token);

        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await bound.RunAsync((_, _) => Task.CompletedTask, cts.Token));

            Assert.Contains("already been run", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            await cts.CancelAsync();
            await first;
            await bound.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_throw_when_RunAsync_is_called_after_dispose()
    {
        var listener = new PipeListener(NewPipeName(), NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        await bound.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await bound.RunAsync((_, _) => Task.CompletedTask, CancellationToken.None));
    }

    [Fact]
    public async Task Should_drain_in_flight_handlers_before_returning_from_RunAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = NewPipeName();
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = false;

        var runTask = bound.RunAsync(
            async (_, _) =>
            {
                started.TrySetResult();
                await Task.Delay(50, CancellationToken.None);
                finished = true;
            },
            cts.Token);

        try
        {
            await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(cancellationToken);

            await started.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            await cts.CancelAsync();
            await runTask;

            Assert.True(finished, "RunAsync must wait for in-flight handlers to drain.");
        }
        finally
        {
            await bound.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_return_immediately_from_RunAsync_when_token_already_canceled()
    {
        var listener = new PipeListener(NewPipeName(), NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var handlerCalls = 0;
        await bound.RunAsync(
            (_, _) =>
            {
                Interlocked.Increment(ref handlerCalls);
                return Task.CompletedTask;
            },
            cts.Token);

        Assert.Equal(0, handlerCalls);
        await bound.DisposeAsync();
    }

    [Fact]
    public async Task Should_log_and_recover_when_a_handler_throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = NewPipeName();
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var calls = 0;
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runTask = bound.RunAsync(
            (_, _) =>
            {
                var n = Interlocked.Increment(ref calls);

                if (n == 1)
                {
                    throw new InvalidOperationException("boom");
                }

                if (n >= 2)
                {
                    recovered.TrySetResult();
                }

                return Task.CompletedTask;
            },
            cts.Token);

        try
        {
            await using var c1 = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            await using var c2 = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);

            await c1.ConnectAsync(cancellationToken);
            await c2.ConnectAsync(cancellationToken);

            await recovered.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            Assert.True(Volatile.Read(ref calls) >= 2);
        }
        finally
        {
            await cts.CancelAsync();
            await runTask;
            await bound.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_allow_multiple_dispose_calls()
    {
        var listener = new PipeListener(NewPipeName(), NullLogger<PipeListener>.Instance);
        var bound = listener.Bind();

        var ex = await Record.ExceptionAsync(async () =>
        {
            await bound.DisposeAsync();
            await bound.DisposeAsync();
            await bound.DisposeAsync();
        });

        Assert.Null(ex);
    }

    private static string NewPipeName() => TestPipeServer.UniqueName("actx-pl-test");
}
