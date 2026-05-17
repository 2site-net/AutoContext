namespace AutoContext.Engine.Protocol.JsonRpc;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Structured error body of a JSON-RPC 2.0 failure response.
/// Carries a numeric <see cref="Code"/> (see <see cref="JsonRpcErrorCodes"/>
/// for the engine's allocations), a human-readable <see cref="Message"/>,
/// and an optional opaque <see cref="Data"/> payload the handler
/// may use to attach machine-readable diagnostics
/// (e.g. <c>{ "expected": 1, "got": 2 }</c> for a protocol-version
/// mismatch).
/// </summary>
public sealed record JsonRpcError
{
    /// <summary>
    /// Numeric error code. See <see cref="JsonRpcErrorCodes"/> for
    /// the standard JSON-RPC reservations and the engine's
    /// server-defined codes.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    /// <summary>
    /// Short human-readable description of the failure.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional machine-readable diagnostics attached by the handler.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Data { get; init; }
}
