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
    public async Task Should_report_bullet_lists_in_body()
    {
        // Arrange
        var content = "feat: x\n\n- first bullet\n- second bullet\n";

        // Act
        var output = await new AnalyzeGitCommitContentTask().ExecuteAsync(new { content });

        // Assert
        Assert.Multiple(
            () => Assert.False(output.GetProperty("passed").GetBoolean()),
            () => Assert.Contains("bullet", output.GetProperty("report").GetString(), StringComparison.Ordinal));
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
