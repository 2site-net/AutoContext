namespace AutoContext.Build.Tasks;

using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

/// <summary>
/// MSBuild task that scans the curated instruction corpus and writes the
/// wire-shape <c>instructions-files.json</c> manifest the engine serves through
/// <c>Instructions.List</c>. The task is idempotent: it rewrites the output only
/// when the generated content differs from what is already on disk.
/// </summary>
public sealed class BuildInstructionsListTask : Task
{
    /// <summary>Gets or sets the curated corpus directory to scan.</summary>
    [Required]
    public string CorpusDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the path the generated manifest is written to.</summary>
    [Required]
    public string OutputPath { get; set; } = string.Empty;

    /// <inheritdoc />
    public override bool Execute()
    {
        try
        {
            var manifest = InstructionsListBuilder.Build(CorpusDirectory);
            WriteIfChanged(InstructionsFilesManifestSerializer.Serialize(manifest));
            return true;
        }
        catch (InvalidOperationException exception)
        {
            Log.LogError(exception.Message);
            return false;
        }
    }

    private void WriteIfChanged(string json)
    {
        var directory = Path.GetDirectoryName(OutputPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        if (File.Exists(OutputPath) && File.ReadAllText(OutputPath, encoding) == json)
        {
            return;
        }

        File.WriteAllText(OutputPath, json, encoding);
    }
}
