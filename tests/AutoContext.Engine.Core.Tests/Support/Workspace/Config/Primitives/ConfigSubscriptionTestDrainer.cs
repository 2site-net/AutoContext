namespace AutoContext.Engine.Core.Tests.Support.Workspace.Config.Primitives;

using AutoContext.Engine.Core.Workspace.Config.Primitives;
using AutoContext.Engine.Protocol.Messages.Config;

internal static class ConfigSubscriptionTestDrainer
{
    public static async Task<List<JsonConfigStreamFrame>> DrainAsync(ConfigSubscription subscription)
    {
        var frames = new List<JsonConfigStreamFrame>();
        await foreach (var frame in subscription.ReadAllAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))
        {
            frames.Add(frame);
        }

        return frames;
    }
}
