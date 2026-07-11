namespace AutoContext.Engine.Core.Tests.Features.Discovery;

using AutoContext.Engine.Core.Features.Discovery;
using AutoContext.Engine.Core.Features.McpTools.Snapshot;

public sealed class CategoryIndexTests
{
    [Fact]
    public void Should_reject_a_null_registry()
        => Assert.Throws<ArgumentNullException>(() => new CategoryIndex(null!));

    [Fact]
    public void Should_route_a_tool_under_its_own_leaf_category()
    {
        var index = new CategoryIndex(BuildDotNetRegistry());

        var (categories, tools) = index.Match("please port this to C#");

        Assert.Multiple(
            () => Assert.Equal(["C#"], categories),
            () => Assert.Equal(["analyze_csharp_code"], tools));
    }

    [Fact]
    public void Should_route_a_tool_under_an_ancestor_category()
    {
        var index = new CategoryIndex(BuildDotNetRegistry());

        // ".NET" is the parent of "C#"; a prompt naming only the parent
        // surfaces the whole family.
        var (categories, tools) = index.Match("upgrade my .NET project");

        Assert.Multiple(
            () => Assert.Equal([".NET"], categories),
            () => Assert.Equal(["analyze_csharp_code"], tools));
    }

    [Fact]
    public void Should_deduplicate_a_tool_matched_through_several_categories()
    {
        var index = new CategoryIndex(BuildDotNetRegistry());

        var (categories, tools) = index.Match("some C# in a .NET app");

        Assert.Multiple(
            () => Assert.Equal([".NET", "C#"], categories),
            () => Assert.Equal(["analyze_csharp_code"], tools));
    }

    [Fact]
    public void Should_match_category_words_only_on_boundaries()
    {
        var registry = new McpToolsRegistry(
            [Category("Web")],
            [Tool("analyze_web", "Web")]);
        var index = new CategoryIndex(registry);

        Assert.Multiple(
            () => Assert.Empty(index.Match("a great website").Categories),
            () => Assert.Equal(["Web"], index.Match("a web app").Categories));
    }

    [Fact]
    public void Should_return_nothing_when_no_category_word_is_present()
    {
        var index = new CategoryIndex(BuildDotNetRegistry());

        var (categories, tools) = index.Match("just fix this bug for me");

        Assert.Multiple(
            () => Assert.Empty(categories),
            () => Assert.Empty(tools));
    }

    private static McpToolsRegistry BuildDotNetRegistry()
        => new(
            [Category(".NET"), Category("C#", parent: ".NET")],
            [Tool("analyze_csharp_code", "C#")]);

    private static McpToolsCategoryEntry Category(string name, string? parent = null)
        => new() { Name = name, Description = name, Parent = parent };

    private static McpToolsRegistryEntry Tool(string name, string category)
        => new()
        {
            Name = name,
            Category = category,
            WorkerId = "dotnet",
            ModelDescription = name,
            DisplayDescription = name,
            Parameters = [],
        };
}
