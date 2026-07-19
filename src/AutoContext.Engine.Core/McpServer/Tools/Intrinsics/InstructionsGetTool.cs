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
/// The <c>get_instructions</c> MCP tool. Returns the projected body of a
/// single instructions file, shimming over the engine's <c>Instructions.Get</c>
/// capability handler.
/// </summary>
internal sealed class InstructionsGetTool : IMcpTool
{
    /// <summary>The tool name.</summary>
    public const string ToolName = "get_instructions";

    private readonly IRpcMethodHandler _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstructionsGetTool"/>
    /// class over the engine's <c>Instructions.*</c> handler.
    /// </summary>
    public InstructionsGetTool(IRpcMethodHandler handler)
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
        var parameters = new JsonInstructionsGetParams
        {
            Name = JsonArguments.TryGetString(arguments, "name"),
            Sections = JsonArguments.TryGetStringArray(arguments, "sections"),
        };

        return HandlerMarshaller.InvokeAsync(
            _handler,
            InstructionsMethods.Get,
            JsonSerializer.SerializeToElement(parameters, ProtocolJsonContext.Default.JsonInstructionsGetParams),
            cancellationToken);
    }

    private static Tool BuildDescriptor()
    {
        return new Tool
        {
            Name = ToolName,
            Description =
                "Get the projected body of a single AutoContext instructions file, "
                + "optionally limited to specific sections.",
            InputSchema = InputSchemaBuilder.Build(
            [
                new McpToolsRegistryParameterEntry
                {
                    Name = "name",
                    Type = "string",
                    Description = "The instructions file name or key.",
                    Required = true,
                },
                new McpToolsRegistryParameterEntry
                {
                    Name = "sections",
                    Type = "array",
                    Description = "Optional section anchors to return; omit for the whole file.",
                    Required = false,
                },
            ]),
        };
    }
}
