namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

internal static class LogRecordFakeData
{
    private static readonly DateTimeOffset DeterministicTimestamp =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static JsonLogRecord CreateLogRecord(
        string category = "engine.test",
        string level = LogLevels.Information,
        string message = "msg",
        DateTimeOffset? timestamp = null)
        => new()
        {
            Timestamp = timestamp ?? DeterministicTimestamp,
            Category = category,
            Level = level,
            Message = message,
        };
}
