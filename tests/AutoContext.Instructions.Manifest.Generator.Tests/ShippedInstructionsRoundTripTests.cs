namespace AutoContext.Instructions.Manifest.Generator.Tests;

using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Instructions.Manifest.Generator;
using AutoContext.Instructions.Parser;

public sealed class ShippedInstructionsRoundTripTests
{
    public sealed class Build
    {
        private readonly CorpusParser _corpusParser = new();
        private readonly InstructionsListBuilder _sut = new();

        [Fact]
        public void Should_build_one_entry_for_every_shipped_file()
        {
            // Arrange
            var instructionsPath = EngineInstructionsPath.Value;
            var expectedCount = Directory.GetFiles(instructionsPath, "*.instructions.md").Length;

            // Act
            var manifest = _sut.Build(_corpusParser.Parse(instructionsPath));

            // Assert
            Assert.Multiple(
                () => Assert.NotEmpty(manifest.Instructions),
                () => Assert.Equal(expectedCount, manifest.Instructions.Count));
        }
    }

    public sealed class ApplyTo
    {
        [Fact]
        public void Should_round_trip_every_shipped_value_verbatim()
        {
            // Arrange
            var files = Directory.GetFiles(EngineInstructionsPath.Value, "*.instructions.md");

            // Act
            var nonRoundTripping = files
                .Where(static path => ParseApplyTo(path) is { RoundTrips: false })
                .Select(Path.GetFileName)
                .ToList();

            // Assert
            Assert.Multiple(
                () => Assert.NotEmpty(files),
                () => Assert.Empty(nonRoundTripping));

            static FrontmatterApplyToParsedResult? ParseApplyTo(string path)
            {
                return InstructionsFileParser.ParseFrontmatter(File.ReadAllText(path)).ApplyTo;
            }
        }
    }

    public sealed class BuildMetadata
    {
        private readonly CorpusParser _corpusParser = new();
        private readonly InstructionsListBuilder _listBuilder = new();
        private readonly InstructionsMetadataBuilder _sut = new();

        [Fact]
        public void Should_enrich_the_shipped_instructions_without_faults()
        {
            // Arrange
            var corpus = _corpusParser.Parse(EngineInstructionsPath.Value);
            var manifest = _listBuilder.Build(corpus);

            // Act
            var exception = Record.Exception(() => _sut.Build(manifest, corpus));

            // Assert
            Assert.Null(exception);
        }
    }

    public sealed class WireInternalSplit
    {
        private readonly CorpusParser _corpusParser = new();
        private readonly InstructionsListBuilder _listBuilder = new();
        private readonly InstructionsMetadataBuilder _metadataBuilder = new();
        private readonly InstructionsManifestSerializer _manifestSerializer = new();
        private readonly InstructionsMetadataSerializer _metadataSerializer = new();

        [Fact]
        public void Should_keep_section_maps_and_extensions_off_the_wire_manifest()
        {
            // Arrange
            var manifest = _listBuilder.Build(_corpusParser.Parse(EngineInstructionsPath.Value));

            // Act
            var wireJson = _manifestSerializer.Serialize(manifest);

            // Assert
            Assert.Multiple(
                () => Assert.DoesNotContain("\"sections\"", wireJson, StringComparison.Ordinal),
                () => Assert.DoesNotContain("\"extensions\"", wireJson, StringComparison.Ordinal));
        }

        [Fact]
        public void Should_carry_section_maps_and_extensions_in_the_metadata_index()
        {
            // Arrange
            var corpus = _corpusParser.Parse(EngineInstructionsPath.Value);
            var metadata = _metadataBuilder.Build(_listBuilder.Build(corpus), corpus);

            // Act
            var metadataJson = _metadataSerializer.Serialize(metadata);

            // Assert
            Assert.Multiple(
                () => Assert.Contains("\"sections\"", metadataJson, StringComparison.Ordinal),
                () => Assert.Contains("\"extensions\"", metadataJson, StringComparison.Ordinal));
        }
    }
}
