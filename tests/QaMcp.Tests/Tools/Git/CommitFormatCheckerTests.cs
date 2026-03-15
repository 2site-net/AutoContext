namespace QaMcp.Tests.Tools.Git;

using QaMcp.Tools.Git;

public sealed class CommitFormatCheckerTests
{
    [Theory]
    [InlineData("feat: add login")]
    [InlineData("fix(dns): handle null zone")]
    [InlineData("chore(ci): update pipeline")]
    [InlineData("refactor(hub): consolidate")]
    [InlineData("docs: update readme")]
    [InlineData("feat!: send email on ship")]
    [InlineData("feat(api)!: drop Node 6 support")]
    public void Should_pass_valid_conventional_commit_subjects(string subject)
    {
        // Act
        var result = new CommitFormatChecker().Check(subject);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Theory]
    [InlineData("Add login feature")]
    [InlineData("FEAT: add login")]
    [InlineData("feat add login")]
    [InlineData("feat:add login")]
    [InlineData("feat(): add login")]
    [InlineData("unknown: something")]
    public void Should_reject_invalid_conventional_commit_subjects(string subject)
    {
        // Act
        var result = new CommitFormatChecker().Check(subject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Conventional Commits format", result);
        });
    }

    [Fact]
    public void Should_reject_subject_exceeding_50_characters()
    {
        // Arrange
        var subject = "feat: " + new string('a', 50);

        // Act
        var result = new CommitFormatChecker().Check(subject);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("maximum is 50", result);
        });
    }

    [Fact]
    public void Should_pass_subject_at_exactly_50_characters()
    {
        // Arrange
        var subject = "feat: " + new string('a', 44);
        Assert.Equal(50, subject.Length);

        // Act
        var result = new CommitFormatChecker().Check(subject);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_reject_missing_blank_line_between_subject_and_body()
    {
        // Arrange
        var message = "feat: add login\nThis is the body without blank line.";

        // Act
        var result = new CommitFormatChecker().Check(message);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("blank line", result);
        });
    }

    [Fact]
    public void Should_pass_with_blank_line_between_subject_and_body()
    {
        // Arrange
        var message = "feat: add login\n\nThis is the body.";

        // Act
        var result = new CommitFormatChecker().Check(message);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_reject_body_lines_exceeding_72_characters()
    {
        // Arrange
        var longLine = new string('x', 73);
        var message = $"feat: add login\n\n{longLine}";

        // Act
        var result = new CommitFormatChecker().Check(message);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("maximum is 72", result);
        });
    }

    [Fact]
    public void Should_pass_body_lines_at_exactly_72_characters()
    {
        // Arrange
        var line = new string('x', 72);
        var message = $"feat: add login\n\n{line}";

        // Act
        var result = new CommitFormatChecker().Check(message);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_report_multiple_violations()
    {
        // Arrange
        var longLine = new string('x', 80);
        var message = $"Add stuff\nno blank line\n{longLine}";

        // Act
        var result = new CommitFormatChecker().Check(message);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("Conventional Commits format", result);
            Assert.Contains("blank line", result);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CommitFormatChecker().Check(input));
    }

    [Fact]
    public void Should_throw_on_null_input()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CommitFormatChecker().Check(null!));
    }

    [Theory]
    [InlineData("feat")]
    [InlineData("fix")]
    [InlineData("docs")]
    [InlineData("style")]
    [InlineData("refactor")]
    [InlineData("perf")]
    [InlineData("test")]
    [InlineData("build")]
    [InlineData("ci")]
    [InlineData("chore")]
    [InlineData("revert")]
    public void Should_accept_all_valid_commit_types(string type)
    {
        // Arrange
        var message = $"{type}: do something";

        // Act
        var result = new CommitFormatChecker().Check(message);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_handle_windows_line_endings()
    {
        // Arrange
        var message = "feat: add login\r\n\r\nThis is the body.";

        // Act
        var result = new CommitFormatChecker().Check(message);

        // Assert
        Assert.StartsWith("✅", result);
    }
}
