namespace AutoContext.Instructions.Parser.Tests.Support;

internal static class InstructionsFileSpanParserTestDrainer
{
    public static Task<List<InstructionsFileParsedSpan>> DrainAsync(InstructionsFileSpanParser parser, string text)
        => DrainAsync(parser, new StringReader(text));

    public static async Task<List<InstructionsFileParsedSpan>> DrainAsync(InstructionsFileSpanParser parser, TextReader reader)
    {
        var spans = new List<InstructionsFileParsedSpan>();

        await foreach (var span in parser.ParseAsync(reader, TestContext.Current.CancellationToken).ConfigureAwait(false))
        {
            spans.Add(span);
        }

        return spans;
    }

    public static async Task<List<InstructionsFileParsedSpan>> DrainFileAsync(InstructionsFileSpanParser parser, string path)
    {
        var spans = new List<InstructionsFileParsedSpan>();

        await foreach (var span in parser.ParseFileAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(false))
        {
            spans.Add(span);
        }

        return spans;
    }
}
