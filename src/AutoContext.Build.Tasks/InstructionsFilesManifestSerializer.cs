namespace AutoContext.Build.Tasks;

using System.Globalization;
using System.Text;

/// <summary>
/// Serialises an <see cref="InstructionsFilesManifest"/> to deterministic,
/// two-space-indented JSON with a trailing newline. The writer is hand-rolled
/// (no <c>System.Text.Json</c> dependency) so the netstandard2.0 MSBuild task
/// loads cleanly under both MSBuild-Full-Framework and MSBuild-Core.
/// </summary>
internal static class InstructionsFilesManifestSerializer
{
    /// <summary>
    /// Serialises <paramref name="manifest"/> to JSON text.
    /// </summary>
    /// <param name="manifest">The manifest to serialise.</param>
    /// <returns>The JSON document, newline-terminated.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is
    /// <see langword="null"/>.</exception>
    public static string Serialize(InstructionsFilesManifest manifest)
    {
        if (manifest is null)
        {
            throw new ArgumentNullException(nameof(manifest));
        }

        var builder = new StringBuilder();
        builder.Append("{\n");
        builder.Append("  \"schemaVersion\": ");
        AppendString(builder, manifest.SchemaVersion);
        builder.Append(",\n");
        builder.Append("  \"instructions\": [");

        if (manifest.Instructions.Count == 0)
        {
            builder.Append("]\n}\n");
            return builder.ToString();
        }

        builder.Append('\n');

        for (var i = 0; i < manifest.Instructions.Count; i++)
        {
            AppendEntry(builder, manifest.Instructions[i]);
            builder.Append(i == manifest.Instructions.Count - 1 ? "\n" : ",\n");
        }

        builder.Append("  ]\n}\n");
        return builder.ToString();
    }

    private static void AppendBoolField(StringBuilder builder, string name, bool value)
    {
        builder.Append("      \"").Append(name).Append("\": ");
        builder.Append(value ? "true" : "false");
    }

    private static void AppendEntry(StringBuilder builder, InstructionsFileManifestEntry entry)
    {
        builder.Append("    {\n");
        AppendStringField(builder, "key", entry.Key);
        builder.Append(",\n");
        AppendStringField(builder, "fileName", entry.FileName);
        builder.Append(",\n");
        AppendStringField(builder, "name", entry.Name);
        builder.Append(",\n");
        AppendStringField(builder, "version", entry.Version);
        builder.Append(",\n");
        AppendStringField(builder, "description", entry.Description);
        builder.Append(",\n");

        if (entry.ApplyTo is not null)
        {
            AppendStringField(builder, "applyTo", entry.ApplyTo);
            builder.Append(",\n");
        }

        AppendBoolField(builder, "hasChangelog", entry.HasChangelog);
        builder.Append(",\n");
        AppendStringField(builder, "contentHash", entry.ContentHash);
        builder.Append(",\n");
        AppendBoolField(builder, "alwaysAttached", entry.AlwaysAttached);
        builder.Append("\n    }");
    }

    private static void AppendOther(StringBuilder builder, char c)
    {
        if (c < ' ')
        {
            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            return;
        }

        builder.Append(c);
    }

    private static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"');

        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    AppendOther(builder, c);
                    break;
            }
        }

        builder.Append('"');
    }

    private static void AppendStringField(StringBuilder builder, string name, string value)
    {
        builder.Append("      \"").Append(name).Append("\": ");
        AppendString(builder, value);
    }
}
