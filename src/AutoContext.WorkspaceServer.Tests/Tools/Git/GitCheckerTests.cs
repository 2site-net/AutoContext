namespace AutoContext.WorkspaceServer.Tests.Tools.Git;

using AutoContext.Mcp.Shared.McpTools;
using AutoContext.WorkspaceServer.Tools.Git;

public sealed class GitCheckerTests
{
    private static GitChecker CreateChecker()
        => new(new McpToolsClient());

    [Fact]
    public async Task Should_pass_when_all_checks_pass()
    {
        // Arrange
        var message = "feat(auth): add token refresh";

        // Act
        var result = await CreateChecker().CheckAsync(message);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_report_format_violations()
    {
        // Arrange — missing type prefix
        var message = "added token refresh support";

        // Act
        var result = await CreateChecker().CheckAsync(message);

        // Assert
        Assert.StartsWith("❌", result);
    }

    [Fact]
    public async Task Should_report_content_violations()
    {
        // Arrange — valid format but bullet list in body
        var message = """
            feat(auth): add token refresh

            - Added refresh token endpoint
            - Updated token service
            """;

        // Act
        var result = await CreateChecker().CheckAsync(message.TrimStart());

        // Assert
        Assert.StartsWith("❌", result);
    }

    [Fact]
    public async Task Should_throw_on_null_or_whitespace_message()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateChecker().CheckAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => CreateChecker().CheckAsync("   "));
    }
}
