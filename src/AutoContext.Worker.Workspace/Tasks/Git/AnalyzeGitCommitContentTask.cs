namespace AutoContext.Worker.Workspace.Tasks.Git;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using AutoContext.Mcp;

/// <summary>
/// <c>analyze_git_commit_content</c> — enforces commit body content rules from
/// <c>git-commit-format.instructions.md</c>: detects anti-patterns in the body.
/// </summary>
/// <remarks>
/// Request <c>data</c>:  <c>{ "content": "&lt;commit-message&gt;" }</c><br/>
/// Response <c>output</c>: <c>{ "passed": &lt;bool&gt;, "report": "&lt;markdown&gt;" }</c>
/// </remarks>
internal sealed partial class AnalyzeGitCommitContentTask : IMcpTask
{
    public string TaskName => "analyze_git_commit_content";

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

        var report = AnalyzeContent(content, out var passed);

        var output = new JsonObject
        {
            ["passed"] = passed,
            ["report"] = report,
        };

        return Task.FromResult(JsonSerializer.SerializeToElement(output));
    }

    private static string AnalyzeContent(string content, out bool passed)
    {
        var normalized = content.ReplaceLineEndings("\n");
        ReadOnlySpan<char> span = normalized;

        var firstNewline = span.IndexOf('\n');

        if (firstNewline < 0)
        {
            passed = true;
            return "✅ Commit content is valid (no body to check).";
        }

        var rest = span[(firstNewline + 1)..];
        var secondNewline = rest.IndexOf('\n');

        if (secondNewline < 0)
        {
            passed = true;
            return "✅ Commit content is valid (no body to check).";
        }

        var body = rest[(secondNewline + 1)..].Trim();

        if (body.IsEmpty)
        {
            passed = true;
            return "✅ Commit content is valid (no body to check).";
        }

        var violations = new List<string>();

        CheckBulletLists(body, violations);
        CheckFilePaths(body, violations);
        CheckCounts(body, violations);
        CheckSectionHeaders(body, violations);
        CheckParameterEnumerations(body, violations);
        CheckSensitiveInfo(body, violations);

        passed = violations.Count == 0;

        return passed
            ? "✅ Commit content is valid."
            : $"❌ Found {violations.Count} content violation(s):\n" +
              string.Join('\n', violations.Select((v, i) => $"  {i + 1}. {v}"));
    }

    // [git-commit-format INST0015]: no bullet lists
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

    // [git-commit-format INST0010]: no file paths
    private static void CheckFilePaths(ReadOnlySpan<char> body, List<string> violations)
    {
        if (FilePathRegex().IsMatch(body))
        {
            violations.Add(
                "Body contains file paths or folder structures. The diff already shows that.");
        }
    }

    // [git-commit-format INST0011]: no counts
    private static void CheckCounts(ReadOnlySpan<char> body, List<string> violations)
    {
        if (CountsRegex().IsMatch(body))
        {
            violations.Add(
                "Body contains counts (e.g., '5 tests', '3 files'). " +
                "Focus on behavioral changes instead.");
        }
    }

    // [git-commit-format INST0015]: no section headers
    private static void CheckSectionHeaders(ReadOnlySpan<char> body, List<string> violations)
    {
        if (SectionHeaderRegex().IsMatch(body))
        {
            violations.Add(
                "Body contains section headers like 'Key features:', 'Changes:', etc. " +
                "Write prose instead.");
        }
    }

    // [git-commit-format INST0013]: no parameter enumerations
    private static void CheckParameterEnumerations(ReadOnlySpan<char> body, List<string> violations)
    {
        if (ParameterEnumRegex().IsMatch(body))
        {
            violations.Add(
                "Body enumerates parameters, properties, or method names. " +
                "Summarize the capability instead.");
        }
    }

    // [git-commit-format INST0016]: no sensitive information
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
