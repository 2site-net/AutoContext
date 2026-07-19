namespace AutoContext.Engine.Core.McpServer;

using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Context;

using Microsoft.Extensions.Hosting;

/// <summary>
/// One-shot startup loader for the stdio MCP-server role. Populates the
/// snapshots the instruction handlers project against — the workspace
/// <c>.autocontext.json</c> config and the workspace detection result —
/// before the tool surface starts serving, and <b>without</b> arming any
/// <see cref="FileSystemWatcher"/>: the config is re-read
/// per request and the workspace is fixed for the process lifetime.
/// Registered ahead of <see cref="Features.Instructions.InstructionsOverridesService"/>
/// so the configured override roots are loaded before its scan runs.
/// </summary>
internal sealed class McpStdioStartupLoader : IHostedService
{
    private readonly ConfigFileManager _configFileManager;
    private readonly WorkspaceContextDetector _workspaceContextDetector;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpStdioStartupLoader"/>
    /// class.
    /// </summary>
    public McpStdioStartupLoader(
        ConfigFileManager configFileManager,
        WorkspaceContextDetector workspaceContextDetector)
    {
        ArgumentNullException.ThrowIfNull(configFileManager);
        ArgumentNullException.ThrowIfNull(workspaceContextDetector);

        _configFileManager = configFileManager;
        _workspaceContextDetector = workspaceContextDetector;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _configFileManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        await _workspaceContextDetector.DetectAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
