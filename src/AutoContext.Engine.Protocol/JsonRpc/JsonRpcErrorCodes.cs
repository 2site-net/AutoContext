namespace AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Numeric error codes carried on the <c>code</c> field of a
/// <see cref="JsonRpcError"/>. Combines the JSON-RPC 2.0 reserved
/// pre-defined range with the engine's server-defined allocations
/// from the <c>-32000…-32099</c> implementation-defined band.
/// </summary>
/// <remarks>
/// The pre-defined JSON-RPC codes (e.g. <see cref="MethodNotFound"/>)
/// are required by the specification; the engine-specific codes
/// document the failure modes the handshake and dispatcher surface
/// today. Add new server-defined codes only with a corresponding
/// design-doc entry so clients can rely on stable semantics.
/// </remarks>
public static class JsonRpcErrorCodes
{
    /// <summary>
    /// JSON-RPC 2.0 pre-defined: invalid JSON received by the
    /// server.
    /// </summary>
    public const int ParseError = -32700;

    /// <summary>
    /// JSON-RPC 2.0 pre-defined: the JSON sent is not a valid
    /// Request object.
    /// </summary>
    public const int InvalidRequest = -32600;

    /// <summary>
    /// JSON-RPC 2.0 pre-defined: the requested method does not exist
    /// or is not available.
    /// </summary>
    public const int MethodNotFound = -32601;

    /// <summary>
    /// JSON-RPC 2.0 pre-defined: invalid method parameter(s).
    /// </summary>
    public const int InvalidParams = -32602;

    /// <summary>
    /// JSON-RPC 2.0 pre-defined: internal JSON-RPC error.
    /// </summary>
    public const int InternalError = -32603;

    /// <summary>
    /// Engine server-defined: the first frame on an <c>rpc</c> or
    /// <c>events</c> pipe was not <c>Engine.Hello</c>. Per
    /// <c>design § Lifecycle &gt; Wire-protocol handshake</c> the
    /// handshake is mandatory and the engine refuses any other
    /// method before it completes.
    /// </summary>
    public const int HelloRequired = -32000;

    /// <summary>
    /// Engine server-defined: the client's <c>protocolVersion</c>
    /// does not match <see cref="ProtocolVersion.Current"/>. The
    /// engine refuses hard — no minimum-version negotiation,
    /// exact-match only.
    /// </summary>
    public const int ProtocolVersionMismatch = -32001;
}
