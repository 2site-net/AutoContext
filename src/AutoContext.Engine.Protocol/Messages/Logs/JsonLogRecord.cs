namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Canonical wire envelope for one log record. Carried as the
/// payload of <c>Engine.WriteLog</c> notifications (worker →
/// engine), as the streamed frame on the <c>logs</c> pipe, and
/// as the body of <c>Logs.GetEngine</c>/<c>Logs.TailEngine</c>
/// (and the matching worker variants) RPC responses. The on-wire
/// shape and the engine's on-disk <c>engine.log</c> /
/// <c>worker-&lt;workerId&gt;.log</c> NDJSON shape match
/// byte-for-byte — there is exactly one record envelope shared
/// across in-process producers, the wire, and disk; the Protocol
/// assembly owns its shape.
/// </summary>
/// <remarks>
/// <para>
/// Source: the <c>Engine.WriteLog</c> record shape under
/// <c>design § RPC surface</c> and <c>design § Log categories</c>.
/// The engine routes records to the correct on-disk file by
/// <see cref="Category"/> prefix — <c>worker.&lt;workerId&gt;.*</c>
/// records land in that worker's <c>worker-&lt;workerId&gt;.log</c>,
/// everything else lands in <c>engine.log</c> — and fans every
/// record out to subscribers on the <c>logs</c> pipe and to any
/// active <c>Logs.Tail*</c> RPC subscriber.
/// </para>
/// <para>
/// Field presence:
/// <list type="bullet">
/// <item><see cref="Timestamp"/>, <see cref="Category"/>,
/// <see cref="Level"/>, and <see cref="Message"/> are required on
/// every record.</item>
/// <item><see cref="EventId"/>, <see cref="Properties"/>, and
/// <see cref="Exception"/> are optional and are omitted from the
/// wire JSON by the
/// <see cref="Serialization.ProtocolJsonContext"/>'s default
/// <see cref="JsonIgnoreCondition.WhenWritingNull"/> policy when
/// absent.</item>
/// </list>
/// </para>
/// <para>
/// The <see cref="Level"/> field is a free-form string for
/// forward compatibility; producers populate it from the
/// <see cref="LogLevels"/> constants (which mirror
/// <see cref="Microsoft.Extensions.Logging.LogLevel"/>) without
/// the protocol assembly taking a runtime dependency on the
/// logging abstractions package.
/// </para>
/// </remarks>
public sealed record JsonLogRecord
{
    /// <summary>
    /// Moment the producer minted this record, in UTC. Serialised
    /// as ISO-8601 by System.Text.Json's default
    /// <see cref="System.DateTimeOffset"/> handling, matching the
    /// design's <c>"timestamp: string (ISO-8601 UTC, set by the
    /// worker at log time)"</c> contract.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Dotted, prefix-groupable category identifying the record's
    /// origin (e.g. <c>engine.lifecycle</c>,
    /// <c>engine.rpc.Instructions.Get</c>,
    /// <c>worker.dotnet.RoslynAnalyzer</c>). Matches the
    /// <see cref="Microsoft.Extensions.Logging.ILogger"/>
    /// category convention; subscribers filter by prefix-match,
    /// not enum-equals. See <c>design § Log categories</c>.
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Severity literal — one of the constants on
    /// <see cref="LogLevels"/>. Stored as a free-form string so a
    /// future producer can introduce a new tier without bumping
    /// the protocol version.
    /// </summary>
    [JsonPropertyName("level")]
    public string Level { get; init; } = string.Empty;

    /// <summary>
    /// Optional structured event id. Mirrors
    /// <see cref="Microsoft.Extensions.Logging.EventId"/> when the
    /// producer minted one; absent otherwise.
    /// </summary>
    [JsonPropertyName("eventId")]
    public JsonLogEventId? EventId { get; init; }

    /// <summary>
    /// Formatted message body. Producers materialise the message
    /// at log time (after applying scope state and message-template
    /// substitution) so the wire never has to carry the template
    /// separately from its arguments.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional structured key-value state captured from the
    /// producer's <see cref="Microsoft.Extensions.Logging.ILogger"/>
    /// scope and state objects. Stored as opaque
    /// <see cref="JsonElement"/> values so the protocol assembly
    /// stays free of runtime dependencies on whatever the producer
    /// shaped its property bag with.
    /// </summary>
    [JsonPropertyName("properties")]
    public IReadOnlyDictionary<string, JsonElement>? Properties { get; init; }

    /// <summary>
    /// Optional flattened exception in the wire shape
    /// <see cref="JsonLogExceptionInfo"/> defines. Producers project a
    /// CLR <see cref="System.Exception"/> to this DTO at the seam
    /// where the record is shaped for the wire.
    /// </summary>
    [JsonPropertyName("exception")]
    public JsonLogExceptionInfo? Exception { get; init; }
}
