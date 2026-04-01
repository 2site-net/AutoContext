namespace SharpPilot.WorkspaceServer.Tests;

using System.IO.Pipes;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class WorkspaceServiceTests : IDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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

            Assert.NotNull(response);
            Assert.Single(response!.Properties);
            Assert.Equal("space", response.Properties["indent_style"]);
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

            Assert.NotNull(responseBytes);

            var response = JsonSerializer.Deserialize<EditorConfigResponse>(responseBytes!, s_jsonOptions);

            Assert.NotNull(response);
            Assert.Empty(response!.Properties);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static WorkspaceService CreateService(string pipeName)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("pipe", pipeName)])
            .Build();

        return new WorkspaceService(config, new EditorConfigResolver(), NullLogger<WorkspaceService>.Instance);
    }

    private static async Task<EditorConfigResponse?> SendRequestAsync(
        string pipeName,
        EditorConfigRequest request,
        CancellationToken ct)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(ct);

        var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, s_jsonOptions));
        await WorkspaceService.WriteMessageAsync(client, requestBytes, ct);

        var responseBytes = await WorkspaceService.ReadMessageAsync(client, ct);

        return responseBytes is null
            ? null
            : JsonSerializer.Deserialize<EditorConfigResponse>(responseBytes, s_jsonOptions);
    }
}
