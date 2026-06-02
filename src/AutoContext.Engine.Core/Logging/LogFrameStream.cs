namespace AutoContext.Engine.Core.Logging;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// Maps a <see cref="BroadcasterSubscription{T}"/> of
/// <see cref="JsonLogRecord"/> onto the <see cref="JsonLogStreamFrame"/>
/// wire protocol: each record becomes a <see cref="JsonLogRecordFrame"/>
/// and a slow-subscriber drop appends a terminal
/// <see cref="JsonLogDroppedFrame"/>.
/// </summary>
internal sealed class LogFrameStream : BroadcasterFrameStream<JsonLogRecord, JsonLogStreamFrame>
{
    /// <inheritdoc/>
    protected override JsonLogStreamFrame CreateDroppedFrame()
        => new JsonLogDroppedFrame(JsonLogDroppedFrame.SlowSubscriberReason);

    /// <inheritdoc/>
    protected override JsonLogStreamFrame ToFrame(JsonLogRecord payload)
        => new JsonLogRecordFrame(payload);
}
