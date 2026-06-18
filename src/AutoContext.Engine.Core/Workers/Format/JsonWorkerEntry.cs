namespace AutoContext.Engine.Core.Workers.Format;

/// <summary>
/// Disk read-model for one row of the build-generated <c>workers.json</c>
/// side-car: the worker's stable id, its informational kind, an optional
/// label, and the launch command with its literal <c>${root}</c>
/// placeholder intact. The engine's
/// <see cref="WorkerProcessInfoResolver"/> expands the placeholder and
/// derives the launch shape from <see cref="Command"/> at runtime.
/// </summary>
/// <param name="Id">The worker's stable short id (the FK target the
/// MCP-tools registry's <c>workerId</c> points at).</param>
/// <param name="Type">Informational worker kind (for example
/// <c>executable</c> or <c>script</c>); the resolver derives how to launch
/// from <see cref="Command"/>, not from this.</param>
/// <param name="Label">Optional human-readable label; omitted from the
/// side-car when absent.</param>
/// <param name="Command">The launch command, carrying the literal
/// <c>${root}</c> placeholder and any launcher token (for example
/// <c>node ${root}/index.js</c>).</param>
internal sealed record JsonWorkerEntry(
    string? Id = null,
    string? Type = null,
    string? Label = null,
    string? Command = null);
