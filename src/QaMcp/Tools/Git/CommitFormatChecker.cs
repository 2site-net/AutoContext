namespace QaMcp.Tools.Git;

using System.ComponentModel;
using System.Text.RegularExpressions;

using ModelContextProtocol.Server;

/// <summary>
/// Validates git commit message formatting against Conventional Commits and line-length rules.
/// </summary>
[McpServerToolType]
public sealed partial class CommitFormatChecker : IChecker
{
    private const int MaxSubjectLength = 50;
    private const int MaxBodyLineLength = 72;

    private static readonly string[] ValidTypes =
    [
        "feat", "fix", "docs", "style", "refactor",
        "perf", "test", "build", "ci", "chore", "revert",
    ];

    /// <inheritdoc />
    public string ToolName
        => "validate_commit_format";

    /// <summary>
    /// Validates a git commit message for Conventional Commits formatting rules.
    /// </summary>
    [McpServerTool(Name = "validate_commit_format", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validates a git commit message for Conventional Commits formatting: " +
        "type(scope): description, subject ≤ 50 chars, body wrap at 72 chars, " +
        "blank line between subject and body.")]
    public string Check(
        [Description("The full git commit message to validate.")]
        string content,
        string? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var violations = new List<string>();
        var normalized = content.ReplaceLineEndings("\n");
        ReadOnlySpan<char> span = normalized;

        var firstNewline = span.IndexOf('\n');
        var subject = firstNewline < 0 ? span : span[..firstNewline];

        ValidateSubjectFormat(subject, violations);
        ValidateSubjectLength(subject, violations);

        if (firstNewline >= 0)
        {
            var afterSubject = span[(firstNewline + 1)..];
            var secondNewline = afterSubject.IndexOf('\n');
            var secondLine = secondNewline < 0 ? afterSubject : afterSubject[..secondNewline];

            ValidateBlankLineAfterSubject(secondLine, violations);

            if (secondNewline >= 0)
            {
                ValidateBodyLineLength(afterSubject[(secondNewline + 1)..], violations);
            }
        }

        return violations.Count == 0
            ? "✅ Commit format is valid."
            : $"❌ Found {violations.Count} format violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    private static void ValidateSubjectFormat(ReadOnlySpan<char> subject, List<string> violations)
    {
        if (!SubjectRegex().IsMatch(subject))
        {
            var typesDisplay = string.Join(", ", ValidTypes);
            violations.Add(
                $"Subject must match Conventional Commits format: type(scope): description " +
                $"or type: description. Valid types: {typesDisplay}.");
        }
    }

    private static void ValidateSubjectLength(ReadOnlySpan<char> subject, List<string> violations)
    {
        if (subject.Length > MaxSubjectLength)
        {
            violations.Add(
                $"Subject is {subject.Length} characters; maximum is {MaxSubjectLength}.");
        }
    }

    private static void ValidateBlankLineAfterSubject(ReadOnlySpan<char> secondLine, List<string> violations)
    {
        if (!secondLine.IsEmpty)
        {
            violations.Add("There must be a blank line between the subject and body.");
        }
    }

    private static void ValidateBodyLineLength(ReadOnlySpan<char> body, List<string> violations)
    {
        var lineNumber = 3;

        foreach (var lineRange in body.Split('\n'))
        {
            var line = body[lineRange];

            if (line.Length > MaxBodyLineLength)
            {
                violations.Add(
                    $"Body line {lineNumber} is {line.Length} characters; " +
                    $"maximum is {MaxBodyLineLength}.");
            }

            lineNumber++;
        }
    }

    [GeneratedRegex(
        @"^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([a-zA-Z0-9_\-]+\))?!?: .+$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SubjectRegex();
}
