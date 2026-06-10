namespace AutoContext.Instructions.Parser.Tests.Model;

using AutoContext.Instructions.Parser.Model;
using AutoContext.Instructions.Parser.Syntax;
using AutoContext.Instructions.Parser.Tests.Support;

public sealed class InstructionsFileTests
{
    public sealed class FromSpans
    {
        [Fact]
        public void Should_reject_null_spans()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => InstructionsFile.FromSpans(null!));
        }

        [Fact]
        public void Should_preserve_verbatim_content()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\n---\n# Title\n\nBody.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);

            // Assert
            Assert.Equal(content, parsed.RawContent);
        }

        [Fact]
        public void Should_strip_frontmatter_from_the_body_raw_value()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\n---\n# Title\n\nBody.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);

            // Assert
            Assert.Equal("# Title\n\nBody.\n", parsed.Body.RawValue);
        }
    }

    public sealed class Frontmatter
    {
        [Fact]
        public void Should_read_name_description_apply_to_and_version()
        {
            // Arrange
            var content = "---\nname: \"lang-csharp (v1.2.3)\"\ndescription: \"C# rules.\"\napplyTo: \"**/*.cs\"\n---\nBody.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);
            var frontmatter = parsed.Frontmatter;

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
        public void Should_leave_all_fields_null_when_no_frontmatter_block()
        {
            // Arrange
            var content = "# Title\n\nBody only.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);
            var frontmatter = parsed.Frontmatter;

            // Assert
            Assert.Multiple(
                () => Assert.Null(frontmatter.Name),
                () => Assert.Null(frontmatter.Description),
                () => Assert.Null(frontmatter.ApplyTo),
                () => Assert.Null(frontmatter.Version),
                () => Assert.Equal(string.Empty, frontmatter.RawValue));
        }

        [Fact]
        public void Should_surface_the_parsed_apply_to_expression()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\ndescription: \"d\"\napplyTo: \"**/*.{cs,fs,vb}\"\n---\nBody.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var applyTo = InstructionsFile.FromSpans(spans).Frontmatter.ApplyTo;

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
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var frontmatter = InstructionsFile.FromSpans(spans).Frontmatter;

            // Assert
            Assert.Null(frontmatter.ApplyTo);
        }

        [Fact]
        public void Should_leave_version_null_when_name_has_no_version_suffix()
        {
            // Arrange
            var content = "---\nname: \"freeform name\"\ndescription: \"No version.\"\n---\nBody.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var frontmatter = InstructionsFile.FromSpans(spans).Frontmatter;

            // Assert
            Assert.Multiple(
                () => Assert.Equal("freeform name", frontmatter.Name),
                () => Assert.Null(frontmatter.Version));
        }
    }

    public sealed class Sections
    {
        [Fact]
        public void Should_index_sections_with_anchors_parents_and_ranges()
        {
            // Arrange
            var content = "# Title\n\n## First Section\n\nText.\n\n### Sub A\n\nMore.\n\n## Second\n\nEnd.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);
            var body = parsed.Body;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(3, body.Sections.Count),
                () => Assert.Equal("First Section", body.Sections[0].Heading),
                () => Assert.Equal(2, body.Sections[0].Level),
                () => Assert.Equal("first-section", body.Sections[0].Anchor),
                () => Assert.Null(body.Sections[0].Parent),
                () => Assert.Equal("Sub A", body.Sections[1].Heading),
                () => Assert.Equal(3, body.Sections[1].Level),
                () => Assert.Equal("first-section-sub-a", body.Sections[1].Anchor),
                () => Assert.Equal("First Section", body.Sections[1].Parent),
                () => Assert.Equal("Second", body.Sections[2].Heading),
                () => Assert.Equal(2, body.Sections[2].Level),
                () => Assert.Equal("second", body.Sections[2].Anchor),
                () => Assert.Null(body.Sections[2].Parent),
                () => Assert.Equal(body.RawValue.IndexOf("## First Section", StringComparison.Ordinal), body.Sections[0].TextSpan.StartIndex),
                () => Assert.Equal(body.RawValue.IndexOf("## Second", StringComparison.Ordinal), body.Sections[0].TextSpan.EndIndex),
                () => Assert.Equal(body.RawValue.IndexOf("## Second", StringComparison.Ordinal), body.Sections[1].TextSpan.EndIndex),
                () => Assert.Equal(body.RawValue.Length, body.Sections[2].TextSpan.EndIndex));
        }

        [Fact]
        public void Should_ignore_the_document_title()
        {
            // Arrange
            var content = "# Title\n\n## Only\n\nText.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);
            var sections = parsed.Body.Sections;

            // Assert
            Assert.Multiple(
                () => Assert.Single(sections),
                () => Assert.Equal("Only", sections[0].Heading));
        }

        [Fact]
        public void Should_ignore_headings_deeper_than_level_three()
        {
            // Arrange
            var content = "# Title\n\n## Section\n\n#### TooDeep\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var sections = InstructionsFile.FromSpans(spans).Body.Sections;

            // Assert
            Assert.Multiple(
                () => Assert.Single(sections),
                () => Assert.Equal("Section", sections[0].Heading));
        }

        [Fact]
        public void Should_ignore_headings_inside_fenced_code_blocks()
        {
            // Arrange
            var content = "## Real\n\n```\n## Fake\n### AlsoFake\n```\n\n## AlsoReal\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var sections = InstructionsFile.FromSpans(spans).Body.Sections;

            // Assert
            Assert.Equal(["Real", "AlsoReal"], sections.Select(static section => section.Heading));
        }

        [Fact]
        public void Should_not_deduplicate_colliding_anchors()
        {
            // Arrange
            var content = "## Same\n\ntext\n\n## Same\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act — the parser reports structure verbatim; collision policy is the consumer's.
            var sections = InstructionsFile.FromSpans(spans).Body.Sections;

            // Assert
            Assert.Equal(["same", "same"], sections.Select(static section => section.Anchor));
        }
    }

    public sealed class Rules
    {
        [Fact]
        public void Should_extract_tagged_and_plain_rule_ids()
        {
            // Arrange
            var content = "## Rules\n\n- [INST0001] **Do** alpha.\n- **Don't** beta.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);
            var rules = parsed.Body.Rules;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, rules.Count),
                () => Assert.Equal("INST0001", rules[0].Id),
                () => Assert.Equal("- [INST0001] **Do** alpha.", rules[0].Text),
                () => Assert.Null(rules[1].Id),
                () => Assert.Equal("- **Don't** beta.", rules[1].Text));
        }

        [Fact]
        public void Should_treat_a_malformed_tag_bullet_as_an_untagged_rule()
        {
            // Arrange
            var content = "## Rules\n\n- [oops] **Do** gamma.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);
            var body = parsed.Body;

            // Assert
            Assert.Multiple(
                () => Assert.Single(body.Rules),
                () => Assert.Null(body.Rules[0].Id),
                () => Assert.Equal("- [oops] **Do** gamma.", body.Rules[0].Text),
                () => Assert.Single(parsed.Diagnostics),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MalformedTag, parsed.Diagnostics[0].Kind));
        }

        [Fact]
        public void Should_address_rule_lines_relative_to_the_body()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\n---\n## Rules\n\n- [INST0001] **Do** a.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);
            var body = parsed.Body;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, body.Rules[0].LineSpan.StartLine),
                () => Assert.Equal(3, body.Rules[0].LineSpan.EndLine),
                () => Assert.Equal(0, body.Sections[0].TextSpan.StartIndex));
        }

        [Fact]
        public void Should_span_a_rule_across_continuation_lines()
        {
            // Arrange
            var content = "## Rules\n\n- [INST0001] **Do** first\n  continued detail\n\n- [INST0002] **Don't** second\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var rules = InstructionsFile.FromSpans(spans).Body.Rules;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, rules.Count),
                () => Assert.Contains("continued detail", rules[0].Text, StringComparison.Ordinal),
                () => Assert.Equal("INST0002", rules[1].Id));
        }

        [Fact]
        public void Should_close_a_rule_at_an_unindented_non_bullet_line()
        {
            // Arrange
            var content = "## Rules\n\n- [INST0001] **Do** first\nUnindented prose ends the rule.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var rule = Assert.Single(InstructionsFile.FromSpans(spans).Body.Rules);

            // Assert
            Assert.DoesNotContain("Unindented", rule.Text, StringComparison.Ordinal);
        }
    }

    public sealed class References
    {
        [Fact]
        public void Should_split_rule_and_section_references()
        {
            // Arrange
            var content = "# Title\n\nSee [testing#INST0014] and [#'Assertions'] and [other#INST0002].\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);
            var references = parsed.References;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(3, references.Count),
                () => Assert.Equal(InstructionsFileReferenceKind.Rule, references[0].Address.Kind),
                () => Assert.Equal("testing", references[0].Address.Locator),
                () => Assert.Equal("INST0014", references[0].Address.Target),
                () => Assert.Equal(InstructionsFileReferenceKind.Section, references[1].Address.Kind),
                () => Assert.Null(references[1].Address.Locator),
                () => Assert.Equal("Assertions", references[1].Address.Target),
                () => Assert.Equal(InstructionsFileReferenceKind.Rule, references[2].Address.Kind),
                () => Assert.Equal("other", references[2].Address.Locator),
                () => Assert.Equal("INST0002", references[2].Address.Target));
        }

        [Fact]
        public void Should_skip_a_malformed_reference_but_keep_its_diagnostic()
        {
            // Arrange
            var content = "# Title\n\nBad [bad locator#INST0001] reference.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);

            // Assert
            Assert.Multiple(
                () => Assert.Empty(parsed.References),
                () => Assert.Single(parsed.Diagnostics),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MalformedReference, parsed.Diagnostics[0].Kind));
        }

        [Fact]
        public void Should_resolve_escaped_apostrophes_in_same_and_cross_file_section_references()
        {
            // Arrange
            var content = "# Title\n\nsee [#'Bob\\'s rules'] and [testing#'Don\\'t repeat yourself'].\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var references = InstructionsFile.FromSpans(spans).References;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, references.Count),
                () => Assert.Equal(InstructionsFileReferenceKind.Section, references[0].Address.Kind),
                () => Assert.Null(references[0].Address.Locator),
                () => Assert.Equal("Bob's rules", references[0].Address.Target),
                () => Assert.Equal(InstructionsFileReferenceKind.Section, references[1].Address.Kind),
                () => Assert.Equal("testing", references[1].Address.Locator),
                () => Assert.Equal("Don't repeat yourself", references[1].Address.Target));
        }

        [Fact]
        public void Should_collapse_an_escaped_backslash_in_a_section_reference()
        {
            // Arrange — source heading text is: path\to (an escaped backslash).
            var content = "# Title\n\nsee [#'path\\\\to'] below.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var reference = Assert.Single(InstructionsFile.FromSpans(spans).References);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileReferenceKind.Section, reference.Address.Kind),
                () => Assert.Equal("path\\to", reference.Address.Target));
        }

        [Fact]
        public void Should_not_treat_a_definition_tag_as_a_reference()
        {
            // Arrange — the bullet tag carries no '#', so it is a definition, not a reference.
            var content = "## Rules\n\n- [INST0006] **Do** cite [design-principles#INST0008] here.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("INST0006", Assert.Single(parsed.Body.Rules).Id),
                () => Assert.Equal("design-principles", Assert.Single(parsed.References).Address.Locator));
        }

        [Fact]
        public void Should_ignore_references_inside_inline_code_and_fenced_blocks()
        {
            // Arrange
            var content = "# Title\n\nReal [testing#INST0001] ref and `[testing#INST0014]` inline.\n\n```\n[testing#INST0002]\n```\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var reference = Assert.Single(InstructionsFile.FromSpans(spans).References);

            // Assert
            Assert.Equal("INST0001", reference.Address.Target);
        }

        [Fact]
        public void Should_not_treat_markdown_link_labels_or_ordinary_bracketed_prose_as_references()
        {
            // Arrange
            var content = "# Title\n\n[a#b](https://example.com) link and notes about [C# generics] go here.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);

            // Assert
            Assert.Multiple(
                () => Assert.Empty(parsed.References),
                () => Assert.Empty(parsed.Diagnostics));
        }

        [Fact]
        public void Should_flag_truncated_ids_and_rule_ranges_as_malformed_references()
        {
            // Arrange
            var content = "# Title\n\nSee [testing#INST014] and [testing#INST0014-INST0016] here.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var file = InstructionsFile.FromSpans(spans);

            // Assert
            Assert.Multiple(
                () => Assert.Empty(file.References),
                () => Assert.Equal(2, file.Diagnostics.Count),
                () => Assert.All(
                    file.Diagnostics,
                    static diagnostic => Assert.Equal(InstructionsFileDiagnosticKind.MalformedReference, diagnostic.Kind)),
                () => Assert.Contains(
                    file.Diagnostics,
                    static diagnostic => diagnostic.Message.Contains("ranges are not allowed", StringComparison.Ordinal)));
        }

        [Fact]
        public void Should_expose_body_relative_offsets_for_a_same_file_rule_reference()
        {
            // Arrange
            var content = "# Title\n\nsee [#INST0017] now.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var file = InstructionsFile.FromSpans(spans);
            var body = file.Body;
            var reference = Assert.Single(file.References);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileReferenceKind.Rule, reference.Address.Kind),
                () => Assert.Null(reference.Address.Locator),
                () => Assert.Equal("INST0017", reference.Address.Target),
                () => Assert.Equal(body.RawValue.IndexOf('[', StringComparison.Ordinal), reference.TextSpan.StartIndex),
                () => Assert.Equal(body.RawValue.IndexOf(']', StringComparison.Ordinal) + 1, reference.TextSpan.EndIndex));
        }
    }

    public sealed class Diagnostics
    {
        [Fact]
        public void Should_report_a_missing_tag_under_rules()
        {
            // Arrange
            var content = "## Rules\n\n- **Do** untagged.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);
            var diagnostics = parsed.Diagnostics;

            // Assert
            Assert.Multiple(
                () => Assert.Single(diagnostics),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MissingTag, diagnostics[0].Kind),
                () => Assert.Equal(2, diagnostics[0].Line));
        }

        [Fact]
        public void Should_report_a_duplicate_tag()
        {
            // Arrange
            var content = "## Rules\n\n- [INST0001] **Do** a.\n- [INST0001] **Do** b.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);
            var diagnostics = parsed.Diagnostics;

            // Assert
            Assert.Multiple(
                () => Assert.Single(diagnostics),
                () => Assert.Equal(InstructionsFileDiagnosticKind.DuplicateTag, diagnostics[0].Kind),
                () => Assert.Equal(3, diagnostics[0].Line));
        }

        [Fact]
        public void Should_report_a_tagged_rule_misplaced_outside_rules()
        {
            // Arrange
            var content = "## Notes\n\n- [INST0001] **Do** a.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = InstructionsFile.FromSpans(spans);
            var diagnostics = parsed.Diagnostics;

            // Assert
            Assert.Multiple(
                () => Assert.Single(diagnostics),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MisplacedRule, diagnostics[0].Kind),
                () => Assert.Equal(2, diagnostics[0].Line));
        }
    }
}
