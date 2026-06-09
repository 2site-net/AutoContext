namespace AutoContext.Instructions.Parser.Tests.Support;

using AutoContext.Instructions.Parser.Syntax;

internal static class InstructionsFileSyntaxParserTestDrainer
{
    public static Task<List<InstructionsFileSyntaxSpan>> DrainAsync(InstructionsFileSyntaxParser parser, string text)
        => Task.FromResult<List<InstructionsFileSyntaxSpan>>([.. parser.Parse(text, TestContext.Current.CancellationToken)]);

    public static async Task<List<InstructionsFileSyntaxSpan>> DrainFileAsync(InstructionsFileSyntaxParser parser, string path)
    {
        var spans = await parser.ParseFileAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(false);

        return [.. spans];
    }
}
