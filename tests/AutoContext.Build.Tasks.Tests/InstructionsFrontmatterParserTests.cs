namespace AutoContext.Build.Tasks.Tests;

using AutoContext.Build.Tasks;

public sealed class InstructionsFrontmatterParserTests
{
    public sealed class Parse
    {
        [Fact]
        public void Should_reject_null_content()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => InstructionsFrontmatterParser.Parse(null!));
        }

        [Fact]
        public void Should_return_empty_fields_when_no_frontmatter_block()
        {
            // Act
            var frontmatter = InstructionsFrontmatterParser.Parse("# Heading\n\nBody only.\n");

            // Assert
            Assert.Multiple(
                () => Assert.Null(frontmatter.Name),
                () => Assert.Null(frontmatter.Description),
                () => Assert.Null(frontmatter.ApplyTo));
        }

        [Fact]
        public void Should_read_name_description_and_apply_to()
        {
            // Arrange
            var content =
                "---\n" +
                "name: \"code-review (v1.0.0)\"\n" +
                "description: \"Apply when reviewing code.\"\n" +
                "applyTo: \"**/*.cs\"\n" +
                "---\n" +
                "Body.\n";

            // Act
            var frontmatter = InstructionsFrontmatterParser.Parse(content);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("code-review (v1.0.0)", frontmatter.Name),
                () => Assert.Equal("Apply when reviewing code.", frontmatter.Description),
                () => Assert.Equal("**/*.cs", frontmatter.ApplyTo));
        }

        [Fact]
        public void Should_read_unquoted_field_values()
        {
            // Arrange
            var content =
                "---\n" +
                "name: code-review (v1.0.0)\n" +
                "description: Plain text.\n" +
                "---\n";

            // Act
            var frontmatter = InstructionsFrontmatterParser.Parse(content);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("code-review (v1.0.0)", frontmatter.Name),
                () => Assert.Equal("Plain text.", frontmatter.Description));
        }

        [Fact]
        public void Should_leave_apply_to_null_when_absent()
        {
            // Arrange
            var content =
                "---\n" +
                "name: \"code-review (v1.0.0)\"\n" +
                "description: \"Apply when reviewing code.\"\n" +
                "---\n";

            // Act
            var frontmatter = InstructionsFrontmatterParser.Parse(content);

            // Assert
            Assert.Null(frontmatter.ApplyTo);
        }
    }
}
