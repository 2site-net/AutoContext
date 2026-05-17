namespace AutoContext.Engine.Core.Tests.Testing.Utils;

using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

/// <summary>
/// Shared client-side helpers for engine pipe tests: connecting to
/// a bound endpoint, sending the <c>Engine.Hello</c> handshake,
/// issuing a JSON-RPC request, and reading a single response frame.
/// Kept in <c>Testing/Utils</c> per the repo's test conventions so
/// individual test classes stay focused on Arrange/Act/Assert.
/// </summary>
internal static class EngineRpcTestClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReadResponseTimeout = TimeSpan.FromSeconds(5);

    public static async Task<NamedPipeClientStream> ConnectAsync(
        EndpointKind kind,
        EngineOptions options,
        CancellationToken cancellationToken)
    {
        var workspaceHash = WorkspaceHash.Compute(options.WorkspacePath);
        var pipeName = new Endpoint(kind, workspaceHash.Value, options.InstanceId).ToString();
        var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(ConnectTimeout, cancellationToken).ConfigureAwait(false);
        return client;
    }

    public static async Task SendHelloAsync(
        LengthPrefixedFrameCodec codec,
        int protocolVersion,
        CancellationToken cancellationToken)
    {
        var paramsElement = JsonSerializer.SerializeToElement(
            new HandshakeParams { ProtocolVersion = protocolVersion },
            ProtocolJsonContext.Default.HandshakeParams);

        var request = new JsonRpcRequest
        {
            Jsonrpc = JsonRpcVersion.Value,
            Id = JsonDocument.Parse("1").RootElement,
            Method = ProtocolMethods.Hello,
            Params = paramsElement,
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);

        await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public static async Task SendRequestAsync(
        LengthPrefixedFrameCodec codec,
        int id,
        string method,
        CancellationToken cancellationToken)
    {
        var idElement = JsonDocument
            .Parse(id.ToString(CultureInfo.InvariantCulture))
            .RootElement;
        var request = new JsonRpcRequest
        {
            Jsonrpc = JsonRpcVersion.Value,
            Id = idElement,
            Method = method,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);
        await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<JsonRpcResponse> ReadResponseAsync(
        LengthPrefixedFrameCodec codec,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ReadResponseTimeout);

        var bytes = await codec.ReadAsync(cts.Token).ConfigureAwait(false);
        Assert.NotNull(bytes);

        var response = JsonSerializer.Deserialize(
            bytes!, ProtocolJsonContext.Default.JsonRpcResponse);
        Assert.NotNull(response);
        return response!;
    }
}
