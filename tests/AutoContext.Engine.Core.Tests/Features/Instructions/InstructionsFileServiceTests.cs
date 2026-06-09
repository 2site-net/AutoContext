namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Tests.Support.IO;

public sealed class InstructionsFileServiceTests
{
    public sealed class Constructor
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_reject_blank_instructions_directory(string directory)
            => Assert.Throws<ArgumentException>(
                () => new InstructionsFileService(
                    directory,
                    new FakeInstructionsOverridesAccessor(),
                    new FakeConfigSnapshotAccessor()));

        [Fact]
        public void Should_reject_null_override_accessor()
            => Assert.Throws<ArgumentNullException>(
                () => new InstructionsFileService(
                    "dir",
                    null!,
                    new FakeConfigSnapshotAccessor()));

        [Fact]
        public void Should_reject_null_config_accessor()
            => Assert.Throws<ArgumentNullException>(
                () => new InstructionsFileService(
                    "dir",
                    new FakeInstructionsOverridesAccessor(),
                    null!));
    }

    public sealed class GetBodyProjectionAsync(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_strip_frontmatter_and_return_all_sections_when_not_sliced()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            var service = new InstructionsFileService(
                directory,
                new FakeInstructionsOverridesAccessor(),
                new FakeConfigSnapshotAccessor());

            // Act
            var projection = await service.GetBodyProjectionAsync(
                InstructionsManifestFileTestFactory.Create("testing"),
                requestedSectionAnchors: null,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.DoesNotContain("name:", projection.Content, StringComparison.Ordinal),
                () => Assert.DoesNotContain("description:", projection.Content, StringComparison.Ordinal),
                () => Assert.Contains("# Title", projection.Content, StringComparison.Ordinal),
                () => Assert.Contains("[INST0001]", projection.Content, StringComparison.Ordinal),
                () => Assert.Contains("[INST0002]", projection.Content, StringComparison.Ordinal),
                () => Assert.Equal(["alpha", "beta"], projection.ReturnedSections),
                () => Assert.Empty(projection.NotFoundSections));
        }

        [Fact]
        public async Task Should_remove_disabled_rule_lines_but_preserve_surviving_tags()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            var config = new FakeConfigSnapshotAccessor
            {
                Current = new ConfigSnapshot
                {
                    Instructions =
                    [
                        new ConfigInstructionsFile
                        {
                            Name = "testing",
                            Rules =
                            [
                                new ConfigInstructionsFile.InstructionsRule
                                {
                                    Disabled = true,
                                    Id = "INST0002",
                                },
                            ],
                        },
                    ],
                },
            };
            var service = new InstructionsFileService(
                directory,
                new FakeInstructionsOverridesAccessor(),
                config);

            // Act
            var projection = await service.GetBodyProjectionAsync(
                InstructionsManifestFileTestFactory.Create("testing"),
                requestedSectionAnchors: null,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.DoesNotContain("[INST0002]", projection.Content, StringComparison.Ordinal),
                () => Assert.DoesNotContain("do the bad thing", projection.Content, StringComparison.Ordinal),
                () => Assert.Contains("[INST0001]", projection.Content, StringComparison.Ordinal),
                () => Assert.Contains("keep this rule", projection.Content, StringComparison.Ordinal),
                () => Assert.Contains("Beta body line.", projection.Content, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_prefer_override_body_over_bundled()
        {
            // Arrange
            var bundledDirectory = tempDirectory.CreateDirectory();
            var overrideDirectory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(bundledDirectory, "testing.instructions.md", "# Bundled\n\nBUNDLED marker.\n");
            var overridePath = InstructionsBodyTestFiles.Write(
                overrideDirectory,
                "testing.instructions.md",
                "# Override\n\nOVERRIDE marker.\n");
            var overrides = new InstructionsOverridesSnapshot(
                new Dictionary<string, string> { ["testing.instructions.md"] = overridePath });
            var service = new InstructionsFileService(
                bundledDirectory,
                new FakeInstructionsOverridesAccessor(overrides),
                new FakeConfigSnapshotAccessor());

            // Act
            var projection = await service.GetBodyProjectionAsync(
                InstructionsManifestFileTestFactory.Create("testing"),
                requestedSectionAnchors: null,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Contains("OVERRIDE marker.", projection.Content, StringComparison.Ordinal),
                () => Assert.DoesNotContain("BUNDLED marker.", projection.Content, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_slice_to_requested_section()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            var service = new InstructionsFileService(
                directory,
                new FakeInstructionsOverridesAccessor(),
                new FakeConfigSnapshotAccessor());

            // Act
            var projection = await service.GetBodyProjectionAsync(
                InstructionsManifestFileTestFactory.Create("testing"),
                requestedSectionAnchors: ["alpha"],
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Contains("## Alpha", projection.Content, StringComparison.Ordinal),
                () => Assert.Contains("Alpha body line.", projection.Content, StringComparison.Ordinal),
                () => Assert.Contains("[INST0001]", projection.Content, StringComparison.Ordinal),
                () => Assert.DoesNotContain("# Title", projection.Content, StringComparison.Ordinal),
                () => Assert.DoesNotContain("## Beta", projection.Content, StringComparison.Ordinal),
                () => Assert.DoesNotContain("Beta body line.", projection.Content, StringComparison.Ordinal),
                () => Assert.Equal(["alpha"], projection.ReturnedSections),
                () => Assert.Empty(projection.NotFoundSections));
        }

        [Fact]
        public async Task Should_preserve_trailing_newline_when_slicing_last_section()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            var body =
                """
                ---
                name: "testing (v1.0.0)"
                description: "Test file."
                ---
                # Title

                ## Alpha

                Alpha body line.

                ## Beta

                Beta body line.

                """;
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", body);
            var service = new InstructionsFileService(
                directory,
                new FakeInstructionsOverridesAccessor(),
                new FakeConfigSnapshotAccessor());

            // Act
            var projection = await service.GetBodyProjectionAsync(
                InstructionsManifestFileTestFactory.Create("testing"),
                requestedSectionAnchors: ["beta"],
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("## Beta\n\nBeta body line.\n", projection.Content);
        }

        [Fact]
        public async Task Should_report_unresolved_requested_sections_as_not_found()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            var service = new InstructionsFileService(
                directory,
                new FakeInstructionsOverridesAccessor(),
                new FakeConfigSnapshotAccessor());

            // Act
            var projection = await service.GetBodyProjectionAsync(
                InstructionsManifestFileTestFactory.Create("testing"),
                requestedSectionAnchors: ["beta", "ghost"],
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Contains("## Beta", projection.Content, StringComparison.Ordinal),
                () => Assert.Contains("Beta body line.", projection.Content, StringComparison.Ordinal),
                () => Assert.DoesNotContain("## Alpha", projection.Content, StringComparison.Ordinal),
                () => Assert.Equal(["beta"], projection.ReturnedSections),
                () => Assert.Equal(["ghost"], projection.NotFoundSections));
        }
    }
}
