namespace AutoContext.Instructions.Manifest.Generator.Tests;

using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Instructions.Manifest.Generator;
using AutoContext.Instructions.Manifest.Generator.Tests.Support;

public sealed class InstructionsMetadataBuilderTests
{
    public sealed class Build(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        private readonly InstructionsMetadataBuilder _sut = new();

        [Fact]
        public void Should_reject_null_manifest()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => _sut.Build(null!, tempDirectory.CreateDirectory()));
        }

        [Fact]
        public void Should_reject_null_corpus_directory()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => _sut.Build(manifest, null!));
        }

        [Fact]
        public void Should_carry_schema_version_from_manifest()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            var manifest = InstructionsManifestFakeData.CreateManifest();

            // Act
            var metadata = _sut.Build(manifest, corpus);

            // Assert
            Assert.Equal("1", metadata.SchemaVersion);
        }

        [Fact]
        public void Should_pass_through_wire_entry_fields()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "code-review.instructions.md", "code-review (v1.0.0)", "Review.");
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(applyTo: "**/*.cs", hasChangelog: true, contentHash: "sha256:xyz"));

            // Act
            var entry = _sut.Build(manifest, corpus).Instructions.Single();

            // Assert
            Assert.Multiple(
                () => Assert.Equal("code-review", entry.Key),
                () => Assert.Equal("code-review.instructions.md", entry.FileName),
                () => Assert.Equal("code-review (v1.0.0)", entry.Name),
                () => Assert.Equal("1.0.0", entry.Version),
                () => Assert.Equal("Apply when reviewing code.", entry.Description),
                () => Assert.Equal("**/*.cs", entry.ApplyTo),
                () => Assert.True(entry.HasChangelog),
                () => Assert.Equal("sha256:xyz", entry.ContentHash));
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
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry());

            // Act
            var sections = _sut.Build(manifest, corpus).Instructions.Single().Sections;

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
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry());

            // Act
            var sections = _sut.Build(manifest, corpus).Instructions.Single().Sections;

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
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(
                    key: "lang-dotnet", fileName: "lang-dotnet.instructions.md", name: "lang-dotnet (v1.0.0)", applyTo: "**/*.{vb,cs,fs}"));

            // Act
            var entry = _sut.Build(manifest, corpus).Instructions.Single();

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
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry());

            // Act
            var entry = _sut.Build(manifest, corpus).Instructions.Single();

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
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(
                    key: "global", fileName: "global.instructions.md", name: "global (v1.0.0)", applyTo: "**/*"));

            // Act
            var entry = _sut.Build(manifest, corpus).Instructions.Single();

            // Assert
            Assert.Empty(entry.Extensions!);
        }

        [Fact]
        public void Should_preserve_manifest_entry_order()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "alpha.instructions.md", "alpha (v1.0.0)", "Alpha.");
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "beta.instructions.md", "beta (v1.0.0)", "Beta.");
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(key: "alpha", fileName: "alpha.instructions.md", name: "alpha (v1.0.0)"),
                InstructionsManifestFakeData.CreateEntry(key: "beta", fileName: "beta.instructions.md", name: "beta (v1.0.0)"));

            // Act
            var keys = _sut.Build(manifest, corpus).Instructions.Select(static entry => entry.Key);

            // Assert
            var expectedKeys = new[] { "alpha", "beta" };
            Assert.Equal(expectedKeys, keys);
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
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry());

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Build(manifest, corpus));

            // Assert
            Assert.Multiple(
                () => Assert.Contains("code-review.instructions.md", exception.Message, StringComparison.Ordinal),
                () => Assert.Contains("duplicate section anchor 'do'", exception.Message, StringComparison.Ordinal));
        }
    }
}
