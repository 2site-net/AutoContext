namespace AutoContext.Mcp.Server.Tools.Invocation;

using System.Text.Json;

using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Workers.Protocol;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Builds the per-tool dispatch closures consumed by the MCP SDK
/// registration step. Each delegate captures one
/// <see cref="McpWorker"/> + <see cref="McpToolDefinition"/> pair and
/// invokes <see cref="ToolInvoker"/> on call, returning the serialized
/// result envelope as a JSON string.
/// </summary>
public static partial class ToolDelegateFactory
{
    /// <summary>
    /// Builds the per-tool delegate map keyed by <c>tool.name</c>. Throws
    /// when two tools across the registry share the same name — the
    /// registry validator should have caught this at startup.
    /// </summary>
    public static IReadOnlyDictionary<string, ToolHandler> Build(McpWorkersCatalog registry, ToolInvoker invoker)
        => Build(registry, invoker, NullLogger.Instance);

    /// <summary>
    /// Builds the per-tool delegate map keyed by <c>tool.name</c>. Throws
    /// when two tools across the registry share the same name — the
    /// registry validator should have caught this at startup.
    /// </summary>
    public static IReadOnlyDictionary<string, ToolHandler> Build(McpWorkersCatalog registry, ToolInvoker invoker, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(logger);

        var delegates = new Dictionary<string, ToolHandler>(StringComparer.Ordinal);

        foreach (var worker in registry.Workers)
        {
            foreach (var tool in worker.Tools)
            {
                var name = tool.Name;

                if (delegates.ContainsKey(name))
                {
                    LogDuplicateToolName(logger, name);
                    throw new InvalidOperationException(
                        $"Registry contains duplicate MCP Tool name '{name}'.");
                }

                delegates[name] = CreateHandler(invoker, worker, tool);
            }
        }

        return delegates;
    }

    /// <summary>
    /// Builds a single per-tool handler. Constructing the closure
    /// inside a dedicated static method makes the
    /// <see cref="McpWorker"/> + <see cref="McpToolDefinition"/> capture
    /// explicit through the parameter list, so there is no enclosing
    /// loop variable to misbind. This is defensive against future
    /// refactors of the <c>foreach</c> in <see cref="Build"/> — e.g. a
    /// switch to an index-based <c>for</c> over an indexable collection,
    /// or inlining the body — that could otherwise re-introduce the
    /// classic closure-over-loop-variable bug.
    /// </summary>
    private static ToolHandler CreateHandler(
        ToolInvoker invoker,
        McpWorker worker,
        McpToolDefinition tool)
    {
        return async (data, correlationId, ct) =>
        {
            var envelope = await invoker
                .InvokeAsync(worker, tool, data, correlationId, ct)
                .ConfigureAwait(false);

            return JsonSerializer.Serialize(envelope, WorkerJsonOptions.Instance);
        };
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Critical,
        Message = "Registry contains duplicate MCP Tool name '{ToolName}' while building delegates.")]
    private static partial void LogDuplicateToolName(ILogger logger, string toolName);
}
