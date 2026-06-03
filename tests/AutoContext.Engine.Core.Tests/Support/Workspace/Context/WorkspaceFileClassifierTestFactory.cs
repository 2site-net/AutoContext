namespace AutoContext.Engine.Core.Tests.Support.Workspace.Context;

using System.Text.RegularExpressions;

using AutoContext.Engine.Core.Workspace.Context;

public static class WorkspaceFileClassifierTestFactory
{
    internal static WorkspaceFileClassifier Create()
        => new(CreateFileRules(), CreateContentScans());

    private static IReadOnlyList<ContentScan> CreateContentScans()
        =>
        [
            new ContentScan(
                [new FileSelector("csproj", FileSelectorKind.Extension)],
                [
                    new ContentPatternRule(
                        "hasXunit",
                        new Regex("xunit", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
                ]),
        ];

    private static IReadOnlyList<FilePresenceRule> CreateFileRules()
        =>
        [
            new FilePresenceRule("hasCSharp", [new FileSelector("cs", FileSelectorKind.Extension)]),
            new FilePresenceRule("hasRust", [new FileSelector("Cargo.toml", FileSelectorKind.FileName)]),
            new FilePresenceRule("hasDocker", [new FileSelector("**/Dockerfile*", FileSelectorKind.GlobPattern)]),
        ];
}
