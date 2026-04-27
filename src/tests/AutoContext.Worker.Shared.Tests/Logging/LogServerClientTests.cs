namespace AutoContext.Worker.Shared.Tests.Logging;

using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;

using AutoContext.Worker.Hosting;
using AutoContext.Worker.Logging;

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class LogServerClientTests
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    [Fact]
    public async Task Should_send_greeting_then_log_record_over_the_pipe()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeName = NewPipeName();

        await using var server = CreateServer(pipeName);
        var acceptTask = server.WaitForConnectionAsync(ct);

        await using var client = NewClient(pipeName, "Test.Worker.Greet");

        await acceptTask;

        client.Enqueue(new LogRecord(
            Category: "AutoContext.Demo",
            Level: LogLevel.Information,
            Message: "hello pipe",
            Exception: null));

        var (greeting, records) = await ReadLinesAsync(server, expected: 2, ct);

        Assert.Multiple(
            () => Assert.Equal("Test.Worker.Greet", greeting!["clientName"]!.GetValue<string>()),
            () => Assert.Equal("AutoContext.Demo", records[0]!["category"]!.GetValue<string>()),
            () => Assert.Equal("Information", records[0]!["level"]!.GetValue<string>()),
            () => Assert.Equal("hello pipe", records[0]!["message"]!.GetValue<string>()),
            () => Assert.Null(records[0]!["exception"]));
    }

    [Fact]
    public async Task Should_serialise_exception_when_record_carries_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeName = NewPipeName();

        await using var server = CreateServer(pipeName);
        var acceptTask = server.WaitForConnectionAsync(ct);

        await using var client = NewClient(pipeName, "Test.Worker.Ex");

        await acceptTask;

        var ex = new InvalidOperationException("boom");
        client.Enqueue(new LogRecord("Cat", LogLevel.Error, "oh no", ex));

        var (_, records) = await ReadLinesAsync(server, expected: 2, ct);
        var serialised = records[0]!["exception"]!.GetValue<string>();

        Assert.Multiple(
            () => Assert.Equal("Error", records[0]!["level"]!.GetValue<string>()),
            () => Assert.Contains("InvalidOperationException", serialised, StringComparison.Ordinal),
            () => Assert.Contains("boom", serialised, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_not_throw_when_log_pipe_is_empty()
    {
        await using var client = NewClient(pipeName: string.Empty, clientName: "Test.Worker.Standalone");

        // Should accept records without blocking and without exceptions.
        for (var i = 0; i < 10; i++)
        {
            client.Enqueue(new LogRecord("Cat", LogLevel.Trace, $"msg {i}", null));
        }

        // DisposeAsync (via await using) must complete cleanly even though
        // there is no pipe server — drain falls back to stderr internally.
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Should_dispose_cleanly_when_no_server_is_listening()
    {
        // No server created — the connect will time out.
        var pipeName = NewPipeName();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await using (var client = NewClient(pipeName, "Test.Worker.Orphan"))
        {
            client.Enqueue(new LogRecord("Cat", LogLevel.Information, "stranded", null));
        }

        sw.Stop();

        // Dispose's hard cap is 2s; allow generous CI slack.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(6),
            $"DisposeAsync took {sw.Elapsed.TotalSeconds:F2}s — expected < 6s.");
    }

    private static LogServerClient NewClient(string pipeName, string clientName)
    {
        var options = Options.Create(new WorkerHostOptions { LogPipe = pipeName });
        return new LogServerClient(options, new FakeEnv { ApplicationName = clientName });
    }

    private static NamedPipeServerStream CreateServer(string pipeName) => new(
        pipeName,
        PipeDirection.In,
        maxNumberOfServerInstances: 1,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous);

    private static string NewPipeName() => $"actx-logsrv-test-{Guid.NewGuid():N}"[..32];

    private static async Task<(JsonNode? Greeting, List<JsonNode?> Records)> ReadLinesAsync(
        Stream stream,
        int expected,
        CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Utf8NoBom, leaveOpen: true);
        var lines = new List<string>();

        // Cap the wait — if the client never wrote, we'd hang forever.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        for (var i = 0; i < expected; i++)
        {
            var line = await reader.ReadLineAsync(timeoutCts.Token);
            if (line is null)
            {
                break;
            }
            lines.Add(line);
        }

        Assert.Equal(expected, lines.Count);

        var greeting = JsonNode.Parse(lines[0]);
        var records = lines.Skip(1).Select(JsonNode.Parse).ToList();
        return (greeting, records);
    }

    private sealed class FakeEnv : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test.Worker";
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
