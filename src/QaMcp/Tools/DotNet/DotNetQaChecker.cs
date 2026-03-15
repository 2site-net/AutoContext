namespace QaMcp.Tools.DotNet;

using System.ComponentModel;

using ModelContextProtocol.Server;

/// <summary>
/// Aggregate tool that runs all enabled .NET source-code checkers and returns
/// a single combined report. Reads <c>tools-status.json</c> to determine which
/// checkers are active; when the file is absent, all checkers run.
/// </summary>
/// <remarks>
/// <see cref="NuGetHygieneChecker"/> is excluded because it operates on project
/// XML, not C# source code.
/// </remarks>
[McpServerToolType]
public static class DotNetQaChecker
{
    /// <summary>
    /// Runs all enabled .NET code quality checks on the supplied C# source code.
    /// </summary>
    [McpServerTool(Name = "check_dotnet", ReadOnly = true, Idempotent = true)]
    [Description(
        "Runs all enabled .NET code quality checks on C# source code and returns a combined report. " +
        "Covers code style, member ordering, naming conventions, async patterns, nullable context, " +
        "project structure, and test style. " +
        "Prefer this over calling individual check tools unless you only need a specific check. " +
        "Does not include NuGet hygiene (use check_nuget_hygiene separately for project files).")]
    public static string Check(
        [Description("The C# source code to check.")]
        string sourceCode,
        [Description("The file name (e.g., 'MyClass.cs'). When provided, validates that the file name matches the declared type name.")]
        string? fileName = null,
        [Description("The root namespace of the production code (e.g., 'MyApp.Services'). When provided, validates that test file structure mirrors the production structure.")]
        string? productionNamespace = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCode);

        var sections = new List<string>();

        if (ToolsStatusConfig.IsEnabled("check_code_style"))
        {
            sections.Add(CodeStyleChecker.Check(sourceCode));
        }

        if (ToolsStatusConfig.IsEnabled("check_member_ordering"))
        {
            sections.Add(MemberOrderingChecker.Check(sourceCode));
        }

        if (ToolsStatusConfig.IsEnabled("check_naming_conventions"))
        {
            sections.Add(NamingConventionsChecker.Check(sourceCode));
        }

        if (ToolsStatusConfig.IsEnabled("check_async_patterns"))
        {
            sections.Add(AsyncPatternChecker.Check(sourceCode));
        }

        if (ToolsStatusConfig.IsEnabled("check_nullable_context"))
        {
            sections.Add(NullableContextChecker.Check(sourceCode));
        }

        if (ToolsStatusConfig.IsEnabled("check_project_structure"))
        {
            sections.Add(ProjectStructureChecker.Check(sourceCode, fileName));
        }

        if (ToolsStatusConfig.IsEnabled("check_tests_style") && IsLikelyTestFile(fileName))
        {
            sections.Add(TestStyleChecker.Check(sourceCode, fileName, productionNamespace));
        }

        if (sections.Count == 0)
        {
            return "⚠️ All .NET checks are disabled.";
        }

        var failures = sections.Where(s => s.StartsWith('❌')).ToList();

        if (failures.Count == 0)
        {
            return "✅ All enabled .NET checks passed.";
        }

        return string.Join("\n\n", failures);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="fileName"/> is absent
    /// (unknown context) or its base name ends with <c>Tests</c>.
    /// </summary>
    private static bool IsLikelyTestFile(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return true;
        }

        var name = Path.GetFileName(fileName);
        var dotIndex = name.IndexOf('.', StringComparison.Ordinal);
        var baseName = dotIndex < 0 ? name : name[..dotIndex];

        return baseName.EndsWith("Tests", StringComparison.Ordinal);
    }
}
