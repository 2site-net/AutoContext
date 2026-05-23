namespace AutoContext.Engine.Core.Tests.Support.Rpc;

using System.Text.Json;

using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Builds minimal <see cref="JsonRpcRequest"/> values used by RPC
/// policy tests: a plain request with a stub <c>id</c>/<c>method</c>,
/// and a <c>hello</c> request carrying a <see cref="HandshakeParams"/>.
/// </summary>
internal static class JsonRpcRequestTestFactory
{
    public static JsonRpcRequest BuildRequest(string method) =>
        new()
        {
            Method = method,
            Id = JsonSerializer.SerializeToElement(1),
        };

    public static JsonRpcRequest BuildHelloRequest(string method, int? protocolVersion)
    {
        var helloParams = new HandshakeParams { ProtocolVersion = protocolVersion };
        var paramsElement = JsonSerializer.SerializeToElement(
            helloParams, ProtocolJsonContext.Default.HandshakeParams);
        return new JsonRpcRequest
        {
            Method = method,
            Id = JsonSerializer.SerializeToElement(1),
            Params = paramsElement,
        };
    }
}
