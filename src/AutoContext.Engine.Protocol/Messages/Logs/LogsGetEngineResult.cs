namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// Result body for <see cref="LogsMethods.GetEngine"/>. Carries
/// the bounded snapshot of records drawn from the engine's active
/// <c>engine.log</c> file. The shape matches the design's
/// <c>{ kind: "ok", records: LogRecord[], truncated: boolean }</c>
/// envelope.
/// </summary>
/// <remarks>
/// <para>
/// The design's outer <c>Logs.Get*</c> response type also includes
/// a <c>{ kind: "not-found", workerId }</c> arm for the worker
/// variants (<c>Logs.GetWorker</c> / <c>Logs.TailWorker</c>);
/// <c>GetEngine</c> can never return that arm because the engine's
/// own log file always exists for the current process, so this
/// result type intentionally carries no discriminator field. When
/// the worker variants are added the polymorphic envelope will
/// land alongside them, at which point the <c>not-found</c> arm
/// becomes addressable.
/// </para>
/// <para>
/// <see cref="Truncated"/> is set to <see langword="true"/> when
/// the engine's active <c>engine.log</c> rolled past part of the
/// requested range (either because rotation discarded older
/// records that satisfied <see cref="LogsGetEngineParams.Since"/>,
/// or because <see cref="LogsGetEngineParams.LastN"/> bounded the
/// reply and earlier records were dropped). Callers use it to
/// surface a "scrolled past start" affordance.
/// </para>
/// </remarks>
public sealed record LogsGetEngineResult
{
    /// <summary>
    /// The records satisfying the request's filter, in
    /// chronological order (oldest first), bounded by
    /// <see cref="LogsGetEngineParams.LastN"/> when present.
    /// Empty when no record in the active file matched the
    /// filter.
    /// </summary>
    [JsonPropertyName("records")]
    public IReadOnlyList<LogRecord> Records { get; init; } = [];

    /// <summary>
    /// <see langword="true"/> when the active file rolled past
    /// part of the requested range. See type-level remarks for
    /// the precise semantics.
    /// </summary>
    [JsonPropertyName("truncated")]
    public bool Truncated { get; init; }
}
