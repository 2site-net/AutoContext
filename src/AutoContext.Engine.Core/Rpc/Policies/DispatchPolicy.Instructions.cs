namespace AutoContext.Engine.Core.Rpc.Policies;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Instructions.Parser;

using Microsoft.Extensions.Logging;

/// <summary>
/// The <c>Instructions.*</c> read handlers for <see cref="DispatchPolicy"/>.
/// Each handler reads the immutable manifest snapshot, the override
/// inventory, and the workspace config through their accessor seams and
/// projects body text through <see cref="InstructionsBodyProjector"/> or
/// <see cref="InstructionsFullTextSearchService"/>. Recoverable input
/// faults reply <see cref="JsonRpcErrorCodes.InvalidParams"/>; unexpected
/// failures reply <see cref="JsonRpcErrorCodes.InternalError"/>; in both
/// cases the connection keeps serving.
/// </summary>
internal sealed partial class DispatchPolicy
{
    private UnaryHandlerResult HandleInstructionsList(JsonRpcRequest request)
    {
        if (TryDeserialize(
                request,
                InstructionsMethods.List,
                ProtocolJsonContext.Default.JsonInstructionsListParams,
                out var parameters) is { } failure)
        {
            return failure;
        }

        try
        {
            var includeSections = parameters?.IncludeSections ?? true;
            var applyWorkspaceFilter = parameters?.ApplyToWorkspaceFilter ?? true;
            var hintExtensions = string.IsNullOrWhiteSpace(parameters?.ApplyToHint)
                ? null
                : FrontmatterApplyToParser.Parse(parameters.ApplyToHint).Extensions;

            var snapshot = _manifestAccessor.Current;
            var overrides = _overridesAccessor.Current;
            var workspacePath = _workspaceAccessor.EngineInfo.WorkspacePath;
            var workspaceExtensions = new HashSet<string>(
                _workspaceAccessor.Current.Extensions, StringComparer.OrdinalIgnoreCase);

            var rows = new List<JsonInstructionsListRow>(snapshot.Files.Count);

            foreach (var entry in snapshot.Files)
            {
                if (applyWorkspaceFilter && !PassesExtensionFilter(entry, workspaceExtensions))
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

            var result = new JsonInstructionsListResult { Files = rows };
            return Success(result, ProtocolJsonContext.Default.JsonInstructionsListResult);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogInstructionsFailed(_logger, InstructionsMethods.List, ex);
            return InternalError("Failed to list the instruction corpus.");
        }
    }

    private UnaryHandlerResult HandleInstructionsCategories()
    {
        var categories = _manifestAccessor.Current.Categories;
        var mapped = new List<JsonInstructionsCategory>(categories.Count);

        foreach (var category in categories)
        {
            mapped.Add(new JsonInstructionsCategory
            {
                Name = category.Name,
                Description = category.Description,
            });
        }

        var result = new JsonInstructionsCategoriesResult { Categories = mapped };
        return Success(result, ProtocolJsonContext.Default.JsonInstructionsCategoriesResult);
    }

    private async Task<RpcHandlerResult> HandleInstructionsGetAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (TryDeserialize(
                request,
                InstructionsMethods.Get,
                ProtocolJsonContext.Default.JsonInstructionsGetParams,
                out var parameters) is { } failure)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(parameters?.Name))
        {
            return InvalidParams(InstructionsMethods.Get);
        }

        var name = parameters.Name;
        var entry = ResolveEntry(name);

        if (entry is null)
        {
            return GetResult(new JsonInstructionsGetNotFoundResult { Name = name });
        }

        if (IsFileDisabled(entry.Key))
        {
            return GetResult(new JsonInstructionsGetDisabledResult { Name = entry.Name, Key = entry.Key });
        }

        try
        {
            var body = await _bodyProjector
                .ToResponseBodyAsync(entry, parameters.Sections, cancellationToken)
                .ConfigureAwait(false);

            return GetResult(new JsonInstructionsGetOkResult
            {
                Name = entry.Name,
                Key = entry.Key,
                FileName = entry.FileName,
                Content = body.Content,
                ReturnedSections = body.ReturnedSections,
                NotFoundSections = body.NotFoundSections.Count > 0 ? body.NotFoundSections : null,
            });
        }
        catch (FileNotFoundException)
        {
            // The manifest lists the file but its body vanished from disk.
            return GetResult(new JsonInstructionsGetNotFoundResult { Name = name });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogInstructionsFailed(_logger, InstructionsMethods.Get, ex);
            return InternalError("Failed to read the instruction file.");
        }
    }

    private async Task<RpcHandlerResult> HandleInstructionsGetAllAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var files = await ProjectFilesAsync(
                entry => !IsFileDisabled(entry.Key), cancellationToken).ConfigureAwait(false);

            var result = new JsonInstructionsFilesResult { Files = files };
            return Success(result, ProtocolJsonContext.Default.JsonInstructionsFilesResult);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogInstructionsFailed(_logger, InstructionsMethods.GetAll, ex);
            return InternalError("Failed to read the instruction corpus.");
        }
    }

    private async Task<RpcHandlerResult> HandleInstructionsGetAlwaysAttachedAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var files = await ProjectFilesAsync(
                entry => entry.AlwaysAttached && !IsFileDisabled(entry.Key),
                cancellationToken).ConfigureAwait(false);

            var result = new JsonInstructionsFilesResult { Files = files };
            return Success(result, ProtocolJsonContext.Default.JsonInstructionsFilesResult);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogInstructionsFailed(_logger, InstructionsMethods.GetAlwaysAttached, ex);
            return InternalError("Failed to read the instruction corpus.");
        }
    }

    private async Task<RpcHandlerResult> HandleInstructionsGetRawAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (TryDeserialize(
                request,
                InstructionsMethods.GetRaw,
                ProtocolJsonContext.Default.JsonInstructionsGetRawParams,
                out var parameters) is { } failure)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(parameters?.Name))
        {
            return InvalidParams(InstructionsMethods.GetRaw);
        }

        var name = parameters.Name;
        var entry = ResolveEntry(name);

        if (entry is null)
        {
            return RawNotFound(name);
        }

        try
        {
            switch (parameters.Source)
            {
                case InstructionsRawSource.Bundled:
                    {
                        var content = await _fileReader
                            .ReadOriginalFileAsync(entry.FileName, cancellationToken)
                            .ConfigureAwait(false);
                        return content is null ? RawNotFound(name) : RawOk(entry, InstructionsSource.Bundled, content);
                    }

                case InstructionsRawSource.Override:
                    {
                        var content = await _fileReader
                            .ReadOverrideFileAsync(entry.FileName, cancellationToken)
                            .ConfigureAwait(false);
                        return content is null ? RawNotFound(name) : RawOk(entry, InstructionsSource.Override, content);
                    }

                case InstructionsRawSource.Active:
                default:
                    {
                        var overrideContent = await _fileReader
                            .ReadOverrideFileAsync(entry.FileName, cancellationToken)
                            .ConfigureAwait(false);

                        if (overrideContent is not null)
                        {
                            return RawOk(entry, InstructionsSource.Override, overrideContent);
                        }

                        var bundled = await _fileReader
                            .ReadOriginalFileAsync(entry.FileName, cancellationToken)
                            .ConfigureAwait(false);
                        return bundled is null ? RawNotFound(name) : RawOk(entry, InstructionsSource.Bundled, bundled);
                    }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogInstructionsFailed(_logger, InstructionsMethods.GetRaw, ex);
            return InternalError("Failed to read the instruction source.");
        }
    }

    private async Task<RpcHandlerResult> HandleInstructionsSearchContentAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (TryDeserialize(
                request,
                InstructionsMethods.SearchContent,
                ProtocolJsonContext.Default.JsonInstructionsSearchContentParams,
                out var parameters) is { } failure)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(parameters?.Query))
        {
            return InvalidParams(InstructionsMethods.SearchContent);
        }

        try
        {
            var includeDisabled = parameters.IncludeDisabled ?? false;
            var hits = await _searchService
                .SearchAsync(parameters.Query, parameters.Limit, includeDisabled, cancellationToken)
                .ConfigureAwait(false);

            var mapped = new List<JsonInstructionsContentHit>(hits.Count);

            foreach (var hit in hits)
            {
                mapped.Add(MapHit(hit));
            }

            var result = new JsonInstructionsSearchContentResult { Hits = mapped };
            return Success(result, ProtocolJsonContext.Default.JsonInstructionsSearchContentResult);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogInstructionsFailed(_logger, InstructionsMethods.SearchContent, ex);
            return InternalError("Failed to search the instruction corpus.");
        }
    }

    private async Task<IReadOnlyList<JsonInstructionsFile>> ProjectFilesAsync(
        Func<InstructionsFileManifestEntry, bool> predicate,
        CancellationToken cancellationToken)
    {
        var files = new List<JsonInstructionsFile>();

        foreach (var entry in _manifestAccessor.Current.Files)
        {
            if (!predicate(entry))
            {
                continue;
            }

            InstructionsResponseBody body;

            try
            {
                body = await _bodyProjector
                    .ToResponseBodyAsync(entry, null, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                // A manifest entry whose body vanished is skipped from the bulk dump.
                continue;
            }

            files.Add(new JsonInstructionsFile
            {
                Name = entry.Name,
                Key = entry.Key,
                FileName = entry.FileName,
                Content = body.Content,
                Sections = MapSections(entry.Sections),
            });
        }

        return files;
    }

    private InstructionsFileManifestEntry? ResolveEntry(string name)
    {
        var snapshot = _manifestAccessor.Current;

        return snapshot.FindByFileName(name)
            ?? snapshot.Files.FirstOrDefault(file => string.Equals(file.Key, name, StringComparison.Ordinal));
    }

    private bool IsFileDisabled(string key) =>
        Array.Find(
            _configAccessor.Current.Instructions,
            file => string.Equals(file.Name, key, StringComparison.Ordinal))?.Disabled == true;

    private UnaryHandlerResult? TryDeserialize<T>(
        JsonRpcRequest request,
        string method,
        JsonTypeInfo<T> typeInfo,
        out T? parameters)
    {
        try
        {
            parameters = request.Params is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } element
                ? element.Deserialize(typeInfo)
                : default;
            return null;
        }
        catch (JsonException ex)
        {
            LogParamsParseFailed(_logger, method, ex);
            parameters = default;
            return InvalidParams(method);
        }
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

    private static List<JsonInstructionsSection> MapSections(
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

    private static JsonInstructionsContentHit MapHit(InstructionsContentHit hit)
    {
        var excerpts = new List<JsonInstructionsContentExcerpt>(hit.Excerpts.Count);

        foreach (var excerpt in hit.Excerpts)
        {
            excerpts.Add(new JsonInstructionsContentExcerpt
            {
                Anchor = excerpt.Anchor,
                Snippet = excerpt.Snippet,
                Line = excerpt.Line,
            });
        }

        return new JsonInstructionsContentHit
        {
            Name = hit.Name,
            Key = hit.Key,
            FileName = hit.FileName,
            Description = hit.Description,
            Score = hit.Score,
            Excerpts = excerpts,
        };
    }

    private static UnaryHandlerResult GetResult(JsonInstructionsGetResult result) =>
        Success(result, ProtocolJsonContext.Default.JsonInstructionsGetResult);

    private static UnaryHandlerResult RawOk(
        InstructionsFileManifestEntry entry,
        InstructionsSource source,
        string content) =>
        Success(
            new JsonInstructionsGetRawOkResult
            {
                Name = entry.Name,
                Key = entry.Key,
                Source = source,
                Content = content,
            },
            ProtocolJsonContext.Default.JsonInstructionsGetRawResult);

    private static UnaryHandlerResult RawNotFound(string name) =>
        Success(
            new JsonInstructionsGetRawNotFoundResult { Name = name },
            ProtocolJsonContext.Default.JsonInstructionsGetRawResult);

    private static UnaryHandlerResult Success<T>(T result, JsonTypeInfo<T> typeInfo) =>
        new(
            Response: new JsonRpcResponse
            {
                Result = JsonSerializer.SerializeToElement(result, typeInfo),
            },
            Continuation: Continuation.Continue);

    private static UnaryHandlerResult InternalError(string message) =>
        new(
            Response: new JsonRpcResponse
            {
                Error = new JsonRpcError
                {
                    Code = JsonRpcErrorCodes.InternalError,
                    Message = message,
                },
            },
            Continuation: Continuation.Continue);

    [LoggerMessage(EventId = 61, Level = LogLevel.Warning,
        Message = "Instructions handler '{Method}' failed.")]
    private static partial void LogInstructionsFailed(ILogger logger, string method, Exception exception);
}
