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
}
