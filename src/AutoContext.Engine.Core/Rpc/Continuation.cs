namespace AutoContext.Engine.Core.Rpc;

/// <summary>
/// What the <see cref="RpcConnectionProcessor"/> should do after
/// flushing a handler's response.
/// </summary>
internal enum Continuation
{
    /// <summary>
    /// Write the response and read the next frame. The default —
    /// used by request/response methods that do not terminate the
    /// connection (e.g. <c>Engine.RegistryEntries</c>).
    /// </summary>
    Continue,

    /// <summary>
    /// Write the response, run any post-flush side effect, and
    /// exit the loop. The processor reports this outcome as
    /// success (<see cref="RpcConnectionProcessor"/> returns
    /// <see langword="true"/>). Used by success-path terminal
    /// methods such as the <c>Engine.Hello</c> handshake reply or
    /// the <c>Engine.Shutdown</c> acknowledgement.
    /// </summary>
    Complete,

    /// <summary>
    /// Write the response, run any post-flush side effect, and
    /// exit the loop. The processor reports this outcome as
    /// failure (<see cref="RpcConnectionProcessor"/> returns
    /// <see langword="false"/>). Used by failure-path terminal
    /// methods such as a refused handshake on an
    /// <see cref="Protocol.EndpointKind.Rpc"/> or
    /// <see cref="Protocol.EndpointKind.Events"/>
    /// connection.
    /// </summary>
    Abort,
}
