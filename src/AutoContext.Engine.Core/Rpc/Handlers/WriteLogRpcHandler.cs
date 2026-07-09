namespace AutoContext.Engine.Core.Rpc.Handlers;

using System;
using System.Collections.Generic;
using System.Text.Json;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// The <c>Engine.WriteLog</c> handler — the engine-side ingest end
/// of the worker → engine logging path. <c>Engine.WriteLog</c> is a
/// JSON-RPC 2.0 notification (no <c>id</c>, no response): a worker
/// marshals one <see cref="JsonLogRecord"/> as the request params
/// and the handler enqueues it onto the shared
/// <see cref="LogChannel"/>, from which
/// <see cref="LogFileSinkService"/> drains it, routes it by
/// <c>category</c> prefix to the right on-disk log, and fans it out
/// to <c>logs</c>-pipe / <c>Logs.Tail*</c> subscribers.
/// </summary>
/// <remarks>
/// <para>
/// Log <em>ingest</em> (this handler — a producer that enqueues and
/// returns no response) is a distinct capability from log
/// <em>read</em> (<see cref="LogsRpcHandler"/> — a consumer that
/// answers <c>Logs.*</c> reads and streams), so it lives in its own
/// capability-named handler rather than folded into the reader.
/// The handler stays paper-thin: deserialise, enqueue, done. The
/// <see cref="LogChannel"/> is multi-producer safe, so this second
/// producer (alongside the engine's own <c>EngineLoggerProvider</c>)
/// preserves the single-reader drain.
/// </para>
/// <para>
/// Because a notification carries no <c>id</c>, the handler never
/// replies — not even on a malformed payload. A record that fails
/// to deserialise, or arrives after the channel has completed, is
/// logged at debug and dropped.
/// </para>
/// </remarks>
internal sealed partial class WriteLogRpcHandler : IRpcMethodHandler
{
    private readonly LogChannel _channel;
    private readonly ILogger<WriteLogRpcHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="WriteLogRpcHandler"/> class.
    /// </summary>
    /// <param name="channel">Shared ingest channel worker records
    /// are enqueued onto.</param>
    /// <param name="logger">Diagnostic sink for dropped records.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public WriteLogRpcHandler(LogChannel channel, ILogger<WriteLogRpcHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(logger);

        _channel = channel;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Methods { get; } = [ProtocolMethods.WriteLog];

    // A notification is consumed with no response and the
    // connection keeps serving the next frame.
    private static ValueTask<RpcHandlerResult> Consumed
        => new(new NotificationHandlerResult());

    /// <inheritdoc />
    public ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        JsonLogRecord? record;

        try
        {
            record = request.Params is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } element
                ? element.Deserialize(ProtocolJsonContext.Default.JsonLogRecord)
                : null;
        }
        catch (JsonException ex)
        {
            // Notification: no id, so no error reply — log and drop.
            LogRecordParseFailed(_logger, ex);
            return Consumed;
        }

        if (record is null)
        {
            LogRecordMissing(_logger);
            return Consumed;
        }

        if (!_channel.TryWrite(record))
        {
            // The ingest channel has completed (engine shutting
            // down); nothing left to drain the record.
            LogRecordDroppedChannelClosed(_logger, record.Category);
        }

        return Consumed;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Engine.WriteLog dropped a '{Category}' record; the ingest channel is closed.")]
    private static partial void LogRecordDroppedChannelClosed(ILogger logger, string category);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Engine.WriteLog received a notification with no record payload; dropping.")]
    private static partial void LogRecordMissing(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Engine.WriteLog dropped a record whose params failed to parse.")]
    private static partial void LogRecordParseFailed(ILogger logger, Exception exception);
}
