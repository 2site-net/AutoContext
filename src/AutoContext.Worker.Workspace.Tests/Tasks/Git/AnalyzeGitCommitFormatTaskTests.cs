namespace AutoContext.Worker.Workspace.Tests.Tasks.Git;

using AutoContext.Worker.Workspace.Tasks.Git;
using AutoContext.Worker.Testing;

public sealed class AnalyzeGitCommitFormatTaskTests
{
    [Theory]
    [InlineData("feat: add login")]
    [InlineData("fix(dns): handle null zone")]
    [InlineData("feat!: send email on ship")]
    public async Task Should_pass_for_valid_subject(string subject)
    {
        // Act
        var output = await McpTaskRunner.RunAsync(new AnalyzeGitCommitFormatTask(), new { content = subject });

        // Assert
        Assert.Multiple(
            () => Assert.True(output.GetProperty("passed").GetBoolean()),
            () => Assert.StartsWith("✅", output.GetProperty("report").GetString()));
    }

    [Theory]
    [InlineData("Add login feature")]
    [InlineData("feat add login")]
    [InlineData("unknown: something")]
    public async Task Should_fail_for_invalid_subject(string subject)
    {
        // Act
        var output = await McpTaskRunner.RunAsync(new AnalyzeGitCommitFormatTask(), new { content = subject });

        // Assert
        Assert.Multiple(
            () => Assert.False(output.GetProperty("passed").GetBoolean()),
            () => Assert.Contains("Conventional Commits format", output.GetProperty("report").GetString()));
    }

    [Fact]
    public async Task Should_report_subject_exceeding_50_characters()
    {
        // Arrange
        var subject = "feat: " + new string('a', 50);

        // Act
        var output = await McpTaskRunner.RunAsync(new AnalyzeGitCommitFormatTask(), new { content = subject });

        // Assert
        Assert.Multiple(
            () => Assert.False(output.GetProperty("passed").GetBoolean()),
            () => Assert.Contains("maximum is 50", output.GetProperty("report").GetString()));
    }

    [Fact]
    public async Task Should_report_missing_blank_line_after_subject()
    {
        // Arrange
        var content = "feat: add login\nbody starts here without blank line\n";

        // Act
        var output = await McpTaskRunner.RunAsync(new AnalyzeGitCommitFormatTask(), new { content });

        // Assert
        Assert.Multiple(
            () => Assert.False(output.GetProperty("passed").GetBoolean()),
            () => Assert.Contains("blank line", output.GetProperty("report").GetString()));
    }

    [Fact]
    public async Task Should_report_body_lines_exceeding_72_characters()
    {
        // Arrange
        var content = "feat: x\n\n" + new string('y', 73);

        // Act
        var output = await McpTaskRunner.RunAsync(new AnalyzeGitCommitFormatTask(), new { content });

        // Assert
        Assert.Multiple(
            () => Assert.False(output.GetProperty("passed").GetBoolean()),
            () => Assert.Contains("maximum is 72", output.GetProperty("report").GetString()));
    }

    [Fact]
    public async Task Should_throw_when_data_content_missing()
    {
        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpTaskRunner.RunAsync(new AnalyzeGitCommitFormatTask(), new { }));
    }
}
