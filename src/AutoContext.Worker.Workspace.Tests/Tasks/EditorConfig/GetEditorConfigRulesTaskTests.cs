namespace AutoContext.Worker.Workspace.Tests.Tasks.EditorConfig;

using AutoContext.Worker.Workspace.Tasks.EditorConfig;
using AutoContext.Worker.Workspace.Tests._Utils;

public sealed class GetEditorConfigRulesTaskTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Should_filter_to_requested_keys_and_omit_missing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = new GetEditorConfigRulesTask();
        var workspace = _temp.CreateDirectory();
        await File.WriteAllTextAsync(Path.Combine(workspace, ".editorconfig"),
            "root = true\n\n[*.cs]\nindent_style = space\nindent_size = 4\n", ct);
        var filePath = Path.Combine(workspace, "Foo.cs");
        await File.WriteAllTextAsync(filePath, string.Empty, ct);

        // Act
        var output = await McpTaskRunner.RunAsync(sut, new
        {
            path = filePath,
            keys = new[] { "indent_style", "missing_key" },
        });

        // Assert
        Assert.Multiple(
            () => Assert.Equal("space", output.GetProperty("indent_style").GetString()),
            () => Assert.False(output.TryGetProperty("missing_key", out _)),
            () => Assert.False(output.TryGetProperty("indent_size", out _)));
    }

    [Fact]
    public async Task Should_return_all_keys_when_keys_filter_absent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = new GetEditorConfigRulesTask();
        var workspace = _temp.CreateDirectory();
        await File.WriteAllTextAsync(Path.Combine(workspace, ".editorconfig"),
            "root = true\n\n[*.cs]\nindent_style = tab\n", ct);
        var filePath = Path.Combine(workspace, "Foo.cs");
        await File.WriteAllTextAsync(filePath, string.Empty, ct);

        // Act
        var output = await McpTaskRunner.RunAsync(sut, new { path = filePath });

        // Assert
        Assert.Equal("tab", output.GetProperty("indent_style").GetString());
    }

    [Fact]
    public async Task Should_throw_when_data_path_missing()
    {
        // Arrange
        var sut = new GetEditorConfigRulesTask();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => McpTaskRunner.RunAsync(sut, new { }));
    }
}
namespace AutoContext.Worker.Workspace.Tests.Tasks.EditorConfig;

using AutoContext.Worker.Workspace.Tasks.EditorConfig;
using AutoContext.Worker.Workspace.Tests._Utils;

public sealed class GetEditorConfigRulesTaskTests : IDisposable
{
    private readonly TempDirectory _workspace = new("ac-worker-tests");

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Should_filter_to_requested_keys_and_omit_missing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _workspace.WriteFileAsync(".editorconfig", "root = true\n\n[*.cs]\nindent_style = space\nindent_size = 4\n", ct);
        var filePath = await _workspace.WriteFileAsync("Foo.cs", string.Empty, ct);

        // Act
        var output = await McpTaskRunner.RunAsync(new GetEditorConfigRulesTask(), new
        {
            path = filePath,
            keys = new[] { "indent_style", "missing_key" },
        });

        // Assert
        Assert.Multiple(
            () => Assert.Equal("space", output.GetProperty("indent_style").GetString()),
            () => Assert.False(output.TryGetProperty("missing_key", out _)),
            () => Assert.False(output.TryGetProperty("indent_size", out _)));
    }

    [Fact]
    public async Task Should_return_all_keys_when_keys_filter_absent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _workspace.WriteFileAsync(".editorconfig", "root = true\n\n[*.cs]\nindent_style = tab\n", ct);
        var filePath = await _workspace.WriteFileAsync("Foo.cs", string.Empty, ct);

        // Act
        var output = await McpTaskRunner.RunAsync(new GetEditorConfigRulesTask(), new { path = filePath });

        // Assert
        Assert.Equal("tab", output.GetProperty("indent_style").GetString());
    }

    [Fact]
    public async Task Should_throw_when_data_path_missing()
    {
        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpTaskRunner.RunAsync(new GetEditorConfigRulesTask(), new { }));
    }
}
namespace AutoContext.Worker.Workspace.Tests.Tasks.EditorConfig;

using System.Text.Json;

using AutoContext.Worker.Workspace.Tasks.EditorConfig;

public sealed class GetEditorConfigRulesTaskTests : IDisposable
{
    private readonly string _tempDir;

    public GetEditorConfigRulesTaskTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ac-worker-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Should_filter_to_requested_keys_and_omit_missing()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".editorconfig"),
            "root = true\n\n[*.cs]\nindent_style = space\nindent_size = 4\n",
            TestContext.Current.CancellationToken);
        var filePath = Path.Combine(_tempDir, "Foo.cs");
        await File.WriteAllTextAsync(filePath, string.Empty, TestContext.Current.CancellationToken);

        // Act
        var output = await ExecuteAsync(filePath, "indent_style", "missing_key");

        // Assert
        Assert.Multiple(
            () => Assert.Equal("space", output.GetProperty("indent_style").GetString()),
            () => Assert.False(output.TryGetProperty("missing_key", out _)),
            () => Assert.False(output.TryGetProperty("indent_size", out _)));
    }

    [Fact]
    public async Task Should_return_all_keys_when_keys_filter_absent()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".editorconfig"),
            "root = true\n\n[*.cs]\nindent_style = tab\n",
            TestContext.Current.CancellationToken);
        var filePath = Path.Combine(_tempDir, "Foo.cs");
        await File.WriteAllTextAsync(filePath, string.Empty, TestContext.Current.CancellationToken);
        var task = new GetEditorConfigRulesTask();
        var data = JsonSerializer.SerializeToElement(new { path = filePath });

        // Act
        var output = await task.ExecuteAsync(data, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("tab", output.GetProperty("indent_style").GetString());
    }

    [Fact]
    public async Task Should_throw_when_data_path_missing()
    {
        // Arrange
        var task = new GetEditorConfigRulesTask();
        var data = JsonSerializer.SerializeToElement(new { });

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            task.ExecuteAsync(data, TestContext.Current.CancellationToken));
    }

    private static async Task<JsonElement> ExecuteAsync(string path, params string[] keys)
    {
        var task = new GetEditorConfigRulesTask();
        var data = JsonSerializer.SerializeToElement(new { path, keys });

        return await task.ExecuteAsync(data, TestContext.Current.CancellationToken);
    }
}
