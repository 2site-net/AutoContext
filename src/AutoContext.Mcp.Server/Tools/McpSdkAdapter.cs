namespace AutoContext.Mcp.Server.Tools;

using System.Text.Json;

using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tools.Invocation;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
/// Bridges the MCP SDK's protocol-level <c>tools/list</c> and
/// <c>tools/call</c> handlers to our registry-driven tool layer. Serves
/// <c>tools/list</c> from a pre-built list of <see cref="Tool"/> DTOs
/// and <c>tools/call</c> by looking the tool up in a pre-built
/// <see cref="ToolHandler"/> map (produced by
/// <see cref="ToolDelegateFactory"/>, backed by <see cref="ToolInvoker"/>).
/// </summary>
/// <remarks>
/// The architecture's invariant — fully registry-driven tool
/// registration, no <c>[McpServerTool]</c> bridge classes — is preserved.
/// We can't use <c>McpServerTool.Create(Delegate, ...)</c> because that
/// derives the advertised <c>inputSchema</c> from the delegate's method
/// signature, which is incompatible with our data-driven schema. The
/// protocol-level list/call handlers expose the
/// <see cref="Tool.InputSchema"/> setter directly, so we drive both
/// endpoints from the registry.
/// </remarks>
public sealed partial class McpSdkAdapter
{
    private readonly IReadOnlyList<Tool> _tools;
    private readonly IReadOnlyDictionary<string, ToolHandler> _handlers;
    private readonly ILogger<McpSdkAdapter> _logger;

    public McpSdkAdapter(McpWorkersCatalog registry, ToolInvoker invoker)
        : this(registry, invoker, NullLogger<McpSdkAdapter>.Instance)
    {
    }

    public McpSdkAdapter(
        McpWorkersCatalog registry,
        ToolInvoker invoker,
        ILogger<McpSdkAdapter> logger)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _handlers = ToolDelegateFactory.Build(registry, invoker, logger);
        _tools = BuildProtocolTools(registry);
    }

    /// <summary>Handler for <c>tools/list</c>.</summary>
    public ValueTask<ListToolsResult> HandleListToolsAsync(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(new ListToolsResult
        {
            Tools = [.. _tools],
        });
    }

    /// <summary>Handler for <c>tools/call</c>.</summary>
    public async ValueTask<CallToolResult> HandleCallToolAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Params?.Name;

        if (string.IsNullOrEmpty(name))
        {
            LogMissingToolName(_logger);
            throw new McpException("tools/call request is missing the 'name' parameter.");
        }

        if (!_handlers.TryGetValue(name, out var handler))
        {
            LogUnknownTool(_logger, name);
            throw new McpException($"Unknown MCP Tool '{name}'.");
        }

        var data = ToDataElement(request.Params?.Arguments);
        var correlationId = NewCorrelationId();
        LogToolDispatch(_logger, name, correlationId);
        var envelopeJson = await handler(data, correlationId, cancellationToken).ConfigureAwait(false);

        // Deserialize<JsonElement> returns a self-contained element
        // (parsed via JsonElement.ParseValue) — unlike
        // JsonDocument.Parse(...).RootElement, which leaks the rented
        // ArrayPool buffer until the document is GC-collected.
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = envelopeJson }],
            StructuredContent = JsonSerializer.Deserialize<JsonElement>(envelopeJson),
        };
    }

    private static List<Tool> BuildProtocolTools(McpWorkersCatalog registry)
    {
        var tools = new List<Tool>();

        foreach (var worker in registry.Workers)
        {
            foreach (var tool in worker.Tools)
            {
                tools.Add(new Tool
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    InputSchema = InputSchemaBuilder.Build(tool.Parameters),
                });
            }
        }

        return tools;
    }

    private static readonly JsonElement EmptyObject = JsonSerializer.SerializeToElement(new { });

    /// <summary>
    /// Generates the per-invocation correlation id stamped onto every
    /// <see cref="Workers.Protocol.TaskRequest"/> dispatched by the
    /// resulting handler. Eight hex chars from a fresh <see cref="Guid"/>
    /// — short enough to read in a log line, wide enough (~32 bits) to
    /// keep collisions vanishingly rare across the lifetime of one
    /// extension session.
    /// </summary>
    private static string NewCorrelationId() => Guid.NewGuid().ToString("N")[..8];

    private static JsonElement ToDataElement(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return EmptyObject;
        }

        return JsonSerializer.SerializeToElement(arguments);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "tools/call request is missing required 'name' parameter.")]
    private static partial void LogMissingToolName(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "tools/call requested unknown MCP Tool '{ToolName}'.")]
    private static partial void LogUnknownTool(ILogger logger, string toolName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Dispatching MCP Tool '{ToolName}' with correlation id '{CorrelationId}'.")]
    private static partial void LogToolDispatch(ILogger logger, string toolName, string correlationId);
}
