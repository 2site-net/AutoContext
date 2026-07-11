namespace AutoContext.Engine.Core.Logging;

using AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// Output shape of <see cref="LogFileReader.ReadAsync"/>.
/// </summary>
/// <param name="Records">Ordered records satisfying the filter.</param>
/// <param name="Truncated"><see langword="true"/> when the active
/// file rolled past part of the requested <c>since</c> range.</param>
internal readonly record struct EngineLogReadResult(
    IReadOnlyList<JsonLogRecord> Records,
    bool Truncated)
{
    /// <summary>
    /// Result shape carrying no records and no truncation.
    /// </summary>
    public static EngineLogReadResult Empty { get; } =
        new([], false);
}
