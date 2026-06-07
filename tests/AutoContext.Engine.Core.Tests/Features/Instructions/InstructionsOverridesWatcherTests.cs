namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Workspace.Context;
using AutoContext.Engine.Tests.Support.IO;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class InstructionsOverridesWatcherTests
{
    public sealed class Constructor
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_reject_blank_workspace_path(string workspacePath)
            => Assert.Throws<ArgumentException>(
                () => InstructionsOverridesWatcherTestFactory.Create(workspacePath));

        [Fact]
        public void Should_reject_null_time_provider()
            => Assert.Throws<ArgumentNullException>(
                () => new InstructionsOverridesWatcher(
                    "ws",
                    [".github"],
                    null!,
                    TimeSpan.FromMilliseconds(100),
                    NullLogger<InstructionsOverridesWatcher>.Instance));

        [Fact]
        public void Should_reject_null_instruction_directories()
            => Assert.Throws<ArgumentNullException>(
                () => new InstructionsOverridesWatcher(
                    "ws",
                    null!,
                    TimeProvider.System,
                    TimeSpan.FromMilliseconds(100),
                    NullLogger<InstructionsOverridesWatcher>.Instance));

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Should_reject_non_positive_debounce_delay(int milliseconds)
            => Assert.Throws<ArgumentOutOfRangeException>(
                () => new InstructionsOverridesWatcher(
                    "ws",
                    [".github"],
                    TimeProvider.System,
                    TimeSpan.FromMilliseconds(milliseconds),
                    NullLogger<InstructionsOverridesWatcher>.Instance));

        [Fact]
        public void Should_reject_null_logger()
            => Assert.Throws<ArgumentNullException>(
                () => new InstructionsOverridesWatcher(
                    "ws",
                    [".github"],
                    TimeProvider.System,
                    TimeSpan.FromMilliseconds(100),
                    null!));
    }

    public sealed class Current(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_be_empty_before_load()
        {
            // Arrange
            using var watcher = InstructionsOverridesWatcherTestFactory.Create(tempDirectory.CreateDirectory());

            // Assert
            Assert.Same(InstructionsOverridesSnapshot.Empty, watcher.Current);
        }
    }

    public sealed class LoadAsync(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_return_empty_when_directory_missing()
        {
            // Arrange
            using var watcher = InstructionsOverridesWatcherTestFactory.Create(tempDirectory.CreateDirectory());

            // Act
            var overrides = await watcher.LoadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(overrides.FileNames);
        }

        [Fact]
        public async Task Should_inventory_override_files()
        {
            // Arrange
            var workspace = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(workspace, ".github/instructions/docker.instructions.md");
            WorkspaceFileTestWriter.Write(workspace, ".github/instructions/python.instructions.md");
            using var watcher = InstructionsOverridesWatcherTestFactory.Create(workspace);

            // Act
            var overrides = await watcher.LoadAsync(TestContext.Current.CancellationToken);
            var found = overrides.TryGetPath("docker.instructions.md", out var dockerPath);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(
                    ["docker.instructions.md", "python.instructions.md"], overrides.FileNames),
                () => Assert.True(found),
                () => Assert.True(File.Exists(dockerPath)));
        }

        [Fact]
        public async Task Should_ignore_non_override_files()
        {
            // Arrange
            var workspace = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(workspace, ".github/instructions/docker.instructions.md");
            WorkspaceFileTestWriter.Write(workspace, ".github/instructions/README.md");
            using var watcher = InstructionsOverridesWatcherTestFactory.Create(workspace);

            // Act
            var overrides = await watcher.LoadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["docker.instructions.md"], overrides.FileNames);
        }

        [Fact]
        public async Task Should_publish_to_current_without_raising_changed()
        {
            // Arrange
            var workspace = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(workspace, ".github/instructions/docker.instructions.md");
            using var watcher = InstructionsOverridesWatcherTestFactory.Create(workspace);

            var raised = false;
            watcher.Changed += (_, _) => raised = true;

            // Act
            var overrides = await watcher.LoadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.False(raised),
                () => Assert.Same(overrides, watcher.Current));
        }
    }

    public sealed class Watch(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_arm_without_throwing_when_github_absent()
        {
            // Arrange
            using var watcher = InstructionsOverridesWatcherTestFactory.Create(tempDirectory.CreateDirectory());

            // Act + Assert
            watcher.Watch();
        }

        [Fact]
        public void Should_be_idempotent()
        {
            // Arrange
            var workspace = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(workspace, ".github/instructions/docker.instructions.md");
            using var watcher = InstructionsOverridesWatcherTestFactory.Create(workspace);

            // Act + Assert
            watcher.Watch();
            watcher.Watch();
        }
    }

    public sealed class RefreshAsync(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_adopt_new_override_and_raise_changed()
        {
            // Arrange
            var workspace = tempDirectory.CreateDirectory();
            using var watcher = InstructionsOverridesWatcherTestFactory.Create(workspace);
            await watcher.LoadAsync(TestContext.Current.CancellationToken);

            InstructionsOverridesSnapshot? observed = null;
            watcher.Changed += (_, overrides) => observed = overrides;

            WorkspaceFileTestWriter.Write(workspace, ".github/instructions/docker.instructions.md");

            // Act
            await watcher.RefreshAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Same(watcher.Current, observed),
                () => Assert.Equal(["docker.instructions.md"], watcher.Current.FileNames));
        }

        [Fact]
        public async Task Should_adopt_deleted_override()
        {
            // Arrange
            var workspace = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(workspace, ".github/instructions/docker.instructions.md");
            using var watcher = InstructionsOverridesWatcherTestFactory.Create(workspace);
            await watcher.LoadAsync(TestContext.Current.CancellationToken);

            File.Delete(Path.Combine(workspace, ".github", "instructions", "docker.instructions.md"));

            // Act
            await watcher.RefreshAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(watcher.Current.FileNames);
        }
    }

    public sealed class MultipleDirectories(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_merge_overrides_across_directories()
        {
            // Arrange
            var workspace = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(workspace, ".github/instructions/docker.instructions.md");
            WorkspaceFileTestWriter.Write(workspace, ".copilot/instructions/python.instructions.md");
            using var watcher = InstructionsOverridesWatcherTestFactory.Create(
                workspace, instructionsOverridesRoots: [".github", ".copilot"]);

            // Act
            var overrides = await watcher.LoadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(
                ["docker.instructions.md", "python.instructions.md"], overrides.FileNames);
        }

        [Fact]
        public async Task Should_prefer_first_directory_on_conflict()
        {
            // Arrange
            var workspace = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(workspace, ".github/instructions/docker.instructions.md");
            WorkspaceFileTestWriter.Write(workspace, ".copilot/instructions/docker.instructions.md");
            using var watcher = InstructionsOverridesWatcherTestFactory.Create(
                workspace, instructionsOverridesRoots: [".github", ".copilot"]);

            // Act
            var overrides = await watcher.LoadAsync(TestContext.Current.CancellationToken);
            var found = overrides.TryGetPath("docker.instructions.md", out var path);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(["docker.instructions.md"], overrides.FileNames),
                () => Assert.True(found),
                () => Assert.Equal(
                    Path.Combine(workspace, ".github", "instructions", "docker.instructions.md"),
                    path));
        }
    }
}
