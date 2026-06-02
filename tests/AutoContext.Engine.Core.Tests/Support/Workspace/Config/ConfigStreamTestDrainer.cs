namespace AutoContext.Engine.Core.Tests.Support.Workspace.Config;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Protocol.Messages.Config;

internal static class ConfigStreamTestDrainer
{
    public static async Task<List<JsonConfigStreamFrame>> DrainAsync(BroadcasterSubscription<JsonConfigSnapshot> subscription)
    {
        var frames = new List<JsonConfigStreamFrame>();
        await foreach (var frame in ConfigStreamFrames.MapAsync(subscription, TestContext.Current.CancellationToken).ConfigureAwait(false))
        {
            frames.Add(frame);
        }

        return frames;
    }
}
