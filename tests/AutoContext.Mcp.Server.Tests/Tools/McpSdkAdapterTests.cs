namespace AutoContext.Mcp.Server.Tests.Tools;

using AutoContext.Mcp.Server.Config;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tools;

using Microsoft.Extensions.Logging.Abstractions;

using AutoContext.Mcp.Server.Tests.Support.Tools;

public sealed class McpSdkAdapterTests
{
    [Fact]
    public void Should_return_all_tools_when_no_config_snapshot_is_supplied()
    {
        var registry = ToolTestFactory.BuildCatalog(
            ("alpha", [ToolTestFactory.BuildTool("alpha_tool")]),
            ("beta", [ToolTestFactory.BuildTool("beta_tool")]));
        var adapter = new McpSdkAdapter(registry, ToolTestFactory.BuildInvoker());

        var visible = adapter.ListVisibleTools();

        Assert.Multiple(
            () => Assert.Equal(2, visible.Count),
            () => Assert.Contains(visible, t => t.Name == "alpha_tool"),
            () => Assert.Contains(visible, t => t.Name == "beta_tool"));
    }

    [Fact]
    public void Should_return_all_tools_when_snapshot_has_nothing_disabled()
    {
        var registry = ToolTestFactory.BuildCatalog(("alpha", [ToolTestFactory.BuildTool("alpha_tool"), ToolTestFactory.BuildTool("beta_tool")]));
        var snapshot = new AutoContextConfigSnapshot();
        var adapter = new McpSdkAdapter(registry, ToolTestFactory.BuildInvoker(), snapshot, NullLogger<McpSdkAdapter>.Instance);

        var visible = adapter.ListVisibleTools();

        Assert.Equal(2, visible.Count);
    }

    [Fact]
    public void Should_filter_disabled_tools_from_tools_list()
    {
        var registry = ToolTestFactory.BuildCatalog(("alpha", [ToolTestFactory.BuildTool("alpha_tool"), ToolTestFactory.BuildTool("beta_tool"), ToolTestFactory.BuildTool("gamma_tool")]));
        var snapshot = new AutoContextConfigSnapshot();
        snapshot.Update(new JsonAutoContextConfigSnapshot { DisabledTools = ["beta_tool"] });
        var adapter = new McpSdkAdapter(registry, ToolTestFactory.BuildInvoker(), snapshot, NullLogger<McpSdkAdapter>.Instance);

        var visible = adapter.ListVisibleTools();

        Assert.Multiple(
            () => Assert.Equal(2, visible.Count),
            () => Assert.DoesNotContain(visible, t => t.Name == "beta_tool"),
            () => Assert.Contains(visible, t => t.Name == "alpha_tool"),
            () => Assert.Contains(visible, t => t.Name == "gamma_tool"));
    }

    [Fact]
    public void Should_reflect_snapshot_updates_on_subsequent_calls()
    {
        var registry = ToolTestFactory.BuildCatalog(("alpha", [ToolTestFactory.BuildTool("alpha_tool"), ToolTestFactory.BuildTool("beta_tool")]));
        var snapshot = new AutoContextConfigSnapshot();
        var adapter = new McpSdkAdapter(registry, ToolTestFactory.BuildInvoker(), snapshot, NullLogger<McpSdkAdapter>.Instance);

        var before = adapter.ListVisibleTools();

        snapshot.Update(new JsonAutoContextConfigSnapshot { DisabledTools = ["alpha_tool"] });
        var after = adapter.ListVisibleTools();

        Assert.Multiple(
            () => Assert.Equal(2, before.Count),
            () => Assert.Single(after),
            () => Assert.Equal("beta_tool", after[0].Name));
    }
}
