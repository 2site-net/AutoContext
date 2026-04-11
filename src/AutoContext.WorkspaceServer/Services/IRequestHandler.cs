namespace AutoContext.WorkspaceServer.Services;

/// <summary>
/// Handles a named pipe request of a specific type.
/// </summary>
internal interface IRequestHandler
{
    /// <summary>Gets the request type discriminator this handler responds to.</summary>
    string RequestType { get; }

    /// <summary>Processes the raw JSON request and returns the serialized response.</summary>
    byte[] Process(ReadOnlySpan<byte> json);
}
