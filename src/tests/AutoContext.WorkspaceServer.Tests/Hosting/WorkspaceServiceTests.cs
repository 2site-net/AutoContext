namespace AutoContext.WorkspaceServer.Tests.Hosting;

using System.IO.Pipes;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using AutoContext.WorkspaceServer.Hosting;
using AutoContext.WorkspaceServer.Hosting.EditorConfig;
using AutoContext.WorkspaceServer.Hosting.McpTools;
using AutoContext.Mcp.Shared.WorkspaceServer.McpTools;

public sealed class WorkspaceServiceTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
    };

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"ec-svc-test-{Guid.NewGuid():N}");

    public WorkspaceServiceTests()
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
    public async Task Should_resolve_properties_over_pipe()
    {
        var ct = TestContext.Current.CancellationToken;

        await File.WriteAllTextAsync(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.cs]
            indent_style = space
            indent_size = 4
            """,
            ct);

        var pipeName = $"ec-test-{Guid.NewGuid():N}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var service = CreateService(pipeName);
        await service.StartAsync(cts.Token);

        try
        {
            var response = await SendRequestAsync(
                pipeName,
                new EditorConfigRequest(Path.Combine(_tempRoot, "Program.cs")),
                ct);

            Assert.NotNull(response);
            Assert.Equal("space", response!.Properties["indent_style"]);
            Assert.Equal("4", response.Properties["indent_size"]);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Should_filter_by_keys_over_pipe()
    {
        var ct = TestContext.Current.CancellationToken;

        await File.WriteAllTextAsync(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.cs]
            indent_style = space
            indent_size = 4
            charset = utf-8
            """,
            ct);

        var pipeName = $"ec-test-{Guid.NewGuid():N}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var service = CreateService(pipeName);
        await service.StartAsync(cts.Token);

        try
        {
            var response = await SendRequestAsync(
                pipeName,
                new EditorConfigRequest(Path.Combine(_tempRoot, "file.cs"), ["indent_style"]),
                ct);

            Assert.Multiple(
                () => Assert.NotNull(response),
                () => Assert.Single(response!.Properties),
                () => Assert.Equal("space", response!.Properties["indent_style"])
            );
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Should_return_empty_for_invalid_json()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeName = $"ec-test-{Guid.NewGuid():N}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var service = CreateService(pipeName);
        await service.StartAsync(cts.Token);

        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(ct);

            var requestBytes = Encoding.UTF8.GetBytes("not valid json");
            await WorkspaceService.WriteMessageAsync(client, requestBytes, ct);

            var responseBytes = await WorkspaceService.ReadMessageAsync(client, ct);

            Assert.Multiple(
                () => Assert.NotNull(responseBytes),
                () =>
                {
                    var response = JsonSerializer.Deserialize<EditorConfigResponse>(responseBytes!, JsonOptions);

                    Assert.NotNull(response);
                    Assert.Empty(response!.Properties);
                }
            );
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
        ];

        return new WorkspaceService(config, handlers, NullLogger<WorkspaceService>.Instance);
    }

    private static async Task<EditorConfigResponse?> SendRequestAsync(
        string pipeName,
        EditorConfigRequest request,
        CancellationToken ct)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(ct);

        var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonOptions));
        await WorkspaceService.WriteMessageAsync(client, requestBytes, ct);

        var responseBytes = await WorkspaceService.ReadMessageAsync(client, ct);

        return responseBytes is null
            ? null
            : JsonSerializer.Deserialize<EditorConfigResponse>(responseBytes, JsonOptions);
    }

    private static async Task<McpToolsResponse?> SendMcpToolsRequestAsync(
        string pipeName,
        McpToolsRequest request,
        CancellationToken ct)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(ct);

        var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonOptions));
        await WorkspaceService.WriteMessageAsync(client, requestBytes, ct);

        var responseBytes = await WorkspaceService.ReadMessageAsync(client, ct);

        return responseBytes is null
            ? null
            : JsonSerializer.Deserialize<McpToolsResponse>(responseBytes, JsonOptions);
    }

    [Fact]
    public async Task Should_return_run_for_enabled_tool()
    {
        var ct = TestContext.Current.CancellationToken;

        await File.WriteAllTextAsync(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.cs]
            indent_style = space
            """,
            ct);

        var pipeName = $"ec-test-{Guid.NewGuid():N}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var service = CreateService(pipeName);
        await service.StartAsync(cts.Token);

        try
        {
            var response = await SendMcpToolsRequestAsync(
                pipeName,
                new McpToolsRequest(
                    ["check-style"],
                    Path.Combine(_tempRoot, "File.cs"),
                    ["indent_style"]),
                ct);

            Assert.Multiple(
                () => Assert.NotNull(response),
                () =>
                {
                    Assert.True(response!.Tools["check-style"]);
                    Assert.NotNull(response.EditorConfig);
                    Assert.Equal("space", response.EditorConfig!["indent_style"]);
                }
            );
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Should_return_skip_for_disabled_tool_without_keys()
    {
        var ct = TestContext.Current.CancellationToken;

        await File.WriteAllTextAsync(
            Path.Combine(_tempRoot, ".autocontext.json"),
            """
            {
                "mcpTools": {
                    "check-style": false
                }
            }
            """,
            ct);

        var pipeName = $"ec-test-{Guid.NewGuid():N}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var service = CreateService(pipeName);
        await service.StartAsync(cts.Token);

        try
        {
            var response = await SendMcpToolsRequestAsync(
                pipeName,
                new McpToolsRequest(
                    ["check-style"],
                    Path.Combine(_tempRoot, "File.cs")),
                ct);

            Assert.Multiple(
                () => Assert.NotNull(response),
                () => Assert.False(response!.Tools["check-style"]),
                () => Assert.Null(response!.EditorConfig)
            );
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Should_return_editorconfig_for_disabled_tool_with_keys()
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

        await File.WriteAllTextAsync(
            Path.Combine(_tempRoot, ".autocontext.json"),
            """
            {
                "mcpTools": {
                    "check-style": false
                }
            }
            """,
            ct);

        var pipeName = $"ec-test-{Guid.NewGuid():N}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var service = CreateService(pipeName);
        await service.StartAsync(cts.Token);

        try
        {
            var response = await SendMcpToolsRequestAsync(
                pipeName,
                new McpToolsRequest(
                    ["check-style"],
                    Path.Combine(_tempRoot, "File.cs"),
                    ["indent_size"]),
                ct);

            Assert.Multiple(
                () => Assert.NotNull(response),
                () => Assert.False(response!.Tools["check-style"]),
                () =>
                {
                    Assert.NotNull(response!.EditorConfig);
                    Assert.Equal("4", response.EditorConfig!["indent_size"]);
                });
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Should_handle_multiple_tools_with_different_modes()
    {
        var ct = TestContext.Current.CancellationToken;

        await File.WriteAllTextAsync(
            Path.Combine(_tempRoot, ".editorconfig"),
            """
            root = true

            [*.cs]
            indent_style = space
            """,
            ct);

        await File.WriteAllTextAsync(
            Path.Combine(_tempRoot, ".autocontext.json"),
            """
            {
                "mcpTools": {
                    "disabled-with-keys": false,
                    "disabled-no-keys": false
                }
            }
            """,
            ct);

        var pipeName = $"ec-test-{Guid.NewGuid():N}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var service = CreateService(pipeName);
        await service.StartAsync(cts.Token);

        try
        {
            var response = await SendMcpToolsRequestAsync(
                pipeName,
                new McpToolsRequest(
                    ["enabled-tool", "disabled-with-keys", "disabled-no-keys"],
                    Path.Combine(_tempRoot, "File.cs"),
                    ["indent_style"]),
                ct);

            Assert.Multiple(
                () => Assert.NotNull(response),
                () => Assert.Equal(3, response!.Tools.Count),
                () => Assert.True(response!.Tools["enabled-tool"]),
                () => Assert.False(response!.Tools["disabled-with-keys"]),
                () => Assert.False(response!.Tools["disabled-no-keys"]),
                () =>
                {
                    Assert.NotNull(response!.EditorConfig);
                    Assert.Equal("space", response.EditorConfig!["indent_style"]);
                });
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Should_resolve_tool_modes_when_file_path_is_missing()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeName = $"ec-test-{Guid.NewGuid():N}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var service = CreateService(pipeName);
        await service.StartAsync(cts.Token);

        try
        {
            var response = await SendMcpToolsRequestAsync(
                pipeName,
                new McpToolsRequest(["check-style"]),
                ct);

            Assert.Multiple(
                () => Assert.NotNull(response),
                () => Assert.True(response!.Tools["check-style"]),
                () => Assert.Null(response!.EditorConfig));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }
}
