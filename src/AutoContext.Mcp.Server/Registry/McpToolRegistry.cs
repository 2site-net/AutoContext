namespace AutoContext.Mcp.Server.Registry;

using System.Text.Json;

using AutoContext.Mcp.Server.Tools.Invocation;

using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
/// Adapter between the registry-driven dispatch layer
/// (<see cref="ToolDelegateFactory"/> + <see cref="ToolHandler"/>) and
/// the MCP SDK's <see cref="WithListToolsHandler"/> /
/// <see cref="WithCallToolHandler"/> request handlers.
/// </summary>
/// <remarks>
/// The architecture's invariant — fully registry-driven tool registration,
/// no <c>[McpServerTool]</c> bridge classes — is preserved. We can't use
/// <c>McpServerTool.Create(Delegate, ...)</c> because that derives the
/// advertised <c>inputSchema</c> from the delegate's method signature,
/// which is incompatible with our data-driven schema. The protocol-level
/// list/call handlers expose the <see cref="Tool.InputSchema"/> setter
/// directly, so we drive both endpoints from the registry.
/// </remarks>
public sealed class McpToolRegistry
{
    private readonly IReadOnlyList<Tool> _tools;
    private readonly IReadOnlyDictionary<string, ToolHandler> _handlers;

    public McpToolRegistry(McpWorkersCatalog registry, ToolInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);

        _handlers = ToolDelegateFactory.Build(registry, invoker);
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

        var name = request.Params?.Name
            ?? throw new McpException("tools/call request is missing the 'name' parameter.");

        if (!_handlers.TryGetValue(name, out var handler))
        {
            throw new McpException($"Unknown MCP Tool '{name}'.");
        }

        var data = ToDataElement(request.Params?.Arguments);
        var envelopeJson = await handler(data, cancellationToken).ConfigureAwait(false);

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

    private static JsonElement ToDataElement(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return EmptyObject;
        }

        return JsonSerializer.SerializeToElement(arguments);
    }
}
