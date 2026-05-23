namespace AutoContext.Engine.Core.Tests.Support.Rpc;

using System.Text.Json;

using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

/// <summary>
/// Drives a <see cref="LengthPrefixedFrameCodec"/> like a JSON-RPC test
/// client: writes a single request, reads a single response, and offers
/// a <see cref="DriveFollowUpAsync"/> convenience that chains both.
/// </summary>
internal static class JsonRpcTestClient
{
    public static async Task<JsonRpcResponse?> DriveFollowUpAsync(LengthPrefixedFrameCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        await WriteRequestAsync(codec, id: 999, method: "Test.FollowUp", TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync(codec, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    public static async Task WriteRequestAsync(
        LengthPrefixedFrameCodec codec, int id, string method, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var request = new JsonRpcRequest
        {
            Jsonrpc = JsonRpcVersion.Value,
            Id = JsonSerializer.SerializeToElement(id),
            Method = method,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);
        await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<JsonRpcResponse> ReadResponseAsync(
        LengthPrefixedFrameCodec codec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codec);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        var bytes = await codec.ReadAsync(cts.Token).ConfigureAwait(false);
        Assert.NotNull(bytes);
        var response = JsonSerializer.Deserialize(
            bytes!, ProtocolJsonContext.Default.JsonRpcResponse);
        Assert.NotNull(response);
        return response!;
    }
}
