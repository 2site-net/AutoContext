namespace AutoContext.Instructions.Manifest.Generator.Tests;

using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Instructions.Manifest.Generator;
using AutoContext.Instructions.Parser;

public sealed class ShippedInstructionsRoundTripTests
{
    public sealed class Build
    {
        private readonly CorpusParser _corpusParser = new();
        private readonly InstructionsManifestBuilder _sut = new();

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

        [Fact]
        public void Should_carry_section_maps_and_extensions_for_the_shipped_files()
        {
            // Act
            var manifest = _sut.Build(_corpusParser.Parse(EngineInstructionsPath.Value));

            // Assert
            Assert.Multiple(
                () => Assert.Contains(manifest.Instructions, static entry => entry.Sections.Count > 0),
                () => Assert.Contains(manifest.Instructions, static entry => entry.Extensions is { Count: > 0 }));
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

            static FrontmatterApplyToParsedMetadata? ParseApplyTo(string path)
            {
                return InstructionsFileParser.ParseFrontmatter(File.ReadAllText(path)).ApplyTo;
            }
        }
    }

    public sealed class Catalog
    {
        private readonly CorpusParser _corpusParser = new();
        private readonly InstructionsCatalogReader _sut = new();

        [Fact]
        public void Should_reconcile_the_shipped_catalog_with_the_corpus()
        {
            // Arrange
            var corpus = _corpusParser.Parse(EngineInstructionsPath.Value);
            var catalogPath = Path.Combine(
                Path.GetDirectoryName(EngineInstructionsPath.Value)!,
                "Resources",
                "instructions-catalog.json");

            // Act
            var exception = Record.Exception(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Null(exception);
        }
    }
}
