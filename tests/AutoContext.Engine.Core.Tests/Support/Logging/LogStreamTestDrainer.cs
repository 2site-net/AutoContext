namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

internal static class LogStreamTestDrainer
{
    public static async Task<List<JsonLogStreamFrame>> DrainAsync(BroadcasterSubscription<JsonLogRecord> subscription)
    {
        var frames = new List<JsonLogStreamFrame>();
        await foreach (var frame in LogStreamFrames.MapAsync(subscription, TestContext.Current.CancellationToken).ConfigureAwait(false))
        {
            frames.Add(frame);
        }

        return frames;
    }
}
