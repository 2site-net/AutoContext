namespace SharpPilot.Mcp.DotNet.Tests.Tools.EditorConfig;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using SharpPilot.Mcp.DotNet.Tools.EditorConfig;

[SuppressMessage("Reliability", "CA1849", Justification = "File.WriteAllText is used for trivial test setup")]
public sealed class EditorConfigReaderTests : IAsyncLifetime, IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"ec-test-{Guid.NewGuid():N}");
    private readonly string _pipeName = $"ec-reader-test-{Guid.NewGuid():N}";
    private readonly CancellationTokenSource _cts = new();

    private SharpPilot.WorkspaceServer.WorkspaceService? _service;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_tempRoot);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("pipe", _pipeName)])
            .Build();

        _service = new SharpPilot.WorkspaceServer.WorkspaceService(
            config,
            new SharpPilot.WorkspaceServer.EditorConfigResolver(),
            NullLogger<SharpPilot.WorkspaceServer.WorkspaceService>.Instance);

        await _service.StartAsync(_cts.Token);
        EditorConfigReader.Configure(_pipeName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_service is not null)
        {
            await _service.StopAsync(CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _service?.Dispose();
        _cts.Dispose();

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Should_return_warning_when_no_editorconfig_exists()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            "root = true");

        // Act
        var result = await EditorConfigReader.ReadAsync(Path.Combine(_tempRoot, "file.cs"));

        // Assert
        Assert.StartsWith("⚠️", result);
        Assert.Contains("No .editorconfig properties", result);
    }

    [Fact]
    public async Task Should_resolve_properties_from_matching_section()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.cs]
            indent_style = space
            indent_size = 4
            """);

        // Act
        var result = await EditorConfigReader.ReadAsync(Path.Combine(_tempRoot, "Program.cs"));

        // Assert
        Assert.Contains("indent_style = space", result);
        Assert.Contains("indent_size = 4", result);
    }

    [Fact]
    public async Task Should_not_include_properties_from_non_matching_section()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.py]
            indent_style = tab
            """);

        // Act
        var result = await EditorConfigReader.ReadAsync(Path.Combine(_tempRoot, "file.cs"));

        // Assert
        Assert.StartsWith("⚠️", result);
    }

    [Fact]
    public async Task Should_cascade_child_over_parent()
    {
        // Arrange
        var child = Path.Combine(_tempRoot, "src");
        Directory.CreateDirectory(child);

        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.cs]
            indent_size = 4
            charset = utf-8
            """);
        File.WriteAllText(
            Path.Combine(child, ".editorconfig"),
            """
            [*.cs]
            indent_size = 2
            """);

        // Act
        var result = await EditorConfigReader.ReadAsync(Path.Combine(child, "file.cs"));

        // Assert
        Assert.Contains("indent_size = 2", result);
        Assert.Contains("charset = utf-8", result);
        Assert.DoesNotContain("indent_size = 4", result);
    }

    [Fact]
    public async Task Should_stop_at_root_equals_true()
    {
        // Arrange
        var parent = Path.Combine(_tempRoot, "parent");
        var child = Path.Combine(parent, "child");
        Directory.CreateDirectory(child);

        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.cs]
            should_not_appear = yes
            """);
        File.WriteAllText(
            Path.Combine(parent, ".editorconfig"),
            """
            root = true

            [*.cs]
            parent_rule = yes
            """);

        // Act
        var result = await EditorConfigReader.ReadAsync(Path.Combine(child, "file.cs"));

        // Assert
        Assert.Contains("parent_rule = yes", result);
        Assert.DoesNotContain("should_not_appear", result);
    }

    [Fact]
    public async Task Should_resolve_wildcard_section()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*]
            end_of_line = lf
            """);

        // Act
        var result = await EditorConfigReader.ReadAsync(Path.Combine(_tempRoot, "anything.txt"));

        // Assert
        Assert.Contains("end_of_line = lf", result);
    }

    [Fact]
    public async Task Should_return_warning_for_whitespace_path()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => EditorConfigReader.ReadAsync("   "));
    }

    [Fact]
    public async Task Should_resolve_brace_expansion()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.{cs,vb}]
            indent_style = space
            """);

        // Act
        var csResult = await EditorConfigReader.ReadAsync(Path.Combine(_tempRoot, "file.cs"));
        var vbResult = await EditorConfigReader.ReadAsync(Path.Combine(_tempRoot, "file.vb"));

        // Assert
        Assert.Contains("indent_style = space", csResult);
        Assert.Contains("indent_style = space", vbResult);
    }
}
