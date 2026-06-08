namespace AutoContext.Instructions.Parser.Tests;

using AutoContext.Instructions.Parser.Tests.Support;

public sealed class InstructionsFileStructuredParserTests
{
    public sealed class Parse
    {
        [Fact]
        public void Should_reject_null_spans()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => new InstructionsFileStructuredParser().Parse(null!));
        }

        [Fact]
        public void Should_preserve_verbatim_content()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\n---\n# Title\n\nBody.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = new InstructionsFileStructuredParser().Parse(spans);

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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);

            // Assert
            Assert.Equal("# Title\n\nBody.\n", parsed.Body.RawValue);
        }
    }

    public sealed class ParseFileAsync
    {
        [Fact]
        public async Task Should_reject_null_path()
        {
            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => new InstructionsFileStructuredParser().ParseFileAsync(null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_parse_a_file_from_disk()
        {
            // Arrange
            var content = "---\nname: \"lang-csharp (v1.2.3)\"\n---\n## Rules\n\n- [INST0001] **Do** a.\n";
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.instructions.md");
            await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);

            try
            {
                // Act
                var parsed = await new InstructionsFileStructuredParser().ParseFileAsync(path, TestContext.Current.CancellationToken);

                // Assert
                Assert.Multiple(
                    () => Assert.Equal(content, parsed.RawContent),
                    () => Assert.Equal("lang-csharp (v1.2.3)", parsed.Frontmatter.Name),
                    () => Assert.Equal("1.2.3", parsed.Frontmatter.Version),
                    () => Assert.Equal("INST0001", Assert.Single(parsed.Body.Rules).Id));
            }
            finally
            {
                File.Delete(path);
            }
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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
            var frontmatter = parsed.Frontmatter;

            // Assert
            Assert.Multiple(
                () => Assert.Null(frontmatter.Name),
                () => Assert.Null(frontmatter.Description),
                () => Assert.Null(frontmatter.ApplyTo),
                () => Assert.Null(frontmatter.Version),
                () => Assert.Equal(string.Empty, frontmatter.RawValue));
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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
            var sections = parsed.Body.Sections;

            // Assert
            Assert.Multiple(
                () => Assert.Single(sections),
                () => Assert.Equal("Only", sections[0].Heading));
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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
            var body = parsed.Body;

            // Assert
            Assert.Multiple(
                () => Assert.Single(body.Rules),
                () => Assert.Null(body.Rules[0].Id),
                () => Assert.Equal("- [oops] **Do** gamma.", body.Rules[0].Text),
                () => Assert.Single(body.Diagnostics),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MalformedTag, body.Diagnostics[0].Kind));
        }

        [Fact]
        public void Should_address_rule_lines_relative_to_the_body()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\n---\n## Rules\n\n- [INST0001] **Do** a.\n";
            var spans = InstructionsFileSpanStream.From(content);

            // Act
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
            var body = parsed.Body;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, body.Rules[0].LineSpan.StartLine),
                () => Assert.Equal(3, body.Rules[0].LineSpan.EndLine),
                () => Assert.Equal(0, body.Sections[0].TextSpan.StartIndex));
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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
            var references = parsed.Body.References;

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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
            var body = parsed.Body;

            // Assert
            Assert.Multiple(
                () => Assert.Empty(body.References),
                () => Assert.Single(body.Diagnostics),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MalformedReference, body.Diagnostics[0].Kind));
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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
            var diagnostics = parsed.Body.Diagnostics;

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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
            var diagnostics = parsed.Body.Diagnostics;

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
            var parsed = new InstructionsFileStructuredParser().Parse(spans);
            var diagnostics = parsed.Body.Diagnostics;

            // Assert
            Assert.Multiple(
                () => Assert.Single(diagnostics),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MisplacedRule, diagnostics[0].Kind),
                () => Assert.Equal(2, diagnostics[0].Line));
        }
    }
}
