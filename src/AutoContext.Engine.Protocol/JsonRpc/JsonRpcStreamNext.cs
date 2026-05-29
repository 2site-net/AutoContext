namespace AutoContext.Engine.Protocol.JsonRpc;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// A non-terminal frame carrying one method-specific payload.
/// The <see cref="Result"/> element is shaped by the streaming
/// method's per-frame contract.
/// </summary>
public sealed record JsonRpcStreamNext : JsonRpcStreamFrame
{
    /// <summary>
    /// Method-specific payload for this frame.
    /// </summary>
    [JsonPropertyName("result")]
    public required JsonElement Result { get; init; }
}
