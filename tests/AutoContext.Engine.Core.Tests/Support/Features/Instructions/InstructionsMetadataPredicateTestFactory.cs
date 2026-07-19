namespace AutoContext.Engine.Core.Tests.Support.Features.Instructions;

using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Builds the free-form metadata predicate <see cref="JsonElement"/> that
/// <c>InstructionsMetadataSearchService.Evaluate</c> and the
/// <c>Instructions.SearchByMetadata</c> handler consume, from a set of
/// field/value pairs. Supports the dotted <c>sections.*</c> keys that an
/// anonymous type cannot express.
/// </summary>
internal static class InstructionsMetadataPredicateTestFactory
{
    /// <summary>
    /// Serializes <paramref name="pairs"/> into a predicate object element.
    /// </summary>
    /// <param name="pairs">The field/value clauses.</param>
    /// <returns>The predicate as a JSON object element.</returns>
    public static JsonElement Build(params (string Field, object Value)[] pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        var map = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var (field, value) in pairs)
        {
            map[field] = value;
        }

        return JsonSerializer.SerializeToElement(map);
    }
}
