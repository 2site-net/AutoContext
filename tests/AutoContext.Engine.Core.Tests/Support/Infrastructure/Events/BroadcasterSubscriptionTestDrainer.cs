namespace AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;

using AutoContext.Engine.Core.Infrastructure.Events;

internal static class BroadcasterSubscriptionTestDrainer
{
    public static async Task<List<T>> DrainAsync<T>(BroadcasterSubscription<T> subscription)
    {
        var payloads = new List<T>();
        await foreach (var payload in subscription.ReadAllAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))
        {
            payloads.Add(payload);
        }

        return payloads;
    }
}
