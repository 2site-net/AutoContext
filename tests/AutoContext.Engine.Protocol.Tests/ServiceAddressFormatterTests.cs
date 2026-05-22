namespace AutoContext.Engine.Protocol.Tests;

public sealed class ServiceAddressFormatterTests
{
    [Fact]
    public void Format_WithoutInstanceId_OmitsHashSuffix()
    {
        var address = ServiceAddressFormatter.Format("log", instanceId: null);

        Assert.Equal("autocontext.log", address);
    }

    [Fact]
    public void Format_WithInstanceId_AppendsHashSuffix()
    {
        var address = ServiceAddressFormatter.Format("worker-dotnet", "abc123");

        Assert.Equal("autocontext.worker-dotnet#abc123", address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_WhitespaceOrEmptyInstanceId_TreatedAsAbsent(string instanceId)
    {
        var address = ServiceAddressFormatter.Format("log", instanceId);

        Assert.Equal("autocontext.log", address);
    }

    [Fact]
    public void TryParseRole_WithoutInstanceId_ReturnsRole()
    {
        var ok = ServiceAddressFormatter.TryParseRole("autocontext.log", out var role);

        Assert.True(ok);
        Assert.Equal("log", role);
    }

    [Fact]
    public void TryParseRole_WithInstanceId_StripsSuffix()
    {
        var ok = ServiceAddressFormatter.TryParseRole("autocontext.worker-dotnet#abc123", out var role);

        Assert.True(ok);
        Assert.Equal("worker-dotnet", role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("other.log")]
    [InlineData("autocontext.")]
    [InlineData("autocontext.#abc")]
    public void TryParseRole_InvalidAddress_ReturnsFalse(string address)
    {
        var ok = ServiceAddressFormatter.TryParseRole(address, out var role);

        Assert.False(ok);
        Assert.Equal(string.Empty, role);
    }
}
