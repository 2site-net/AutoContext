namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Tests.Support.IO;

public sealed class InstructionsManifestLoaderTests
{
    public sealed class LoadAsync(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_merge_manifest_and_catalog_in_document_order()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteValid(directory);

            // Act
            var snapshot = await InstructionsManifestLoader.LoadAsync(
                directory, TestContext.Current.CancellationToken);

            // Assert
            Assert.Collection(
                snapshot.Files,
                first => Assert.Equal("autocontext", first.Key),
                second => Assert.Equal("docker", second.Key));
        }

        [Fact]
        public async Task Should_derive_always_attached_from_catalog()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteValid(directory);

            // Act
            var snapshot = await InstructionsManifestLoader.LoadAsync(
                directory, TestContext.Current.CancellationToken);

            // Assert — the always-attached file carries no curatorial data.
            var file = snapshot.FindByFileName("autocontext.instructions.md");

            Assert.NotNull(file);
            Assert.Multiple(
                () => Assert.Equal("autocontext", file.Key),
                () => Assert.Equal("autocontext (v1.0.0)", file.Name),
                () => Assert.Equal("1.0.0", file.Version),
                () => Assert.Equal("Always attached.", file.Description),
                () => Assert.Equal("sha256:aaa", file.ContentHash),
                () => Assert.True(file.AlwaysAttached),
                () => Assert.False(file.HasChangelog),
                () => Assert.Null(file.ApplyTo),
                () => Assert.Null(file.Extensions),
                () => Assert.Null(file.Label),
                () => Assert.Null(file.Category),
                () => Assert.Empty(file.ActivationFlags));
        }

        [Fact]
        public async Task Should_carry_manifest_apply_to_extensions_and_sections()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteValid(directory);

            // Act
            var snapshot = await InstructionsManifestLoader.LoadAsync(
                directory, TestContext.Current.CancellationToken);

            // Assert
            var file = snapshot.FindByFileName("docker.instructions.md");

            Assert.NotNull(file);
            Assert.Multiple(
                () => Assert.False(file.AlwaysAttached),
                () => Assert.Equal("**/Dockerfile*", file.ApplyTo),
                () => Assert.Equal(["yml", "yaml"], file.Extensions),
                () => Assert.True(file.HasChangelog));

            Assert.Collection(
                file.Sections,
                top => Assert.Multiple(
                    () => Assert.Equal("Build", top.Heading),
                    () => Assert.Equal("build", top.Anchor),
                    () => Assert.Null(top.Parent)),
                nested => Assert.Multiple(
                    () => Assert.Equal("Stages", nested.Heading),
                    () => Assert.Equal("build-stages", nested.Anchor),
                    () => Assert.Equal("Build", nested.Parent)));
        }

        [Fact]
        public async Task Should_merge_catalog_label_categories_and_activation_flags()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteValid(directory);

            // Act
            var snapshot = await InstructionsManifestLoader.LoadAsync(
                directory, TestContext.Current.CancellationToken);

            // Assert
            var file = snapshot.FindByFileName("docker.instructions.md");

            Assert.NotNull(file);
            Assert.Multiple(
                () => Assert.Equal("Docker", file.Label),
                () => Assert.Equal("Tools", file.Category),
                () => Assert.Equal(["hasDocker"], file.ActivationFlags));
        }

        [Fact]
        public async Task Should_expose_category_taxonomy()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteValid(directory);

            // Act
            var snapshot = await InstructionsManifestLoader.LoadAsync(
                directory, TestContext.Current.CancellationToken);

            // Assert
            var category = Assert.Single(snapshot.Categories);
            Assert.Multiple(
                () => Assert.Equal("Tools", category.Name),
                () => Assert.Equal(
                    "Developer tooling and platform conventions.", category.Description));
        }

        [Fact]
        public async Task Should_default_sections_to_empty_when_absent()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteValid(directory);

            // Act
            var snapshot = await InstructionsManifestLoader.LoadAsync(
                directory, TestContext.Current.CancellationToken);

            // Assert — the always-attached file declares one section.
            var file = snapshot.FindByFileName("autocontext.instructions.md");

            Assert.NotNull(file);
            var section = Assert.Single(file.Sections);
            Assert.Equal("intro", section.Anchor);
        }

        [Fact]
        public async Task Should_return_null_from_find_for_unknown_file_name()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteValid(directory);

            // Act
            var snapshot = await InstructionsManifestLoader.LoadAsync(
                directory, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(snapshot.FindByFileName("missing.instructions.md"));
        }

        [Fact]
        public async Task Should_throw_when_manifest_missing()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteCatalog(
                directory, InstructionsManifestTestFiles.CatalogJson);

            // Act + Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => InstructionsManifestLoader.LoadAsync(
                    directory, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_throw_when_catalog_missing()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteManifest(
                directory, InstructionsManifestTestFiles.ManifestJson);

            // Act + Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => InstructionsManifestLoader.LoadAsync(
                    directory, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_throw_when_manifest_is_not_valid_json()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteManifest(directory, "not json");
            InstructionsManifestTestFiles.WriteCatalog(
                directory, InstructionsManifestTestFiles.CatalogJson);

            // Act + Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => InstructionsManifestLoader.LoadAsync(
                    directory, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_throw_when_non_always_attached_file_has_no_catalog_entry()
        {
            // Arrange — docker is not always-attached and has no catalog row.
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteManifest(
                directory, InstructionsManifestTestFiles.ManifestJson);
            InstructionsManifestTestFiles.WriteCatalog(
                directory,
                """
                {
                  "schemaVersion": "1",
                  "alwaysAttached": [ "autocontext.instructions.md" ],
                  "categories": [],
                  "instructions": []
                }
                """);

            // Act + Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => InstructionsManifestLoader.LoadAsync(
                    directory, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_throw_when_manifest_row_is_missing_a_required_field()
        {
            // Arrange — the row omits fileName.
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteManifest(
                directory,
                """
                {
                  "schemaVersion": "1",
                  "instructions": [
                    {
                      "key": "broken",
                      "name": "broken (v1.0.0)",
                      "version": "1.0.0",
                      "description": "Missing fileName.",
                      "hasChangelog": false,
                      "contentHash": "sha256:ddd"
                    }
                  ]
                }
                """);
            InstructionsManifestTestFiles.WriteCatalog(
                directory,
                """
                {
                  "schemaVersion": "1",
                  "alwaysAttached": [],
                  "categories": [],
                  "instructions": []
                }
                """);

            // Act + Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => InstructionsManifestLoader.LoadAsync(
                    directory, TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_reject_blank_resources_directory(string directory)
        {
            // Act + Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => InstructionsManifestLoader.LoadAsync(
                    directory, TestContext.Current.CancellationToken));
        }
    }
}
