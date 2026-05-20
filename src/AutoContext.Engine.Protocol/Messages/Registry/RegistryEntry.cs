namespace AutoContext.Engine.Protocol.Messages.Registry;

using System.Text.Json.Serialization;

/// <summary>
/// One record in the machine-wide engine-liveness registry
/// (<c>engine-registry.json</c>) and, per
/// <c>design § RPC surface (initial)</c>, the wire shape returned
/// inside the <c>Engine.RegistryEntries</c> response. The
/// on-disk file and the RPC response carry the same value type —
/// hence the record's home in the protocol assembly rather than
/// alongside its file-side I/O.
/// </summary>
/// <remarks>
/// <para>
/// Entries are written additively at engine startup (one entry per
/// fresh <see cref="InstanceId"/> per spawn) and removed by the
/// owning engine on graceful shutdown. A crashed engine leaves its
/// entry in place; staleness is detected by the next graceful
/// peer's shutdown sweep using <see cref="ProcessId"/> +
/// <see cref="ProcessStartTimeUtc"/> against
/// <c>Process.GetProcessById(processId).StartTime</c>. See
/// <c>design § engine-registry.json entry lifecycle</c>.
/// </para>
/// </remarks>
/// <param name="EngineVersion">Semver string from
/// <c>AssemblyInformationalVersionAttribute</c>.</param>
/// <param name="WorkspaceHash">Workspace identity hash —
/// <c>sha256(normalisedWorkspacePath):0..16</c> per
/// <c>design § Endpoint</c>.</param>
/// <param name="WorkspacePath">Absolute, normalised workspace root
/// path the hash was derived from. Carried alongside the hash so
/// diagnostics, housekeeping, and <c>Engine.RegistryEntries</c>
/// consumers can name the workspace without a reverse lookup.</param>
/// <param name="InstanceId">Per-spawn UUIDv4 the launcher minted.
/// Forms the <c>&lt;instanceId&gt;</c> segment of every endpoint.</param>
/// <param name="InstanceLabel">Optional freeform descriptor;
/// pure observability, no semantic effect.</param>
/// <param name="ProcessId">OS process id of the engine.</param>
/// <param name="ProcessStartTimeUtc">Process start time used with
/// <see cref="ProcessId"/> to defeat pid recycling when asserting
/// liveness.</param>
/// <param name="StartedAt">When this entry was written.</param>
/// <param name="Retention">Per-entry housekeeping retention window;
/// honoured by the peer whose shutdown sweep reaps this entry, not
/// by the sweeping peer's own <c>--retention</c>.</param>
public sealed record RegistryEntry(
    [property: JsonPropertyName("engineVersion")] string EngineVersion,
    [property: JsonPropertyName("workspaceHash")] string WorkspaceHash,
    [property: JsonPropertyName("workspacePath")] string WorkspacePath,
    [property: JsonPropertyName("instanceId")] Guid InstanceId,
    [property: JsonPropertyName("instanceLabel")] string InstanceLabel,
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("processStartTimeUtc")] DateTimeOffset ProcessStartTimeUtc,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("retention")] TimeSpan Retention);
