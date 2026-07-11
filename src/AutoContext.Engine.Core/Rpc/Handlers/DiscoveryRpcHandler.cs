namespace AutoContext.Engine.Core.Rpc.Handlers;

using System;
using System.Collections.Generic;

using AutoContext.Engine.Core.Features.Discovery;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Discovery;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// The <c>Discovery.*</c> handler. Marshals <c>Discovery.RouteForPrompt</c>
/// and <c>Discovery.RouteForTool</c> onto the <see cref="DiscoveryService"/>,
/// which answers from indices the engine already owns. Both methods read
/// in-memory state and always succeed once their params parse, so the
/// connection keeps serving; a malformed payload replies
/// <see cref="JsonRpcErrorCodes.InvalidParams"/>.
/// </summary>
internal sealed class DiscoveryRpcHandler : IRpcMethodHandler
{
    private readonly DiscoveryService _discoveryService;
    private readonly ILogger<DiscoveryRpcHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryRpcHandler"/>
    /// class.
    /// </summary>
    public DiscoveryRpcHandler(DiscoveryService discoveryService, ILogger<DiscoveryRpcHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(discoveryService);
        ArgumentNullException.ThrowIfNull(logger);

        _discoveryService = discoveryService;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Methods { get; } =
        [DiscoveryMethods.RouteForPrompt, DiscoveryMethods.RouteForTool];

    /// <inheritdoc />
    public ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        RpcHandlerResult result = request.Method switch
        {
            DiscoveryMethods.RouteForTool => HandleRouteForTool(request),
            _ => HandleRouteForPrompt(request),
        };

        return ValueTask.FromResult(result);
    }

    private UnaryHandlerResult HandleRouteForPrompt(JsonRpcRequest request)
    {
        var parseError = RpcMethodResults.TryDeserialize(
            request,
            DiscoveryMethods.RouteForPrompt,
            ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptParams,
            _logger,
            out var parameters);

        if (parseError is not null)
        {
            return parseError;
        }

        var result = _discoveryService.RouteForPrompt(parameters?.Prompt ?? string.Empty);
        return RpcMethodResults.Success(result, ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptResult);
    }

    private UnaryHandlerResult HandleRouteForTool(JsonRpcRequest request)
    {
        var parseError = RpcMethodResults.TryDeserialize(
            request,
            DiscoveryMethods.RouteForTool,
            ProtocolJsonContext.Default.JsonDiscoveryRouteForToolParams,
            _logger,
            out var parameters);

        if (parseError is not null)
        {
            return parseError;
        }

        var result = _discoveryService.RouteForTool(parameters?.Name ?? string.Empty);
        return RpcMethodResults.Success(result, ProtocolJsonContext.Default.JsonDiscoveryRouteForToolResult);
    }
}
