namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

internal static class LogSubscriptionTestDrainer
{
    public static async Task<List<LogStreamFrame>> DrainAsync(LogSubscription subscription)
    {
        var frames = new List<LogStreamFrame>();
        await foreach (var frame in subscription.ReadAllAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))
        {
            frames.Add(frame);
        }

        return frames;
    }
}
