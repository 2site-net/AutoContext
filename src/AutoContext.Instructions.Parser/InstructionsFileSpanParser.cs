namespace AutoContext.Instructions.Parser;

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// A lower-level, incremental, source-positioned lexer for instructions files. It
/// consumes decoded text from a <see cref="TextReader"/> and yields
/// <see cref="InstructionsFileParsedSpan"/> values addressed by whole-file,
/// zero-based, exclusive-ended coordinates (no frontmatter stripping, no newline
/// normalisation, so a <c>CRLF</c> pair counts as two characters).
/// <para>
/// Two emission layers are produced. The <em>block</em> layer
/// (<see cref="InstructionsFileSpanEmitLevel.Blocks"/>) is a gapless,
/// non-overlapping partition of the file — every character belongs to exactly one
/// block span, and anything that matches no richer structure becomes
/// <see cref="InstructionsFileSpanKind.Text"/>. The <em>token</em> layer
/// (<see cref="InstructionsFileSpanEmitLevel.Tokens"/>) is sparse and may nest
/// inside the blocks that contain it. A span is emitted only when its kind belongs
/// to a selected level <em>and</em> a selected scope; on overlap a container span
/// is always emitted before the spans it contains.
/// </para>
/// <para>
/// Fence handling mirrors the legacy parser: rule bullets are recognised
/// everywhere (fence-agnostic), while headings and references are recognised only
/// outside fenced code blocks (fence-aware).
/// </para>
/// </summary>
internal sealed partial class InstructionsFileSpanParser(
    InstructionsFileSpanEmitLevel emitLevel = InstructionsFileSpanEmitLevel.Full,
    InstructionsFileSpanEmitScope emitScope = InstructionsFileSpanEmitScope.All)
{
    private const int ReadBufferSize = 4096;
    private const int StackMaskThreshold = 256;

    private readonly bool _emitReferences = IsEmitted(emitLevel, emitScope, InstructionsFileSpanKind.Reference);
    private readonly bool _emitTags = IsEmitted(emitLevel, emitScope, InstructionsFileSpanKind.Tag);

    private enum ParsePhase
    {
        Start,
        Frontmatter,
        Body,
    }

    /// <summary>
    /// Streams the span decomposition of the text behind <paramref name="reader"/>,
    /// consuming it incrementally without materialising the whole file.
    /// </summary>
    /// <param name="reader">The decoded instructions text.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The span stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is
    /// <see langword="null"/>.</exception>
    public async IAsyncEnumerable<InstructionsFileParsedSpan> ParseAsync(
        TextReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var state = new ParserState();

        await foreach (var line in ReadPhysicalLinesAsync(reader, cancellationToken).ConfigureAwait(false))
        {
            foreach (var span in Advance(state, line))
            {
                yield return span;
            }
        }

        foreach (var span in Finish(state))
        {
            yield return span;
        }
    }

    /// <summary>
    /// Opens <paramref name="path"/> and streams its span decomposition. Owns the
    /// file I/O and delegates parsing to <see cref="ParseAsync"/>.
    /// </summary>
    /// <param name="path">The instructions file to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The span stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    public async IAsyncEnumerable<InstructionsFileParsedSpan> ParseFileAsync(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ReadBufferSize,
            useAsync: true);

        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

        await foreach (var span in ParseAsync(reader, cancellationToken).ConfigureAwait(false))
        {
            yield return span;
        }
    }

    private static void AddReferenceDrafts(PhysicalLine line, List<TokenDraft> tokens)
    {
        var content = line.Content.AsSpan();

        if (!content.Contains('#'))
        {
            return;
        }

        char[]? rented = null;
        var masked = content.Length <= StackMaskThreshold
            ? stackalloc char[content.Length]
            : (rented = ArrayPool<char>.Shared.Rent(content.Length)).AsSpan(0, content.Length);

        try
        {
            MaskInlineCode(content, masked);

            foreach (var match in GeneratedReferenceTokenRegex().EnumerateMatches(masked))
            {
                var token = content.Slice(match.Index, match.Length);
                var inner = token[1..^1];
                var separator = inner.IndexOf('#');
                var locator = inner[..separator];
                var fragment = inner[(separator + 1)..];
                var hasLocator = locator.Length > 0;
                var locatorValid = !hasLocator || IsValidLocator(locator);
                var fragmentLooksReference = fragment.StartsWith("INST", StringComparison.Ordinal)
                    || (fragment.Length > 0 && fragment[0] == '\'');

                if (!((hasLocator && locatorValid) || fragmentLooksReference))
                {
                    continue;
                }

                tokens.Add(new TokenDraft(
                    line.StartIndex + match.Index,
                    match.Length,
                    line.LineIndex,
                    token.ToString(),
                    InstructionsFileSpanKind.Reference));
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private static string BuildText(List<PhysicalLine> lines, int length)
    {
        var builder = new StringBuilder(length);

        foreach (var line in lines)
        {
            builder.Append(line.Content);
            builder.Append(line.Terminator);
        }

        return builder.ToString();
    }

    private static string BuildText(List<TextLine> lines, int length)
    {
        var builder = new StringBuilder(length);

        foreach (var entry in lines)
        {
            builder.Append(entry.Line.Content);
            builder.Append(entry.Line.Terminator);
        }

        return builder.ToString();
    }

    private static int FindClosingBacktickRun(ReadOnlySpan<char> buffer, int start, int fence)
    {
        var index = start;

        while (index < buffer.Length)
        {
            if (buffer[index] != '`')
            {
                index++;
                continue;
            }

            var run = index;
            var length = 0;

            while (index < buffer.Length && buffer[index] == '`')
            {
                length++;
                index++;
            }

            if (length == fence)
            {
                return run;
            }
        }

        return -1;
    }

    [GeneratedRegex(@"\[([^\]]*)\]")]
    private static partial Regex GeneratedBracketTagRegex();

    [GeneratedRegex("^```")]
    private static partial Regex GeneratedFenceRegex();

    [GeneratedRegex("^(\\w+):\\s*\"?([^\"\\r\\n]*)\"?\\s*$")]
    private static partial Regex GeneratedFrontmatterFieldRegex();

    [GeneratedRegex(@"^(#{1,3}) +(.+?)\s*$")]
    private static partial Regex GeneratedHeadingRegex();

    [GeneratedRegex(@"^(?:\.{1,2}/)?(?:[^/\s]+/)*[^/\s]+\.instructions\.md$")]
    private static partial Regex GeneratedLocatorFileRegex();

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex GeneratedLocatorKeyRegex();

    [GeneratedRegex(@"^[a-z][a-z0-9+.-]*://\S+$")]
    private static partial Regex GeneratedLocatorUriRegex();

    [GeneratedRegex(@"^[-*]\s\[(?!INST\d{4}\])[^\]]*\]\s*\*\*(Do|Don't)\*\*")]
    private static partial Regex GeneratedMalformedRuleBulletRegex();

    [GeneratedRegex(@"\[([^\[\]#]*)#([^\[\]]*)\](?![(\[:])")]
    private static partial Regex GeneratedReferenceTokenRegex();

    [GeneratedRegex(@"^[-*]\s(?:\[(INST\d{4})\]\s*)?\*\*(Do|Don't)\*\*")]
    private static partial Regex GeneratedRuleBulletRegex();

    private static bool IsEmitted(
        InstructionsFileSpanEmitLevel level,
        InstructionsFileSpanEmitScope scope,
        InstructionsFileSpanKind kind)
        => (level & LevelOf(kind)) != 0 && (scope & ScopeOf(kind)) != 0;

    private static bool IsValidLocator(ReadOnlySpan<char> locator)
        => GeneratedLocatorKeyRegex().IsMatch(locator)
        || GeneratedLocatorFileRegex().IsMatch(locator)
        || GeneratedLocatorUriRegex().IsMatch(locator);

    private static InstructionsFileSpanEmitLevel LevelOf(InstructionsFileSpanKind kind)
        => kind switch
        {
            InstructionsFileSpanKind.FrontmatterProperty
                or InstructionsFileSpanKind.FrontmatterKey
                or InstructionsFileSpanKind.FrontmatterValue
                or InstructionsFileSpanKind.Tag
                or InstructionsFileSpanKind.Reference => InstructionsFileSpanEmitLevel.Tokens,
            InstructionsFileSpanKind.Text
                or InstructionsFileSpanKind.FrontmatterBlock
                or InstructionsFileSpanKind.Heading1
                or InstructionsFileSpanKind.Heading2
                or InstructionsFileSpanKind.Heading3
                or InstructionsFileSpanKind.PlainRule
                or InstructionsFileSpanKind.TaggedRule => InstructionsFileSpanEmitLevel.Blocks,
            _ => InstructionsFileSpanEmitLevel.Blocks,
        };

    private static void MaskInlineCode(ReadOnlySpan<char> line, Span<char> destination)
    {
        line.CopyTo(destination);

        if (!line.Contains('`'))
        {
            return;
        }

        var index = 0;

        while (index < destination.Length)
        {
            if (destination[index] != '`')
            {
                index++;
                continue;
            }

            var open = index;
            var fence = 0;

            while (index < destination.Length && destination[index] == '`')
            {
                fence++;
                index++;
            }

            var close = FindClosingBacktickRun(destination, index, fence);

            if (close < 0)
            {
                break;
            }

            destination[open..(close + fence)].Fill(' ');
            index = close + fence;
        }
    }

    private static async IAsyncEnumerable<PhysicalLine> ReadPhysicalLinesAsync(
        TextReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new char[ReadBufferSize];
        var pending = new StringBuilder();
        var lineStart = 0;
        var absolute = 0;
        var lineIndex = 0;
        int read;

        while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var c = buffer[i];

                if (c == '\n')
                {
                    string content;
                    string terminator;

                    if (pending.Length > 0 && pending[^1] == '\r')
                    {
                        content = pending.ToString(0, pending.Length - 1);
                        terminator = "\r\n";
                    }
                    else
                    {
                        content = pending.ToString();
                        terminator = "\n";
                    }

                    yield return new PhysicalLine(content, terminator, lineStart, lineIndex);
                    pending.Clear();
                    lineIndex++;
                    absolute++;
                    lineStart = absolute;
                }
                else
                {
                    pending.Append(c);
                    absolute++;
                }
            }
        }

        if (pending.Length > 0)
        {
            yield return new PhysicalLine(pending.ToString(), string.Empty, lineStart, lineIndex);
        }
    }

    private static InstructionsFileSpanEmitScope ScopeOf(InstructionsFileSpanKind kind)
        => kind switch
        {
            InstructionsFileSpanKind.Text => InstructionsFileSpanEmitScope.Text,
            InstructionsFileSpanKind.FrontmatterBlock
                or InstructionsFileSpanKind.FrontmatterProperty
                or InstructionsFileSpanKind.FrontmatterKey
                or InstructionsFileSpanKind.FrontmatterValue => InstructionsFileSpanEmitScope.Frontmatter,
            InstructionsFileSpanKind.Heading1
                or InstructionsFileSpanKind.Heading2
                or InstructionsFileSpanKind.Heading3 => InstructionsFileSpanEmitScope.Headings,
            InstructionsFileSpanKind.PlainRule
                or InstructionsFileSpanKind.TaggedRule
                or InstructionsFileSpanKind.Tag => InstructionsFileSpanEmitScope.Rules,
            InstructionsFileSpanKind.Reference => InstructionsFileSpanEmitScope.References,
            _ => InstructionsFileSpanEmitScope.References,
        };

    private static void StartRule(ParserState state, PhysicalLine line)
    {
        var content = line.Content;
        var valid = GeneratedRuleBulletRegex().Match(content);

        InstructionsFileSpanKind kind;
        TagExtent? tag = null;

        if (valid.Success && valid.Groups[1].Success)
        {
            kind = InstructionsFileSpanKind.TaggedRule;
            var bracket = GeneratedBracketTagRegex().Match(content);
            tag = new TagExtent(bracket.Index, bracket.Length);
        }
        else if (valid.Success)
        {
            kind = InstructionsFileSpanKind.PlainRule;
        }
        else
        {
            kind = InstructionsFileSpanKind.TaggedRule;
            var bracket = GeneratedBracketTagRegex().Match(content);
            tag = new TagExtent(bracket.Index, bracket.Length);
        }

        var rule = new PendingRule(kind, tag, line.StartIndex, line.LineIndex, []);
        rule.Lines.Add(new TextLine(line, !state.InFence));
        state.Rule = rule;
    }

    private List<InstructionsFileParsedSpan> Advance(ParserState state, PhysicalLine line)
    {
        var output = new List<InstructionsFileParsedSpan>();

        if (state.Phase == ParsePhase.Start)
        {
            state.Phase = line.Content == "---" ? ParsePhase.Frontmatter : ParsePhase.Body;

            if (state.Phase == ParsePhase.Frontmatter)
            {
                state.FrontmatterLines.Add(line);
            }
            else
            {
                AdvanceBody(state, line, output);
            }
        }
        else if (state.Phase == ParsePhase.Frontmatter)
        {
            state.FrontmatterLines.Add(line);

            if (line.Content == "---")
            {
                BuildFrontmatterSpans(state.FrontmatterLines, output);
                state.Phase = ParsePhase.Body;
            }
        }
        else
        {
            AdvanceBody(state, line, output);
        }

        return output;
    }

    private void AdvanceBody(ParserState state, PhysicalLine line, List<InstructionsFileParsedSpan> output)
    {
        var content = line.Content;

        if (GeneratedFenceRegex().IsMatch(content))
        {
            state.InFence = !state.InFence;
            FlushRule(state, output);
            state.PendingText.Add(new TextLine(line, RefsScanned: false));
            return;
        }

        var validRule = GeneratedRuleBulletRegex().IsMatch(content);
        var malformedRule = !validRule && GeneratedMalformedRuleBulletRegex().IsMatch(content);

        if (validRule || malformedRule)
        {
            FlushRule(state, output);
            FlushText(state, output);
            StartRule(state, line);
            return;
        }

        if (state.Rule is not null)
        {
            if (content.Length == 0 || char.IsWhiteSpace(content[0]))
            {
                state.Rule.Lines.Add(new TextLine(line, !state.InFence));
                return;
            }

            FlushRule(state, output);
        }

        if (!state.InFence && GeneratedHeadingRegex().IsMatch(content))
        {
            FlushText(state, output);
            EmitHeading(line, output);
            return;
        }

        state.PendingText.Add(new TextLine(line, !state.InFence));
    }

    private void BuildFrontmatterSpans(List<PhysicalLine> frontmatterLines, List<InstructionsFileParsedSpan> output)
    {
        var first = frontmatterLines[0];
        var last = frontmatterLines[^1];
        var endIndex = last.StartIndex + last.FullLength;

        var block = MakeLineBlockSpan(
            frontmatterLines,
            InstructionsFileSpanKind.FrontmatterBlock,
            first.StartIndex,
            endIndex - first.StartIndex,
            first.LineIndex);

        if (block is not null)
        {
            output.Add(block);
        }

        if (!ShouldEmit(InstructionsFileSpanKind.FrontmatterProperty))
        {
            return;
        }

        var tokens = new List<TokenDraft>();

        for (var i = 1; i < frontmatterLines.Count - 1; i++)
        {
            var line = frontmatterLines[i];
            var field = GeneratedFrontmatterFieldRegex().Match(line.Content);

            if (!field.Success)
            {
                continue;
            }

            var key = field.Groups[1];
            var value = field.Groups[2];

            tokens.Add(new TokenDraft(
                line.StartIndex,
                line.Content.Length,
                line.LineIndex,
                line.Content,
                InstructionsFileSpanKind.FrontmatterProperty));
            tokens.Add(new TokenDraft(
                line.StartIndex + key.Index,
                key.Length,
                line.LineIndex,
                key.Value,
                InstructionsFileSpanKind.FrontmatterKey));

            if (value.Length > 0)
            {
                tokens.Add(new TokenDraft(
                    line.StartIndex + value.Index,
                    value.Length,
                    line.LineIndex,
                    value.Value,
                    InstructionsFileSpanKind.FrontmatterValue));
            }
        }

        EmitOrdered(tokens, output);
    }

    private void EmitHeading(PhysicalLine line, List<InstructionsFileParsedSpan> output)
    {
        var heading = GeneratedHeadingRegex().Match(line.Content);
        var level = heading.Groups[1].Value.Length;

        var kind = level switch
        {
            1 => InstructionsFileSpanKind.Heading1,
            2 => InstructionsFileSpanKind.Heading2,
            _ => InstructionsFileSpanKind.Heading3,
        };

        var block = MakeSpan(line.Content + line.Terminator, kind, line.StartIndex, line.FullLength, line.LineIndex, 1);

        if (block is not null)
        {
            output.Add(block);
        }

        if (!_emitReferences)
        {
            return;
        }

        var tokens = new List<TokenDraft>();
        AddReferenceDrafts(line, tokens);
        EmitOrdered(tokens, output);
    }

    private void EmitOrdered(List<TokenDraft> tokens, List<InstructionsFileParsedSpan> output)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        tokens.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.Length.CompareTo(a.Length));

        foreach (var token in tokens)
        {
            var span = MakeSpan(token.Text, token.Kind, token.Start, token.Length, token.LineIndex, 1);

            if (span is not null)
            {
                output.Add(span);
            }
        }
    }

    private List<InstructionsFileParsedSpan> Finish(ParserState state)
    {
        var output = new List<InstructionsFileParsedSpan>();

        if (state.Phase == ParsePhase.Frontmatter)
        {
            // The opening '---' was never closed, so it is not a frontmatter block;
            // reprocess the buffered lines as ordinary body content.
            var buffered = state.FrontmatterLines;
            state.FrontmatterLines = [];
            state.Phase = ParsePhase.Body;

            foreach (var line in buffered)
            {
                AdvanceBody(state, line, output);
            }
        }

        FlushRule(state, output);
        FlushText(state, output);

        return output;
    }

    private void FlushRule(ParserState state, List<InstructionsFileParsedSpan> output)
    {
        var rule = state.Rule;

        if (rule is null)
        {
            return;
        }

        state.Rule = null;

        var lines = rule.Lines;
        var end = lines.Count;

        while (end > 0 && lines[end - 1].Line.Content.AsSpan().Trim().IsEmpty)
        {
            end--;
        }

        var bodyLines = lines.GetRange(0, end);
        var trailing = lines.GetRange(end, lines.Count - end);

        var lastLine = bodyLines[^1].Line;
        var endIndex = lastLine.StartIndex + lastLine.FullLength;

        var block = MakeLineBlockSpan(bodyLines, rule.Kind, rule.StartIndex, endIndex - rule.StartIndex, rule.StartLine);

        if (block is not null)
        {
            output.Add(block);
        }

        var tokens = new List<TokenDraft>();

        if (_emitTags && rule.Tag is { } extent)
        {
            var first = bodyLines[0].Line;
            var text = first.Content.Substring(extent.Offset, extent.Length);
            tokens.Add(new TokenDraft(first.StartIndex + extent.Offset, extent.Length, first.LineIndex, text, InstructionsFileSpanKind.Tag));
        }

        if (_emitReferences)
        {
            foreach (var entry in bodyLines)
            {
                if (entry.RefsScanned)
                {
                    AddReferenceDrafts(entry.Line, tokens);
                }
            }
        }

        EmitOrdered(tokens, output);

        foreach (var entry in trailing)
        {
            state.PendingText.Add(entry);
        }
    }

    private void FlushText(ParserState state, List<InstructionsFileParsedSpan> output)
    {
        if (state.PendingText.Count == 0)
        {
            return;
        }

        var first = state.PendingText[0].Line;
        var length = 0;

        foreach (var entry in state.PendingText)
        {
            length += entry.Line.FullLength;
        }

        var block = MakeLineBlockSpan(
            state.PendingText,
            InstructionsFileSpanKind.Text,
            first.StartIndex,
            length,
            first.LineIndex);

        if (block is not null)
        {
            output.Add(block);
        }

        if (_emitReferences)
        {
            var tokens = new List<TokenDraft>();

            foreach (var entry in state.PendingText)
            {
                if (entry.RefsScanned)
                {
                    AddReferenceDrafts(entry.Line, tokens);
                }
            }

            EmitOrdered(tokens, output);
        }

        state.PendingText.Clear();
    }

    private InstructionsFileParsedSpan? MakeLineBlockSpan(
        List<PhysicalLine> lines,
        InstructionsFileSpanKind kind,
        int startIndex,
        int length,
        int startLine)
        => ShouldEmit(kind)
            ? new InstructionsFileParsedSpan(
                BuildText(lines, length),
                kind,
                new InstructionsFileTextSpan(startIndex, length),
                new InstructionsFileLineSpan(startLine, lines.Count))
            : null;

    private InstructionsFileParsedSpan? MakeLineBlockSpan(
        List<TextLine> lines,
        InstructionsFileSpanKind kind,
        int startIndex,
        int length,
        int startLine)
        => ShouldEmit(kind)
            ? new InstructionsFileParsedSpan(
                BuildText(lines, length),
                kind,
                new InstructionsFileTextSpan(startIndex, length),
                new InstructionsFileLineSpan(startLine, lines.Count))
            : null;

    private InstructionsFileParsedSpan? MakeSpan(
        string text,
        InstructionsFileSpanKind kind,
        int startIndex,
        int length,
        int startLine,
        int lineCount)
        => ShouldEmit(kind)
            ? new InstructionsFileParsedSpan(
                text,
                kind,
                new InstructionsFileTextSpan(startIndex, length),
                new InstructionsFileLineSpan(startLine, lineCount))
            : null;

    private bool ShouldEmit(InstructionsFileSpanKind kind)
        => IsEmitted(emitLevel, emitScope, kind);

    private sealed class ParserState
    {
        /// <summary>Gets or sets the buffered frontmatter lines, including the delimiters.</summary>
        public List<PhysicalLine> FrontmatterLines { get; set; } = [];

        /// <summary>Gets or sets a value indicating whether the cursor is inside a fenced code block.</summary>
        public bool InFence { get; set; }

        /// <summary>Gets the body lines buffered for the next flushed text block.</summary>
        public List<TextLine> PendingText { get; } = [];

        /// <summary>Gets or sets the current parse phase.</summary>
        public ParsePhase Phase { get; set; } = ParsePhase.Start;

        /// <summary>Gets or sets the rule currently being accumulated, or null when none is open.</summary>
        public PendingRule? Rule { get; set; }
    }

    private sealed record PendingRule(
        InstructionsFileSpanKind Kind,
        TagExtent? Tag,
        int StartIndex,
        int StartLine,
        List<TextLine> Lines);

    private readonly record struct PhysicalLine(string Content, string Terminator, int StartIndex, int LineIndex)
    {
        /// <summary>Gets the length of the line including its terminator.</summary>
        public int FullLength
            => Content.Length + Terminator.Length;
    }

    private readonly record struct TagExtent(int Offset, int Length);

    private readonly record struct TextLine(PhysicalLine Line, bool RefsScanned);

    private readonly record struct TokenDraft(int Start, int Length, int LineIndex, string Text, InstructionsFileSpanKind Kind);
}
