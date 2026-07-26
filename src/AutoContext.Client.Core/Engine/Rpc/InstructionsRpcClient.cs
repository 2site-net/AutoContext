namespace AutoContext.Client.Core.Engine.Rpc;

using System.Text.Json;

using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Typed client for the engine's <c>Instructions.*</c> RPC family over
/// a live <see cref="EngineConnection"/>. Covers the corpus listing,
/// taxonomy, per-file projected and raw reads, and the two content /
/// metadata searches. The reads that distinguish disabled from
/// not-found (<see cref="GetAsync"/>, <see cref="GetRawAsync"/>,
/// <see cref="SearchByMetadataAsync"/>) return their discriminated
/// result base so callers branch on the arm rather than a nullable.
/// The <c>Instructions.Subscribe</c> stream is a separate consumer
/// (<c>Subscriptions.InstructionsSubscription</c>).
/// </summary>
public sealed class InstructionsRpcClient
{
    private readonly EngineConnection _connection;

    /// <summary>
    /// Creates a new <see cref="InstructionsRpcClient"/> over
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">A live, handshaked <c>rpc</c>
    /// connection. Must not be <see langword="null"/>.</param>
    public InstructionsRpcClient(EngineConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    /// <summary>
    /// Returns the curated category definitions (name + description)
    /// the per-file <see cref="JsonInstructionsListRow.Category"/>
    /// membership resolves against. Static for the engine's lifetime.
    /// </summary>
    public Task<JsonInstructionsCategoriesResult> CategoriesAsync(CancellationToken cancellationToken)
        => _connection.InvokeAsync(
            InstructionsMethods.Categories,
            parameters: null,
            ProtocolJsonContext.Default.JsonInstructionsCategoriesResult,
            cancellationToken);

    /// <summary>
    /// Bulk read of every non-disabled file's projected body.
    /// </summary>
    public Task<JsonInstructionsFilesResult> GetAllAsync(CancellationToken cancellationToken)
        => _connection.InvokeAsync(
            InstructionsMethods.GetAll,
            parameters: null,
            ProtocolJsonContext.Default.JsonInstructionsFilesResult,
            cancellationToken);

    /// <summary>
    /// Returns only the non-disabled files the catalog declares as
    /// always-attached, in deterministic order.
    /// </summary>
    public Task<JsonInstructionsFilesResult> GetAlwaysAttachedAsync(CancellationToken cancellationToken)
        => _connection.InvokeAsync(
            InstructionsMethods.GetAlwaysAttached,
            parameters: null,
            ProtocolJsonContext.Default.JsonInstructionsFilesResult,
            cancellationToken);

    /// <summary>
    /// Reads one file's projected body (disabled rules filtered,
    /// override preferred over bundled), optionally sliced to
    /// <paramref name="sections"/>. Returns the discriminated result —
    /// <c>ok</c> / <c>disabled</c> / <c>not-found</c>.
    /// </summary>
    /// <param name="name">Corpus file name. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="sections">Section anchors to slice down to, or
    /// <see langword="null"/> for the whole body.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonInstructionsGetResult> GetAsync(
        string name, IReadOnlyList<string>? sections, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonInstructionsGetParams { Name = name, Sections = sections },
            ProtocolJsonContext.Default.JsonInstructionsGetParams);

        return _connection.InvokeAsync(
            InstructionsMethods.Get,
            parameters,
            ProtocolJsonContext.Default.JsonInstructionsGetResult,
            cancellationToken);
    }

    /// <summary>
    /// Returns the source-faithful on-disk bytes of one file under
    /// explicit override resolution. Returns the discriminated result —
    /// <c>ok</c> / <c>not-found</c>.
    /// </summary>
    /// <param name="name">Corpus file name. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="source">Override-resolution selector.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonInstructionsGetRawResult> GetRawAsync(
        string name, InstructionsRawSource source, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonInstructionsGetRawParams { Name = name, Source = source },
            ProtocolJsonContext.Default.JsonInstructionsGetRawParams);

        return _connection.InvokeAsync(
            InstructionsMethods.GetRaw,
            parameters,
            ProtocolJsonContext.Default.JsonInstructionsGetRawResult,
            cancellationToken);
    }

    /// <summary>
    /// Lists every bundled and override file as an identity row
    /// (disabled rows carry <c>disabled: true</c>).
    /// </summary>
    /// <param name="includeSections">Whether each row carries its
    /// section index, or <see langword="null"/> for the engine
    /// default.</param>
    /// <param name="applyToWorkspaceFilter">Whether to drop rows whose
    /// <c>applyTo</c> extension set is disjoint from the detected
    /// workspace extensions, or <see langword="null"/> for the engine
    /// default.</param>
    /// <param name="applyToHint">Extension-only narrowing hint (e.g.
    /// <c>".ts"</c>), or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonInstructionsListResult> ListAsync(
        bool? includeSections,
        bool? applyToWorkspaceFilter,
        string? applyToHint,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.SerializeToElement(
            new JsonInstructionsListParams
            {
                IncludeSections = includeSections,
                ApplyToWorkspaceFilter = applyToWorkspaceFilter,
                ApplyToHint = applyToHint,
            },
            ProtocolJsonContext.Default.JsonInstructionsListParams);

        return _connection.InvokeAsync(
            InstructionsMethods.List,
            parameters,
            ProtocolJsonContext.Default.JsonInstructionsListResult,
            cancellationToken);
    }

    /// <summary>
    /// Evaluates a free-form metadata <paramref name="predicate"/>
    /// against the corpus and returns the matched rows. Returns the
    /// discriminated result — <c>ok</c> / <c>error</c> (structured
    /// predicate fault).
    /// </summary>
    /// <param name="predicate">Metadata predicate object, or
    /// <see langword="null"/> to match every file.</param>
    /// <param name="includeSections">Whether each matched row carries
    /// its section index, or <see langword="null"/> for the engine
    /// default.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonInstructionsSearchByMetadataResult> SearchByMetadataAsync(
        JsonElement? predicate, bool? includeSections, CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.SerializeToElement(
            new JsonInstructionsSearchByMetadataParams
            {
                Predicate = predicate,
                IncludeSections = includeSections,
            },
            ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataParams);

        return _connection.InvokeAsync(
            InstructionsMethods.SearchByMetadata,
            parameters,
            ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataResult,
            cancellationToken);
    }

    /// <summary>
    /// Runs an engine-owned content search over the projected corpus.
    /// </summary>
    /// <param name="query">The search query. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="limit">Maximum hits to return, or
    /// <see langword="null"/> for the engine default.</param>
    /// <param name="includeDisabled">Whether disabled files
    /// participate, or <see langword="null"/> to exclude them.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonInstructionsSearchContentResult> SearchContentAsync(
        string query, int? limit, bool? includeDisabled, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(query);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonInstructionsSearchContentParams
            {
                Query = query,
                Limit = limit,
                IncludeDisabled = includeDisabled,
            },
            ProtocolJsonContext.Default.JsonInstructionsSearchContentParams);

        return _connection.InvokeAsync(
            InstructionsMethods.SearchContent,
            parameters,
            ProtocolJsonContext.Default.JsonInstructionsSearchContentResult,
            cancellationToken);
    }
}
