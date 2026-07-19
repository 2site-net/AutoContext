namespace AutoContext.Engine.Core.McpServer.Tools.Intrinsics;

using System.Collections.Generic;

using AutoContext.Engine.Core.Rpc.Handlers;

/// <summary>
/// The instruction tool family: the fixed set of <c>instructions_*</c> leaves
/// that shim over the engine's <c>Instructions.*</c> capability handler. Add a
/// new instruction tool by adding a leaf here — the adapter is unaffected.
/// </summary>
internal sealed class InstructionsToolSource : IMcpToolSource
{
    private readonly IReadOnlyList<IMcpTool> _tools;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstructionsToolSource"/>
    /// class over the engine's <c>Instructions.*</c> handler.
    /// </summary>
    /// <param name="instructionsHandler">The capability handler the daemon's
    /// pipe RPC also dispatches into.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="instructionsHandler"/> is <see langword="null"/>.
    /// </exception>
    public InstructionsToolSource(InstructionsRpcHandler instructionsHandler)
    {
        ArgumentNullException.ThrowIfNull(instructionsHandler);

        _tools =
        [
            new InstructionsListTool(instructionsHandler),
            new InstructionsSearchContentTool(instructionsHandler),
            new InstructionsSearchMetadataTool(instructionsHandler),
            new InstructionsGetTool(instructionsHandler),
        ];
    }

    /// <inheritdoc />
    public IReadOnlyList<IMcpTool> GetTools() => _tools;
}
