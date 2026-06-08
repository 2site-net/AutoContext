namespace AutoContext.Instructions.Parser.Tests.Support;

internal static class InstructionsFileSyntaxParserFakeData
{
    public const string AllKinds =
        "---\n" +
        "name: \"x (v1.0.0)\"\n" +
        "---\n" +
        "# Title\n" +
        "\n" +
        "Body text with a [foo.instructions.md#INST0001] reference.\n" +
        "\n" +
        "## Rules\n" +
        "\n" +
        "- [INST0002] **Do** the thing.\n" +
        "\n" +
        "- **Do** a plain thing.\n" +
        "\n" +
        "### Subsection\n" +
        "\n" +
        "More body text.\n" +
        "\n" +
        "```\n" +
        "# not a heading\n" +
        "```\n";
}
