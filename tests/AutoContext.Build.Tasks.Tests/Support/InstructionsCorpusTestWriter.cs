namespace AutoContext.Build.Tasks.Tests.Support;

using System.Text;

public static class InstructionsCorpusTestWriter
{
    public static void WriteInstruction(
        string directory,
        string fileName,
        string name,
        string description,
        string? applyTo = null,
        string body = "# Heading\n\nBody text.\n")
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append("name: \"").Append(name).Append("\"\n");
        builder.Append("description: \"").Append(description).Append("\"\n");
        if (applyTo is not null)
        {
            builder.Append("applyTo: \"").Append(applyTo).Append("\"\n");
        }

        builder.Append("---\n");
        builder.Append(body);
        File.WriteAllText(Path.Combine(directory, fileName), builder.ToString());
    }

    public static void WriteChangelog(string directory, string key)
        => File.WriteAllText(Path.Combine(directory, key + ".CHANGELOG.md"), "# Changelog\n");
}
