namespace AutoContext.Engine.Core.Tests.Workspace.Context;

using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Core.Tests.Support.Workspace.Context;

public sealed partial class WorkspaceContextDetectorTests
{
    [Trait("Category", "Smoke")]
    public sealed class Watch(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(20);

        [Fact]
        public async Task Should_raise_flag_when_a_triggering_file_is_created()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            using var sut = WorkspaceContextDetectorTestFactory.Create(root, DebounceDelay);
            await sut.DetectAsync(TestContext.Current.CancellationToken);
            sut.Watch();

            // Act
            WorkspaceFileTestWriter.Write(root, "app.py");
            var result = await WorkspaceWatchTestPoller.WaitUntilAsync(
                sut, flags => flags.Has("hasPython"), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Has("hasPython"));
        }

        [Fact]
        public async Task Should_reclassify_only_when_a_manifest_content_change_adds_a_dependency()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "package.json", "{ }");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root, DebounceDelay);
            var seeded = await sut.DetectAsync(TestContext.Current.CancellationToken);
            sut.Watch();

            // Act
            WorkspaceFileTestWriter.Write(
                root, "package.json", """{ "dependencies": { "react": "^1" } }""");
            var result = await WorkspaceWatchTestPoller.WaitUntilAsync(
                sut, flags => flags.Has("hasReact"), TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.False(seeded.Has("hasReact")),
                () => Assert.True(result.Has("hasReact")));
        }

        [Fact]
        public async Task Should_keep_flag_until_the_last_contributor_is_removed()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "a.py");
            WorkspaceFileTestWriter.Write(root, "b.py");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root, DebounceDelay);
            await sut.DetectAsync(TestContext.Current.CancellationToken);
            sut.Watch();

            // Act: deleting one of two contributors and adding an unrelated file
            // proves the surviving sibling keeps the flag on (count-based).
            File.Delete(Path.Combine(root, "a.py"));
            WorkspaceFileTestWriter.Write(root, "marker.ts");
            var afterSiblingDelete = await WorkspaceWatchTestPoller.WaitUntilAsync(
                sut, flags => flags.Has("hasTypeScript"), TestContext.Current.CancellationToken);

            // Act: removing the last contributor flips the flag off.
            File.Delete(Path.Combine(root, "b.py"));
            var afterLastDelete = await WorkspaceWatchTestPoller.WaitUntilAsync(
                sut, flags => !flags.Has("hasPython"), TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(afterSiblingDelete.Has("hasPython")),
                () => Assert.False(afterLastDelete.Has("hasPython")));
        }
    }
}
