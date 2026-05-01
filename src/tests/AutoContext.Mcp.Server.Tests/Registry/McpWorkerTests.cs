namespace AutoContext.Mcp.Server.Tests.Registry;

using AutoContext.Mcp.Server.Registry;

public sealed class McpWorkerTests
{
    [Theory]
    [InlineData("dotnet")]
    [InlineData("workspace")]
    [InlineData("web")]
    [InlineData("a")]
    [InlineData("a1")]
    [InlineData("test-guid-abc123")]
    public void Should_accept_kebab_case_lowercase_id(string id)
    {
        var worker = new McpWorker
        {
            Id = id,
            Name = "AutoContext.Worker.Test",
            Tools = [],
        };

        Assert.Equal(id, worker.Id);
        Assert.Equal($"worker-{id}", worker.Role);
    }

    [Theory]
    [InlineData("DotNet")]        // uppercase
    [InlineData("dot_net")]       // underscore
    [InlineData("dot.net")]       // dot
    [InlineData("1dotnet")]       // leading digit
    [InlineData("-dotnet")]       // leading hyphen
    [InlineData("dotnet ")]       // trailing whitespace
    public void Should_reject_invalid_id(string id)
    {
        var ex = Assert.Throws<ArgumentException>(() => new McpWorker
        {
            Id = id,
            Name = "AutoContext.Worker.Test",
            Tools = [],
        });

        Assert.Contains("kebab-case", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_reject_empty_or_whitespace_id(string id)
    {
        Assert.Throws<ArgumentException>(() => new McpWorker
        {
            Id = id,
            Name = "AutoContext.Worker.Test",
            Tools = [],
        });
    }
}
