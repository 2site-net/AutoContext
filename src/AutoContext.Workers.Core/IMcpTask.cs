namespace AutoContext.Workers.Core;

using System.Text.Json;

/// <summary>
/// A single tool-execution unit handled by a .NET worker process.
/// </summary>
/// <remarks>
/// This is an optional convenience for .NET workers, not the worker
/// contract. A worker is defined by its <c>.autocontext-worker.json</c>
/// descriptor and by the dispatch protocol it speaks over its pipe;
/// implementing this interface is simply how <see cref="WorkerTaskDispatcherService"/>
/// discovers in-process handlers. Workers on other runtimes satisfy the
/// same dispatch protocol with their own types.
/// <para>
/// The contract is JSON-native end to end: the request payload is the JSON
/// supplied to the tool, and the return value is whatever JSON the
/// task wants to surface.
/// </para>
/// </remarks>
public interface IMcpTask
{
    /// <summary>
    /// Snake_case identifier matching the tool's <c>name</c> in the engine's
    /// tool registry.
    /// </summary>
    string TaskName { get; }

    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <param name="data">
    /// The JSON payload from the tool invocation. EditorConfig values the
    /// tool declares in the registry are merged in
    /// as flat properties prefixed with <c>editorconfig.</c> (e.g.
    /// <c>data["editorconfig.indent_style"]</c>); missing keys are simply
    /// absent.
    /// </param>
    /// <param name="cancellationToken">Cancellation token threaded from the engine through the pipe protocol.</param>
    Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken);
}
