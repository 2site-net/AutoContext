namespace AutoContext.Engine.Protocol;

/// <summary>
/// The four logical channels the engine binds per (workspace, launcher
/// instance) pair — separated by purpose so a slow consumer on one
/// channel never back-pressures another.
/// </summary>
/// <remarks>
/// The wire spelling of each kind is the lowercase enum name — see
/// <see cref="Endpoint"/> for the canonical address format. The semantics
/// mirror <c>design § Lifecycle &gt; Pipe topology</c>. The "endpoint"
/// vocabulary is transport-neutral: the four channels are backed by named
/// pipes today, but the address shape and these semantics do not depend on
/// that choice.
/// </remarks>
public enum EndpointKind
{
    /// <summary>
    /// Request/response and server-streaming RPC. Requires the
    /// <c>Engine.Hello</c> handshake. Keep-alive — connections on this
    /// channel pin the engine alive against the idle-timeout gate.
    /// </summary>
    Rpc,

    /// <summary>
    /// Engine-broadcast lifecycle stream (<c>Engine.Lifecycle.Subscribe</c>
    /// and future global broadcasts). Requires the <c>Engine.Hello</c>
    /// handshake. Keep-alive.
    /// </summary>
    Events,

    /// <summary>
    /// Passive readiness / heartbeat probe. Cheap connect-and-read shape;
    /// no handshake required. Not keep-alive — connections here do not pin
    /// the engine alive.
    /// </summary>
    Health,

    /// <summary>
    /// Server-streaming log tail — unified sink for engine-emitted and
    /// worker-emitted records, distinguished by the <c>category</c> field
    /// on every record. No handshake required, not keep-alive.
    /// </summary>
    Logs,
}
