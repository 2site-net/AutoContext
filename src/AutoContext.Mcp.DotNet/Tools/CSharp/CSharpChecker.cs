namespace AutoContext.Mcp.DotNet.Tools.CSharp;

using System.ComponentModel;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

using AutoContext.Mcp.Shared.Checkers;
using AutoContext.Mcp.Shared.McpTools;

/// <summary>
/// Aggregate tool that runs all enabled C# source-code checkers and returns
/// a single combined report. Reads <c>.autocontext.json</c> to determine which
/// checkers are active; when the file is absent, all checkers run.
/// </summary>
/// <remarks>
/// <see cref="NuGetHygieneChecker"/> is excluded because it operates on project
/// XML, not C# source code.
/// </remarks>
[McpServerToolType]
public sealed partial class CSharpChecker(McpToolsClient mcpToolsClient, ILogger<CSharpChecker> logger)
    : CompositeChecker(mcpToolsClient, logger)
{
    /// <inheritdoc />
    public override string ToolName
        => "check_csharp_all";

    /// <inheritdoc />
    protected override string ToolLabel
        => "C#";

    /// <inheritdoc />
    protected override IChecker[] CreateCheckers() =>
    [
        new CSharpCodingStyleChecker(),
        new CSharpMemberOrderingChecker(),
        new CSharpNamingConventionsChecker(),
        new CSharpAsyncPatternChecker(),
        new CSharpNullableContextChecker(),
        new CSharpProjectStructureChecker(),
        new CSharpTestStyleChecker(),
    ];

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
        "When filePath is provided, resolves its effective .editorconfig properties and uses them to " +
        "drive checker behavior (e.g., brace and namespace style enforcement direction). " +
        "The file name is also extracted from filePath to validate that the declared type name matches.")]
    public async Task<string> CheckAsync(
        [Description("The C# source code to check.")]
        string content,
        [Description("Absolute path of the C# source file being checked. " +
            "Used to resolve .editorconfig properties and to validate that the type name matches the file name.")]
        string? filePath = null,
        [Description("Namespace of the corresponding production type (e.g. 'MyApp.Services'). " +
            "Pass when checking a test file to validate namespace mirroring.")]
        string? productionNamespace = null,
        [Description("File name of the test file (e.g. 'UserServiceTests.cs'). " +
            "Pass when checking a test file to validate the name ends with 'Tests'.")]
        string? testFileName = null)
    {
        var data = new Dictionary<string, string>();

        if (filePath is not null)
        {
            data["filePath"] = filePath;
            data["productionFileName"] = Path.GetFileName(filePath);
        }

        if (productionNamespace is not null)
        {
            data["productionNamespace"] = productionNamespace;
        }

        if (testFileName is not null)
        {
            data["testFileName"] = testFileName;
        }

        return await CheckAsync(content, data.Count > 0 ? data : null).ConfigureAwait(false);
    }
}
