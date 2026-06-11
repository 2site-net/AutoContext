namespace AutoContext.Instructions.Parser.Tests;

using AutoContext.Instructions.Parser.Syntax;
using AutoContext.Instructions.Parser.Tests.Support;

public sealed class InstructionsFileSyntaxParserTests
{
    public sealed class ParseFileAsync
    {
        [Fact]
        public async Task Should_reject_null_path()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser();

            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => InstructionsFileSyntaxParserTestDrainer.DrainFileAsync(parser, null!));
        }

        [Fact]
        public async Task Should_match_string_based_parse()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.instructions.md");
            await File.WriteAllTextAsync(path, InstructionsFileSyntaxParserFakeData.AllKinds, TestContext.Current.CancellationToken);

            try
            {
                // Act
                var fromFile = await InstructionsFileSyntaxParserTestDrainer.DrainFileAsync(parser, path);
                var fromString = await InstructionsFileSyntaxParserTestDrainer.DrainAsync(
                    parser,
                    InstructionsFileSyntaxParserFakeData.AllKinds);

                // Assert
                Assert.Equal(
                    fromString.Select(span => (span.Kind, span.TextSpan, span.LineSpan)),
                    fromFile.Select(span => (span.Kind, span.TextSpan, span.LineSpan)));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    public sealed class BlockPartition
    {
        [Fact]
        public async Task Should_partition_the_file_gaplessly()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(InstructionsFileSpanEmitLevel.Blocks, InstructionsFileSpanEmitScope.All);
            var document = InstructionsFileSyntaxParserFakeData.AllKinds;

            // Act
            var spans = (await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, document))
                .OrderBy(span => span.TextSpan.StartIndex)
                .ToList();

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, spans[0].TextSpan.StartIndex),
                () => Assert.Equal(document.Length, spans[^1].TextSpan.EndIndex),
                () => Assert.Equal(document, string.Concat(spans.Select(span => span.Text.ToString()))),
                () => Assert.All(spans, span => Assert.Equal(
                    document.Substring(span.TextSpan.StartIndex, span.TextSpan.Length),
                    span.Text.ToString())));
        }
    }

    public sealed class EmitMatrix
    {
        [Theory]
        [InlineData(
            InstructionsFileSpanEmitLevel.None,
            InstructionsFileSpanEmitScope.All,
            new InstructionsFileSpanKind[] { })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Full,
            InstructionsFileSpanEmitScope.None,
            new InstructionsFileSpanKind[] { })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Full,
            InstructionsFileSpanEmitScope.All,
            new[]
            {
                InstructionsFileSpanKind.FrontmatterBlock,
                InstructionsFileSpanKind.FrontmatterProperty,
                InstructionsFileSpanKind.FrontmatterKey,
                InstructionsFileSpanKind.FrontmatterValue,
                InstructionsFileSpanKind.Heading1,
                InstructionsFileSpanKind.Heading2,
                InstructionsFileSpanKind.Heading3,
                InstructionsFileSpanKind.Text,
                InstructionsFileSpanKind.PlainRule,
                InstructionsFileSpanKind.TaggedRule,
                InstructionsFileSpanKind.Tag,
                InstructionsFileSpanKind.Reference,
            })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Blocks,
            InstructionsFileSpanEmitScope.All,
            new[]
            {
                InstructionsFileSpanKind.FrontmatterBlock,
                InstructionsFileSpanKind.Heading1,
                InstructionsFileSpanKind.Heading2,
                InstructionsFileSpanKind.Heading3,
                InstructionsFileSpanKind.Text,
                InstructionsFileSpanKind.PlainRule,
                InstructionsFileSpanKind.TaggedRule,
            })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Tokens,
            InstructionsFileSpanEmitScope.All,
            new[]
            {
                InstructionsFileSpanKind.FrontmatterProperty,
                InstructionsFileSpanKind.FrontmatterKey,
                InstructionsFileSpanKind.FrontmatterValue,
                InstructionsFileSpanKind.Tag,
                InstructionsFileSpanKind.Reference,
            })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Full,
            InstructionsFileSpanEmitScope.Frontmatter,
            new[]
            {
                InstructionsFileSpanKind.FrontmatterBlock,
                InstructionsFileSpanKind.FrontmatterProperty,
                InstructionsFileSpanKind.FrontmatterKey,
                InstructionsFileSpanKind.FrontmatterValue,
            })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Blocks,
            InstructionsFileSpanEmitScope.Frontmatter,
            new[] { InstructionsFileSpanKind.FrontmatterBlock })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Tokens,
            InstructionsFileSpanEmitScope.Frontmatter,
            new[]
            {
                InstructionsFileSpanKind.FrontmatterProperty,
                InstructionsFileSpanKind.FrontmatterKey,
                InstructionsFileSpanKind.FrontmatterValue,
            })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Full,
            InstructionsFileSpanEmitScope.Headings,
            new[]
            {
                InstructionsFileSpanKind.Heading1,
                InstructionsFileSpanKind.Heading2,
                InstructionsFileSpanKind.Heading3,
            })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Tokens,
            InstructionsFileSpanEmitScope.Headings,
            new InstructionsFileSpanKind[] { })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Full,
            InstructionsFileSpanEmitScope.Text,
            new[] { InstructionsFileSpanKind.Text })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Full,
            InstructionsFileSpanEmitScope.Rules,
            new[]
            {
                InstructionsFileSpanKind.PlainRule,
                InstructionsFileSpanKind.TaggedRule,
                InstructionsFileSpanKind.Tag,
            })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Blocks,
            InstructionsFileSpanEmitScope.Rules,
            new[] { InstructionsFileSpanKind.PlainRule, InstructionsFileSpanKind.TaggedRule })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Tokens,
            InstructionsFileSpanEmitScope.Rules,
            new[] { InstructionsFileSpanKind.Tag })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Full,
            InstructionsFileSpanEmitScope.References,
            new[] { InstructionsFileSpanKind.Reference })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Blocks,
            InstructionsFileSpanEmitScope.References,
            new InstructionsFileSpanKind[] { })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Full,
            InstructionsFileSpanEmitScope.Body,
            new[]
            {
                InstructionsFileSpanKind.Heading1,
                InstructionsFileSpanKind.Heading2,
                InstructionsFileSpanKind.Heading3,
                InstructionsFileSpanKind.Text,
                InstructionsFileSpanKind.PlainRule,
                InstructionsFileSpanKind.TaggedRule,
                InstructionsFileSpanKind.Tag,
                InstructionsFileSpanKind.Reference,
            })]
        [InlineData(
            InstructionsFileSpanEmitLevel.Tokens,
            InstructionsFileSpanEmitScope.Body,
            new[] { InstructionsFileSpanKind.Tag, InstructionsFileSpanKind.Reference })]
        public async Task Should_emit_exactly_the_kinds_for_each_level_and_scope(
            InstructionsFileSpanEmitLevel level,
            InstructionsFileSpanEmitScope scope,
            InstructionsFileSpanKind[] expected)
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(level, scope);

            // Act
            var spans = await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, InstructionsFileSyntaxParserFakeData.AllKinds);
            HashSet<InstructionsFileSpanKind> kinds = [.. spans.Select(span => span.Kind)];

            // Assert
            HashSet<InstructionsFileSpanKind> expectedKinds = [.. expected];
            Assert.Equal(expectedKinds, kinds);
        }
    }

    public sealed class Frontmatter
    {
        [Fact]
        public async Task Should_cover_the_block_including_the_closing_delimiter()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser();
            var content = "---\nname: \"x\"\n---\nBody.\n";

            // Act
            var spans = await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, content);
            var block = spans.Single(span => span.Kind == InstructionsFileSpanKind.FrontmatterBlock);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, block.TextSpan.StartIndex),
                () => Assert.Equal(18, block.TextSpan.Length),
                () => Assert.Equal(0, block.LineSpan.StartLine),
                () => Assert.Equal(3, block.LineSpan.LineCount),
                () => Assert.Equal("---\nname: \"x\"\n---\n", block.Text.ToString()));
        }

        [Fact]
        public async Task Should_emit_property_before_key_and_value()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.Frontmatter);
            var content = "---\nname: \"x\"\n---\nBody.\n";

            // Act
            var kinds = (await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, content))
                .Select(span => span.Kind)
                .ToList();

            // Assert
            Assert.Equal(
                [
                    InstructionsFileSpanKind.FrontmatterProperty,
                    InstructionsFileSpanKind.FrontmatterKey,
                    InstructionsFileSpanKind.FrontmatterValue,
                ],
                kinds);
        }
    }

    public sealed class Headings
    {
        [Fact]
        public async Task Should_classify_levels_one_two_and_three()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Headings);
            var content = "# One\n## Two\n### Three\n";

            // Act
            var kinds = (await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, content))
                .Select(span => span.Kind)
                .ToList();

            // Assert
            Assert.Equal(
                [
                    InstructionsFileSpanKind.Heading1,
                    InstructionsFileSpanKind.Heading2,
                    InstructionsFileSpanKind.Heading3,
                ],
                kinds);
        }

        [Fact]
        public async Task Should_not_treat_a_fenced_hash_line_as_a_heading()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Headings);
            var content = "```\n# not a heading\n```\n";

            // Act
            var spans = await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, content);

            // Assert
            Assert.Empty(spans);
        }
    }

    public sealed class Rules
    {
        [Fact]
        public async Task Should_emit_the_tagged_rule_before_its_tokens()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "- [INST0001] **Do** the thing.\n";

            // Act
            var kinds = (await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, content))
                .Select(span => span.Kind)
                .ToList();

            // Assert
            Assert.Equal(
                [
                    InstructionsFileSpanKind.TaggedRule,
                    InstructionsFileSpanKind.Tag,
                ],
                kinds);
        }

        [Fact]
        public async Task Should_trim_trailing_blank_lines_from_the_rule_block()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Rules);
            var content = "- **Do** a.\n\n\nNext para.\n";

            // Act
            var rule = (await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.PlainRule, rule.Kind),
                () => Assert.Equal(0, rule.TextSpan.StartIndex),
                () => Assert.Equal(12, rule.TextSpan.Length),
                () => Assert.Equal(1, rule.LineSpan.LineCount));
        }
    }

    public sealed class References
    {
        [Fact]
        public async Task Should_emit_a_reference_for_a_valid_locator()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "See [foo.instructions.md#INST0001] please.\n";

            // Act
            var spans = await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, content);

            // Assert
            var reference = Assert.Single(spans);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.Reference, reference.Kind),
                () => Assert.Equal("[foo.instructions.md#INST0001]", reference.Text.ToString()));
        }

        [Fact]
        public async Task Should_emit_a_reference_for_a_malformed_attempt()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "See [Some Heading#INST0001] here.\n";

            // Act
            var spans = await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, content);

            // Assert
            var reference = Assert.Single(spans);
            Assert.Equal("[Some Heading#INST0001]", reference.Text.ToString());
        }

        [Fact]
        public async Task Should_not_emit_a_reference_inside_a_fence()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "```\n[foo.instructions.md#INST0001]\n```\n";

            // Act
            var spans = await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, content);

            // Assert
            Assert.Empty(spans);
        }
    }

    public sealed class Coordinates
    {
        [Fact]
        public async Task Should_count_crlf_as_two_characters()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.All);
            var content = "# A\r\nB\r\n";

            // Act
            var spans = await InstructionsFileSyntaxParserTestDrainer.DrainAsync(parser, content);
            var heading = spans.Single(span => span.Kind == InstructionsFileSpanKind.Heading1);
            var text = spans.Single(span => span.Kind == InstructionsFileSpanKind.Text);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, heading.TextSpan.StartIndex),
                () => Assert.Equal(5, heading.TextSpan.Length),
                () => Assert.Equal(5, text.TextSpan.StartIndex),
                () => Assert.Equal(3, text.TextSpan.Length),
                () => Assert.Equal(content.Length, text.TextSpan.EndIndex));
        }
    }

    public sealed class Diagnostics
    {
        [Fact]
        public async Task Should_flag_a_plain_rule_under_rules_as_missing_a_tag()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n- **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var rule = Assert.Single(tree.Body);
            var diagnostic = Assert.Single(tree.Diagnostics);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.PlainRule, rule.Kind),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MissingTag, diagnostic.Diagnostic.Kind));
        }

        [Fact]
        public async Task Should_not_flag_a_plain_rule_outside_rules()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Notes\n\n- **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var rule = Assert.Single(tree.Body);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.PlainRule, rule.Kind),
                () => Assert.Empty(tree.Diagnostics));
        }

        [Fact]
        public async Task Should_flag_a_tagged_rule_outside_rules_as_misplaced()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Rules);
            var content = "- [INST0001] **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var rule = Assert.Single(tree.Body);
            var diagnostic = Assert.Single(tree.Diagnostics);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.TaggedRule, rule.Kind),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MisplacedRule, diagnostic.Diagnostic.Kind));
        }

        [Fact]
        public async Task Should_not_flag_a_unique_tagged_rule_under_rules()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n- [INST0001] **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            Assert.Empty(tree.Diagnostics);
        }

        [Fact]
        public async Task Should_flag_a_repeated_tag_on_the_second_rule_only()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n- [INST0001] **Do** a.\n\n- [INST0001] **Do** b.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var rules = tree.Body
                .Where(span => span.Kind == InstructionsFileSpanKind.TaggedRule)
                .ToList();
            var diagnostic = Assert.Single(tree.Diagnostics);
            Assert.Multiple(
                () => Assert.Equal(2, rules.Count),
                () => Assert.Equal(InstructionsFileDiagnosticKind.DuplicateTag, diagnostic.Diagnostic.Kind),
                () => Assert.Equal(rules[1].LineSpan.StartLine, diagnostic.LineSpan.StartLine));
        }

        [Fact]
        public async Task Should_emit_a_malformed_tag_diagnostic_alongside_the_tag_token()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n- [foo] **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var diagnostic = Assert.Single(tree.Diagnostics);
            Assert.Multiple(
                () => Assert.Contains(tree.Body, span => span.Kind == InstructionsFileSpanKind.Tag),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MalformedTag, diagnostic.Diagnostic.Kind));
        }

        [Fact]
        public async Task Should_still_emit_a_malformed_tag_diagnostic_when_the_tag_token_is_filtered_out()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n- [foo] **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var rule = Assert.Single(tree.Body);
            var diagnostic = Assert.Single(tree.Diagnostics);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.TaggedRule, rule.Kind),
                () => Assert.DoesNotContain(tree.Body, span => span.Kind == InstructionsFileSpanKind.Tag),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MalformedTag, diagnostic.Diagnostic.Kind));
        }

        [Fact]
        public async Task Should_emit_a_malformed_reference_diagnostic_for_the_reference_token()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "See [Bad Locator#INST0001] here.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var reference = Assert.Single(tree.Body);
            var diagnostic = Assert.Single(tree.Diagnostics);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.Reference, reference.Kind),
                () => Assert.Empty(tree.References),
                () => Assert.Equal(InstructionsFileDiagnosticKind.MalformedReference, diagnostic.Diagnostic.Kind));
        }

        [Fact]
        public async Task Should_flag_a_reference_rule_range_as_malformed()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "See [#INST0001-INST0003] here.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var diagnostic = Assert.Single(tree.Diagnostics);
            Assert.Equal(InstructionsFileDiagnosticKind.MalformedReference, diagnostic.Diagnostic.Kind);
        }

        [Fact]
        public async Task Should_not_flag_a_well_formed_reference()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "See [foo.instructions.md#INST0001] here.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            Assert.Multiple(
                () => Assert.Single(tree.References),
                () => Assert.Empty(tree.Diagnostics));
        }

        [Fact]
        public async Task Should_not_flag_a_malformed_reference_when_reference_tokens_are_not_emitted()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.All);
            var content = "See [Bad Locator#INST0001] here.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            Assert.Empty(tree.Diagnostics);
        }

        [Fact]
        public async Task Should_close_the_rules_section_on_a_thematic_break()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n---\n\n- **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var rule = Assert.Single(tree.Body);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.PlainRule, rule.Kind),
                () => Assert.Empty(tree.Diagnostics));
        }

        [Fact]
        public async Task Should_keep_the_rules_section_open_for_a_thematic_break_inside_a_fence()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n```\n---\n```\n\n- **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var diagnostic = Assert.Single(tree.Diagnostics);
            Assert.Equal(InstructionsFileDiagnosticKind.MissingTag, diagnostic.Diagnostic.Kind);
        }

        [Fact]
        public async Task Should_keep_the_rules_section_open_across_a_subsection_heading()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n### Subsection\n\n- **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var diagnostic = Assert.Single(tree.Diagnostics);
            Assert.Equal(InstructionsFileDiagnosticKind.MissingTag, diagnostic.Diagnostic.Kind);
        }

        [Fact]
        public async Task Should_close_the_rules_section_on_the_next_level_two_heading()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n## Other\n\n- **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            Assert.Empty(tree.Diagnostics);
        }

        [Fact]
        public async Task Should_close_the_rules_section_on_a_level_one_heading()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n# Top\n\n- **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            Assert.Empty(tree.Diagnostics);
        }

        [Fact]
        public async Task Should_omit_diagnostics_when_disabled()
        {
            // Arrange
            var parser = new InstructionsFileSyntaxParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules,
                includeDiagnostics: false);
            var content = "## Rules\n\n- **Do** a thing.\n";

            // Act
            var tree = await InstructionsFileSyntaxParserTestDrainer.DrainTreeAsync(parser, content);

            // Assert
            var rule = Assert.Single(tree.Body);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.PlainRule, rule.Kind),
                () => Assert.Empty(tree.Diagnostics));
        }
    }
}
