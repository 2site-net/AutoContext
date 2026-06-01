namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

internal static class LogChannelTestDrainer
{
    public static async Task<List<JsonLogRecord>> DrainAsync(LogChannel channel)
    {
        var drained = new List<JsonLogRecord>();
        await foreach (var record in channel.ReadAllAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))
        {
            drained.Add(record);
        }

        return drained;
    }
}
