namespace AutoContext.Mcp.Server.Tests.Tools;

using AutoContext.Mcp.Server.Config;
using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tools;
using AutoContext.Mcp.Server.Tools.Invocation;
using AutoContext.Mcp.Server.Workers;

using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

public sealed class McpSdkAdapterTests
{
    [Fact]
    public async Task Should_return_all_tools_when_no_config_snapshot_is_supplied()
    {
        var registry = BuildCatalog(
            ("alpha", [BuildTool("alpha_tool")]),
            ("beta", [BuildTool("beta_tool")]));
        var adapter = new McpSdkAdapter(registry, BuildInvoker());

        var result = await adapter.HandleListToolsAsync(BuildRequest(), CancellationToken.None);

        Assert.Multiple(
            () => Assert.Equal(2, result.Tools.Count),
            () => Assert.Contains(result.Tools, t => t.Name == "alpha_tool"),
            () => Assert.Contains(result.Tools, t => t.Name == "beta_tool"));
    }

    [Fact]
    public async Task Should_return_all_tools_when_snapshot_has_nothing_disabled()
    {
        var registry = BuildCatalog(("alpha", [BuildTool("alpha_tool"), BuildTool("beta_tool")]));
        var snapshot = new AutoContextConfigSnapshot();
        var adapter = new McpSdkAdapter(registry, BuildInvoker(), snapshot, NullLogger<McpSdkAdapter>.Instance);

        var result = await adapter.HandleListToolsAsync(BuildRequest(), CancellationToken.None);

        Assert.Equal(2, result.Tools.Count);
    }

    [Fact]
    public async Task Should_filter_disabled_tools_from_tools_list()
    {
        var registry = BuildCatalog(("alpha", [BuildTool("alpha_tool"), BuildTool("beta_tool"), BuildTool("gamma_tool")]));
        var snapshot = new AutoContextConfigSnapshot();
        snapshot.Update(new AutoContextConfigSnapshotDto { DisabledTools = ["beta_tool"] });
        var adapter = new McpSdkAdapter(registry, BuildInvoker(), snapshot, NullLogger<McpSdkAdapter>.Instance);

        var result = await adapter.HandleListToolsAsync(BuildRequest(), CancellationToken.None);

        Assert.Multiple(
            () => Assert.Equal(2, result.Tools.Count),
            () => Assert.DoesNotContain(result.Tools, t => t.Name == "beta_tool"),
            () => Assert.Contains(result.Tools, t => t.Name == "alpha_tool"),
            () => Assert.Contains(result.Tools, t => t.Name == "gamma_tool"));
    }

    [Fact]
    public async Task Should_reflect_snapshot_updates_on_subsequent_calls()
    {
        var registry = BuildCatalog(("alpha", [BuildTool("alpha_tool"), BuildTool("beta_tool")]));
        var snapshot = new AutoContextConfigSnapshot();
        var adapter = new McpSdkAdapter(registry, BuildInvoker(), snapshot, NullLogger<McpSdkAdapter>.Instance);

        var before = await adapter.HandleListToolsAsync(BuildRequest(), CancellationToken.None);

        snapshot.Update(new AutoContextConfigSnapshotDto { DisabledTools = ["alpha_tool"] });
        var after = await adapter.HandleListToolsAsync(BuildRequest(), CancellationToken.None);

        Assert.Multiple(
            () => Assert.Equal(2, before.Tools.Count),
            () => Assert.Single(after.Tools),
            () => Assert.Equal("beta_tool", after.Tools[0].Name));
    }

    private static RequestContext<ListToolsRequestParams> BuildRequest() => new(
        server: null!,
        jsonRpcRequest: new JsonRpcRequest { Method = "tools/list" },
        parameters: new ListToolsRequestParams());

    private static ToolInvoker BuildInvoker()
    {
        var workerClient = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(
            workerClient,
            "autocontext-test-workspace-unused",
            NullLogger<EditorConfigBatcher>.Instance);
        return new ToolInvoker(workerClient, batcher);
    }

    private static McpWorkersCatalog BuildCatalog(
        params (string Id, IReadOnlyList<McpToolDefinition> Definitions)[] workers)
    {
        var list = new List<McpWorker>(workers.Length);

        foreach (var (id, definitions) in workers)
        {
            list.Add(new McpWorker
            {
                Id = id,
                Name = $"AutoContext.Worker.{id}",
                Tools = definitions,
            });
        }

        return new McpWorkersCatalog
        {
            SchemaVersion = "1",
            Workers = list,
        };
    }

    private static McpToolDefinition BuildTool(string name) => new()
    {
        Name = name,
        Description = "Test tool.",
        Parameters = new Dictionary<string, McpToolParameter>(StringComparer.Ordinal),
        Tasks = [new McpTaskDefinition { Name = $"{name}_task" }],
    };
}
