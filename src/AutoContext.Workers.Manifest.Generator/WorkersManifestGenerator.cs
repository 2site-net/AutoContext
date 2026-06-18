namespace AutoContext.Workers.Manifest.Generator;

using System.Text;

using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates one build-time generation pass: aggregates the per-worker
/// <c>.autocontext-worker.json</c> descriptors under the
/// <c>src/AutoContext.Worker.*</c> projects via <see cref="IWorkerDescriptorScanner"/>,
/// serialises the manifest via <see cref="IWorkersManifestSerializer"/>, and
/// writes it to disk only when the bytes differ from the file already there.
/// The generator owns the process exit-code contract the MSBuild <c>&lt;Exec&gt;</c>
/// caller observes: <c>0</c> on success, <c>1</c> on a scan fault (a missing or
/// invalid descriptor, or a duplicate worker id), <c>2</c> on a usage error.
/// </summary>
internal sealed partial class WorkersManifestGenerator(
    IWorkerDescriptorScanner scanner,
    IWorkersManifestSerializer manifestSerializer,
    ILogger<WorkersManifestGenerator> logger)
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Runs the generation pass described by the positional <paramref name="args"/>:
    /// <c>[workers-source-directory, workers-json-output-path]</c>.
    /// </summary>
    /// <param name="args">The positional command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is
    /// <see langword="null"/>.</exception>
    public Task<int> RunAsync(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count != 2)
        {
            LogUsage(logger);
            return Task.FromResult(2);
        }

        var sourceDirectory = args[0];
        var outputPath = args[1];

        try
        {
            var manifest = scanner.Scan(sourceDirectory);
            WriteIfChanged(outputPath, manifestSerializer.Serialize(manifest));

            return Task.FromResult(0);
        }
        catch (InvalidOperationException exception)
        {
            LogScanFault(logger, exception.Message);
            return Task.FromResult(1);
        }
    }

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "{Message}")]
    private static partial void LogScanFault(ILogger logger, string message);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "usage: workers-manifest-gen <workers-source-directory> <workers-json-output-path>")]
    private static partial void LogUsage(ILogger logger);

    private static void WriteIfChanged(string outputPath, string json)
    {
        var directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(outputPath) && File.ReadAllText(outputPath, Utf8NoBom) == json)
        {
            return;
        }

        File.WriteAllText(outputPath, json, Utf8NoBom);
    }
}
