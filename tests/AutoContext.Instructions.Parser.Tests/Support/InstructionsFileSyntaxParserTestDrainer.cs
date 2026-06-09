namespace AutoContext.Instructions.Parser.Tests.Support;

using AutoContext.Instructions.Parser.Syntax;

internal static class InstructionsFileSyntaxParserTestDrainer
{
    public static Task<List<InstructionsFileSyntaxSpan>> DrainAsync(InstructionsFileSyntaxParser parser, string text)
    {
        var tree = parser.Parse(text, TestContext.Current.CancellationToken);

        return Task.FromResult<List<InstructionsFileSyntaxSpan>>([.. tree.Frontmatter, .. tree.Body]);
    }

    public static async Task<List<InstructionsFileSyntaxSpan>> DrainFileAsync(InstructionsFileSyntaxParser parser, string path)
    {
        var tree = await parser.ParseFileAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(false);

        return [.. tree.Frontmatter, .. tree.Body];
    }

    public static Task<InstructionsFileSyntaxTree> DrainTreeAsync(InstructionsFileSyntaxParser parser, string text)
        => Task.FromResult(parser.Parse(text, TestContext.Current.CancellationToken));
}
