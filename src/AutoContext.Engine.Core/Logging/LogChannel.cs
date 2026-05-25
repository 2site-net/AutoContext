namespace AutoContext.Engine.Core.Logging;

using System.Threading.Channels;

using AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// Single in-process ingest channel for engine log records.
/// Producers — the engine's own <c>ILogger&lt;T&gt;</c> records
/// routed via <c>EngineLoggerProvider</c> in a later phase, and
/// the worker-bound <c>Engine.WriteLog</c> RPC handler in Phase 8
/// — enqueue <see cref="LogRecord"/> instances through
/// <see cref="TryWrite"/>; <c>LogFileSinkService</c> drains the
/// channel via <see cref="ReadAllAsync"/> and dispatches each
/// record to the file sink (and, from row 5 onwards, the
/// <c>logs</c>-pipe broadcaster) along its single drain loop.
/// </summary>
/// <remarks>
/// <para>
/// The channel owns a single bounded <see cref="Channel{T}"/> of
/// <see cref="LogRecord"/> with capacity
/// <see cref="DefaultCapacity"/>. Producers are non-blocking;
/// overflow is handled with
/// <see cref="BoundedChannelFullMode.DropOldest"/> so a sustained
/// burst sheds the oldest queued records rather than blocking the
/// caller — this matches the worker-side ingest-ring shape
/// described in <c>design § Log pipeline backpressure</c>.
/// </para>
/// <para>
/// Row 2 of Phase 2a wires a single drain loop
/// (<c>LogFileSinkService</c>) to the channel and writes records
/// straight to <c>engine.log</c>. Row 5 keeps the channel and its
/// single reader unchanged; it reshapes the drain loop into a
/// dispatcher that fans each drained record out to two inner
/// sinks — the file sink and the <c>logs</c>-pipe /
/// <c>Logs.Tail*</c> broadcaster — instead of adding a second
/// consumer of the channel itself (the channel stays
/// <see cref="BoundedChannelOptions.SingleReader"/>).
/// </para>
/// <para>
/// Thread-safety: <see cref="TryWrite"/> and <see cref="Complete"/>
/// are safe to call concurrently from any thread.
/// <see cref="ReadAllAsync"/> is intended for a single drain
/// loop (<see cref="BoundedChannelOptions.SingleReader"/> is
/// <see langword="true"/>); concurrent enumeration is not
/// supported.
/// </para>
/// </remarks>
internal sealed class LogChannel
{
    /// <summary>
    /// Capacity of the bounded ingest channel. Sized to absorb a
    /// burst of records without dropping under steady load while
    /// keeping the drop-oldest window small enough that a slow
    /// file sink cannot wedge live memory.
    /// </summary>
    internal const int DefaultCapacity = 1024;

    private readonly Channel<LogRecord> _channel;

    /// <summary>
    /// Creates a new <see cref="LogChannel"/> backed by a
    /// bounded channel of capacity <see cref="DefaultCapacity"/>.
    /// </summary>
    public LogChannel()
    {
        _channel = Channel.CreateBounded<LogRecord>(
            new BoundedChannelOptions(DefaultCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
            });
    }

    /// <summary>
    /// Marks the channel as completed so the drain loop in
    /// <c>LogFileSinkService</c> exits naturally once it has
    /// flushed every queued record. Called from the file sink
    /// service's
    /// <see cref="Microsoft.Extensions.Hosting.IHostedService.StopAsync"/>
    /// during graceful shutdown.
    /// </summary>
    public void Complete()
        => _channel.Writer.TryComplete();

    /// <summary>
    /// Asynchronously enumerates every record queued on the
    /// channel until <see cref="Complete"/> is called and the
    /// buffer has been fully drained. Intended for the single
    /// drain loop owned by <c>LogFileSinkService</c>; row 5
    /// keeps this enumeration single-reader and adds the
    /// broadcaster as a downstream stage of the same loop
    /// rather than a second consumer of the channel.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token observed
    /// while waiting for the next record. Note: cancelling
    /// abandons records still buffered on the channel, so
    /// graceful-shutdown callers should pass
    /// <see cref="CancellationToken.None"/> and rely on
    /// <see cref="Complete"/> to terminate the enumeration
    /// instead.</param>
    /// <returns>An async sequence of queued records.</returns>
    public IAsyncEnumerable<LogRecord> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Enqueues <paramref name="record"/> on the ingest channel.
    /// Never blocks: when the channel is at capacity the oldest
    /// queued record is dropped to make room (per
    /// <see cref="BoundedChannelFullMode.DropOldest"/>). Returns
    /// <see langword="false"/> only once the channel has been
    /// completed via <see cref="Complete"/> — at which point no
    /// further records will be accepted.
    /// </summary>
    /// <param name="record">Record to enqueue.</param>
    /// <returns><see langword="true"/> if the record was accepted;
    /// <see langword="false"/> if the channel is already completed.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="record"/> is <see langword="null"/>.
    /// </exception>
    public bool TryWrite(LogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return _channel.Writer.TryWrite(record);
    }
}
