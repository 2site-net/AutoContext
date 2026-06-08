namespace AutoContext.Instructions.Parser.Tests;

using AutoContext.Instructions.Parser.Tests.Support;

public sealed class InstructionsFileCatalogTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_a_null_entry_sequence()
        {
            // Act / Assert
            Assert.Throws<ArgumentNullException>(() => new InstructionsFileCatalog(null!));
        }

        [Fact]
        public void Should_reject_two_entries_that_share_a_key()
        {
            // Arrange
            var entries = new[]
            {
                new InstructionsFileCatalogEntry("testing", new HashSet<string>(StringComparer.Ordinal), []),
                new InstructionsFileCatalogEntry("testing", new HashSet<string>(StringComparer.Ordinal), []),
            };

            // Act / Assert
            Assert.Throws<ArgumentException>(() => new InstructionsFileCatalog(entries));
        }

        [Fact]
        public void Should_report_a_missing_key_as_absent()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([]);

            // Act
            var found = catalog.TryGet("testing", out var entry);

            // Assert
            Assert.Multiple(
                () => Assert.False(found),
                () => Assert.Null(entry));
        }
    }

    public sealed class FromParsedCorpus
    {
        [Fact]
        public void Should_reject_a_null_map()
        {
            // Act / Assert
            Assert.Throws<ArgumentNullException>(() => InstructionsFileCatalog.FromParsedCorpus(null!));
        }

        [Fact]
        public void Should_project_tagged_rule_ids_into_the_entry()
        {
            // Arrange
            var parsed = InstructionsFileSpanStream.Parse("- [INST0001] **Do** one.\n- [INST0002] **Don't** two.\n");
            var parsedByKey = new Dictionary<string, InstructionsFileParsedContent>(StringComparer.Ordinal)
            {
                ["testing"] = parsed,
            };

            // Act
            var catalog = InstructionsFileCatalog.FromParsedCorpus(parsedByKey);

            // Assert
            Assert.True(catalog.TryGet("testing", out var entry));
            Assert.Multiple(
                () => Assert.Equal("testing", entry!.Key),
                () => Assert.Contains("INST0001", entry!.RuleIds),
                () => Assert.Contains("INST0002", entry!.RuleIds));
        }

        [Fact]
        public void Should_omit_untagged_rules_from_the_entry()
        {
            // Arrange
            var parsed = InstructionsFileSpanStream.Parse("- [INST0001] **Do** one.\n- **Do** untagged.\n");
            var parsedByKey = new Dictionary<string, InstructionsFileParsedContent>(StringComparer.Ordinal)
            {
                ["testing"] = parsed,
            };

            // Act
            var catalog = InstructionsFileCatalog.FromParsedCorpus(parsedByKey);

            // Assert
            Assert.True(catalog.TryGet("testing", out var entry));
            Assert.Single(entry!.RuleIds);
        }

        [Fact]
        public void Should_project_the_section_index_into_the_entry()
        {
            // Arrange
            var parsed = InstructionsFileSpanStream.Parse("## Assertions\n\n## Test Support\n");
            var parsedByKey = new Dictionary<string, InstructionsFileParsedContent>(StringComparer.Ordinal)
            {
                ["testing"] = parsed,
            };

            // Act
            var catalog = InstructionsFileCatalog.FromParsedCorpus(parsedByKey);

            // Assert
            Assert.True(catalog.TryGet("testing", out var entry));
            Assert.Equal(["assertions", "test-support"], entry!.Sections.Select(section => section.Anchor));
        }
    }
}
