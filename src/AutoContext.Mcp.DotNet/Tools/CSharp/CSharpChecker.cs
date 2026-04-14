namespace AutoContext.Mcp.DotNet.Tools.CSharp;

using System.ComponentModel;

using ModelContextProtocol.Server;

using AutoContext.Mcp.Shared.Checkers;
using AutoContext.Mcp.Shared.WorkspaceServer;

/// <summary>
/// Aggregate tool that runs all enabled C# source-code checkers and returns
/// a single combined report. Reads <c>.autocontext.json</c> to determine which
/// checkers are active; when the file is absent, all checkers run.
/// </summary>
[McpServerToolType]
public sealed class CSharpChecker(WorkspaceServerClient workspaceServerClient)
    : CompositeChecker(workspaceServerClient)
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
        "Runs all enabled C# code quality checks and returns a combined report. " +
        "Covers code style, member ordering, naming conventions, async patterns, nullable context, " +
        "project structure, and test style. " +
        "Pass the source to check as content and its file path as originalPath. " +
        "When checking a test file, also pass the original type's namespace as originalNamespace " +
        "and the test file path as comparedPath.")]
    public async Task<string> CheckAsync(
        [Description("The C# source code to check.")]
        string content,
        [Description("Absolute path of the file whose source is in content.")]
        string? originalPath = null,
        [Description("Namespace of the type in the original file (e.g. 'MyApp.Services').")]
        string? originalNamespace = null,
        [Description("Absolute path of the compared file.")]
        string? comparedPath = null)
    {
        var data = new Dictionary<string, string>();

        if (originalPath is not null)
        {
            var normalized = originalPath.Replace('\\', Path.AltDirectorySeparatorChar);
            data["filePath"] = normalized;
            data["originalFileName"] = Path.GetFileName(normalized);
        }

        if (originalNamespace is not null)
        {
            data["originalNamespace"] = originalNamespace;
        }

        if (comparedPath is not null)
        {
            var normalized = comparedPath.Replace('\\', Path.AltDirectorySeparatorChar);
            data["comparedFileName"] = Path.GetFileName(normalized);
        }

        return await CheckAsync(content, data.Count > 0 ? data : null).ConfigureAwait(false);
    }
}
