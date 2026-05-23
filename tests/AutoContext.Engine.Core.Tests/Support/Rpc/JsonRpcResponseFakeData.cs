namespace AutoContext.Engine.Core.Tests.Support.Rpc;

using System.Text.Json;

using AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Builds canned <see cref="JsonRpcResponse"/> payloads (empty-result
/// success or coded error) used by RPC dispatch and connection tests.
/// </summary>
internal static class JsonRpcResponseFakeData
{
    public static JsonRpcResponse BuildOkResponse() =>
        new() { Result = JsonSerializer.SerializeToElement<object?>(new { }) };

    public static JsonRpcResponse BuildErrorResponse(int code, string message) =>
        new()
        {
            Error = new JsonRpcError { Code = code, Message = message },
        };
}
