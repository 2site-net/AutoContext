namespace AutoContext.Engine.Core.Workers;

using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Workers.Format;
using AutoContext.Engine.Protocol;

/// <summary>
/// Transforms the parsed <see cref="JsonWorkersManifest"/> read-model into
/// the immutable <see cref="WorkerProcessInfo"/> launch specifications the
/// <see cref="WorkerProcessService"/> spawns from. This is a pure mapping: it
/// expands each row's <c>${root}</c> placeholder to the worker's staging
/// subdir, splits the launch command into an executable and its leading
/// arguments, threads the engine instance id onto every spawn, derives
/// the listen endpoint each worker is dialled on, and hands each worker
/// the engine's own rpc address as the sink for its <c>Engine.WriteLog</c>
/// records.
/// </summary>
/// <remarks>
/// The manifest is an engine build artifact, so a row missing a required
/// field or a duplicate worker id is a packaging defect and throws. The
/// launch shape is derived from the <c>command</c> alone — the row's
/// informational <c>type</c> is not consulted: the first command token is
/// the executable, and a token carrying <c>${root}</c> is the worker's own
/// staged binary (extensionless, so it gains a <c>.exe</c> suffix on
/// Windows), whereas a bare leading token such as <c>node</c> is an
/// external launcher resolved through <c>PATH</c>.
/// </remarks>
internal static class WorkerProcessInfoResolver
{
    private const string InstanceIdArgument = "--instance-id";
    private const string LogServiceRolePrefix = "log=";
    private const string RootPlaceholder = "${root}";
    private const string ServiceArgument = "--service";
    private const string WindowsExecutableSuffix = ".exe";
    private const string WorkspaceRootArgument = "--workspace-root";

    /// <summary>
    /// The worker id that receives the engine's <c>--workspace-root</c>
    /// argument; the workspace worker is the only one scoped to a single
    /// workspace tree.
    /// </summary>
    private const string WorkspaceWorkerId = "workspace";

    /// <summary>
    /// Maps every row of <paramref name="manifest"/> to a resolved
    /// <see cref="WorkerProcessInfo"/>.
    /// </summary>
    /// <param name="manifest">The parsed worker-manifest read-model.</param>
    /// <param name="workersDirectory">Absolute path of the per-worker
    /// staging root (the <c>Workers/</c> directory beside the engine
    /// binary); <c>${root}</c> expands to
    /// <c>&lt;workersDirectory&gt;/&lt;id&gt;</c>.</param>
    /// <param name="instanceId">The engine instance id threaded onto every
    /// spawn as <c>--instance-id</c> and woven into each worker's listen
    /// endpoint so worker and engine agree on the address.</param>
    /// <param name="workspacePath">The engine's workspace root; appended as
    /// <c>--workspace-root</c> to the workspace worker only. Ignored when
    /// empty.</param>
    /// <returns>The resolved launch specifications, in manifest order.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="manifest"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="workersDirectory"/> or <paramref name="instanceId"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The manifest is missing
    /// its worker list, a row is missing a required field, a command is
    /// empty, or a worker id is duplicated.</exception>
    public static IReadOnlyList<WorkerProcessInfo> Resolve(
        JsonWorkersManifest manifest,
        string workersDirectory,
        string instanceId,
        string workspacePath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(workersDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        // Every spawned worker ships its ILogger<T> records back to this
        // engine over Engine.WriteLog. Derive the engine's own rpc endpoint
        // from the shared identity (workspace hash + instance id) — the exact
        // address the engine binds its rpc pipe on — and hand it to each
        // worker as --service log=<address>. Empty when the workspace path is
        // absent (standalone resolves), which disables worker→engine logging
        // so records fall back to the worker's stderr without the call site
        // special-casing the address.
        var engineLogAddress = string.IsNullOrWhiteSpace(workspacePath)
            ? string.Empty
            : new Endpoint(
                EndpointKind.Rpc,
                WorkspaceHash.Compute(workspacePath).Value,
                Guid.Parse(instanceId)).ToString();

        var rows = manifest.Workers
            ?? throw Malformed("its 'workers' array is missing.");

        var resolved = new List<WorkerProcessInfo>(rows.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var id = Require(row.Id, "a worker row is missing its 'id'.");
            var command = Require(
                row.Command, $"worker '{id}' is missing its 'command'.");

            if (!seenIds.Add(id))
            {
                throw Malformed($"the worker id '{id}' appears more than once.");
            }

            var (executable, leadingArguments) = ParseCommand(command, workersDirectory, id);

            var arguments = new List<string>(leadingArguments)
            {
                InstanceIdArgument,
                instanceId,
            };

            if (string.Equals(id, WorkspaceWorkerId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(workspacePath))
            {
                arguments.Add(WorkspaceRootArgument);
                arguments.Add(workspacePath);
            }

            if (engineLogAddress.Length > 0)
            {
                arguments.Add(ServiceArgument);
                arguments.Add(LogServiceRolePrefix + engineLogAddress);
            }

            resolved.Add(new WorkerProcessInfo
            {
                WorkerId = id,
                Command = executable,
                Arguments = arguments,
                Endpoint = ServiceAddressFormatter.Format($"worker-{id}", instanceId),
            });
        }

        return resolved;
    }

    private static string AppendExecutableSuffix(string path)
        => OperatingSystem.IsWindows()
            && !path.EndsWith(WindowsExecutableSuffix, StringComparison.OrdinalIgnoreCase)
            ? path + WindowsExecutableSuffix
            : path;

    private static string Expand(string token, string workerRoot)
    {
        if (!token.Contains(RootPlaceholder, StringComparison.Ordinal))
        {
            return token;
        }

        return token
            .Replace(RootPlaceholder, workerRoot, StringComparison.Ordinal)
            .Replace('/', Path.DirectorySeparatorChar);
    }

    private static InvalidOperationException Malformed(string reason)
        => new($"Bundled workers manifest is malformed: {reason}");

    private static (string Executable, IReadOnlyList<string> LeadingArguments) ParseCommand(
        string command,
        string workersDirectory,
        string id)
    {
        var tokens = command.Split(
            ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
        {
            throw Malformed($"the command for worker '{id}' is empty.");
        }

        var workerRoot = Path.Combine(workersDirectory, id);

        var firstTokenIsStagedBinary =
            tokens[0].Contains(RootPlaceholder, StringComparison.Ordinal);
        var executable = Expand(tokens[0], workerRoot);

        if (firstTokenIsStagedBinary)
        {
            executable = AppendExecutableSuffix(executable);
        }

        var leadingArguments = new List<string>(tokens.Length - 1);

        for (var index = 1; index < tokens.Length; index++)
        {
            leadingArguments.Add(Expand(tokens[index], workerRoot));
        }

        return (executable, leadingArguments);
    }

    private static string Require(string? value, string reason)
        => string.IsNullOrWhiteSpace(value) ? throw Malformed(reason) : value;
}
