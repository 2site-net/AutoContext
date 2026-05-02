namespace AutoContext.Framework.Tests.Logging;

using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;

using AutoContext.Framework.Logging;
using AutoContext.Framework.Tests.Testing.Utils;

using Microsoft.Extensions.Logging;

public sealed class LoggingClientTests
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    [Fact]
    public async Task Should_send_greeting_then_log_entry_over_the_pipe()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = NewPipeName();

        await using var server = CreateServer(pipeName);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);

        await using var client = NewClient(pipeName, "Test.Worker.Greet");

        await acceptTask;

        client.Post(new LogEntry(
            Category: "AutoContext.Demo",
            Level: LogLevel.Information,
            Message: "hello pipe",
            Exception: null,
            CorrelationId: null));

        var (greeting, records) = await ReadLinesAsync(server, expected: 2, cancellationToken);

        Assert.Multiple(
            () => Assert.Equal("Test.Worker.Greet", greeting!["clientName"]!.GetValue<string>()),
            () => Assert.Equal("AutoContext.Demo", records[0]!["category"]!.GetValue<string>()),
            () => Assert.Equal("Information", records[0]!["level"]!.GetValue<string>()),
            () => Assert.Equal("hello pipe", records[0]!["message"]!.GetValue<string>()),
            () => Assert.Null(records[0]!["exception"]),
            () => Assert.Null(records[0]!["correlationId"]));
    }

    [Fact]
    public async Task Should_propagate_correlation_id_into_wire_record()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = NewPipeName();

        await using var server = CreateServer(pipeName);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);

        await using var client = NewClient(pipeName, "Test.Worker.Corr");

        await acceptTask;

        client.Post(new LogEntry(
            Category: "AutoContext.Demo",
            Level: LogLevel.Information,
            Message: "scoped",
            Exception: null,
            CorrelationId: "abcd1234"));

        var (_, records) = await ReadLinesAsync(server, expected: 2, cancellationToken);

        Assert.Equal("abcd1234", records[0]!["correlationId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Should_serialise_exception_when_record_carries_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = NewPipeName();

        await using var server = CreateServer(pipeName);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);

        await using var client = NewClient(pipeName, "Test.Worker.Ex");

        await acceptTask;

        var ex = new InvalidOperationException("boom");
        client.Post(new LogEntry("Cat", LogLevel.Error, "oh no", ex, CorrelationId: null));

        var (_, records) = await ReadLinesAsync(server, expected: 2, cancellationToken);
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
            client.Post(new LogEntry("Cat", LogLevel.Trace, $"msg {i}", null, CorrelationId: null));
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
            client.Post(new LogEntry("Cat", LogLevel.Information, "stranded", null, CorrelationId: null));
        }

        sw.Stop();

        // Dispose's hard cap is 2s; allow generous CI slack.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(6),
            $"DisposeAsync took {sw.Elapsed.TotalSeconds:F2}s — expected < 6s.");
    }

    private static LoggingClient NewClient(string pipeName, string clientName) =>
        new(pipeName, clientName);

    private static NamedPipeServerStream CreateServer(string pipeName) =>
        TestPipeServer.Create(pipeName, PipeDirection.In);

    private static string NewPipeName() => TestPipeServer.UniqueName("actx-logsrv-test");

    private static async Task<(JsonNode? Greeting, List<JsonNode?> Records)> ReadLinesAsync(
        Stream stream,
        int expected,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Utf8NoBom, leaveOpen: true);
        var lines = new List<string>();

        // Cap the wait — if the client never wrote, we'd hang forever.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
        var records = lines.Skip(1).Select(line => JsonNode.Parse(line)).ToList();
        return (greeting, records);
    }
}
