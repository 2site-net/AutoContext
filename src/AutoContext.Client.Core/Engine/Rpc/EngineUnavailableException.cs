namespace AutoContext.Client.Core.Engine.Rpc;

/// <summary>
/// Thrown when the resolver cannot reach an engine on the requested
/// endpoint and cannot start one: either the spawn policy disabled
/// spawning (<see cref="ClientOptions.SpawnDisabled"/>) or a spawned
/// engine did not begin accepting connections within the cold-start
/// budget. Distinct from <see cref="EngineProtocolException"/>, which
/// signals that an engine <em>was</em> reached but spoke an
/// incompatible protocol version.
/// </summary>
public sealed class EngineUnavailableException : Exception
{
    /// <summary>
    /// Creates a new <see cref="EngineUnavailableException"/> with a
    /// descriptive message.
    /// </summary>
    public EngineUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new <see cref="EngineUnavailableException"/> wrapping
    /// the underlying connect failure.
    /// </summary>
    public EngineUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
