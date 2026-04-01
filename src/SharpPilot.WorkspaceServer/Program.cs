using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using SharpPilot.WorkspaceServer;

var builder = Host.CreateApplicationBuilder(args);

// stdout is reserved for the ready-signal protocol — send all log
// output to stderr, which the extension captures and forwards to the
// output channel.  Suppress the host's "Application started" banners.
builder.Services.Configure<ConsoleLoggerOptions>(o =>
    o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.Configure<ConsoleLifetimeOptions>(o =>
    o.SuppressStatusMessages = true);

builder.Services.AddSingleton<EditorConfigResolver>();
builder.Services.AddHostedService<WorkspaceService>();

var host = builder.Build();

// Signal readiness — the extension reads this to know the pipe is active.
var pipeName = builder.Configuration["pipe"]
    ?? throw new InvalidOperationException("Missing required argument: --pipe");

Console.WriteLine(JsonSerializer.Serialize(
    new { pipe = pipeName },
    WorkspaceService.JsonOptions));

await host.RunAsync().ConfigureAwait(false);
