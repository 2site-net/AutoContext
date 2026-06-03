namespace AutoContext.Engine.Core.Tests.Workspace.Context;

using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Core.Tests.Support.Workspace.Context;
using AutoContext.Engine.Core.Workspace.Context;

public sealed class WorkspaceFileClassifierTests
{
    public sealed class ClassifyAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_flag_a_file_by_extension()
        {
            // Arrange
            var classifier = WorkspaceFileClassifierTestFactory.Create();
            var fullPath = tempDirectory.CreatePath("App.cs");

            // Act
            var flags = await classifier.ClassifyAsync(
                fullPath, "App.cs", TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains("hasCSharp", flags);
        }

        [Fact]
        public async Task Should_flag_a_file_by_name()
        {
            // Arrange
            var classifier = WorkspaceFileClassifierTestFactory.Create();
            var fullPath = tempDirectory.CreatePath("Cargo.toml");

            // Act
            var flags = await classifier.ClassifyAsync(
                fullPath, "Cargo.toml", TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains("hasRust", flags);
        }

        [Fact]
        public async Task Should_flag_a_file_by_glob_against_the_relative_path()
        {
            // Arrange
            var classifier = WorkspaceFileClassifierTestFactory.Create();
            var fullPath = tempDirectory.CreatePath("Dockerfile.production");

            // Act
            var flags = await classifier.ClassifyAsync(
                fullPath, "deploy/Dockerfile.production", TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains("hasDocker", flags);
        }

        [Fact]
        public async Task Should_set_hasNodeJs_for_package_json()
        {
            // Arrange
            var classifier = WorkspaceFileClassifierTestFactory.Create();
            var fullPath = tempDirectory.CreatePath("package.json");

            // Act
            var flags = await classifier.ClassifyAsync(
                fullPath, "package.json", TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains("hasNodeJs", flags);
        }

        [Fact]
        public async Task Should_flag_a_manifest_when_its_body_matches_a_content_rule()
        {
            // Arrange
            var classifier = WorkspaceFileClassifierTestFactory.Create();
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(
                root,
                "Tests.csproj",
                """<Project><ItemGroup><PackageReference Include="xunit" /></ItemGroup></Project>""");
            var fullPath = Path.Combine(root, "Tests.csproj");

            // Act
            var flags = await classifier.ClassifyAsync(
                fullPath, "Tests.csproj", TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains("hasXunit", flags);
        }

        [Fact]
        public async Task Should_not_flag_a_manifest_whose_body_matches_no_content_rule()
        {
            // Arrange
            var classifier = WorkspaceFileClassifierTestFactory.Create();
            var root = tempDirectory.CreateDirectory();
            WorkspaceFileTestWriter.Write(root, "Lib.csproj", "<Project></Project>");
            var fullPath = Path.Combine(root, "Lib.csproj");

            // Act
            var flags = await classifier.ClassifyAsync(
                fullPath, "Lib.csproj", TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(flags);
        }

        [Fact]
        public async Task Should_return_no_flags_for_an_unmatched_file()
        {
            // Arrange
            var classifier = WorkspaceFileClassifierTestFactory.Create();
            var fullPath = tempDirectory.CreatePath("notes.md");

            // Act
            var flags = await classifier.ClassifyAsync(
                fullPath, "notes.md", TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(flags);
        }
    }

    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_arguments()
        {
            // Arrange
            var rules = Array.Empty<FilePresenceRule>();
            var scans = Array.Empty<ContentScan>();

            // Act + Assert
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(
                    () => new WorkspaceFileClassifier(null!, scans)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new WorkspaceFileClassifier(rules, null!)));
        }
    }

    public sealed class IsRelevant
    {
        [Theory]
        [InlineData("App.cs", "App.cs")]
        [InlineData("Cargo.toml", "Cargo.toml")]
        [InlineData("package.json", "package.json")]
        [InlineData("Dockerfile.production", "deploy/Dockerfile.production")]
        [InlineData("Tests.csproj", "Tests.csproj")]
        public void Should_accept_files_matching_any_selector(string fileName, string relativePath)
        {
            // Arrange
            var classifier = WorkspaceFileClassifierTestFactory.Create();

            // Act + Assert
            Assert.True(classifier.IsRelevant(fileName, relativePath));
        }

        [Theory]
        [InlineData("notes.md", "notes.md")]
        [InlineData("logo.png", "assets/logo.png")]
        public void Should_reject_files_matching_no_selector(string fileName, string relativePath)
        {
            // Arrange
            var classifier = WorkspaceFileClassifierTestFactory.Create();

            // Act + Assert
            Assert.False(classifier.IsRelevant(fileName, relativePath));
        }

        [Fact]
        public void Should_treat_every_file_rule_extension_and_name_selector_as_relevant()
        {
            // Arrange: the watcher filters events through IsRelevant, so every
            // extension/name selector in the rule table must survive the filter
            // or its files would never reclassify (the TS yaml/dart/ruby/swift drift).
            var classifier = new WorkspaceFileClassifier(
                WorkspaceDetectionRules.FileRules, WorkspaceDetectionRules.ContentScans);
            var selectors = WorkspaceDetectionRules.FileRules
                .SelectMany(rule => rule.Selectors)
                .Where(selector => selector.Kind is FileSelectorKind.Extension or FileSelectorKind.FileName)
                .ToArray();

            // Act
            var drifted = selectors
                .Select(selector => selector.Kind == FileSelectorKind.Extension
                    ? $"sample.{selector.Value}"
                    : selector.Value)
                .Where(fileName => !classifier.IsRelevant(fileName, fileName))
                .Order(StringComparer.Ordinal);

            // Assert
            Assert.Multiple(
                () => Assert.NotEmpty(selectors),
                () => Assert.Empty(drifted));
        }
    }
}
