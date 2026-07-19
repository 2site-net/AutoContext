namespace AutoContext.Engine.Core.McpServer;

using System;
using System.Collections.Generic;
using System.Text.Json;

using AutoContext.Engine.Core.McpServer.Tools;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using JsonRpcResponse = Protocol.JsonRpc.JsonRpcResponse;

/// <summary>
/// Bridges the MCP SDK's protocol-level <c>tools/list</c> and <c>tools/call</c>
/// handlers to the engine's capabilities. It is a generic router: every tool
/// comes from an <see cref="IMcpToolSource"/>, and each <c>tools/call</c> is
/// routed by name to the matching <see cref="IMcpTool"/> leaf, whose response
/// is marshalled onto a <see cref="CallToolResult"/>. The adapter knows no
/// concrete tools — adding a tool or family never touches this class.
/// </summary>
internal sealed partial class McpSdkAdapter
{
    private readonly IConfigSnapshotAccessor _configAccessor;
    private readonly IConfigReloader _configReloader;
    private readonly ILogger<McpSdkAdapter> _logger;
    private readonly IReadOnlyList<IMcpToolSource> _sources;
    private readonly Lazy<IReadOnlyDictionary<string, IMcpTool>> _toolsByName;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpSdkAdapter"/> class.
    /// </summary>
    public McpSdkAdapter(
        IEnumerable<IMcpToolSource> toolSources,
        IConfigSnapshotAccessor configAccessor,
        IConfigReloader configReloader,
        ILogger<McpSdkAdapter> logger)
    {
        ArgumentNullException.ThrowIfNull(toolSources);
        ArgumentNullException.ThrowIfNull(configAccessor);
        ArgumentNullException.ThrowIfNull(configReloader);
        ArgumentNullException.ThrowIfNull(logger);

        _sources = [.. toolSources];
        _configAccessor = configAccessor;
        _configReloader = configReloader;
        _logger = logger;

        // Built on first request (not construction): registry-sourced tools
        // read the snapshot loaded during host startup, which is not populated
        // when the adapter is resolved. The tool set is immutable thereafter.
        _toolsByName = new Lazy<IReadOnlyDictionary<string, IMcpTool>>(BuildToolMap);
    }

    /// <summary>Handler for <c>tools/call</c>.</summary>
    public ValueTask<CallToolResult> HandleCallToolAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return CallToolAsync(request.Params?.Name, request.Params?.Arguments, cancellationToken);
    }

    /// <summary>Handler for <c>tools/list</c>.</summary>
    public async ValueTask<ListToolsResult> HandleListToolsAsync(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ListToolsResult
        {
            Tools = await BuildVisibleToolsAsync(cancellationToken).ConfigureAwait(false),
        };
    }

    /// <summary>
    /// Re-reads the config, then projects every tool's descriptor, hiding the
    /// ones disabled in <c>.autocontext.json</c>. The transport-independent
    /// core of <see cref="HandleListToolsAsync"/>.
    /// </summary>
    internal async ValueTask<List<Tool>> BuildVisibleToolsAsync(CancellationToken cancellationToken)
    {
        await _configReloader.ReloadAsync(cancellationToken).ConfigureAwait(false);

        var disabledTools = _configAccessor.Current.McpTools;
        var tools = _toolsByName.Value;

        var visible = new List<Tool>(tools.Count);

        foreach (var tool in tools.Values)
        {
            if (IsDisabled(tool.Descriptor.Name, disabledTools))
            {
                continue;
            }

            visible.Add(tool.Descriptor);
        }

        return visible;
    }

    /// <summary>
    /// Re-reads the config, routes the named tool to its leaf, and marshals
    /// the response onto a <see cref="CallToolResult"/>. The
    /// transport-independent core of <see cref="HandleCallToolAsync"/>.
    /// </summary>
    internal async ValueTask<CallToolResult> CallToolAsync(
        string? name,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name))
        {
            LogMissingToolName(_logger);
            throw new McpException("tools/call request is missing the 'name' parameter.");
        }

        await _configReloader.ReloadAsync(cancellationToken).ConfigureAwait(false);

        if (!_toolsByName.Value.TryGetValue(name, out var tool))
        {
            LogUnknownTool(_logger, name);
            throw new McpException($"Unknown MCP tool '{name}'.");
        }

        var response = await tool.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);

        return ToCallToolResult(response);
    }

    private static bool IsDisabled(string? name, ConfigMcpTool[] disabledTools)
        => name is not null
            && Array.Find(
                disabledTools,
                t => string.Equals(t.Name, name, StringComparison.Ordinal))?.Disabled == true;

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "tools/call request is missing the required 'name' parameter.")]
    private static partial void LogMissingToolName(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "tools/call requested unknown MCP tool '{ToolName}'.")]
    private static partial void LogUnknownTool(ILogger logger, string toolName);

    private static CallToolResult ToCallToolResult(JsonRpcResponse response)
    {
        if (response.Error is { } error)
        {
            throw new McpException(error.Message);
        }

        if (response.Result is not { } result)
        {
            throw new McpException("The engine handler returned neither a result nor an error.");
        }

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.GetRawText() }],
            StructuredContent = result,
        };
    }

    private Dictionary<string, IMcpTool> BuildToolMap()
    {
        var map = new Dictionary<string, IMcpTool>(StringComparer.Ordinal);

        foreach (var source in _sources)
        {
            foreach (var tool in source.GetTools())
            {
                if (tool.Descriptor.Name is { } name)
                {
                    map[name] = tool;
                }
            }
        }

        return map;
    }
}
