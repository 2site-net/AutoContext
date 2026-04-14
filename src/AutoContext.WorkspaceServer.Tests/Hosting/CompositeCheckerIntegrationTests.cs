namespace AutoContext.WorkspaceServer.Tests.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using AutoContext.Mcp.Shared.Checkers;
using AutoContext.Mcp.Shared.WorkspaceServer;
using AutoContext.WorkspaceServer.Hosting;
using AutoContext.WorkspaceServer.Hosting.EditorConfig;
using AutoContext.WorkspaceServer.Hosting.Logging;
using AutoContext.WorkspaceServer.Hosting.McpTools;

public sealed class CompositeCheckerIntegrationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"cc-test-{Guid.NewGuid():N}");

    public CompositeCheckerIntegrationTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task EditorConfig_should_override_explicit_params_on_conflict()
    {
        var ct = TestContext.Current.CancellationToken;

        // .editorconfig says indent_size = 4
        await File.WriteAllTextAsync(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.cs]
            indent_size = 4
            """,
            ct);

        var pipeName = $"cc-test-{Guid.NewGuid():N}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var service = CreateService(pipeName);
        await service.StartAsync(cts.Token);

        try
        {
            var spy = new SpyChecker("indent_size");
            var checker = new TestCompositeChecker(
                new WorkspaceServerClient(pipeName),
                [spy]);

            // Explicit param says indent_size = 2, but .editorconfig says 4
            var filePath = Path.Combine(_tempRoot, "Program.cs");
            var data = new Dictionary<string, string>
            {
                ["filePath"] = filePath,
                ["indent_size"] = "2",
            };

            await checker.CheckAsync("class C { }", data);

            // EditorConfig (project rules) must win over the explicit param (model)
            Assert.Multiple(
                () => Assert.NotNull(spy.ReceivedData),
                () => Assert.Equal("4", spy.ReceivedData!["indent_size"]));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task EditorConfig_should_merge_with_explicit_params_when_no_conflict()
    {
        var ct = TestContext.Current.CancellationToken;

        await File.WriteAllTextAsync(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.cs]
            indent_size = 4
            """,
            ct);

        var pipeName = $"cc-test-{Guid.NewGuid():N}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var service = CreateService(pipeName);
        await service.StartAsync(cts.Token);

        try
        {
            var spy = new SpyChecker("indent_size");
            var checker = new TestCompositeChecker(
                new WorkspaceServerClient(pipeName),
                [spy]);

            // Explicit param has a non-overlapping key
            var filePath = Path.Combine(_tempRoot, "Program.cs");
            var data = new Dictionary<string, string>
            {
                ["filePath"] = filePath,
                ["custom_param"] = "hello",
            };

            await checker.CheckAsync("class C { }", data);

            // Both values should be present
            Assert.Multiple(
                () => Assert.NotNull(spy.ReceivedData),
                () => Assert.Equal("4", spy.ReceivedData!["indent_size"]),
                () => Assert.Equal("hello", spy.ReceivedData!["custom_param"]));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private WorkspaceService CreateService(string pipeName)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("pipe", pipeName),
                new("workspace-root", _tempRoot),
            ])
            .Build();

        var resolver = new EditorConfigResolver();

        IRequestHandler[] handlers =
        [
            new EditorConfigRequestHandler(resolver),
            new McpToolsRequestHandler(resolver, new McpToolsConfig(config)),
            new LogRequestHandler(),
        ];

        return new WorkspaceService(config, handlers, NullLogger<WorkspaceService>.Instance);
    }

    /// <summary>
    /// A sub-checker that captures the <c>data</c> dictionary it receives,
    /// enabling assertions on the merged result.
    /// </summary>
    private sealed class SpyChecker(params string[] editorConfigKeys) : IChecker, IEditorConfigFilter
    {
        public string ToolName => "spy_checker";

        public IReadOnlyList<string> EditorConfigKeys { get; } = editorConfigKeys;

        public IReadOnlyDictionary<string, string>? ReceivedData { get; private set; }

        public Task<string> CheckAsync(string content, IReadOnlyDictionary<string, string>? data = null)
        {
            ReceivedData = data;
            return Task.FromResult("✅ Spy passed.");
        }
    }

    private sealed class TestCompositeChecker(
        WorkspaceServerClient workspaceServerClient,
        IChecker[] checkers) : CompositeChecker(workspaceServerClient)
    {
        public override string ToolName => "test_composite";

        protected override string ToolLabel => "Test";

        protected override IChecker[] CreateCheckers() => checkers;
    }
}
