namespace QaMcp.Tests.Tools.Git;

using QaMcp.Tools.Git;

[Collection("ToolsStatus")]
public sealed class GitQaCheckerTests
{
    [Fact]
    public void Should_pass_when_all_checks_pass()
    {
        // Arrange
        var message = "feat(auth): add token refresh";

        // Act
        var result = GitQaChecker.Check(message);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_report_format_violations()
    {
        // Arrange — missing type prefix
        var message = "added token refresh support";

        // Act
        var result = GitQaChecker.Check(message);

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
        var result = GitQaChecker.Check(message.TrimStart());

        // Assert
        Assert.StartsWith("❌", result);
    }

    [Fact]
    public void Should_throw_on_null_or_whitespace_message()
    {
        Assert.Throws<ArgumentException>(() => GitQaChecker.Check(""));
        Assert.Throws<ArgumentException>(() => GitQaChecker.Check("   "));
    }
}
