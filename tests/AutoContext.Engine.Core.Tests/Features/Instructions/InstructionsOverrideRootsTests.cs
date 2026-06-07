namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

public sealed class InstructionsOverrideRootsTests
{
    public sealed class Resolve
    {
        [Fact]
        public void Should_return_default_when_engine_absent()
        {
            // Act
            var directories = InstructionsOverrideRoots.Resolve(ConfigSnapshot.Empty);

            // Assert
            Assert.Equal([".github"], directories);
        }

        [Fact]
        public void Should_return_default_when_directories_empty()
        {
            // Arrange
            var config = ConfigSnapshot.Empty with
            {
                Engine = new ConfigEngineSettings { InstructionsOverrideRoots = [] },
            };

            // Act
            var directories = InstructionsOverrideRoots.Resolve(config);

            // Assert
            Assert.Equal([".github"], directories);
        }

        [Fact]
        public void Should_return_configured_directories()
        {
            // Arrange
            var config = ConfigSnapshot.Empty with
            {
                Engine = new ConfigEngineSettings { InstructionsOverrideRoots = [".copilot", ".github"] },
            };

            // Act
            var directories = InstructionsOverrideRoots.Resolve(config);

            // Assert
            Assert.Equal([".copilot", ".github"], directories);
        }

        [Fact]
        public void Should_reject_null_config()
            => Assert.Throws<ArgumentNullException>(
                () => InstructionsOverrideRoots.Resolve(null!));
    }
}
