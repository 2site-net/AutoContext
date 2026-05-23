namespace AutoContext.Framework.Logging.Tests;

using System.IO.Pipes;

using AutoContext.Framework.Logging;
using AutoContext.Framework.Logging.Tests.Support;
using AutoContext.Framework.Tests.Support.Pipes;

using Microsoft.Extensions.Logging;

public sealed class LoggingClientTests
{

    [Fact]
    public async Task Should_send_greeting_then_log_entry_over_the_pipe()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = PipeTestServer.UniqueName("actx-logsrv-test");

        await using var server = PipeTestServer.Create(pipeName, PipeDirection.In);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);

        await using var client = LoggingClientTestFactory.Create(pipeName, "Test.Worker.Greet");

        await acceptTask;

        client.Post(new LogEntry(
            Category: "AutoContext.Demo",
            Level: LogLevel.Information,
            Message: "hello pipe",
            Exception: null,
            CorrelationId: null));

        var (greeting, records) = await LoggingClientTestReader.ReadLinesAsync(server, expected: 2, cancellationToken);

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
        var pipeName = PipeTestServer.UniqueName("actx-logsrv-test");

        await using var server = PipeTestServer.Create(pipeName, PipeDirection.In);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);

        await using var client = LoggingClientTestFactory.Create(pipeName, "Test.Worker.Corr");

        await acceptTask;

        client.Post(new LogEntry(
            Category: "AutoContext.Demo",
            Level: LogLevel.Information,
            Message: "scoped",
            Exception: null,
            CorrelationId: "abcd1234"));

        var (_, records) = await LoggingClientTestReader.ReadLinesAsync(server, expected: 2, cancellationToken);

        Assert.Equal("abcd1234", records[0]!["correlationId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Should_serialise_exception_when_record_carries_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = PipeTestServer.UniqueName("actx-logsrv-test");

        await using var server = PipeTestServer.Create(pipeName, PipeDirection.In);
        var acceptTask = server.WaitForConnectionAsync(cancellationToken);

        await using var client = LoggingClientTestFactory.Create(pipeName, "Test.Worker.Ex");

        await acceptTask;

        var ex = new InvalidOperationException("boom");
        client.Post(new LogEntry("Cat", LogLevel.Error, "oh no", ex, CorrelationId: null));

        var (_, records) = await LoggingClientTestReader.ReadLinesAsync(server, expected: 2, cancellationToken);
        var serialised = records[0]!["exception"]!.GetValue<string>();

        Assert.Multiple(
            () => Assert.Equal("Error", records[0]!["level"]!.GetValue<string>()),
            () => Assert.Contains("InvalidOperationException", serialised, StringComparison.Ordinal),
            () => Assert.Contains("boom", serialised, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_not_throw_when_log_pipe_is_empty()
    {
        await using var client = LoggingClientTestFactory.Create(pipeName: string.Empty, clientName: "Test.Worker.Standalone");

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
        var pipeName = PipeTestServer.UniqueName("actx-logsrv-test");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await using (var client = LoggingClientTestFactory.Create(pipeName, "Test.Worker.Orphan"))
        {
            client.Post(new LogEntry("Cat", LogLevel.Information, "stranded", null, CorrelationId: null));
        }

        sw.Stop();

        // Dispose's hard cap is 2s; allow generous CI slack.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(6),
            $"DisposeAsync took {sw.Elapsed.TotalSeconds:F2}s — expected < 6s.");
    }
}
