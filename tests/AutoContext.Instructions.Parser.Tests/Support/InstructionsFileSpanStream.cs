namespace AutoContext.Instructions.Parser.Tests.Support;

internal static class InstructionsFileSpanStream
{
    public static IAsyncEnumerable<InstructionsFileParsedSpan> From(string text)
        => new InstructionsFileSpanParser().ParseAsync(new StringReader(text), TestContext.Current.CancellationToken);
}
