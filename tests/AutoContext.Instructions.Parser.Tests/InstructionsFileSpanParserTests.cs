namespace AutoContext.Instructions.Parser.Tests;

using AutoContext.Instructions.Parser.Tests.Support;

public sealed class InstructionsFileSpanParserTests
{
    public sealed class ParseAsync
    {
        [Fact]
        public async Task Should_reject_null_reader()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser();

            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => InstructionsFileSpanParserTestDrainer.DrainAsync(parser, (TextReader)null!));
        }
    }

    public sealed class ParseFileAsync
    {
        [Fact]
        public async Task Should_reject_null_path()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser();

            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => InstructionsFileSpanParserTestDrainer.DrainFileAsync(parser, null!));
        }

        [Fact]
        public async Task Should_match_string_based_parse()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser();
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.instructions.md");
            await File.WriteAllTextAsync(path, InstructionsFileSpanParserFakeData.AllKinds, TestContext.Current.CancellationToken);

            try
            {
                // Act
                var fromFile = await InstructionsFileSpanParserTestDrainer.DrainFileAsync(parser, path);
                var fromString = await InstructionsFileSpanParserTestDrainer.DrainAsync(
                    parser,
                    InstructionsFileSpanParserFakeData.AllKinds);

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
            var parser = new InstructionsFileSpanParser(InstructionsFileSpanEmitLevel.Blocks, InstructionsFileSpanEmitScope.All);
            var document = InstructionsFileSpanParserFakeData.AllKinds;

            // Act
            var spans = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, document))
                .OrderBy(span => span.TextSpan.StartIndex)
                .ToList();

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, spans[0].TextSpan.StartIndex),
                () => Assert.Equal(document.Length, spans[^1].TextSpan.EndIndex),
                () => Assert.Equal(document, string.Concat(spans.Select(span => span.Text))),
                () => Assert.All(spans, span => Assert.Equal(
                    document.Substring(span.TextSpan.StartIndex, span.TextSpan.Length),
                    span.Text)));
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
            var parser = new InstructionsFileSpanParser(level, scope);

            // Act
            var spans = await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, InstructionsFileSpanParserFakeData.AllKinds);
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
            var parser = new InstructionsFileSpanParser();
            var content = "---\nname: \"x\"\n---\nBody.\n";

            // Act
            var spans = await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content);
            var block = spans.Single(span => span.Kind == InstructionsFileSpanKind.FrontmatterBlock);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, block.TextSpan.StartIndex),
                () => Assert.Equal(18, block.TextSpan.Length),
                () => Assert.Equal(0, block.LineSpan.StartLine),
                () => Assert.Equal(3, block.LineSpan.LineCount),
                () => Assert.Equal("---\nname: \"x\"\n---\n", block.Text));
        }

        [Fact]
        public async Task Should_emit_property_before_key_and_value()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.Frontmatter);
            var content = "---\nname: \"x\"\n---\nBody.\n";

            // Act
            var kinds = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content))
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
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Headings);
            var content = "# One\n## Two\n### Three\n";

            // Act
            var kinds = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content))
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
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Headings);
            var content = "```\n# not a heading\n```\n";

            // Act
            var spans = await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content);

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
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "- [INST0001] **Do** the thing.\n";

            // Act
            var kinds = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content))
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
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Rules);
            var content = "- **Do** a.\n\n\nNext para.\n";

            // Act
            var rule = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

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
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "See [foo.instructions.md#INST0001] please.\n";

            // Act
            var spans = await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content);

            // Assert
            var reference = Assert.Single(spans);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.Reference, reference.Kind),
                () => Assert.Equal("[foo.instructions.md#INST0001]", reference.Text));
        }

        [Fact]
        public async Task Should_emit_a_reference_for_a_malformed_attempt()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "See [Some Heading#INST0001] here.\n";

            // Act
            var spans = await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content);

            // Assert
            var reference = Assert.Single(spans);
            Assert.Equal("[Some Heading#INST0001]", reference.Text);
        }

        [Fact]
        public async Task Should_not_emit_a_reference_inside_a_fence()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "```\n[foo.instructions.md#INST0001]\n```\n";

            // Act
            var spans = await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content);

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
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.All);
            var content = "# A\r\nB\r\n";

            // Act
            var spans = await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content);
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
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n- **Do** a thing.\n";

            // Act
            var rule = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            var diagnostic = Assert.Single(rule.Diagnostics);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.PlainRule, rule.Kind),
                () => Assert.Equal(InstructionsFileSpanDiagnosticKind.MissingTag, diagnostic.Kind));
        }

        [Fact]
        public async Task Should_not_flag_a_plain_rule_outside_rules()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Notes\n\n- **Do** a thing.\n";

            // Act
            var rule = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.PlainRule, rule.Kind),
                () => Assert.Empty(rule.Diagnostics));
        }

        [Fact]
        public async Task Should_flag_a_tagged_rule_outside_rules_as_misplaced()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Rules);
            var content = "- [INST0001] **Do** a thing.\n";

            // Act
            var rule = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            var diagnostic = Assert.Single(rule.Diagnostics);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.TaggedRule, rule.Kind),
                () => Assert.Equal(InstructionsFileSpanDiagnosticKind.MisplacedRule, diagnostic.Kind));
        }

        [Fact]
        public async Task Should_not_flag_a_unique_tagged_rule_under_rules()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n- [INST0001] **Do** a thing.\n";

            // Act
            var spans = await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content);

            // Assert
            Assert.All(spans, span => Assert.Empty(span.Diagnostics));
        }

        [Fact]
        public async Task Should_flag_a_repeated_tag_on_the_second_rule_only()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n- [INST0001] **Do** a.\n\n- [INST0001] **Do** b.\n";

            // Act
            var rules = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content))
                .Where(span => span.Kind == InstructionsFileSpanKind.TaggedRule)
                .ToList();

            // Assert
            var diagnostic = Assert.Single(rules[1].Diagnostics);
            Assert.Multiple(
                () => Assert.Empty(rules[0].Diagnostics),
                () => Assert.Equal(InstructionsFileSpanDiagnosticKind.DuplicateTag, diagnostic.Kind));
        }

        [Fact]
        public async Task Should_attach_a_malformed_tag_to_the_tag_token_when_tokens_are_emitted()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n- [foo] **Do** a thing.\n";

            // Act
            var spans = await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content);
            var tag = spans.Single(span => span.Kind == InstructionsFileSpanKind.Tag);
            var rule = spans.Single(span => span.Kind == InstructionsFileSpanKind.TaggedRule);

            // Assert
            var diagnostic = Assert.Single(tag.Diagnostics);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanDiagnosticKind.MalformedTag, diagnostic.Kind),
                () => Assert.Empty(rule.Diagnostics));
        }

        [Fact]
        public async Task Should_promote_a_malformed_tag_to_the_rule_block_when_the_tag_token_is_filtered_out()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n- [foo] **Do** a thing.\n";

            // Act
            var rule = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            var diagnostic = Assert.Single(rule.Diagnostics);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.TaggedRule, rule.Kind),
                () => Assert.Equal(InstructionsFileSpanDiagnosticKind.MalformedTag, diagnostic.Kind));
        }

        [Fact]
        public async Task Should_attach_a_malformed_reference_to_the_reference_token()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "See [Bad Locator#INST0001] here.\n";

            // Act
            var reference = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            var diagnostic = Assert.Single(reference.Diagnostics);
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.Reference, reference.Kind),
                () => Assert.Equal(InstructionsFileSpanDiagnosticKind.MalformedReference, diagnostic.Kind));
        }

        [Fact]
        public async Task Should_flag_a_reference_rule_range_as_malformed()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "See [#INST0001-INST0003] here.\n";

            // Act
            var reference = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            var diagnostic = Assert.Single(reference.Diagnostics);
            Assert.Equal(InstructionsFileSpanDiagnosticKind.MalformedReference, diagnostic.Kind);
        }

        [Fact]
        public async Task Should_not_flag_a_well_formed_reference()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Tokens,
                InstructionsFileSpanEmitScope.References);
            var content = "See [foo.instructions.md#INST0001] here.\n";

            // Act
            var reference = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            Assert.Empty(reference.Diagnostics);
        }

        [Fact]
        public async Task Should_not_promote_a_malformed_reference_to_a_block()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Blocks,
                InstructionsFileSpanEmitScope.All);
            var content = "See [Bad Locator#INST0001] here.\n";

            // Act
            var spans = await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content);

            // Assert
            Assert.All(spans, span => Assert.Empty(span.Diagnostics));
        }

        [Fact]
        public async Task Should_close_the_rules_section_on_a_thematic_break()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n---\n\n- **Do** a thing.\n";

            // Act
            var rule = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.PlainRule, rule.Kind),
                () => Assert.Empty(rule.Diagnostics));
        }

        [Fact]
        public async Task Should_keep_the_rules_section_open_for_a_thematic_break_inside_a_fence()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n```\n---\n```\n\n- **Do** a thing.\n";

            // Act
            var rule = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            var diagnostic = Assert.Single(rule.Diagnostics);
            Assert.Equal(InstructionsFileSpanDiagnosticKind.MissingTag, diagnostic.Kind);
        }

        [Fact]
        public async Task Should_keep_the_rules_section_open_across_a_subsection_heading()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n### Subsection\n\n- **Do** a thing.\n";

            // Act
            var rule = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            var diagnostic = Assert.Single(rule.Diagnostics);
            Assert.Equal(InstructionsFileSpanDiagnosticKind.MissingTag, diagnostic.Kind);
        }

        [Fact]
        public async Task Should_close_the_rules_section_on_the_next_level_two_heading()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n## Other\n\n- **Do** a thing.\n";

            // Act
            var rule = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            Assert.Empty(rule.Diagnostics);
        }

        [Fact]
        public async Task Should_close_the_rules_section_on_a_level_one_heading()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules);
            var content = "## Rules\n\n# Top\n\n- **Do** a thing.\n";

            // Act
            var rule = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            Assert.Empty(rule.Diagnostics);
        }

        [Fact]
        public async Task Should_omit_diagnostics_when_disabled()
        {
            // Arrange
            var parser = new InstructionsFileSpanParser(
                InstructionsFileSpanEmitLevel.Full,
                InstructionsFileSpanEmitScope.Rules,
                includeDiagnostics: false);
            var content = "## Rules\n\n- **Do** a thing.\n";

            // Act
            var rule = (await InstructionsFileSpanParserTestDrainer.DrainAsync(parser, content)).Single();

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileSpanKind.PlainRule, rule.Kind),
                () => Assert.Empty(rule.Diagnostics));
        }
    }
}
