namespace AutoContext.Instructions.Parser.Tests.Support;

internal static class InstructionsFileSyntaxParserTestDrainer
{
    public static Task<List<InstructionsFileParsedSpan>> DrainAsync(InstructionsFileSyntaxParser parser, string text)
        => Task.FromResult<List<InstructionsFileParsedSpan>>([.. parser.Parse(text, TestContext.Current.CancellationToken)]);

    public static async Task<List<InstructionsFileParsedSpan>> DrainFileAsync(InstructionsFileSyntaxParser parser, string path)
    {
        var spans = await parser.ParseFileAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(false);

        return [.. spans];
    }
}
