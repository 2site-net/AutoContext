import * as vscode from 'vscode';
import { join } from 'node:path';
import { existsSync } from 'node:fs';
import { WorkspaceContextDetector } from './workspace-context-detector.js';
import { InstructionsFilesManifestLoader } from './instructions-files-manifest-loader.js';
import { commandIds, contextKeys, globalStateKeys, EXTENSION_NAME } from './ui-constants.js';
import { AutoConfigurer } from './auto-configurer.js';
import { InstructionsFilesExporter } from './instructions-files-exporter.js';
import { AutoContextConfigManager } from './autocontext-config.js';
import { InstructionsViewerDocumentProvider, instructionScheme } from './instructions-viewer-document-provider.js';
import { InstructionsViewerCodeLensProvider } from './instructions-viewer-codelens-provider.js';
import { InstructionsViewerDecorationManager } from './instructions-viewer-decoration-manager.js';
import { InstructionsFilesManager } from './instructions-files-manager.js';
import { InstructionsFilesDiagnosticsReporter } from './instructions-files-diagnostics-reporter.js';
import { InstructionsFilesDiagnosticsRunner } from './instructions-files-diagnostics-runner.js';
import { ConfigContextProjector } from './config-context-projector.js';
import { InstructionsFilesTreeProvider } from './instructions-files-tree-provider.js';
import { MetadataLoader } from './metadata-loader.js';
import { McpToolsManifestLoader } from './mcp-tools-manifest-loader.js';
import { McpToolsTreeProvider } from './mcp-tools-tree-provider.js';
import { TreeViewStateResolver } from './tree-view-state-resolver.js';
import { TreeViewTooltip } from './tree-view-tooltip.js';
import { McpServerProvider } from './mcp-server-provider.js';
import { WorkerManager } from './worker-manager.js';
import { ServersManifestLoader } from './servers-manifest-loader.js';
import { HealthMonitorServer } from './health-monitor-server.js';
import { LogServer } from './log-server.js';
import { OutputChannelLogger } from './output-channel-logger.js';
import { LogCategory } from '#types/logger.js';

let subscriptions: vscode.Disposable[] | undefined;

export async function activate(context: vscode.ExtensionContext) {
    if (!vscode.workspace.workspaceFolders?.length) {
        return;
    }

    subscriptions = context.subscriptions;

    const version = context.extension.packageJSON.version as string;
    const didChangeEmitter = new vscode.EventEmitter<void>();

    const metadataLoader = new MetadataLoader(context.extensionPath);
    const mcpToolsManifest = new McpToolsManifestLoader(context.extensionPath).load();
    const instructionsMetadata = metadataLoader.getInstructionsInfo();

    const instructionsManifest = new InstructionsFilesManifestLoader(context.extensionPath).load(instructionsMetadata);
    const rootLogger = OutputChannelLogger.create(EXTENSION_NAME);
    const activationLogger = rootLogger.forCategory(LogCategory.Activation);
    activationLogger.info(`Activating AutoContext v${version}`);
    const instructionsExporter = new InstructionsFilesExporter(context.extensionPath, rootLogger.forCategory(LogCategory.Instructions));
    const workspaceContextDetector = new WorkspaceContextDetector(instructionsManifest, rootLogger.forCategory(LogCategory.Detection));
    const configManager = new AutoContextConfigManager(context.extensionPath, version, rootLogger.forCategory(LogCategory.Config));
    const contentProvider = new InstructionsViewerDocumentProvider(context.extensionPath, configManager, rootLogger.forCategory(LogCategory.Instructions));
    const codeLensProvider = new InstructionsViewerCodeLensProvider(context.extensionPath, configManager, workspaceContextDetector, instructionsManifest, rootLogger.forCategory(LogCategory.Instructions));
    const decorationManager = new InstructionsViewerDecorationManager(context.extensionPath, configManager, rootLogger.forCategory(LogCategory.Decorations));
    const instructionsWriter = new InstructionsFilesManager(context.extensionPath, configManager, instructionsManifest, rootLogger.forCategory(LogCategory.InstructionsWriter));
    const configProjector = new ConfigContextProjector(configManager, instructionsManifest, mcpToolsManifest, rootLogger.forCategory(LogCategory.ConfigProjector));
    const serversManifest = new ServersManifestLoader(context.extensionPath).load();
    const workerIds = new Set(
        mcpToolsManifest.topCategories
            .map(c => c.workerId)
            .filter((id): id is string => id !== undefined),
    );
    const workerEntries = serversManifest.servers.filter(s => workerIds.has(s.id));
    const logServer = new LogServer(rootLogger.forCategory(LogCategory.LogServer));
    logServer.start();
    context.subscriptions.push(logServer);
    const healthMonitor = new HealthMonitorServer(rootLogger.forCategory(LogCategory.HealthMonitor));
    const workerManager = new WorkerManager(context.extensionPath, rootLogger.forCategory(LogCategory.WorkerManager), vscode.workspace.workspaceFolders?.[0]?.uri.fsPath, workerEntries, logServer.getPipeName(), healthMonitor.getPipeName());
    const mcpServerProvider = new McpServerProvider(context.extensionPath, version, didChangeEmitter.event, mcpToolsManifest, workerManager, serversManifest, configManager, logServer.getPipeName(), healthMonitor.getPipeName(), rootLogger.forCategory(LogCategory.McpServerProvider));
    const stateResolver = new TreeViewStateResolver(workspaceContextDetector);

    // Pre-read the config so tree providers get the real config on first render.
    await configManager.read();

    const instructionsTreeProvider = new InstructionsFilesTreeProvider(workspaceContextDetector, instructionsManifest, stateResolver, new TreeViewTooltip('instructions'), configManager, rootLogger.forCategory(LogCategory.InstructionsTree));
    const mcpToolsTreeProvider = new McpToolsTreeProvider(workspaceContextDetector, mcpToolsManifest, stateResolver, new TreeViewTooltip('tools'), configManager, rootLogger.forCategory(LogCategory.McpToolsTree), healthMonitor, mcpServerProvider);

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

    const diagnosticsLogger = rootLogger.forCategory(LogCategory.Diagnostics);
    const writerLogger = rootLogger.forCategory(LogCategory.InstructionsWriter);
    const diagnosticsRunner = new InstructionsFilesDiagnosticsRunner(context.extensionPath, configManager, instructionsManifest);
    const diagnosticsReporter = new InstructionsFilesDiagnosticsReporter(diagnosticsRunner, rootLogger);
    const logDiagnostics = () => diagnosticsReporter.report();

    context.subscriptions.push(
        didChangeEmitter,
        rootLogger,
        healthMonitor,
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
    workerManager.start();

    activationLogger.debug('Health monitor and worker manager started');

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
        workerManager.whenWorkspaceReady()
            .then(() => clearTimeout(workspaceReadyTimeout))
            // If the manager is disposed before the worker signals ready,
            // whenWorkspaceReady() rejects. The activation flow has already
            // moved on (or will, via the timeout branch); just absorb it.
            .catch(() => clearTimeout(workspaceReadyTimeout)),
        new Promise<void>(resolve => {
            workspaceReadyTimeout = setTimeout(() => {
                workspaceReadyTimedOut = true;
                resolve();
            }, 30_000);
        }),
    ]);
    if (workspaceReadyTimedOut) {
        rootLogger.forCategory(LogCategory.Activation).warn('Timed out waiting for Worker.Workspace ready marker; continuing activation.');
    } else {
        activationLogger.debug('Worker.Workspace ready');
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
        vscode.commands.registerCommand(commandIds.AutoConfigure, async () => { await new AutoConfigurer(workspaceContextDetector, instructionsManifest, mcpToolsManifest, configManager).run(); }),
        // CodeLens (internal)
        vscode.commands.registerCommand(commandIds.ToggleInstruction, (fileName: string, id: string) =>
            configManager.toggleInstruction(fileName, id, instructionsManifest.findByName(fileName)?.version)),
        vscode.commands.registerCommand(commandIds.ResetInstructions, (fileName: string) =>
            configManager.resetInstructions(fileName)),
        configManager.onDidChange(() =>
            void logDiagnostics().catch(err =>
                diagnosticsLogger.error('Failed to log diagnostics', err),
            ),
        ),
        vscode.window.onDidChangeWindowState(e => {
            if (e.focused) {
                void instructionsWriter.write().catch(err =>
                    writerLogger.error('Failed to write on focus', err),
                );
            }
        }),
        vscode.workspace.onDidGrantWorkspaceTrust(() => {
            void instructionsWriter.write().catch(err =>
                writerLogger.error('Failed to write on trust grant', err),
            );
        }),
        configManager.onDidChange(() => didChangeEmitter.fire()),
        vscode.commands.registerCommand(commandIds.EnableInstruction, (node) => instructionsTreeProvider.enableInstruction(node)),
        vscode.commands.registerCommand(commandIds.DisableInstruction, (node) => instructionsTreeProvider.disableInstruction(node)),
        vscode.commands.registerCommand(commandIds.DeleteOverride, InstructionsFilesTreeProvider.deleteOverride),
        vscode.commands.registerCommand(commandIds.ShowOriginal, InstructionsFilesTreeProvider.showOriginal),
        vscode.commands.registerCommand(commandIds.ShowChangelog, async (node: { entry: { name: string } }) => {
            const changelogName = node.entry.name.replace('.instructions.md', '.CHANGELOG.md');
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
        instructionsManifest.instructions
            .filter(e => e.version !== undefined)
            .map(e => [e.name, e.version!]),
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

    activationLogger.info('Activation complete');

    return { mcpServerProvider, configManager, codeLensProvider, contentProvider, workspaceContextDetector, workerManager, healthMonitor, instructionsTreeProvider, mcpToolsTreeProvider };
}

export function deactivate(): void {
    if (subscriptions) {
        for (const d of subscriptions) {
            d.dispose();
        }
        subscriptions = undefined;
    }
}
