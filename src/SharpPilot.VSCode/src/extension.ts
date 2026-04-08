import * as vscode from 'vscode';
import { WorkspaceContextDetector } from './workspace-context-detector.js';
import { McpToolsConfigWriter } from './mcp-tools-config-writer.js';
import { McpToolsCatalog } from './mcp-tools-catalog.js';
import { InstructionsCatalog } from './instructions-catalog.js';
import { McpServersCatalog } from './mcp-servers-catalog.js';
import { mcpToolEntries, instructionEntries, mcpServerEntries, commandIds } from './ui-constants.js';
import { AutoConfigurer } from './auto-configurer.js';
import { InstructionsExporter } from './instructions-exporter.js';
import { SharpPilotConfigManager } from './sharppilot-config.js';
import { InstructionsContentProvider, instructionScheme } from './instructions-content-provider.js';
import { InstructionsCodeLensProvider } from './instructions-codelens-provider.js';
import { InstructionsDecorationManager } from './instructions-decoration-manager.js';
import { InstructionsConfigWriter } from './instructions-config-writer.js';
import { InstructionsDiagnostics } from './instructions-diagnostics.js';
import { InstructionsTreeProvider } from './instructions-tree-provider.js';
import { McpToolsTreeProvider } from './mcp-tools-tree-provider.js';
import { McpServerProvider } from './mcp-server-provider.js';
import { WorkspaceServerManager } from './workspace-server-manager.js';

export function activate(context: vscode.ExtensionContext) {
    if (!vscode.workspace.workspaceFolders?.length) {
        return;
    }

    const version = context.extension.packageJSON.version as string;
    const didChangeEmitter = new vscode.EventEmitter<void>();

    const toolsCatalog = new McpToolsCatalog(mcpToolEntries);
    const instructionsCatalog = new InstructionsCatalog(instructionEntries);
    const serversCatalog = new McpServersCatalog(mcpServerEntries);
    const workspaceContextDetector = new WorkspaceContextDetector(instructionsCatalog, serversCatalog);
    const instructionsExporter = new InstructionsExporter(context.extensionPath);
    const configManager = new SharpPilotConfigManager(context.extensionPath, version);
    const toolsStatusWriter = new McpToolsConfigWriter(configManager, toolsCatalog);
    const contentProvider = new InstructionsContentProvider(context.extensionPath, configManager);
    const codeLensProvider = new InstructionsCodeLensProvider(context.extensionPath, configManager, workspaceContextDetector, instructionsCatalog);
    const decorationManager = new InstructionsDecorationManager(context.extensionPath, configManager);
    const instructionsWriter = new InstructionsConfigWriter(context.extensionPath, configManager, instructionsCatalog);
    const outputChannel = vscode.window.createOutputChannel('SharpPilot');
    const workspaceServer = new WorkspaceServerManager(context.extensionPath, outputChannel, vscode.workspace.workspaceFolders?.[0]?.uri.fsPath);
    const mcpServerProvider = new McpServerProvider(context.extensionPath, version, workspaceContextDetector, didChangeEmitter.event, workspaceServer, toolsCatalog, serversCatalog);
    const instructionsTreeProvider = new InstructionsTreeProvider(workspaceContextDetector, instructionsCatalog);
    const mcpToolsTreeProvider = new McpToolsTreeProvider(workspaceContextDetector, toolsCatalog);

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

    workspaceServer.start();
    toolsStatusWriter.write();
    workspaceContextDetector.detect();
    configManager.removeOrphanedIds();
    instructionsWriter.removeOrphanedStagingDirs();
    instructionsWriter.write();
    logDiagnostics();

    context.subscriptions.push(
        didChangeEmitter,
        outputChannel,
        workspaceServer,
        workspaceContextDetector,
        configManager,
        contentProvider,
        codeLensProvider,
        decorationManager,
        instructionsWriter,
        instructionsTreeProvider,
        mcpToolsTreeProvider,
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
        vscode.commands.registerCommand(commandIds.AutoConfigure, async () => { await AutoConfigurer.configure(workspaceContextDetector, instructionsCatalog, toolsCatalog); }),
        // CodeLens (internal)
        vscode.commands.registerCommand(commandIds.ToggleInstruction, (fileName: string, id: string) => {
            configManager.toggleInstruction(fileName, id);
        }),
        vscode.commands.registerCommand(commandIds.ResetInstructions, (fileName: string) => {
            configManager.resetInstructions(fileName);
        }),
        configManager.onDidChange(() => logDiagnostics()),
        vscode.window.onDidChangeWindowState(e => {
            if (e.focused) {
                instructionsWriter.write();
            }
        }),
        vscode.workspace.onDidGrantWorkspaceTrust(() => {
            instructionsWriter.write();
        }),
        vscode.workspace.onDidChangeConfiguration(e => {
            if (e.affectsConfiguration('sharppilot.tools')) {
                toolsStatusWriter.write();
                didChangeEmitter.fire();
            }
        }),
        workspaceContextDetector.onDidChange(() => didChangeEmitter.fire()),
        vscode.commands.registerCommand(commandIds.EnableInstruction, InstructionsTreeProvider.enableInstruction),
        vscode.commands.registerCommand(commandIds.DisableInstruction, InstructionsTreeProvider.disableInstruction),
        vscode.commands.registerCommand(commandIds.DeleteOverride, InstructionsTreeProvider.deleteOverride),
        vscode.commands.registerCommand(commandIds.ShowOriginal, InstructionsTreeProvider.showOriginal),
        vscode.commands.registerCommand(commandIds.ShowNotDetected, () => setShowNotDetected(true)),
        vscode.commands.registerCommand(commandIds.HideNotDetected, () => setShowNotDetected(false)),
        vscode.lm.registerMcpServerDefinitionProvider('sharpPilotProvider', mcpServerProvider),
    );

    return { mcpServerProvider, configManager, codeLensProvider, contentProvider, workspaceContextDetector, workspaceServer, instructionsTreeProvider, mcpToolsTreeProvider };
}

export function deactivate(): void {}
