using AutoContext.Workers.Manifest.Generator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(static options => options.SingleLine = true);
builder.Logging.Services.Configure<ConsoleLoggerOptions>(
    static options => options.LogToStandardErrorThreshold = LogLevel.Warning);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddWorkersManifestGenerator();

using var host = builder.Build();

return await host.Services
    .GetRequiredService<WorkersManifestGenerator>()
    .RunAsync(args)
    .ConfigureAwait(false);
