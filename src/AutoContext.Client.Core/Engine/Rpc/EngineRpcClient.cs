namespace AutoContext.Client.Core.Engine.Rpc;

using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Typed client for the engine's <c>Engine.*</c> RPC family over a
/// live <see cref="EngineConnection"/>. Covers graceful shutdown and
/// the machine-wide liveness registry read. The <c>Engine.Hello</c>
/// handshake is not exposed here — it is performed by the connection
/// before it is handed to any client — and <c>Engine.WriteLog</c> is
/// deliberately absent: that is a worker→engine notification owned by
/// <c>AutoContext.Workers.Core</c>, not a client-side call.
/// </summary>
public sealed class EngineRpcClient
{
    private readonly EngineConnection _connection;

    /// <summary>
    /// Creates a new <see cref="EngineRpcClient"/> over
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">A live, handshaked <c>rpc</c>
    /// connection. Must not be <see langword="null"/>.</param>
    public EngineRpcClient(EngineConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    /// <summary>
    /// Reads the machine-wide engine-liveness registry snapshot the
    /// engine held when the request arrived.
    /// </summary>
    public Task<JsonRegistryEntriesResult> RegistryEntriesAsync(CancellationToken cancellationToken)
        => _connection.InvokeAsync(
            RegistryMethods.RegistryEntries,
            parameters: null,
            ProtocolJsonContext.Default.JsonRegistryEntriesResult,
            cancellationToken);

    /// <summary>
    /// Requests graceful engine shutdown. The engine acknowledges
    /// immediately and then drains and exits.
    /// </summary>
    public Task<JsonShutdownResult> ShutdownAsync(CancellationToken cancellationToken)
        => _connection.InvokeAsync(
            ProtocolMethods.Shutdown,
            parameters: null,
            ProtocolJsonContext.Default.JsonShutdownResult,
            cancellationToken);
}
