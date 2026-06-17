namespace AutoContext.Engine.Core.Rpc.Policies;

using System;
using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// The <c>McpTools.*</c> handlers for <see cref="DispatchPolicy"/>. Each
/// handler reads the immutable MCP-tools registry snapshot through the
/// <see cref="Features.McpTools.IMcpToolsRegistryAccessor"/> seam and
/// layers the engine-resolved per-tool disabled state from the config
/// snapshot. Unexpected failures reply
/// <see cref="JsonRpcErrorCodes.InternalError"/>; the
/// connection keeps serving.
/// </summary>
internal sealed partial class DispatchPolicy
{
    private static readonly JsonElement EmptyArguments = JsonSerializer.SerializeToElement(new { });

    private async Task<UnaryHandlerResult> HandleMcpToolsInvokeAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        JsonMcpToolsInvokeParams? parameters;

        try
        {
            parameters = request.Params is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } element
                ? element.Deserialize(ProtocolJsonContext.Default.JsonMcpToolsInvokeParams)
                : null;
        }
        catch (JsonException ex)
        {
            LogParamsParseFailed(_logger, McpToolsMethods.Invoke, ex);
            return InvalidParams(McpToolsMethods.Invoke);
        }

        if (string.IsNullOrWhiteSpace(parameters?.Name))
        {
            return InvalidParams(McpToolsMethods.Invoke);
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
            return InternalError("Failed to invoke the MCP tool.");
        }
    }

    private UnaryHandlerResult HandleMcpToolsList()
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
            return Success(result, ProtocolJsonContext.Default.JsonMcpToolsListResult);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMcpToolsFailed(_logger, McpToolsMethods.List, ex);
            return InternalError("Failed to list the MCP tools catalog.");
        }
    }

    private static bool IsTypeMatch(JsonValueKind valueKind, string expectedType) =>
        expectedType switch
        {
            "array" => valueKind == JsonValueKind.Array,
            "boolean" => valueKind is JsonValueKind.True or JsonValueKind.False,
            "number" => valueKind == JsonValueKind.Number,
            "object" => valueKind == JsonValueKind.Object,
            "string" => valueKind == JsonValueKind.String,
            _ => false,
        };

    private static UnaryHandlerResult InvokeResult(JsonMcpToolsInvokeResult result) =>
        Success(result, ProtocolJsonContext.Default.JsonMcpToolsInvokeResult);

    private static string JsonTypeName(JsonValueKind valueKind) =>
        valueKind switch
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

    private bool IsToolDisabled(string name)
    {
        var configTools = _configAccessor.Current.McpTools;

        return Array.Find(
            configTools,
            t => string.Equals(t.Name, name, StringComparison.Ordinal))?.Disabled == true;
    }

    private static JsonElement NormalizeArguments(JsonElement? arguments) =>
        arguments is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } value
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

    [LoggerMessage(EventId = 71, Level = LogLevel.Warning,
        Message = "McpTools handler '{Method}' failed.")]
    private static partial void LogMcpToolsFailed(ILogger logger, string method, Exception exception);
}
