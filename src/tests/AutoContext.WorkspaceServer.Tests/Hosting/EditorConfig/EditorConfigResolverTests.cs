namespace AutoContext.WorkspaceServer.Tests.Hosting.EditorConfig;

using AutoContext.WorkspaceServer.Hosting.EditorConfig;

public sealed class EditorConfigResolverTests : IDisposable
{
    private readonly EditorConfigResolver _resolver = new();
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"ec-test-{Guid.NewGuid():N}");

    public EditorConfigResolverTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Should_return_empty_when_no_editorconfig_exists()
    {
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            "root = true");

        var result = _resolver.Resolve(Path.Combine(_tempRoot, "file.cs"));

        Assert.Empty(result);
    }

    [Fact]
    public void Should_resolve_properties_from_matching_section()
    {
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.cs]
            indent_style = space
            indent_size = 4
            """);

        var result = _resolver.Resolve(Path.Combine(_tempRoot, "Program.cs"));

        Assert.Multiple(
            () => Assert.Equal("space", result["indent_style"]),
            () => Assert.Equal("4", result["indent_size"]));
    }

    [Fact]
    public void Should_not_include_properties_from_non_matching_section()
    {
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.py]
            indent_style = tab
            """);

        var result = _resolver.Resolve(Path.Combine(_tempRoot, "file.cs"));

        Assert.Empty(result);
    }

    [Fact]
    public void Should_cascade_child_over_parent()
    {
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

        var result = _resolver.Resolve(Path.Combine(child, "file.cs"));

        Assert.Multiple(
            () => Assert.Equal("2", result["indent_size"]),
            () => Assert.Equal("utf-8", result["charset"]));
    }

    [Fact]
    public void Should_stop_at_root_equals_true()
    {
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

        var result = _resolver.Resolve(Path.Combine(child, "file.cs"));

        Assert.Multiple(
            () => Assert.Equal("yes", result["parent_rule"]),
            () => Assert.False(result.ContainsKey("should_not_appear")));
    }

    [Fact]
    public void Should_resolve_wildcard_section()
    {
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*]
            end_of_line = lf
            """);

        var result = _resolver.Resolve(Path.Combine(_tempRoot, "anything.txt"));

        Assert.Equal("lf", result["end_of_line"]);
    }

    [Fact]
    public void Should_resolve_brace_expansion()
    {
        File.WriteAllText(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.{cs,vb}]
            indent_style = space
            """);

        var csResult = _resolver.Resolve(Path.Combine(_tempRoot, "file.cs"));
        var vbResult = _resolver.Resolve(Path.Combine(_tempRoot, "file.vb"));

        Assert.Multiple(
            () => Assert.Equal("space", csResult["indent_style"]),
            () => Assert.Equal("space", vbResult["indent_style"]));
    }
}
