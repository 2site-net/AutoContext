namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Tests.Support.IO;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class InstructionsFullTextSearchServiceTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_manifest_accessor()
            => Assert.Throws<ArgumentNullException>(
                () => new InstructionsFullTextSearchService(
                    null!,
                    new InstructionsBodyProjector(
                        "dir",
                        new FakeInstructionsOverridesAccessor(),
                        new FakeConfigSnapshotAccessor()),
                    new FakeConfigSnapshotAccessor(),
                    NullLogger<InstructionsFullTextSearchService>.Instance));

        [Fact]
        public void Should_reject_null_body_projector()
            => Assert.Throws<ArgumentNullException>(
                () => new InstructionsFullTextSearchService(
                    new FakeInstructionsManifestAccessor(),
                    null!,
                    new FakeConfigSnapshotAccessor(),
                    NullLogger<InstructionsFullTextSearchService>.Instance));

        [Fact]
        public void Should_reject_null_config_accessor()
            => Assert.Throws<ArgumentNullException>(
                () => new InstructionsFullTextSearchService(
                    new FakeInstructionsManifestAccessor(),
                    new InstructionsBodyProjector(
                        "dir",
                        new FakeInstructionsOverridesAccessor(),
                        new FakeConfigSnapshotAccessor()),
                    null!,
                    NullLogger<InstructionsFullTextSearchService>.Instance));

        [Fact]
        public void Should_reject_null_logger()
            => Assert.Throws<ArgumentNullException>(
                () => new InstructionsFullTextSearchService(
                    new FakeInstructionsManifestAccessor(),
                    new InstructionsBodyProjector(
                        "dir",
                        new FakeInstructionsOverridesAccessor(),
                        new FakeConfigSnapshotAccessor()),
                    new FakeConfigSnapshotAccessor(),
                    null!));
    }

    public sealed class SearchAsync(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_reject_null_query()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("testing"));

            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => service.SearchAsync(
                    null!,
                    limit: null,
                    includeDisabled: false,
                    TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("!!!")]
        public async Task Should_return_empty_when_query_has_no_usable_tokens(string query)
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("testing"));

            // Act
            var results = await service.SearchAsync(
                query,
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task Should_match_file_when_body_contains_query_token()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("testing"));

            // Act
            var results = await service.SearchAsync(
                "intro",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            var hit = Assert.Single(results);
            Assert.Multiple(
                () => Assert.Equal("testing", hit.Key),
                () => Assert.Equal("testing.instructions.md", hit.FileName),
                () => Assert.True(hit.Score > 0));
        }

        [Fact]
        public async Task Should_match_only_when_all_query_tokens_present()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("testing"));

            // Act
            var results = await service.SearchAsync(
                "alpha beta",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(results);
        }

        [Fact]
        public async Task Should_not_match_when_any_query_token_is_absent()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("testing"));

            // Act
            var results = await service.SearchAsync(
                "alpha nonexistenttoken",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task Should_match_against_the_manifest_description()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("testing", description: "Pineapple guidance."));

            // Act
            var results = await service.SearchAsync(
                "pineapple",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            var hit = Assert.Single(results);
            Assert.Multiple(
                () => Assert.Equal("testing", hit.Key),
                () => Assert.Empty(hit.Excerpts));
        }

        [Fact]
        public async Task Should_rank_description_matches_above_body_only_matches()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "aaa.instructions.md", InstructionsBodyTestFiles.Body);
            InstructionsBodyTestFiles.Write(directory, "bbb.instructions.md", InstructionsBodyTestFiles.Body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("aaa", description: "alpha gateway."),
                InstructionsFileManifestEntryTestFactory.Create("bbb", description: "unrelated note."));

            // Act
            var results = await service.SearchAsync(
                "alpha",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, results.Count),
                () => Assert.Equal("aaa", results[0].Key),
                () => Assert.Equal("bbb", results[1].Key),
                () => Assert.True(results[0].Score > results[1].Score));
        }

        [Fact]
        public async Task Should_break_score_ties_by_key_ascending()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "aaa.instructions.md", InstructionsBodyTestFiles.Body);
            InstructionsBodyTestFiles.Write(directory, "zzz.instructions.md", InstructionsBodyTestFiles.Body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("zzz"),
                InstructionsFileManifestEntryTestFactory.Create("aaa"));

            // Act
            var results = await service.SearchAsync(
                "alpha",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, results.Count),
                () => Assert.Equal(results[0].Score, results[1].Score),
                () => Assert.Equal("aaa", results[0].Key),
                () => Assert.Equal("zzz", results[1].Key));
        }

        [Fact]
        public async Task Should_match_spaced_query_against_compound_identifier_text()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            const string body =
                """
                # Title

                ## ConfigureAwait

                Body text.
                """;
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("testing"));

            // Act
            var results = await service.SearchAsync(
                "configure await",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(results);
        }

        [Fact]
        public async Task Should_exclude_whole_file_disabled_entries_by_default()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            var config = new FakeConfigSnapshotAccessor
            {
                Current = new ConfigSnapshot
                {
                    Instructions = [new ConfigInstructionsFile { Name = "testing", Disabled = true }],
                },
            };
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                config,
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("testing"));

            // Act
            var results = await service.SearchAsync(
                "alpha",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task Should_include_whole_file_disabled_entries_when_requested()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            var config = new FakeConfigSnapshotAccessor
            {
                Current = new ConfigSnapshot
                {
                    Instructions = [new ConfigInstructionsFile { Name = "testing", Disabled = true }],
                },
            };
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                config,
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("testing"));

            // Act
            var results = await service.SearchAsync(
                "alpha",
                limit: null,
                includeDisabled: true,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(results);
        }

        [Fact]
        public async Task Should_carry_the_section_anchor_on_each_excerpt()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            const string body =
                """
                # Title

                ## Alpha

                Alpha content.

                ## Beta

                Zebra marker here.
                """;
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("testing"));

            // Act
            var results = await service.SearchAsync(
                "zebra",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            var hit = Assert.Single(results);
            var excerpt = Assert.Single(hit.Excerpts);
            Assert.Multiple(
                () => Assert.Equal("beta", excerpt.Anchor),
                () => Assert.Contains("Zebra marker here.", excerpt.Snippet, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_limit_results_to_the_requested_limit()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "a1.instructions.md", InstructionsBodyTestFiles.Body);
            InstructionsBodyTestFiles.Write(directory, "a2.instructions.md", InstructionsBodyTestFiles.Body);
            InstructionsBodyTestFiles.Write(directory, "a3.instructions.md", InstructionsBodyTestFiles.Body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("a1"),
                InstructionsFileManifestEntryTestFactory.Create("a2"),
                InstructionsFileManifestEntryTestFactory.Create("a3"));

            // Act
            var results = await service.SearchAsync(
                "alpha",
                limit: 1,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(results);
        }

        [Fact]
        public async Task Should_apply_the_default_limit_when_the_requested_limit_is_not_positive()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "a1.instructions.md", InstructionsBodyTestFiles.Body);
            InstructionsBodyTestFiles.Write(directory, "a2.instructions.md", InstructionsBodyTestFiles.Body);
            InstructionsBodyTestFiles.Write(directory, "a3.instructions.md", InstructionsBodyTestFiles.Body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("a1"),
                InstructionsFileManifestEntryTestFactory.Create("a2"),
                InstructionsFileManifestEntryTestFactory.Create("a3"));

            // Act
            var results = await service.SearchAsync(
                "alpha",
                limit: 0,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(3, results.Count);
        }

        [Fact]
        public async Task Should_skip_files_whose_projection_fails_without_failing_the_search()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "present.instructions.md", InstructionsBodyTestFiles.Body);
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                new FakeConfigSnapshotAccessor(),
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("present"),
                InstructionsFileManifestEntryTestFactory.Create("missing"));

            // Act
            var results = await service.SearchAsync(
                "alpha",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            var hit = Assert.Single(results);
            Assert.Equal("present", hit.Key);
        }

        [Fact]
        public async Task Should_rebuild_the_index_after_invalidate()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
            var config = new FakeConfigSnapshotAccessor();
            using var service = InstructionsFullTextSearchServiceTestFactory.Create(
                directory,
                config,
                new FakeInstructionsOverridesAccessor(),
                InstructionsFileManifestEntryTestFactory.Create("testing"));

            var beforeDisable = await service.SearchAsync(
                "bad",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            config.Current = new ConfigSnapshot
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
            };

            var beforeInvalidate = await service.SearchAsync(
                "bad",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Act
            service.Invalidate();
            var afterInvalidate = await service.SearchAsync(
                "bad",
                limit: null,
                includeDisabled: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Single(beforeDisable),
                () => Assert.Single(beforeInvalidate),
                () => Assert.Empty(afterInvalidate));
        }
    }
}
