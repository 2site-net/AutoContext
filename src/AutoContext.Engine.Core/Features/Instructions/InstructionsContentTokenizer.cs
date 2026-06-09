namespace AutoContext.Engine.Core.Features.Instructions;

using System.Buffers;
using System.Text.RegularExpressions;

/// <summary>
/// Identifier-aware tokenizer shared by the instruction-body full-text
/// search index. Splits text on non-word runs <em>and</em> on
/// camelCase / kebab / snake boundaries, so a query of
/// <c>ConfigureAwait</c> matches a heading written as
/// "Configure Await" and a snake-cased identifier
/// <c>configure_await</c> alike. Every piece is lowercased; pieces
/// shorter than <see cref="MinTokenLength"/> are dropped as noise.
/// </summary>
internal static partial class InstructionsContentTokenizer
{
    private const int MinTokenLength = 2;
    private const int StackBufferThreshold = 256;

    /// <summary>
    /// Collects the distinct query tokens of <paramref name="query"/>, in
    /// first-seen order, for the AND-across-tokens match.
    /// </summary>
    /// <param name="query">The raw query text. Must not be
    /// <see langword="null"/>.</param>
    /// <returns>The distinct tokens, in the order they first appear.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is
    /// <see langword="null"/>.</exception>
    public static IReadOnlyList<string> CollectQueryTokens(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var tokens = new List<string>();
        var sink = new QueryTokenSink(seen, tokens);

        Scan(query, ref sink);

        return tokens;
    }

    /// <summary>
    /// Tokenizes <paramref name="text"/> into a token-to-frequency map.
    /// </summary>
    /// <param name="text">The text to tokenize. Must not be
    /// <see langword="null"/>.</param>
    /// <returns>An ordinal map of each token to how many times it
    /// occurs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is
    /// <see langword="null"/>.</exception>
    public static IReadOnlyDictionary<string, int> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var sink = new FrequencySink(counts);

        Scan(text, ref sink);

        return counts;
    }

    private static void Expand<TSink>(ReadOnlySpan<char> word, ref TSink sink)
        where TSink : struct, ITokenSink, allows ref struct
    {
        char[]? rented = null;
        var lowered = word.Length <= StackBufferThreshold
            ? stackalloc char[word.Length]
            : (rented = ArrayPool<char>.Shared.Rent(word.Length)).AsSpan(0, word.Length);

        try
        {
            word.ToLowerInvariant(lowered);

            if (lowered.Length >= MinTokenLength)
            {
                sink.Push(lowered);
            }

            // The boundary split runs on the original (cased) word, but its
            // ranges index identically into the length-preserving lowercased
            // buffer, so each piece is a free slice rather than a fresh string.
            foreach (var range in IdentifierBoundaryPattern().EnumerateSplits(word))
            {
                var piece = lowered[range];

                if (piece.Length >= MinTokenLength && !piece.SequenceEqual(lowered))
                {
                    sink.Push(piece);
                }
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

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|[-_]")]
    private static partial Regex IdentifierBoundaryPattern();

    private static bool IsWordCharacter(char value)
        => char.IsAsciiLetterOrDigit(value) || value == '_';

    private static void Scan<TSink>(ReadOnlySpan<char> text, ref TSink sink)
        where TSink : struct, ITokenSink, allows ref struct
    {
        var index = 0;

        while (index < text.Length)
        {
            if (!IsWordCharacter(text[index]))
            {
                index++;
                continue;
            }

            var start = index;

            while (index < text.Length && IsWordCharacter(text[index]))
            {
                index++;
            }

            Expand(text[start..index], ref sink);
        }
    }

    /// <summary>
    /// Token sink that counts occurrences into a frequency map, materializing
    /// a string key only the first time a token is seen.
    /// </summary>
    private ref struct FrequencySink : ITokenSink
    {
        private Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _counts;

        /// <summary>
        /// Creates a sink over <paramref name="counts"/>, the frequency map to
        /// accumulate into.
        /// </summary>
        /// <param name="counts">The destination frequency map.</param>
        public FrequencySink(Dictionary<string, int> counts)
            => _counts = counts.GetAlternateLookup<ReadOnlySpan<char>>();

        /// <inheritdoc />
        public void Push(scoped ReadOnlySpan<char> token)
            => _counts[token] = _counts.TryGetValue(token, out var count) ? count + 1 : 1;
    }

    private interface ITokenSink
    {
        /// <summary>
        /// Receives one lowercased token slice. The slice is only valid for
        /// the duration of the call; an implementation that retains it must
        /// copy it (for example, via <see cref="ReadOnlySpan{T}.ToString"/>).
        /// </summary>
        /// <param name="token">The lowercased token.</param>
        void Push(scoped ReadOnlySpan<char> token);
    }

    /// <summary>
    /// Token sink that collects distinct tokens in first-seen order,
    /// materializing a string only when a token is first encountered.
    /// </summary>
    private readonly ref struct QueryTokenSink : ITokenSink
    {
        private readonly HashSet<string> _seen;
        private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _seenLookup;
        private readonly List<string> _tokens;

        /// <summary>
        /// Creates a sink that records distinct tokens into
        /// <paramref name="tokens"/>, using <paramref name="seen"/> for the
        /// ordinal distinctness check.
        /// </summary>
        /// <param name="seen">The set tracking already-seen tokens.</param>
        /// <param name="tokens">The destination list, in first-seen order.</param>
        public QueryTokenSink(HashSet<string> seen, List<string> tokens)
        {
            _seen = seen;
            _seenLookup = seen.GetAlternateLookup<ReadOnlySpan<char>>();
            _tokens = tokens;
        }

        /// <inheritdoc />
        public void Push(scoped ReadOnlySpan<char> token)
        {
            if (_seenLookup.Contains(token))
            {
                return;
            }

            var materialized = token.ToString();

            _seen.Add(materialized);
            _tokens.Add(materialized);
        }
    }
}
