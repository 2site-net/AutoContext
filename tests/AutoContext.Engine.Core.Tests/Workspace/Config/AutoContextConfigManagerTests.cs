namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config;

public sealed class AutoContextConfigManagerTests
{
    public sealed class LoadAsync(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_return_empty_when_file_missing()
        {
            // Arrange
            using var manager = AutoContextConfigTestFactory.Create(tempDirectory.CreateDirectory());

            // Act
            var config = await manager.LoadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Empty(config.Instructions),
                () => Assert.Empty(config.McpTools),
                () => Assert.Null(config.Diagnostic));
        }

        [Fact]
        public async Task Should_read_persisted_graph_from_disk()
        {
            // Arrange
            var workspace = tempDirectory.CreateDirectory();

            using (var writer = AutoContextConfigTestFactory.Create(workspace))
            {
                await writer.LoadAsync(TestContext.Current.CancellationToken);
                await writer.UpdateAsync(
                    config => config with
                    {
                        Instructions =
                        [
                            new InstructionsFileConfig
                            {
                                Name = "a.md",
                                Version = "1.0",
                                Rules = [new InstructionsFileConfig.InstructionsRule { Id = "x", Disabled = true }],
                            },
                        ],
                    },
                    TestContext.Current.CancellationToken);
            }

            using var reader = AutoContextConfigTestFactory.Create(workspace);

            // Act
            var config = await reader.LoadAsync(TestContext.Current.CancellationToken);

            // Assert
            var file = Assert.Single(config.Instructions);
            var rule = Assert.Single(file.Rules);

            Assert.Multiple(
                () => Assert.Equal("a.md", file.Name),
                () => Assert.Equal("1.0", file.Version),
                () => Assert.Equal("x", rule.Id),
                () => Assert.True(rule.Disabled));
        }

        [Fact]
        public async Task Should_return_empty_for_corrupt_file()
        {
            // Arrange
            var workspace = tempDirectory.CreateDirectory();
            using var manager = AutoContextConfigTestFactory.Create(workspace);
            await File.WriteAllTextAsync(
                manager.ConfigPath,
                "not json",
                TestContext.Current.CancellationToken);

            // Act
            var config = await manager.LoadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(config.Instructions);
        }
    }

    public sealed class UpdateAsync(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_stamp_engine_version_and_publish_snapshot()
        {
            // Arrange
            using var manager = AutoContextConfigTestFactory.Create(tempDirectory.CreateDirectory());
            await manager.LoadAsync(TestContext.Current.CancellationToken);

            // Act
            await manager.UpdateAsync(
                config => config with
                {
                    McpTools = [new McpToolConfig { Name = "t1", Disabled = true }],
                },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(AutoContextConfigTestFactory.EngineVersion, manager.Current.Version),
                () => Assert.Equal("t1", Assert.Single(manager.Current.McpTools).Name),
                () => Assert.True(File.Exists(manager.ConfigPath)));
        }

        [Fact]
        public async Task Should_raise_changed_with_new_snapshot()
        {
            // Arrange
            using var manager = AutoContextConfigTestFactory.Create(tempDirectory.CreateDirectory());
            await manager.LoadAsync(TestContext.Current.CancellationToken);

            AutoContextConfig? observed = null;
            manager.Changed += (_, snapshot) => observed = snapshot;

            // Act
            await manager.UpdateAsync(
                config => config with { McpTools = [new McpToolConfig { Name = "t1", Disabled = true }] },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Same(manager.Current, observed);
        }

        [Fact]
        public async Task Should_not_write_or_raise_changed_on_no_op()
        {
            // Arrange
            using var manager = AutoContextConfigTestFactory.Create(tempDirectory.CreateDirectory());
            await manager.LoadAsync(TestContext.Current.CancellationToken);

            var raised = false;
            manager.Changed += (_, _) => raised = true;

            // Act
            await manager.UpdateAsync(config => config, TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.False(raised),
                () => Assert.False(File.Exists(manager.ConfigPath)));
        }

        [Fact]
        public async Task Should_delete_file_when_edit_empties_config()
        {
            // Arrange
            using var manager = AutoContextConfigTestFactory.Create(tempDirectory.CreateDirectory());
            await manager.LoadAsync(TestContext.Current.CancellationToken);
            await manager.UpdateAsync(
                config => config with { McpTools = [new McpToolConfig { Name = "t1", Disabled = true }] },
                TestContext.Current.CancellationToken);

            // Act
            await manager.UpdateAsync(
                config => config with { McpTools = [] },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.False(File.Exists(manager.ConfigPath)),
                () => Assert.Empty(manager.Current.McpTools));
        }
    }

    public sealed class RefreshAsync(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_adopt_external_change_and_raise_changed()
        {
            // Arrange
            using var manager = AutoContextConfigTestFactory.Create(tempDirectory.CreateDirectory());
            await manager.LoadAsync(TestContext.Current.CancellationToken);

            var raised = false;
            manager.Changed += (_, _) => raised = true;

            await File.WriteAllTextAsync(
                manager.ConfigPath,
                "{ \"mcpTools\": { \"t1\": false } }",
                TestContext.Current.CancellationToken);

            // Act
            await manager.RefreshAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(raised),
                () => Assert.Equal("t1", Assert.Single(manager.Current.McpTools).Name),
                () => Assert.True(Assert.Single(manager.Current.McpTools).Disabled));
        }

        [Fact]
        public async Task Should_ignore_echo_of_own_write()
        {
            // Arrange
            using var manager = AutoContextConfigTestFactory.Create(tempDirectory.CreateDirectory());
            await manager.LoadAsync(TestContext.Current.CancellationToken);
            await manager.UpdateAsync(
                config => config with { McpTools = [new McpToolConfig { Name = "t1", Disabled = true }] },
                TestContext.Current.CancellationToken);

            var raised = false;
            manager.Changed += (_, _) => raised = true;

            // Act
            await manager.RefreshAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.False(raised);
        }

        [Fact]
        public async Task Should_adopt_external_delete()
        {
            // Arrange
            using var manager = AutoContextConfigTestFactory.Create(tempDirectory.CreateDirectory());
            await manager.LoadAsync(TestContext.Current.CancellationToken);
            await manager.UpdateAsync(
                config => config with { McpTools = [new McpToolConfig { Name = "t1", Disabled = true }] },
                TestContext.Current.CancellationToken);

            // Act
            File.Delete(manager.ConfigPath);
            await manager.RefreshAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(manager.Current.McpTools);
        }
    }
}
