namespace AutoContext.Engine.Core.Tests.Features.McpTools;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;

public sealed class McpToolsRegistryTests
{
    private static McpToolsRegistryEntry Tool(string name)
    {
        return new McpToolsRegistryEntry
        {
            Name = name,
            Category = "Sample",
            WorkerId = "dotnet",
            ModelDescription = "A tool.",
            DisplayDescription = "A sample tool.",
            Parameters = [],
        };
    }

    private static McpToolsCategoryEntry Category(string name)
    {
        return new McpToolsCategoryEntry
        {
            Name = name,
            Description = "A category.",
        };
    }

    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_categories()
            => Assert.Throws<ArgumentNullException>(() => new McpToolsRegistry(null!, []));

        [Fact]
        public void Should_reject_null_tools()
            => Assert.Throws<ArgumentNullException>(() => new McpToolsRegistry([], null!));

        [Fact]
        public void Should_reject_a_null_category_element()
            => Assert.Throws<ArgumentException>(() => new McpToolsRegistry([null!], []));

        [Fact]
        public void Should_reject_a_null_tool_element()
            => Assert.Throws<ArgumentException>(() => new McpToolsRegistry([], [null!]));

        [Fact]
        public void Should_reject_a_duplicate_category_name()
            => Assert.Throws<ArgumentException>(
                () => new McpToolsRegistry([Category("C#"), Category("C#")], []));

        [Fact]
        public void Should_reject_a_duplicate_tool_name()
            => Assert.Throws<ArgumentException>(
                () => new McpToolsRegistry(
                    [], [Tool("analyze_sample_code"), Tool("analyze_sample_code")]));

        [Fact]
        public void Should_preserve_categories_in_document_order()
        {
            // Act
            var registry = new McpToolsRegistry([Category("B"), Category("A")], []);

            // Assert
            Assert.Collection(
                registry.Categories,
                first => Assert.Equal("B", first.Name),
                second => Assert.Equal("A", second.Name));
        }

        [Fact]
        public void Should_preserve_tools_in_document_order()
        {
            // Act
            var registry = new McpToolsRegistry([], [Tool("b_tool"), Tool("a_tool")]);

            // Assert
            Assert.Collection(
                registry.Tools,
                first => Assert.Equal("b_tool", first.Name),
                second => Assert.Equal("a_tool", second.Name));
        }
    }

    public sealed class FindByName
    {
        [Fact]
        public void Should_return_the_matching_tool()
        {
            // Arrange
            var tool = Tool("analyze_sample_code");
            var registry = new McpToolsRegistry([], [tool]);

            // Act
            var found = registry.FindByName("analyze_sample_code");

            // Assert
            Assert.Same(tool, found);
        }

        [Fact]
        public void Should_return_null_for_an_unknown_name()
        {
            // Arrange
            var registry = new McpToolsRegistry([], [Tool("analyze_sample_code")]);

            // Act
            var found = registry.FindByName("missing_tool");

            // Assert
            Assert.Null(found);
        }

        [Fact]
        public void Should_reject_a_null_name()
        {
            // Arrange
            var registry = McpToolsRegistry.Empty;

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => registry.FindByName(null!));
        }
    }

    public sealed class Empty
    {
        [Fact]
        public void Should_expose_no_categories()
            => Assert.Empty(McpToolsRegistry.Empty.Categories);

        [Fact]
        public void Should_expose_no_tools()
            => Assert.Empty(McpToolsRegistry.Empty.Tools);
    }
}
