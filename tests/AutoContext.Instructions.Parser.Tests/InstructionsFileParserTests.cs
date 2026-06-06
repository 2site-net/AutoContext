namespace AutoContext.Instructions.Parser.Tests;

using AutoContext.Engine.Tests.Support.IO;

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
                () => Assert.Null(frontmatter.Version),
                () => Assert.Equal(string.Empty, frontmatter.RawValue));
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
                () => Assert.Equal("1.2.3", frontmatter.Version),
                () => Assert.Equal(
                    "name: \"lang-csharp (v1.2.3)\"\ndescription: \"C# rules.\"\napplyTo: \"**/*.cs\"",
                    frontmatter.RawValue));
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

    public sealed class Parse
    {
        [Fact]
        public void Should_preserve_the_verbatim_content_as_raw_content()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\ndescription: \"d\"\n---\n## Heading\n\nBody.\n";

            // Act
            var result = InstructionsFileParser.Parse(content);

            // Assert
            Assert.Equal(content, result.RawContent);
        }

        [Fact]
        public void Should_end_raw_content_with_the_body_when_frontmatter_is_present()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\ndescription: \"d\"\n---\n## Heading\n\nBody.\n";

            // Act
            var result = InstructionsFileParser.Parse(content);

            // Assert
            Assert.Multiple(
                () => Assert.StartsWith("---", result.RawContent, StringComparison.Ordinal),
                () => Assert.EndsWith(result.Body.RawValue, result.RawContent, StringComparison.Ordinal));
        }

        [Fact]
        public void Should_equate_raw_content_and_body_when_no_frontmatter_is_present()
        {
            // Arrange
            var content = "## Heading\n\nBody only.\n";

            // Act
            var result = InstructionsFileParser.Parse(content);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(content, result.RawContent),
                () => Assert.Equal(result.Body.RawValue, result.RawContent),
                () => Assert.Equal(string.Empty, result.Frontmatter.RawValue));
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
            Assert.StartsWith("## Heading", result.Body.RawValue, StringComparison.Ordinal);
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

    public sealed class ParseReferences
    {
        [Fact]
        public void Should_capture_a_cross_file_rule_reference()
        {
            // Act
            var reference = Assert.Single(
                InstructionsFileParser.Parse("See [testing#INST0014] for details.\n").Body.References);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileReferenceKind.Rule, reference.Kind),
                () => Assert.Equal("testing", reference.Locator),
                () => Assert.Equal("INST0014", reference.Target));
        }

        [Fact]
        public void Should_treat_an_absent_locator_as_a_same_file_reference()
        {
            // Act
            var reference = Assert.Single(
                InstructionsFileParser.Parse("Group them with the API (see [#INST0017]).\n").Body.References);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileReferenceKind.Rule, reference.Kind),
                () => Assert.Null(reference.Locator),
                () => Assert.Equal("INST0017", reference.Target));
        }

        [Fact]
        public void Should_capture_a_cross_file_section_reference_without_its_quotes()
        {
            // Act
            var reference = Assert.Single(
                InstructionsFileParser.Parse("per [testing#'Test Support'] above.\n").Body.References);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileReferenceKind.Section, reference.Kind),
                () => Assert.Equal("testing", reference.Locator),
                () => Assert.Equal("Test Support", reference.Target));
        }

        [Fact]
        public void Should_capture_a_same_file_section_reference()
        {
            // Act
            var reference = Assert.Single(
                InstructionsFileParser.Parse("see [#'Assertions'].\n").Body.References);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileReferenceKind.Section, reference.Kind),
                () => Assert.Null(reference.Locator),
                () => Assert.Equal("Assertions", reference.Target));
        }

        [Fact]
        public void Should_resolve_an_escaped_apostrophe_in_a_section_reference()
        {
            // Act
            var reference = Assert.Single(
                InstructionsFileParser.Parse("see [#'Bob\\'s rules'] below.\n").Body.References);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileReferenceKind.Section, reference.Kind),
                () => Assert.Null(reference.Locator),
                () => Assert.Equal("Bob's rules", reference.Target));
        }

        [Fact]
        public void Should_resolve_an_escaped_apostrophe_in_a_cross_file_section_reference()
        {
            // Act
            var reference = Assert.Single(
                InstructionsFileParser.Parse("see [testing#'Don\\'t repeat yourself'] above.\n").Body.References);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileReferenceKind.Section, reference.Kind),
                () => Assert.Equal("testing", reference.Locator),
                () => Assert.Equal("Don't repeat yourself", reference.Target));
        }

        [Fact]
        public void Should_collapse_an_escaped_backslash_in_a_section_reference()
        {
            // Arrange
            // Source heading text is: path\to  (an escaped backslash).
            var body = "see [#'path\\\\to'] below.\n";

            // Act
            var reference = Assert.Single(InstructionsFileParser.Parse(body).Body.References);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileReferenceKind.Section, reference.Kind),
                () => Assert.Equal("path\\to", reference.Target));
        }

        [Fact]
        public void Should_capture_multiple_references_on_one_line_in_order()
        {
            // Arrange
            var body = "those names (see [dotnet-testing#INST0011]; root principle in [testing#INST0019]).\n";

            // Act
            var references = InstructionsFileParser.Parse(body).Body.References;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, references.Count),
                () => Assert.Equal("dotnet-testing", references[0].Locator),
                () => Assert.Equal("INST0011", references[0].Target),
                () => Assert.Equal("testing", references[1].Locator),
                () => Assert.Equal("INST0019", references[1].Target));
        }

        [Fact]
        public void Should_not_treat_a_definition_tag_as_a_reference()
        {
            // Act — the bullet tag carries no '#', so it is a definition, not a reference.
            var result = InstructionsFileParser.Parse("- [INST0006] **Do** cite [design-principles#INST0008] here.\n");

            // Assert
            Assert.Multiple(
                () => Assert.Equal("INST0006", Assert.Single(result.Body.Rules).Id),
                () => Assert.Equal("design-principles", Assert.Single(result.Body.References).Locator));
        }

        [Fact]
        public void Should_ignore_references_inside_inline_code_spans()
        {
            // Act
            var result = InstructionsFileParser.Parse("The form `[testing#INST0014]` is an example.\n");

            // Assert
            Assert.Empty(result.Body.References);
        }

        [Fact]
        public void Should_ignore_references_inside_fenced_code_blocks()
        {
            // Arrange
            var body = "Real [testing#INST0001] ref.\n\n```\n[testing#INST0002]\n```\n";

            // Act
            var reference = Assert.Single(InstructionsFileParser.Parse(body).Body.References);

            // Assert
            Assert.Equal("INST0001", reference.Target);
        }

        [Fact]
        public void Should_not_treat_a_markdown_link_label_as_a_reference()
        {
            // Act
            var result = InstructionsFileParser.Parse("[a#b](https://example.com) link.\n");

            // Assert
            Assert.Empty(result.Body.References);
        }

        [Fact]
        public void Should_leave_ordinary_bracketed_prose_alone()
        {
            // Act — uppercase locator is not a deliberate reference attempt.
            var result = InstructionsFileParser.Parse("Notes about [C# generics] go here.\n");

            // Assert
            Assert.Multiple(
                () => Assert.Empty(result.Body.References),
                () => Assert.Empty(result.Body.Diagnostics));
        }

        [Fact]
        public void Should_flag_a_truncated_rule_id_as_a_malformed_reference()
        {
            // Act
            var result = InstructionsFileParser.Parse("See [testing#INST014] please.\n");

            // Assert
            Assert.Multiple(
                () => Assert.Empty(result.Body.References),
                () => Assert.Equal(
                    InstructionsFileDiagnosticKind.MalformedReference,
                    Assert.Single(result.Body.Diagnostics).Kind));
        }

        [Fact]
        public void Should_flag_a_rule_range_as_a_malformed_reference()
        {
            // Act
            var result = InstructionsFileParser.Parse("See [testing#INST0014-INST0016] here.\n");

            // Assert
            Assert.Multiple(
                () => Assert.Empty(result.Body.References),
                () => Assert.Contains(
                    "ranges are not allowed",
                    Assert.Single(result.Body.Diagnostics).Message,
                    StringComparison.Ordinal));
        }

        [Fact]
        public void Should_flag_a_malformed_locator()
        {
            // Act — a deliberate id fragment with a non-key locator is a botched reference.
            var result = InstructionsFileParser.Parse("See [My File#INST0014] here.\n");

            // Assert
            Assert.Equal(
                InstructionsFileDiagnosticKind.MalformedReference,
                Assert.Single(result.Body.Diagnostics).Kind);
        }

        [Fact]
        public void Should_expose_body_relative_offsets_for_a_reference()
        {
            // Arrange
            var body = "see [#INST0017] now.\n";

            // Act
            var reference = Assert.Single(InstructionsFileParser.Parse(body).Body.References);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(body.IndexOf('[', StringComparison.Ordinal), reference.CharStart),
                () => Assert.Equal(body.IndexOf(']', StringComparison.Ordinal) + 1, reference.CharEnd),
                () => Assert.Equal(0, reference.Line));
        }
    }

    public sealed class ParseFile(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_reject_a_null_file_name()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => InstructionsFileParser.ParseFile(null!));
        }

        [Fact]
        public void Should_read_and_parse_an_existing_file()
        {
            // Arrange
            var content = "---\nname: \"lang-csharp (v1.2.3)\"\ndescription: \"C# rules.\"\n---\n## Heading\n\nBody.\n";
            var path = tempDirectory.CreatePath("lang-csharp.instructions.md");
            File.WriteAllText(path, content);

            // Act
            var result = InstructionsFileParser.ParseFile(path);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(content, result.RawContent),
                () => Assert.Equal("lang-csharp (v1.2.3)", result.Frontmatter.Name));
        }

        [Fact]
        public void Should_throw_when_the_file_does_not_exist()
        {
            // Arrange
            var path = tempDirectory.CreatePath("absent.instructions.md");

            // Act + Assert
            Assert.Throws<FileNotFoundException>(() => InstructionsFileParser.ParseFile(path));
        }
    }

    public sealed class TryParseFile(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_reject_a_null_file_name()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => InstructionsFileParser.TryParseFile(null!, out _));
        }

        [Fact]
        public void Should_read_and_parse_an_existing_file()
        {
            // Arrange
            var content = "---\nname: \"lang-csharp (v1.2.3)\"\ndescription: \"C# rules.\"\n---\n## Heading\n\nBody.\n";
            var path = tempDirectory.CreatePath("lang-csharp.instructions.md");
            File.WriteAllText(path, content);

            // Act
            var read = InstructionsFileParser.TryParseFile(path, out var result);

            // Assert
            Assert.Multiple(
                () => Assert.True(read),
                () => Assert.NotNull(result),
                () => Assert.Equal(content, result!.RawContent),
                () => Assert.Equal("lang-csharp (v1.2.3)", result!.Frontmatter.Name));
        }

        [Fact]
        public void Should_return_false_when_the_file_does_not_exist()
        {
            // Arrange
            var path = tempDirectory.CreatePath("absent.instructions.md");

            // Act
            var read = InstructionsFileParser.TryParseFile(path, out var result);

            // Assert
            Assert.Multiple(
                () => Assert.False(read),
                () => Assert.Null(result));
        }
    }
}
