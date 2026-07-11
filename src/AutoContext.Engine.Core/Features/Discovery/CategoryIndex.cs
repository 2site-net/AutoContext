namespace AutoContext.Engine.Core.Features.Discovery;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;

/// <summary>
/// Inverts the MCP-tools catalog into a <c>category-name → tool-names</c>
/// index for prompt routing. Each tool is keyed under its own leaf
/// category <b>and every ancestor category</b> (walking the catalog
/// <see cref="McpToolsCategoryEntry.Parent"/> chain), so a broad prompt
/// word such as <c>.net</c> surfaces the whole family (C#, NuGet), not
/// just the tools filed directly under it. Built once over the immutable
/// registry snapshot; the prompt scan matches category names
/// case-insensitively and reports each match under the catalog's
/// canonical name.
/// </summary>
internal sealed class CategoryIndex
{
    private readonly IReadOnlyList<string> _matchableCategories;
    private readonly Dictionary<string, IReadOnlyList<string>> _toolsByCategory;

    /// <summary>
    /// Builds the index over <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">The immutable MCP-tools registry snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/>
    /// is <see langword="null"/>.</exception>
    public CategoryIndex(McpToolsRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var categoriesByName = new Dictionary<string, McpToolsCategoryEntry>(
            registry.Categories.Count, StringComparer.Ordinal);

        foreach (var category in registry.Categories)
        {
            categoriesByName[category.Name] = category;
        }

        var toolsByCategory = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var seenPerCategory = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var tool in registry.Tools)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = tool.Category;

            while (current is not null && visited.Add(current))
            {
                if (!toolsByCategory.TryGetValue(current, out var tools))
                {
                    tools = [];
                    toolsByCategory[current] = tools;
                    seenPerCategory[current] = new HashSet<string>(StringComparer.Ordinal);
                }

                if (seenPerCategory[current].Add(tool.Name))
                {
                    tools.Add(tool.Name);
                }

                current = categoriesByName.TryGetValue(current, out var entry) ? entry.Parent : null;
            }
        }

        var matchable = new List<string>();

        foreach (var category in registry.Categories)
        {
            if (toolsByCategory.ContainsKey(category.Name))
            {
                matchable.Add(category.Name);
            }
        }

        _matchableCategories = matchable;
        _toolsByCategory = toolsByCategory.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Scans <paramref name="prompt"/> for category words and returns the
    /// matched category names together with the union of their tools.
    /// </summary>
    /// <param name="prompt">The user prompt to scan.</param>
    /// <returns>The matched category names (catalog document order) and
    /// the routed tool names (de-duplicated, in the order the matched
    /// categories contribute them).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prompt"/>
    /// is <see langword="null"/>.</exception>
    public (IReadOnlyList<string> Categories, IReadOnlyList<string> Tools) Match(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var categories = new List<string>();
        var tools = new List<string>();
        var toolSeen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var category in _matchableCategories)
        {
            if (!ContainsWholeWord(prompt, category))
            {
                continue;
            }

            categories.Add(category);

            foreach (var tool in _toolsByCategory[category])
            {
                if (toolSeen.Add(tool))
                {
                    tools.Add(tool);
                }
            }
        }

        return (categories, tools);
    }

    private static bool ContainsWholeWord(string haystack, string needle)
    {
        if (needle.Length == 0)
        {
            return false;
        }

        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var leftIsBoundary = index == 0 || !IsWordCharacter(haystack[index - 1]);
            var end = index + needle.Length;
            var rightIsBoundary = end == haystack.Length || !IsWordCharacter(haystack[end]);

            if (leftIsBoundary && rightIsBoundary)
            {
                return true;
            }

            index++;
        }

        return false;
    }

    private static bool IsWordCharacter(char value)
        => char.IsAsciiLetterOrDigit(value) || value == '_';
}
