namespace AutoContext.Engine.Protocol.JsonRpc;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of a single inbound JSON-RPC 2.0 request frame.
/// Carries the framing fields the dispatcher needs (<c>jsonrpc</c>,
/// <c>id</c>, <c>method</c>) plus an opaque <c>params</c> payload
/// the per-method handler decodes against its own DTO. Per
/// <c>design § Lifecycle &gt; Wire-protocol handshake</c> every
/// <c>rpc</c> and <c>events</c> frame is a JSON-RPC 2.0 envelope;
/// this is the unifying request shape.
/// </summary>
/// <remarks>
/// <para>
/// Both <see cref="Id"/> and <see cref="Params"/> are deliberately
/// typed as <see cref="JsonElement"/>. <see cref="Id"/> may be a
/// string, number, or null per JSON-RPC 2.0, and we copy it back
/// onto the response untouched. <see cref="Params"/> is opaque at
/// the framing layer; dispatchers re-deserialize it against the
/// per-method params record once <see cref="Method"/> identifies
/// the handler.
/// </para>
/// <para>
/// Absent fields surface as <see cref="JsonValueKind.Undefined"/>;
/// callers must check before reading.
/// </para>
/// </remarks>
public sealed record JsonRpcRequest
{
    /// <summary>
    /// Protocol marker. Must equal <c>"2.0"</c> per JSON-RPC 2.0.
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = JsonRpcVersion.Value;

    /// <summary>
    /// Caller-supplied request identifier. Echoed verbatim on the
    /// matching response.
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement Id { get; init; }

    /// <summary>
    /// Method name (e.g. <c>"Engine.Hello"</c>).
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// Opaque parameters payload. The dispatcher re-deserializes
    /// this against the per-method params record. Absent on
    /// notification-style frames; absent fields read back as
    /// <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Params { get; init; }
}
