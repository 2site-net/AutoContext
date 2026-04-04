namespace SharpPilot.WorkspaceServer.Tests.Features.EditorConfig;

using SharpPilot.WorkspaceServer.Features.EditorConfig;

public sealed class EditorConfigToolTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"ec-tool-test-{Guid.NewGuid():N}");
    private readonly EditorConfigTool _tool;

    public EditorConfigToolTests()
    {
        Directory.CreateDirectory(_tempRoot);
        _tool = new EditorConfigTool(new EditorConfigResolver());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Should_return_warning_when_properties_are_empty()
    {
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            "root = true");

        var result = _tool.Read(Path.Combine(_tempRoot, "file.cs"));

        Assert.Multiple(
            () => Assert.StartsWith("⚠️", result),
            () => Assert.Contains("No .editorconfig properties", result));
    }

    [Fact]
    public void Should_resolve_properties()
    {
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.cs]
            indent_style = space
            indent_size = 4
            """);

        var result = _tool.Read(Path.Combine(_tempRoot, "Program.cs"));

        Assert.Multiple(
            () => Assert.Contains("indent_style = space", result),
            () => Assert.Contains("indent_size = 4", result));
    }

    [Fact]
    public void Should_throw_for_whitespace_path()
    {
        Assert.Throws<ArgumentException>(() => _tool.Read("   "));
    }
}
