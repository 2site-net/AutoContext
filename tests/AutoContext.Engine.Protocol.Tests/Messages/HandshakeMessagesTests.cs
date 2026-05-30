namespace AutoContext.Engine.Protocol.Tests.Messages;

using System.Text.Json;

using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;

public sealed class HandshakeMessagesTests
{
    [Fact]
    public void Should_deserialize_hello_params_from_camelCase_protocol_version()
    {
        // Arrange
        var json = """{"protocolVersion":1}""";

        // Act
        var helloParams = JsonSerializer.Deserialize(
            json, ProtocolJsonContext.Default.JsonHandshakeParams);

        // Assert
        Assert.NotNull(helloParams);
        Assert.Equal(1, helloParams!.ProtocolVersion);
    }

    [Fact]
    public void Should_serialize_hello_result_with_camelCase_fields()
    {
        // Arrange
        var result = new JsonHandshakeResult
        {
            ProtocolVersion = 1,
            EngineVersion = "0.9.5",
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            result, ProtocolJsonContext.Default.JsonHandshakeResult);

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32()),
            () => Assert.Equal("0.9.5", root.GetProperty("engineVersion").GetString()));
    }

    [Fact]
    public void Should_expose_hello_method_constant_matching_design()
    {
        // The wire identifier is part of the contract; this guards
        // against accidental rename. See design § Lifecycle &gt;
        // RPC surface.
        Assert.Equal("Engine.Hello", ProtocolMethods.Hello);
    }
}
