namespace AutoContext.Instructions.Manifest.Generator.Tests;

using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Instructions.Manifest.Generator;
using AutoContext.Instructions.Manifest.Generator.Tests.Support;
using AutoContext.Instructions.Parser.Model;

public sealed class InstructionsReferenceValidatorTests
{
    public sealed class Validate(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        private readonly CorpusParser _corpusParser = new();
        private readonly InstructionsReferenceValidator _sut = new();

        [Fact]
        public void Should_reject_null_corpus()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => _sut.Validate(null!));
        }

        [Fact]
        public async Task Should_yield_no_findings_when_every_reference_resolves()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "testing.instructions.md", "testing (v1.0.0)", "Testing.", body: "- [INST0001] **Do** test.\n");
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "dotnet-testing.instructions.md", "dotnet-testing (v1.0.0)", ".NET testing.", body: "See [testing#INST0001] here.\n");

            // Act
            var findings = _sut.Validate(await _corpusParser.ParseAsync(corpus, TestContext.Current.CancellationToken));

            // Assert
            Assert.Empty(findings);
        }

        [Fact]
        public async Task Should_flag_unknown_locator()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "testing.instructions.md", "testing (v1.0.0)", "Testing.", body: "See [nosuch#INST0001] here.\n");

            // Act
            var finding = Assert.Single(_sut.Validate(await _corpusParser.ParseAsync(corpus, TestContext.Current.CancellationToken)));

            // Assert
            Assert.Multiple(
                () => Assert.Equal("testing", finding.SourceKey),
                () => Assert.Equal("testing.instructions.md", finding.SourceFileName),
                () => Assert.Equal(InstructionsFileReferenceFindingKind.UnknownLocator, finding.Failure.Kind));
        }

        [Fact]
        public async Task Should_flag_dangling_rule_reference()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "testing.instructions.md", "testing (v1.0.0)", "Testing.", body: "- [INST0001] **Do** test.\n");
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "dotnet-testing.instructions.md", "dotnet-testing (v1.0.0)", ".NET testing.", body: "See [testing#INST9999] here.\n");

            // Act
            var finding = Assert.Single(_sut.Validate(await _corpusParser.ParseAsync(corpus, TestContext.Current.CancellationToken)));

            // Assert
            Assert.Equal(InstructionsFileReferenceFindingKind.DanglingRuleReference, finding.Failure.Kind);
        }

        [Fact]
        public async Task Should_flag_redundant_self_locator()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus,
                "testing.instructions.md",
                "testing (v1.0.0)",
                "Testing.",
                body: "- [INST0001] **Do** test, see [testing#INST0001].\n");

            // Act
            var finding = Assert.Single(_sut.Validate(await _corpusParser.ParseAsync(corpus, TestContext.Current.CancellationToken)));

            // Assert
            Assert.Equal(InstructionsFileReferenceFindingKind.RedundantLocator, finding.Failure.Kind);
        }

        [Fact]
        public async Task Should_aggregate_findings_across_files_in_key_order()
        {
            // Arrange
            var corpus = tempDirectory.CreateDirectory();
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "alpha.instructions.md", "alpha (v1.0.0)", "Alpha.", body: "See [nosuch#INST0001] here.\n");
            InstructionsCorpusTestWriter.WriteInstruction(
                corpus, "beta.instructions.md", "beta (v1.0.0)", "Beta.", body: "See [missing#INST0002] here.\n");

            // Act
            var findings = _sut.Validate(await _corpusParser.ParseAsync(corpus, TestContext.Current.CancellationToken));

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, findings.Count),
                () => Assert.Equal("alpha", findings[0].SourceKey),
                () => Assert.Equal("beta", findings[1].SourceKey));
        }
    }
}
