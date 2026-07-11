namespace AutoContext.Engine.Core.Tests.Features.Discovery;

using AutoContext.Engine.Core.Features.Discovery;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;

public sealed class ExtensionIndexTests
{
    [Fact]
    public void Should_reject_a_null_snapshot()
        => Assert.Throws<ArgumentNullException>(() => new ExtensionIndex(null!));

    [Fact]
    public void Should_route_a_file_by_an_extension_named_in_the_prompt()
    {
        var index = new ExtensionIndex(Snapshot(File("lang-csharp", ["cs"])));

        var (extensions, files) = index.Match("please edit Foo.cs for me");

        Assert.Multiple(
            () => Assert.Equal([".cs"], extensions),
            () => Assert.Equal(["lang-csharp.instructions.md"], FileNames(files)));
    }

    [Fact]
    public void Should_match_extensions_case_insensitively_and_report_the_canonical_form()
    {
        var index = new ExtensionIndex(Snapshot(File("lang-csharp", ["cs"])));

        var (extensions, files) = index.Match("open Foo.CS");

        Assert.Multiple(
            () => Assert.Equal([".cs"], extensions),
            () => Assert.Equal(["lang-csharp.instructions.md"], FileNames(files)));
    }

    [Fact]
    public void Should_ignore_extensions_that_map_to_no_file()
    {
        var index = new ExtensionIndex(Snapshot(File("lang-csharp", ["cs"])));

        var (extensions, files) = index.Match("read config.yaml");

        Assert.Multiple(
            () => Assert.Empty(extensions),
            () => Assert.Empty(files));
    }

    [Fact]
    public void Should_deduplicate_repeated_extensions_and_files()
    {
        var index = new ExtensionIndex(Snapshot(File("lang-csharp", ["cs"])));

        var (extensions, files) = index.Match("compare a.cs with b.cs");

        Assert.Multiple(
            () => Assert.Equal([".cs"], extensions),
            () => Assert.Equal(["lang-csharp.instructions.md"], FileNames(files)));
    }

    [Fact]
    public void Should_route_every_file_that_shares_an_extension_in_document_order()
    {
        var index = new ExtensionIndex(Snapshot(
            File("web-a", ["ts"]),
            File("web-b", ["ts"])));

        var (_, files) = index.Match("edit app.ts");

        Assert.Equal(
            ["web-a.instructions.md", "web-b.instructions.md"],
            FileNames(files));
    }

    private static IReadOnlyList<string> FileNames(IReadOnlyList<InstructionsFileManifestEntry> files)
        => [.. files.Select(file => file.FileName)];

    private static InstructionsManifestSnapshot Snapshot(params InstructionsFileManifestEntry[] files)
        => new([], files);

    private static InstructionsFileManifestEntry File(string key, IReadOnlyList<string> extensions)
        => new()
        {
            Key = key,
            FileName = key + ".instructions.md",
            Name = key + " (v1.0.0)",
            Version = "1.0.0",
            Description = key,
            HasChangelog = false,
            ContentHash = "sha256:0",
            AlwaysAttached = false,
            Extensions = extensions,
        };
}
