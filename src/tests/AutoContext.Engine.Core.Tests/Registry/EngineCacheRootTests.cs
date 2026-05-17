namespace AutoContext.Engine.Core.Tests.Registry;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Testing.Fixtures;

public sealed class EngineCacheRootTests
{
    public sealed class Resolve
    {
        [Fact]
        public void Should_return_override_normalised_to_full_path()
        {
            // Arrange
            var rooted = OperatingSystem.IsWindows()
                ? @"C:\some\absolute\override"
                : "/some/absolute/override";

            // Act
            var resolved = EngineCacheRoot.Resolve(rooted);

            // Assert
            Assert.Equal(Path.GetFullPath(rooted), resolved);
        }

        [Fact]
        public void Should_treat_whitespace_override_as_unset()
        {
            // Arrange + Act
            var fromWhitespace = EngineCacheRoot.Resolve("   ");
            var fromNull = EngineCacheRoot.Resolve(null);

            // Assert
            Assert.Equal(fromNull, fromWhitespace);
        }

        [Fact]
        public void Should_use_local_app_data_on_windows()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows-only resolution path.");

            // Arrange
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "autocontext");

            // Act
            var resolved = EngineCacheRoot.Resolve(null);

            // Assert
            Assert.Equal(expected, resolved);
        }

        [Fact]
        public void Should_prefer_xdg_cache_home_on_posix()
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "POSIX-only resolution path.");

            // Arrange
            using var xdg = new EnvironmentVariableFixture("XDG_CACHE_HOME", "/tmp/xdg-cache");

            // Act
            var resolved = EngineCacheRoot.Resolve(null);

            // Assert
            Assert.Equal(Path.Combine("/tmp/xdg-cache", "autocontext"), resolved);
        }

        [Fact]
        public void Should_fall_back_to_home_dot_cache_on_posix()
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "POSIX-only resolution path.");

            // Arrange
            using var xdg = new EnvironmentVariableFixture("XDG_CACHE_HOME", null);
            using var home = new EnvironmentVariableFixture("HOME", "/home/tester");

            // Act
            var resolved = EngineCacheRoot.Resolve(null);

            // Assert
            Assert.Equal(Path.Combine("/home/tester", ".cache", "autocontext"), resolved);
        }

        [Fact]
        public void Should_throw_when_posix_has_neither_xdg_nor_home()
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "POSIX-only resolution path.");

            // Arrange
            using var xdg = new EnvironmentVariableFixture("XDG_CACHE_HOME", null);
            using var home = new EnvironmentVariableFixture("HOME", null);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => EngineCacheRoot.Resolve(null));
        }
    }

    public sealed class ResolveRegistryFilePath
    {
        [Fact]
        public void Should_append_engine_registry_json_to_root()
        {
            // Arrange
            var rooted = OperatingSystem.IsWindows()
                ? @"C:\override\root"
                : "/override/root";

            // Act
            var path = EngineCacheRoot.ResolveRegistryFilePath(rooted);

            // Assert
            Assert.Equal(
                Path.Combine(Path.GetFullPath(rooted), "engine-registry.json"),
                path);
        }
    }
}
