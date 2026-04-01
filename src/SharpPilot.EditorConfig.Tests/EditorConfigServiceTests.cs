namespace SharpPilot.EditorConfig.Tests;

using System.IO.Pipes;
using System.Text;
using System.Text.Json;

public sealed class EditorConfigServiceTests : IDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"ec-svc-test-{Guid.NewGuid():N}");

    public EditorConfigServiceTests()
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
        var service = new EditorConfigService(pipeName, cts.Token);
        var serviceTask = service.RunAsync();

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
            await StopServiceAsync(cts, serviceTask);
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
        var service = new EditorConfigService(pipeName, cts.Token);
        var serviceTask = service.RunAsync();

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
            await StopServiceAsync(cts, serviceTask);
        }
    }

    [Fact]
    public async Task Should_return_empty_for_invalid_json()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeName = $"ec-test-{Guid.NewGuid():N}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var service = new EditorConfigService(pipeName, cts.Token);
        var serviceTask = service.RunAsync();

        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(ct);

            var requestBytes = Encoding.UTF8.GetBytes("not valid json");
            await EditorConfigService.WriteMessageAsync(client, requestBytes, ct);

            var responseBytes = await EditorConfigService.ReadMessageAsync(client, ct);

            Assert.NotNull(responseBytes);

            var response = JsonSerializer.Deserialize<EditorConfigResponse>(responseBytes!, s_jsonOptions);

            Assert.NotNull(response);
            Assert.Empty(response!.Properties);
        }
        finally
        {
            await StopServiceAsync(cts, serviceTask);
        }
    }

    private static async Task<EditorConfigResponse?> SendRequestAsync(
        string pipeName,
        EditorConfigRequest request,
        CancellationToken ct)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(ct);

        var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, s_jsonOptions));
        await EditorConfigService.WriteMessageAsync(client, requestBytes, ct);

        var responseBytes = await EditorConfigService.ReadMessageAsync(client, ct);

        return responseBytes is null
            ? null
            : JsonSerializer.Deserialize<EditorConfigResponse>(responseBytes, s_jsonOptions);
    }

    private static async Task StopServiceAsync(CancellationTokenSource cts, Task serviceTask)
    {
        await cts.CancelAsync();

        // The cancellation callback inside the service disposes the
        // waiting pipe, so no dummy connection is needed.
        var completed = await Task.WhenAny(serviceTask, Task.Delay(TimeSpan.FromSeconds(5)));

        if (completed != serviceTask)
        {
            throw new TimeoutException("EditorConfig service did not shut down within 5 seconds.");
        }

        await serviceTask;
    }
}
