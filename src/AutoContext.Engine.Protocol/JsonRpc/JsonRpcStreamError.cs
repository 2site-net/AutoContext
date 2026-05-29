namespace AutoContext.Engine.Protocol.JsonRpc;

using System.Text.Json.Serialization;

/// <summary>
/// The terminal frame of a stream that aborted with a structured
/// failure. The <see cref="Error"/> follows the same shape as
/// <see cref="JsonRpcResponse.Error"/>.
/// </summary>
public sealed record JsonRpcStreamError : JsonRpcStreamFrame
{
    /// <summary>
    /// Structured failure description.
    /// </summary>
    [JsonPropertyName("error")]
    public required JsonRpcError Error { get; init; }
}
