namespace AutoContext.Instructions.Parser.Tests.Support;

internal static class InstructionsFileSpanStream
{
    public static IEnumerable<InstructionsFileParsedSpan> From(string text)
        => new InstructionsFileSpanParser().Parse(text, TestContext.Current.CancellationToken);
}
