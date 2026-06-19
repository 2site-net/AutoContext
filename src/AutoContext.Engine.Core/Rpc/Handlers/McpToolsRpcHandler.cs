namespace AutoContext.Engine.Core.Rpc.Handlers;

using System;
using System.Collections.Generic;
using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools;
using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// The <c>McpTools.*</c> handler. Reads the immutable MCP-tools registry
/// snapshot through the <see cref="IMcpToolsRegistryAccessor"/> seam and
/// layers the engine-resolved per-tool disabled state from the config
/// snapshot. Schema-invalid invocations reply with a structured result;
/// unexpected failures reply <see cref="JsonRpcErrorCodes.InternalError"/>;
/// in every case the connection keeps serving.
/// </summary>
internal sealed partial class McpToolsRpcHandler : IRpcMethodHandler
{
    private static readonly JsonElement EmptyArguments = JsonSerializer.SerializeToElement(new { });

    private readonly IConfigSnapshotAccessor _configAccessor;
    private readonly ILogger<McpToolsRpcHandler> _logger;
    private readonly IMcpToolsInvoker _mcpToolsInvoker;
    private readonly IMcpToolsRegistryAccessor _mcpToolsRegistryAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolsRpcHandler"/>
    /// class.
    /// </summary>
    public McpToolsRpcHandler(
        IMcpToolsRegistryAccessor mcpToolsRegistryAccessor,
        IMcpToolsInvoker mcpToolsInvoker,
        IConfigSnapshotAccessor configAccessor,
        ILogger<McpToolsRpcHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(mcpToolsRegistryAccessor);
        ArgumentNullException.ThrowIfNull(mcpToolsInvoker);
        ArgumentNullException.ThrowIfNull(configAccessor);
        ArgumentNullException.ThrowIfNull(logger);

        _mcpToolsRegistryAccessor = mcpToolsRegistryAccessor;
        _mcpToolsInvoker = mcpToolsInvoker;
        _configAccessor = configAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Methods { get; } =
        [McpToolsMethods.List, McpToolsMethods.Invoke];

    /// <inheritdoc />
    public async ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Method switch
        {
            McpToolsMethods.Invoke => await HandleInvokeAsync(request, cancellationToken).ConfigureAwait(false),
            _ => HandleList(),
        };
    }

    private static UnaryHandlerResult InvokeResult(JsonMcpToolsInvokeResult result)
        => RpcMethodResults.Success(result, ProtocolJsonContext.Default.JsonMcpToolsInvokeResult);

    private static bool IsTypeMatch(JsonValueKind valueKind, string expectedType)
        => expectedType switch
        {
            "array" => valueKind == JsonValueKind.Array,
            "boolean" => valueKind is JsonValueKind.True or JsonValueKind.False,
            "number" => valueKind == JsonValueKind.Number,
            "object" => valueKind == JsonValueKind.Object,
            "string" => valueKind == JsonValueKind.String,
            _ => false,
        };

    private static string JsonTypeName(JsonValueKind valueKind)
        => valueKind switch
        {
            JsonValueKind.Array => "array",
            JsonValueKind.False or JsonValueKind.True => "boolean",
            JsonValueKind.Null => "null",
            JsonValueKind.Number => "number",
            JsonValueKind.Object => "object",
            JsonValueKind.String => "string",
            JsonValueKind.Undefined => "undefined",
            _ => "undefined",
        };

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "McpTools handler '{Method}' failed.")]
    private static partial void LogMcpToolsFailed(ILogger logger, string method, Exception exception);

    private static JsonElement NormalizeArguments(JsonElement? arguments)
        => arguments is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } value
            ? value
            : EmptyArguments;

    private static List<JsonMcpToolsSchemaError> ValidateArguments(
        McpToolsRegistryEntry tool,
        JsonElement? arguments)
    {
        var errors = new List<JsonMcpToolsSchemaError>();
        var knownNames = new HashSet<string>(StringComparer.Ordinal);
        var provided = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var parameter in tool.Parameters)
        {
            knownNames.Add(parameter.Name);
        }

        if (arguments is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } value)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new JsonMcpToolsSchemaError
                {
                    Path = string.Empty,
                    Message = "arguments must be a JSON object.",
                });

                return errors;
            }

            foreach (var property in value.EnumerateObject())
            {
                provided[property.Name] = property.Value;
            }
        }

        foreach (var parameter in tool.Parameters)
        {
            if (!provided.TryGetValue(parameter.Name, out var argumentValue))
            {
                if (parameter.Required)
                {
                    errors.Add(new JsonMcpToolsSchemaError
                    {
                        Path = parameter.Name,
                        Message = "Required parameter is missing.",
                    });
                }

                continue;
            }

            if (!IsTypeMatch(argumentValue.ValueKind, parameter.Type))
            {
                errors.Add(new JsonMcpToolsSchemaError
                {
                    Path = parameter.Name,
                    Message =
                        $"Expected type '{parameter.Type}' but got '{JsonTypeName(argumentValue.ValueKind)}'.",
                });
            }
        }

        foreach (var key in provided.Keys)
        {
            if (knownNames.Contains(key))
            {
                continue;
            }

            errors.Add(new JsonMcpToolsSchemaError
            {
                Path = key,
                Message = "Unknown parameter.",
            });
        }

        return errors;
    }

    private async Task<UnaryHandlerResult> HandleInvokeAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        if (RpcMethodResults.TryDeserialize(
                request,
                McpToolsMethods.Invoke,
                ProtocolJsonContext.Default.JsonMcpToolsInvokeParams,
                _logger,
                out var parameters) is { } failure)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(parameters?.Name))
        {
            return RpcMethodResults.InvalidParams(McpToolsMethods.Invoke);
        }

        var name = parameters.Name;
        var registry = _mcpToolsRegistryAccessor.Current;
        var tool = registry.FindByName(name);

        if (tool is null)
        {
            return InvokeResult(new JsonMcpToolsInvokeNotFoundResult { Name = name });
        }

        if (IsToolDisabled(tool.Name))
        {
            return InvokeResult(new JsonMcpToolsInvokeDisabledResult { Name = name });
        }

        var schemaErrors = ValidateArguments(tool, parameters.Arguments);

        if (schemaErrors.Count > 0)
        {
            return InvokeResult(new JsonMcpToolsInvokeSchemaErrorResult
            {
                Name = name,
                Errors = schemaErrors,
            });
        }

        try
        {
            var result = await _mcpToolsInvoker
                .InvokeAsync(tool, NormalizeArguments(parameters.Arguments), cancellationToken)
                .ConfigureAwait(false);

            return InvokeResult(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMcpToolsFailed(_logger, McpToolsMethods.Invoke, ex);
            return RpcMethodResults.InternalError("Failed to invoke the MCP tool.");
        }
    }

    private UnaryHandlerResult HandleList()
    {
        try
        {
            var registry = _mcpToolsRegistryAccessor.Current;
            var configTools = _configAccessor.Current.McpTools;

            var rows = new List<JsonMcpToolsListRow>(registry.Tools.Count);

            foreach (var tool in registry.Tools)
            {
                var disabled = Array.Find(
                    configTools,
                    t => string.Equals(t.Name, tool.Name, StringComparison.Ordinal))?.Disabled == true;

                rows.Add(new JsonMcpToolsListRow
                {
                    Key = tool.Name,
                    Name = tool.Name,
                    Description = tool.ModelDescription,
                    WorkerId = tool.WorkerId,
                    Category = tool.Category,
                    Disabled = disabled,
                });
            }

            var result = new JsonMcpToolsListResult { Tools = rows };
            return RpcMethodResults.Success(result, ProtocolJsonContext.Default.JsonMcpToolsListResult);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMcpToolsFailed(_logger, McpToolsMethods.List, ex);
            return RpcMethodResults.InternalError("Failed to list the MCP tools catalog.");
        }
    }

    private bool IsToolDisabled(string name)
    {
        var configTools = _configAccessor.Current.McpTools;

        return Array.Find(
            configTools,
            t => string.Equals(t.Name, name, StringComparison.Ordinal))?.Disabled == true;
    }
}
