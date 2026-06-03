namespace AutoContext.Engine.Core.Tests.Workspace.Context;

using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Core.Tests.Support.Workspace.Context;
using AutoContext.Engine.Core.Workspace.Context;

public sealed class WorkspaceContextDetectorTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_invalid_arguments()
        {
            // Arrange
            var rules = WorkspaceDetectionRules.FileRules;
            var scans = WorkspaceDetectionRules.ContentScans;
            var edges = WorkspaceDetectionRules.FlagActivationEdges;

            // Act + Assert
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(
                    () => new WorkspaceContextDetector(null!, rules, scans, edges)),
                () => Assert.Throws<ArgumentException>(
                    () => new WorkspaceContextDetector(
                        new FakeWorkspaceEngineInfo { WorkspacePath = "  " }, rules, scans, edges)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new WorkspaceContextDetector(
                        new FakeWorkspaceEngineInfo { WorkspacePath = "x" }, null!, scans, edges)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new WorkspaceContextDetector(
                        new FakeWorkspaceEngineInfo { WorkspacePath = "x" }, rules, null!, edges)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new WorkspaceContextDetector(
                        new FakeWorkspaceEngineInfo { WorkspacePath = "x" }, rules, scans, null!)),
                () => Assert.Throws<ArgumentOutOfRangeException>(
                    () => new WorkspaceContextDetector(
                        new FakeWorkspaceEngineInfo { WorkspacePath = "x" },
                        rules, scans, edges, debounceDelay: TimeSpan.Zero)));
        }
    }

    public sealed class DetectAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_return_empty_for_empty_workspace()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(result.Flags);
        }

        [Fact]
        public async Task Should_expose_workspace_info_metadata_and_bump_revision_when_snapshot_changes()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "App.csproj");
            var instanceId = Guid.NewGuid();

            using var sut = new WorkspaceContextDetector(
                new FakeWorkspaceEngineInfo
                {
                    WorkspacePath = root,
                    InstanceId = instanceId,
                    InstanceLabel = "primary",
                    IdleTimeout = TimeSpan.FromSeconds(15),
                },
                WorkspaceDetectionRules.FileRules,
                WorkspaceDetectionRules.ContentScans,
                WorkspaceDetectionRules.FlagActivationEdges);

            // Act
            _ = await sut.DetectAsync(TestContext.Current.CancellationToken);
            WorkspaceFileTestWriter.Write(root, "app.py");
            var second = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(instanceId, sut.EngineInfo.InstanceId),
                () => Assert.Equal("primary", sut.EngineInfo.InstanceLabel),
                () => Assert.Equal(TimeSpan.FromSeconds(15), sut.EngineInfo.IdleTimeout),
                () => Assert.Equal(2L, sut.Revision),
                () => Assert.True(second.Has("hasCSharp")),
                () => Assert.True(second.Has("hasPython")));
        }

        [Theory]
        [InlineData("App.csproj", "hasCSharp")]
        [InlineData("Lib.fsproj", "hasFSharp")]
        [InlineData("app.py", "hasPython")]
        [InlineData("styles.css", "hasCss")]
        [InlineData("script.ps1", "hasPowerShell")]
        [InlineData("main.lua", "hasLua")]
        public async Task Should_detect_flag_from_extension(string relativePath, string expectedFlag)
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, relativePath);
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Has(expectedFlag));
        }

        [Theory]
        [InlineData("Cargo.toml", "hasRust")]
        [InlineData("Gemfile", "hasRuby")]
        [InlineData("go.mod", "hasGo")]
        [InlineData("composer.json", "hasPhp")]
        [InlineData("pom.xml", "hasJava")]
        public async Task Should_detect_flag_from_file_name(string relativePath, string expectedFlag)
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, relativePath);
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Has(expectedFlag));
        }

        [Fact]
        public async Task Should_set_hasNodeJs_for_package_json()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "package.json", "{}");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has("hasNodeJs")),
                () => Assert.True(result.Has("hasJavaScript")));
        }

        [Fact]
        public async Task Should_detect_docker_from_glob_pattern()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "Dockerfile");
            WorkspaceFileTestWriter.Write(root, "deploy/Dockerfile.production");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Has("hasDocker"));
        }

        [Fact]
        public async Task Should_detect_unity_from_nested_glob_pattern()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "ProjectSettings/ProjectSettings.asset");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has("hasUnity")),
                () => Assert.True(result.Has("hasCSharp")));
        }

        [Fact]
        public async Task Should_set_hasGit_when_git_directory_present()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Has("hasGit"));
        }

        [Fact]
        public async Task Should_cascade_typescript_to_javascript()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "app.ts");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has("hasTypeScript")),
                () => Assert.True(result.Has("hasJavaScript")));
        }

        [Fact]
        public async Task Should_cascade_react_content_to_nodejs_and_javascript()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "package.json", """{ "dependencies": { "react": "^18.0.0" } }""");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has("hasReact")),
                () => Assert.True(result.Has("hasNodeJs")),
                () => Assert.True(result.Has("hasJavaScript")));
        }

        [Fact]
        public async Task Should_cascade_nextjs_content_to_react()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "package.json", """{ "dependencies": { "next": "^14.0.0" } }""");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has("hasNextJs")),
                () => Assert.True(result.Has("hasReact")),
                () => Assert.True(result.Has("hasNodeJs")));
        }

        [Fact]
        public async Task Should_detect_dotnet_content_flag_from_project_file()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(
                root,
                "Tests.csproj",
                """<Project><ItemGroup><PackageReference Include="xunit" Version="2.0.0" /></ItemGroup></Project>""");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Has("hasXunit")),
                () => Assert.True(result.Has("hasDotNetTesting")),
                () => Assert.True(result.Has("hasDotNet")));
        }

        [Fact]
        public async Task Should_ignore_files_in_excluded_directories()
        {
            // Arrange
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "node_modules/pkg/Library.csproj");
            WorkspaceFileTestWriter.Write(root, "obj/Generated.cs");
            using var sut = WorkspaceContextDetectorTestFactory.Create(root);

            // Act
            var result = await sut.DetectAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(result.Flags);
        }
    }
}
