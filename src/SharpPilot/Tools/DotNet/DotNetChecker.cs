namespace SharpPilot.Tools.DotNet;

using System.ComponentModel;
using System.Text.Json.Nodes;

using ModelContextProtocol.Server;

using SharpPilot.Configuration;
using SharpPilot.Core;
using SharpPilot.Tools.EditorConfig;

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
public sealed class DotNetChecker : IChecker
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
        "When editorConfigFilePath is provided, resolves .editorconfig properties and uses them to " +
        "suppress checks that conflict with the project's editorconfig settings.")]
    public string Check(
        [Description("The C# source code to check.")]
        string content,
        [Description("Optional JSON metadata. " +
            "'editorConfigFilePath' (absolute path) resolves .editorconfig rules for this file. " +
            "'productionFileName' (e.g., 'MyClass.cs') validates the file name matches the declared type. " +
            "'testFileName' (e.g., 'UserServiceTests.cs') validates the test file name ends with 'Tests'. " +
            "'productionNamespace' (e.g., 'MyApp.Services') validates test namespace mirroring.")]
        JsonObject? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        data = MergeEditorConfig(data);

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

    private static JsonObject? MergeEditorConfig(JsonObject? data)
    {
        var filePath = data?["editorConfigFilePath"]?.GetValue<string>();
        var properties = EditorConfigReader.Resolve(filePath);

        if (properties is null)
        {
            return data;
        }

        data ??= [];

        foreach (var kv in properties)
        {
            // Only add editorconfig keys that aren't already in data
            data.TryAdd(kv.Key, JsonValue.Create(kv.Value));
        }

        return data;
    }
}
