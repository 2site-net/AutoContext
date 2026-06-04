namespace AutoContext.Build.Tasks;

using System.Text.RegularExpressions;

/// <summary>
/// Reads the leading YAML frontmatter block of an instruction file into its
/// <c>name</c>, <c>description</c>, and <c>applyTo</c> fields. The parser is a
/// deliberate mirror of the extension's TypeScript reader: it matches simple
/// <c>key: "value"</c> lines and never interprets nested YAML structure.
/// </summary>
internal static class InstructionsFrontmatterParser
{
    private static readonly Regex FieldLine =
        new("^(\\w+):\\s*\"?([^\"\\r\\n]*)\"?\\s*$", RegexOptions.Compiled);

    private static readonly Regex FrontmatterBlock =
        new(@"^---\r?\n([\s\S]*?)\r?\n---", RegexOptions.Compiled);

    /// <summary>
    /// Parses <paramref name="content"/>'s leading frontmatter block.
    /// </summary>
    /// <param name="content">The full instruction file text.</param>
    /// <returns>The parsed frontmatter; all fields are <see langword="null"/>
    /// when no frontmatter block is present.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/>
    /// is <see langword="null"/>.</exception>
    public static InstructionsFrontmatter Parse(string content)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        var block = FrontmatterBlock.Match(content);

        if (!block.Success)
        {
            return new InstructionsFrontmatter(null, null, null);
        }

        string? name = null;
        string? description = null;
        string? applyTo = null;

        foreach (var line in block.Groups[1].Value.Split('\n'))
        {
            var field = FieldLine.Match(line.Trim());

            if (!field.Success)
            {
                continue;
            }

            switch (field.Groups[1].Value)
            {
                case "name":
                    name = field.Groups[2].Value;
                    break;
                case "description":
                    description = field.Groups[2].Value;
                    break;
                case "applyTo":
                    applyTo = field.Groups[2].Value;
                    break;
                default:
                    break;
            }
        }

        return new InstructionsFrontmatter(name, description, applyTo);
    }
}
