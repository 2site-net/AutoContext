namespace AutoContext.Engine.Core.Rpc.Policies;

using System;

using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// The <c>McpTools.*</c> handlers for <see cref="DispatchPolicy"/>. Each
/// handler reads the immutable MCP-tools registry snapshot through the
/// <see cref="Features.McpTools.IMcpToolsRegistryAccessor"/> seam and
/// layers the engine-resolved per-tool disabled state from the config
/// snapshot. Unexpected failures reply
/// <see cref="Protocol.JsonRpc.JsonRpcErrorCodes.InternalError"/>; the
/// connection keeps serving.
/// </summary>
internal sealed partial class DispatchPolicy
{
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

    [LoggerMessage(EventId = 71, Level = LogLevel.Warning,
        Message = "McpTools handler '{Method}' failed.")]
    private static partial void LogMcpToolsFailed(ILogger logger, string method, Exception exception);
}
