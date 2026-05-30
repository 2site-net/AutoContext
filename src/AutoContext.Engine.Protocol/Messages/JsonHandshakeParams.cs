namespace AutoContext.Engine.Protocol.Messages;

using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <c>Engine.Hello</c> request. Carries the
/// caller's protocol version, which the engine compares against
/// <see cref="ProtocolVersion.Current"/> for exact match per
/// <c>design § Lifecycle &gt; Wire-protocol handshake</c>.
/// </summary>
public sealed record JsonHandshakeParams
{
    /// <summary>
    /// Wire-protocol version the client is speaking. Must be
    /// present and equal <see cref="ProtocolVersion.Current"/>
    /// exactly; absence refuses with
    /// <see cref="JsonRpc.JsonRpcErrorCodes.InvalidParams"/> and
    /// mismatch refuses with
    /// <see cref="JsonRpc.JsonRpcErrorCodes.ProtocolVersionMismatch"/>.
    /// Modelled as <see langword="int?"/> so the handshake can
    /// distinguish a missing field from a wire value of <c>0</c>.
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public int? ProtocolVersion { get; init; }
}
