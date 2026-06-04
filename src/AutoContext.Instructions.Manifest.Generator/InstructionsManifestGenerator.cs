namespace AutoContext.Instructions.Manifest.Generator;

using System.Text;

using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates one build-time generation pass: builds the wire-shape manifest
/// via <see cref="IInstructionsListBuilder"/>, enriches it into the metadata
/// catalogue via <see cref="IInstructionsMetadataBuilder"/>, serialises each with
/// its serializer, and writes both results to disk only when the bytes differ
/// from the files already there. The generator owns the process exit-code
/// contract the MSBuild <c>&lt;Exec&gt;</c> caller observes: <c>0</c> on success,
/// <c>1</c> on a curatorial fault, <c>2</c> on a usage error.
/// </summary>
internal sealed partial class InstructionsManifestGenerator(
    IInstructionsListBuilder builder,
    IInstructionsManifestSerializer manifestSerializer,
    IInstructionsMetadataBuilder metadataBuilder,
    IInstructionsMetadataSerializer metadataSerializer,
    ILogger<InstructionsManifestGenerator> logger)
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Runs the generation pass described by the positional
    /// <paramref name="args"/>: <c>[corpus-directory,
    /// instructions-files-json-path, instructions-files-metadata-json-path]</c>.
    /// </summary>
    /// <param name="args">The positional command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is
    /// <see langword="null"/>.</exception>
    public int Run(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count != 3)
        {
            LogUsage(logger);
            return 2;
        }

        var corpusDirectory = args[0];
        var manifestOutputPath = args[1];
        var metadataOutputPath = args[2];

        try
        {
            var manifest = builder.Build(corpusDirectory);
            WriteIfChanged(manifestOutputPath, manifestSerializer.Serialize(manifest));

            var metadata = metadataBuilder.Build(manifest, corpusDirectory);
            WriteIfChanged(metadataOutputPath, metadataSerializer.Serialize(metadata));

            return 0;
        }
        catch (InvalidOperationException exception)
        {
            LogCuratorialFault(logger, exception.Message);
            return 1;
        }
    }

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "{Message}")]
    private static partial void LogCuratorialFault(ILogger logger, string message);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "usage: instructions-manifest-gen <corpus-directory> <instructions-files-json-path> <instructions-files-metadata-json-path>")]
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
