namespace AutoContext.Engine.Tests.Support.Integration;

using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

/// <summary>
/// Client-side handshake and JSON-RPC helpers for the engine
/// integration suite. The cross-process companion to
/// <c>EngineRpcTestClient</c> in the in-process Engine.Core test
/// assembly — duplicated here because that helper is
/// <c>internal</c> to its own assembly and the integration suite
/// runs in a separate test project.
/// </summary>
internal static class EngineWireTestClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReadResponseTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Connects to <paramref name="kind"/> on the engine identified
    /// by <paramref name="workspacePath"/> + <paramref name="instanceId"/>.
    /// </summary>
    internal static async Task<NamedPipeClientStream> ConnectAsync(
        EndpointKind kind,
        string workspacePath,
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        var hash = WorkspaceHash.Compute(workspacePath);
        var pipeName = new Endpoint(kind, hash.Value, instanceId).ToString();
        var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        try
        {
            await client.ConnectAsync(ConnectTimeout, cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Writes the mandatory <c>Engine.Hello</c> first frame.</summary>
    internal static async Task SendHelloAsync(
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

    /// <summary>Writes a parameter-less JSON-RPC request with the given id and method.</summary>
    internal static async Task SendRequestAsync(
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

    /// <summary>Reads exactly one JSON-RPC response frame.</summary>
    internal static async Task<JsonRpcResponse> ReadResponseAsync(
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
