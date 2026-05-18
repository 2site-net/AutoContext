namespace AutoContext.Engine.Protocol.Messages.Lifecycle;

using System.Text.Json.Serialization;

/// <summary>
/// Payload of an <c>Engine.Lifecycle</c> JSON-RPC notification
/// emitted on the <c>events</c> pipe. Identifies one transition
/// in the engine's lifecycle by its <see cref="Kind"/> and
/// carries the snapshot key <c>(instanceId, revision)</c> clients
/// dedup against — see
/// <c>design § Engine.Lifecycle.Subscribe</c>.
/// </summary>
/// <remarks>
/// <para>
/// One event per envelope; the <c>events</c> stream is unbatched
/// per <c>design § Per-stream contracts</c>. The kebab-case
/// <see cref="Kind"/> literals are defined as constants on
/// <see cref="LifecycleEventKinds"/>.
/// </para>
/// <para>
/// Field presence:
/// <list type="bullet">
/// <item><see cref="InstanceId"/> and <see cref="Revision"/> are
/// populated on <c>started</c>, <c>reloading</c>,
/// <c>reloaded</c>, and <c>shutting-down</c> events.</item>
/// <item><see cref="Reason"/> is populated only on the terminal
/// <c>evicted</c> frame the engine writes when a slow subscriber
/// fills its bounded buffer (P9, <c>design § events &gt; backpressure</c>).</item>
/// </list>
/// All absent fields are omitted from the wire JSON by the
/// <see cref="Serialization.ProtocolJsonContext"/>'s default
/// <see cref="JsonIgnoreCondition.WhenWritingNull"/> policy.
/// </para>
/// </remarks>
public sealed record LifecycleEvent
{
    /// <summary>
    /// Kebab-case wire string identifying the transition. One of
    /// the constants on <see cref="LifecycleEventKinds"/>.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Per-spawn engine instance the event originates from.
    /// Present on every event except the terminal <c>evicted</c>
    /// frame, where it is omitted because the client already knows
    /// which engine evicted it from the pipe it subscribed on.
    /// </summary>
    [JsonPropertyName("instanceId")]
    public Guid? InstanceId { get; init; }

    /// <summary>
    /// Snapshot revision counter at the moment the event was
    /// published; clients key dedup off <c>(instanceId, revision)</c>.
    /// Always <c>0</c> in Phase 1 (no snapshot pipeline yet); the
    /// counter is bumped by the reload pipeline that lands later.
    /// </summary>
    [JsonPropertyName("revision")]
    public long? Revision { get; init; }

    /// <summary>
    /// Human-readable reason for the transition. Currently used
    /// only on the terminal <c>evicted</c> frame to distinguish
    /// the eviction trigger (e.g. <c>"slow-subscriber"</c>).
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
