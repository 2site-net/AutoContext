namespace AutoContext.Workers.Core.Tests.Support.Logging;

using AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// Builds <see cref="JsonLogRecord"/> instances with deterministic
/// defaults for the worker-log tests. Override any field inline with a
/// <c>with</c> expression when a test cares about it.
/// </summary>
internal static class JsonLogRecordFakeData
{
    private static readonly DateTimeOffset DeterministicTimestamp =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static JsonLogRecord CreateRecord(
        string category = "Sample.Category",
        string level = LogLevels.Information,
        string message = "sample message")
        => new()
        {
            Timestamp = DeterministicTimestamp,
            Category = category,
            Level = level,
            Message = message,
        };
}
