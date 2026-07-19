namespace AutoContext.Engine.Core.Tests.McpServer;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using AutoContext.Engine.Core.McpServer;
using AutoContext.Engine.Core.McpServer.Tools;
using AutoContext.Engine.Core.Tests.Support.McpServer.Tools;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol;
using ModelContextProtocol.Protocol;

public sealed class McpSdkAdapterTests
{
    private static McpSdkAdapter CreateAdapter(
        ConfigSnapshot config,
        IReadOnlyList<IMcpToolSource> sources,
        FakeConfigReloader? configReloader = null) => new(
            sources,
            new FakeConfigSnapshotAccessor { Current = config },
            configReloader ?? new FakeConfigReloader(),
            NullLogger<McpSdkAdapter>.Instance);

    public sealed class BuildVisibleToolsAsync
    {
        [Fact]
        public async Task Should_aggregate_tools_from_every_source()
        {
            // Arrange
            var adapter = CreateAdapter(
                ConfigSnapshot.Empty,
                [
                    new FakeMcpToolSource(new FakeMcpTool("analyze_a")),
                    new FakeMcpToolSource(new FakeMcpTool("list_instructions")),
                ]);

            // Act
            var tools = await adapter.BuildVisibleToolsAsync(TestContext.Current.CancellationToken);

            // Assert
            var names = tools.Select(t => t.Name!).ToArray();
            Assert.Multiple(
                () => Assert.Contains("analyze_a", names),
                () => Assert.Contains("list_instructions", names));
        }

        [Fact]
        public async Task Should_hide_tools_disabled_in_config()
        {
            // Arrange
            var config = new ConfigSnapshot
            {
                McpTools = [new ConfigMcpTool { Name = "read_b", Disabled = true }],
            };
            var adapter = CreateAdapter(
                config,
                [new FakeMcpToolSource(new FakeMcpTool("analyze_a"), new FakeMcpTool("read_b"))]);

            // Act
            var tools = await adapter.BuildVisibleToolsAsync(TestContext.Current.CancellationToken);

            // Assert
            var names = tools.Select(t => t.Name!).ToArray();
            Assert.Multiple(
                () => Assert.Contains("analyze_a", names),
                () => Assert.DoesNotContain("read_b", names));
        }

        [Fact]
        public async Task Should_re_read_config_before_projecting()
        {
            // Arrange
            var configReloader = new FakeConfigReloader();
            var adapter = CreateAdapter(ConfigSnapshot.Empty, [], configReloader);

            // Act
            await adapter.BuildVisibleToolsAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, configReloader.ReloadCallCount);
        }
    }

    public sealed class CallToolAsync
    {
        [Fact]
        public async Task Should_route_to_the_named_tool_and_marshal_its_response()
        {
            // Arrange
            var tool = new FakeMcpTool("analyze_a")
            {
                Response = new() { Result = JsonSerializer.SerializeToElement(new { kind = "ok" }) },
            };
            var adapter = CreateAdapter(ConfigSnapshot.Empty, [new FakeMcpToolSource(tool)]);
            var arguments = new Dictionary<string, JsonElement>
            {
                ["content"] = JsonSerializer.SerializeToElement("class C {}"),
            };

            // Act
            var result = await adapter.CallToolAsync(
                "analyze_a", arguments, TestContext.Current.CancellationToken);

            // Assert
            var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
            Assert.Multiple(
                () => Assert.Equal(1, tool.InvokeCallCount),
                () => Assert.Same(arguments, tool.LastArguments),
                () => Assert.Equal(tool.Response.Result!.Value.GetRawText(), block.Text));
        }

        [Fact]
        public async Task Should_throw_for_an_unknown_tool()
        {
            // Arrange
            var adapter = CreateAdapter(
                ConfigSnapshot.Empty, [new FakeMcpToolSource(new FakeMcpTool("analyze_a"))]);

            // Act + Assert
            await Assert.ThrowsAsync<McpException>(
                () => adapter.CallToolAsync(
                    "not_a_tool", arguments: null, TestContext.Current.CancellationToken).AsTask());
        }

        [Fact]
        public async Task Should_throw_when_the_tool_name_is_missing()
        {
            // Arrange
            var adapter = CreateAdapter(ConfigSnapshot.Empty, []);

            // Act + Assert
            await Assert.ThrowsAsync<McpException>(
                () => adapter.CallToolAsync(
                    name: null, arguments: null, TestContext.Current.CancellationToken).AsTask());
        }
    }
}
