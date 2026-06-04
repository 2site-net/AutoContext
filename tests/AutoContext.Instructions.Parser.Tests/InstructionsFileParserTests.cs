namespace AutoContext.Instructions.Parser.Tests;

public sealed class InstructionsFileParserTests
{
    public sealed class ParseFrontmatter
    {
        [Fact]
        public void Should_reject_null_content()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => InstructionsFileParser.ParseFrontmatter(null!));
        }

        [Fact]
        public void Should_return_all_null_when_no_frontmatter_block()
        {
            // Act
            var frontmatter = InstructionsFileParser.ParseFrontmatter("# Title\n\nBody only.\n");

            // Assert
            Assert.Multiple(
                () => Assert.Null(frontmatter.Name),
                () => Assert.Null(frontmatter.Description),
                () => Assert.Null(frontmatter.ApplyTo),
                () => Assert.Null(frontmatter.Version));
        }

        [Fact]
        public void Should_read_name_description_and_apply_to()
        {
            // Arrange
            var content = "---\nname: \"lang-csharp (v1.2.3)\"\ndescription: \"C# rules.\"\napplyTo: \"**/*.cs\"\n---\nBody.\n";

            // Act
            var frontmatter = InstructionsFileParser.ParseFrontmatter(content);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("lang-csharp (v1.2.3)", frontmatter.Name),
                () => Assert.Equal("C# rules.", frontmatter.Description),
                () => Assert.Equal("**/*.cs", frontmatter.ApplyTo?.RawValue),
                () => Assert.Equal("1.2.3", frontmatter.Version));
        }

        [Fact]
        public void Should_surface_the_parsed_apply_to_expression()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\ndescription: \"d\"\napplyTo: \"**/*.{cs,fs,vb}\"\n---\nBody.\n";

            // Act
            var applyTo = InstructionsFileParser.ParseFrontmatter(content).ApplyTo;

            // Assert
            Assert.Multiple(
                () => Assert.NotNull(applyTo),
                () => Assert.Equal(["**/*.cs", "**/*.fs", "**/*.vb"], applyTo!.ExpandedGlobs),
                () => Assert.True(applyTo!.RoundTrips));
        }

        [Fact]
        public void Should_omit_apply_to_when_absent()
        {
            // Arrange
            var content = "---\nname: \"design (v1.0.0)\"\ndescription: \"Cross-cutting.\"\n---\nBody.\n";

            // Act
            var frontmatter = InstructionsFileParser.ParseFrontmatter(content);

            // Assert
            Assert.Null(frontmatter.ApplyTo);
        }

        [Fact]
        public void Should_leave_version_null_when_name_has_no_version_suffix()
        {
            // Arrange
            var content = "---\nname: \"freeform name\"\ndescription: \"No version.\"\n---\nBody.\n";

            // Act
            var frontmatter = InstructionsFileParser.ParseFrontmatter(content);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("freeform name", frontmatter.Name),
                () => Assert.Null(frontmatter.Version));
        }
    }

    public sealed class ParseSections
    {
        [Fact]
        public void Should_strip_frontmatter_from_the_body()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\ndescription: \"d\"\n---\n## Heading\n\nBody.\n";

            // Act
            var result = InstructionsFileParser.Parse(content);

            // Assert
            Assert.StartsWith("## Heading", result.Body.RawBody, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_index_level_two_and_three_headings_with_parent_attribution()
        {
            // Arrange
            var body = "## Naming\n\nText.\n\n### Types\n\nMore.\n\n## Other\n";

            // Act
            var sections = InstructionsFileParser.Parse(body).Body.Sections;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(3, sections.Count),
                () => Assert.Equal(("Naming", 2, "naming", null), Shape(sections[0])),
                () => Assert.Equal(("Types", 3, "naming-types", "Naming"), Shape(sections[1])),
                () => Assert.Equal(("Other", 2, "other", null), Shape(sections[2])));
        }

        [Fact]
        public void Should_ignore_the_document_title_and_deeper_headings()
        {
            // Arrange
            var body = "# Title\n\n## Section\n\n#### TooDeep\n";

            // Act
            var sections = InstructionsFileParser.Parse(body).Body.Sections;

            // Assert
            Assert.Multiple(
                () => Assert.Single(sections),
                () => Assert.Equal("Section", sections[0].Heading));
        }

        [Fact]
        public void Should_ignore_headings_inside_fenced_code_blocks()
        {
            // Arrange
            var body = "## Real\n\n```\n## Fake\n### AlsoFake\n```\n\n## AlsoReal\n";

            // Act
            var sections = InstructionsFileParser.Parse(body).Body.Sections;

            // Assert
            Assert.Equal(["Real", "AlsoReal"], sections.Select(static section => section.Heading));
        }

        [Fact]
        public void Should_emit_offsets_that_span_to_the_next_equal_or_shallower_heading()
        {
            // Arrange
            var body = "## A\nbody\n## B\n";

            // Act
            var sections = InstructionsFileParser.Parse(body).Body.Sections;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, sections[0].CharStart),
                () => Assert.Equal(body.IndexOf("## B", StringComparison.Ordinal), sections[0].CharEnd),
                () => Assert.Equal(body.Length, sections[1].CharEnd));
        }

        [Fact]
        public void Should_not_deduplicate_colliding_anchors()
        {
            // Arrange
            var body = "## Same\n\ntext\n\n## Same\n";

            // Act
            var sections = InstructionsFileParser.Parse(body).Body.Sections;

            // Assert — the parser reports structure verbatim; collision policy is the consumer's.
            Assert.Equal(["same", "same"], sections.Select(static section => section.Anchor));
        }

        private static (string Heading, int Level, string Anchor, string? Parent) Shape(InstructionsFileSection section)
            => (section.Heading, section.Level, section.Anchor, section.Parent);
    }

    public sealed class ParseRules
    {
        [Fact]
        public void Should_reject_null_content()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => InstructionsFileParser.Parse(null!));
        }

        [Fact]
        public void Should_capture_a_tagged_rule_without_diagnostics()
        {
            // Act
            var result = InstructionsFileParser.Parse("- [INST0001] **Do** the thing.\n");

            // Assert
            Assert.Multiple(
                () => Assert.Equal("INST0001", Assert.Single(result.Body.Rules).Id),
                () => Assert.Empty(result.Body.Diagnostics));
        }

        [Fact]
        public void Should_flag_a_bullet_with_no_tag_as_missing_id()
        {
            // Act
            var result = InstructionsFileParser.Parse("- **Do** the untagged thing.\n");

            // Assert
            Assert.Multiple(
                () => Assert.Null(Assert.Single(result.Body.Rules).Id),
                () => Assert.Equal(
                    InstructionsFileDiagnosticKind.MissingId,
                    Assert.Single(result.Body.Diagnostics).Kind));
        }

        [Fact]
        public void Should_flag_a_malformed_tag()
        {
            // Act
            var result = InstructionsFileParser.Parse("- [INST01] **Do** the thing.\n");

            // Assert
            Assert.Equal(
                InstructionsFileDiagnosticKind.MalformedId,
                Assert.Single(result.Body.Diagnostics).Kind);
        }

        [Fact]
        public void Should_flag_a_duplicate_tag()
        {
            // Arrange
            var body = "- [INST0001] **Do** first.\n- [INST0001] **Don't** repeat.\n";

            // Act
            var result = InstructionsFileParser.Parse(body);

            // Assert
            Assert.Equal(
                InstructionsFileDiagnosticKind.DuplicateId,
                Assert.Single(result.Body.Diagnostics).Kind);
        }

        [Fact]
        public void Should_span_a_rule_across_continuation_lines()
        {
            // Arrange
            var body = "- [INST0001] **Do** first\n  continued detail\n\n- [INST0002] **Don't** second\n";

            // Act
            var rules = InstructionsFileParser.Parse(body).Body.Rules;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, rules.Count),
                () => Assert.Contains("continued detail", rules[0].Text, StringComparison.Ordinal),
                () => Assert.Equal(0, rules[0].StartLine),
                () => Assert.Equal(1, rules[0].EndLine),
                () => Assert.Equal("INST0002", rules[1].Id));
        }

        [Fact]
        public void Should_close_a_rule_at_an_unindented_non_bullet_line()
        {
            // Arrange
            var body = "- [INST0001] **Do** first\nUnindented prose ends the rule.\n";

            // Act
            var rule = Assert.Single(InstructionsFileParser.Parse(body).Body.Rules);

            // Assert
            Assert.DoesNotContain("Unindented", rule.Text, StringComparison.Ordinal);
        }
    }
}
