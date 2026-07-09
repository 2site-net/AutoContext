namespace AutoContext.Workers.Core.Logging;

/// <summary>
/// Single source of the worker-side log-category contract. The engine
/// routes every ingested <c>Engine.WriteLog</c> record to the correct
/// on-disk log by its <c>category</c> prefix — a record whose category
/// begins <c>worker.&lt;workerId&gt;.</c> lands in that worker's
/// <c>worker-&lt;workerId&gt;.log</c>. A worker's own
/// <see cref="Microsoft.Extensions.Logging.ILogger"/> categories are
/// bare type names, so the worker stamps that prefix onto every record
/// before it leaves the process.
/// </summary>
internal static class WorkerLogCategory
{
    /// <summary>
    /// Composes the wire category for a record a worker emits: the
    /// <c>worker.&lt;workerId&gt;.</c> routing prefix followed by the
    /// originating <see cref="Microsoft.Extensions.Logging.ILogger"/>
    /// category.
    /// </summary>
    /// <param name="workerId">The worker's stable short identifier
    /// (for example <c>dotnet</c>).</param>
    /// <param name="category">The originating logger category (a bare
    /// type name, possibly empty).</param>
    /// <returns>The prefixed category the engine routes on.</returns>
    public static string Compose(string workerId, string category)
        => $"worker.{workerId}.{category}";
}
