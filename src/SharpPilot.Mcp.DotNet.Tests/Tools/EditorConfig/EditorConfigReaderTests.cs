namespace SharpPilot.Mcp.DotNet.Tests.Tools.EditorConfig;

using SharpPilot.Mcp.DotNet.Tests.Fakes;
using SharpPilot.Mcp.DotNet.Tools.EditorConfig;

public sealed class EditorConfigReaderTests : IAsyncLifetime, IDisposable
{
    private readonly string _pipeName = $"ec-reader-test-{Guid.NewGuid():N}";
    private readonly CancellationTokenSource _cts = new();
    private readonly FakeWorkspaceServer _server;

    public EditorConfigReaderTests()
    {
        _server = new FakeWorkspaceServer(_pipeName);
    }

    public ValueTask InitializeAsync()
    {
        EditorConfigReader.Configure(_pipeName);
        _ = _server.RunAsync(_cts.Token);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
    }

    public void Dispose()
    {
        _cts.Dispose();
    }

    [Fact]
    public async Task Should_return_warning_when_properties_are_empty()
    {
        // Arrange — server returns empty properties for the path.
        _server.SetResponse("/workspace/file.cs", []);

        // Act
        var result = await EditorConfigReader.ReadAsync("/workspace/file.cs");

        // Assert
        Assert.StartsWith("⚠️", result);
        Assert.Contains("No .editorconfig properties", result);
    }

    [Fact]
    public async Task Should_resolve_properties()
    {
        // Arrange
        _server.SetResponse("/workspace/Program.cs", new()
        {
            ["indent_style"] = "space",
            ["indent_size"] = "4",
        });

        // Act
        var result = await EditorConfigReader.ReadAsync("/workspace/Program.cs");

        // Assert
        Assert.Contains("indent_style = space", result);
        Assert.Contains("indent_size = 4", result);
    }

    [Fact]
    public async Task Should_return_warning_for_whitespace_path()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => EditorConfigReader.ReadAsync("   "));
    }

    [Fact]
    public async Task Should_resolve_returns_null_for_empty_path()
    {
        // Act
        var result = await EditorConfigReader.ResolveAsync("");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Should_resolve_returns_null_when_no_properties()
    {
        // Arrange
        _server.SetResponse("/workspace/empty.cs", []);

        // Act
        var result = await EditorConfigReader.ResolveAsync("/workspace/empty.cs");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Should_resolve_returns_dictionary()
    {
        // Arrange
        _server.SetResponse("/workspace/file.cs", new()
        {
            ["indent_style"] = "space",
            ["end_of_line"] = "lf",
        });

        // Act
        var result = await EditorConfigReader.ResolveAsync("/workspace/file.cs");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("space", result["indent_style"]);
        Assert.Equal("lf", result["end_of_line"]);
    }

    [Fact]
    public async Task Should_resolve_filters_by_keys()
    {
        // Arrange — server receives the keys and returns only matching ones.
        _server.SetResponse("/workspace/file.cs", new()
        {
            ["indent_style"] = "space",
        });

        // Act
        var result = await EditorConfigReader.ResolveAsync("/workspace/file.cs", ["indent_style"]);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("space", result["indent_style"]);
    }
}
