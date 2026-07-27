namespace AutoContext.Client.Core.Engine.Rpc;

/// <summary>
/// Thrown when the mandatory <c>Engine.Hello</c> handshake refuses the
/// connection: the engine reports a protocol version that does not
/// exactly match the client's, or it answers the handshake with an
/// error or an unparsable frame. The protocol is exact-match by design
/// — engine and client ship versioned together — so this signals a
/// packaging mismatch, not a negotiable condition.
/// </summary>
public sealed class EngineProtocolException : Exception
{
    /// <summary>
    /// Creates a new <see cref="EngineProtocolException"/> with a
    /// descriptive message.
    /// </summary>
    public EngineProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new <see cref="EngineProtocolException"/> wrapping the
    /// underlying cause.
    /// </summary>
    public EngineProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
