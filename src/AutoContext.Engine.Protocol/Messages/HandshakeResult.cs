namespace AutoContext.Engine.Protocol.Messages;

using System.Text.Json.Serialization;

/// <summary>
/// Result body of a successful <c>Engine.Hello</c> response. Echoes
/// the engine's protocol-version constant (so clients can sanity-check
/// the value they negotiated against) and reports the engine's
/// informational semver for diagnostics and telemetry.
/// </summary>
public sealed record HandshakeResult
{
    /// <summary>
    /// Engine's wire-protocol version (== client's, since the
    /// engine accepts only exact matches).
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; }

    /// <summary>
    /// Engine binary's informational semver
    /// (<see cref="System.Reflection.AssemblyInformationalVersionAttribute"/>).
    /// </summary>
    [JsonPropertyName("engineVersion")]
    public string EngineVersion { get; init; } = string.Empty;
}
