namespace AutoContext.Engine.Core.Tests.Workspace.Context;

using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Core.Tests.Support.Workspace.Context;
using AutoContext.Engine.Core.Workspace.Context;

public sealed class WorkspaceDetectionServiceTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Should_throw_when_detector_is_null()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => new WorkspaceDetectionService(null!));
        }
    }

    public sealed class StartAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_populate_detection_result_with_initial_scan()
        {
            // Arrange — a workspace whose contents the start-up scan
            // must surface before the first Workspace.Detect can land.
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "App.csproj");
            using var detector = WorkspaceContextDetectorTestFactory.Create(root);
            var service = new WorkspaceDetectionService(detector);

            // Act
            await service.StartAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(detector.Current.Has("hasCSharp"));
        }
    }

    public sealed class StopAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_complete_without_disposing_the_detector()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            using var detector = WorkspaceContextDetectorTestFactory.Create(root);
            var service = new WorkspaceDetectionService(detector);
            await service.StartAsync(TestContext.Current.CancellationToken);

            // Act + Assert — the no-op stop neither throws nor tears
            // down the detector, whose Current stays readable.
            await service.StopAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(detector.Current);
        }
    }
}
