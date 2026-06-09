namespace AutoContext.Instructions.Parser.Tests.Support;

using AutoContext.Instructions.Parser.Model;
using AutoContext.Instructions.Parser.Syntax;

internal static class InstructionsFileSpanStream
{
    public static IEnumerable<InstructionsFileSyntaxSpan> From(string text)
        => new InstructionsFileSyntaxParser().Parse(text, TestContext.Current.CancellationToken);

    public static InstructionsFile Parse(string text)
        => InstructionsFile.FromSpans(From(text));
}
