namespace AutoContext.Engine.Core.Features.Instructions;

/// <summary>
/// One recognised metadata predicate field, describing the JSON value type it
/// expects and how that value is interpreted. The frozen set of these is
/// returned with every <see cref="InstructionsMetadataSearchError"/> so the
/// caller can correct an invalid predicate without a second lookup.
/// </summary>
/// <param name="Field">The predicate field name.</param>
/// <param name="Type">The expected JSON value type (<c>string</c> /
/// <c>number</c> / <c>boolean</c>).</param>
/// <param name="Match">How the value is interpreted (<c>regex</c> / <c>glob</c>
/// / <c>equality</c>).</param>
internal sealed record InstructionsMetadataFieldDescriptor(
    string Field,
    string Type,
    string Match);
