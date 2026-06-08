namespace AutoContext.Instructions.Parser.Tests;

using AutoContext.Instructions.Parser.Tests.Support;

public sealed class InstructionsFileReferenceResolverTests
{
    public sealed class Resolve
    {
        [Fact]
        public void Should_reject_null_arguments()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([]);

            // Act / Assert
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(
                    () => InstructionsFileReferenceResolver.Resolve(null!, [], catalog)),
                () => Assert.Throws<ArgumentNullException>(
                    () => InstructionsFileReferenceResolver.Resolve("testing", null!, catalog)),
                () => Assert.Throws<ArgumentNullException>(
                    () => InstructionsFileReferenceResolver.Resolve("testing", [], null!)));
        }

        [Fact]
        public void Should_resolve_a_cross_file_rule_reference()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([
                new InstructionsFileCatalogEntry(
                    "testing",
                    new HashSet<string>(StringComparer.Ordinal) { "INST0014" },
                    []),
            ]);
            var references = InstructionsFileSpanStream.Parse("see [testing#INST0014].\n").Body.References;

            // Act
            var findings = InstructionsFileReferenceResolver.Resolve("dotnet-testing", references, catalog);

            // Assert
            Assert.Empty(findings);
        }

        [Fact]
        public void Should_resolve_a_same_file_rule_reference_against_the_source_key()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([
                new InstructionsFileCatalogEntry(
                    "testing",
                    new HashSet<string>(StringComparer.Ordinal) { "INST0017" },
                    []),
            ]);
            var references = InstructionsFileSpanStream.Parse("see [#INST0017].\n").Body.References;

            // Act
            var findings = InstructionsFileReferenceResolver.Resolve("testing", references, catalog);

            // Assert
            Assert.Empty(findings);
        }

        [Fact]
        public void Should_flag_a_rule_reference_whose_target_is_undefined()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([
                new InstructionsFileCatalogEntry(
                    "testing",
                    new HashSet<string>(StringComparer.Ordinal) { "INST0014" },
                    []),
            ]);
            var references = InstructionsFileSpanStream.Parse("see [testing#INST9999].\n").Body.References;

            // Act
            var finding = Assert.Single(
                InstructionsFileReferenceResolver.Resolve("dotnet-testing", references, catalog));

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileReferenceFindingKind.DanglingRuleReference, finding.Kind),
                () => Assert.Equal("INST9999", finding.Reference.Address.Target));
        }

        [Fact]
        public void Should_flag_a_locator_that_matches_no_known_file()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([]);
            var references = InstructionsFileSpanStream.Parse("see [nosuch#INST0001].\n").Body.References;

            // Act
            var finding = Assert.Single(
                InstructionsFileReferenceResolver.Resolve("testing", references, catalog));

            // Assert
            Assert.Equal(InstructionsFileReferenceFindingKind.UnknownLocator, finding.Kind);
        }

        [Fact]
        public void Should_resolve_a_section_reference_by_anchor()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([
                new InstructionsFileCatalogEntry(
                    "testing",
                    new HashSet<string>(StringComparer.Ordinal),
                    InstructionsFileSpanStream.Parse("## Test Support\n").Body.Sections),
            ]);
            var references = InstructionsFileSpanStream.Parse("see [testing#'Test Support'].\n").Body.References;

            // Act
            var findings = InstructionsFileReferenceResolver.Resolve("dotnet-testing", references, catalog);

            // Assert
            Assert.Empty(findings);
        }

        [Fact]
        public void Should_resolve_a_section_reference_by_exact_heading_when_the_anchor_is_parent_qualified()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([
                new InstructionsFileCatalogEntry(
                    "testing",
                    new HashSet<string>(StringComparer.Ordinal),
                    InstructionsFileSpanStream.Parse("## General\n\n### Layout\n").Body.Sections),
            ]);
            var references = InstructionsFileSpanStream.Parse("see [testing#'Layout'].\n").Body.References;

            // Act
            var findings = InstructionsFileReferenceResolver.Resolve("dotnet-testing", references, catalog);

            // Assert
            Assert.Empty(findings);
        }

        [Fact]
        public void Should_resolve_a_section_reference_by_slug_ignoring_heading_case()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([
                new InstructionsFileCatalogEntry(
                    "testing",
                    new HashSet<string>(StringComparer.Ordinal),
                    InstructionsFileSpanStream.Parse("## Assertions\n").Body.Sections),
            ]);
            var references = InstructionsFileSpanStream.Parse("see [testing#'assertions'].\n").Body.References;

            // Act
            var findings = InstructionsFileReferenceResolver.Resolve("dotnet-testing", references, catalog);

            // Assert
            Assert.Empty(findings);
        }

        [Fact]
        public void Should_flag_a_section_reference_whose_heading_is_undefined()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([
                new InstructionsFileCatalogEntry(
                    "testing",
                    new HashSet<string>(StringComparer.Ordinal),
                    InstructionsFileSpanStream.Parse("## Assertions\n").Body.Sections),
            ]);
            var references = InstructionsFileSpanStream.Parse("see [testing#'No Such Section'].\n").Body.References;

            // Act
            var finding = Assert.Single(
                InstructionsFileReferenceResolver.Resolve("dotnet-testing", references, catalog));

            // Assert
            Assert.Multiple(
                () => Assert.Equal(InstructionsFileReferenceFindingKind.UnresolvedSectionReference, finding.Kind),
                () => Assert.Equal("No Such Section", finding.Reference.Address.Target));
        }

        [Fact]
        public void Should_flag_an_explicit_locator_that_names_its_own_file_as_redundant()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([
                new InstructionsFileCatalogEntry(
                    "testing",
                    new HashSet<string>(StringComparer.Ordinal) { "INST0014" },
                    []),
            ]);
            var references = InstructionsFileSpanStream.Parse("see [testing#INST0014].\n").Body.References;

            // Act
            var finding = Assert.Single(
                InstructionsFileReferenceResolver.Resolve("testing", references, catalog));

            // Assert
            Assert.Equal(InstructionsFileReferenceFindingKind.RedundantLocator, finding.Kind);
        }

        [Fact]
        public void Should_flag_a_redundant_self_locator_that_is_also_dangling()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([
                new InstructionsFileCatalogEntry(
                    "testing",
                    new HashSet<string>(StringComparer.Ordinal) { "INST0014" },
                    []),
            ]);
            var references = InstructionsFileSpanStream.Parse("see [testing#INST9999].\n").Body.References;

            // Act
            var findings = InstructionsFileReferenceResolver.Resolve("testing", references, catalog);

            // Assert
            Assert.Equal(
                [
                    InstructionsFileReferenceFindingKind.RedundantLocator,
                    InstructionsFileReferenceFindingKind.DanglingRuleReference,
                ],
                findings.Select(finding => finding.Kind));
        }

        [Fact]
        public void Should_skip_a_uri_locator_without_resolving_it()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([]);
            var references = InstructionsFileSpanStream
                .Parse("see [https://example.com/testing.instructions.md#INST0001].\n")
                .Body.References;

            // Act
            var findings = InstructionsFileReferenceResolver.Resolve("testing", references, catalog);

            // Assert
            Assert.Empty(findings);
        }

        [Fact]
        public void Should_normalize_a_filename_locator_to_a_key_before_resolving()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([
                new InstructionsFileCatalogEntry(
                    "testing",
                    new HashSet<string>(StringComparer.Ordinal) { "INST0014" },
                    []),
            ]);
            var references = InstructionsFileSpanStream
                .Parse("see [testing.instructions.md#INST0014].\n")
                .Body.References;

            // Act
            var findings = InstructionsFileReferenceResolver.Resolve("dotnet-testing", references, catalog);

            // Assert
            Assert.Empty(findings);
        }

        [Fact]
        public void Should_report_findings_for_each_failing_reference_in_input_order()
        {
            // Arrange
            var catalog = new InstructionsFileCatalog([
                new InstructionsFileCatalogEntry(
                    "testing",
                    new HashSet<string>(StringComparer.Ordinal) { "INST0014" },
                    []),
            ]);
            var references = InstructionsFileSpanStream
                .Parse("first [testing#INST9999] then [nosuch#INST0001] then [testing#INST0014].\n")
                .Body.References;

            // Act
            var findings = InstructionsFileReferenceResolver.Resolve("dotnet-testing", references, catalog);

            // Assert
            Assert.Equal(
                [
                    InstructionsFileReferenceFindingKind.DanglingRuleReference,
                    InstructionsFileReferenceFindingKind.UnknownLocator,
                ],
                findings.Select(finding => finding.Kind));
        }
    }
}
