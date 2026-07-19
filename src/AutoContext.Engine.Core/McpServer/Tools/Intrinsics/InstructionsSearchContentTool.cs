namespace AutoContext.Engine.Core.McpServer.Tools.Intrinsics;

using System.Collections.Generic;
using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.Rpc.Handlers;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Serialization;

using ModelContextProtocol.Protocol;

using JsonRpcResponse = Protocol.JsonRpc.JsonRpcResponse;

/// <summary>
/// The <c>search_instructions_by_content</c> MCP tool. Full-text searches the
/// instructions file bodies, shimming over the engine's
/// <c>Instructions.SearchContent</c> capability handler.
/// </summary>
internal sealed class InstructionsSearchContentTool : IMcpTool
{
    /// <summary>The tool name.</summary>
    public const string ToolName = "search_instructions_by_content";

    private readonly IRpcMethodHandler _handler;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="InstructionsSearchContentTool"/> class over the engine's
    /// <c>Instructions.*</c> handler.
    /// </summary>
    public InstructionsSearchContentTool(IRpcMethodHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler = handler;
        Descriptor = BuildDescriptor();
    }

    /// <inheritdoc />
    public Tool Descriptor { get; }

    /// <inheritdoc />
    public ValueTask<JsonRpcResponse> InvokeAsync(
        IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        var parameters = new JsonInstructionsSearchContentParams
        {
            Query = JsonArguments.TryGetString(arguments, "query"),
            Limit = JsonArguments.TryGetInt(arguments, "limit"),
            IncludeDisabled = JsonArguments.TryGetBool(arguments, "includeDisabled"),
        };

        return HandlerMarshaller.InvokeAsync(
            _handler,
            InstructionsMethods.SearchContent,
            JsonSerializer.SerializeToElement(parameters, ProtocolJsonContext.Default.JsonInstructionsSearchContentParams),
            cancellationToken);
    }

    private static Tool BuildDescriptor()
    {
        return new Tool
        {
            Name = ToolName,
            Description = "Full-text search across the bodies of the AutoContext instructions files.",
            InputSchema = InputSchemaBuilder.Build(
            [
                new McpToolsRegistryParameterEntry
                {
                    Name = "query",
                    Type = "string",
                    Description = "The full-text query.",
                    Required = true,
                },
                new McpToolsRegistryParameterEntry
                {
                    Name = "limit",
                    Type = "number",
                    Description = "Maximum number of hits to return.",
                    Required = false,
                },
                new McpToolsRegistryParameterEntry
                {
                    Name = "includeDisabled",
                    Type = "boolean",
                    Description = "Include files disabled in .autocontext.json.",
                    Required = false,
                },
            ]),
        };
    }
}
