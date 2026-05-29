namespace AutoContext.Engine.Protocol.JsonRpc;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Base wire shape of a single frame in a server-streaming RPC
/// response. Server-streaming responses replace the canonical
/// single <see cref="JsonRpcResponse"/> with a sequence of these
/// frames, each carrying the request's <c>id</c> verbatim so the
/// client can correlate, terminated by exactly one
/// <see cref="JsonRpcStreamComplete"/> or
/// <see cref="JsonRpcStreamError"/> frame.
/// </summary>
/// <remarks>
/// <para>
/// Per <c>design § Engine binary &gt; RPC pipe</c> server-streaming
/// is used by <c>Logs.TailEngine</c>, <c>Logs.TailWorker</c>,
/// <c>Config.Subscribe</c>, <c>Instructions.Subscribe</c>, and
/// other subscription channels. The frame shape is generic — each
/// method places its method-specific payload inside the
/// <see cref="JsonRpcStreamNext.Result"/> element of a
/// <see cref="JsonRpcStreamNext"/> frame, and the terminal
/// <see cref="JsonRpcStreamComplete"/> /
/// <see cref="JsonRpcStreamError"/> frame is synthesised by the
/// <c>RpcConnectionProcessor</c> on stream exhaustion or fault.
/// </para>
/// <para>
/// Concrete frame types are selected by the <c>"kind"</c>
/// discriminator (<c>"next"</c> / <c>"complete"</c> /
/// <c>"error"</c>). The discriminated record hierarchy guarantees
/// frame shapes are total: a <see cref="JsonRpcStreamComplete"/>
/// cannot carry a <c>result</c>, and a
/// <see cref="JsonRpcStreamNext"/> cannot carry an <c>error</c>.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(JsonRpcStreamNext), typeDiscriminator: "next")]
[JsonDerivedType(typeof(JsonRpcStreamComplete), typeDiscriminator: "complete")]
[JsonDerivedType(typeof(JsonRpcStreamError), typeDiscriminator: "error")]
public abstract record JsonRpcStreamFrame
{
    /// <summary>
    /// Protocol marker. Must equal <c>"2.0"</c>.
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = JsonRpcVersion.Value;

    /// <summary>
    /// Echo of the originating request <c>id</c>. Repeated on every
    /// frame of the stream so clients can correlate without
    /// per-frame state.
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement Id { get; init; }
}
