namespace AutoContext.Engine.Core.Features.Instructions;

using System.Collections.Generic;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// One file that satisfied a metadata predicate: the matched manifest
/// <see cref="Entry"/> and the anchors of the sections that satisfied every
/// <c>sections.*</c> clause (<see langword="null"/> when the predicate named no
/// <c>sections.*</c> field).
/// </summary>
/// <param name="Entry">The matched manifest entry.</param>
/// <param name="MatchedAnchors">The matched section anchors, or
/// <see langword="null"/> when no <c>sections.*</c> clause was present.</param>
internal sealed record InstructionsMetadataMatch(
    InstructionsFileManifestEntry Entry,
    IReadOnlyList<string>? MatchedAnchors);
