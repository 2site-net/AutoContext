namespace AutoContext.Engine.Protocol.Messages.Workspace;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape returned by the <c>Workspace.Info</c> RPC — engine-process
/// metadata for the pinned workspace. Unlike
/// <see cref="JsonWorkspaceDetectResult"/> (which describes workspace
/// contents), this describes the engine serving the workspace: which
/// version is running, the <c>(instanceId, revision)</c> identity and
/// state-version pair, the human-readable instance label, and the
/// idle-timeout state. It is a state-bearing read, hence it carries
/// the <see cref="Revision"/> counter. See
/// <c>design § RPC surface</c>.
/// </summary>
public sealed record JsonWorkspaceInfoResult
{
    /// <summary>
    /// Semver string of the running engine, from
    /// <c>AssemblyInformationalVersionAttribute</c>.
    /// </summary>
    [JsonPropertyName("engineVersion")]
    public string EngineVersion { get; init; } = string.Empty;

    /// <summary>
    /// The configured idle-shutdown window.
    /// <see cref="TimeSpan.Zero"/> reports the
    /// "idle gate disabled" state — the engine lives until killed by
    /// signal, by <c>Engine.Shutdown</c>, or by the parent-process
    /// watchdog.
    /// </summary>
    [JsonPropertyName("idleTimeout")]
    public TimeSpan IdleTimeout { get; init; }

    /// <summary>
    /// Per-spawn UUIDv4 the launcher minted; the
    /// <c>&lt;instanceId&gt;</c> segment of every endpoint.
    /// </summary>
    [JsonPropertyName("instanceId")]
    public Guid InstanceId { get; init; }

    /// <summary>
    /// Optional freeform descriptor the launcher attached to this
    /// instance; pure observability, empty when unset.
    /// </summary>
    [JsonPropertyName("instanceLabel")]
    public string InstanceLabel { get; init; } = string.Empty;

    /// <summary>
    /// Monotonic state-version counter for the engine's workspace
    /// snapshot. Pairs with <see cref="InstanceId"/> to let a client
    /// tell whether the engine's state changed since its last read.
    /// </summary>
    [JsonPropertyName("revision")]
    public long Revision { get; init; }
}
