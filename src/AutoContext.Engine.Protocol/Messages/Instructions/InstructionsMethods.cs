namespace AutoContext.Engine.Protocol.Messages.Instructions;

/// <summary>
/// JSON-RPC method-name constants for the <c>Instructions.*</c>
/// family — the engine's authority over the bundled instructions
/// corpus and its per-workspace projection. Grouped here so handlers
/// and transports share one spelling of each dotted method name per
/// <c>design § RPC surface</c>.
/// </summary>
public static class InstructionsMethods
{
    /// <summary>
    /// Listing RPC. Returns one identity <see cref="JsonInstructionsListRow"/>
    /// per bundled and override file — disabled rows carry
    /// <c>disabled: true</c> so a tree view can render the toggle UI.
    /// Bodies are never included. Takes
    /// <see cref="JsonInstructionsListParams"/>; returns
    /// <see cref="JsonInstructionsListResult"/>.
    /// </summary>
    public const string List = "Instructions.List";

    /// <summary>
    /// Taxonomy RPC. Returns the curated category definitions
    /// (<c>name</c> + <c>description</c>) hand-authored in
    /// <c>instructions-catalog.json</c> — static for the engine's
    /// process lifetime, so clients fetch it once and cache it. The
    /// per-file <see cref="JsonInstructionsListRow.Category"/>
    /// membership string on <see cref="List"/> rows resolves against
    /// these definitions. Takes no params; returns
    /// <see cref="JsonInstructionsCategoriesResult"/>.
    /// </summary>
    public const string Categories = "Instructions.Categories";

    /// <summary>
    /// Reads one file's projected body (disabled rules filtered,
    /// <c>[INSTxxxx]</c> tags stripped, override preferred over
    /// bundled). Takes <see cref="JsonInstructionsGetParams"/>;
    /// returns the discriminated <see cref="JsonInstructionsGetResult"/>
    /// — <c>ok</c> / <c>disabled</c> (identity only) / <c>not-found</c>
    /// per <c>design § P2</c>.
    /// </summary>
    public const string Get = "Instructions.Get";

    /// <summary>
    /// Bulk read of every non-disabled file's projected body — the
    /// tree-view render and CLI-dump path. Filters disabled files
    /// unconditionally (consumers that need disabled identity read
    /// <see cref="List"/>). Takes no params; returns
    /// <see cref="JsonInstructionsFilesResult"/>.
    /// </summary>
    public const string GetAll = "Instructions.GetAll";

    /// <summary>
    /// Returns only the non-disabled files the catalog declares in its
    /// <c>alwaysAttached[]</c> array, in deterministic order — the
    /// SessionStart / PreCompact consumer. Never returns a disabled
    /// identity envelope. Takes no params; returns
    /// <see cref="JsonInstructionsFilesResult"/>.
    /// </summary>
    public const string GetAlwaysAttached = "Instructions.GetAlwaysAttached";

    /// <summary>
    /// Returns the source-faithful bytes of the on-disk markdown file
    /// — frontmatter and <c>[INSTxxxx]</c> tags intact, no disabled
    /// filter — with override resolution under explicit caller control
    /// via <see cref="JsonInstructionsGetRawParams.Source"/>. Backs the
    /// rule-toggle CodeLens and "open instruction source" commands.
    /// Returns the discriminated <see cref="JsonInstructionsGetRawResult"/>
    /// — <c>ok</c> / <c>not-found</c> (no <c>disabled</c> branch).
    /// </summary>
    public const string GetRaw = "Instructions.GetRaw";

    /// <summary>
    /// Engine-owned content search over the projected corpus index.
    /// Disabled files are excluded by default. Takes
    /// <see cref="JsonInstructionsSearchContentParams"/>; returns
    /// <see cref="JsonInstructionsSearchContentResult"/>.
    /// </summary>
    public const string SearchContent = "Instructions.SearchContent";

    /// <summary>
    /// Engine-owned metadata search over the corpus. Evaluates a free-form
    /// field predicate (case-insensitive regex for string fields, coarse
    /// <c>applyTo</c> glob intersection, boolean/number equality, and
    /// per-section <c>sections.*</c> AND-intersection) and returns the matched
    /// identity rows with their matched section anchors. Disabled files are
    /// omitted. Takes <see cref="JsonInstructionsSearchByMetadataParams"/>;
    /// returns the discriminated <see cref="JsonInstructionsSearchByMetadataResult"/>
    /// — <c>ok</c> (matched rows) / <c>error</c> (structured predicate fault).
    /// </summary>
    public const string SearchByMetadata = "Instructions.SearchByMetadata";

    /// <summary>
    /// Opens a server-streaming subscription to the corpus. The engine
    /// emits one <see cref="JsonInstructionsStreamFrame"/> per frame — a
    /// <see cref="JsonInstructionsSnapshotFrame"/> with the current
    /// listing at subscribe time (snapshot-on-subscribe) and again on
    /// every corpus reload, or a terminal
    /// <see cref="JsonInstructionsDroppedFrame"/> for a slow subscriber.
    /// </summary>
    public const string Subscribe = "Instructions.Subscribe";
}
