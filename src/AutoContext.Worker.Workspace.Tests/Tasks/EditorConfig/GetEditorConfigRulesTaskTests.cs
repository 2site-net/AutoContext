namespace AutoContext.Worker.Workspace.Tests.Tasks.EditorConfig;

using AutoContext.Worker.Workspace.Tasks.EditorConfig;
using AutoContext.Worker.Workspace.Tests.Testing.Utils;

public sealed class GetEditorConfigRulesTaskTests : IDisposable
{
    private static readonly string[] FilteredKeys = ["indent_style", "missing_key"];

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
            keys = FilteredKeys,
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
