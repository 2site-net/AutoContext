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
/// The <c>list_instructions</c> MCP tool. Lists the available AutoContext
/// instructions files, shimming over the engine's <c>Instructions.List</c>
/// capability handler.
/// </summary>
internal sealed class InstructionsListTool : IMcpTool
{
    /// <summary>The tool name.</summary>
    public const string ToolName = "list_instructions";

    private readonly IRpcMethodHandler _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstructionsListTool"/>
    /// class over the engine's <c>Instructions.*</c> handler.
    /// </summary>
    public InstructionsListTool(IRpcMethodHandler handler)
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
        var parameters = new JsonInstructionsListParams
        {
            IncludeSections = JsonArguments.TryGetBool(arguments, "includeSections"),
            ApplyToHint = JsonArguments.TryGetString(arguments, "applyTo"),
        };

        return HandlerMarshaller.InvokeAsync(
            _handler,
            InstructionsMethods.List,
            JsonSerializer.SerializeToElement(parameters, ProtocolJsonContext.Default.JsonInstructionsListParams),
            cancellationToken);
    }

    private static Tool BuildDescriptor()
    {
        return new Tool
        {
            Name = ToolName,
            Description =
                "List the available AutoContext instructions files with their metadata "
                + "(applyTo, category, disabled state, and optional section index).",
            InputSchema = InputSchemaBuilder.Build(
            [
                new McpToolsRegistryParameterEntry
                {
                    Name = "includeSections",
                    Type = "boolean",
                    Description = "Include each file's section index in the result.",
                    Required = false,
                },
                new McpToolsRegistryParameterEntry
                {
                    Name = "applyTo",
                    Type = "string",
                    Description =
                        "Optional applyTo glob hint; only files whose applyTo extensions "
                        + "intersect the hint are returned.",
                    Required = false,
                },
            ]),
        };
    }
}
