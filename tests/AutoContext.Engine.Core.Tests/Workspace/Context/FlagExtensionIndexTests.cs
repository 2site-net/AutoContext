namespace AutoContext.Engine.Core.Tests.Workspace.Context;

using AutoContext.Engine.Core.Workspace.Context;

public sealed class FlagExtensionIndexTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_file_rules()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => new FlagExtensionIndex(null!));
        }

        [Fact]
        public void Should_reject_duplicate_file_rule_flags()
        {
            // Act + Assert
            Assert.Throws<ArgumentException>(() => new FlagExtensionIndex(
            [
                new("hasCSharp", [new("cs", FileSelectorKind.Extension)]),
                new("hasCSharp", [new("csx", FileSelectorKind.Extension)]),
            ]));
        }
    }

    public sealed class Resolve
    {
        [Fact]
        public void Should_reject_null_active_flags()
        {
            // Arrange
            var index = new FlagExtensionIndex([]);

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => index.Resolve(null!));
        }

        [Fact]
        public void Should_return_empty_for_no_active_flags()
        {
            // Arrange
            var index = new FlagExtensionIndex(
            [
                new("hasCSharp", [new("cs", FileSelectorKind.Extension)]),
            ]);

            // Act
            var extensions = index.Resolve(new HashSet<string>());

            // Assert
            Assert.Empty(extensions);
        }

        [Fact]
        public void Should_union_extension_selectors_of_active_flags()
        {
            // Arrange
            var index = new FlagExtensionIndex(
            [
                new("hasCSharp", [new("cs", FileSelectorKind.Extension)]),
                new("hasTypeScript", [new("ts", FileSelectorKind.Extension), new("tsx", FileSelectorKind.Extension)]),
            ]);

            // Act
            var extensions = index.Resolve(new HashSet<string> { "hasCSharp", "hasTypeScript" });

            // Assert
            Assert.Equal(["cs", "ts", "tsx"], extensions);
        }

        [Fact]
        public void Should_ignore_file_name_and_glob_selectors()
        {
            // Arrange
            var index = new FlagExtensionIndex(
            [
                new("hasRust", [new("rs", FileSelectorKind.Extension), new("Cargo.toml", FileSelectorKind.FileName)]),
                new("hasDocker", [new("**/Dockerfile*", FileSelectorKind.GlobPattern)]),
            ]);

            // Act
            var extensions = index.Resolve(new HashSet<string> { "hasRust", "hasDocker" });

            // Assert
            Assert.Equal(["rs"], extensions);
        }

        [Fact]
        public void Should_ignore_flags_without_a_file_rule()
        {
            // Arrange
            var index = new FlagExtensionIndex(
            [
                new("hasCSharp", [new("cs", FileSelectorKind.Extension)]),
            ]);

            // Act
            var extensions = index.Resolve(new HashSet<string> { "hasXunit", "hasGit" });

            // Assert
            Assert.Empty(extensions);
        }

        [Fact]
        public void Should_ignore_inactive_flags()
        {
            // Arrange
            var index = new FlagExtensionIndex(
            [
                new("hasCSharp", [new("cs", FileSelectorKind.Extension)]),
                new("hasTypeScript", [new("ts", FileSelectorKind.Extension)]),
            ]);

            // Act
            var extensions = index.Resolve(new HashSet<string> { "hasCSharp" });

            // Assert
            Assert.Equal(["cs"], extensions);
        }

        [Fact]
        public void Should_deduplicate_extensions_shared_by_multiple_active_flags()
        {
            // Arrange
            var index = new FlagExtensionIndex(
            [
                new("hasHtml", [new("html", FileSelectorKind.Extension), new("cshtml", FileSelectorKind.Extension)]),
                new("hasRazor", [new("cshtml", FileSelectorKind.Extension)]),
            ]);

            // Act
            var extensions = index.Resolve(new HashSet<string> { "hasHtml", "hasRazor" });

            // Assert
            Assert.Equal(["cshtml", "html"], extensions);
        }
    }
}
