namespace AutoContext.Instructions.Manifest.Generator.Tests;

using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Instructions.Manifest.Generator;
using AutoContext.Instructions.Manifest.Generator.Tests.Support;

public sealed class CorpusParserTests
{
    public sealed class Parse(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        private readonly CorpusParser _sut = new();

        [Fact]
        public async Task Should_reject_null_corpus_directory()
        {
            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _sut.ParseAsync(null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_key_files_by_basename_stem()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "testing.instructions.md", "testing (v1.0.0)", "Testing.");
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "code-review.instructions.md", "code-review (v1.0.0)", "Review.");

            // Act
            var parsed = await _sut.ParseAsync(corpus, TestContext.Current.CancellationToken);

            // Assert
            var expectedKeys = new[] { "code-review", "testing" };
            Assert.Equal(expectedKeys, parsed.Keys.Order(StringComparer.Ordinal));
        }

        [Fact]
        public async Task Should_bundle_verbatim_content_and_full_parse()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "testing.instructions.md", "testing (v1.0.0)", "Testing.", body: "## Heading\n\n- [INST0001] **Do** test.\n");

            // Act
            var file = (await _sut.ParseAsync(corpus, TestContext.Current.CancellationToken))["testing"];

            // Assert
            Assert.Multiple(
                () => Assert.Equal("testing.instructions.md", file.FileName),
                () => Assert.Contains("[INST0001]", file.Content.RawContent, StringComparison.Ordinal),
                () => Assert.Equal("testing (v1.0.0)", file.Content.Frontmatter.Name),
                () => Assert.Equal("INST0001", Assert.Single(file.Content.Body.Rules).Id));
        }

        [Fact]
        public async Task Should_compute_sha256_content_hash()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "testing.instructions.md", "testing (v1.0.0)", "Testing.");

            // Act
            var file = (await _sut.ParseAsync(corpus, TestContext.Current.CancellationToken))["testing"];

            // Assert
            Assert.Multiple(
                () => Assert.StartsWith("sha256:", file.ContentHash, StringComparison.Ordinal),
                () => Assert.Equal(71, file.ContentHash.Length));
        }

        [Fact]
        public async Task Should_exclude_frontmatter_from_content_hash()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "testing.instructions.md", "testing (v1.0.0)", "Testing.", body: "# Heading\n\nShared body.\n");
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "design.instructions.md", "design (v9.9.9)", "Different frontmatter.", applyTo: "**/*.cs", body: "# Heading\n\nShared body.\n");

            // Act
            var parsed = await _sut.ParseAsync(corpus, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(parsed["testing"].ContentHash, parsed["design"].ContentHash);
        }

        [Fact]
        public async Task Should_flag_sibling_changelog()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "testing.instructions.md", "testing (v1.0.0)", "Testing.");
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "design.instructions.md", "design (v1.0.0)", "Design.");
            InstructionsCorpusTestWriter.WriteChangelog(corpus, "testing");

            // Act
            var parsed = await _sut.ParseAsync(corpus, TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(parsed["testing"].HasChangelog),
                () => Assert.False(parsed["design"].HasChangelog));
        }
    }
}
