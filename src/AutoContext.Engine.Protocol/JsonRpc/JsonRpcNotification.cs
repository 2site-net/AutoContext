namespace AutoContext.Engine.Protocol.JsonRpc;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of an outbound JSON-RPC 2.0 notification frame. A
/// notification carries <c>jsonrpc</c>, <c>method</c>, and an
/// opaque <c>params</c> payload but — unlike a request — has no
/// <c>id</c> and never receives a response. Per
/// <c>design § Pipe topology &gt; events</c> the engine pushes
/// lifecycle and (future) agent broadcasts to subscribed clients
/// as notifications, leaving the <c>id</c>-keyed reply machinery
/// to the <c>rpc</c> pipe.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Params"/> is deliberately typed as
/// <see cref="JsonElement"/> so the notification envelope is
/// reusable across broadcast families (Engine.Lifecycle today,
/// Engine.Agent.* later) without a new envelope per family. The
/// concrete payload record is serialized into a
/// <see cref="JsonElement"/> by the producer and decoded by the
/// consumer against the per-method DTO.
/// </para>
/// </remarks>
public sealed record JsonRpcNotification
{
    /// <summary>
    /// Protocol marker. Must equal <c>"2.0"</c> per JSON-RPC 2.0.
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = JsonRpcVersion.Value;

    /// <summary>
    /// Broadcast method name (e.g. <c>"Engine.Lifecycle"</c>).
    /// Acts as the discriminator on the receiving side so a single
    /// pipe can multiplex multiple broadcast families.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// Opaque payload for the broadcast. The consumer
    /// re-deserializes this against the per-method DTO. Absent on
    /// notifications that carry no payload; absent fields read
    /// back as <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}
