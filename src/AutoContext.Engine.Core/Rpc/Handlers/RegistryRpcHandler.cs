namespace AutoContext.Engine.Core.Rpc.Handlers;

using System;
using System.Collections.Generic;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// The <c>Engine.RegistryEntries</c> handler. Reads the engine instance
/// registry through the <see cref="RegistryFileReader"/> and returns the
/// current entries. A faulted read replies
/// <see cref="JsonRpcErrorCodes.InternalError"/> and the connection keeps
/// serving.
/// </summary>
internal sealed partial class RegistryRpcHandler : IRpcMethodHandler
{
    private readonly ILogger<RegistryRpcHandler> _logger;
    private readonly RegistryFileReader _registryReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryRpcHandler"/>
    /// class.
    /// </summary>
    public RegistryRpcHandler(
        RegistryFileReader registryReader,
        ILogger<RegistryRpcHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(registryReader);
        ArgumentNullException.ThrowIfNull(logger);

        _registryReader = registryReader;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Methods { get; } = [RegistryMethods.RegistryEntries];

    /// <inheritdoc />
    public async ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var entries = await _registryReader.ReadAsync(cancellationToken)
                .ConfigureAwait(false);

            var result = new JsonRegistryEntriesResult { Entries = entries };
            return RpcResults.Success(result, ProtocolJsonContext.Default.JsonRegistryEntriesResult);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRegistryEntriesFailed(_logger, ex);
            return RpcResults.InternalError("Failed to read the engine registry.");
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Engine.RegistryEntries handler failed to read the registry.")]
    private static partial void LogRegistryEntriesFailed(ILogger logger, Exception exception);
}
