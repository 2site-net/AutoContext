namespace AutoContext.Client.Core.Engine.Rpc;

/// <summary>
/// Thrown when an engine RPC returns a JSON-RPC error response, or a
/// server-streaming subscription terminates with an error frame, or
/// the engine's success payload cannot be parsed into the expected
/// result shape. Distinct from <see cref="EngineProtocolException"/>
/// (handshake / version refusal) and <see cref="EngineUnavailableException"/>
/// (no engine reachable): those are connection-establishment failures,
/// this is a fault reported by an engine the client already reached.
/// </summary>
public sealed class EngineRpcException : Exception
{
    /// <summary>
    /// Creates an exception for a method that faulted without a
    /// numeric error code (e.g. an unparsable result).
    /// </summary>
    public EngineRpcException(string method, string message)
        : base($"Engine RPC '{method}' failed: {message}")
    {
        Method = method;
    }

    /// <summary>
    /// Creates an exception for a method that faulted without a
    /// numeric error code, wrapping the underlying cause.
    /// </summary>
    public EngineRpcException(string method, string message, Exception innerException)
        : base($"Engine RPC '{method}' failed: {message}", innerException)
    {
        Method = method;
    }

    /// <summary>
    /// Creates an exception for a JSON-RPC error response or error
    /// stream frame, carrying the engine's numeric code.
    /// </summary>
    public EngineRpcException(string method, int errorCode, string message)
        : base($"Engine RPC '{method}' failed ({errorCode}): {message}")
    {
        Method = method;
        ErrorCode = errorCode;
    }

    /// <summary>The JSON-RPC error code the engine reported, when the
    /// fault came from an error response or stream frame; otherwise
    /// <see langword="null"/>.</summary>
    public int? ErrorCode { get; }

    /// <summary>The JSON-RPC method that faulted.</summary>
    public string Method { get; }
}
