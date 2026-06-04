namespace AutoContext.Build.Tasks.Tests;

using AutoContext.Build.Tasks;
using AutoContext.Build.Tasks.Tests.Support;
using AutoContext.Engine.Tests.Support.IO;

public sealed class InstructionsListBuilderTests
{
    public sealed class Build(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_reject_null_corpus_directory()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => InstructionsListBuilder.Build(null!));
        }

        [Fact]
        public void Should_build_one_entry_per_corpus_file()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "alpha.instructions.md", "alpha (v1.0.0)", "Alpha.");
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "beta.instructions.md", "beta (v2.3.4)", "Beta.");

            // Act
            var manifest = InstructionsListBuilder.Build(corpus);

            // Assert
            Assert.Equal(2, manifest.Instructions.Count);
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
            var manifest = InstructionsListBuilder.Build(corpus);

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
            var entry = InstructionsListBuilder.Build(corpus).Instructions.Single();

            // Assert
            Assert.Multiple(
                () => Assert.Equal("code-review", entry.Key),
                () => Assert.Equal("3.1.4", entry.Version),
                () => Assert.Equal("code-review.instructions.md", entry.FileName),
                () => Assert.Equal("Review.", entry.Description));
        }

        [Fact]
        public void Should_flag_always_attached_files()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "copilot.instructions.md", "copilot (v1.0.0)", "Always.");
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "code-review.instructions.md", "code-review (v1.0.0)", "Review.");

            // Act
            var manifest = InstructionsListBuilder.Build(corpus);

            // Assert
            Assert.Multiple(
                () => Assert.True(manifest.Instructions.Single(static e => e.Key == "copilot").AlwaysAttached),
                () => Assert.False(manifest.Instructions.Single(static e => e.Key == "code-review").AlwaysAttached));
        }

        [Fact]
        public void Should_carry_verbatim_apply_to()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "lang-csharp.instructions.md", "lang-csharp (v1.0.0)", "C#.", applyTo: "**/*.cs");

            // Act
            var entry = InstructionsListBuilder.Build(corpus).Instructions.Single();

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
            var entry = InstructionsListBuilder.Build(corpus).Instructions.Single();

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
            var manifest = InstructionsListBuilder.Build(corpus);

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
            var entry = InstructionsListBuilder.Build(corpus).Instructions.Single();

            // Assert
            Assert.Multiple(
                () => Assert.StartsWith("sha256:", entry.ContentHash, StringComparison.Ordinal),
                () => Assert.Equal(71, entry.ContentHash.Length));
        }

        [Fact]
        public void Should_throw_when_name_is_missing()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            File.WriteAllText(Path.Combine(corpus, "broken.instructions.md"), "---\ndescription: \"No name.\"\n---\nBody.\n");

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => InstructionsListBuilder.Build(corpus));

            // Assert
            Assert.Contains("broken.instructions.md", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_throw_when_key_does_not_match_file_name()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(corpus, "code-review.instructions.md", "mismatch (v1.0.0)", "Review.");

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => InstructionsListBuilder.Build(corpus));

            // Assert
            Assert.Contains("does not equal file basename", exception.Message, StringComparison.Ordinal);
        }
    }
}
