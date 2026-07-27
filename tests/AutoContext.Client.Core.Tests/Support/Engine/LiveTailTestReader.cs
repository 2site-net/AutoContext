namespace AutoContext.Client.Core.Tests.Support.Engine;

using AutoContext.Client.Core.Engine;
using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Engine.Protocol.Messages.Agent;
using AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// Reads the first frame from a live-tail engine stream. Unlike the
/// snapshot-on-subscribe channels, <c>Agent.Events.Subscribe</c> and
/// <c>Logs.TailEngine</c> replay nothing to a new subscriber, so a frame only
/// arrives if something is published after the subscription reaches the
/// engine's broadcaster. Publishing once and awaiting would race that
/// registration; this reader keeps publishing on an interval until the first
/// frame lands, then stops.
/// </summary>
/// <remarks>
/// A frame arrives reliably only when each publish yields exactly one frame.
/// Actions the engine batches, debounces, or suppresses — a config edit, say —
/// yield frames sporadically and turn the read into a coin toss. Each stream
/// therefore gets its own method below carrying its own one-to-one trigger, so
/// an unsuitable trigger cannot be supplied.
/// </remarks>
internal static class LiveTailTestReader
{
    /// <summary>How long to keep publishing before giving up on a frame.</summary>
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Delay between successive publish attempts.</summary>
    private static readonly TimeSpan PublishInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Grace period before the first publish. Subscribing dials a fresh
    /// connection and handshakes it, so publishing immediately would emit into a
    /// stream the engine has not yet registered a subscriber for.
    /// </summary>
    private static readonly TimeSpan SubscribeGrace = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Subscribes to <c>Agent.Events.Subscribe</c>, fires
    /// <paramref name="notify"/> until an event arrives, and returns it. Every
    /// <see cref="AgentRpcClient"/> notification broadcasts exactly one event.
    /// </summary>
    /// <param name="client">Connected client whose event stream is read.</param>
    /// <param name="notify">The agent notification to fire.</param>
    /// <param name="cancellationToken">Cancellation for the read.</param>
    public static Task<JsonAgentEvent> ReadFirstAgentEventAsync(
        EngineClient client,
        Func<AgentRpcClient, CancellationToken, Task> notify,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(notify);

        return ReadFirstAsync(
            client.AgentEvents().SubscribeAsync(cancellationToken),
            token => notify(client.Agent, token),
            cancellationToken);
    }

    /// <summary>
    /// Subscribes to <c>Logs.TailEngine</c>, dials <paramref name="engine"/>
    /// until a record arrives, and returns it. Every dial runs an
    /// <c>Engine.Hello</c> handshake, which the engine logs exactly once.
    /// </summary>
    /// <param name="engine">Harness whose engine is dialled to force a record.</param>
    /// <param name="client">Connected client whose log stream is read.</param>
    /// <param name="cancellationToken">Cancellation for the read.</param>
    public static Task<JsonLogRecord> ReadFirstLogRecordAsync(
        InProcessEngineTestHarness engine,
        EngineClient client,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(client);

        return ReadFirstAsync(
            client.LogsTail().SubscribeAsync(cancellationToken),
            engine.HandshakeAsync,
            cancellationToken);
    }

    /// <summary>
    /// Subscribes to <paramref name="stream"/>, drives
    /// <paramref name="publish"/> until a frame arrives, and returns it.
    /// </summary>
    /// <typeparam name="TFrame">Stream payload type.</typeparam>
    /// <param name="stream">The live-tail stream to read.</param>
    /// <param name="publish">Action that causes the engine to emit a frame.
    /// Invoked repeatedly until the first frame is observed.</param>
    /// <param name="cancellationToken">Cancellation for the read.</param>
    /// <exception cref="InvalidOperationException">The stream completed without
    /// yielding a frame.</exception>
    /// <exception cref="OperationCanceledException">No frame arrived within
    /// the read timeout.</exception>
    private static async Task<TFrame> ReadFirstAsync<TFrame>(
        IAsyncEnumerable<TFrame> stream,
        Func<CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(publish);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(ReadTimeout);

        var first = new TaskCompletionSource<TFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = deadline.Token.Register(
            () => first.TrySetCanceled(deadline.Token));

        var reader = Task.Run(
            async () =>
            {
                await foreach (var frame in stream.WithCancellation(deadline.Token).ConfigureAwait(false))
                {
                    first.TrySetResult(frame);
                    break;
                }

                first.TrySetException(new InvalidOperationException(
                    "The engine completed the stream before yielding a frame."));
            },
            deadline.Token);

        await Task.Delay(SubscribeGrace, deadline.Token).ConfigureAwait(false);

        while (!first.Task.IsCompleted)
        {
            await publish(deadline.Token).ConfigureAwait(false);
            await Task.Delay(PublishInterval, deadline.Token).ConfigureAwait(false);
        }

        _ = reader;

        return await first.Task.ConfigureAwait(false);
    }
}
