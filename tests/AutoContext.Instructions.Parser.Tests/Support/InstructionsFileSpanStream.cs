namespace AutoContext.Instructions.Parser.Tests.Support;

internal static class InstructionsFileSpanStream
{
    public static IEnumerable<InstructionsFileParsedSpan> From(string text)
        => new InstructionsFileSyntaxParser().Parse(text, TestContext.Current.CancellationToken);

    public static InstructionsFileParsedContent Parse(string text)
        => new InstructionsFileParser().Parse(From(text));
}
