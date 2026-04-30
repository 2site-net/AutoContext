namespace AutoContext.Worker.Workspace.Tests.Tasks.Git;

using AutoContext.Worker.Workspace.Tasks.Git;
using AutoContext.Worker.Testing;

public sealed class AnalyzeGitCommitContentTaskTests
{
    [Fact]
    public async Task Should_pass_when_no_body_present()
    {
        // Act
        var output = await new AnalyzeGitCommitContentTask().ExecuteAsync(new { content = "feat: add login" });

        // Assert
        Assert.Multiple(
            () => Assert.True(output.GetProperty("passed").GetBoolean()),
            () => Assert.Contains("no body", output.GetProperty("report").GetString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_pass_for_clean_prose_body()
    {
        // Arrange
        var content = "feat: add login\n\nDescribe the new behavior in plain prose.\n";

        // Act
        var output = await new AnalyzeGitCommitContentTask().ExecuteAsync(new { content });

        // Assert
        Assert.True(output.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public async Task Should_pass_for_body_with_justified_bullet_list()
    {
        // Arrange
        var content = "feat: x\n\n- Caching now persists across sessions.\n- Errors surface upstream status codes verbatim.\n";

        // Act
        var output = await new AnalyzeGitCommitContentTask().ExecuteAsync(new { content });

        // Assert
        Assert.True(output.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public async Task Should_report_star_bullets_in_body()
    {
        // Arrange
        var content = "feat: x\n\n* one behavior change.\n* another behavior change.\n";

        // Act
        var output = await new AnalyzeGitCommitContentTask().ExecuteAsync(new { content });

        // Assert
        Assert.Multiple(
            () => Assert.False(output.GetProperty("passed").GetBoolean()),
            () => Assert.Contains("hyphen", output.GetProperty("report").GetString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Should_report_lists_nested_deeper_than_two_levels()
    {
        // Arrange
        var content = "feat: x\n\n- top level item.\n  - second level item.\n    - third level item.\n";

        // Act
        var output = await new AnalyzeGitCommitContentTask().ExecuteAsync(new { content });

        // Assert
        Assert.Multiple(
            () => Assert.False(output.GetProperty("passed").GetBoolean()),
            () => Assert.Contains("two levels", output.GetProperty("report").GetString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_pass_for_two_level_list_with_four_space_indent()
    {
        // Arrange — Markdown allows 4-space nesting; only count distinct
        // indent widths, not absolute spaces.
        var content = "feat: x\n\n- top level item.\n    - second level item.\n";

        // Act
        var output = await new AnalyzeGitCommitContentTask().ExecuteAsync(new { content });

        // Assert
        Assert.True(output.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public async Task Should_report_sensitive_information_in_body()
    {
        // Arrange
        var content = "feat: x\n\nUses password=secret123 to connect.\n";

        // Act
        var output = await new AnalyzeGitCommitContentTask().ExecuteAsync(new { content });

        // Assert
        Assert.Multiple(
            () => Assert.False(output.GetProperty("passed").GetBoolean()),
            () => Assert.Contains("sensitive", output.GetProperty("report").GetString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_throw_when_data_content_missing()
    {
        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AnalyzeGitCommitContentTask().ExecuteAsync(new { }));
    }
}
