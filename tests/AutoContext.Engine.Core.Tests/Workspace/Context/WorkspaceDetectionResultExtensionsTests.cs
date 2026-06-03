namespace AutoContext.Engine.Core.Tests.Workspace.Context;

using AutoContext.Engine.Core.Workspace.Context;

public sealed class WorkspaceDetectionResultExtensionsTests
{
    public sealed class ToWireFormat
    {
        [Fact]
        public void Should_throw_when_result_is_null()
        {
            // Arrange
            WorkspaceDetectionResult result = null!;

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => _ = result.ToWireFormat());
        }

        [Fact]
        public void Should_project_empty_result_to_empty_extensions_and_all_false_flags()
        {
            // Act
            var wire = WorkspaceDetectionResult.Empty.ToWireFormat();

            // Assert
            Assert.Multiple(
                () => Assert.Empty(wire.Extensions),
                () => Assert.False(wire.Flags.HasCSharp),
                () => Assert.False(wire.Flags.HasNodeJs),
                () => Assert.False(wire.Flags.HasGit),
                () => Assert.False(wire.Flags.HasYaml));
        }

        [Fact]
        public void Should_copy_extensions_in_order()
        {
            // Arrange
            var result = new WorkspaceDetectionResult
            {
                Extensions = ["cs", "ts", "json"],
                Flags = new HashSet<string>(),
            };

            // Act
            var wire = result.ToWireFormat();

            // Assert
            Assert.Equal(["cs", "ts", "json"], wire.Extensions);
        }

        [Fact]
        public void Should_raise_only_the_flags_present_in_the_result()
        {
            // Arrange — a representative cross-section of flags is
            // raised; every other flag must project to false.
            var result = new WorkspaceDetectionResult
            {
                Extensions = [],
                Flags = new HashSet<string>
                {
                    "hasCSharp",
                    "hasNodeJs",
                    "hasGit",
                    "hasEntityFrameworkCore",
                    "hasSignalR",
                },
            };

            // Act
            var wire = result.ToWireFormat();

            // Assert
            Assert.Multiple(
                () => Assert.True(wire.Flags.HasCSharp),
                () => Assert.True(wire.Flags.HasNodeJs),
                () => Assert.True(wire.Flags.HasGit),
                () => Assert.True(wire.Flags.HasEntityFrameworkCore),
                () => Assert.True(wire.Flags.HasSignalR),
                () => Assert.False(wire.Flags.HasPython),
                () => Assert.False(wire.Flags.HasAngular),
                () => Assert.False(wire.Flags.HasDocker));
        }
    }
}
