namespace AutoContext.Engine.Protocol.Tests.JsonRpc;

using System.Text.Json;

using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Serialization;

public sealed class JsonRpcEnvelopeTests
{
    [Fact]
    public void Should_round_trip_request_through_source_generated_context()
    {
        // Arrange
        var json = """
            {"jsonrpc":"2.0","id":42,"method":"Engine.Hello","params":{"protocolVersion":1}}
            """;

        // Act
        var request = JsonSerializer.Deserialize(
            json, ProtocolJsonContext.Default.JsonRpcRequest);

        // Assert
        Assert.NotNull(request);
        Assert.Multiple(
            () => Assert.Equal("2.0", request!.JsonRpc),
            () => Assert.Equal(42, request!.Id.GetInt32()),
            () => Assert.Equal("Engine.Hello", request!.Method),
            () => Assert.NotNull(request!.Params),
            () => Assert.Equal(JsonValueKind.Object, request!.Params!.Value.ValueKind),
            () => Assert.Equal(1, request!.Params!.Value.GetProperty("protocolVersion").GetInt32()));
    }

    [Fact]
    public void Should_emit_result_and_omit_error_for_success_response()
    {
        // Arrange
        var id = JsonDocument.Parse("7").RootElement;
        var result = JsonDocument.Parse("""{"ok":true}""").RootElement;
        var response = new JsonRpcResponse { Id = id, Result = result };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            response, ProtocolJsonContext.Default.JsonRpcResponse);

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString()),
            () => Assert.Equal(7, root.GetProperty("id").GetInt32()),
            () => Assert.True(root.GetProperty("result").GetProperty("ok").GetBoolean()),
            () => Assert.False(root.TryGetProperty("error", out _)));
    }

    [Fact]
    public void Should_emit_error_and_omit_result_for_error_response()
    {
        // Arrange
        var id = JsonDocument.Parse("null").RootElement;
        var error = new JsonRpcError
        {
            Code = JsonRpcErrorCodes.ProtocolVersionMismatch,
            Message = "mismatch",
        };
        var response = new JsonRpcResponse { Id = id, Error = error };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            response, ProtocolJsonContext.Default.JsonRpcResponse);

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString()),
            () => Assert.Equal(JsonValueKind.Null, root.GetProperty("id").ValueKind),
            () => Assert.Equal(
                JsonRpcErrorCodes.ProtocolVersionMismatch,
                root.GetProperty("error").GetProperty("code").GetInt32()),
            () => Assert.Equal("mismatch", root.GetProperty("error").GetProperty("message").GetString()),
            () => Assert.False(root.TryGetProperty("result", out _)));
    }
}
