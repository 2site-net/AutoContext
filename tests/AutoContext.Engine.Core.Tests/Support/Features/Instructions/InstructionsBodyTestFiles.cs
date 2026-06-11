namespace AutoContext.Engine.Core.Tests.Support.Features.Instructions;

/// <summary>
/// The canonical instructions-file body used by
/// <c>InstructionsBodyProjectorTests</c>: a frontmatter block followed by
/// two <c>##</c> sections (<c>alpha</c>, <c>beta</c>) where the
/// <c>alpha</c> section carries two tagged rule bullets
/// (<c>INST0001</c>, <c>INST0002</c>). Writing it to disk lets the
/// projector exercise frontmatter stripping, section slicing, and
/// disabled-rule filtering against a single known shape.
/// </summary>
internal static class InstructionsBodyTestFiles
{
    public const string Body =
        """
        ---
        name: "testing (v1.0.0)"
        description: "Test file."
        ---
        # Title

        Intro paragraph.

        ## Alpha

        Alpha body line.

        - [INST0001] **Do** keep this rule.
        - [INST0002] **Don't** do the bad thing.

        ## Beta

        Beta body line.
        """;

    public static string Write(string directory, string fileName, string content)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
