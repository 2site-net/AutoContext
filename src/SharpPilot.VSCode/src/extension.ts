import * as vscode from 'vscode';
import { WorkspaceContextDetector } from './workspace-context-detector.js';
import { McpToolsConfigWriter } from './mcp-tools-config-writer.js';
import { AutoConfigurer } from './auto-configurer.js';
import { InstructionsExporter } from './instructions-exporter.js';
import { InstructionsBrowser } from './instructions-browser.js';
import { SharpPilotConfigManager } from './sharppilot-config.js';
import { InstructionsContentProvider, instructionScheme } from './instructions-content-provider.js';
import { InstructionsCodeLensProvider, toggleInstructionCommandId, resetInstructionsCommandId } from './instructions-codelens-provider.js';
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

    const workspaceContextDetector = new WorkspaceContextDetector();
    const instructionsExporter = new InstructionsExporter(context.extensionPath);
    const instructionsBrowser = new InstructionsBrowser();
    const configManager = new SharpPilotConfigManager(context.extensionPath, version);
    const toolsStatusWriter = new McpToolsConfigWriter(configManager);
    const contentProvider = new InstructionsContentProvider(context.extensionPath, configManager);
    const codeLensProvider = new InstructionsCodeLensProvider(context.extensionPath, configManager, workspaceContextDetector);
    const decorationManager = new InstructionsDecorationManager(context.extensionPath, configManager);
    const instructionsWriter = new InstructionsConfigWriter(context.extensionPath, configManager);
    const outputChannel = vscode.window.createOutputChannel('SharpPilot');
    const workspaceServer = new WorkspaceServerManager(context.extensionPath, outputChannel, vscode.workspace.workspaceFolders?.[0]?.uri.fsPath);
    const mcpServerProvider = new McpServerProvider(context.extensionPath, version, workspaceContextDetector, didChangeEmitter.event, workspaceServer);
    const instructionsTreeProvider = new InstructionsTreeProvider(workspaceContextDetector);
    const mcpToolsTreeProvider = new McpToolsTreeProvider(workspaceContextDetector);

    const showNotDetectedKey = 'sharppilot.showNotDetected';
    const showNotDetected = context.globalState.get<boolean>(showNotDetectedKey, true);
    instructionsTreeProvider.showNotDetected = showNotDetected;
    mcpToolsTreeProvider.showNotDetected = showNotDetected;
    void vscode.commands.executeCommand('setContext', showNotDetectedKey, showNotDetected);

    const setShowNotDetected = async (value: boolean) => {
        await context.globalState.update(showNotDetectedKey, value);
        instructionsTreeProvider.showNotDetected = value;
        mcpToolsTreeProvider.showNotDetected = value;
        void vscode.commands.executeCommand('setContext', showNotDetectedKey, value);
    };

    const logDiagnostics = () => InstructionsDiagnostics.log(outputChannel, context.extensionPath, configManager);

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
        vscode.commands.registerCommand(InstructionsTreeProvider.enterExportCommandId, () => instructionsTreeProvider.enterExportMode()),
        vscode.commands.registerCommand(InstructionsTreeProvider.cancelExportCommandId, () => instructionsTreeProvider.cancelExportMode()),
        vscode.commands.registerCommand(InstructionsTreeProvider.confirmExportCommandId, async () => {
            const entries = instructionsTreeProvider.getCheckedEntries();
            instructionsTreeProvider.cancelExportMode();
            await instructionsExporter.exportEntries(entries);
        }),
        vscode.workspace.registerTextDocumentContentProvider(instructionScheme, contentProvider),
        vscode.languages.registerCodeLensProvider({ scheme: instructionScheme }, codeLensProvider),
        vscode.commands.registerCommand('sharppilot.exportInstructions', () => instructionsExporter.export()),
        vscode.commands.registerCommand('sharppilot.browseInstructions', () => instructionsBrowser.browse()),
        // Workspace auto-configuration (instructions + tools)
        vscode.commands.registerCommand('sharppilot.autoConfigure', async () => { await AutoConfigurer.configure(workspaceContextDetector); }),
        // CodeLens (internal)
        vscode.commands.registerCommand(toggleInstructionCommandId, (fileName: string, id: string) => {
            configManager.toggleInstruction(fileName, id);
        }),
        vscode.commands.registerCommand(resetInstructionsCommandId, (fileName: string) => {
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
        vscode.commands.registerCommand(InstructionsTreeProvider.enableCommandId, InstructionsTreeProvider.enableInstruction),
        vscode.commands.registerCommand(InstructionsTreeProvider.disableCommandId, InstructionsTreeProvider.disableInstruction),
        vscode.commands.registerCommand(InstructionsTreeProvider.deleteOverrideCommandId, InstructionsTreeProvider.deleteOverride),
        vscode.commands.registerCommand(InstructionsTreeProvider.showOriginalCommandId, InstructionsTreeProvider.showOriginal),
        vscode.commands.registerCommand('sharppilot.showNotDetected', () => setShowNotDetected(true)),
        vscode.commands.registerCommand('sharppilot.hideNotDetected', () => setShowNotDetected(false)),
        vscode.lm.registerMcpServerDefinitionProvider('sharpPilotProvider', mcpServerProvider),
    );

    return { mcpServerProvider, configManager, codeLensProvider, contentProvider, workspaceContextDetector, workspaceServer, instructionsTreeProvider, mcpToolsTreeProvider };
}

export function deactivate(): void {}
