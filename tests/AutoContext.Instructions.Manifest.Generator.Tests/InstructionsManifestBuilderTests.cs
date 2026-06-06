namespace AutoContext.Instructions.Manifest.Generator.Tests;

using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Instructions.Manifest.Generator;
using AutoContext.Instructions.Manifest.Generator.Tests.Support;

public sealed class InstructionsManifestBuilderTests
{
    public sealed class Build(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        private readonly CorpusParser _corpusParser = new();
        private readonly InstructionsManifestBuilder _sut = new();

        [Fact]
        public void Should_reject_null_corpus()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => _sut.Build(null!));
        }

        [Fact]
        public void Should_build_one_entry_per_corpus_file()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "alpha.instructions.md", "alpha (v1.0.0)", "Alpha.");
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "beta.instructions.md", "beta (v2.3.4)", "Beta.");

            // Act
            var manifest = _sut.Build(_corpusParser.Parse(corpus));

            // Assert
            Assert.Multiple(
                () => Assert.Equal("1", manifest.SchemaVersion),
                () => Assert.Equal(2, manifest.Instructions.Count));
        }

        [Fact]
        public void Should_order_entries_by_key()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "zulu.instructions.md", "zulu (v1.0.0)", "Zulu.");
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "alpha.instructions.md", "alpha (v1.0.0)", "Alpha.");
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "mike.instructions.md", "mike (v1.0.0)", "Mike.");

            // Act
            var manifest = _sut.Build(_corpusParser.Parse(corpus));

            // Assert
            var expectedKeys = new[] { "alpha", "mike", "zulu" };
            Assert.Equal(expectedKeys, manifest.Instructions.Select(static entry => entry.Key));
        }

        [Fact]
        public void Should_extract_key_and_version_from_name()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "code-review.instructions.md", "code-review (v3.1.4)", "Review.");

            // Act
            var entry = _sut.Build(_corpusParser.Parse(corpus)).Instructions.Single();

            // Assert
            Assert.Multiple(
                () => Assert.Equal("code-review", entry.Key),
                () => Assert.Equal("3.1.4", entry.Version),
                () => Assert.Equal("code-review.instructions.md", entry.FileName),
                () => Assert.Equal("Review.", entry.Description));
        }

        [Fact]
        public void Should_carry_verbatim_apply_to()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "lang-csharp.instructions.md", "lang-csharp (v1.0.0)", "C#.", applyTo: "**/*.cs");

            // Act
            var entry = _sut.Build(_corpusParser.Parse(corpus)).Instructions.Single();

            // Assert
            Assert.Equal("**/*.cs", entry.ApplyTo);
        }

        [Fact]
        public void Should_omit_apply_to_when_absent()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "code-review.instructions.md", "code-review (v1.0.0)", "Review.");

            // Act
            var entry = _sut.Build(_corpusParser.Parse(corpus)).Instructions.Single();

            // Assert
            Assert.Null(entry.ApplyTo);
        }

        [Fact]
        public void Should_detect_sibling_changelog()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "code-review.instructions.md", "code-review (v1.0.0)", "Review.");
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "design.instructions.md", "design (v1.0.0)", "Design.");
            InstructionsCorpusTestWriter.WriteChangelog(corpus, "code-review");

            // Act
            var manifest = _sut.Build(_corpusParser.Parse(corpus));

            // Assert
            Assert.Multiple(
                () => Assert.True(manifest.Instructions.Single(static e => e.Key == "code-review").HasChangelog),
                () => Assert.False(manifest.Instructions.Single(static e => e.Key == "design").HasChangelog));
        }

        [Fact]
        public void Should_hash_body_with_sha256_prefix()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "code-review.instructions.md", "code-review (v1.0.0)", "Review.");

            // Act
            var entry = _sut.Build(_corpusParser.Parse(corpus)).Instructions.Single();

            // Assert
            Assert.Multiple(
                () => Assert.StartsWith("sha256:", entry.ContentHash, StringComparison.Ordinal),
                () => Assert.Equal(71, entry.ContentHash.Length));
        }

        [Fact]
        public void Should_extract_section_index_from_body()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus,
                "code-review.instructions.md",
                "code-review (v1.0.0)",
                "Review.",
                body: "## Overview\n\nText.\n\n### Details\n\nMore.\n");

            // Act
            var sections = _sut.Build(_corpusParser.Parse(corpus)).Instructions.Single().Sections;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, sections.Count),
                () => Assert.Equal("Overview", sections[0].Heading),
                () => Assert.Equal("overview", sections[0].Anchor),
                () => Assert.Null(sections[0].Parent),
                () => Assert.Equal("Details", sections[1].Heading),
                () => Assert.Equal("overview-details", sections[1].Anchor),
                () => Assert.Equal("Overview", sections[1].Parent));
        }

        [Fact]
        public void Should_yield_empty_sections_when_body_has_no_headings()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "code-review.instructions.md", "code-review (v1.0.0)", "Review.", body: "Just prose.\n");

            // Act
            var sections = _sut.Build(_corpusParser.Parse(corpus)).Instructions.Single().Sections;

            // Assert
            Assert.Empty(sections);
        }

        [Fact]
        public void Should_derive_sorted_extension_set_from_apply_to()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "lang-dotnet.instructions.md", "lang-dotnet (v1.0.0)", ".NET.", applyTo: "**/*.{vb,cs,fs}");

            // Act
            var entry = _sut.Build(_corpusParser.Parse(corpus)).Instructions.Single();

            // Assert
            var expectedExtensions = new[] { "cs", "fs", "vb" };
            Assert.Equal(expectedExtensions, entry.Extensions);
        }

        [Fact]
        public void Should_yield_null_extensions_when_apply_to_absent()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "code-review.instructions.md", "code-review (v1.0.0)", "Review.");

            // Act
            var entry = _sut.Build(_corpusParser.Parse(corpus)).Instructions.Single();

            // Assert
            Assert.Null(entry.Extensions);
        }

        [Fact]
        public void Should_yield_empty_extensions_when_apply_to_names_no_extension()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "global.instructions.md", "global (v1.0.0)", "Global.", applyTo: "**/*");

            // Act
            var entry = _sut.Build(_corpusParser.Parse(corpus)).Instructions.Single();

            // Assert
            Assert.Empty(entry.Extensions!);
        }

        [Fact]
        public void Should_throw_when_name_is_missing()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            File.WriteAllText(Path.Combine(corpus, "broken.instructions.md"), "---\ndescription: \"No name.\"\n---\nBody.\n");
            var parsed = _corpusParser.Parse(corpus);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Build(parsed));

            // Assert
            Assert.Contains("broken.instructions.md", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_throw_when_key_does_not_match_file_name()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "code-review.instructions.md", "mismatch (v1.0.0)", "Review.");
            var parsed = _corpusParser.Parse(corpus);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Build(parsed));

            // Assert
            Assert.Contains("does not equal file basename", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_throw_on_duplicate_section_anchor()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus,
                "code-review.instructions.md",
                "code-review (v1.0.0)",
                "Review.",
                body: "## Do\n\nFirst.\n\n## Do\n\nSecond.\n");
            var parsed = _corpusParser.Parse(corpus);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Build(parsed));

            // Assert
            Assert.Multiple(
                () => Assert.Contains("code-review.instructions.md", exception.Message, StringComparison.Ordinal),
                () => Assert.Contains("duplicate section anchor 'do'", exception.Message, StringComparison.Ordinal));
        }
    }
}
