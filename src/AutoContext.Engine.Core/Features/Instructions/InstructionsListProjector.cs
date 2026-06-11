namespace AutoContext.Engine.Core.Features.Instructions;

using System;
using System.Collections.Generic;
using System.IO;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Protocol.Messages.Instructions;

/// <summary>
/// Projects the immutable manifest snapshot, the override inventory,
/// and the workspace config into the identity
/// <see cref="JsonInstructionsListRow"/> rows shared by the
/// <c>Instructions.List</c> RPC and the <c>Instructions.Subscribe</c>
/// snapshot frame. Centralising the projection keeps the listing
/// shape — disabled resolution, override source, section index — in
/// one place so both surfaces project each row identically.
/// </summary>
internal sealed class InstructionsListProjector
{
    private readonly IConfigSnapshotAccessor _configAccessor;
    private readonly IInstructionsManifestAccessor _manifestAccessor;
    private readonly IInstructionsOverridesAccessor _overridesAccessor;
    private readonly IWorkspaceContextAccessor _workspaceAccessor;

    /// <summary>
    /// Creates a new <see cref="InstructionsListProjector"/>.
    /// </summary>
    /// <param name="manifestAccessor">Read seam over the immutable
    /// instruction manifest snapshot.</param>
    /// <param name="overridesAccessor">Read seam over the workspace
    /// override inventory used to mark overridden rows.</param>
    /// <param name="configAccessor">Read seam over the workspace
    /// config used to resolve each row's disabled state.</param>
    /// <param name="workspaceAccessor">Read seam over the workspace
    /// context, supplying the workspace path and detected
    /// extensions.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public InstructionsListProjector(
        IInstructionsManifestAccessor manifestAccessor,
        IInstructionsOverridesAccessor overridesAccessor,
        IConfigSnapshotAccessor configAccessor,
        IWorkspaceContextAccessor workspaceAccessor)
    {
        ArgumentNullException.ThrowIfNull(manifestAccessor);
        ArgumentNullException.ThrowIfNull(overridesAccessor);
        ArgumentNullException.ThrowIfNull(configAccessor);
        ArgumentNullException.ThrowIfNull(workspaceAccessor);

        _manifestAccessor = manifestAccessor;
        _overridesAccessor = overridesAccessor;
        _configAccessor = configAccessor;
        _workspaceAccessor = workspaceAccessor;
    }

    /// <summary>
    /// Projects the whole corpus — every bundled and override file,
    /// sections included, no workspace or hint filtering — as the
    /// snapshot-on-subscribe payload for <c>Instructions.Subscribe</c>.
    /// </summary>
    /// <returns>The full listing as identity rows.</returns>
    public IReadOnlyList<JsonInstructionsListRow> ProjectAll() =>
        Project(includeSections: true, applyWorkspaceFilter: false, hintExtensions: null);

    /// <summary>
    /// Projects the corpus into identity rows, optionally dropping
    /// rows whose <c>applyTo</c> extension set is disjoint from the
    /// workspace extensions and/or a caller-supplied hint set.
    /// </summary>
    /// <param name="includeSections">Whether each row carries its
    /// section index.</param>
    /// <param name="applyWorkspaceFilter">Whether to drop rows whose
    /// extensions are disjoint from the workspace's detected
    /// extensions (always-attached files are exempt).</param>
    /// <param name="hintExtensions">Optional extension hint set; rows
    /// whose extensions are disjoint from it are dropped.</param>
    /// <returns>The projected listing as identity rows.</returns>
    public IReadOnlyList<JsonInstructionsListRow> Project(
        bool includeSections,
        bool applyWorkspaceFilter,
        IReadOnlySet<string>? hintExtensions)
    {
        var snapshot = _manifestAccessor.Current;
        var overrides = _overridesAccessor.Current;
        var workspacePath = _workspaceAccessor.EngineInfo.WorkspacePath;
        var workspaceExtensions = applyWorkspaceFilter
            ? new HashSet<string>(
                _workspaceAccessor.Current.Extensions, StringComparer.OrdinalIgnoreCase)
            : null;

        var rows = new List<JsonInstructionsListRow>(snapshot.Files.Count);

        foreach (var entry in snapshot.Files)
        {
            if (workspaceExtensions is not null && !PassesExtensionFilter(entry, workspaceExtensions))
            {
                continue;
            }

            if (hintExtensions is not null && !PassesExtensionFilter(entry, hintExtensions))
            {
                continue;
            }

            rows.Add(CreateListRow(
                entry,
                overrides,
                workspacePath,
                IsFileDisabled(entry.Key),
                includeSections));
        }

        return rows;
    }

    /// <summary>
    /// Maps a manifest entry's section index onto its wire shape.
    /// Shared with the body-projection handlers so the section
    /// mapping lives in one place.
    /// </summary>
    /// <param name="sections">The entry's section index.</param>
    /// <returns>The mapped wire sections.</returns>
    internal static List<JsonInstructionsSection> MapSections(
        IReadOnlyList<InstructionsSection> sections)
    {
        if (sections.Count == 0)
        {
            return [];
        }

        var mapped = new List<JsonInstructionsSection>(sections.Count);

        foreach (var section in sections)
        {
            mapped.Add(new JsonInstructionsSection
            {
                Heading = section.Heading,
                Anchor = section.Anchor,
                Parent = section.Parent,
            });
        }

        return mapped;
    }

    private static JsonInstructionsListRow CreateListRow(
        InstructionsFileManifestEntry entry,
        InstructionsOverridesSnapshot overrides,
        string workspacePath,
        bool disabled,
        bool includeSections)
    {
        var isOverride = overrides.TryGetPath(entry.FileName, out var overridePath) && overridePath is not null;

        return new JsonInstructionsListRow
        {
            Key = entry.Key,
            FileName = entry.FileName,
            Name = entry.Name,
            Version = entry.Version,
            Description = entry.Description,
            ApplyTo = entry.ApplyTo,
            HasChangelog = entry.HasChangelog,
            ContentHash = entry.ContentHash,
            AlwaysAttached = entry.AlwaysAttached,
            Label = entry.Label,
            Categories = entry.Categories,
            Disabled = disabled,
            Source = isOverride ? InstructionsSource.Override : InstructionsSource.Bundled,
            OverridePath = overridePath is not null ? ToWorkspaceRelative(workspacePath, overridePath) : null,
            Sections = includeSections ? MapSections(entry.Sections) : null,
        };
    }

    private static bool PassesExtensionFilter(
        InstructionsFileManifestEntry entry,
        IReadOnlySet<string> candidateExtensions)
    {
        if (entry.AlwaysAttached)
        {
            return true;
        }

        if (entry.Extensions is not { Count: > 0 } entryExtensions)
        {
            return false;
        }

        foreach (var extension in entryExtensions)
        {
            if (candidateExtensions.Contains(extension))
            {
                return true;
            }
        }

        return false;
    }

    private static string ToWorkspaceRelative(string workspacePath, string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return absolutePath.Replace('\\', '/');
        }

        return Path.GetRelativePath(workspacePath, absolutePath).Replace('\\', '/');
    }

    private bool IsFileDisabled(string key) =>
        Array.Find(
            _configAccessor.Current.Instructions,
            file => string.Equals(file.Name, key, StringComparison.Ordinal))?.Disabled == true;
}
