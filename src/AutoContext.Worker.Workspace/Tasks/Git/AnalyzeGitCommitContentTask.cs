namespace AutoContext.Worker.Workspace.Tasks.Git;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using AutoContext.Mcp;

/// <summary>
/// <c>analyze_git_commit_content</c> — inspects the <em>prose</em> of a commit
/// body against <c>git-commit.instructions.md</c>: flags low-signal patterns
/// such as file-path dumps, change counts, section headers, parameter
/// enumerations, leaked secrets, and inconsistent or over-nested lists.
/// Companion to <see cref="AnalyzeGitCommitFormatTask"/>, which judges the
/// structural shape of the message rather than what it says.
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

        CheckListStyle(body, violations);
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

    // [git-commit INST0015]: bullet style consistency ('-' only) and nesting depth
    private static void CheckListStyle(ReadOnlySpan<char> body, List<string> violations)
    {
        var sawDisallowed = false;
        var indentLevels = new HashSet<int>();

        foreach (var lineRange in body.Split('\n'))
        {
            var line = body[lineRange];

            if (!TryParseBullet(line, out var indent, out var marker))
            {
                continue;
            }

            if (marker != '-')
            {
                sawDisallowed = true;
            }

            indentLevels.Add(indent);
        }

        if (sawDisallowed)
        {
            violations.Add(
                "Body uses non-hyphen bullets ('*', '+', or '•'). " +
                "Use '-' (hyphen) consistently.");
        }

        // Count distinct indent widths used by bullet lines. Any indent-style
        // (2-space, 4-space, tab) maps cleanly: more than two distinct widths
        // means more than two nesting levels, regardless of width per level.
        if (indentLevels.Count > 2)
        {
            violations.Add(
                "Body nests lists deeper than two levels. Flatten or rewrite as prose.");
        }
    }

    // Parses "<whitespace><marker><whitespace>..." where marker is '-', '*', or '•'.
    // Returns false for any other shape. Span-only; allocation-free.
    private static bool TryParseBullet(ReadOnlySpan<char> line, out int indent, out char marker)
    {
        var i = 0;

        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            i++;
        }

        if (i >= line.Length)
        {
            indent = 0;
            marker = '\0';
            return false;
        }

        var c = line[i];

        // Recognize every plausible bullet marker so the caller can flag
        // anything other than '-'. CommonMark / GFM allows '-', '*', '+';
        // '•' is included because users sometimes paste rendered bullets.
        if (c is not '-' and not '*' and not '+' and not '\u2022')
        {
            indent = 0;
            marker = '\0';
            return false;
        }

        // The marker must be followed by at least one whitespace character;
        // otherwise '-' is just a hyphenated word like "well-known".
        if (i + 1 >= line.Length || (line[i + 1] != ' ' && line[i + 1] != '\t'))
        {
            indent = 0;
            marker = '\0';
            return false;
        }

        indent = i;
        marker = c;
        return true;
    }

    // [git-commit INST0010]: no file paths
    private static void CheckFilePaths(ReadOnlySpan<char> body, List<string> violations)
    {
        if (FilePathRegex().IsMatch(body))
        {
            violations.Add(
                "Body contains file paths or folder structures. The diff already shows that.");
        }
    }

    // [git-commit INST0011]: no counts
    private static void CheckCounts(ReadOnlySpan<char> body, List<string> violations)
    {
        if (CountsRegex().IsMatch(body))
        {
            violations.Add(
                "Body contains counts (e.g., '5 tests', '3 files'). " +
                "Focus on behavioral changes instead.");
        }
    }

    // [git-commit INST0015]: no section headers
    private static void CheckSectionHeaders(ReadOnlySpan<char> body, List<string> violations)
    {
        if (SectionHeaderRegex().IsMatch(body))
        {
            violations.Add(
                "Body contains section headers like 'Key features:', 'Changes:', etc. " +
                "Write prose instead.");
        }
    }

    // [git-commit INST0013]: no parameter enumerations
    private static void CheckParameterEnumerations(ReadOnlySpan<char> body, List<string> violations)
    {
        if (ParameterEnumRegex().IsMatch(body))
        {
            violations.Add(
                "Body enumerates parameters, properties, or method names. " +
                "Summarize the capability instead.");
        }
    }

    // [git-commit INST0016]: no sensitive information
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
