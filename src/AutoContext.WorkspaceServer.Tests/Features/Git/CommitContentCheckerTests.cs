namespace AutoContext.WorkspaceServer.Tests.Features.Git;

using AutoContext.WorkspaceServer.Features.Git;

public sealed class CommitContentCheckerTests
{
    [Fact]
    public async Task Should_pass_valid_prose_body()
    {
        // Arrange
        var message =
            "feat: add token refresh\n\n" +
            "The auth module now automatically refreshes expired tokens\n" +
            "before making API calls, preventing session timeouts.";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_pass_when_no_body_present()
    {
        // Arrange
        var message = "fix: typo in readme";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Theory]
    [InlineData("- added feature X")]
    [InlineData("* fixed bug Y")]
    [InlineData("• improved performance")]
    public async Task Should_reject_bullet_lists(string bulletLine)
    {
        // Arrange
        var message = $"feat: add feature\n\n{bulletLine}";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("bullet lists", result);
        });
    }

    [Theory]
    [InlineData("Updated src/Services/AuthService.cs to handle refresh.")]
    [InlineData("Changed files in Controllers/Api/UsersController.cs.")]
    public async Task Should_reject_file_paths(string bodyLine)
    {
        // Arrange
        var message = $"feat: update auth\n\n{bodyLine}";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("file paths", result);
        });
    }

    [Theory]
    [InlineData("Added 5 tests for the new feature.")]
    [InlineData("Updated 3 files to fix the bug.")]
    [InlineData("Removed 12 lines of dead code.")]
    public async Task Should_reject_counts(string bodyLine)
    {
        // Arrange
        var message = $"fix: cleanup\n\n{bodyLine}";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("counts", result);
        });
    }

    [Theory]
    [InlineData("Key features:\nSomething new.")]
    [InlineData("Changes:\nSomething changed.")]
    [InlineData("Summary of changes:\nSomething summarized.")]
    [InlineData("Highlights:\nSomething highlighted.")]
    public async Task Should_reject_section_headers(string bodyContent)
    {
        // Arrange
        var message = $"feat: new stuff\n\n{bodyContent}";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("section headers", result);
        });
    }

    [Fact]
    public async Task Should_reject_parameter_enumerations()
    {
        // Arrange
        var message =
            "feat: update config\n\n" +
            "Added `Timeout`, `RetryCount`, `MaxConnections` parameters.";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("enumerates parameters", result);
        });
    }

    [Theory]
    [InlineData("password= secret123")]
    [InlineData("token: abc123def456")]
    [InlineData("api_key=my-secret-key")]
    [InlineData("connectionstring: Server=prod;Password=x")]
    public async Task Should_reject_sensitive_information(string bodyLine)
    {
        // Arrange
        var message = $"fix: update config\n\n{bodyLine}";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("sensitive information", result);
        });
    }

    [Fact]
    public async Task Should_report_multiple_violations()
    {
        // Arrange
        var message =
            "feat: big change\n\n" +
            "- added feature\n" +
            "Updated 5 files for the change.";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("bullet lists", result);
            Assert.Contains("counts", result);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => new CommitContentChecker().CheckAsync(input));
    }

    [Fact]
    public async Task Should_throw_on_null_input()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => new CommitContentChecker().CheckAsync(null!));
    }

    [Fact]
    public async Task Should_not_flag_prose_mentioning_password_conceptually()
    {
        // Arrange
        var message =
            "feat: add password reset\n\n" +
            "Users can now reset their password via email verification.";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_handle_windows_line_endings()
    {
        // Arrange
        var message =
            "feat: add feature\r\n\r\n" +
            "The module now supports batch processing for improved throughput.";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public async Task Should_pass_body_with_only_blank_line_after_subject()
    {
        // Arrange
        var message = "feat: add login\n\n";

        // Act
        var result = await new CommitContentChecker().CheckAsync(message);

        // Assert
        Assert.StartsWith("✅", result);
    }
}
