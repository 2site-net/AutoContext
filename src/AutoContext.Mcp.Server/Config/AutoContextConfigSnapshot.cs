namespace AutoContext.Mcp.Server.Config;

using System.Collections.Immutable;

/// <summary>
/// Thread-safe holder for the latest disabled-tool / disabled-task
/// projection of <c>.autocontext.json</c> received from the
/// extension's <c>AutoContextConfigServer</c>. Registered as a
/// singleton; updated by <see cref="AutoContextConfigClient"/> via
/// <see cref="Update"/> and read by the request handlers in
/// <see cref="Tools.McpSdkAdapter"/> and the per-task dispatch path.
/// </summary>
/// <remarks>
/// The two collections are stored together inside a single
/// immutable <see cref="State"/> record so reader paths always see a
/// consistent (tools, tasks) pair: <see cref="Volatile.Read{T}"/> /
/// <see cref="Volatile.Write{T}"/> swap the whole record atomically.
/// The default value (no extension connection / standalone runs) is
/// "nothing disabled".
/// </remarks>
public sealed class AutoContextConfigSnapshot
{
    private static readonly State EmptyState = new(
        ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal),
        ImmutableDictionary<string, ImmutableHashSet<string>>.Empty.WithComparers(StringComparer.Ordinal));

    private State _state = EmptyState;

    /// <summary>
    /// Raised after <see cref="Update"/> swaps in a new snapshot that
    /// differs from the previous one. Fires synchronously on the
    /// caller's thread; subscribers must not block.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Names of MCP Tools the user has disabled in
    /// <c>.autocontext.json</c>. <see cref="Tools.McpSdkAdapter"/>
    /// filters these out of the <c>tools/list</c> response so they
    /// disappear from the VS Code Quick Pick.
    /// </summary>
    public ImmutableHashSet<string> DisabledTools => Volatile.Read(ref _state).Tools;

    /// <summary>
    /// Per-tool sets of disabled MCP Task names. The parent tool's
    /// own enabled state is independent of its disabled-tasks set
    /// (mirrors the extension's <c>AutoContextConfig.isToolEnabled</c>).
    /// </summary>
    public ImmutableDictionary<string, ImmutableHashSet<string>> DisabledTasks => Volatile.Read(ref _state).Tasks;

    /// <summary>
    /// Convenience predicate for the dispatch path; <see langword="true"/>
    /// when <paramref name="toolName"/> appears in
    /// <see cref="DisabledTools"/>.
    /// </summary>
    public bool IsToolDisabled(string toolName)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        return Volatile.Read(ref _state).Tools.Contains(toolName);
    }

    /// <summary>
    /// Convenience predicate for the dispatch path; <see langword="true"/>
    /// when the tool's entry in <see cref="DisabledTasks"/> contains
    /// <paramref name="taskName"/>.
    /// </summary>
    public bool IsTaskDisabled(string toolName, string taskName)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        ArgumentException.ThrowIfNullOrEmpty(taskName);

        var tasks = Volatile.Read(ref _state).Tasks;
        return tasks.TryGetValue(toolName, out var disabled) && disabled.Contains(taskName);
    }

    /// <summary>
    /// Replaces the held snapshot with the projection of
    /// <paramref name="dto"/>. Returns <see langword="true"/> when the
    /// new snapshot differs from the previous one (and raises
    /// <see cref="Changed"/>); <see langword="false"/> on a no-op
    /// frame (e.g. handshake replay after reconnect).
    /// </summary>
    internal bool Update(AutoContextConfigSnapshotDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var newTools = dto.DisabledTools is { Count: > 0 }
            ? dto.DisabledTools.ToImmutableHashSet(StringComparer.Ordinal)
            : EmptyState.Tools;

        var newTasks = BuildTasksMap(dto.DisabledTasks);

        var current = Volatile.Read(ref _state);

        if (current.Tools.SetEquals(newTools) && TasksEqual(current.Tasks, newTasks))
        {
            return false;
        }

        Volatile.Write(ref _state, new State(newTools, newTasks));

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static ImmutableDictionary<string, ImmutableHashSet<string>> BuildTasksMap(
        Dictionary<string, List<string>>? source)
    {
        if (source is null || source.Count == 0)
        {
            return EmptyState.Tasks;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>(StringComparer.Ordinal);

        foreach (var (toolName, taskNames) in source)
        {
            if (string.IsNullOrEmpty(toolName) || taskNames is null || taskNames.Count == 0)
            {
                continue;
            }

            builder[toolName] = taskNames.ToImmutableHashSet(StringComparer.Ordinal);
        }

        return builder.ToImmutable();
    }

    private static bool TasksEqual(
        ImmutableDictionary<string, ImmutableHashSet<string>> a,
        ImmutableDictionary<string, ImmutableHashSet<string>> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (key, aTasks) in a)
        {
            if (!b.TryGetValue(key, out var bTasks) || !aTasks.SetEquals(bTasks))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record State(
        ImmutableHashSet<string> Tools,
        ImmutableDictionary<string, ImmutableHashSet<string>> Tasks);
}
