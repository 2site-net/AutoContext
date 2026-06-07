namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;

public sealed class InstructionsOverridesTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_map()
            => Assert.Throws<ArgumentNullException>(() => new InstructionsOverrides(null!));
    }

    public sealed class Empty
    {
        [Fact]
        public void Should_have_no_entries()
            => Assert.Multiple(
                () => Assert.Equal(0, InstructionsOverrides.Empty.Count),
                () => Assert.Empty(InstructionsOverrides.Empty.FileNames));
    }

    public sealed class Contains
    {
        [Fact]
        public void Should_report_membership_by_file_name()
        {
            // Arrange
            var overrides = new InstructionsOverrides(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["docker.instructions.md"] = "/ws/.github/instructions/docker.instructions.md",
            });

            // Act + Assert
            Assert.Multiple(
                () => Assert.True(overrides.Contains("docker.instructions.md")),
                () => Assert.False(overrides.Contains("python.instructions.md")));
        }

        [Fact]
        public void Should_reject_null_file_name()
            => Assert.Throws<ArgumentNullException>(() => InstructionsOverrides.Empty.Contains(null!));

        [Fact]
        public void Should_match_case_insensitively()
        {
            // Arrange
            var overrides = new InstructionsOverrides(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Docker.Instructions.MD"] = "/ws/.github/instructions/Docker.Instructions.MD",
            });

            // Act + Assert
            Assert.True(overrides.Contains("docker.instructions.md"));
        }
    }

    public sealed class TryGetPath
    {
        [Fact]
        public void Should_return_path_when_present()
        {
            // Arrange
            var overrides = new InstructionsOverrides(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["docker.instructions.md"] = "/ws/.github/instructions/docker.instructions.md",
            });

            // Act
            var found = overrides.TryGetPath("docker.instructions.md", out var path);

            // Assert
            Assert.Multiple(
                () => Assert.True(found),
                () => Assert.Equal("/ws/.github/instructions/docker.instructions.md", path));
        }

        [Fact]
        public void Should_return_false_when_absent()
        {
            // Act
            var found = InstructionsOverrides.Empty.TryGetPath("docker.instructions.md", out var path);

            // Assert
            Assert.Multiple(
                () => Assert.False(found),
                () => Assert.Null(path));
        }

        [Fact]
        public void Should_reject_null_file_name()
            => Assert.Throws<ArgumentNullException>(
                () => InstructionsOverrides.Empty.TryGetPath(null!, out _));
    }

    public sealed class FileNames
    {
        [Fact]
        public void Should_be_ordinal_sorted()
        {
            // Arrange
            var overrides = new InstructionsOverrides(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["python.instructions.md"] = "/ws/.github/instructions/python.instructions.md",
                ["docker.instructions.md"] = "/ws/.github/instructions/docker.instructions.md",
            });

            // Act + Assert
            Assert.Equal(
                ["docker.instructions.md", "python.instructions.md"],
                overrides.FileNames);
        }
    }
}
