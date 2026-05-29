namespace AutoContext.Engine.Core.Rpc;

/// <summary>
/// Whether the <see cref="RpcConnectionProcessor"/> should keep
/// serving a connection after a frame-level failure (frame not
/// valid JSON, frame not a valid JSON-RPC 2.0 request).
/// </summary>
internal enum FrameFailurePolicy
{
    /// <summary>
    /// Write the appropriate JSON-RPC error reply
    /// (<see cref="Protocol.JsonRpc.JsonRpcErrorCodes.ParseError"/>
    /// or
    /// <see cref="Protocol.JsonRpc.JsonRpcErrorCodes.InvalidRequest"/>)
    /// and continue reading subsequent frames on the same
    /// connection. Used by the post-handshake RPC dispatch loop,
    /// where a single bad frame is recoverable and the peer is
    /// expected to keep sending well-formed requests.
    /// </summary>
    Recover,

    /// <summary>
    /// Write the JSON-RPC error reply if possible, then close the
    /// connection (the processor returns <see langword="false"/>).
    /// Used by the <c>Engine.Hello</c> handshake, where a
    /// malformed first frame is a packaging or wire-protocol bug
    /// that must surface immediately rather than be silently
    /// retried.
    /// </summary>
    Terminate,
}
