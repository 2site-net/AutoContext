namespace SharpPilot.Mcp.DotNet.Tools.Checkers.DotNet.CSharp;

using System.ComponentModel;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

using SharpPilot.Mcp.DotNet.Configuration;
using SharpPilot.Mcp.DotNet.Tools.Checkers;
using SharpPilot.Mcp.DotNet.Tools.EditorConfig;

/// <summary>
/// Aggregate tool that runs all enabled C# source-code checkers and returns
/// a single combined report. Reads <c>.sharppilot.json</c> to determine which
/// checkers are active; when the file is absent, all checkers run.
/// </summary>
/// <remarks>
/// <see cref="NuGetHygieneChecker"/> is excluded because it operates on project
/// XML, not C# source code.
/// </remarks>
[McpServerToolType]
public sealed partial class CSharpChecker(ILogger<CSharpChecker> logger) : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_csharp_all";

    string IChecker.Check(string content, IReadOnlyDictionary<string, string>? data)
        => Check(content,
            editorConfigFilePath: data?.GetValueOrDefault("editorConfigFilePath"),
            productionFileName: data?.GetValueOrDefault("productionFileName"),
            productionNamespace: data?.GetValueOrDefault("productionNamespace"),
            testFileName: data?.GetValueOrDefault("testFileName"));

    /// <summary>
    /// Runs all enabled C# code quality checks on the supplied C# source code.
    /// </summary>
    [McpServerTool(Name = "check_csharp_all", ReadOnly = true, Idempotent = true)]
    [Description(
        "Runs all enabled C# code quality checks on C# source code and returns a combined report. " +
        "Covers code style, member ordering, naming conventions, async patterns, nullable context, " +
        "project structure, and test style. " +
        "Prefer this over calling individual check tools unless you only need a specific check. " +
        "Does not include NuGet hygiene (use check_nuget_hygiene separately for project files). " +
        "When editorConfigFilePath is provided (the path of the source file being checked), " +
        "resolves its effective .editorconfig properties and uses them to " +
        "drive checker behavior (e.g., brace and namespace style enforcement direction).")]
    public string Check(
        [Description("The C# source code to check.")]
        string content,
        [Description("Absolute path of the C# source file being checked. " +
            "Used to resolve its effective .editorconfig properties. " +
            "Pass the same path used when calling get_editorconfig.")]
        string? editorConfigFilePath = null,
        [Description("File name of the C# source file (e.g. 'MyClass.cs'). " +
            "Used to validate that the declared type name matches the file name.")]
        string? productionFileName = null,
        [Description("Namespace of the corresponding production type (e.g. 'MyApp.Services'). " +
            "Pass when checking a test file to validate namespace mirroring.")]
        string? productionNamespace = null,
        [Description("File name of the test file (e.g. 'UserServiceTests.cs'). " +
            "Pass when checking a test file to validate the name ends with 'Tests'.")]
        string? testFileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var providedKeys = string.Join(", ",
            new[] { editorConfigFilePath is not null ? nameof(editorConfigFilePath) : null,
                    productionFileName is not null ? nameof(productionFileName) : null,
                    productionNamespace is not null ? nameof(productionNamespace) : null,
                    testFileName is not null ? nameof(testFileName) : null }
                .OfType<string>());

        LogToolInvoked(logger, ToolName, content.Length,
            providedKeys.Length > 0 ? providedKeys : "(none)");

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

        var data = BuildData(checkers, editorConfigFilePath, productionFileName, productionNamespace, testFileName);

        var sections = new List<string>();

        foreach (var checker in checkers)
        {
            if (ToolsStatusConfig.IsEnabled(checker.ToolName))
            {
                sections.Add(checker.Check(content, data));
            }
        }

        if (sections.Count == 0)
        {
            return "⚠️ All C# checks are disabled.";
        }

        var failures = sections.Where(s => s.StartsWith('❌')).ToList();

        if (failures.Count == 0)
        {
            return "✅ All enabled C# checks passed.";
        }

        return string.Join("\n\n", failures);
    }

    private static Dictionary<string, string>? BuildData(
        IChecker[] checkers,
        string? editorConfigFilePath,
        string? productionFileName,
        string? productionNamespace,
        string? testFileName)
    {
        var data = new Dictionary<string, string>();

        if (productionFileName is not null)
        {
            data["productionFileName"] = productionFileName;
        }

        if (productionNamespace is not null)
        {
            data["productionNamespace"] = productionNamespace;
        }

        if (testFileName is not null)
        {
            data["testFileName"] = testFileName;
        }

        var allKeys = checkers.OfType<IEditorConfigFilter>()
            .SelectMany(f => f.EditorConfigKeys)
            .Distinct()
            .ToArray();
        var properties = EditorConfigReader.Resolve(editorConfigFilePath, allKeys);

        if (properties is not null)
        {
            foreach (var kv in properties)
            {
                data.TryAdd(kv.Key, kv.Value);
            }
        }

        return data.Count > 0 ? data : null;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Tool invoked: {ToolName} | content length: {ContentLength} | data keys: {DataKeys}")]
    private static partial void LogToolInvoked(ILogger logger, string toolName, int contentLength, string dataKeys);
}
