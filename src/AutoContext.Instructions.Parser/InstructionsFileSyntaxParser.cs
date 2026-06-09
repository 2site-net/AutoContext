namespace AutoContext.Instructions.Parser;

using System.Buffers;
using System.Text.RegularExpressions;

using AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// Scans the raw text of an instructions file and breaks it into
/// <see cref="InstructionsFileSyntaxSpan"/> pieces — frontmatter, headings, rule
/// bullets, tags, and references — each marked with where it sits in the file.
/// This is the first of two passes; <see cref="Model.InstructionsFile.FromSpans"/>
/// takes the <see cref="InstructionsFileSyntaxTree"/> produced here and turns it
/// into the final structured result. The tree splits the spans into a frontmatter
/// stream and a body stream, with references and diagnostics gathered into their
/// own self-locating side streams.
/// <para>
/// Two things to know about the positions it reports. They are measured from the
/// very start of the file, with the frontmatter counted in rather than stripped
/// out, and they count raw characters, so a <c>CRLF</c> line break counts as two.
/// A span's start is inclusive and its end is exclusive, like an ordinary .NET
/// slice.
/// </para>
/// <para>
/// Spans come in two sizes. <em>Block</em> spans
/// (<see cref="InstructionsFileSpanEmitLevel.Blocks"/>) tile the whole file end to
/// end with no gaps: every character belongs to exactly one block, and anything
/// that is not a heading, rule, or frontmatter falls through to a plain
/// <see cref="InstructionsFileSpanKind.Text"/> block. <em>Token</em> spans
/// (<see cref="InstructionsFileSpanEmitLevel.Tokens"/>) are the smaller pieces that
/// sit inside a block — a frontmatter key, a tag, a reference. A span is emitted
/// only when both its level (block or token) and its scope (frontmatter, headings,
/// rules, references) are switched on; when a token and the block around it are
/// both emitted, the block comes first.
/// </para>
/// <para>
/// Fenced code blocks are treated two ways on purpose: rule bullets are still
/// picked up inside a fence, but headings and references are not.
/// </para>
/// <para>
/// Frontmatter is read as plain, single-line <c>key: value</c> pairs only; the
/// value may be wrapped in double quotes and runs to the end of the line. Anything
/// richer — values spread across several lines, block scalars (<c>|</c>,
/// <c>&gt;</c>), embedded quotes, lists, nested maps — is deliberately left alone:
/// that line produces no
/// <see cref="InstructionsFileSpanKind.FrontmatterProperty"/> token, though its raw
/// text is still available on the surrounding
/// <see cref="InstructionsFileSpanKind.FrontmatterBlock"/> span. That covers
/// everything the real instructions files use, since their frontmatter is only
/// single-line <c>name</c> / <c>description</c> / <c>applyTo</c> entries.
/// </para>
/// </summary>
public sealed partial class InstructionsFileSyntaxParser(
    InstructionsFileSpanEmitLevel emitLevel = InstructionsFileSpanEmitLevel.Full,
    InstructionsFileSpanEmitScope emitScope = InstructionsFileSpanEmitScope.All,
    bool includeDiagnostics = true)
{
    private const int StackMaskThreshold = 256;

    private readonly bool _emitReferences = IsEmitted(emitLevel, emitScope, InstructionsFileSpanKind.Reference);
    private readonly bool _emitTags = IsEmitted(emitLevel, emitScope, InstructionsFileSpanKind.Tag);
    private readonly bool _includeDiagnostics = includeDiagnostics;

    private enum ParsePhase
    {
        Start,
        Frontmatter,
        Body,
    }

    private enum ReferenceFault
    {
        None,
        Locator,
        Range,
        Fragment,
    }

    /// <summary>
    /// Scans <paramref name="text"/> and returns its spans as a finished
    /// <see cref="InstructionsFileSyntaxTree"/>, with each stream in the order it
    /// appears in the file.
    /// </summary>
    /// <param name="text">The decoded instructions text.</param>
    /// <param name="cancellationToken">Cancels the scan; checked once per line.</param>
    /// <returns>The parsed syntax tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is
    /// <see langword="null"/>.</exception>
    public InstructionsFileSyntaxTree Parse(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var state = new ParserState { Source = text };

        foreach (var line in ReadPhysicalLines(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Advance(state, line);
        }

        Finish(state);

        return new InstructionsFileSyntaxTree(
            state.Frontmatter,
            state.Body,
            state.References,
            state.Diagnostics);
    }

    /// <summary>
    /// Reads the instructions file at <paramref name="path"/> into memory, detecting
    /// its encoding from any byte-order mark, and returns its spans. Only the file
    /// read is asynchronous; the scan itself runs synchronously once the text is
    /// loaded.
    /// </summary>
    /// <param name="path">The instructions file to read.</param>
    /// <param name="cancellationToken">Cancels the read and the scan that
    /// follows.</param>
    /// <returns>The parsed syntax tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    public async Task<InstructionsFileSyntaxTree> ParseFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        return Parse(text, cancellationToken);
    }

    private static void AddReferenceDrafts(PhysicalLine line, List<TokenDraft> tokens, bool includeDiagnostics)
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
                var scan = ScanReference(token);

                if (!scan.IsReference)
                {
                    continue;
                }

                var diagnostic = includeDiagnostics && scan.Fault != ReferenceFault.None
                    ? ReferenceFaultDiagnostic(scan.Fault, token.ToString())
                    : null;

                tokens.Add(new TokenDraft(
                    line.StartIndex + match.Index,
                    match.Length,
                    line.LineIndex,
                    InstructionsFileSpanKind.Reference,
                    diagnostic,
                    scan.Address));
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

    // Matches a single flat frontmatter scalar: an unquoted key, a colon, then an
    // optionally double-quoted value running to end of line. By design it does not
    // span lines or honour block scalars; see the class summary for the contract.
    [GeneratedRegex("^(\\w+):\\s*\"?([^\"\\r\\n]*)\"?\\s*$")]
    private static partial Regex GeneratedFrontmatterFieldRegex();

    [GeneratedRegex(@"\\(.)")]
    private static partial Regex GeneratedHeadingEscapeRegex();

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

    [GeneratedRegex(@"^INST\d{4}\s*[-\u2013/]")]
    private static partial Regex GeneratedReferenceRangeFragmentRegex();

    [GeneratedRegex(@"^INST\d{4}$")]
    private static partial Regex GeneratedReferenceRuleFragmentRegex();

    [GeneratedRegex(@"^'(?:[^'\\]|\\.)+'$")]
    private static partial Regex GeneratedReferenceSectionFragmentRegex();

    [GeneratedRegex(@"\[([^\[\]#]*)#([^\[\]]*)\](?![(\[:])")]
    private static partial Regex GeneratedReferenceTokenRegex();

    [GeneratedRegex(@"^[-*]\s(?:\[(INST\d{4})\]\s*)?\*\*(Do|Don't)\*\*")]
    private static partial Regex GeneratedRuleBulletRegex();

    private static bool IsEmitted(
        InstructionsFileSpanEmitLevel level,
        InstructionsFileSpanEmitScope scope,
        InstructionsFileSpanKind kind)
        => (level & LevelOf(kind)) != 0 && (scope & ScopeOf(kind)) != 0;

    private static bool IsThematicBreak(ReadOnlySpan<char> content)
    {
        var trimmed = content.Trim();

        if (trimmed.Length < 3)
        {
            return false;
        }

        foreach (var c in trimmed)
        {
            if (c != '-')
            {
                return false;
            }
        }

        return true;
    }

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

    private static IEnumerable<PhysicalLine> ReadPhysicalLines(string text)
    {
        var lineStart = 0;
        var lineIndex = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
            {
                continue;
            }

            string content;
            string terminator;

            if (i > lineStart && text[i - 1] == '\r')
            {
                content = text[lineStart..(i - 1)];
                terminator = "\r\n";
            }
            else
            {
                content = text[lineStart..i];
                terminator = "\n";
            }

            yield return new PhysicalLine(content, terminator, lineStart, lineIndex);
            lineStart = i + 1;
            lineIndex++;
        }

        if (lineStart < text.Length)
        {
            yield return new PhysicalLine(text[lineStart..], string.Empty, lineStart, lineIndex);
        }
    }

    private static InstructionsFileDiagnostic? ReferenceFaultDiagnostic(ReferenceFault fault, string token)
        => fault switch
        {
            ReferenceFault.Locator => new InstructionsFileDiagnostic(
                InstructionsFileDiagnosticKind.MalformedReference,
                $"Malformed reference locator in {token}."),
            ReferenceFault.Range => new InstructionsFileDiagnostic(
                InstructionsFileDiagnosticKind.MalformedReference,
                $"Reference ranges are not allowed in {token}; cite each rule or the enclosing section."),
            ReferenceFault.Fragment => new InstructionsFileDiagnostic(
                InstructionsFileDiagnosticKind.MalformedReference,
                $"Malformed reference fragment in {token}."),
            ReferenceFault.None => null,
            _ => null,
        };

    private static ReferenceScanResult ScanReference(ReadOnlySpan<char> token)
    {
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
            return new ReferenceScanResult(false, null, ReferenceFault.None);
        }

        if (hasLocator && !locatorValid)
        {
            return new ReferenceScanResult(true, null, ReferenceFault.Locator);
        }

        if (GeneratedReferenceRuleFragmentRegex().IsMatch(fragment))
        {
            return new ReferenceScanResult(
                true,
                new InstructionsFileReferenceAddress(
                    InstructionsFileReferenceKind.Rule,
                    hasLocator ? locator.ToString() : null,
                    fragment.ToString()),
                ReferenceFault.None);
        }

        if (GeneratedReferenceSectionFragmentRegex().IsMatch(fragment))
        {
            return new ReferenceScanResult(
                true,
                new InstructionsFileReferenceAddress(
                    InstructionsFileReferenceKind.Section,
                    hasLocator ? locator.ToString() : null,
                    UnescapeHeading(fragment[1..^1].ToString())),
                ReferenceFault.None);
        }

        var fault = GeneratedReferenceRangeFragmentRegex().IsMatch(fragment)
            ? ReferenceFault.Range
            : ReferenceFault.Fragment;

        return new ReferenceScanResult(true, null, fault);
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

    private static string UnescapeHeading(string heading)
        => heading.Contains('\\', StringComparison.Ordinal)
            ? GeneratedHeadingEscapeRegex().Replace(heading, "$1")
            : heading;

    private void Advance(ParserState state, PhysicalLine line)
    {
        if (state.Phase == ParsePhase.Start)
        {
            state.Phase = line.Content == "---" ? ParsePhase.Frontmatter : ParsePhase.Body;

            if (state.Phase == ParsePhase.Frontmatter)
            {
                state.FrontmatterLines.Add(line);
            }
            else
            {
                AdvanceBody(state, line);
            }
        }
        else if (state.Phase == ParsePhase.Frontmatter)
        {
            state.FrontmatterLines.Add(line);

            if (line.Content == "---")
            {
                BuildFrontmatterSpans(state);
                state.Phase = ParsePhase.Body;
            }
        }
        else
        {
            AdvanceBody(state, line);
        }
    }

    private void AdvanceBody(ParserState state, PhysicalLine line)
    {
        var content = line.Content;

        if (GeneratedFenceRegex().IsMatch(content))
        {
            state.InFence = !state.InFence;
            FlushRule(state);
            state.PendingText.Add(new TextLine(line, RefsScanned: false));
            return;
        }

        var validRule = GeneratedRuleBulletRegex().IsMatch(content);
        var malformedRule = !validRule && GeneratedMalformedRuleBulletRegex().IsMatch(content);

        if (validRule || malformedRule)
        {
            FlushRule(state);
            FlushText(state);
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

            FlushRule(state);
        }

        if (!state.InFence && GeneratedHeadingRegex().IsMatch(content))
        {
            FlushText(state);
            EmitHeading(state, line);
            return;
        }

        if (_includeDiagnostics && !state.InFence && IsThematicBreak(content))
        {
            state.UnderRules = false;
        }

        state.PendingText.Add(new TextLine(line, !state.InFence));
    }

    private void BuildFrontmatterSpans(ParserState state)
    {
        var frontmatterLines = state.FrontmatterLines;
        var first = frontmatterLines[0];
        var last = frontmatterLines[^1];
        var endIndex = last.StartIndex + last.FullLength;

        var block = MakeSpan(
            state,
            InstructionsFileSpanKind.FrontmatterBlock,
            first.StartIndex,
            endIndex - first.StartIndex,
            first.LineIndex,
            frontmatterLines.Count);

        if (block is not null)
        {
            EmitSpan(state, block);
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
                InstructionsFileSpanKind.FrontmatterProperty));
            tokens.Add(new TokenDraft(
                line.StartIndex + key.Index,
                key.Length,
                line.LineIndex,
                InstructionsFileSpanKind.FrontmatterKey));

            if (value.Length > 0)
            {
                tokens.Add(new TokenDraft(
                    line.StartIndex + value.Index,
                    value.Length,
                    line.LineIndex,
                    InstructionsFileSpanKind.FrontmatterValue));
            }
        }

        EmitOrdered(state, tokens);
    }

    private void EmitHeading(ParserState state, PhysicalLine line)
    {
        var heading = GeneratedHeadingRegex().Match(line.Content);
        var level = heading.Groups[1].Length;

        var kind = level switch
        {
            1 => InstructionsFileSpanKind.Heading1,
            2 => InstructionsFileSpanKind.Heading2,
            _ => InstructionsFileSpanKind.Heading3,
        };

        if (_includeDiagnostics && level <= 2)
        {
            // A level-3 heading keeps the current section; level 1 always ends it,
            // and level 2 opens the addressable-rule region only for an exact `Rules`.
            state.UnderRules = level == 2 && heading.Groups[2].ValueSpan.SequenceEqual("Rules");
        }

        var block = MakeSpan(state, kind, line.StartIndex, line.FullLength, line.LineIndex, 1);

        if (block is not null)
        {
            EmitSpan(state, block);
        }

        if (!_emitReferences)
        {
            return;
        }

        var tokens = new List<TokenDraft>();
        AddReferenceDrafts(line, tokens, _includeDiagnostics);
        EmitOrdered(state, tokens);
    }

    private void EmitOrdered(ParserState state, List<TokenDraft> tokens)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        tokens.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.Length.CompareTo(a.Length));

        foreach (var token in tokens)
        {
            var span = MakeSpan(state, token.Kind, token.Start, token.Length, token.LineIndex, 1);

            if (span is null)
            {
                continue;
            }

            EmitSpan(state, span);

            if (token.Address is { } address)
            {
                state.References.Add(new InstructionsFileSyntaxReference(address, span.TextSpan, span.LineSpan));
            }

            if (token.Diagnostic is { } diagnostic)
            {
                state.Diagnostics.Add(new InstructionsFileSyntaxDiagnostic(diagnostic, span.TextSpan, span.LineSpan));
            }
        }
    }

    private static void EmitSpan(ParserState state, InstructionsFileSyntaxSpan span)
    {
        if (ScopeOf(span.Kind) == InstructionsFileSpanEmitScope.Frontmatter)
        {
            state.Frontmatter.Add(span);
        }
        else
        {
            state.Body.Add(span);
        }
    }

    private void Finish(ParserState state)
    {
        if (state.Phase == ParsePhase.Frontmatter)
        {
            // The opening '---' was never closed, so it is not a frontmatter block;
            // reprocess the buffered lines as ordinary body content.
            var buffered = state.FrontmatterLines;
            state.FrontmatterLines = [];
            state.Phase = ParsePhase.Body;

            foreach (var line in buffered)
            {
                AdvanceBody(state, line);
            }
        }

        FlushRule(state);
        FlushText(state);
    }

    private void FlushRule(ParserState state)
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

        var block = MakeSpan(state, rule.Kind, rule.StartIndex, endIndex - rule.StartIndex, rule.StartLine, bodyLines.Count);

        if (block is not null)
        {
            EmitSpan(state, block);
        }

        // Rule diagnostics live in the diagnostic stream, independent of whether the
        // rule block (or its tag token) is emitted. A rule carries either block
        // diagnostics (duplicate/misplaced/missing) or a malformed-tag diagnostic,
        // never both, so their order relative to each other does not matter.
        if (rule.BlockDiagnostics is { } blockDiagnostics)
        {
            var blockTextSpan = new InstructionsFileTextSpan(rule.StartIndex, endIndex - rule.StartIndex);
            var blockLineSpan = new InstructionsFileLineSpan(rule.StartLine, bodyLines.Count);

            foreach (var diagnostic in blockDiagnostics)
            {
                state.Diagnostics.Add(new InstructionsFileSyntaxDiagnostic(diagnostic, blockTextSpan, blockLineSpan));
            }
        }

        if (rule.TagDiagnostic is { } tagDiagnostic && rule.Tag is { } tagExtent)
        {
            var first = bodyLines[0].Line;

            state.Diagnostics.Add(new InstructionsFileSyntaxDiagnostic(
                tagDiagnostic,
                new InstructionsFileTextSpan(first.StartIndex + tagExtent.Offset, tagExtent.Length),
                new InstructionsFileLineSpan(first.LineIndex, 1)));
        }

        var tokens = new List<TokenDraft>();

        if (_emitTags && rule.Tag is { } extent)
        {
            var first = bodyLines[0].Line;
            tokens.Add(new TokenDraft(first.StartIndex + extent.Offset, extent.Length, first.LineIndex, InstructionsFileSpanKind.Tag));
        }

        if (_emitReferences)
        {
            foreach (var entry in bodyLines)
            {
                if (entry.RefsScanned)
                {
                    AddReferenceDrafts(entry.Line, tokens, _includeDiagnostics);
                }
            }
        }

        EmitOrdered(state, tokens);

        foreach (var entry in trailing)
        {
            state.PendingText.Add(entry);
        }
    }

    private void FlushText(ParserState state)
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

        var block = MakeSpan(
            state,
            InstructionsFileSpanKind.Text,
            first.StartIndex,
            length,
            first.LineIndex,
            state.PendingText.Count);

        if (block is not null)
        {
            EmitSpan(state, block);
        }

        if (_emitReferences)
        {
            var tokens = new List<TokenDraft>();

            foreach (var entry in state.PendingText)
            {
                if (entry.RefsScanned)
                {
                    AddReferenceDrafts(entry.Line, tokens, _includeDiagnostics);
                }
            }

            EmitOrdered(state, tokens);
        }

        state.PendingText.Clear();
    }

    private InstructionsFileSyntaxSpan? MakeSpan(
        ParserState state,
        InstructionsFileSpanKind kind,
        int startIndex,
        int length,
        int startLine,
        int lineCount)
        => ShouldEmit(kind)
            ? new InstructionsFileSyntaxSpan(
                state.Source.AsMemory(startIndex, length),
                kind,
                new InstructionsFileTextSpan(startIndex, length),
                new InstructionsFileLineSpan(startLine, lineCount))
            : null;

    private bool ShouldEmit(InstructionsFileSpanKind kind)
        => IsEmitted(emitLevel, emitScope, kind);

    private void StartRule(ParserState state, PhysicalLine line)
    {
        var content = line.Content;
        var valid = GeneratedRuleBulletRegex().Match(content);

        InstructionsFileSpanKind kind;
        TagExtent? tag = null;
        InstructionsFileDiagnostic? tagDiagnostic = null;
        List<InstructionsFileDiagnostic>? blockDiagnostics = null;

        if (valid.Success && valid.Groups[1].Success)
        {
            kind = InstructionsFileSpanKind.TaggedRule;
            var bracket = GeneratedBracketTagRegex().Match(content);
            tag = new TagExtent(bracket.Index, bracket.Length);

            if (_includeDiagnostics)
            {
                var id = valid.Groups[1].Value;

                if (state.SeenTags.TryGetValue(id, out var firstLine))
                {
                    (blockDiagnostics ??= []).Add(new InstructionsFileDiagnostic(
                        InstructionsFileDiagnosticKind.DuplicateTag,
                        $"Duplicate INST tag [{id}]; first defined at line {firstLine + 1}."));
                }
                else
                {
                    state.SeenTags[id] = line.LineIndex;
                }

                if (!state.UnderRules)
                {
                    (blockDiagnostics ??= []).Add(new InstructionsFileDiagnostic(
                        InstructionsFileDiagnosticKind.MisplacedRule,
                        $"Tagged rule [{id}] appears outside the ## Rules section."));
                }
            }
        }
        else if (valid.Success)
        {
            kind = InstructionsFileSpanKind.PlainRule;

            if (_includeDiagnostics && state.UnderRules)
            {
                (blockDiagnostics ??= []).Add(new InstructionsFileDiagnostic(
                    InstructionsFileDiagnosticKind.MissingTag,
                    "Rule has no INST#### tag, so it cannot be addressed."));
            }
        }
        else
        {
            kind = InstructionsFileSpanKind.TaggedRule;
            var bracket = GeneratedBracketTagRegex().Match(content);
            tag = new TagExtent(bracket.Index, bracket.Length);

            if (_includeDiagnostics)
            {
                tagDiagnostic = new InstructionsFileDiagnostic(
                    InstructionsFileDiagnosticKind.MalformedTag,
                    $"Malformed INST tag [{bracket.Groups[1].Value}]; expected the form [INST####].");
            }
        }

        var rule = new PendingRule(kind, tag, line.StartIndex, line.LineIndex, [], tagDiagnostic, blockDiagnostics);
        rule.Lines.Add(new TextLine(line, !state.InFence));
        state.Rule = rule;
    }

    private sealed class ParserState
    {
        /// <summary>Gets the body spans emitted so far, in document order.</summary>
        public List<InstructionsFileSyntaxSpan> Body { get; } = [];

        /// <summary>Gets the diagnostics emitted so far, in document order.</summary>
        public List<InstructionsFileSyntaxDiagnostic> Diagnostics { get; } = [];

        /// <summary>Gets the frontmatter spans emitted so far, in document order.</summary>
        public List<InstructionsFileSyntaxSpan> Frontmatter { get; } = [];

        /// <summary>Gets or sets the buffered frontmatter lines, including the delimiters.</summary>
        public List<PhysicalLine> FrontmatterLines { get; set; } = [];

        /// <summary>Gets or sets a value indicating whether the cursor is inside a fenced code block.</summary>
        public bool InFence { get; set; }

        /// <summary>Gets the body lines buffered for the next flushed text block.</summary>
        public List<TextLine> PendingText { get; } = [];

        /// <summary>Gets or sets the current parse phase.</summary>
        public ParsePhase Phase { get; set; } = ParsePhase.Start;

        /// <summary>Gets the references emitted so far, in document order.</summary>
        public List<InstructionsFileSyntaxReference> References { get; } = [];

        /// <summary>Gets or sets the rule currently being accumulated, or null when none is open.</summary>
        public PendingRule? Rule { get; set; }

        /// <summary>Gets the first source line, by zero-based index, on which each
        /// <c>INST####</c> tag was seen, used to flag later repeats as duplicates.</summary>
        public Dictionary<string, int> SeenTags { get; } = [];

        /// <summary>Gets the decoded source buffer the spans are sliced from.</summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the cursor sits within the
        /// <c>## Rules</c> section (or one of its <c>###</c> subsections), where tagged
        /// rules belong and plain rules are faults.</summary>
        public bool UnderRules { get; set; }
    }

    private sealed record PendingRule(
        InstructionsFileSpanKind Kind,
        TagExtent? Tag,
        int StartIndex,
        int StartLine,
        List<TextLine> Lines,
        InstructionsFileDiagnostic? TagDiagnostic,
        List<InstructionsFileDiagnostic>? BlockDiagnostics);

    private readonly record struct PhysicalLine(string Content, string Terminator, int StartIndex, int LineIndex)
    {
        /// <summary>Gets the length of the line including its terminator.</summary>
        public int FullLength
            => Content.Length + Terminator.Length;
    }

    private readonly record struct ReferenceScanResult(
        bool IsReference,
        InstructionsFileReferenceAddress? Address,
        ReferenceFault Fault);

    private readonly record struct TagExtent(int Offset, int Length);

    private readonly record struct TextLine(PhysicalLine Line, bool RefsScanned);

    private readonly record struct TokenDraft(
        int Start,
        int Length,
        int LineIndex,
        InstructionsFileSpanKind Kind,
        InstructionsFileDiagnostic? Diagnostic = null,
        InstructionsFileReferenceAddress? Address = null);
}
