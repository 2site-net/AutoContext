namespace SharpPilot.Tests.Tools.Git;

using Microsoft.Extensions.Logging.Abstractions;

using SharpPilot.Tools.Checkers.Git;

[Collection("ToolsStatus")]
public sealed class GitCheckerTests
{
    [Fact]
    public void Should_pass_when_all_checks_pass()
    {
        // Arrange
        var message = "feat(auth): add token refresh";

        // Act
        var result = new GitChecker(NullLogger<GitChecker>.Instance).Check(message);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_report_format_violations()
    {
        // Arrange — missing type prefix
        var message = "added token refresh support";

        // Act
        var result = new GitChecker(NullLogger<GitChecker>.Instance).Check(message);

        // Assert
        Assert.StartsWith("❌", result);
    }

    [Fact]
    public void Should_report_content_violations()
    {
        // Arrange — valid format but bullet list in body
        var message = """
            feat(auth): add token refresh

            - Added refresh token endpoint
            - Updated token service
            """;

        // Act
        var result = new GitChecker(NullLogger<GitChecker>.Instance).Check(message.TrimStart());

        // Assert
        Assert.StartsWith("❌", result);
    }

    [Fact]
    public void Should_throw_on_null_or_whitespace_message()
    {
        Assert.Throws<ArgumentException>(() => new GitChecker(NullLogger<GitChecker>.Instance).Check(""));
        Assert.Throws<ArgumentException>(() => new GitChecker(NullLogger<GitChecker>.Instance).Check("   "));
    }
}
