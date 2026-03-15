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
public sealed class DotNetQaChecker : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_dotnet";

    /// <summary>
    /// Runs all enabled .NET code quality checks on the supplied C# source code.
    /// </summary>
    [McpServerTool(Name = "check_dotnet", ReadOnly = true, Idempotent = true)]
    [Description(
        "Runs all enabled .NET code quality checks on C# source code and returns a combined report. " +
        "Covers code style, member ordering, naming conventions, async patterns, nullable context, " +
        "project structure, and test style. " +
        "Prefer this over calling individual check tools unless you only need a specific check. " +
        "Does not include NuGet hygiene (use check_nuget_hygiene separately for project files). " +
        "When data is provided, expects comma-separated fileName,productionNamespace.")]
    public string Check(
        [Description("The C# source code to check.")]
        string content,
        [Description("Optional comma-separated metadata: fileName,productionNamespace. " +
            "fileName (e.g., 'MyClass.cs') validates the file name matches the declared type. " +
            "productionNamespace (e.g., 'MyApp.Services') validates test namespace mirroring.")]
        string? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var sections = new List<string>();

        IChecker[] checkers =
        [
            new CSharpCodingStyleChecker(),
            new CSharpMemberOrderingChecker(),
            new CSharpNamingConventionsChecker(),
            new CSharpAsyncPatternChecker(),
            new CSharpNullableContextChecker(),
            new CSharpProjectStructureChecker(),
            new CSharpTestStyleChecker(),
        ];

        foreach (var checker in checkers)
        {
            if (ToolsStatusConfig.IsEnabled(checker.ToolName))
            {
                sections.Add(checker.Check(content, data));
            }
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
}
