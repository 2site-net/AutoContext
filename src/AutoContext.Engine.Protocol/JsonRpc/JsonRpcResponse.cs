namespace AutoContext.Engine.Protocol.JsonRpc;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of a single outbound JSON-RPC 2.0 response frame.
/// Either <see cref="Result"/> or <see cref="Error"/> is populated,
/// never both — the dispatcher chooses based on the handler outcome.
/// Per <c>design § Lifecycle &gt; Wire-protocol handshake</c> every
/// reply to an <c>rpc</c> request is one of these frames; the
/// <see cref="Id"/> echoes the request's <c>id</c> verbatim so the
/// client can correlate.
/// </summary>
public sealed record JsonRpcResponse
{
    /// <summary>
    /// Protocol marker. Must equal <c>"2.0"</c>.
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = JsonRpcVersion.Value;

    /// <summary>
    /// Echo of the request <c>id</c>. May be absent
    /// (<see cref="JsonValueKind.Undefined"/>) if the request had
    /// no id.
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement Id { get; init; }

    /// <summary>
    /// Method-specific success payload. Present when the handler
    /// succeeded; absent on error responses.
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; init; }

    /// <summary>
    /// Structured failure description. Present when the handler
    /// failed; absent on success responses.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; init; }
}
