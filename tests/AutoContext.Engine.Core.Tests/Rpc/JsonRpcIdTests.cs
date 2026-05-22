namespace AutoContext.Engine.Core.Tests.Rpc;

using System.Text.Json;

using AutoContext.Engine.Core.Rpc;

public sealed class JsonRpcIdTests
{
    [Fact]
    public void Should_expose_Null_as_a_JsonValueKind_Null_element()
    {
        // Arrange + Act
        var id = JsonRpcId.Null;

        // Assert
        Assert.Equal(JsonValueKind.Null, id.ValueKind);
    }

    [Fact]
    public void Should_normalize_Undefined_to_Null()
    {
        // Arrange
        var undefined = default(JsonElement);

        // Act
        var normalized = JsonRpcId.Normalize(undefined);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(JsonValueKind.Undefined, undefined.ValueKind),
            () => Assert.Equal(JsonValueKind.Null, normalized.ValueKind));
    }

    [Fact]
    public void Should_normalize_explicit_Null_to_Null()
    {
        // Arrange
        var nullElement = JsonDocument.Parse("null").RootElement;

        // Act
        var normalized = JsonRpcId.Normalize(nullElement);

        // Assert
        Assert.Equal(JsonValueKind.Null, normalized.ValueKind);
    }

    [Fact]
    public void Should_pass_a_numeric_id_through_unchanged()
    {
        // Arrange
        var numeric = JsonDocument.Parse("42").RootElement;

        // Act
        var normalized = JsonRpcId.Normalize(numeric);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(JsonValueKind.Number, normalized.ValueKind),
            () => Assert.Equal(42, normalized.GetInt32()));
    }

    [Fact]
    public void Should_pass_a_string_id_through_unchanged()
    {
        // Arrange
        var text = JsonDocument.Parse("\"alpha\"").RootElement;

        // Act
        var normalized = JsonRpcId.Normalize(text);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(JsonValueKind.String, normalized.ValueKind),
            () => Assert.Equal("alpha", normalized.GetString()));
    }

    [Theory]
    [InlineData("{}", JsonValueKind.Object)]
    [InlineData("[]", JsonValueKind.Array)]
    [InlineData("true", JsonValueKind.True)]
    public void Should_pass_non_spec_id_kinds_through_unchanged(string json, JsonValueKind expectedKind)
    {
        // Arrange
        var id = JsonDocument.Parse(json).RootElement;

        // Act
        var normalized = JsonRpcId.Normalize(id);

        // Assert
        Assert.Equal(expectedKind, normalized.ValueKind);
    }
}
