namespace AutoContext.Framework.Protocol.Tests;

using System.Globalization;

public class EndpointTests
{
    private const string SampleHash = "0123456789abcdef";
    private const string SampleInstanceText = "12345678-1234-4567-8901-234567890abc";
    private static readonly Guid SampleInstanceId = Guid.Parse("12345678-1234-4567-8901-234567890abc");

    [Theory]
    [InlineData(EndpointKind.Rpc, "rpc")]
    [InlineData(EndpointKind.Events, "events")]
    [InlineData(EndpointKind.Health, "health")]
    [InlineData(EndpointKind.Logs, "logs")]
    public void Should_format_canonical_wire_shape_when_converted_to_string(EndpointKind kind, string wireKind)
    {
        // Arrange
        var endpoint = new Endpoint(kind, SampleHash, SampleInstanceId);

        // Act
        var rendered = endpoint.ToString();

        // Assert
        Assert.Equal($"autocontext-engine:{wireKind}@{SampleHash}#{SampleInstanceText}", rendered);
    }

    [Theory]
    [InlineData(EndpointKind.Rpc)]
    [InlineData(EndpointKind.Events)]
    [InlineData(EndpointKind.Health)]
    [InlineData(EndpointKind.Logs)]
    public void Should_round_trip_through_to_string_and_parse(EndpointKind kind)
    {
        // Arrange
        var original = new Endpoint(kind, SampleHash, SampleInstanceId);

        // Act
        var parsed = Endpoint.Parse(original.ToString(), CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void Should_accept_canonical_form_when_try_parsing()
    {
        // Arrange
        var input = $"autocontext-engine:rpc@{SampleHash}#{SampleInstanceText}";

        // Act
        var ok = Endpoint.TryParse(input, provider: null, out var endpoint);

        // Assert
        Assert.Multiple(
            () => Assert.True(ok),
            () => Assert.Equal(EndpointKind.Rpc, endpoint.Kind),
            () => Assert.Equal(SampleHash, endpoint.WorkspaceHash),
            () => Assert.Equal(SampleInstanceId, endpoint.InstanceId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("autocontext-engine:rpc@0123456789abcdef")]
    [InlineData("autocontext-engine:rpc0123456789abcdef#12345678-1234-4567-8901-234567890abc")]
    [InlineData("autocontext:rpc@0123456789abcdef#12345678-1234-4567-8901-234567890abc")]
    [InlineData("autocontext-engine:bogus@0123456789abcdef#12345678-1234-4567-8901-234567890abc")]
    [InlineData("autocontext-engine:rpc@0123456789ABCDEF#12345678-1234-4567-8901-234567890abc")]
    [InlineData("autocontext-engine:rpc@0123456789abcde#12345678-1234-4567-8901-234567890abc")]
    [InlineData("autocontext-engine:rpc@0123456789abcdef0#12345678-1234-4567-8901-234567890abc")]
    [InlineData("autocontext-engine:rpc@0123456789abcdez#12345678-1234-4567-8901-234567890abc")]
    [InlineData("autocontext-engine:rpc@0123456789abcdef#12345678-1234-4567-8901-234567890ABC")]
    [InlineData("autocontext-engine:rpc@0123456789abcdef#not-a-uuid")]
    [InlineData("autocontext-engine:@0123456789abcdef#12345678-1234-4567-8901-234567890abc")]
    [InlineData("autocontext-engine:rpc@#12345678-1234-4567-8901-234567890abc")]
    [InlineData("autocontext-engine:rpc@0123456789abcdef#")]
    public void Should_reject_malformed_input_when_try_parsing(string? input)
    {
        // Act
        var ok = Endpoint.TryParse(input, provider: null, out var endpoint);

        // Assert
        Assert.Multiple(
            () => Assert.False(ok),
            () => Assert.Equal(default, endpoint));
    }

    [Fact]
    public void Should_throw_format_exception_when_parsing_malformed_input()
    {
        // Act & Assert
        Assert.Throws<FormatException>(
            () => Endpoint.Parse("not an endpoint", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Should_compare_equal_when_all_components_match()
    {
        // Arrange
        var a = new Endpoint(EndpointKind.Events, SampleHash, SampleInstanceId);
        var b = new Endpoint(EndpointKind.Events, SampleHash, SampleInstanceId);

        // Act & Assert
        Assert.Multiple(
            () => Assert.Equal(a, b),
            () => Assert.Equal(a.GetHashCode(), b.GetHashCode()));
    }

    [Fact]
    public void Should_not_compare_equal_when_kind_differs()
    {
        // Arrange
        var baseline = new Endpoint(EndpointKind.Events, SampleHash, SampleInstanceId);
        var other = new Endpoint(EndpointKind.Rpc, SampleHash, SampleInstanceId);

        // Act & Assert
        Assert.NotEqual(baseline, other);
    }

    [Fact]
    public void Should_not_compare_equal_when_workspace_hash_differs()
    {
        // Arrange
        var baseline = new Endpoint(EndpointKind.Events, SampleHash, SampleInstanceId);
        var other = new Endpoint(EndpointKind.Events, "fedcba9876543210", SampleInstanceId);

        // Act & Assert
        Assert.NotEqual(baseline, other);
    }

    [Fact]
    public void Should_not_compare_equal_when_instance_id_differs()
    {
        // Arrange
        var baseline = new Endpoint(EndpointKind.Events, SampleHash, SampleInstanceId);
        var other = new Endpoint(EndpointKind.Events, SampleHash, Guid.NewGuid());

        // Act & Assert
        Assert.NotEqual(baseline, other);
    }
}
