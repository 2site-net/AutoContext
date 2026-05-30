namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// Optional parameters of the <see cref="LogsMethods.GetEngine"/>
/// request. Carries the snapshot bounds the caller wants to apply
/// to the engine's active <c>engine.log</c> file. The whole params
/// object is itself optional on the wire — an absent or empty
/// <c>params</c> value means "every record currently in the active
/// file, no filter".
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LastN"/> and <see cref="Since"/> are independent —
/// callers may set neither, either, or both. When both are set
/// the engine applies <see cref="Since"/> first (timestamp filter)
/// and then <see cref="LastN"/> (tail cap), matching the design's
/// "<c>opts.lastN</c> caps from the tail" wording.
/// </para>
/// <para>
/// Both fields are nullable so the engine can distinguish "field
/// absent" (apply no bound) from a wire value of <c>0</c> (return
/// no records, but still indicate truncation correctly) — see the
/// matching handling in <c>HandshakeParams.ProtocolVersion</c>.
/// </para>
/// </remarks>
public sealed record JsonLogsGetEngineParams
{
    /// <summary>
    /// Maximum number of records to return, counted from the
    /// tail of the active file (most recent first, but the reply
    /// preserves chronological order). When <see langword="null"/>
    /// the engine returns every record matching
    /// <see cref="Since"/> (or every record in the active file
    /// when <see cref="Since"/> is also <see langword="null"/>).
    /// </summary>
    [JsonPropertyName("lastN")]
    public int? LastN { get; init; }

    /// <summary>
    /// Inclusive lower bound on <see cref="JsonLogRecord.Timestamp"/>.
    /// Records with a timestamp strictly older than this value are
    /// excluded. <see langword="null"/> means "no lower bound".
    /// </summary>
    [JsonPropertyName("since")]
    public DateTimeOffset? Since { get; init; }
}
