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
/// The <c>search_instructions_by_metadata</c> MCP tool. Searches the instructions
/// files by a metadata field predicate, shimming over the engine's
/// <c>Instructions.SearchByMetadata</c> capability handler.
/// </summary>
internal sealed class InstructionsSearchMetadataTool : IMcpTool
{
    /// <summary>The tool name.</summary>
    public const string ToolName = "search_instructions_by_metadata";

    private readonly IRpcMethodHandler _handler;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="InstructionsSearchMetadataTool"/> class over the engine's
    /// <c>Instructions.*</c> handler.
    /// </summary>
    public InstructionsSearchMetadataTool(IRpcMethodHandler handler)
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
        var parameters = new JsonInstructionsSearchByMetadataParams
        {
            Predicate = JsonArguments.TryGetObject(arguments, "predicate"),
            IncludeSections = JsonArguments.TryGetBool(arguments, "includeSections"),
        };

        return HandlerMarshaller.InvokeAsync(
            _handler,
            InstructionsMethods.SearchByMetadata,
            JsonSerializer.SerializeToElement(parameters, ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataParams),
            cancellationToken);
    }

    private static Tool BuildDescriptor()
    {
        return new Tool
        {
            Name = ToolName,
            Description = "Search the AutoContext instructions files by a metadata field predicate "
                + "(regex over name/description/category/version, applyTo glob, hasChangelog, "
                + "and per-section sections.* clauses).",
            InputSchema = InputSchemaBuilder.Build(
            [
                new McpToolsRegistryParameterEntry
                {
                    Name = "predicate",
                    Type = "object",
                    Description = "Field-to-pattern map ANDed across keys. String fields match "
                        + "case-insensitive regex; applyTo matches a workspace glob; hasChangelog "
                        + "matches a boolean; sections.level matches a number. An empty object "
                        + "matches every file.",
                    Required = false,
                },
                new McpToolsRegistryParameterEntry
                {
                    Name = "includeSections",
                    Type = "boolean",
                    Description = "Attach each matched file's section index to its row.",
                    Required = false,
                },
            ]),
        };
    }
}
