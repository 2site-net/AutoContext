namespace AutoContext.Engine.Core.Tests.Support.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Instructions;

internal static class InstructionsStreamTestDrainer
{
    public static async Task<List<JsonInstructionsStreamFrame>> DrainAsync(
        BroadcasterSubscription<IReadOnlyList<JsonInstructionsListRow>> subscription)
    {
        var frames = new List<JsonInstructionsStreamFrame>();
        await foreach (var frame in new InstructionsFrameStream().StreamAsync(subscription, TestContext.Current.CancellationToken).ConfigureAwait(false))
        {
            frames.Add(frame);
        }

        return frames;
    }
}
