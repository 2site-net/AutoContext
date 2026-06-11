namespace AutoContext.Instructions.Manifest.Generator.Tests;

using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Instructions.Manifest.Generator;
using AutoContext.Instructions.Manifest.Generator.Tests.Support;

public sealed class InstructionsCatalogReaderTests
{
    public sealed class Read(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        private readonly CorpusParser _corpusParser = new();
        private readonly InstructionsCatalogReader _sut = new();

        [Fact]
        public async Task Should_reject_null_catalog_path()
        {
            // Arrange
            var corpus = await _corpusParser.ParseAsync(tempDirectory.CreateDirectory(), TestContext.Current.CancellationToken);

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => _sut.Read(null!, corpus));
        }

        [Fact]
        public void Should_reject_null_corpus()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => _sut.Read("catalog.json", null!));
        }

        [Fact]
        public async Task Should_read_a_catalog_that_reconciles_with_the_corpus()
        {
            // Arrange
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "copilot", "autocontext", "code-review");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                ["copilot.instructions.md", "autocontext.instructions.md"],
                [InstructionsManifestFakeData.CreateCategory("General")],
                InstructionsManifestFakeData.CreateCatalogEntry("Code Review", "code-review.instructions.md", ["General"]));

            // Act
            var catalog = _sut.Read(catalogPath, corpus);

            // Assert
            Assert.Single(catalog.Instructions);
        }

        [Fact]
        public async Task Should_exempt_always_attached_files_from_the_catalog_requirement()
        {
            // Arrange — copilot/autocontext ship but are declared always-attached, not cataloged.
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "copilot", "autocontext", "code-review");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                ["copilot.instructions.md", "autocontext.instructions.md"],
                [InstructionsManifestFakeData.CreateCategory("General")],
                InstructionsManifestFakeData.CreateCatalogEntry("Code Review", "code-review.instructions.md", ["General"]));

            // Act
            var exception = Record.Exception(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public async Task Should_throw_when_an_always_attached_file_is_not_in_the_corpus()
        {
            // Arrange — the always-attached array names a file the corpus does not ship.
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                ["ghost.instructions.md"],
                [InstructionsManifestFakeData.CreateCategory("General")],
                InstructionsManifestFakeData.CreateCatalogEntry("Code Review", "code-review.instructions.md", ["General"]));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Multiple(
                () => Assert.Contains("always-attached entry 'ghost.instructions.md'", exception.Message, StringComparison.Ordinal),
                () => Assert.Contains("is not in the corpus", exception.Message, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_throw_when_an_always_attached_file_is_also_cataloged()
        {
            // Arrange — a file may be curated OR always-attached, never both.
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                ["code-review.instructions.md"],
                [InstructionsManifestFakeData.CreateCategory("General")],
                InstructionsManifestFakeData.CreateCatalogEntry("Code Review", "code-review.instructions.md", ["General"]));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Contains("is declared always-attached and also cataloged", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_throw_on_duplicate_always_attached_entry()
        {
            // Arrange
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "copilot");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                ["copilot.instructions.md", "copilot.instructions.md"],
                [InstructionsManifestFakeData.CreateCategory("General")]);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Contains("duplicate always-attached entry 'copilot.instructions.md'", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_throw_when_an_always_attached_entry_is_blank()
        {
            // Arrange
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                [" "],
                [InstructionsManifestFakeData.CreateCategory("General")],
                InstructionsManifestFakeData.CreateCatalogEntry("Code Review", "code-review.instructions.md", ["General"]));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Contains("always-attached entry has a missing or blank file name", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_throw_when_an_entry_names_a_file_outside_the_corpus()
        {
            // Arrange
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                [InstructionsManifestFakeData.CreateCategory("General")],
                InstructionsManifestFakeData.CreateCatalogEntry("Missing", "missing.instructions.md", ["General"]));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Multiple(
                () => Assert.Contains("instructions-catalog.json", exception.Message, StringComparison.Ordinal),
                () => Assert.Contains("missing.instructions.md", exception.Message, StringComparison.Ordinal),
                () => Assert.Contains("is not in the corpus", exception.Message, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_throw_when_a_corpus_file_is_not_cataloged()
        {
            // Arrange — 'stray' ships, is not always-attached, and is left out of the catalog.
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review", "stray");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                [InstructionsManifestFakeData.CreateCategory("General")],
                InstructionsManifestFakeData.CreateCatalogEntry("Code Review", "code-review.instructions.md", ["General"]));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Multiple(
                () => Assert.Contains("stray.instructions.md", exception.Message, StringComparison.Ordinal),
                () => Assert.Contains("is not cataloged", exception.Message, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_throw_when_an_entry_references_an_undeclared_category()
        {
            // Arrange
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                [InstructionsManifestFakeData.CreateCategory("General")],
                InstructionsManifestFakeData.CreateCatalogEntry("Code Review", "code-review.instructions.md", ["Nonexistent"]));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Contains("undeclared category 'Nonexistent'", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_throw_on_duplicate_category()
        {
            // Arrange
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                [
                    InstructionsManifestFakeData.CreateCategory("General"),
                    InstructionsManifestFakeData.CreateCategory("General"),
                ],
                InstructionsManifestFakeData.CreateCatalogEntry("Code Review", "code-review.instructions.md", ["General"]));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Contains("duplicate category 'General'", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_throw_on_duplicate_entry_for_a_file()
        {
            // Arrange
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                [InstructionsManifestFakeData.CreateCategory("General")],
                InstructionsManifestFakeData.CreateCatalogEntry("Code Review", "code-review.instructions.md", ["General"]),
                InstructionsManifestFakeData.CreateCatalogEntry("Code Review Again", "code-review.instructions.md", ["General"]));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Contains("duplicate entry for file 'code-review.instructions.md'", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_throw_when_an_entry_declares_no_categories()
        {
            // Arrange
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.Write(
                tempDirectory.CreateDirectory(),
                [InstructionsManifestFakeData.CreateCategory("General")],
                InstructionsManifestFakeData.CreateCatalogEntry("Code Review", "code-review.instructions.md", []));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Contains("declares no categories", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_throw_on_malformed_json()
        {
            // Arrange
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.WriteRaw(tempDirectory.CreateDirectory(), "{ not json");

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Contains("not valid JSON", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_throw_when_the_categories_array_is_missing()
        {
            // Arrange — structurally valid JSON, but the top-level `categories` key is absent.
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.WriteRaw(
                tempDirectory.CreateDirectory(),
                """{ "schemaVersion": "1", "instructions": [] }""");

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Multiple(
                () => Assert.Contains("instructions-catalog.json", exception.Message, StringComparison.Ordinal),
                () => Assert.Contains("missing its `categories` array", exception.Message, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_throw_when_the_instructions_array_is_missing()
        {
            // Arrange — structurally valid JSON, but the top-level `instructions` key is absent.
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.WriteRaw(
                tempDirectory.CreateDirectory(),
                """{ "schemaVersion": "1", "alwaysAttached": [], "categories": [] }""");

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Multiple(
                () => Assert.Contains("instructions-catalog.json", exception.Message, StringComparison.Ordinal),
                () => Assert.Contains("missing its `instructions` array", exception.Message, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_throw_when_the_always_attached_array_is_missing()
        {
            // Arrange — structurally valid JSON, but the top-level `alwaysAttached` key is absent.
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.WriteRaw(
                tempDirectory.CreateDirectory(),
                """{ "schemaVersion": "1", "categories": [], "instructions": [] }""");

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Multiple(
                () => Assert.Contains("instructions-catalog.json", exception.Message, StringComparison.Ordinal),
                () => Assert.Contains("missing its `alwaysAttached` array", exception.Message, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_throw_when_an_entry_omits_its_categories_array()
        {
            // Arrange — the entry has no `categories` key at all (null rather than empty).
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var catalogPath = InstructionsCatalogTestWriter.WriteRaw(
                tempDirectory.CreateDirectory(),
                """
                {
                  "schemaVersion": "1",
                  "alwaysAttached": [],
                  "categories": [ { "name": "General", "description": "General." } ],
                  "instructions": [ { "label": "Code Review", "fileName": "code-review.instructions.md" } ]
                }
                """);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(catalogPath, corpus));

            // Assert
            Assert.Contains("declares no categories", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_throw_when_the_catalog_file_is_missing()
        {
            // Arrange
            var corpus = await InstructionsCorpusTestWriter.WriteAndParseAsync(
                tempDirectory.CreateDirectory(), "code-review");
            var missingPath = Path.Combine(tempDirectory.CreateDirectory(), "instructions-catalog.json");

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Read(missingPath, corpus));

            // Assert
            Assert.Contains("not found", exception.Message, StringComparison.Ordinal);
        }
    }
}
