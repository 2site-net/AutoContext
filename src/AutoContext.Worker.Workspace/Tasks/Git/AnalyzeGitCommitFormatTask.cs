namespace AutoContext.Worker.Workspace.Tasks.Git;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using AutoContext.Mcp.Abstractions;

/// <summary>
/// <c>analyze_git_commit_format</c> — enforces commit format rules from
/// <c>git-commit-format.instructions.md</c>: Conventional Commits structure
/// and line-length limits.
/// </summary>
/// <remarks>
/// Request <c>data</c>:  <c>{ "content": "&lt;commit-message&gt;" }</c><br/>
/// Response <c>output</c>: <c>{ "passed": &lt;bool&gt;, "report": "&lt;markdown&gt;" }</c>
/// </remarks>
internal sealed partial class AnalyzeGitCommitFormatTask : IMcpTask
{
    private const int MaxSubjectLength = 50;
    private const int MaxBodyLineLength = 72;

    private static readonly string[] ValidTypes =
    [
        "feat", "fix", "docs", "style", "refactor",
        "perf", "test", "build", "ci", "chore", "revert",
    ];

    public string TaskName => "analyze_git_commit_format";

    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken ct)
    {
        if (data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("content", out var contentElement)
            || contentElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("'data.content' is required and must be a string.");
        }

        var content = contentElement.GetString()!;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("'data.content' must not be empty or whitespace.");
        }

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

        var passed = violations.Count == 0;
        var report = passed
            ? "✅ Commit format is valid."
            : $"❌ Found {violations.Count} format violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));

        var output = new JsonObject
        {
            ["passed"] = passed,
            ["report"] = report,
        };

        return Task.FromResult(JsonSerializer.SerializeToElement(output));
    }

    // [git-commit-format INST0001]: Conventional Commits format
    private static void ValidateSubjectFormat(ReadOnlySpan<char> subject, List<string> violations)
    {
        if (!SubjectRegex().IsMatch(subject))
        {
            var typesDisplay = string.Join(", ", ValidTypes);
            violations.Add(
                "Subject must match Conventional Commits format: type(scope): description " +
                $"or type: description. Valid types: {typesDisplay}.");
        }
    }

    // [git-commit-format INST0004]: subject ≤ 50 characters
    private static void ValidateSubjectLength(ReadOnlySpan<char> subject, List<string> violations)
    {
        if (subject.Length > MaxSubjectLength)
        {
            violations.Add(
                $"Subject is {subject.Length} characters; maximum is {MaxSubjectLength}.");
        }
    }

    // [git-commit-format INST0006]: blank line between subject and body
    private static void ValidateBlankLineAfterSubject(ReadOnlySpan<char> secondLine, List<string> violations)
    {
        if (!secondLine.IsEmpty)
        {
            violations.Add("There must be a blank line between the subject and body.");
        }
    }

    // [git-commit-format INST0005]: body wrap at 72 characters
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
