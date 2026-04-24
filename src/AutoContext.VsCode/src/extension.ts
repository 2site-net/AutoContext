import * as vscode from 'vscode';
import { join } from 'node:path';
import { existsSync } from 'node:fs';
import { WorkspaceContextDetector } from './workspace-context-detector.js';
import { McpToolsCatalog } from './mcp-tools-catalog.js';
import { InstructionsCatalog } from './instructions-catalog.js';
import { McpServersCatalog } from './mcp-servers-catalog.js';
import { mcpTools, instructionsFiles, mcpServers, commandIds, contextKeys, globalStateKeys } from './ui-constants.js';
import { AutoConfigurer } from './auto-configurer.js';
import { InstructionsExporter } from './instructions-exporter.js';
import { AutoContextConfigManager } from './autocontext-config.js';
import { InstructionsContentProvider, instructionScheme } from './instructions-content-provider.js';
import { InstructionsCodeLensProvider } from './instructions-codelens-provider.js';
import { InstructionsDecorationManager } from './instructions-decoration-manager.js';
import { InstructionsConfigWriter } from './instructions-config-writer.js';
import { InstructionsDiagnostics } from './instructions-diagnostics.js';
import { ConfigContextProjector } from './config-context-projector.js';
import { InstructionsTreeProvider } from './instructions-tree-provider.js';
import { MetadataLoader } from './metadata-loader.js';
import { McpToolsTreeProvider } from './mcp-tools-tree-provider.js';
import { TreeViewStateResolver } from './tree-view-state-resolver.js';
import { TreeViewTooltip } from './tree-view-tooltip.js';
import { McpServerProvider } from './mcp-server-provider.js';
import { WorkspaceServerManager } from './workspace-server-manager.js';
import { WorkerManager } from './worker-manager.js';
import { ServersManifest } from './servers-manifest.js';
import { HealthMonitorServer } from './health-monitor.js';

let subscriptions: vscode.Disposable[] | undefined;

export async function activate(context: vscode.ExtensionContext) {
    if (!vscode.workspace.workspaceFolders?.length) {
        return;
    }

    subscriptions = context.subscriptions;

    const version = context.extension.packageJSON.version as string;
    const didChangeEmitter = new vscode.EventEmitter<void>();

    const metadataLoader = new MetadataLoader(context.extensionPath);
    const toolsMetadata = metadataLoader.getMcpToolsInfo();
    const instructionsMetadata = metadataLoader.getInstructionsInfo(instructionsFiles);

    const toolsCatalog = new McpToolsCatalog(mcpTools, toolsMetadata);
    const instructionsCatalog = new InstructionsCatalog(instructionsFiles, instructionsMetadata);
    const serversCatalog = new McpServersCatalog(mcpServers);
    const instructionsExporter = new InstructionsExporter(context.extensionPath);
    const outputChannel = vscode.window.createOutputChannel('AutoContext');
    const workspaceContextDetector = new WorkspaceContextDetector(instructionsCatalog, serversCatalog, outputChannel);
    const configManager = new AutoContextConfigManager(context.extensionPath, version, outputChannel);
    const contentProvider = new InstructionsContentProvider(context.extensionPath, configManager, outputChannel);
    const codeLensProvider = new InstructionsCodeLensProvider(context.extensionPath, configManager, workspaceContextDetector, instructionsCatalog, outputChannel);
    const decorationManager = new InstructionsDecorationManager(context.extensionPath, configManager, outputChannel);
    const instructionsWriter = new InstructionsConfigWriter(context.extensionPath, configManager, instructionsCatalog, outputChannel);
    const configProjector = new ConfigContextProjector(configManager, instructionsCatalog, toolsCatalog, outputChannel);
    const workspaceServer = new WorkspaceServerManager(context.extensionPath, outputChannel, vscode.workspace.workspaceFolders?.[0]?.uri.fsPath);
    const serversManifest = ServersManifest.load(context.extensionPath);
    const workerManager = new WorkerManager(context.extensionPath, outputChannel, vscode.workspace.workspaceFolders?.[0]?.uri.fsPath, serversManifest);
    const healthMonitor = new HealthMonitorServer(outputChannel);
    const mcpServerProvider = new McpServerProvider(context.extensionPath, version, didChangeEmitter.event, toolsCatalog, healthMonitor, workerManager, serversManifest, configManager, outputChannel);
    const stateResolver = new TreeViewStateResolver(workspaceContextDetector);

    // Pre-read the config so tree providers get the real config on first render.
    await configManager.read();

    const instructionsTreeProvider = new InstructionsTreeProvider(workspaceContextDetector, instructionsCatalog, stateResolver, new TreeViewTooltip('instructions'), configManager, outputChannel);
    const mcpToolsTreeProvider = new McpToolsTreeProvider(workspaceContextDetector, toolsCatalog, stateResolver, new TreeViewTooltip('tools'), configManager, outputChannel, healthMonitor, mcpServerProvider);

    const showNotDetected = context.globalState.get<boolean>(commandIds.ShowNotDetected, true);
    instructionsTreeProvider.showNotDetected = showNotDetected;
    mcpToolsTreeProvider.showNotDetected = showNotDetected;
    void vscode.commands.executeCommand('setContext', commandIds.ShowNotDetected, showNotDetected);

    const setShowNotDetected = async (value: boolean) => {
        await context.globalState.update(commandIds.ShowNotDetected, value);
        instructionsTreeProvider.showNotDetected = value;
        mcpToolsTreeProvider.showNotDetected = value;
        void vscode.commands.executeCommand('setContext', commandIds.ShowNotDetected, value);
    };

    const logDiagnostics = () => InstructionsDiagnostics.log(outputChannel, context.extensionPath, configManager, instructionsCatalog);

    context.subscriptions.push(
        didChangeEmitter,
        outputChannel,
        healthMonitor,
        workspaceServer,
        workerManager,
        workspaceContextDetector,
        configManager,
        contentProvider,
        codeLensProvider,
        decorationManager,
        instructionsWriter,
        configProjector,
        instructionsTreeProvider,
        mcpToolsTreeProvider,
        mcpServerProvider,
    );

    healthMonitor.start();
    workspaceServer.start();
    workerManager.start();

    // Register MCP provider early so tools appear in the picker immediately.
    // detect() below populates context flags (hasDotNet, hasTypeScript, etc.)
    // that refine which servers are returned; the onDidChange event fires once
    // detection completes, prompting VS Code to re-query the full list.
    context.subscriptions.push(
        vscode.lm.registerMcpServerDefinitionProvider('AutoContextProvider', mcpServerProvider),
    );

    await workspaceContextDetector.detect();

    // The onDidChange forwarding listener (below) isn't wired yet, so the
    // change event that detect() fired during commitState() was not forwarded
    // to the MCP provider.  Notify VS Code explicitly so it re-queries with
    // the full set of detected servers.
    didChangeEmitter.fire();

    // Gate any config-dependent work on Worker.Workspace being up. Config
    // reads will move to Worker.Workspace in Phase 6b, so this wire-up is
    // in place now. Soft 30 s timeout avoids hanging activation if the
    // worker fails to signal ready.
    let workspaceReadyTimedOut = false;
    let workspaceReadyTimeout: ReturnType<typeof setTimeout> | undefined;
    await Promise.race([
        workerManager.whenWorkspaceReady().then(() => clearTimeout(workspaceReadyTimeout)),
        new Promise<void>(resolve => {
            workspaceReadyTimeout = setTimeout(() => {
                workspaceReadyTimedOut = true;
                resolve();
            }, 30_000);
        }),
    ]);
    if (workspaceReadyTimedOut) {
        outputChannel.appendLine('[WorkerManager] Timed out waiting for Worker.Workspace ready marker; continuing activation.');
    }

    await Promise.all([
        configProjector.project(),
        instructionsWriter.removeOrphanedStagingDirs(),
        configManager.removeOrphanedIds(),
    ]);

    context.subscriptions.push(
        vscode.commands.registerCommand(commandIds.EnterExportMode, () => instructionsTreeProvider.enterExportMode()),
        vscode.commands.registerCommand(commandIds.CancelExport, () => instructionsTreeProvider.cancelExportMode()),
        vscode.commands.registerCommand(commandIds.ConfirmExport, async () => {
            const entries = instructionsTreeProvider.getCheckedEntries();
            instructionsTreeProvider.cancelExportMode();
            await instructionsExporter.export(entries);
        }),
        vscode.workspace.registerTextDocumentContentProvider(instructionScheme, contentProvider),
        vscode.languages.registerCodeLensProvider({ scheme: instructionScheme }, codeLensProvider),
        // Workspace auto-configuration (instructions + tools)
        vscode.commands.registerCommand(commandIds.AutoConfigure, async () => { await new AutoConfigurer(workspaceContextDetector, instructionsCatalog, toolsCatalog, configManager).run(); }),
        // CodeLens (internal)
        vscode.commands.registerCommand(commandIds.ToggleInstruction, (fileName: string, id: string) =>
            configManager.toggleInstruction(fileName, id, instructionsCatalog.findByFileName(fileName)?.version)),
        vscode.commands.registerCommand(commandIds.ResetInstructions, (fileName: string) =>
            configManager.resetInstructions(fileName)),
        configManager.onDidChange(() =>
            void logDiagnostics().catch(err =>
                outputChannel.appendLine(`[Diagnostics] Failed to log diagnostics: ${err instanceof Error ? err.message : err}`),
            ),
        ),
        vscode.window.onDidChangeWindowState(e => {
            if (e.focused) {
                void instructionsWriter.write().catch(err =>
                    outputChannel.appendLine(`[InstructionsWriter] Failed to write on focus: ${err instanceof Error ? err.message : err}`),
                );
            }
        }),
        vscode.workspace.onDidGrantWorkspaceTrust(() => {
            void instructionsWriter.write().catch(err =>
                outputChannel.appendLine(`[InstructionsWriter] Failed to write on trust grant: ${err instanceof Error ? err.message : err}`),
            );
        }),
        configManager.onDidChange(() => didChangeEmitter.fire()),
        workspaceContextDetector.onDidChange(() => didChangeEmitter.fire()),
        vscode.commands.registerCommand(commandIds.EnableInstruction, (node) => instructionsTreeProvider.enableInstruction(node)),
        vscode.commands.registerCommand(commandIds.DisableInstruction, (node) => instructionsTreeProvider.disableInstruction(node)),
        vscode.commands.registerCommand(commandIds.DeleteOverride, InstructionsTreeProvider.deleteOverride),
        vscode.commands.registerCommand(commandIds.ShowOriginal, InstructionsTreeProvider.showOriginal),
        vscode.commands.registerCommand(commandIds.ShowChangelog, async (node: { entry: { fileName: string } }) => {
            const changelogName = node.entry.fileName.replace('.instructions.md', '.CHANGELOG.md');
            const uri = vscode.Uri.file(join(context.extensionPath, 'instructions', changelogName));
            await vscode.commands.executeCommand('markdown.showPreview', uri);
        }),
        vscode.commands.registerCommand(commandIds.ShowWhatsNew, async () => {
            const uri = vscode.Uri.file(join(context.extensionPath, 'CHANGELOG.md'));
            await vscode.commands.executeCommand('markdown.showPreview', uri);
        }),
        vscode.commands.registerCommand(commandIds.ShowNotDetected, () => setShowNotDetected(true)),
        vscode.commands.registerCommand(commandIds.HideNotDetected, () => setShowNotDetected(false)),
        vscode.commands.registerCommand(commandIds.StartMcpServer, async (node: { name: string }) => {
            const ids = mcpServerProvider.getDefinitionIds(node.name);
            for (const id of ids) {
                await vscode.commands.executeCommand('workbench.mcp.startServer', id, { autoTrustChanges: true });
            }
            if (ids.length > 0) {
                await vscode.commands.executeCommand('workbench.mcp.showOutput', ids[ids.length - 1]);
            }
        }),
        vscode.commands.registerCommand(commandIds.StopMcpServer, async (node: { name: string }) => {
            const ids = mcpServerProvider.getDefinitionIds(node.name);
            for (const id of ids) {
                await vscode.commands.executeCommand('workbench.mcp.stopServer', id);
            }
            if (ids.length > 0) {
                await vscode.commands.executeCommand('workbench.mcp.showOutput', ids[ids.length - 1]);
            }
        }),
        vscode.commands.registerCommand(commandIds.RestartMcpServer, async (node: { name: string }) => {
            const ids = mcpServerProvider.getDefinitionIds(node.name);
            for (const id of ids) {
                await vscode.commands.executeCommand('workbench.mcp.restartServer', id, { autoTrustChanges: true });
            }
            if (ids.length > 0) {
                await vscode.commands.executeCommand('workbench.mcp.showOutput', ids[ids.length - 1]);
            }
        }),
        vscode.commands.registerCommand(commandIds.ShowMcpServerOutput, async (node: { name: string }) => {
            for (const id of mcpServerProvider.getDefinitionIds(node.name)) {
                await vscode.commands.executeCommand('workbench.mcp.showOutput', id);
            }
        }),
    );

    const catalogVersions = new Map(
        instructionsCatalog.all
            .filter(e => e.version !== undefined)
            .map(e => [e.fileName, e.version!]),
    );

    const clearedFiles = await configManager.clearStaleDisabledIds(catalogVersions);

    if (clearedFiles.length > 0) {
        const names = clearedFiles.map(f => f.replace('.instructions.md', '')).join(', ');
        void vscode.window.showInformationMessage(
            `AutoContext: Disabled instructions cleared for ${names} (version updated).`,
        );
    }

    await instructionsWriter.write();

    const lastSeenVersion = context.globalState.get<string>(globalStateKeys.LastSeenVersion);
    const hasUpdate = lastSeenVersion !== undefined && lastSeenVersion !== version;

    if (hasUpdate) {
        instructionsTreeProvider.setBadge(1, 'New version available');
        instructionsTreeProvider.dismissBadgeOnNextReveal(async () => {
            await context.globalState.update(globalStateKeys.LastSeenVersion, version);
        });
    }

    if (lastSeenVersion === undefined) {
        await context.globalState.update(globalStateKeys.LastSeenVersion, version);
    }

    const hasWhatsNew = existsSync(join(context.extensionPath, 'CHANGELOG.md'));

    void vscode.commands.executeCommand('setContext', contextKeys.HasWhatsNew, hasWhatsNew);

    await logDiagnostics();

    return { mcpServerProvider, configManager, codeLensProvider, contentProvider, workspaceContextDetector, workspaceServer, healthMonitor, instructionsTreeProvider, mcpToolsTreeProvider };
}

export function deactivate(): void {
    if (subscriptions) {
        for (const d of subscriptions) {
            d.dispose();
        }
        subscriptions = undefined;
    }
}
