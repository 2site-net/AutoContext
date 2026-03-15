namespace QaMcp.Tools.Git;

using System.ComponentModel;
using System.Text.RegularExpressions;

using ModelContextProtocol.Server;

/// <summary>
/// Validates git commit message body content against anti-pattern rules.
/// </summary>
[McpServerToolType]
public sealed partial class CommitContentChecker : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "validate_commit_content";

    /// <summary>
    /// Validates a git commit message body for content anti-patterns.
    /// </summary>
    [McpServerTool(Name = "validate_commit_content", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validates a git commit message body for content anti-patterns: " +
        "no bullet lists, no file paths, no counts, no enumerated properties, " +
        "no 'Key features:' sections, and no sensitive information.")]
    public string Check(
        [Description("The full git commit message to validate.")]
        string content,
        string? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var violations = new List<string>();
        var normalized = content.ReplaceLineEndings("\n");
        ReadOnlySpan<char> span = normalized;

        // Body starts after the blank line (line index 2+)
        var firstNewline = span.IndexOf('\n');

        if (firstNewline < 0)
        {
            return "✅ Commit content is valid (no body to check).";
        }

        var rest = span[(firstNewline + 1)..];
        var secondNewline = rest.IndexOf('\n');

        if (secondNewline < 0)
        {
            return "✅ Commit content is valid (no body to check).";
        }

        var body = rest[(secondNewline + 1)..].Trim();

        if (body.IsEmpty)
        {
            return "✅ Commit content is valid (no body to check).";
        }

        CheckBulletLists(body, violations);
        CheckFilePaths(body, violations);
        CheckCounts(body, violations);
        CheckSectionHeaders(body, violations);
        CheckParameterEnumerations(body, violations);
        CheckSensitiveInfo(body, violations);

        return violations.Count == 0
            ? "✅ Commit content is valid."
            : $"❌ Found {violations.Count} content violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    private static void CheckBulletLists(ReadOnlySpan<char> body, List<string> violations)
    {
        foreach (var lineRange in body.Split('\n'))
        {
            if (BulletListRegex().IsMatch(body[lineRange]))
            {
                violations.Add(
                    "Body contains bullet lists (-, *, •). Write prose instead.");

                return;
            }
        }
    }

    private static void CheckFilePaths(ReadOnlySpan<char> body, List<string> violations)
    {
        if (FilePathRegex().IsMatch(body))
        {
            violations.Add(
                "Body contains file paths or folder structures. The diff already shows that.");
        }
    }

    private static void CheckCounts(ReadOnlySpan<char> body, List<string> violations)
    {
        if (CountsRegex().IsMatch(body))
        {
            violations.Add(
                "Body contains counts (e.g., '5 tests', '3 files'). " +
                "Focus on behavioral changes instead.");
        }
    }

    private static void CheckSectionHeaders(ReadOnlySpan<char> body, List<string> violations)
    {
        if (SectionHeaderRegex().IsMatch(body))
        {
            violations.Add(
                "Body contains section headers like 'Key features:', 'Changes:', etc. " +
                "Write prose instead.");
        }
    }

    private static void CheckParameterEnumerations(ReadOnlySpan<char> body, List<string> violations)
    {
        if (ParameterEnumRegex().IsMatch(body))
        {
            violations.Add(
                "Body enumerates parameters, properties, or method names. " +
                "Summarize the capability instead.");
        }
    }

    private static void CheckSensitiveInfo(ReadOnlySpan<char> body, List<string> violations)
    {
        if (SensitiveInfoRegex().IsMatch(body))
        {
            violations.Add(
                "Body may contain sensitive information (tokens, passwords, " +
                "connection strings). Remove it immediately.");
        }
    }

    [GeneratedRegex(
        @"^\s*[-*•]\s+",
        RegexOptions.CultureInvariant)]
    private static partial Regex BulletListRegex();

    [GeneratedRegex(
        @"(?:^|[\s(])(?:[A-Za-z]:\\|/)?(?:[A-Za-z0-9._\-]+[/\\]){2,}[A-Za-z0-9._\-]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex FilePathRegex();

    [GeneratedRegex(
        @"\b\d+\s+(?:tests?|files?|lines?|changes?|cases?|demos?|methods?|classes?|projects?)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CountsRegex();

    [GeneratedRegex(
        @"(?:^|\n)\s*(?:Key features|Changes|What changed|Summary of changes|Highlights)\s*:",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SectionHeaderRegex();

    [GeneratedRegex(
        @"(?:added|removed|renamed|updated|introduced)\s+(?:the\s+)?`[A-Za-z]+`(?:\s*,\s*`[A-Za-z]+`)+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ParameterEnumRegex();

    [GeneratedRegex(
        @"(?:password|secret|token|api[_\-]?key|connectionstring|credential)\s*[:=]\s*\S+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveInfoRegex();
}
