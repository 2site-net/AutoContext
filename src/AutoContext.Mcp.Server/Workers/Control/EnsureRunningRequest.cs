namespace AutoContext.Mcp.Server.Workers.Control;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of an <c>EnsureRunning</c> request sent over the
/// extension's worker-control pipe. <see cref="Type"/> is fixed at
/// <c>"ensureRunning"</c> so future request kinds can extend the
/// protocol without breaking back-compat. <see cref="WorkerId"/> is
/// the **short** id from <c>mcp-workers-registry.json</c>
/// (e.g. <c>"workspace"</c>, <c>"dotnet"</c>, <c>"web"</c>) — the
/// same id that already flows through pipe naming and health-monitor
/// identity.
/// </summary>
internal sealed record EnsureRunningRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "ensureRunning";

    [JsonPropertyName("workerId")]
    public required string WorkerId { get; init; }
}
