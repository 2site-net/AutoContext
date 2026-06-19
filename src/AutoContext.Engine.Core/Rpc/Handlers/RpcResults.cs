namespace AutoContext.Engine.Core.Rpc.Handlers;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using AutoContext.Engine.Core.Rpc;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.JsonRpc;

using Microsoft.Extensions.Logging;

/// <summary>
/// Shared JSON-RPC result builders for the <see cref="IRpcMethodHandler"/>
/// implementations. Each helper produces a <see cref="UnaryHandlerResult"/>
/// that keeps the connection serving
/// (<see cref="Continuation.Continue"/>) — recoverable faults are surfaced
/// as JSON-RPC errors rather than tearing the connection down.
/// </summary>
internal static partial class RpcResults
{
    /// <summary>
    /// Builds an <see cref="JsonRpcErrorCodes.InternalError"/> reply with
    /// the supplied operator-facing message.
    /// </summary>
    public static UnaryHandlerResult InternalError(string message)
        => new(
            Response: new JsonRpcResponse
            {
                Error = new JsonRpcError
                {
                    Code = JsonRpcErrorCodes.InternalError,
                    Message = message,
                },
            },
            Continuation: Continuation.Continue);

    /// <summary>
    /// Builds an <see cref="JsonRpcErrorCodes.InvalidParams"/> reply for the
    /// named method.
    /// </summary>
    public static UnaryHandlerResult InvalidParams(string method)
        => new(
            Response: new JsonRpcResponse
            {
                Error = new JsonRpcError
                {
                    Code = JsonRpcErrorCodes.InvalidParams,
                    Message = $"Invalid params for '{method}'.",
                },
            },
            Continuation: Continuation.Continue);

    /// <summary>
    /// Builds a success reply, serializing <paramref name="result"/> through
    /// the source-generated <paramref name="typeInfo"/>.
    /// </summary>
    public static UnaryHandlerResult Success<T>(T result, JsonTypeInfo<T> typeInfo)
        => new(
            Response: new JsonRpcResponse
            {
                Result = JsonSerializer.SerializeToElement(result, typeInfo),
            },
            Continuation: Continuation.Continue);

    /// <summary>
    /// Deserializes the request params through the source-generated
    /// <paramref name="typeInfo"/>. Returns <see langword="null"/> on success
    /// (with <paramref name="parameters"/> set, possibly to the default when
    /// no params were supplied); on a malformed payload it logs the fault and
    /// returns an <see cref="JsonRpcErrorCodes.InvalidParams"/> reply for the
    /// caller to short-circuit on.
    /// </summary>
    public static UnaryHandlerResult? TryDeserialize<T>(
        JsonRpcRequest request,
        string method,
        JsonTypeInfo<T> typeInfo,
        ILogger logger,
        out T? parameters)
    {
        try
        {
            parameters = request.Params is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } element
                ? element.Deserialize(typeInfo)
                : default;
            return null;
        }
        catch (JsonException ex)
        {
            LogParamsParseFailed(logger, method, ex);
            parameters = default;
            return InvalidParams(method);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "RPC dispatch could not parse params for '{Method}'.")]
    private static partial void LogParamsParseFailed(ILogger logger, string method, Exception exception);
}
